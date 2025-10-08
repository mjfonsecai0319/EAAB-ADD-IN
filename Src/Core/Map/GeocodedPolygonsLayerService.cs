using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace EAABAddIn.Src.Core.Map;

public static class GeocodedPolygonsLayerService
{
    private const string SourcePointsClass = "GeocodedAddresses";
    private const string TargetPolygonsClass = "PoligonosGeoCod";
    private static FeatureLayer polygonLayer;
    private static FeatureLayer pointsLayer;

    public static Task<List<string>> ListIdentifiersAsync(string gdbPath = null)
    {
        return QueuedTask.Run(() => _ListIdentifiersInternal(gdbPath));
    }

    private static List<string> _ListIdentifiersInternal(string gdbPath)
    {
        var ids = new List<string>();
        FeatureClass pointsFc = GetPointsFeatureClass(gdbPath, out Geodatabase gdbLocal);
        if (pointsFc == null) return ids;
        using (gdbLocal)
        using (pointsFc)
        {
            var def = pointsFc.GetDefinition();
            var idField = def.GetFields().FirstOrDefault(f => f.Name.Equals("Identificador", StringComparison.OrdinalIgnoreCase))?.Name ?? "Identificador";
            using var cursor = pointsFc.Search(new QueryFilter { SubFields = idField }, true);
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                var val = row[idField]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) set.Add(val);
            }
            ids = set.OrderBy(s => s).ToList();
        }
        return ids;
    }

    public static Task<Dictionary<string,int>> GenerateAsync(string gdbPath)
    {
        return QueuedTask.Run(() => _GenerateInternal(gdbPath));
    }

    public static Task<Dictionary<string,int>> GenerateAsync(IEnumerable<string> identifiers, string gdbPath = null)
    {
        var set = identifiers?.Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
        return QueuedTask.Run(() => _GenerateInternal(gdbPath, set));
    }

    private static Dictionary<string,int> _GenerateInternal(string gdbPath, HashSet<string> filterIds = null)
    {
        var result = new Dictionary<string,int>();
        if (string.IsNullOrWhiteSpace(gdbPath) || !Directory.Exists(gdbPath))
            gdbPath = Project.Current.DefaultGeodatabasePath;
        if (string.IsNullOrWhiteSpace(gdbPath) || !Directory.Exists(gdbPath))
            return result;

        FeatureClass pointsFc = GetPointsFeatureClass(gdbPath, out Geodatabase gdb);
        if (pointsFc == null)
        {
            gdb?.Dispose();
            return result;
        }
        // Si la feature class provino de una capa ya cargada, 'gdb' será null. Abrimos la GDB manualmente.
        if (gdb == null)
        {
            try
            {
                var gdbConn = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
                gdb = new Geodatabase(gdbConn);
            }
            catch
            {
                pointsFc.Dispose();
                return result; // no se pudo abrir la geodatabase
            }
        }
        var pointDef = pointsFc.GetDefinition();
        var oidField = pointDef.GetObjectIDField();
        var shapeField = pointDef.GetShapeField();
        var identifierField = pointDef.GetFields().FirstOrDefault(f => f.Name.Equals("Identificador", StringComparison.OrdinalIgnoreCase))?.Name ?? "Identificador";
        EnsurePolygonFeatureClass(gdbPath, gdb, pointDef.GetSpatialReference());

        using var polygonsFc = gdb.OpenDataset<FeatureClass>(TargetPolygonsClass);
        var polygonDef = polygonsFc.GetDefinition();
        var polyIdentifierField = polygonDef.GetFields().FirstOrDefault(f => f.Name.Equals("identificador", StringComparison.OrdinalIgnoreCase))?.Name;
        if (polyIdentifierField == null)
        {
            // agregar campo identificador si no existe
            var addFieldParams = Geoprocessing.MakeValueArray(Path.Combine(gdbPath, TargetPolygonsClass), "identificador", "TEXT", "", "", 100);
            var _ = Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams, null, CancelableProgressor.None, GPExecuteToolFlags.None).GetAwaiter().GetResult();
            polyIdentifierField = "identificador";
        }

        // Limpiar registros previos (asumimos reemplazo completo)
        DeleteAll(polygonsFc);

        // Agrupar puntos por identificador
        var groups = new Dictionary<string, List<MapPoint>>(StringComparer.OrdinalIgnoreCase);
        using (var cursor = pointsFc.Search(new QueryFilter { SubFields = string.Join(",", new [] { oidField, shapeField, identifierField }) }, false))
        {
            while (cursor.MoveNext())
            {
                using var row = cursor.Current as Feature;
                if (row == null) continue;
                var idValue = row[identifierField]?.ToString();
                if (string.IsNullOrWhiteSpace(idValue)) continue;
                if (filterIds != null && filterIds.Count > 0 && !filterIds.Contains(idValue)) continue;
                if (row[shapeField] is not MapPoint mp) continue;
                if (!groups.ContainsKey(idValue)) groups[idValue] = new List<MapPoint>();
                groups[idValue].Add(mp);
            }
        }

        // Configuración: por defecto exigir >=4; si permitirTresPuntos=true, aceptar también grupos de tamaño 3.
        bool allow3 = Module1.Settings?.permitirTresPuntos == true;
        int minPoints = allow3 ? 3 : 4;
        var discarded = new List<string>();

        foreach (var kv in groups)
        {
            if (kv.Value.Count < minPoints)
            {
                discarded.Add($"{kv.Key}: {kv.Value.Count} (<{minPoints} puntos)");
                continue;
            }
            var sr = kv.Value.First().SpatialReference;
            var mpBuilder = new MultipointBuilderEx(sr);
            foreach (var p in kv.Value) mpBuilder.AddPoint(p);
            var multi = mpBuilder.ToGeometry();
            var hull = GeometryEngine.Instance.ConvexHull(multi);
            if (hull is Polygon poly)
            {
                using var rowBuffer = polygonsFc.CreateRowBuffer();
                rowBuffer[polygonDef.GetShapeField()] = poly;
                rowBuffer[polyIdentifierField] = kv.Key;
                using var row = polygonsFc.CreateRow(rowBuffer);
                result[kv.Key] = kv.Value.Count;
            }
        }

        // Si no se generó ningún polígono, devolver diagnóstico embebido usando clave especial
        if (result.Count == 0 && discarded.Count > 0)
        {
            // Guardamos un pseudo-registro con clave vacía para comunicar diagnóstico.
            result["__DIAGNOSTICO__"] = -1; // consumidor puede interpretar esto y mostrar mensaje detallado
            // Para no prolongar memoria, truncamos a primeros 12
            var brief = string.Join("; ", discarded.Take(12));
            if (discarded.Count > 12) brief += $" ... (+{discarded.Count - 12})";
            // Usamos consola debug; la UI puede volver a calcular si desea el texto completo.
            System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] Ningún polígono generado. Causas: {brief}");
        }

        // Recargar siempre la capa (evita quedar apuntando a otra geodatabase distinta)
        try
        {
            var mvReload = MapView.Active;
            if (mvReload?.Map != null && polygonLayer != null)
            {
                mvReload.Map.RemoveLayer(polygonLayer);
                polygonLayer = null;
            }
        }
        catch { }

        AddLayerToMap(gdbPath); // volver a crear / asociar
        polygonLayer?.ClearDisplayCache();

        // Validar realmente cuántos registros quedaron en la feature class
        int actualCount = 0;
        try
        {
            using (var checkCursor = polygonsFc.Search(null, false))
            {
                while (checkCursor.MoveNext()) actualCount++;
            }
        }
        catch { }
        if (actualCount == 0)
        {
            // Si el diccionario decía que había resultados pero la tabla quedó vacía, limpiamos para forzar diagnóstico arriba.
            if (result.Keys.Any(k => k != "__DIAGNOSTICO__"))
            {
                result.Clear();
                result["__DIAGNOSTICO__"] = -1;
                System.Diagnostics.Debug.WriteLine("[GeocodedPolygons] Inconsistencia: no se materializaron los polígonos esperados.");
            }
        }

        // Intentar seleccionar y hacer zoom al primer polígono creado
        try
        {
            if (polygonLayer != null && result.Keys.Any(k => k != "__DIAGNOSTICO__"))
            {
                var firstId = result.Keys.First(k => k != "__DIAGNOSTICO__");
                var oidFieldName = polygonsFc.GetDefinition().GetObjectIDField();
                // Buscar OID del primer registro con ese identificador
                var idFieldName = polyIdentifierField;
                long? oid = null;
                using (var cursor = polygonsFc.Search(new QueryFilter { WhereClause = $"{idFieldName} = '{firstId.Replace("'","''")}'", SubFields = oidFieldName + "," + idFieldName }, false))
                {
                    if (cursor.MoveNext())
                    {
                        using var row = cursor.Current;
                        oid = row.GetObjectID();
                    }
                }
                if (oid.HasValue)
                {
                    var mv = MapView.Active;
                    if (mv != null)
                    {
                        // Seleccionar (usando QueryFilter sobre el OID)
                        polygonLayer.ClearSelection();
                        var qfSel = new QueryFilter { WhereClause = $"{oidFieldName} = {oid.Value}" };
                        polygonLayer.Select(qfSel);
                        // Zoom al shape
                        using var feat = polygonsFc.Search(new QueryFilter { WhereClause = $"{oidFieldName} = {oid.Value}", SubFields = oidFieldName + "," + polygonsFc.GetDefinition().GetShapeField() }, false);
                        if (feat.MoveNext())
                        {
                            using var r = feat.Current as Feature;
                            if (r?.GetShape() is Geometry g && !g.IsEmpty)
                            {
                                var env = g.Extent;
                                mv.ZoomTo(env, new TimeSpan(0,0,0,0,600));
                            }
                        }
                    }
                }
            }
        }
        catch { /* silencioso */ }
        pointsFc.Dispose();
        gdb.Dispose();
        return result;
    }

    private static FeatureClass GetPointsFeatureClass(string gdbPath, out Geodatabase gdb)
    {
        gdb = null;
        var mapView = MapView.Active;
        if (mapView?.Map != null)
        {
            var existing = mapView.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                .FirstOrDefault(l => l.Name.Equals(SourcePointsClass, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                pointsLayer = existing;
                try
                {
                    var fc = pointsLayer.GetTable() as FeatureClass;
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

    private static void EnsurePolygonFeatureClass(string gdbPath, Geodatabase gdb, SpatialReference sr)
    {
        var exists = gdb.GetDefinitions<FeatureClassDefinition>()
            .Any(d => d.GetName().Equals(TargetPolygonsClass, StringComparison.OrdinalIgnoreCase));
        if (exists) return;
        var parameters = Geoprocessing.MakeValueArray(
            gdbPath,
            TargetPolygonsClass,
            "POLYGON",
            "",
            "DISABLED",
            "DISABLED",
            sr.Wkid > 0 ? sr.Wkid : 4326
        );
        var _ = Geoprocessing.ExecuteToolAsync("management.CreateFeatureclass", parameters, null, CancelableProgressor.None, GPExecuteToolFlags.AddToHistory).GetAwaiter().GetResult();
    }

    private static void DeleteAll(FeatureClass fc)
    {
        var oidField = fc.GetDefinition().GetObjectIDField();
        var toDelete = new List<Row>();
        using (var cursor = fc.Search(new QueryFilter { SubFields = oidField }, false))
        {
            while (cursor.MoveNext())
            {
                if (cursor.Current is Row row)
                    toDelete.Add(row);
            }
        }
        foreach (var row in toDelete)
        {
            try { row.Delete(); } catch { }
            row.Dispose();
        }
    }

    private static void AddLayerToMap(string gdbPath)
    {
        var mapView = MapView.Active;
        if (mapView?.Map == null) return;

        var gdbConn = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
        using var gdb = new Geodatabase(gdbConn);
        using var fc = gdb.OpenDataset<FeatureClass>(TargetPolygonsClass);

        // No reutilizamos instancias previas para evitar caches de esquema obsoletos.
        var layerParams = new FeatureLayerCreationParams(fc) { Name = TargetPolygonsClass };
        polygonLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, mapView.Map);
    }
}
