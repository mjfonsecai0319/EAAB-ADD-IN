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
using EAABAddIn.Core.Geometry;

namespace EAABAddIn.Src.Core.Map;

public static class GeocodedPolygonsLayerService
{
    public static readonly string SourceClass = "GeocodedAddresses";
    public static readonly string TargetClass = "PoligonosGeoCod";
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

    public static Task<Dictionary<string,int>> GenerateAsync(string gdbPath, string identifierFieldName = "Identificador", string neighborhoodsPath = null, string clientsPath = null, string neighborhoodNameField = "NEIGHBORHOOD_DESC")
    {
        return QueuedTask.Run(() => _GenerateInternal(gdbPath, null, identifierFieldName, neighborhoodsPath, clientsPath, neighborhoodNameField));
    }

    public static Task<Dictionary<string,int>> GenerateAsync(IEnumerable<string> identifiers, string gdbPath = null, string identifierFieldName = "Identificador", string neighborhoodsPath = null, string clientsPath = null, string neighborhoodNameField = "NEIGHBORHOOD_DESC")
    {
        var set = identifiers?.Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
        return QueuedTask.Run(() => _GenerateInternal(gdbPath, set, identifierFieldName, neighborhoodsPath, clientsPath, neighborhoodNameField));
    }

    private static Dictionary<string,int> _GenerateInternal(string gdbPath, HashSet<string> filterIds = null, string identifierFieldName = "Identificador", string neighborhoodsPath = null, string clientsPath = null, string neighborhoodNameField = "NEIGHBORHOOD_DESC")
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
                return result; 
            }
        }
        
        var pointDef = pointsFc.GetDefinition();
        var oidField = pointDef.GetObjectIDField();
        var shapeField = pointDef.GetShapeField();
        
        var identifierField = pointDef.GetFields().FirstOrDefault(f => 
            f.Name.Equals(identifierFieldName, StringComparison.OrdinalIgnoreCase))?.Name ?? "Identificador";
        
        System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] Usando campo identificador: '{identifierField}'");
        
        TryRemovePolygonLayerFromMap();

        EnsurePolygonFeatureClass(gdbPath, gdb, pointDef.GetSpatialReference());
    EnsureFieldExists(gdbPath, TargetClass, "identificador", "TEXT", 100);
    EnsureFieldExists(gdbPath, TargetClass, "barrios", "TEXT", 4096);
        EnsureFieldExists(gdbPath, TargetClass, "clientes", "LONG", 0);

        using var polygonsFc = gdb.OpenDataset<FeatureClass>(TargetClass);
        var polygonDef = polygonsFc.GetDefinition();
        var polyIdentifierField = "identificador";
        var polyBarriosField = "barrios";
        var polyClientesField = "clientes";
        int barriosMaxLen = 0;
        try
        {
            var barriosFieldDef = polygonDef.GetFields().FirstOrDefault(f => f.Name.Equals(polyBarriosField, StringComparison.OrdinalIgnoreCase));
            if (barriosFieldDef != null)
                barriosMaxLen = barriosFieldDef.Length;
        }
        catch { barriosMaxLen = 0; }

        DeleteAll(polygonsFc);

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

        System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] Encontrados {groups.Count} grupos únicos");

        bool allow3 = Module1.Settings?.permitirTresPuntos == true;
        int minPoints = allow3 ? 3 : 4;
        var discarded = new List<string>();

        using var neighborhoodsFc = OpenFeatureClassFromPath(neighborhoodsPath);
        using var clientsFc = OpenFeatureClassFromPath(clientsPath);

        string barrioNameFieldResolved = null;
        if (neighborhoodsFc != null)
        {
            var ndef = neighborhoodsFc.GetDefinition();
            var nameCandidates = new[] { neighborhoodNameField, "NEIGHBORHOOD", "NEIGHBORHOOD_DESC", "BARRIO", "BARRIOS", "NOMBRE", "NOMBRE_BARRIO" };
            barrioNameFieldResolved = ndef.GetFields()
                .Select(f => f.Name)
                .FirstOrDefault(n => nameCandidates.Any(c => c.Equals(n, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var kv in groups)
        {
            if (kv.Value.Count < minPoints)
            {
                discarded.Add($"{kv.Key}: {kv.Value.Count} (<{minPoints} puntos)");
                continue;
            }

            try
            {
                var polygon = CreatePolygonFromAllPoints(kv.Value, kv.Value.First().SpatialReference);
                
                if (polygon != null && !polygon.IsEmpty && polygon.Area > 0)
                {
                    if (ValidatePolygonGeometry(polygon))
                    {
                        using var rowBuffer = polygonsFc.CreateRowBuffer();
                        rowBuffer[polygonDef.GetShapeField()] = polygon;
                        rowBuffer[polyIdentifierField] = kv.Key;

                        if (neighborhoodsFc != null && !string.IsNullOrWhiteSpace(barrioNameFieldResolved))
                        {
                            try
                            {
                                var barriosValue = GetNeighborhoodsForPolygon(neighborhoodsFc, barrioNameFieldResolved, polygon);
                                if (!string.IsNullOrWhiteSpace(barriosValue))
                                {
                                    if (barriosMaxLen > 0 && barriosValue.Length > barriosMaxLen)
                                        barriosValue = barriosValue.Substring(0, barriosMaxLen - 1);
                                    rowBuffer[polyBarriosField] = barriosValue;
                                }
                            }
                            catch { /* dejar null */ }
                        }

                        if (clientsFc != null)
                        {
                            try
                            {
                                var clientesCount = GetClientsCountForPolygon(clientsFc, polygon);
                                rowBuffer[polyClientesField] = clientesCount;
                            }
                            catch { /* dejar null */ }
                        }
                        
                        try
                        {
                            using var row = polygonsFc.CreateRow(rowBuffer);
                            result[kv.Key] = kv.Value.Count;
                            
                            System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] ✓ {kv.Key}: Polígono guardado ({kv.Value.Count} puntos, área={polygon.Area:F6})");
                        }
                        catch (Exception dbEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] ✗ {kv.Key}: Error al guardar en BD - {dbEx.Message}");
                            discarded.Add($"{kv.Key}: Error BD - {dbEx.Message}");
                        }
                    }
                    else
                    {
                        discarded.Add($"{kv.Key}: Geometría inválida después de validación");
                        System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] ✗ {kv.Key}: Falló validación de geometría");
                    }
                }
                else
                {
                    discarded.Add($"{kv.Key}: No se pudo generar polígono válido (área={polygon?.Area ?? 0})");
                    System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] ✗ {kv.Key}: Polígono nulo o vacío (área={polygon?.Area ?? 0})");
                }
            }
            catch (Exception ex)
            {
                discarded.Add($"{kv.Key}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] ✗ {kv.Key}: Excepción - {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] StackTrace: {ex.StackTrace}");
            }
        }

        if (result.Count == 0 && discarded.Count > 0)
        {
            result["__DIAGNOSTICO__"] = -1; 
            
            var brief = string.Join("; ", discarded.Take(12));
            if (discarded.Count > 12) brief += $" ... (+{discarded.Count - 12})";
            System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] Ningún polígono generado. Causas: {brief}");
        }

        AddLayerToMap(gdbPath);
        polygonLayer?.ClearDisplayCache();

        
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
            if (result.Keys.Any(k => k != "__DIAGNOSTICO__"))
            {
                result.Clear();
                result["__DIAGNOSTICO__"] = -1;
                System.Diagnostics.Debug.WriteLine("[GeocodedPolygons] Inconsistencia: no se materializaron los polígonos esperados.");
            }
        }

        try
        {
            if (polygonLayer != null && result.Keys.Any(k => k != "__DIAGNOSTICO__"))
            {
                var firstId = result.Keys.First(k => k != "__DIAGNOSTICO__");
                var oidFieldName = polygonsFc.GetDefinition().GetObjectIDField();
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
                        polygonLayer.ClearSelection();
                        var qfSel = new QueryFilter { WhereClause = $"{oidFieldName} = {oid.Value}" };
                        polygonLayer.Select(qfSel);
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

    public static void TryRemovePolygonLayerFromMap()
    {
        try
        {
            var mv = MapView.Active;
            if (mv?.Map == null) return;
            var layers = mv.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                .Where(l => l.Name.Equals(TargetClass, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var l in layers)
            {
                mv.Map.RemoveLayer(l);
            }
            polygonLayer = null;
        }
        catch { }
    }

    private static void EnsureFieldExists(string gdbPath, string featureClassName, string fieldName, string fieldType, int length = 0)
    {
        try
        {
            var fcPath = Path.Combine(gdbPath, featureClassName);
            var gdbConn = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            using var gdb = new Geodatabase(gdbConn);
            using var fc = gdb.OpenDataset<FeatureClass>(featureClassName);
            var exists = fc.GetDefinition().GetFields()
                .Any(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            if (exists) return;

        }
        catch { }

      
        try
        {
            if (string.Equals(fieldType, "TEXT", StringComparison.OrdinalIgnoreCase))
            {
                var addFieldParams = Geoprocessing.MakeValueArray(
                    Path.Combine(gdbPath, featureClassName), fieldName, fieldType, "", "", length > 0 ? length : 255);
                var _ = Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams, null, CancelableProgressor.None, GPExecuteToolFlags.None).GetAwaiter().GetResult();
            }
            else
            {
                var addFieldParams = Geoprocessing.MakeValueArray(
                    Path.Combine(gdbPath, featureClassName), fieldName, fieldType);
                var _ = Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams, null, CancelableProgressor.None, GPExecuteToolFlags.None).GetAwaiter().GetResult();
            }
        }
        catch { }
    }

    private static FeatureClass OpenFeatureClassFromPath(string featureClassPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(featureClassPath)) return null;
            var idx = featureClassPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var gdbEnd = idx + 4;
            var gdbPath = featureClassPath.Substring(0, gdbEnd);
            var remainder = featureClassPath.Length > gdbEnd ? featureClassPath.Substring(gdbEnd).TrimStart('\\', '/') : string.Empty;
            if (string.IsNullOrWhiteSpace(remainder)) return null;
            var datasetName = remainder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last();
            var gdbConn = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            var gdb = new Geodatabase(gdbConn);
            return gdb.OpenDataset<FeatureClass>(datasetName);
        }
        catch
        {
            return null;
        }
    }

    private static string GetNeighborhoodsForPolygon(FeatureClass neighborhoodsFc, string nameField, Polygon poly)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filter = new SpatialQueryFilter
        {
            WhereClause = "1=1",
            SubFields = nameField,
            SpatialRelationship = SpatialRelationship.Intersects,
            FilterGeometry = poly
        };
        using var cursor = neighborhoodsFc.Search(filter, false);
        while (cursor.MoveNext())
        {
            using var row = cursor.Current;
            var val = row[nameField]?.ToString();
            if (!string.IsNullOrWhiteSpace(val)) names.Add(val.Trim());
        }
        return string.Join(", ", names.OrderBy(n => n));
    }

    private static int GetClientsCountForPolygon(FeatureClass clientsFc, Polygon poly)
    {
        int count = 0;

        var def = clientsFc.GetDefinition();
        var fields = def.GetFields();
        string oidFld = def.GetObjectIDField();

        string tipoServicioFld = fields.FirstOrDefault(f => f.Name.Equals("TIPOSERVICIOAC", StringComparison.OrdinalIgnoreCase))?.Name ?? "TIPOSERVICIOAC";
        string clasificacionFld = fields.FirstOrDefault(f => f.Name.Equals("DOMCLASIFICACIONPREDIO", StringComparison.OrdinalIgnoreCase))?.Name ?? "DOMCLASIFICACIONPREDIO";

        bool tipoServicioEsNumerico = fields.FirstOrDefault(f => f.Name.Equals(tipoServicioFld, StringComparison.OrdinalIgnoreCase))?.FieldType is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;
        bool clasificacionEsNumerico = fields.FirstOrDefault(f => f.Name.Equals(clasificacionFld, StringComparison.OrdinalIgnoreCase))?.FieldType is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;

        string whereTipo = tipoServicioEsNumerico ? $"{tipoServicioFld} = 10" : $"{tipoServicioFld} = '10'";
        string whereClasif;
        if (clasificacionEsNumerico)
            whereClasif = $"{clasificacionFld} IN (1,4,6)";
        else
            whereClasif = $"{clasificacionFld} IN ('1','4','6')";

        var filter = new SpatialQueryFilter
        {
            SpatialRelationship = SpatialRelationship.Intersects,
            FilterGeometry = poly,
            SubFields = oidFld,
            WhereClause = $"{whereTipo} AND {whereClasif}"
        };

        using var cursor = clientsFc.Search(filter, true);
        while (cursor.MoveNext())
        {
            count++;
        }
        return count;
    }
    private static FeatureClass GetPointsFeatureClass(string gdbPath, out Geodatabase gdb)
    {
        gdb = null;
        var mapView = MapView.Active;
        if (mapView?.Map != null)
        {
            var existing = mapView.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                .FirstOrDefault(l => l.Name.Equals(SourceClass, StringComparison.OrdinalIgnoreCase));
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
            return gdb.OpenDataset<FeatureClass>(SourceClass);
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
            .Any(d => d.GetName().Equals(TargetClass, StringComparison.OrdinalIgnoreCase));
        if (exists) return;
        var parameters = Geoprocessing.MakeValueArray(
            gdbPath,
            TargetClass,
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
        using var fc = gdb.OpenDataset<FeatureClass>(TargetClass);

        var layerParams = new FeatureLayerCreationParams(fc) { Name = TargetClass };
        polygonLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, mapView.Map);
    }

    private static Polygon CreatePolygonFromAllPoints(List<MapPoint> points, SpatialReference sr)
    {
        if (points == null || points.Count < 3)
            return null;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[AllPointsPolygon] Creando polígono con {points.Count} puntos como vértices");

            var orderedPoints = OrderPointsForSimplePolygon(points);
            
            var coords = new List<Coordinate2D>();
            foreach (var point in orderedPoints)
            {
                coords.Add(new Coordinate2D(point.X, point.Y));
            }
            
            if (orderedPoints.Count > 0)
            {
                coords.Add(new Coordinate2D(orderedPoints[0].X, orderedPoints[0].Y));
            }
            
            var polygon = PolygonBuilderEx.CreatePolygon(coords, sr);
            
            if (polygon != null && !polygon.IsEmpty)
            {
                var cleanedPolygon = CleanSelfIntersections(polygon, sr);
                if (cleanedPolygon != null && !cleanedPolygon.IsEmpty)
                {
                    polygon = cleanedPolygon;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[AllPointsPolygon] ✓ Polígono creado con {orderedPoints.Count} vértices, área={polygon?.Area:F2}");
            return polygon;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AllPointsPolygon] Error: {ex.Message}");
            return CreateConvexHullPolygon(points, sr); // Fallback
        }
    }

    private static List<MapPoint> OrderPointsForSimplePolygon(List<MapPoint> points)
    {
        if (points.Count <= 3)
            return points.ToList();

        try
        {
            double centroidX = points.Average(p => p.X);
            double centroidY = points.Average(p => p.Y);
            
            System.Diagnostics.Debug.WriteLine($"[PolarOrdering] Centroide: ({centroidX:F2}, {centroidY:F2})");
            
            var ordered = points.OrderBy(p => 
            {
                double angle = Math.Atan2(p.Y - centroidY, p.X - centroidX);
                return angle < 0 ? angle + 2 * Math.PI : angle;
            }).ToList();
            
            System.Diagnostics.Debug.WriteLine($"[PolarOrdering] ✓ {ordered.Count} puntos ordenados por ángulo polar");
            
            return ordered;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PolarOrdering] Error: {ex.Message}, usando orden original");
            return points.ToList();
        }
    }


    private static Polygon CleanSelfIntersections(Polygon polygon, SpatialReference sr)
    {
        try
        {
            var simplified = GeometryEngine.Instance.SimplifyAsFeature(polygon);
            if (simplified is Polygon simplePoly && !simplePoly.IsEmpty)
            {
                System.Diagnostics.Debug.WriteLine($"[CleanPolygon] Auto-intersecciones corregidas");
                return simplePoly;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CleanPolygon] Error en simplificación: {ex.Message}");
            return RebuildPolygonWithoutIntersections(polygon, sr);
        }
        
        return polygon;
    }

    private static Polygon RebuildPolygonWithoutIntersections(Polygon polygon, SpatialReference sr)
    {
        try
        {
            var allPoints = new List<MapPoint>();
            for (int partIndex = 0; partIndex < polygon.PartCount; partIndex++)
            {
                var part = polygon.Parts[partIndex];
                foreach (var segment in part)
                {
                    allPoints.Add(segment.StartPoint);
                }
            }
            
            var uniquePoints = new List<MapPoint>();
            foreach (var point in allPoints)
            {
                if (!uniquePoints.Any(p => 
                    Math.Abs(p.X - point.X) < 0.001 && 
                    Math.Abs(p.Y - point.Y) < 0.001))
                {
                    uniquePoints.Add(point);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[RebuildPolygon] Reconstruyendo con {uniquePoints.Count} puntos únicos");
            
            return CreatePolygonFromAllPoints(uniquePoints, sr);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RebuildPolygon] Error: {ex.Message}");
            return polygon; 
        }
    }

    private static Polygon CreateConcaveHull(List<MapPoint> points, string gdbPath, Geodatabase gdb)
    {
        if (points == null || points.Count < 3)
            return null;

        try
        {
            var sr = points.First().SpatialReference;
            
            System.Diagnostics.Debug.WriteLine($"[PolygonAlgorithm] Procesando {points.Count} puntos");
            
            if (points.Count <= 15)
            {
                System.Diagnostics.Debug.WriteLine($"[PolygonAlgorithm] Usando algoritmo de todos-los-puntos para {points.Count} puntos");
                var resultPolygon = CreatePolygonFromAllPoints(points, sr);
                
                if (resultPolygon != null && !resultPolygon.IsEmpty && resultPolygon.Area > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[PolygonAlgorithm] ✓ Polígono creado con {points.Count} vértices, área={resultPolygon.Area:F2}");
                    
                    bool allIncluded = VerifyAllPointsIncluded(resultPolygon, points);
                    if (allIncluded)
                    {
                        return resultPolygon;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[PolygonAlgorithm] Usando Concave Hull para {points.Count} puntos");
            try
            {
                double chiValue = 0.1; 
                var concaveHull = new DelaunayConcaveHull(points, chiValue);
                var resultPolygon = concaveHull.BuildConcaveHull();
                
                if (resultPolygon != null && !resultPolygon.IsEmpty && resultPolygon.Area > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[PolygonAlgorithm] ✓ Concave Hull completado, vértices={resultPolygon.PointCount}, área={resultPolygon.Area:F2}");
                    return resultPolygon;
                }
            }
            catch (Exception exDelaunay)
            {
                System.Diagnostics.Debug.WriteLine($"[PolygonAlgorithm] ⚠ Error en Concave Hull: {exDelaunay.Message}");
            }
            
            System.Diagnostics.Debug.WriteLine($"[PolygonAlgorithm] Usando fallback de todos-los-puntos");
            return CreatePolygonFromAllPoints(points, sr);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PolygonAlgorithm] Excepción: {ex.Message}");
            return CreateConvexHullPolygon(points, points.First().SpatialReference);
        }
    }

    private static Polygon CreateConvexHullPolygon(List<MapPoint> points, SpatialReference sr)
    {
        try
        {
            var mpBuilder = new MultipointBuilderEx(sr);
            foreach (var p in points) mpBuilder.AddPoint(p);
            var multi = mpBuilder.ToGeometry();
            return GeometryEngine.Instance.ConvexHull(multi) as Polygon;
        }
        catch
        {
            return null;
        }
    }

    private static bool ValidatePolygonGeometry(Polygon polygon)
    {
        if (polygon == null || polygon.IsEmpty)
            return false;
        
        try
        {
            if (polygon.Area <= 0)
            {
                System.Diagnostics.Debug.WriteLine("[Validation] Área <= 0");
                return false;
            }
            
            if (polygon.Length <= 0)
            {
                System.Diagnostics.Debug.WriteLine("[Validation] Perímetro <= 0");
                return false;
            }
            
            if (polygon.PartCount == 0)
            {
                System.Diagnostics.Debug.WriteLine("[Validation] Sin parts");
                return false;
            }
            
            var extent = polygon.Extent;
            if (extent == null || extent.Width <= 0 || extent.Height <= 0)
            {
                System.Diagnostics.Debug.WriteLine("[Validation] Extensión inválida");
                return false;
            }
    
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Validation] Excepción: {ex.Message}");
            return false;
        }
    }


    private static bool VerifyAllPointsIncluded(Polygon polygon, List<MapPoint> originalPoints)
    {
        if (polygon == null || polygon.IsEmpty || originalPoints == null)
            return false;
        
        try
        {
            int pointsInside = 0;
            int pointsOnBoundary = 0;
            int pointsOutside = 0;
            double tolerance = 0.01; 
            
            foreach (var point in originalPoints)
            {
                if (GeometryEngine.Instance.Contains(polygon, point))
                {
                    pointsInside++;
                }
                else
                {
                    double distanceToBoundary = GeometryEngine.Instance.Distance(polygon, point);
                    if (distanceToBoundary <= tolerance)
                    {
                        pointsOnBoundary++;
                    }
                    else
                    {
                        pointsOutside++;
                        System.Diagnostics.Debug.WriteLine($"[Verification] Punto fuera del polígono: ({point.X:F2}, {point.Y:F2})");
                    }
                }
            }
            
            int totalIncluded = pointsInside + pointsOnBoundary;
            double inclusionRate = (double)totalIncluded / originalPoints.Count;
            
            System.Diagnostics.Debug.WriteLine($"[Verification] Puntos: {totalIncluded}/{originalPoints.Count} incluidos ({inclusionRate:P0}) - Dentro: {pointsInside}, Borde: {pointsOnBoundary}, Fuera: {pointsOutside}");
            
            return inclusionRate >= 0.95;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Verification] Error: {ex.Message}");
            return true; 
        }
    }

    private static double CrossProduct(MapPoint o, MapPoint a, MapPoint b)
    {
        return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
    }
    
    private static double Distance(MapPoint p1, MapPoint p2)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    private static double DistanceToSegment(MapPoint p, MapPoint a, MapPoint b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSquared = dx * dx + dy * dy;
        
        if (lengthSquared < 0.0001) return Distance(p, a);
        
        double t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared));
        double projX = a.X + t * dx;
        double projY = a.Y + t * dy;
        
        double distX = p.X - projX;
        double distY = p.Y - projY;
        
        return Math.Sqrt(distX * distX + distY * distY);
    }
}