using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Geometry;

namespace EAABAddIn.Core.Geometry
{
    /// <summary>
    /// Implementación de Concave Hull basado en Triangulación de Delaunay
    /// Algoritmo basado en: https://github.com/Logismos/concave-hull
    /// </summary>
    public class DelaunayConcaveHull
    {
        private readonly double _chi;
        private readonly List<MapPoint> _points;
        private DelaunayTriangulation _delaunay;

        /// <summary>
        /// Constructor del Concave Hull
        /// </summary>
        /// <param name="points">Lista de puntos para construir el hull</param>
        /// <param name="chi">Factor chi entre 0.0 (más cóncavo) y 1.0 (más convexo). Default: 0.1</param>
        public DelaunayConcaveHull(List<MapPoint> points, double chi = 0.1)
        {
            if (chi < 0.0 || chi > 1.0)
            {
                throw new ArgumentException("Chi factor must be between 0 and 1 inclusive");
            }

            if (points == null || points.Count < 3)
            {
                throw new ArgumentException("At least 3 points are required");
            }

            _chi = chi;
            _points = points;
        }

        /// <summary>
        /// Construye el Concave Hull y retorna el polígono resultante
        /// </summary>
        public Polygon BuildConcaveHull()
        {
            try
            {
                // 1. Crear triangulación de Delaunay usando ConvexHull inicial
                _delaunay = new DelaunayTriangulation(_points);

                // 2. Obtener índices del borde inicial (Convex Hull)
                var boundaryIndices = _delaunay.GetBoundaryIndices();
                var boundarySet = new HashSet<int>(boundaryIndices);

                // 3. Crear heap de bordes con sus longitudes
                var boundaryHeap = new List<EdgeLengthPair>();
                double minLen = double.MaxValue;
                double maxLen = double.MinValue;

                foreach (var edgeIndex in boundaryIndices)
                {
                    var edge = _delaunay.GetEdge(edgeIndex);
                    double length = CalculateDistance(_points[edge.Item1], _points[edge.Item2]);

                    boundaryHeap.Add(new EdgeLengthPair(edgeIndex, edge.Item1, edge.Item2, length));
                    
                    minLen = Math.Min(minLen, length);
                    maxLen = Math.Max(maxLen, length);
                }

                // Ordenar por longitud (mayor a menor)
                boundaryHeap.Sort((a, b) => b.Length.CompareTo(a.Length));

                // 4. Determinar parámetro de longitud basado en chi
                double lengthParam = _chi * maxLen + (1.0 - _chi) * minLen;

                Console.WriteLine($"[DelaunayConcaveHull] Chi={_chi:F2}, LengthParam={lengthParam:F2} (min={minLen:F2}, max={maxLen:F2})");

                // 5. Iterativamente agregar puntos al borde
                while (boundaryHeap.Count > 0)
                {
                    // Obtener el borde más largo
                    var edge = boundaryHeap[0];
                    boundaryHeap.RemoveAt(0);

                    // Si el borde es muy pequeño, terminar
                    if (edge.Length <= lengthParam)
                    {
                        break;
                    }

                    // Encontrar punto interior para este borde
                    int interiorPoint = _delaunay.GetInteriorPoint(edge.IndexA, edge.IndexB);

                    // Si no hay punto interior o ya está en el borde, continuar
                    if (interiorPoint < 0 || boundarySet.Contains(interiorPoint))
                    {
                        continue;
                    }

                    // Agregar nuevos bordes al heap
                    var newEdge1Length = CalculateDistance(_points[edge.IndexA], _points[interiorPoint]);
                    var newEdge2Length = CalculateDistance(_points[interiorPoint], _points[edge.IndexB]);

                    boundaryHeap.Add(new EdgeLengthPair(-1, edge.IndexA, interiorPoint, newEdge1Length));
                    boundaryHeap.Add(new EdgeLengthPair(-1, interiorPoint, edge.IndexB, newEdge2Length));

                    // Reordenar heap
                    boundaryHeap.Sort((a, b) => b.Length.CompareTo(a.Length));

                    // Actualizar el borde
                    _delaunay.UpdateBoundary(edge.IndexA, interiorPoint, edge.IndexB);
                    boundarySet.Add(interiorPoint);

                    Console.WriteLine($"[DelaunayConcaveHull] Agregado punto interior {interiorPoint}, boundary size: {boundarySet.Count}");
                }

                // 6. Construir polígono final con los índices del borde
                var finalBoundary = _delaunay.GetBoundaryIndices();
                var orderedPoints = finalBoundary.Select(idx => _points[idx]).ToList();

                Console.WriteLine($"[DelaunayConcaveHull] ✓ Hull construido con {orderedPoints.Count} vértices");

                // Crear polígono
                var builder = new PolygonBuilderEx(_points[0].SpatialReference);
                builder.AddPart(orderedPoints);

                return builder.ToGeometry();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DelaunayConcaveHull] ✗ Error: {ex.Message}");
                throw;
            }
        }

