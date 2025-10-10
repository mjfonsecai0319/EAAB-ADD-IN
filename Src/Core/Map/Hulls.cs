using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Geometry;

namespace EAABAddIn.Src.Core.Map;

/// <summary>
/// Servicio para calcular envolventes cóncavas (concave hull) usando el algoritmo K-Nearest Neighbors.
/// Adaptado para usar geometrías del SDK de ArcGIS Pro.
/// </summary>
public static class HullsService
{
    /// <summary>
    /// Estructura simple para representar un segmento de línea entre dos puntos.
    /// </summary>
    private struct LineSegment
    {
        public MapPoint Start { get; }
        public MapPoint End { get; }

        public LineSegment(MapPoint start, MapPoint end)
        {
            Start = start;
            End = end;
        }
    }

    /// <summary>
    /// Compara dos MapPoint para determinar si son iguales (mismas coordenadas X, Y).
    /// </summary>
    private static bool MapPointEquals(MapPoint a, MapPoint b)
    {
        const double tolerance = 1e-9;
        return Math.Abs(a.X - b.X) < tolerance && Math.Abs(a.Y - b.Y) < tolerance;
    }

    /// <summary>
    /// Calcula la distancia euclidiana entre dos puntos.
    /// </summary>
    private static double Distance(MapPoint a, MapPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Encuentra los K vecinos más cercanos a un punto dado.
    /// </summary>
    private static List<MapPoint> KNearestNeighbors(List<MapPoint> points, MapPoint currentPoint, int k, out int actualK)
    {
        actualK = Math.Min(k, points.Count);
        var ret = points
            .Where(p => !MapPointEquals(p, currentPoint)) // Excluir el punto actual
            .OrderBy(p => Distance(currentPoint, p))
            .Take(actualK)
            .ToList();
        actualK = ret.Count; // Actualizar con el conteo real
        return ret;
    }

    /// <summary>
    /// Calcula el ángulo entre dos puntos en radianes.
    /// </summary>
    private static double Angle(MapPoint a, MapPoint b)
    {
        var ret = Math.Atan2(b.Y - a.Y, b.X - a.X);
        return NormaliseAngle(ret);
    }

    /// <summary>
    /// Normaliza un ángulo al rango [0, 2π).
    /// </summary>
    private static double NormaliseAngle(double a)
    {
        if (a < 0.0) { return a + Math.PI + Math.PI; }
        return a;
    }

    /// <summary>
    /// Ordena una lista de puntos por ángulo relativo a un punto y ángulo de referencia.
    /// </summary>
    private static List<MapPoint> SortByAngle(List<MapPoint> kNearest, MapPoint currentPoint, double angle)
    {
        kNearest.Sort((a, b) =>
        {
            var angleA = NormaliseAngle(Angle(currentPoint, a) - angle);
            var angleB = NormaliseAngle(Angle(currentPoint, b) - angle);
            return angleA > angleB ? -1 : 1;
        });
        return kNearest;
    }

    /// <summary>
    /// Determina si tres puntos están en sentido antihorario (CCW - Counter-Clockwise).
    /// </summary>
    private static bool CCW(MapPoint p1, MapPoint p2, MapPoint p3)
    {
        var cw = ((p3.Y - p1.Y) * (p2.X - p1.X)) - ((p2.Y - p1.Y) * (p3.X - p1.X));
        return cw > 0 ? true : cw < 0 ? false : true; // colinear 
    }

    /// <summary>
    /// Verifica si dos segmentos de línea se intersectan (excluyendo puntos compartidos en los extremos).
    /// </summary>
    private static bool _Intersect(LineSegment seg1, LineSegment seg2)
    {
        return CCW(seg1.Start, seg2.Start, seg2.End) != CCW(seg1.End, seg2.Start, seg2.End)
            && CCW(seg1.Start, seg1.End, seg2.Start) != CCW(seg1.Start, seg1.End, seg2.End);
    }

    /// <summary>
    /// Verifica si dos segmentos de línea se intersectan, ignorando si comparten extremos.
    /// </summary>
    private static bool Intersect(LineSegment seg1, LineSegment seg2)
    {
        if ((seg1.Start.X == seg2.Start.X && seg1.Start.Y == seg2.Start.Y)
            || (seg1.End.X == seg2.End.X && seg1.End.Y == seg2.End.Y))
        {
            return false;
        }
        return _Intersect(seg1, seg2);
    }

    /// <summary>
    /// Determina si un punto está dentro de un polígono usando el algoritmo de ray casting.
    /// </summary>
    private static bool IsPointInPolygon(List<MapPoint> polygon, MapPoint testPoint)
    {
        bool result = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
            if ((polygon[i].Y < testPoint.Y && polygon[j].Y >= testPoint.Y
                || polygon[j].Y < testPoint.Y && polygon[i].Y >= testPoint.Y)
                && (polygon[i].X <= testPoint.X || polygon[j].X <= testPoint.X))
            {
                if (polygon[i].X + (testPoint.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < testPoint.X)
                {
                    result = !result;
                }
            }
            j = i;
        }
        return result;
    }

    /// <summary>
    /// Calcula la envolvente cóncava (concave hull) de un conjunto de puntos usando el algoritmo K-Nearest Neighbors.
    /// </summary>
    /// <param name="points">Lista de puntos de entrada (MapPoint de ArcGIS).</param>
    /// <param name="k">Número inicial de vecinos más cercanos a considerar (mínimo 3).</param>
    /// <returns>Lista ordenada de puntos que forman la envolvente cóncava, o null si no es posible.</returns>
    public static List<MapPoint> KNearestConcaveHull(List<MapPoint> points, int k)
    {
        return KNearestConcaveHullInternal(points, k, 0);
    }

    /// <summary>
    /// Implementación interna recursiva con límite de profundidad.
    /// </summary>
    private static List<MapPoint> KNearestConcaveHullInternal(List<MapPoint> points, int k, int depth)
    {
        const int MAX_RECURSION_DEPTH = 50;

        if (points == null || points.Count < 3)
            return null;

        // Evitar recursión infinita
        if (depth > MAX_RECURSION_DEPTH)
        {
            System.Diagnostics.Debug.WriteLine($"[HullsService] Máxima profundidad de recursión alcanzada (k={k}). Abortando concave hull.");
            return null;
        }

        // Ordenar puntos por Y, luego por X
        var sortedPoints = points.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();

        var len = sortedPoints.Count;
        if (len == 3)
        {
            return new List<MapPoint>(sortedPoints);
        }

        // Limitar K al número de puntos disponibles - 1
        var kk = Math.Min(Math.Max(k, 3), len - 1);

        // Si K es demasiado grande, no tiene sentido continuar
        if (kk >= len - 1)
        {
            System.Diagnostics.Debug.WriteLine($"[HullsService] K muy grande ({kk}) para {len} puntos. Abortando concave hull.");
            return null;
        }

        // Crear conjunto de datos sin duplicados usando comparación de coordenadas
        var dataset = new List<MapPoint>();
        foreach (var pt in sortedPoints)
        {
            if (!dataset.Any(existing => MapPointEquals(existing, pt)))
            {
                dataset.Add(pt);
            }
        }

        if (dataset.Count < 3)
            return null;

        var hull = new List<MapPoint>();
        var firstPoint = dataset[0];
        hull.Add(firstPoint);

        var currentPoint = firstPoint;
        dataset.RemoveAt(0);

        double previousAngle = 0;
        int step = 2;
        int i;
        int iterations = 0;
        const int MAX_ITERATIONS = 10000;

        while ((!MapPointEquals(currentPoint, firstPoint) || step == 2) && dataset.Count > 0)
        {
            iterations++;
            if (iterations > MAX_ITERATIONS)
            {
                System.Diagnostics.Debug.WriteLine($"[HullsService] Máximo de iteraciones alcanzado. Abortando concave hull.");
                return null;
            }

            if (step == 5) { dataset.Add(firstPoint); }

            var kNearest = KNearestNeighbors(dataset, currentPoint, kk, out int actualK);

            if (kNearest.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[HullsService] No hay vecinos disponibles. Abortando concave hull.");
                return null;
            }

            var cPoints = SortByAngle(kNearest, currentPoint, previousAngle);

            var its = true;
            i = 0;

            while (its == true && i < cPoints.Count)
            {
                i++;
                int lastPoint = 0;
                if (MapPointEquals(cPoints[i - 1], firstPoint))
                {
                    lastPoint = 1;
                }

                int j = 2;
                its = false;

                while (its == false && j < hull.Count - lastPoint)
                {
                    var line1 = new LineSegment(hull[step - 2], cPoints[i - 1]);
                    var line2 = new LineSegment(hull[step - 2 - j], hull[step - 1 - j]);

                    its = Intersect(line1, line2);
                    j++;
                }
            }

            if (its == true)
            {
                // Recursivamente aumentar K si hay intersecciones
                return KNearestConcaveHullInternal(points, actualK + 1, depth + 1);
            }

            currentPoint = cPoints[i - 1];
            hull.Add(currentPoint);
            previousAngle = Angle(hull[step - 1], hull[step - 2]);

            // Remover currentPoint de dataset buscando por coordenadas
            for (int idx = 0; idx < dataset.Count; idx++)
            {
                if (MapPointEquals(dataset[idx], currentPoint))
                {
                    dataset.RemoveAt(idx);
                    break;
                }
            }

            step++;
        }

        // Verificar que todos los puntos restantes estén dentro del hull
        bool allInside = true;
        i = dataset.Count;
        while (allInside == true && i > 0)
        {
            allInside = IsPointInPolygon(hull, dataset[i - 1]);
            i--;
        }

        if (allInside == false)
        {
            // Recursivamente aumentar K si hay puntos fuera
            return KNearestConcaveHullInternal(points, kk + 1, depth + 1);
        }

        return hull;
    }

    /// <summary>
    /// Convierte una lista de MapPoint a un polígono de ArcGIS (Polygon).
    /// </summary>
    /// <param name="points">Lista de puntos que forman el perímetro del polígono.</param>
    /// <param name="spatialReference">Referencia espacial del polígono. Si es null, se usa la del primer punto.</param>
    /// <returns>Polygon de ArcGIS o null si la lista está vacía.</returns>
    public static Polygon CreatePolygonFromPoints(List<MapPoint> points, SpatialReference spatialReference = null)
    {
        var sr = spatialReference ?? points[0].SpatialReference;
        var builder = new PolygonBuilderEx(sr);

        // Crear anillo exterior
        var ringPoints = new List<MapPoint>(points);

        // Asegurar que el polígono esté cerrado
        if (!MapPointEquals(ringPoints[0], ringPoints[ringPoints.Count - 1]))
        {
            ringPoints.Add(ringPoints[0]);
        }

        builder.AddPart(ringPoints);
        return builder.ToGeometry();
    }
}
