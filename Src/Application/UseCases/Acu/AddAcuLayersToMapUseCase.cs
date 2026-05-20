#nullable enable

namespace EAABAddIn.Src.Application.UseCases.Acu;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using EAABAddIn.Src.Application.Utils;

/// <summary>
/// Agrega las capas migradas de acueducto al mapa activo.
/// </summary>
public class AddAcuLayersToMapUseCase
{
    /// <summary>
    /// Ejecuta la adicion de capas de acueducto al mapa.
    /// </summary>
    /// <param name="targetGdbPath">Ruta de la geodatabase de destino.</param>
    /// <param name="linesOnly">Si es true, solo agrega capas de lineas.</param>
    /// <param name="pointsOnly">Si es true, solo agrega capas de puntos.</param>
    /// <returns>Tupla con el estado y el mensaje de resultado.</returns>
    public Task<(bool success, string message)> Invoke(string targetGdbPath, bool linesOnly = false, bool pointsOnly = false)
    {
        return QueuedTask.Run(() => Internal(targetGdbPath, linesOnly, pointsOnly));
    }

    private (bool success, string message) Internal(string targetGdbPath, bool linesOnly, bool pointsOnly)
    {
        var log = new StringBuilder();
        var map = MapView.Active?.Map;
        if (map is null)
        {
            return (false, "No hay un mapa activo. Abre un mapa en ArcGIS Pro primero.");
        }

        if (string.IsNullOrWhiteSpace(targetGdbPath))
        {
            return (false, "Ruta de geodatabase invalida.");
        }

        if (!Directory.Exists(targetGdbPath))
        {
            return (false, $"GDB no existe: {targetGdbPath}");
        }

        using var targetGdb = GeodatabaseUtils.OpenGeodatabase(targetGdbPath);
        if (targetGdb is null)
        {
            return (false, "No se pudo abrir la geodatabase de destino.");
        }

        var layersAdded = new List<string>();
        Envelope? combinedExtent = null;

        if (!pointsOnly)
        {
            var lineClasses = new[] { "acd_RedMatriz", "acd_Conduccion", "acd_RedMenor", "acd_LineaLateral" };
            foreach (var className in lineClasses)
            {
                var (added, extent) = AddFeatureLayerToMap(map, targetGdb, className, isLine: true);
                if (added)
                {
                    layersAdded.Add(className);
                    if (extent != null)
                    {
                        combinedExtent = combinedExtent == null ? extent : combinedExtent.Union(extent);
                    }
                }
            }
        }

        if (!linesOnly)
        {
            var pointClasses = new[] { "acd_Accesorio", "acd_ValvulaControl", "acd_ValvulaSistema", "acd_Hidrante", "acd_CamaraAcceso" };
            foreach (var className in pointClasses)
            {
                var (added, extent) = AddFeatureLayerToMap(map, targetGdb, className, isLine: false);
                if (added)
                {
                    layersAdded.Add(className);
                    if (extent != null)
                    {
                        combinedExtent = combinedExtent == null ? extent : combinedExtent.Union(extent);
                    }
                }
            }
        }

        if (layersAdded.Count > 0)
        {
            log.AppendLine($"Se agregaron {layersAdded.Count} capas al mapa");

            if (combinedExtent != null)
            {
                if (!double.IsNaN(combinedExtent.XMin) && !double.IsNaN(combinedExtent.YMin))
                {
                    var width = combinedExtent.Width;
                    var height = combinedExtent.Height;
                    var expandedExtent = new EnvelopeBuilderEx(
                        combinedExtent.XMin - width * 0.1,
                        combinedExtent.YMin - height * 0.1,
                        combinedExtent.XMax + width * 0.1,
                        combinedExtent.YMax + height * 0.1,
                        combinedExtent.SpatialReference)
                        .ToGeometry();

                    MapView.Active?.ZoomTo(expandedExtent, TimeSpan.FromSeconds(1.5));
                    MapView.Active?.Redraw(true);
                }
            }

            return (true, log.ToString());
        }

        log.AppendLine("No se agregaron capas.");
        return (false, log.ToString());
    }

    private (bool added, Envelope? extent) AddFeatureLayerToMap(Map map, Geodatabase gdb, string className, bool isLine)
    {
        try
        {
            using var fc = FeatureClassUtils.TryOpenFromGeodatabase(gdb, className);
            if (fc == null)
            {
                return (false, null);
            }

            var count = fc.GetCount();
            if (count == 0)
            {
                return (false, null);
            }

            Envelope? calculatedExtent = CalculateExtent(fc);

            var existingLayer = map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .FirstOrDefault(l => Shared.StringComparer.Equals(l.Name, className));

            if (existingLayer != null)
            {
                bool sameDatasource = false;
                try
                {
                    using var layerFc = existingLayer.GetFeatureClass();
                    var layerGdb = layerFc?.GetDatastore() as Geodatabase;
                    var layerGdbPath = layerGdb?.GetPath().LocalPath ?? string.Empty;
                    var targetGdbPath = gdb.GetPath().LocalPath;
                    var layerFcName = layerFc?.GetName() ?? string.Empty;
                    var targetFcName = fc.GetName();
                    sameDatasource =
                        !string.IsNullOrEmpty(layerGdbPath) &&
                        Shared.StringComparer.Equals(layerGdbPath, targetGdbPath) &&
                        Shared.StringComparer.Equals(layerFcName, targetFcName);
                }
                catch
                {
                }

                if (sameDatasource)
                {
                    Shared.ApplySymbology(existingLayer, isLine);
                    Shared.EnsureLayerIsVisibleAndSelectable(existingLayer, fc);
                    return (true, calculatedExtent);
                }

                try { map.RemoveLayer(existingLayer); } catch { }
            }

            var flParams = new FeatureLayerCreationParams(fc)
            {
                Name = className,
                MapMemberPosition = MapMemberPosition.AddToTop
            };
            var layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flParams, map);

            if (layer != null)
            {
                Shared.ApplySymbology(layer, isLine);
                Shared.EnsureLayerIsVisibleAndSelectable(layer, fc);
                try
                {
                    layer.SetDefinitionQuery("1=1");
                    layer.SetDefinitionQuery("");
                }
                catch
                {
                }

                return (true, calculatedExtent);
            }

            return (false, null);
        }
        catch
        {
            return (false, null);
        }
    }

    private static Envelope? CalculateExtent(FeatureClass fc)
    {
        try
        {
            Envelope? calculatedExtent = null;
            using var cursor = fc.Search();
            int geomCount = 0;
            while (cursor.MoveNext() && geomCount < 100)
            {
                using var feature = cursor.Current as Feature;
                if (feature != null)
                {
                    var geom = feature.GetShape();
                    if (geom != null && !geom.IsEmpty)
                    {
                        var geomExtent = geom.Extent;
                        if (geomExtent != null && !double.IsNaN(geomExtent.XMin))
                        {
                            calculatedExtent = calculatedExtent == null
                                ? geomExtent
                                : calculatedExtent.Union(geomExtent);
                            geomCount++;
                        }
                    }
                }
            }

            return calculatedExtent;
        }
        catch
        {
            return null;
        }
    }
}
