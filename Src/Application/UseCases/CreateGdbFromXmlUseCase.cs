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

        public async Task<(bool ok, string gdbPath, string message)> Invoke(string outFolder, string xmlPath)
        {
            if (string.IsNullOrWhiteSpace(outFolder) || string.IsNullOrWhiteSpace(xmlPath))
                return (false, string.Empty, "Invalid input parameters.");

            if (!File.Exists(xmlPath))
                return (false, string.Empty, "XML schema file not found.");

            try
            {
                if (!Directory.Exists(outFolder))
                    Directory.CreateDirectory(outFolder);

                const string gdbName = "GDB_Cargue";
                var gdbPath = Path.Combine(outFolder, $"{gdbName}.gdb");
                
                if (Directory.Exists(gdbPath))
                {
                    System.Diagnostics.Debug.WriteLine($"📂 GDB existente encontrada: {gdbPath}");
                    System.Diagnostics.Debug.WriteLine($"   ✓ Reutilizando GDB existente - NO se recreará");
                    
                    var reportsFolder = Path.Combine(gdbPath, "reportes");
                    if (!Directory.Exists(reportsFolder))
                    {
                        try
                        {
                            Directory.CreateDirectory(reportsFolder);
                            System.Diagnostics.Debug.WriteLine($"   ✓ Carpeta 'reportes' creada en la GDB");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"   ⚠ No se pudo crear carpeta reportes: {ex.Message}");
                        }
                    }
                    
                    return (true, gdbPath, $"✓ Reutilizando GDB existente '{gdbName}.gdb' - Los datos se actualizarán/agregarán.");
                }
                
                System.Diagnostics.Debug.WriteLine($"📂 GDB NO existe - Creando nueva: {gdbPath}");
                
                var createParams = Geoprocessing.MakeValueArray(outFolder, gdbName);
                var createResult = await Geoprocessing.ExecuteToolAsync("management.CreateFileGDB", createParams);
                
                if (createResult.IsFailed)
                    return (false, string.Empty, $"Error al crear la GDB: {createResult.Messages}");

                var importParams = Geoprocessing.MakeValueArray(
                    gdbPath,                    
                    xmlPath,                    
                    "SCHEMA_ONLY"               
                );
                var importResult = await Geoprocessing.ExecuteToolAsync("management.ImportXMLWorkspaceDocument", importParams);

                if (importResult.IsFailed)
                {
                    var errorMessages = string.Join("; ", importResult.Messages.Select(m => m.Text));
                    return (false, gdbPath, $"Error al importar esquema XML: {errorMessages}");
                }
                
                var newReportsFolder = Path.Combine(gdbPath, "reportes");
                try
                {
                    Directory.CreateDirectory(newReportsFolder);
                    System.Diagnostics.Debug.WriteLine($"   ✓ Carpeta 'reportes' creada en la nueva GDB");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"   ⚠ No se pudo crear carpeta reportes: {ex.Message}");
                }
                
                System.Diagnostics.Debug.WriteLine($"   ✓ GDB '{gdbName}.gdb' creada exitosamente con esquema XML");
                return (true, gdbPath, $"✓ GDB '{gdbName}.gdb' creada nueva con esquema XML importado.");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Excepción: {ex.Message}");
            }
        }
    }
}