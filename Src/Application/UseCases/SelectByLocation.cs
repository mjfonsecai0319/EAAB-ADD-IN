#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace EAABAddIn.Src.Application.UseCases;

public class SelectByLocationUseCase
{
    public async Task<string?> Invoke(Feature feature, string? path = null)
    {
        if (path is null)
        {
            return null;
        }

        // Run the spatial query and attribute extraction on the QueuedTask
        var result = await QueuedTask.Run(() => this.InvokeInternal(feature, path));
        return result;
    }

    private string InvokeInternal(Feature feature, string path)
    {
        var map = MapView.Active?.Map;
        if (map == null)
            return string.Empty;

        var geo = feature.GetShape();
        var query = new SpatialQueryFilter
        {
            FilterGeometry = geo,
            SpatialRelationship = SpatialRelationship.Intersects
        };

        var datasetName = GetDatasetNameFromPath(path);
        var fl2 = map.FindLayers(datasetName).FirstOrDefault() as FeatureLayer;
        if (fl2 is null)
        {
            return string.Empty;
        }

        fl2.Select(query);

        var table = fl2.GetTable();
        var neighborhoodValues = new System.Collections.Generic.List<string>();

        using (var cursor = table.Search(query, false))
        {
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                try
                {
                    object? valObj = null;
                    var def = table.GetDefinition();
                    var fieldIndex = def.FindField("NEIGHBORHOOD_DESC");
                    if (fieldIndex >= 0)
                    {
                        valObj = row[fieldIndex];
                    }
                    else
                    {
                        valObj = row["NEIGHBORHOOD_DESC"];
                    }

                    var val = valObj?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(val) && !neighborhoodValues.Contains(val))
                    {
                        neighborhoodValues.Add(val);
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        neighborhoodValues.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join(", ", neighborhoodValues);
    }

    private static string GetDatasetNameFromPath(string featureClassPath)
    {
        if (string.IsNullOrWhiteSpace(featureClassPath))
        {
            return featureClassPath;
        }

        var idx = featureClassPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);

        if (idx >= 0)
        {
            var gdbEnd = idx + 4;
            var remainder = featureClassPath.Length > gdbEnd ? featureClassPath.Substring(gdbEnd).TrimStart('\\', '/') : string.Empty;
            if (string.IsNullOrWhiteSpace(remainder))
            {
                return System.IO.Path.GetFileNameWithoutExtension(featureClassPath);
            }

            var parts = remainder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Last();
        }

        return System.IO.Path.GetFileName(featureClassPath);
    }
}