        private double CalculateDistance(MapPoint p1, MapPoint p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private class EdgeLengthPair
        {
            public int EdgeIndex { get; }
            public int IndexA { get; }
            public int IndexB { get; }
            public double Length { get; }

            public EdgeLengthPair(int edgeIndex, int indexA, int indexB, double length)
            {
                EdgeIndex = edgeIndex;
                IndexA = indexA;
                IndexB = indexB;
                Length = length;
            }
        }
    }

    /// <summary>
    /// Implementación simplificada de triangulación de Delaunay
    /// Para ArcGIS, usamos el Convex Hull y vamos agregando puntos interiores
    /// </summary>
    internal class DelaunayTriangulation
    {
        private readonly List<MapPoint> _points;
        private readonly List<int> _hullNext;
        private readonly List<int> _hullPrev;
        private readonly Dictionary<string, int> _edgeToInteriorPoint;

        public DelaunayTriangulation(List<MapPoint> points)
        {
            _points = points;
            _hullNext = new List<int>(new int[points.Count]);
            _hullPrev = new List<int>(new int[points.Count]);
            _edgeToInteriorPoint = new Dictionary<string, int>();

            // Inicializar con Convex Hull
            InitializeWithConvexHull();
        }

        private void InitializeWithConvexHull()
        {
            // Crear Convex Hull para obtener el borde inicial
            var multipoint = MultipointBuilderEx.CreateMultipoint(_points, _points[0].SpatialReference);
            var convexHull = GeometryEngine.Instance.ConvexHull(multipoint) as Polygon;

            if (convexHull == null || convexHull.PointCount == 0)
            {
                throw new InvalidOperationException("Failed to create convex hull");
            }

            // Obtener puntos del Convex Hull en orden
            var hullPoints = new List<MapPoint>();
            foreach (var point in convexHull.Points)
            {
                hullPoints.Add(point);
            }

            // Eliminar el último punto si es duplicado del primero
            if (hullPoints.Count > 1 && 
                Math.Abs(hullPoints[0].X - hullPoints[hullPoints.Count - 1].X) < 0.0001 &&
                Math.Abs(hullPoints[0].Y - hullPoints[hullPoints.Count - 1].Y) < 0.0001)
            {
                hullPoints.RemoveAt(hullPoints.Count - 1);
            }

            // Mapear puntos del hull a índices originales
            var hullIndices = new List<int>();
            foreach (var hullPoint in hullPoints)
            {
                int idx = FindClosestPointIndex(hullPoint);
                if (idx >= 0 && !hullIndices.Contains(idx))
                {
                    hullIndices.Add(idx);
                }
            }

            Console.WriteLine($"[DelaunayTriangulation] Convex hull inicial: {hullIndices.Count} puntos");

            // Inicializar conectividad del hull
            for (int i = 0; i < hullIndices.Count; i++)
            {
                int current = hullIndices[i];
                int next = hullIndices[(i + 1) % hullIndices.Count];
                int prev = hullIndices[(i - 1 + hullIndices.Count) % hullIndices.Count];

                _hullNext[current] = next;
                _hullPrev[current] = prev;
            }

            // Crear mapa de bordes a puntos interiores
            BuildInteriorPointMap(hullIndices);
        }

        private void BuildInteriorPointMap(List<int> hullIndices)
        {
            var hullSet = new HashSet<int>(hullIndices);

            // Para cada borde del hull, encontrar el punto interior más cercano
            for (int i = 0; i < hullIndices.Count; i++)
            {
                int a = hullIndices[i];
                int b = hullIndices[(i + 1) % hullIndices.Count];

                string edgeKey = GetEdgeKey(a, b);

                // Buscar punto interior más cercano a este borde
                int closestInterior = -1;
                double minDist = double.MaxValue;

                for (int p = 0; p < _points.Count; p++)
                {
                    if (hullSet.Contains(p)) continue;

                    // Calcular distancia perpendicular al borde
                    double dist = DistanceToEdge(_points[p], _points[a], _points[b]);
                    
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestInterior = p;
                    }
                }

                if (closestInterior >= 0)
                {
                    _edgeToInteriorPoint[edgeKey] = closestInterior;
                }
            }

            Console.WriteLine($"[DelaunayTriangulation] Mapeados {_edgeToInteriorPoint.Count} bordes a puntos interiores");
        }

        private double DistanceToEdge(MapPoint p, MapPoint a, MapPoint b)
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

