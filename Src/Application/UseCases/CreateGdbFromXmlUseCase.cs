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
                
                // Si la GDB ya existe, eliminarla para sobrescribir
                if (Directory.Exists(gdbPath))
                {
                    try
                    {
                        Directory.Delete(gdbPath, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        return (false, string.Empty, $"No se pudo eliminar la GDB existente: {ex.Message}");
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
                
                return (true, gdbPath, "GDB 'migracion' creada y esquema XML importado exitosamente.");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Excepción: {ex.Message}");
            }
        }
    }
}