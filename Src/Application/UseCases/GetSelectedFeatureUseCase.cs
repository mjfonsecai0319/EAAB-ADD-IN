#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
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
        var filtered = mapView.Map.GetSelection().ToDictionary().Select(
            it => it
        ).Where(
            it => it.Key.Name.Equals(target, StringComparison.OrdinalIgnoreCase)
        ).ToList();



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
}