        private double Distance(MapPoint p1, MapPoint p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private int FindClosestPointIndex(MapPoint target)
        {
            double minDist = double.MaxValue;
            int closestIdx = -1;

            for (int i = 0; i < _points.Count; i++)
            {
                double dist = Distance(_points[i], target);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestIdx = i;
                }
            }

            return closestIdx;
        }

        public List<int> GetBoundaryIndices()
        {
            var boundary = new List<int>();
            
            // Encontrar un punto inicial que esté en el borde
            int start = -1;
            for (int i = 0; i < _points.Count; i++)
            {
                if (_hullNext[i] != 0 || _hullPrev[i] != 0 || i == 0)
                {
                    // Este punto está en el borde
                    bool isInBoundary = false;
                    for (int j = 0; j < _points.Count; j++)
                    {
                        if (_hullNext[j] == i || _hullPrev[j] == i)
                        {
                            isInBoundary = true;
                            break;
                        }
                    }
                    
                    if (isInBoundary || (_hullNext[i] != 0))
                    {
                        start = i;
                        break;
                    }
                }
            }

            if (start < 0)
            {
                // Fallback: usar todos los puntos que tienen next definido
                for (int i = 0; i < _points.Count; i++)
                {
                    if (_hullNext[i] != 0 || i == 0)
                    {
                        start = i;
                        break;
                    }
                }
            }

            if (start < 0) return boundary;

            // Recorrer el borde siguiendo los enlaces next
            int current = start;
            var visited = new HashSet<int>();

            do
            {
                boundary.Add(current);
                visited.Add(current);
                current = _hullNext[current];

                // Evitar bucles infinitos
                if (visited.Count > _points.Count) break;

            } while (current != start && current >= 0 && !visited.Contains(current));

            return boundary;
        }

        public Tuple<int, int> GetEdge(int edgeIndex)
        {
            // En esta implementación simplificada, el edgeIndex no se usa directamente
            // Se calcula en tiempo real desde el boundary
            var boundary = GetBoundaryIndices();
            if (edgeIndex >= boundary.Count) return null;

            int a = boundary[edgeIndex];
            int b = boundary[(edgeIndex + 1) % boundary.Count];

            return Tuple.Create(a, b);
        }

        public int GetInteriorPoint(int indexA, int indexB)
        {
            string edgeKey = GetEdgeKey(indexA, indexB);
            
            if (_edgeToInteriorPoint.TryGetValue(edgeKey, out int interiorPoint))
            {
                return interiorPoint;
            }

            // Si no existe, buscar el más cercano no visitado
            var boundary = new HashSet<int>(GetBoundaryIndices());
            
            double minDist = double.MaxValue;
            int closest = -1;

            for (int p = 0; p < _points.Count; p++)
            {
                if (boundary.Contains(p)) continue;

                double dist = DistanceToEdge(_points[p], _points[indexA], _points[indexB]);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = p;
                }
            }

            return closest;
        }

        public void UpdateBoundary(int indexA, int interiorPoint, int indexB)
        {
            // Actualizar conectividad: A -> Interior -> B
            _hullNext[indexA] = interiorPoint;
            _hullPrev[interiorPoint] = indexA;
            
            _hullNext[interiorPoint] = indexB;
            _hullPrev[indexB] = interiorPoint;

            // Actualizar mapa de puntos interiores para los nuevos bordes
            string edgeKey1 = GetEdgeKey(indexA, interiorPoint);
            string edgeKey2 = GetEdgeKey(interiorPoint, indexB);

            // Buscar nuevos puntos interiores para estos bordes
            var currentBoundary = new HashSet<int>(GetBoundaryIndices());

            for (int p = 0; p < _points.Count; p++)
            {
                if (currentBoundary.Contains(p)) continue;

                double dist1 = DistanceToEdge(_points[p], _points[indexA], _points[interiorPoint]);
                double dist2 = DistanceToEdge(_points[p], _points[interiorPoint], _points[indexB]);

                if (!_edgeToInteriorPoint.ContainsKey(edgeKey1) || dist1 < DistanceToEdge(_points[_edgeToInteriorPoint[edgeKey1]], _points[indexA], _points[interiorPoint]))
                {
                    _edgeToInteriorPoint[edgeKey1] = p;
                }

                if (!_edgeToInteriorPoint.ContainsKey(edgeKey2) || dist2 < DistanceToEdge(_points[_edgeToInteriorPoint[edgeKey2]], _points[interiorPoint], _points[indexB]))
                {
                    _edgeToInteriorPoint[edgeKey2] = p;
                }
            }
        }

        private string GetEdgeKey(int a, int b)
        {
            // Crear clave única para el borde (orden independiente)
            return a < b ? $"{a}-{b}" : $"{b}-{a}";
        }
    }
}
