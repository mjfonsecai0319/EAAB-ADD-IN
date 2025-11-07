using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using EAABAddIn.Src.Application.Services;

namespace EAABAddIn.Src.Application.UseCases.Validation
{
    public class ValidateDatasetsUseCase
    {
        public async Task<ValidationResult> Invoke(ValidationRequest request)
        {
            var result = new ValidationResult();
            try
            {
                var csv = new CsvReportService();
                var reportsFolder = csv.EnsureReportsFolder(string.IsNullOrWhiteSpace(request.OutputFolder)
                    ? Path.GetTempPath()
                    : request.OutputFolder);
                result.ReportFolder = reportsFolder;

                // Ejecutar en MCT cuando abramos datasets
                await QueuedTask.Run(() =>
                {
                    foreach (var ds in request.Datasets ?? Enumerable.Empty<DatasetInput>())
                    {
                        if (string.IsNullOrWhiteSpace(ds.Path))
                            continue; // sin entrada, no valida

                        var rows = new List<string[]>();
                        // Validación mínima: se puede abrir la FC
                        var (opened, openMsg) = TryOpenFeatureClass(ds.Path, out var fc);
                        if (!opened)
                        {
                            rows.Add(new[] { "ERROR", "No se pudo abrir el dataset", openMsg });
                            result.TotalWarnings += 1;
                        }
                        else
                        {
                            // Placeholder de reglas: aquí irán las validaciones de dominio/omisión/comisión.
                            // Por ahora, confirmar que tiene al menos 1 campo OID.
                            try
                            {
                                var def = fc.GetDefinition();
                                if (string.IsNullOrWhiteSpace(def?.GetObjectIDField()))
                                {
                                    rows.Add(new[] { "ADVERTENCIA", "No tiene campo OID", "Se esperaría un OID" });
                                    result.TotalWarnings += 1;
                                }
                            }
                            catch (Exception ex)
                            {
                                rows.Add(new[] { "ERROR", "Excepción validando definición", ex.Message });
                                result.TotalWarnings += 1;
                            }
                            finally
                            {
                                fc?.Dispose();
                            }
                        }

                        // Escribe CSV aun si no hay filas, dejando registro del dataset
                        if (rows.Count == 0)
                            rows.Add(new[] { "OK", "Sin advertencias", string.Empty });
                        var file = csv.WriteReport(reportsFolder, ds.Name, rows);
                        result.ReportFiles.Add(file);
                    }
                });
            }
            catch (Exception ex)
            {
                // En caso de fallo general, dejamos un CSV de error genérico
                try
                {
                    var csv = new CsvReportService();
                    var folder = csv.EnsureReportsFolder(Path.GetTempPath());
                    csv.WriteReport(folder, "ERROR_GENERAL", new[] { new[] { "ERROR", "Excepción en validación", ex.Message } });
                    result.ReportFolder = folder;
                    result.TotalWarnings += 1;
                }
                catch { /* ignored */ }
            }

            return result;
        }

        private static (bool ok, string message) TryOpenFeatureClass(string path, out FeatureClass featureClass)
        {
            featureClass = null;
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return (false, "Ruta vacía");

                path = Path.GetFullPath(path);

                // Soporte SHP directo
                if (path.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                {
                    var folder = Path.GetDirectoryName(path);
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(name))
                        return (false, "Ruta SHP inválida");
                    var fsConn = new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile);
                    using var fs = new FileSystemDatastore(fsConn);
                    featureClass = fs.OpenDataset<FeatureClass>(name);
                    return (featureClass != null, featureClass == null ? "No se pudo abrir SHP" : "");
                }

                // Soporta FGDB y ruta de dataset dentro de la GDB: C:\foo\bar.gdb\FC o C:\foo\bar.gdb\FD\FC
                var idx = path.LastIndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    return (false, "Ruta no parece una feature class de FGDB (se espera .gdb)");
                }
                var gdbPath = path.Substring(0, idx + 4);
                var dsPath = path.Length > idx + 4 ? path.Substring(idx + 5).TrimStart('\\', '/') : string.Empty;
                if (string.IsNullOrEmpty(dsPath))
                    return (false, "No se especificó nombre de FeatureClass dentro de la GDB");

                var conn = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
                using var gdb = new Geodatabase(conn);

                // 1) Intento directo (funciona si está en la raíz o ArcGIS admite FD\FC)
                try
                {
                    featureClass = gdb.OpenDataset<FeatureClass>(dsPath);
                    if (featureClass != null)
                        return (true, "");
                }
                catch { }

                // 2) Si trae separadores, intentar abrir FD y luego FC
                var parts = dsPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var fdName = parts[0];
                    var fcName = parts[parts.Length - 1];
                    try
                    {
                        using var fd = gdb.OpenDataset<FeatureDataset>(fdName);
                        var fcFromFd = fd.OpenDataset<FeatureClass>(fcName);
                        if (fcFromFd != null)
                        {
                            featureClass = fcFromFd;
                            return (true, "");
                        }
                    }
                    catch { }
                }

                // 3) Escanear todos los FeatureDatasets por si el usuario omitió el FD correcto
                try
                {
                    var fdDefs = gdb.GetDefinitions<FeatureDatasetDefinition>();
                    foreach (var fdDef in fdDefs)
                    {
                        try
                        {
                            using var fd = gdb.OpenDataset<FeatureDataset>(fdDef.GetName());
                            var fc = fd.OpenDataset<FeatureClass>(dsPath); // probar nombre completo como último segmento
                            if (fc != null)
                            {
                                featureClass = fc;
                                return (true, "");
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                return (false, "No se pudo abrir la FeatureClass (verifique FD/FC)");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
