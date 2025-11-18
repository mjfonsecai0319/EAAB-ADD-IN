using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace EAABAddIn.Src.Application.UseCases;

public class AppendOptions
{
    public string SourcePath { get; set; } = string.Empty; 
    public string TargetPath { get; set; } = string.Empty; 
    public bool UseSelection { get; set; } = false;        
    public string SchemaType { get; set; } = "NO_TEST";    
    public string FieldMappings { get; set; } = string.Empty; 
}

public class AppendFeaturesUseCase
{
    public async Task<(bool ok, string message)> Invoke(AppendOptions options)
    {
        if (string.IsNullOrWhiteSpace(options?.SourcePath) || string.IsNullOrWhiteSpace(options?.TargetPath))
            return (false, "Debe especificar origen y destino");

        try
        {
            return await QueuedTask.Run<(bool, string)>(async () =>
            {
                string input = options.SourcePath;

                if (options.UseSelection)
                {
                    var selectedOids = GetSelectedOidsForDataset(options.SourcePath);
                    if (selectedOids == null || selectedOids.Count == 0)
                        return (false, "No hay entidades seleccionadas del origen");

                    string oidField;
                    using (var fc = OpenFeatureClass(options.SourcePath))
                    {
                        if (fc == null) return (false, "No se pudo abrir el origen para determinar el OID");
                        oidField = fc.GetDefinition()?.GetObjectIDField();
                    }
                    if (string.IsNullOrWhiteSpace(oidField))
                        return (false, "No se encontró el campo OID del origen");

                    var inList = string.Join(",", selectedOids);
                    var where = $"{EscapeField(oidField)} IN ({inList})";

                    var lyrName = $"lyr_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                    var makeParams = Geoprocessing.MakeValueArray(options.SourcePath, lyrName, where);
                    var makeRes = await Geoprocessing.ExecuteToolAsync("management.MakeFeatureLayer", makeParams, null, null, GPExecuteToolFlags.AddToHistory);
                    if (makeRes.IsFailed) return (false, "Fallo MakeFeatureLayer");

                    var memPath = $"in_memory/sel_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                    var copyParams = Geoprocessing.MakeValueArray(lyrName, memPath);
                    var copyRes = await Geoprocessing.ExecuteToolAsync("management.CopyFeatures", copyParams, null, null, GPExecuteToolFlags.AddToHistory);
                    if (copyRes.IsFailed) return (false, "Fallo CopyFeatures de la selección");

                    input = memPath;
                }

                var appendParams = Geoprocessing.MakeValueArray(
                    input,
                    options.TargetPath,
                    options.SchemaType,
                    string.IsNullOrWhiteSpace(options.FieldMappings) ? "" : options.FieldMappings,
                    ""
                );
                var gpResult = await Geoprocessing.ExecuteToolAsync("management.Append", appendParams, null, null, GPExecuteToolFlags.AddToHistory);
                if (gpResult.IsFailed)
                {
                    var msg = gpResult.ErrorMessages != null && gpResult.ErrorMessages.Any()
                        ? string.Join(" | ", gpResult.ErrorMessages.Select(m => m.Text))
                        : "Append falló";
                    return (false, msg);
                }

                return (true, "Migración completada");
            });
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static FeatureClass OpenFeatureClass(string path)
    {
        try
        {
            var idx = path.LastIndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var gdbPath = path.Substring(0, idx + 4);
            var dsPath = path.Substring(idx + 5).TrimStart('\\', '/');

            var conn = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            var gdb = new Geodatabase(conn);
            return gdb.OpenDataset<FeatureClass>(dsPath);
        }
        catch { return null; }
    }

    private static string EscapeField(string field)
    {
        if (string.IsNullOrWhiteSpace(field)) return field;
        return field.Contains(' ') ? $"\"{field}\"" : field;
    }

    private static List<long> GetSelectedOidsForDataset(string featureClassPath)
    {
        try
        {
            var mapView = ArcGIS.Desktop.Mapping.MapView.Active;
            if (mapView == null) return null;

            var helper = new EAABAddIn.Src.Application.UseCases.GetSelectedFeatureUseCase();
            var features = helper.InvokeInternal(mapView, featureClassPath);
            var ids = new List<long>();
            foreach (var f in features)
            {
                ids.Add(f.GetObjectID());
            }
            features.ForEach(f => f.Dispose());
            return ids;
        }
        catch { return null; }
    }
}
