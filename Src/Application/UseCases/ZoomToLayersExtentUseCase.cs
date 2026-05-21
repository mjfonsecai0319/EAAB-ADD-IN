#nullable enable

namespace EAABAddIn.Src.Application.UseCases;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

public class ZoomToLayersExtentUseCase
{
    private static readonly IEqualityComparer<string> _IgnoreCaseComparer = StringComparer.OrdinalIgnoreCase;

    public Task Invoke(params string[] layerNames)
    {
        return QueuedTask.Run(() => InternalExecute(layerNames));
    }

    private void InternalExecute(string[] layerNames)
    {
        try
        {
            var map = MapView.Active?.Map;
            if (map is null || layerNames.Length == 0)
                return;

            Envelope? combinedExtent = null;

            foreach (var layerName in layerNames)
            {
                var extent = GetLayerExtent(map, layerName);
                if (extent != null)
                {
                    combinedExtent = combinedExtent?.Union(extent) ?? extent;
                }
            }

            if (combinedExtent != null)
            {
                ZoomToExtent(combinedExtent);
            }
        }
        catch (Exception)
        {

        }
    }

    private static Envelope? GetLayerExtent(Map map, string layerName)
    {
        try
        {
            var layer = map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .FirstOrDefault(l => _IgnoreCaseComparer.Equals(l.Name, layerName));

            if (layer?.GetFeatureClass() is not FeatureClass fc)
                return null;

            return CalculateExtent(fc);
        }
        catch
        {
            return null;
        }
    }

    private static Envelope? CalculateExtent(FeatureClass fc)
    {
        try
        {
            var extent = fc.GetExtent();

            if (extent == null || extent.IsEmpty || double.IsNaN(extent.XMin))
            {
                Envelope? calculated = null;
                using var cursor = fc.Search();
                int count = 0;

                while (cursor.MoveNext() && count < 80)
                {
                    using var feature = cursor.Current as Feature;
                    var geomExtent = feature?.GetShape()?.Extent;
                    if (geomExtent?.IsEmpty == false)
                    {
                        calculated = calculated?.Union(geomExtent) ?? geomExtent;
                        count++;
                    }
                }
                return calculated;
            }

            return extent;
        }
        catch
        {
            return null;
        }
    }

    private static void ZoomToExtent(Envelope extent)
    {
        var width = extent.Width;
        var height = extent.Height;

        var expanded = new EnvelopeBuilderEx(
            extent.XMin - width * 0.1,
            extent.YMin - height * 0.1,
            extent.XMax + width * 0.1,
            extent.YMax + height * 0.1,
            extent.SpatialReference
        ).ToGeometry();

        MapView.Active?.ZoomTo(expanded, TimeSpan.FromSeconds(1.5));
        MapView.Active?.Redraw(true);
    }
}