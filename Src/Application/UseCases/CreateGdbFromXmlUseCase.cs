#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Core;

namespace EAABAddIn.Src.Application.UseCases
{
    public class CreateGdbFromXmlUseCase
    {
        // Crea o sobrescribe una FGDB llamada "migracion.gdb" en outFolder y carga el esquema desde xmlPath
        // Retorna: ok, ruta completa de la GDB, mensaje
        public async Task<(bool ok, string gdbPath, string message)> Invoke(string outFolder, string xmlPath)
        {
            if (string.IsNullOrWhiteSpace(outFolder) || string.IsNullOrWhiteSpace(xmlPath))
                return (false, string.Empty, "Invalid input parameters.");

            if (!File.Exists(xmlPath))
                return (false, string.Empty, "XML schema file not found.");

            try
            {
                // Validate and prepare paths
                if (!Directory.Exists(outFolder))
                    Directory.CreateDirectory(outFolder);

                const string gdbName = "GDB_Cargue";
                var gdbPath = Path.Combine(outFolder, $"{gdbName}.gdb");
                
                // Si la GDB ya existe, intentar limpiar de forma segura (manejo de locks)
                if (Directory.Exists(gdbPath))
                {
                    // Forzar recolección de objetos que puedan retener handles
                    try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }

                    bool deleted = false;
                    Exception? lastEx = null;

                    // Reintentos controlados de borrado (p. ej., si hay .lock)
                    for (int attempt = 1; attempt <= 3 && !deleted; attempt++)
                    {
                        try
                        {
                            Directory.Delete(gdbPath, recursive: true);
                            deleted = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            lastEx = ex;
                            await Task.Delay(attempt * 250);
                        }
                    }

                    if (!deleted)
                    {
                        // Intentar renombrar como fallback para liberar el nombre
                        try
                        {
                            var backupName = $"{gdbName}_old_{DateTime.Now:yyyyMMdd_HHmmss}.gdb";
                            var backupPath = Path.Combine(outFolder, backupName);
                            Directory.Move(gdbPath, backupPath);
                            // Intentar borrar el backup en background (best effort)
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await Task.Delay(1000);
                                    Directory.Delete(backupPath, true);
                                }
                                catch { }
                            });
                        }
                        catch (Exception moveEx)
                        {
                            // Último recurso: crear con nombre único para no bloquear el flujo
                            var uniqueName = $"{gdbName}_{DateTime.Now:yyyyMMdd_HHmmss}";
                            var uniqueGdbPath = Path.Combine(outFolder, uniqueName + ".gdb");
                            var createParamsUnique = Geoprocessing.MakeValueArray(outFolder, uniqueName);
                            var createResultUnique = await Geoprocessing.ExecuteToolAsync("management.CreateFileGDB", createParamsUnique);
                            if (createResultUnique.IsFailed)
                                return (false, string.Empty, $"La GDB existente está bloqueada y no se pudo crear una nueva: {(lastEx ?? moveEx).Message}");

                            // Importar esquema al nombre único
                            var importParamsUnique = Geoprocessing.MakeValueArray(
                                uniqueGdbPath,
                                xmlPath,
                                "SCHEMA_ONLY"
                            );
                            var importResultUnique = await Geoprocessing.ExecuteToolAsync("management.ImportXMLWorkspaceDocument", importParamsUnique);
                            if (importResultUnique.IsFailed)
                            {
                                var errorMessagesU = string.Join("; ", importResultUnique.Messages.Select(m => m.Text));
                                return (false, uniqueGdbPath, $"Creada GDB única, pero falló la importación del XML: {errorMessagesU}");
                            }

                            return (true, uniqueGdbPath, $"GDB existente bloqueada (.lock). Se creó y usará '{uniqueName}.gdb'.");
                        }
                    }
                }

                // Step 1: Create empty File Geodatabase
                var createParams = Geoprocessing.MakeValueArray(outFolder, gdbName);
                var createResult = await Geoprocessing.ExecuteToolAsync("management.CreateFileGDB", createParams);
                
                if (createResult.IsFailed)
                    return (false, string.Empty, $"Error al crear la GDB: {createResult.Messages}");

                // Step 2: Import XML schema into GDB
                // Parámetros de ImportXMLWorkspaceDocument:
                // 1. target_geodatabase - ruta a la GDB
                // 2. import_type - "DATA" (datos), "SCHEMA_ONLY" (solo esquema), o "DATA_AND_SCHEMA" (ambos)
                // 3. import_file - ruta al archivo XML
                var importParams = Geoprocessing.MakeValueArray(
                    gdbPath,                    // target_geodatabase
                    xmlPath,                    // import_file
                    "SCHEMA_ONLY"               // import_type - solo esquema, sin datos
                );
                var importResult = await Geoprocessing.ExecuteToolAsync("management.ImportXMLWorkspaceDocument", importParams);

                if (importResult.IsFailed)
                {
                    // Obtener los mensajes detallados del error
                    var errorMessages = string.Join("; ", importResult.Messages.Select(m => m.Text));
                    return (false, gdbPath, $"Error al importar esquema XML: {errorMessages}");
                }
                
                return (true, gdbPath, "GDB 'GDB_Cargue' creada y esquema XML importado exitosamente.");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Excepción: {ex.Message}");
            }
        }
    }
}