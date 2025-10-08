using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace EAABAddIn.Src.Core.Map;

public static class IdentifierSublayersService
{
    private const string SourcePointsClass = "GeocodedAddresses";
    private const string GroupLayerName = "Identificadores";

    public static Task<List<string>> CreateIdentifierSublayersAsync(string gdbPath = null)
    {
        return QueuedTask.Run(() => _CreateInternal(gdbPath));
    }

    private static List<string> _CreateInternal(string gdbPath)
    {
        var created = new List<string>();
        var mapView = MapView.Active;
        if (mapView?.Map == null) return created;

        // Localizar o crear group layer
        var group = mapView.Map.GetLayersAsFlattenedList().OfType<GroupLayer>()
            .FirstOrDefault(gl => gl.Name.Equals(GroupLayerName, StringComparison.OrdinalIgnoreCase));
        if (group == null)
        {
            group = LayerFactory.Instance.CreateGroupLayer(mapView.Map, 0, GroupLayerName);
        }

        // Obtener FeatureClass de puntos
        FeatureClass fc = GeocodedPolygonsLayerService_ListPoints(gdbPath, out Geodatabase gdb);
        if (fc == null)
        {
            gdb?.Dispose();
            return created;
        }

        using (gdb)
        using (fc)
        {
            var def = fc.GetDefinition();
            var shapeField = def.GetShapeField();
            var idField = def.GetFields().FirstOrDefault(f => f.Name.Equals("Identificador", StringComparison.OrdinalIgnoreCase))?.Name ?? "Identificador";

            var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var cursor = fc.Search(new QueryFilter { SubFields = idField + "," + shapeField }, false))
            {
                while (cursor.MoveNext())
                {
                    using var row = cursor.Current as Feature;
                    var idv = row?[idField]?.ToString();
                    if (string.IsNullOrWhiteSpace(idv)) continue;
                    if (!ids.ContainsKey(idv)) ids[idv] = 0;
                    ids[idv]++;
                }
            }

            // Crear sublayers para ids con >=3 puntos si no existen
            // Necesitamos una capa base de puntos en el mapa para poder clonar y filtrar.
            FeatureLayer baseLayer = mapView.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                .FirstOrDefault(l => l.Name.Equals(SourcePointsClass, StringComparison.OrdinalIgnoreCase));
            if (baseLayer == null)
            {
                // Si no existe, creamos una sobre la feature class completa primero
                var flParams = new FeatureLayerCreationParams(fc) { Name = SourcePointsClass };
                baseLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flParams, mapView.Map);
            }

            using var dataset = baseLayer.GetTable() as FeatureClass;
            foreach (var kv in ids.Where(k => k.Value >= 3))
            {
                var existing = group.Layers.OfType<FeatureLayer>()
                    .FirstOrDefault(l => l.Name.Equals(kv.Key, StringComparison.OrdinalIgnoreCase));
                if (existing != null) continue;
                var flc = new FeatureLayerCreationParams(dataset) { Name = kv.Key };
                var lyr = LayerFactory.Instance.CreateLayer<FeatureLayer>(flc, group);
                if (lyr == null) continue;
                lyr.SetDefinitionQuery($"{idField} = '{kv.Key.Replace("'","''")}'");
                created.Add(kv.Key);
            }
        }

        return created.OrderBy(s => s).ToList();
    }

    // Reutiliza lógica auxiliar similar al servicio de polígonos, sin exponerlo públicamente.
    private static FeatureClass GeocodedPolygonsLayerService_ListPoints(string gdbPath, out Geodatabase gdb)
    {
        gdb = null;
        var mapView = MapView.Active;
        if (mapView?.Map != null)
        {
            var existing = mapView.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                .FirstOrDefault(l => l.Name.Equals(SourcePointsClass, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                try
                {
                    var fc = existing.GetTable() as FeatureClass;
                    if (fc != null) return fc;
                }
                catch { }
            }
        }
        if (string.IsNullOrWhiteSpace(gdbPath) || !Directory.Exists(gdbPath))
            gdbPath = Project.Current.DefaultGeodatabasePath;
        if (string.IsNullOrWhiteSpace(gdbPath) || !Directory.Exists(gdbPath)) return null;
        var gdbConn = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
        try
        {
            gdb = new Geodatabase(gdbConn);
            return gdb.OpenDataset<FeatureClass>(SourcePointsClass);
        }
        catch
        {
            gdb?.Dispose();
            return null;
        }
    }
}
