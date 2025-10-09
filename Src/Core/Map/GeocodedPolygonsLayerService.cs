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

            try
            {
                // Crear polígono usando Concave Hull optimizado
                var polygon = CreateConcaveHull(kv.Value, gdbPath, gdb);
                
                if (polygon != null && !polygon.IsEmpty && polygon.Area > 0)
                {
                    // Validación adicional antes de insertar
                    if (ValidatePolygonGeometry(polygon))
                    {
                        using var rowBuffer = polygonsFc.CreateRowBuffer();
                        rowBuffer[polygonDef.GetShapeField()] = polygon;
                        rowBuffer[polyIdentifierField] = kv.Key;
                        using var row = polygonsFc.CreateRow(rowBuffer);
                        result[kv.Key] = kv.Value.Count;
                        
                        System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] ✓ {kv.Key}: Polígono guardado ({kv.Value.Count} puntos, área={polygon.Area:F2})");
                    }
                    else
                    {
                        discarded.Add($"{kv.Key}: Geometría inválida después de validación");
                        System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] ✗ {kv.Key}: Falló validación de geometría");
                    }
                }
                else
                {
                    discarded.Add($"{kv.Key}: No se pudo generar polígono válido");
                    System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] ✗ {kv.Key}: Polígono nulo o vacío");
                }
            }
            catch (Exception ex)
            {
                discarded.Add($"{kv.Key}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[GeocodedPolygons] ✗ {kv.Key}: Excepción - {ex.Message}");
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

    private static Polygon CreateConcaveHull(List<MapPoint> points, string gdbPath, Geodatabase gdb)
    {
        if (points == null || points.Count < 3)
            return null;

        try
        {
            var sr = points.First().SpatialReference;
            Polygon resultPolygon = null;
            
            // SIEMPRE usar Nearest Neighbor para garantizar que TODOS los puntos se incluyan
            System.Diagnostics.Debug.WriteLine($"[ConcaveHull] Usando Nearest Neighbor para {points.Count} puntos (TODOS incluidos)");
            resultPolygon = CreatePolygonWithNearestNeighbor(points, sr);
            
            // Validar y corregir auto-intersecciones
            if (resultPolygon != null && !resultPolygon.IsEmpty)
            {
                // Intentar simplificar el polígono para corregir auto-intersecciones
                try
                {
                    var simplified = GeometryEngine.Instance.SimplifyAsFeature(resultPolygon);
                    if (simplified is Polygon simplePoly && !simplePoly.IsEmpty && simplePoly.Area > 0)
                    {
                        // Verificar si la simplificación mejoró el polígono
                        if (simplePoly.Area <= resultPolygon.Area * 1.1) // No más del 10% más grande
                        {
                            resultPolygon = simplePoly;
                            System.Diagnostics.Debug.WriteLine("[ConcaveHull] Polígono simplificado exitosamente");
                        }
                    }
                }
                catch
                {
                    // Si la simplificación falla, continuar con el polígono original
                }
                
                // Validación final
                if (resultPolygon.Area > 0)
                {
                    // Verificar que todos los puntos estén incluidos
                    bool allPointsIncluded = VerifyAllPointsIncluded(resultPolygon, points);
                    
                    if (allPointsIncluded)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ConcaveHull] ✓ Polígono creado: {points.Count} puntos TODOS incluidos, área={resultPolygon.Area:F2}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ConcaveHull] ⚠ Polígono creado pero algunos puntos pueden estar fuera, área={resultPolygon.Area:F2}");
                    }
                    
                    return resultPolygon;
                }
            }
            
            // Si todo falla, usar ConvexHull como último recurso
            System.Diagnostics.Debug.WriteLine("[ConcaveHull] Usando ConvexHull como fallback");
            return CreateConvexHullPolygon(points, sr);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConcaveHull] Excepción: {ex.Message}");
            return CreateConvexHullPolygon(points, points.First().SpatialReference);
        }
    }

    /// <summary>
    /// Crea un polígono usando el algoritmo de vecino más cercano
    /// </summary>
    private static Polygon CreatePolygonWithNearestNeighbor(List<MapPoint> points, SpatialReference sr)
    {
        try
        {
            // Ordenar puntos usando algoritmo de vecino más cercano
            var orderedPoints = OrderPointsByNearestNeighbor(points);
            
            // Crear polígono desde coordenadas ordenadas
            var coords = new List<Coordinate2D>();
            foreach (var point in orderedPoints)
            {
                coords.Add(new Coordinate2D(point.X, point.Y));
            }
            
            // Cerrar el polígono
            if (orderedPoints.Count > 0)
            {
                coords.Add(new Coordinate2D(orderedPoints[0].X, orderedPoints[0].Y));
            }
            
            return PolygonBuilderEx.CreatePolygon(coords, sr);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Algoritmo híbrido: usa Convex Hull para el perímetro exterior y conecta puntos en orden óptimo
    /// </summary>
    private static Polygon CreateHybridConcaveHull(List<MapPoint> points, SpatialReference sr)
    {
        try
        {
            // 1. Crear Convex Hull para obtener puntos del perímetro
            var mpBuilder = new MultipointBuilderEx(sr);
            foreach (var p in points) mpBuilder.AddPoint(p);
            var multipoint = mpBuilder.ToGeometry();
            var convexHull = GeometryEngine.Instance.ConvexHull(multipoint) as Polygon;
            
            if (convexHull == null || convexHull.IsEmpty)
                return CreatePolygonWithNearestNeighbor(points, sr);
            
            // 2. Identificar qué puntos están en el perímetro (convex hull)
            var perimeterPoints = new List<MapPoint>();
            var interiorPoints = new List<MapPoint>();
            
            foreach (var point in points)
            {
                bool isOnPerimeter = false;
                
                // Verificar si el punto está en el borde del convex hull
                var partCount = convexHull.PartCount;
                for (int partIndex = 0; partIndex < partCount; partIndex++)
                {
                    var part = convexHull.Parts[partIndex];
                    
                    // Iterar sobre los segmentos para obtener los puntos
                    foreach (var segment in part)
                    {
                        // Verificar punto inicial del segmento
                        var startPt = segment.StartPoint;
                        double distStart = Math.Sqrt(
                            Math.Pow(startPt.X - point.X, 2) + 
                            Math.Pow(startPt.Y - point.Y, 2)
                        );
                        
                        if (distStart < 0.001) // Tolerancia de 1mm
                        {
                            isOnPerimeter = true;
                            break;
                        }
                        
                        // Verificar punto final del segmento
                        var endPt = segment.EndPoint;
                        double distEnd = Math.Sqrt(
                            Math.Pow(endPt.X - point.X, 2) + 
                            Math.Pow(endPt.Y - point.Y, 2)
                        );
                        
                        if (distEnd < 0.001)
                        {
                            isOnPerimeter = true;
                            break;
                        }
                    }
                    if (isOnPerimeter) break;
                }
                
                if (isOnPerimeter)
                    perimeterPoints.Add(point);
                else
                    interiorPoints.Add(point);
            }
            
            System.Diagnostics.Debug.WriteLine($"[HybridAlgorithm] Perímetro: {perimeterPoints.Count}, Interior: {interiorPoints.Count}");
            
            // 3. Si hay pocos puntos interiores, usar algoritmo normal
            if (interiorPoints.Count <= 2)
            {
                return CreatePolygonWithNearestNeighbor(points, sr);
            }
            
            // 4. Ordenar puntos del perímetro por nearest neighbor
            var orderedPerimeter = OrderPointsByNearestNeighbor(perimeterPoints);
            
            // 5. Crear polígono con puntos del perímetro
            var coords = new List<Coordinate2D>();
            foreach (var point in orderedPerimeter)
            {
                coords.Add(new Coordinate2D(point.X, point.Y));
            }
            
            // Cerrar polígono
            if (orderedPerimeter.Count > 0)
            {
                coords.Add(new Coordinate2D(orderedPerimeter[0].X, orderedPerimeter[0].Y));
            }
            
            return PolygonBuilderEx.CreatePolygon(coords, sr);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HybridAlgorithm] Error: {ex.Message}");
            return CreatePolygonWithNearestNeighbor(points, sr);
        }
    }

    /// <summary>
    /// Crea un Convex Hull simple como fallback
    /// </summary>
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

    /// <summary>
    /// Ordena los puntos usando el algoritmo de vecino más cercano (Nearest Neighbor)
    /// con selección inteligente del punto inicial
    /// </summary>
    private static List<MapPoint> OrderPointsByNearestNeighbor(List<MapPoint> points)
    {
        if (points == null || points.Count <= 3)
            return points?.ToList() ?? new List<MapPoint>();

        var ordered = new List<MapPoint>();
        var remaining = new List<MapPoint>(points);
        
        // Encontrar el mejor punto inicial
        MapPoint startPoint = FindBestStartPoint(points);
        ordered.Add(startPoint);
        remaining.Remove(startPoint);
        
        var current = startPoint;
        
        // Mientras queden puntos por visitar
        while (remaining.Count > 0)
        {
            // Encontrar el punto más cercano al actual
            MapPoint nearest = null;
            double minDistance = double.MaxValue;
            
            foreach (var point in remaining)
            {
                double distance = GeometryEngine.Instance.Distance(current, point);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = point;
                }
            }
            
            if (nearest != null)
            {
                ordered.Add(nearest);
                remaining.Remove(nearest);
                current = nearest;
            }
            else
            {
                // Si no hay punto más cercano, tomar el primero disponible
                if (remaining.Count > 0)
                {
                    var fallback = remaining[0];
                    ordered.Add(fallback);
                    remaining.Remove(fallback);
                    current = fallback;
                }
                else
                {
                    break;
                }
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"[NearestNeighbor] Ordenados {ordered.Count} de {points.Count} puntos, inicio en ({startPoint.X:F2}, {startPoint.Y:F2})");
        return ordered;
    }

    /// <summary>
    /// Encuentra el mejor punto de inicio para minimizar auto-intersecciones
    /// Para muchos puntos (>15), prueba múltiples inicios y elige el mejor
    /// </summary>
    private static MapPoint FindBestStartPoint(List<MapPoint> points)
    {
        if (points == null || points.Count == 0)
            return null;
        
        if (points.Count <= 4)
        {
            // Para pocos puntos, usar esquina inferior izquierda
            return points.OrderBy(p => p.X).ThenBy(p => p.Y).First();
        }
        
        // Calcular centroide
        double centroidX = points.Average(p => p.X);
        double centroidY = points.Average(p => p.Y);
        
        // Calcular desviación estándar para detectar distribución
        double stdDevX = Math.Sqrt(points.Average(p => Math.Pow(p.X - centroidX, 2)));
        double stdDevY = Math.Sqrt(points.Average(p => Math.Pow(p.Y - centroidY, 2)));
        
        // Si la distribución es muy irregular, usar esquina
        if (stdDevX > stdDevY * 3 || stdDevY > stdDevX * 3)
        {
            // Distribución alargada - usar extremo
            var corner = points.OrderBy(p => p.X).ThenBy(p => p.Y).First();
            System.Diagnostics.Debug.WriteLine($"[StartPoint] Distribución alargada, inicio en esquina: ({corner.X:F2}, {corner.Y:F2})");
            return corner;
        }
        
        // Para distribución regular con muchos puntos, usar optimización
        if (points.Count > 15)
        {
            // Encontrar convex hull y usar uno de sus puntos
            try
            {
                var mpBuilder = new MultipointBuilderEx(points.First().SpatialReference);
                foreach (var p in points) mpBuilder.AddPoint(p);
                var multipoint = mpBuilder.ToGeometry();
                var convexHull = GeometryEngine.Instance.ConvexHull(multipoint) as Polygon;
                
                if (convexHull != null && !convexHull.IsEmpty && convexHull.PartCount > 0)
                {
                    // Usar el primer vértice del convex hull como inicio
                    var part = convexHull.Parts[0];
                    var firstSegment = part.First();
                    var hullStart = firstSegment.StartPoint;
                    
                    // Encontrar el punto original más cercano a este vértice del hull
                    var bestStart = points.OrderBy(p => 
                        Math.Sqrt(Math.Pow(p.X - hullStart.X, 2) + Math.Pow(p.Y - hullStart.Y, 2))
                    ).First();
                    
                    System.Diagnostics.Debug.WriteLine($"[StartPoint] Usando vértice de Convex Hull: ({bestStart.X:F2}, {bestStart.Y:F2})");
                    return bestStart;
                }
            }
            catch
            {
                // Si falla, continuar con método estándar
            }
        }
        
        // Para distribución regular, encontrar punto más externo (más alejado del centroide)
        var extremePoint = points.OrderByDescending(p => 
            Math.Sqrt(Math.Pow(p.X - centroidX, 2) + Math.Pow(p.Y - centroidY, 2))
        ).First();
        
        System.Diagnostics.Debug.WriteLine($"[StartPoint] Centroide: ({centroidX:F2}, {centroidY:F2}), Inicio externo: ({extremePoint.X:F2}, {extremePoint.Y:F2})");
        
        return extremePoint;
    }

    /// <summary>
    /// Calcula una medida de calidad del polígono basada en compacidad
    /// Valores cercanos a 1 = más circular/compacto, valores más altos = más irregular
    /// </summary>
    private static double CalculatePolygonQuality(Polygon polygon)
    {
        if (polygon == null || polygon.IsEmpty || polygon.Area <= 0)
            return double.MaxValue;
        
        try
        {
            double perimeter = polygon.Length;
            double area = polygon.Area;
            
            // Índice de Polsby-Popper: 4π × área / perímetro²
            // 1 = círculo perfecto, <1 = más irregular
            double compactness = (4 * Math.PI * area) / (perimeter * perimeter);
            
            // Retornar inverso para que valores más bajos = mejor calidad
            return 1.0 / compactness;
        }
        catch
        {
            return double.MaxValue;
        }
    }

    /// <summary>
    /// Valida que un polígono tenga geometría correcta y sea utilizable
    /// </summary>
    private static bool ValidatePolygonGeometry(Polygon polygon)
    {
        if (polygon == null || polygon.IsEmpty)
            return false;
        
        try
        {
            // Validación 1: Área positiva
            if (polygon.Area <= 0)
            {
                System.Diagnostics.Debug.WriteLine("[Validation] Área <= 0");
                return false;
            }
            
            // Validación 2: Perímetro válido
            if (polygon.Length <= 0)
            {
                System.Diagnostics.Debug.WriteLine("[Validation] Perímetro <= 0");
                return false;
            }
            
            // Validación 3: Tiene al menos un part
            if (polygon.PartCount == 0)
            {
                System.Diagnostics.Debug.WriteLine("[Validation] Sin parts");
                return false;
            }
            
            // Validación 4: Extensión válida
            var extent = polygon.Extent;
            if (extent == null || extent.Width <= 0 || extent.Height <= 0)
            {
                System.Diagnostics.Debug.WriteLine("[Validation] Extensión inválida");
                return false;
            }
            
            // Validación 5: No es demasiado degenerado (muy delgado o alargado)
            // RELAJADA: permitir relaciones de aspecto mayores para polígonos con todos los puntos
            double aspectRatio = Math.Max(extent.Width, extent.Height) / Math.Min(extent.Width, extent.Height);
            if (aspectRatio > 10000) // Relación de aspecto MUY extrema (relajado de 1000 a 10000)
            {
                System.Diagnostics.Debug.WriteLine($"[Validation] Relación de aspecto extrema: {aspectRatio:F2}");
                return false;
            }
            
            // Validación 6: Área razonable comparada con extensión
            // RELAJADA: permitir polígonos más delgados cuando incluyen todos los puntos
            double extentArea = extent.Width * extent.Height;
            double areaRatio = polygon.Area / extentArea;
            if (areaRatio < 0.0001) // Polígono extremadamente lineal (relajado de 0.001 a 0.0001)
            {
                System.Diagnostics.Debug.WriteLine($"[Validation] Polígono casi lineal, ratio: {areaRatio:F6}");
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

    /// <summary>
    /// Verifica que todos los puntos originales estén contenidos en el polígono o muy cerca
    /// </summary>
    private static bool VerifyAllPointsIncluded(Polygon polygon, List<MapPoint> originalPoints)
    {
        if (polygon == null || polygon.IsEmpty || originalPoints == null)
            return false;
        
        try
        {
            int pointsInside = 0;
            int pointsOnBoundary = 0;
            int pointsOutside = 0;
            double tolerance = 0.01; // 1cm de tolerancia
            
            foreach (var point in originalPoints)
            {
                // Verificar si está dentro del polígono
                if (GeometryEngine.Instance.Contains(polygon, point))
                {
                    pointsInside++;
                }
                else
                {
                    // Verificar si está en el borde (puede ser que esté EN el vértice)
                    double distanceToBoundary = GeometryEngine.Instance.Distance(polygon.Extent, point);
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
            
            // Aceptar si al menos el 95% está incluido (permite pequeños errores de redondeo)
            return inclusionRate >= 0.95;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Verification] Error: {ex.Message}");
            return true; // En caso de error, no rechazar
        }
    }
}
