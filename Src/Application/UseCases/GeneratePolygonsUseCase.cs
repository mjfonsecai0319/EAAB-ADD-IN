#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Core.Map;

namespace EAABAddIn.Src.Application.UseCases;

public class GeneratePolygonsUseCase
{
    public async Task<Dictionary<string, int>> Invoke(
        string workspace,
        string rootClassPath,
        string rootClassField,
        string? neighborhoodsClassPath,
        string? clientsClassPath
    )
    {
        var result = await QueuedTask.Run(() => Internal(
            workspace,
            rootClassPath,
            rootClassField,
            neighborhoodsClassPath,
            clientsClassPath
        ));

        return result;
    }

    private async Task<Dictionary<string, int>> Internal(
        string workspace,
        string rootClassPath,
        string rootClassField,
        string? neighborhoodsClassPath,
        string? clientsClassPath
    )
    {
        // Extract unique identifiers from the root feature class
        var identifiers = GetUniqueIdentifiers(rootClassPath, rootClassField);

        if (identifiers == null || identifiers.Count == 0)
            return new Dictionary<string, int>();

        try
        {
            // Call the core service to generate polygons. GenerateAsync returns a Task but
            // we're already running inside QueuedTask.Run, so block on the Task.Result here.
            return await GeocodedPolygonsLayerService.GenerateAsync(
                identifiers: identifiers,
                gdbPath: workspace,
                rootClassPath: rootClassPath,
                rootClassField: rootClassField,
                classPathA: neighborhoodsClassPath,
                classPathB: clientsClassPath,
                field: "NEIGHBORHOOD_DESC"
            );
        }
        catch
        {
            return new Dictionary<string, int>();
        }
    }

    private List<string> GetUniqueIdentifiers(string rootClassPath, string identifierField)
    {
        var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(rootClassPath) || string.IsNullOrWhiteSpace(identifierField))
            return identifiers.OrderBy(s => s).ToList();

        try
        {
            // Open the feature class directly
            var connIdx = rootClassPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
            if (connIdx < 0) return identifiers.OrderBy(s => s).ToList();

            var gdbEnd = connIdx + 4;
            var gdbPath = rootClassPath.Substring(0, gdbEnd);
            var remainder = rootClassPath.Length > gdbEnd ? rootClassPath.Substring(gdbEnd).TrimStart('\\', '/') : string.Empty;
            if (string.IsNullOrWhiteSpace(remainder)) return identifiers.OrderBy(s => s).ToList();

            var datasetName = remainder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last();
            var connPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            using var gdb = new Geodatabase(connPath);
            using var fc = gdb.OpenDataset<FeatureClass>(datasetName);

            var def = fc.GetDefinition();
            var idField = def.GetFields().FirstOrDefault(f => f.Name.Equals(identifierField, StringComparison.OrdinalIgnoreCase))?.Name;
            if (idField == null)
                return identifiers.OrderBy(s => s).ToList();

            using var cursor = fc.Search(new QueryFilter { SubFields = idField }, true);
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                var val = row[idField]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) identifiers.Add(val.Trim());
            }
        }
        catch
        {
            // ignore errors and return whatever we collected
        }

        return identifiers.OrderBy(s => s).ToList();
    }
}