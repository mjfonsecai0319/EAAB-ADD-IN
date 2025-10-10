#nullable enable

using System;
using System.Linq;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace EAABAddIn.Src.Application.UseCases;

public class SelectByLocation
{
    public void Invoke(Feature feature)
    {
        QueuedTask.Run(() => this.InvokeInternal(feature));  
    }
    
    private void InvokeInternal(Feature feature)
    {
        var map = MapView.Active.Map;
        var geo = feature.GetShape();
        var query = new SpatialQueryFilter
        {
            FilterGeometry = geo,
            SpatialRelationship = SpatialRelationship.Intersects
        };
        var fl2 = map.FindLayers("BARRIOS_MUNICIPIO").FirstOrDefault() as FeatureLayer;

        fl2?.Select(query);
    }
}
