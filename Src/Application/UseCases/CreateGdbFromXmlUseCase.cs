using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace EAABAddIn.Src.Application.UseCases
{
    public class CreateGdbFromXmlUseCase
    {
        // Crea una FGDB en outFolder con nombre gdbName (sin .gdb) y carga el esquema desde xmlPath
        // Retorna: ok, ruta completa de la GDB, mensaje
        public async Task<(bool ok, string gdbPath, string message)> Invoke(string outFolder, string gdbName, string xmlPath)
        {
            if (string.IsNullOrWhiteSpace(outFolder))
                return (false, string.Empty, "Carpeta de salida no definida");
            if (string.IsNullOrWhiteSpace(xmlPath))
                return (false, string.Empty, "Ruta XML no definida");

            try
            {
                var nameOnly = gdbName?.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase) == true
                    ? Path.GetFileNameWithoutExtension(gdbName)
                    : (string.IsNullOrWhiteSpace(gdbName) ? $"GDB_{DateTime.Now:yyyyMMdd_HHmmss}" : gdbName);

                var gdbPath = Path.Combine(outFolder, nameOnly + ".gdb");

                // Crear FGDB si no existe
                if (!Directory.Exists(gdbPath))
                {
                    var createParams = Geoprocessing.MakeValueArray(outFolder, nameOnly, "CURRENT");
                    var createRes = await Geoprocessing.ExecuteToolAsync("management.CreateFileGDB", createParams, null, null, GPExecuteToolFlags.AddToHistory);
                    if (createRes.IsFailed)
                    {
                        var msg = createRes.ErrorMessages != null && createRes.ErrorMessages.Any()
                            ? string.Join(" | ", createRes.ErrorMessages.Select(m => m.Text))
                            : "Fallo CreateFileGDB";
                        return (false, string.Empty, msg);
                    }
                }

                // Importar sólo el esquema desde el XML
                var importParams = Geoprocessing.MakeValueArray(gdbPath, xmlPath, "SCHEMA_ONLY");
                var importRes = await Geoprocessing.ExecuteToolAsync("management.ImportXMLWorkspaceDocument", importParams, null, null, GPExecuteToolFlags.AddToHistory);
                if (importRes.IsFailed)
                {
                    var msg = importRes.ErrorMessages != null && importRes.ErrorMessages.Any()
                        ? string.Join(" | ", importRes.ErrorMessages.Select(m => m.Text))
                        : "Fallo ImportXMLWorkspaceDocument";
                    return (false, gdbPath, msg);
                }

                return (true, gdbPath, "GDB creada e importado esquema");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, ex.Message);
            }
        }
    }
}
