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

                await QueuedTask.Run(() =>
                {
                    foreach (var ds in request.Datasets ?? Enumerable.Empty<DatasetInput>())
                    {
                        if (string.IsNullOrWhiteSpace(ds.Path))
                            continue; 

                        var rows = new List<string[]>();
                        var (opened, openMsg) = TryOpenFeatureClass(ds.Path, out var fc);
                        if (!opened)
                        {
                            rows.Add(new[] { "ERROR", "No se pudo abrir el dataset", openMsg });
                            result.TotalWarnings += 1;
                        }
                        else
                        {
                            try
                            {
                                var def = fc.GetDefinition();
                                
                                if (string.IsNullOrWhiteSpace(def?.GetObjectIDField()))
                                {
                                    rows.Add(new[] { "ERROR", "No tiene campo OID", "Se requiere un campo ObjectID" });
                                    result.TotalWarnings += 1;
                                }
                                
                                var fieldNames = def.GetFields().Select(f => f.Name.ToUpper()).ToList();
                                var requiredFields = GetRequiredFieldsForDataset(ds.Name);
                                
                                foreach (var reqField in requiredFields)
                                {
                                    if (!fieldNames.Contains(reqField.ToUpper()))
                                    {
                                        rows.Add(new[] { "ADVERTENCIA", $"Campo obligatorio faltante: {reqField}", $"El dataset {ds.Name} debería tener el campo {reqField}" });
                                        result.TotalWarnings += 1;
                                    }
                                }
                                
                                long recordCount = fc.GetCount();
                                if (recordCount == 0)
                                {
                                    rows.Add(new[] { "ADVERTENCIA", "Dataset vacío", "No contiene registros para migrar" });
                                    result.TotalWarnings += 1;
                                }
                                
                                if (recordCount > 0)
                                {
                                    var criticalFields = GetCriticalFieldsForDataset(ds.Name);
                                    var nullCounts = new Dictionary<string, int>();
                                    
                                    using var cursor = fc.Search(null, false);
                                    int sampleSize = 0;
                                    while (cursor.MoveNext() && sampleSize < 100)
                                    {
                                        using var feature = cursor.Current as Feature;
                                        if (feature != null)
                                        {
                                            foreach (var field in criticalFields)
                                            {
                                                var idx = feature.FindField(field);
                                                if (idx >= 0)
                                                {
                                                    var val = feature[idx];
                                                    if (val == null || val is DBNull || (val is string s && string.IsNullOrWhiteSpace(s)))
                                                    {
                                                        if (!nullCounts.ContainsKey(field))
                                                            nullCounts[field] = 0;
                                                        nullCounts[field]++;
                                                    }
                                                }
                                            }
                                            sampleSize++;
                                        }
                                    }
                                    
                                    foreach (var kvp in nullCounts)
                                    {
                                        if (kvp.Value > 0)
                                        {
                                            rows.Add(new[] { "ADVERTENCIA", $"Campo '{kvp.Key}' con valores vacíos", $"{kvp.Value} de {sampleSize} registros muestreados tienen el campo '{kvp.Key}' vacío" });
                                            result.TotalWarnings += 1;
                                        }
                                    }
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

                        if (rows.Count == 0)
                            rows.Add(new[] { "OK", "Sin advertencias", string.Empty });
                        var file = csv.WriteReport(reportsFolder, ds.Name, rows);
                        result.ReportFiles.Add(file);
                    }
                });
            }
            catch (Exception ex)
            {
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

        private static List<string> GetRequiredFieldsForDataset(string datasetName)
        {

            return datasetName?.ToUpper() switch
            {
                "L_ACU_ORIGEN" => new List<string> { "CLASE", "SUBTIPO", "DIAMETRO", "MATERIAL", "FECHAINST" },
                "P_ACU_ORIGEN" => new List<string> { "CLASE", "SUBTIPO", "FECHAINST" },
                "L_ALC_ORIGEN" => new List<string> { "CLASE", "SUBTIPO", "SISTEMA", "DIAMETRO", "MATERIAL", "FECHAINST" },
                "P_ALC_ORIGEN" => new List<string> { "CLASE", "SUBTIPO", "SISTEMA", "FECHADATO" },
                "L_ALC_PLUV_ORIGEN" => new List<string> { "CLASE", "SUBTIPO", "SISTEMA", "DIAMETRO", "MATERIAL", "FECHAINST" },
                "P_ALC_PLUV_ORIGEN" => new List<string> { "CLASE", "SUBTIPO", "SISTEMA", "FECHADATO" },
                _ => new List<string>()
            };
        }

        private static List<string> GetCriticalFieldsForDataset(string datasetName)
        {
            return datasetName?.ToUpper() switch
            {
                "L_ACU_ORIGEN" => new List<string> { "N_INICIAL", "N_FINAL", "CONTRATO_I", "NDISENO" },
                "P_ACU_ORIGEN" => new List<string> { "IDENTIFIC", "CONTRATO_I" },
                "L_ALC_ORIGEN" => new List<string> { "N_INICIAL", "N_FINAL", "CONTRATO_ID" },
                "P_ALC_ORIGEN" => new List<string> { "IDENTIFIC", "CONTRATO_ID" },
                "L_ALC_PLUV_ORIGEN" => new List<string> { "N_INICIAL", "N_FINAL", "CONTRATO_ID" },
                "P_ALC_PLUV_ORIGEN" => new List<string> { "IDENTIFIC", "CONTRATO_ID" },
                _ => new List<string>()
            };
        }

        private static (bool ok, string message) TryOpenFeatureClass(string path, out FeatureClass featureClass)
        {
            featureClass = null;
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return (false, "Ruta vacía");

                path = Path.GetFullPath(path);

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

   
                try
                {
                    featureClass = gdb.OpenDataset<FeatureClass>(dsPath);
                    if (featureClass != null)
                        return (true, "");
                }
                catch { }


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


                try
                {
                    var fdDefs = gdb.GetDefinitions<FeatureDatasetDefinition>();
                    foreach (var fdDef in fdDefs)
                    {
                        try
                        {
                            using var fd = gdb.OpenDataset<FeatureDataset>(fdDef.GetName());
                            var fc = fd.OpenDataset<FeatureClass>(dsPath); 
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
