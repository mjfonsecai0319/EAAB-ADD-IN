#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Mapping.Locate;
using ArcGIS.Desktop.Mapping;

namespace EAABAddIn.Src.Application.UseCases;

public class GetSelectedFeatureUseCase
{
    public async Task<List<Feature>> Invoke(MapView? mapView, string target)
    {
        if (mapView == null)
        {
            return [];
        }

        return await QueuedTask.Run(() => this.InvokeInternal(mapView, target));
    }

    public List<Feature> InvokeInternal(MapView mapView, string target)
    {
        // Collect all selected features that belong to the target polygons class
        var selected = new List<Feature>();
        var selectionDict = mapView.Map.GetSelection().ToDictionary();

        // If no explicit target provided, include ALL selected polygon layers
        if (string.IsNullOrWhiteSpace(target))
        {
            foreach (var kv in selectionDict)
            {
                if (kv.Key is FeatureLayer fl && fl.ShapeType == esriGeometryType.esriGeometryPolygon)
                {
                    var features = GetSelectedFeatures(fl, kv.Value);
                    if (features != null && features.Count > 0)
                        selected.AddRange(features);
                }
            }
            return selected;
        }

        var targetDatasetName = GetDatasetName(target);

        var filtered = selectionDict.Where(kv =>
        {
            if (kv.Key is FeatureLayer fl)
            {
                // Match either by layer name or by underlying dataset name
                if (fl.Name.Equals(targetDatasetName, StringComparison.OrdinalIgnoreCase))
                    return true;
                try
                {
                    using var table = fl.GetTable();
                    var def = table?.GetDefinition();
                    var dsName = def?.GetName();
                    if (!string.IsNullOrWhiteSpace(dsName) && dsName.Equals(targetDatasetName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }
            }
            return false;
        }).ToList();
        

        foreach (var kv in filtered)
        {
            FeatureLayer? creationLayer = null;

            if (kv.Key is FeatureLayer fl)
            {
                var features = GetSelectedFeatures(fl, kv.Value);
                if (features != null && features.Count > 0)
                {
                    if (creationLayer == null) creationLayer = fl;
                    selected.AddRange(features);
                }
            }
        }

        return selected;
    }

    private List<Feature> GetSelectedFeatures(FeatureLayer layer, List<long> objectIDs)
    {
        var ret = new List<Feature>();
        try
        {
            using var table = layer.GetTable();
            using var cursor = table.Search(new QueryFilter { ObjectIDs = objectIDs }, false);
            while (cursor.MoveNext())
            {
                var f = cursor.Current as Feature;
                if (f != null) ret.Add(f);
            }
        }
        catch { }
        return ret;
    }

    private string GetDatasetName(string featureClassPath)
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
