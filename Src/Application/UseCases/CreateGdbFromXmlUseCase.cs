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
        // Crea una FGDB en outFolder con nombre gdbName (sin .gdb) y carga el esquema desde xmlPath
        // Retorna: ok, ruta completa de la GDB, mensaje
        public async Task<(bool ok, string gdbPath, string message)> Invoke(string outFolder, string gdbName, string xmlPath)
        {
            if (string.IsNullOrWhiteSpace(outFolder) || string.IsNullOrWhiteSpace(gdbName) || string.IsNullOrWhiteSpace(xmlPath))
                return (false, string.Empty, "Invalid input parameters.");

            if (!File.Exists(xmlPath))
                return (false, string.Empty, "XML schema file not found.");

            try
            {
                // Validate and prepare paths
                if (!Directory.Exists(outFolder))
                    Directory.CreateDirectory(outFolder);

                var gdbPath = Path.Combine(outFolder, $"{gdbName}.gdb");
                if (Directory.Exists(gdbPath))
                    return (false, string.Empty, "GDB already exists.");

                // Step 1: Create empty File Geodatabase
                var createParams = Geoprocessing.MakeValueArray(outFolder, gdbName);
                var createResult = await Geoprocessing.ExecuteToolAsync("management.CreateFileGDB", createParams);
                if (!createResult.IsFailed)
                {
                    // Step 2: Import XML schema into GDB
                    var importParams = Geoprocessing.MakeValueArray(
                        gdbPath,                    // target_workspace
                        "Feature",                  // target_type (for feature classes)
                        string.Empty,               // config_keyword
                        "ALL",                      // schema_type
                        xmlPath,                    // import_file
                        string.Empty                // config_keyword2
                    );
                    var importResult = await Geoprocessing.ExecuteToolAsync("management.ImportXMLWorkspaceDocument", importParams);

                    if (importResult.IsFailed)
                        return (false, gdbPath, $"Schema import failed: {importResult.Messages}");
                    
                    return (true, gdbPath, "GDB created and schema imported successfully.");
                }
                else
                {
                    return (false, string.Empty, $"GDB creation failed: {createResult.Messages}");
                }
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Exception: {ex.Message}");
            }
        }
    }
}