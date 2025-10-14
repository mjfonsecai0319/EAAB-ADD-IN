#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Mapping.Locate;
using ArcGIS.Desktop.Mapping;

using static EAABAddIn.Src.Core.Map.GeocodedPolygonsLayerService;

namespace EAABAddIn.Src.Application.UseCases;

public class GetSelectedFeatureUseCase
{
    public async Task<Feature?> Invoke(MapView? mapView)
    {
        if (mapView == null)
        {
            return null;
        }

        return await QueuedTask.Run(() => this.InvokeInternal(mapView));
    }

    public Feature? InvokeInternal(MapView mapView)
    {
        var selectedFeatures = mapView.Map.GetSelection().ToDictionary()
        .Select(
            it => it
        ).Where(
            it => it.Key.Name.Equals(TargetClass)
        ).ToList();
        
        var kvp = selectedFeatures.ElementAt(0);
        var layer = kvp.Key as FeatureLayer;

        if (layer is not null)
        {
            var objectIDs = kvp.Value;

            if (objectIDs == null || objectIDs.Count < 1)
            {
                return null;
            }

            return this.GetSelectedFeature(layer, objectIDs);
        }

        return null;
    }

    private Feature? GetSelectedFeature(FeatureLayer layer, System.Collections.Generic.List<long> objectIDs)
    {
        using var table = layer.GetTable();
        using var cursor = table.Search(new QueryFilter { ObjectIDs = objectIDs }, false);
        if (cursor.MoveNext())
        {
            return cursor.Current as Feature;
        }

        return null;
    }
}
