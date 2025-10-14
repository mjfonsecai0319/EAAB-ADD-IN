#nullable enable

using System;
using System.Linq;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace EAABAddIn.Src.Application.UseCases;

public class SelectByLocationUseCase
{
    public void Invoke(Feature feature, string? path = null)
    {
        if (path is null)
        {
            return;
        }

        QueuedTask.Run(() => this.InvokeInternal(feature, path));
    }

    private void InvokeInternal(Feature feature, string path)
    {
        var map = MapView.Active.Map;
        var geo = feature.GetShape();
        var query = new SpatialQueryFilter
        {
            FilterGeometry = geo,
            SpatialRelationship = SpatialRelationship.Intersects
        };
        var datasetName = GetDatasetNameFromPath(path);
        var fl2 = map.FindLayers(datasetName).FirstOrDefault() as FeatureLayer;
        fl2?.Select(query);
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
