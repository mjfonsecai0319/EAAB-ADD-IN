using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using EAABAddIn.Src.Core.Entities;

namespace EAABAddIn.Src.Core.Map
{
    public static class ResultsLayerService
    {
        private static FeatureLayer _addressPointLayer;
        private const string FeatureClassName = "GeocodedAddresses";

        private static readonly List<PtAddressGralEntity> _pendingEntities = new();
        private static readonly object _pendingLock = new();

        public static Task AddPointAsync(PtAddressGralEntity entidad, string gdbPath = null, bool skipDuplicates = true)
        {
            return QueuedTask.Run(() => _AddPointAsync(entidad, gdbPath, skipDuplicates));
        }

        private static async Task _AddPointAsync(PtAddressGralEntity entidad, string gdbPath, bool skipDuplicates)
        {
            var mapView = MapView.Active;

            if (string.IsNullOrEmpty(gdbPath) || !Directory.Exists(gdbPath))
            {
                gdbPath = Project.Current.DefaultGeodatabasePath;
            }

            if (!Directory.Exists(gdbPath))
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: "La geodatabase predeterminada no está configurada, no existe o la ruta especificada no es válida. Por favor, verifique la configuración del proyecto y asegúrese de que la geodatabase esté accesible.",
                    caption: "Error - Geodatabase no válida"
                );
                return;
            }

            if (mapView?.Map == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: "No hay un mapa activo en ArcGIS Pro. Por favor, asegúrese de tener un proyecto abierto y un mapa visible antes de intentar agregar puntos de resultados.",
                    caption: "Error - Mapa no activo"
                );
                return;
            }

            if (!await _CreateFeatureClassIfNotExist(gdbPath))
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: "No se pudo crear la clase de entidad 'GeocodedAddresses' en la geodatabase especificada. Esto puede deberse a un problema de permisos, un bloqueo de esquema, o a que la geodatabase está dañada o inaccesible. Verifique que la ruta sea válida, que tenga permisos de escritura y que ningún otro proceso esté bloqueando la geodatabase.",
                    caption: "Error - Clase de entidad no creada"
                );
                return;
            }

            await EnsureFieldsExist(gdbPath);
            _AddFeatureClassToMapView(gdbPath, mapView);

            if (_addressPointLayer is not null)
            {
                if (!entidad.Latitud.HasValue || !entidad.Longitud.HasValue)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        messageText: "La entidad no tiene coordenadas válidas o no existen valores de latitud y/o longitud. No se puede agregar el punto al mapa.",
                        caption: "Error - Coordenadas inválidas o inexistentes"
                    );
                    return;
                }

                var mapPoint = MapPointBuilderEx.CreateMapPoint(
                    (double)entidad.Longitud.Value,
                    (double)entidad.Latitud.Value,
                    SpatialReferences.WGS84
                );

                var featureClass = _addressPointLayer.GetTable() as FeatureClass;

                if (featureClass == null)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        messageText: "No se pudo acceder a la FeatureClass de resultados. Esto puede deberse a un bloqueo de esquema, a que la conexión a la geodatabase quedó en un estado inestable o a un problema interno de ArcGIS Pro. Guarde su trabajo y reinicie ArcGIS Pro para liberar los bloqueos e intente nuevamente.",
                        caption: "Error - Reinicie ArcGIS Pro"
                    );
                    return;
                }

                try
                {
                    // Nueva detección de duplicados: búsqueda espacial muy pequeña + comparación de dirección
                    if (skipDuplicates)
                    {
                        var addrCandidate = entidad.FullAddressEAAB ?? entidad.FullAddressCadastre ?? entidad.MainStreet ?? string.Empty;
                        if (SpatialFeatureExists(featureClass, mapPoint, addrCandidate))
                        {
                            await _ZoomSingleAsync(mapView, mapPoint); // garantizar zoom aunque ya exista
                            return;
                        }
                    }

                    using (var rowBuffer = featureClass.CreateRowBuffer())
                    {
                        var def = featureClass.GetDefinition();
                        rowBuffer[def.GetShapeField()] = mapPoint;
                        var direccionOriginal = !string.IsNullOrWhiteSpace(entidad.FullAddressOld)
                            ? entidad.FullAddressOld
                            : (entidad.MainStreet ?? entidad.FullAddressEAAB ?? entidad.FullAddressCadastre ?? string.Empty);
                        rowBuffer["Direccion"] = direccionOriginal;
                        rowBuffer["Poblacion"] = entidad.CityDesc ?? entidad.CityCode ?? string.Empty;
                        rowBuffer["FullAdressEAAB"] = entidad.FullAddressEAAB ?? string.Empty;
                        rowBuffer["FullAdressUACD"] = entidad.FullAddressCadastre ?? string.Empty;
                        rowBuffer["Geocoder"] = string.IsNullOrWhiteSpace(entidad.Source) ? "EAAB" : entidad.Source;
                        rowBuffer["Score"] = entidad.Score.HasValue ? entidad.Score.Value : null;
                        rowBuffer["ScoreText"] = entidad.ScoreText ?? string.Empty;
                        if (def.GetFields().Any(f => f.Name.Equals("FechaHora", StringComparison.OrdinalIgnoreCase)))
                            rowBuffer["FechaHora"] = DateTime.Now;
                        using (var row = featureClass.CreateRow(rowBuffer)) { }
                    }
                    _addressPointLayer?.ClearDisplayCache();
                }
                catch (Exception ex)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        messageText: $"Error al agregar el punto al mapa: {ex.Message}",
                        caption: "Error - Inserción de punto"
                    );
                    return;
                }

                // Zoom suave centrado en el punto recién agregado (buffer pequeño)
                try
                {
                    var currentSR = mapView.Map.SpatialReference ?? SpatialReferences.WGS84;
                    MapPoint mpProjected = mapPoint;
                    if (!mapPoint.SpatialReference.Equals(currentSR))
                    {
                        mpProjected = (MapPoint)GeometryEngine.Instance.Project(mapPoint, currentSR);
                    }

                    // Definir un buffer en metros (aprox) dependiendo de la referencia espacial
                    // Si el SR es geográfico (WGS84) usamos ~0.00045 grados (~50m). Si es proyectado, 50 metros.
                    Envelope env;
                    if (currentSR.IsGeographic)
                    {
                        const double delta = 0.00045; // ~50 m en lat media
                        env = EnvelopeBuilderEx.CreateEnvelope(mpProjected.X - delta, mpProjected.Y - delta, mpProjected.X + delta, mpProjected.Y + delta, currentSR);
                    }
                    else
                    {
                        const double delta = 50; // 50 m
                        env = EnvelopeBuilderEx.CreateEnvelope(mpProjected.X - delta, mpProjected.Y - delta, mpProjected.X + delta, mpProjected.Y + delta, currentSR);
                    }

                    await mapView.ZoomToAsync(env, TimeSpan.FromSeconds(0.6));
                }
                catch
                {
                    // Si el zoom personalizado falla, fallback al extent de la capa
                    await mapView.ZoomToAsync(_addressPointLayer.QueryExtent(), TimeSpan.FromSeconds(1));
                }
            }
            else
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: "La capa de resultados no está disponible o no es editable.",
                    caption: "Error - Capa no disponible o editable"
                );
            }
        }

        private async static Task<bool> _CreateFeatureClassIfNotExist(string path)
        {
            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(path));
            var exists = false;

            using (var geodatabase = new Geodatabase(gdbConnectionPath))
            {
                exists = geodatabase
                    .GetDefinitions<FeatureClassDefinition>()
                    .Any(d => string.Equals(d.GetName(), FeatureClassName, StringComparison.OrdinalIgnoreCase));
            }

            if (exists) return true;

            var parameters = Geoprocessing.MakeValueArray(
                path,
                FeatureClassName,
                "POINT",
                "",
                "DISABLED",
                "DISABLED",
                4326
            );
            var gpResult = await Geoprocessing.ExecuteToolAsync(
                "management.CreateFeatureclass",
                parameters,
                null,
                CancelableProgressor.None,
                GPExecuteToolFlags.AddToHistory
            );
            return !gpResult.IsFailed;
        }

        private static void _AddFeatureClassToMapView(string path, MapView mapView)
        {
            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(path));

            if (_addressPointLayer is null && mapView?.Map is not null)
            {
                var existing = mapView.Map
                    .GetLayersAsFlattenedList()
                    .OfType<FeatureLayer>()
                    .FirstOrDefault(fl =>
                    {
                        if (string.Equals(fl.Name, FeatureClassName, StringComparison.OrdinalIgnoreCase))
                            return true;

                        try
                        {
                            using var fc = fl.GetFeatureClass();
                            return string.Equals(fc?.GetName(), FeatureClassName, StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    });

                if (existing is not null)
                {
                    _addressPointLayer = existing;
                    return; // ya existe en el mapa, reutilizar
                }
            }

            if (_addressPointLayer is not null)
            {
                return; // ya existe
            }

            using (var geodatabase = new Geodatabase(gdbConnectionPath))
            using (var featureClass = geodatabase.OpenDataset<FeatureClass>(FeatureClassName))
            {
                var layerParams = new FeatureLayerCreationParams(featureClass)
                {
                    Name = FeatureClassName
                };
                _addressPointLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, mapView.Map);
            }
        }


        public static void ClearPending()
        {
            lock (_pendingLock)
            {
                _pendingEntities.Clear();
            }
        }

        public static void AddPointToMemory(PtAddressGralEntity entidad)
        {
            if (entidad == null) return;
            lock (_pendingLock)
            {
                _pendingEntities.Add(entidad);
            }
        }

        public static void AddPointsToMemory(IEnumerable<PtAddressGralEntity> entidades)
        {
            if (entidades == null) return;
            lock (_pendingLock)
            {
                _pendingEntities.AddRange(entidades);
            }
        }

        public static Task CommitPointsAsync(string gdbPath)
        {
            return QueuedTask.Run(() => _CommitPointsInternal(gdbPath, true));
        }

        /// <summary>
        /// Commit batch de puntos. Permite suprimir el zoom automático al extent de la capa.
        /// </summary>
        public static Task CommitPointsAsync(string gdbPath, bool zoomExtent)
        {
            return QueuedTask.Run(() => _CommitPointsInternal(gdbPath, zoomExtent));
        }

        /// <summary>
        /// Elimina todas las entidades de la capa GeocodedAddresses (si existe) y opcionalmente hace refresh visual.
        /// </summary>
        public static Task ClearLayerAsync(string gdbPath, bool refresh = true)
        {
            return QueuedTask.Run(() => _ClearLayerInternal(gdbPath, refresh));
        }

        private static void _ClearLayerInternal(string gdbPath, bool refresh)
        {
            var mapView = MapView.Active;
            if (mapView?.Map == null) return;
            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            using var geodatabase = new Geodatabase(gdbConnectionPath);
            FeatureClass fc = null;
            try { fc = geodatabase.OpenDataset<FeatureClass>(FeatureClassName); } catch { }
            if (fc == null) return;
            // Crear lista de OIDs para borrado seguro
            var oids = new List<long>();
            var oidField = fc.GetDefinition().GetObjectIDField();
            using (var cursor = fc.Search(new QueryFilter { SubFields = oidField }, false))
            {
                while (cursor.MoveNext())
                {
                    using var row = cursor.Current as Feature;
                    if (row != null) oids.Add(row.GetObjectID());
                }
            }
            if (oids.Count == 0) return;
            var editOperation = new EditOperation { Name = "Limpiar GeocodedAddresses" };
            editOperation.Delete(fc, oids);
            editOperation.Execute();
            if (refresh && _addressPointLayer != null)
                _addressPointLayer.ClearDisplayCache();
        }

        private static async Task _CommitPointsInternal(string gdbPath, bool zoomExtent)
        {
            List<PtAddressGralEntity> toInsert;
            lock (_pendingLock)
            {
                if (!_pendingEntities.Any()) return;
                toInsert = _pendingEntities.ToList();
                _pendingEntities.Clear(); // limpiamos para evitar duplicados
            }

            var mapView = MapView.Active;
            if (mapView?.Map == null || toInsert == null || toInsert.Count == 0) return;

            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));

            bool exists;
            using (var geodatabase = new Geodatabase(gdbConnectionPath))
            {
                exists = geodatabase
                    .GetDefinitions<FeatureClassDefinition>()
                    .Any(d => string.Equals(d.GetName(), FeatureClassName, StringComparison.OrdinalIgnoreCase));
            }
            if (!exists)
            {
                var parameters = Geoprocessing.MakeValueArray(
                    gdbPath,
                    FeatureClassName,
                    "POINT",
                    "",
                    "DISABLED",
                    "DISABLED",
                    4326
                );
                var result = await Geoprocessing.ExecuteToolAsync(
                    "management.CreateFeatureclass",
                    parameters,
                    null,
                    CancelableProgressor.None,
                    GPExecuteToolFlags.AddToHistory);
                if (result.IsFailed) return;
            }

            await EnsureFieldsExist(gdbPath);

            if (_addressPointLayer == null)
            {
                using (var geodatabase = new Geodatabase(gdbConnectionPath))
                using (var featureClass = geodatabase.OpenDataset<FeatureClass>(FeatureClassName))
                {
                    var layerParams = new FeatureLayerCreationParams(featureClass)
                    {
                        Name = FeatureClassName
                    };
                    _addressPointLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, mapView.Map);
                }
            }

            if (_addressPointLayer == null) return;

            var batchFc = _addressPointLayer.GetTable() as FeatureClass;
            if (batchFc != null)
            {
                try
                {
                    // Variables para envelope de puntos nuevos
                    double? minX = null, minY = null, maxX = null, maxY = null;
                    // Construir índice de duplicados existentes (posición + dirección EAAB) para evitar insertar repetidos si ya existen
                    var existingSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        var shapeField = batchFc.GetDefinition().GetShapeField();
                        using var cur = batchFc.Search(new QueryFilter { SubFields = shapeField + ", FullAdressEAAB, Direccion" }, false);
                        while (cur.MoveNext())
                        {
                            using var r = cur.Current;
                            if (r[shapeField] is MapPoint mpEx)
                            {
                                var addrEx = (r["FullAdressEAAB"]?.ToString() ?? r["Direccion"]?.ToString() ?? string.Empty).Trim();
                                var sig = BuildSignature(mpEx.X, mpEx.Y, addrEx);
                                existingSignatures.Add(sig);
                            }
                        }
                    }
                    catch { }
                    foreach (var entidad in toInsert)
                    {
                        if (!entidad.Latitud.HasValue || !entidad.Longitud.HasValue) continue;
                        var mapPoint = MapPointBuilderEx.CreateMapPoint(
                            (double)entidad.Longitud.Value,
                            (double)entidad.Latitud.Value,
                            SpatialReferences.WGS84
                        );
                        var addrIns = entidad.FullAddressEAAB ?? entidad.FullAddressCadastre ?? entidad.MainStreet ?? string.Empty;
                        var signature = BuildSignature(mapPoint.X, mapPoint.Y, addrIns);
                        if (existingSignatures.Contains(signature))
                            continue; // duplicado
                        existingSignatures.Add(signature);
                        // Expandir bounds
                        if (minX == null || mapPoint.X < minX) minX = mapPoint.X;
                        if (maxX == null || mapPoint.X > maxX) maxX = mapPoint.X;
                        if (minY == null || mapPoint.Y < minY) minY = mapPoint.Y;
                        if (maxY == null || mapPoint.Y > maxY) maxY = mapPoint.Y;
                        using (var rowBuffer = batchFc.CreateRowBuffer())
                        {
                            var def = batchFc.GetDefinition();
                            rowBuffer[def.GetShapeField()] = mapPoint;
                            rowBuffer["Identificador"] = entidad.ID.ToString();
                            var direccionFinal = !string.IsNullOrWhiteSpace(entidad.FullAddressEAAB)
                                ? entidad.FullAddressEAAB
                                : (!string.IsNullOrWhiteSpace(entidad.FullAddressCadastre)
                                    ? entidad.FullAddressCadastre
                                    : (!string.IsNullOrWhiteSpace(entidad.FullAddressOld)
                                        ? entidad.FullAddressOld
                                        : (entidad.MainStreet ?? string.Empty)));
                            rowBuffer["Direccion"] = direccionFinal;
                            rowBuffer["Poblacion"] = entidad.CityDesc ?? entidad.CityCode ?? string.Empty;
                            rowBuffer["FullAdressEAAB"] = entidad.FullAddressEAAB ?? string.Empty;
                            rowBuffer["FullAdressUACD"] = entidad.FullAddressCadastre ?? string.Empty;
                            rowBuffer["Geocoder"] = string.IsNullOrWhiteSpace(entidad.Source) ? "EAAB" : entidad.Source;
                            if (entidad.Score.HasValue) rowBuffer["Score"] = entidad.Score.Value; else rowBuffer["Score"] = null;
                            rowBuffer["ScoreText"] = entidad.ScoreText ?? string.Empty;
                            if (def.GetFields().Any(f => f.Name.Equals("FechaHora", StringComparison.OrdinalIgnoreCase)))
                                rowBuffer["FechaHora"] = DateTime.Now;
                            using (var row = batchFc.CreateRow(rowBuffer)) { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"Error en inserción en lote: {ex.Message}");
                    return;
                }
                if (zoomExtent)
                {
                    try
                    {
                        // NOTA: las variables de bounds se declararon dentro del try anterior; necesitamos capturarlas aquí.
                        // Recalculamos rápidamente si fuera necesario (en caso de excepción previa). Si no hubo inserciones, zoomExtent caerá al extent capa.
                        double? locMinX = null, locMinY = null, locMaxX = null, locMaxY = null;
                        if (!_pendingEntities.Any()) // usamos 'toInsert' local para recomputar
                        {
                            foreach (var entidad in toInsert)
                            {
                                if (!entidad.Latitud.HasValue || !entidad.Longitud.HasValue) continue;
                                var x = (double)entidad.Longitud.Value;
                                var y = (double)entidad.Latitud.Value;
                                if (locMinX == null || x < locMinX) locMinX = x;
                                if (locMaxX == null || x > locMaxX) locMaxX = x;
                                if (locMinY == null || y < locMinY) locMinY = y;
                                if (locMaxY == null || y > locMaxY) locMaxY = y;
                            }
                        }
                        if (locMinX.HasValue && locMinY.HasValue && locMaxX.HasValue && locMaxY.HasValue)
                        {
                            var sr = mapView.Map?.SpatialReference ?? SpatialReferences.WGS84;
                            var dx = (locMaxX.Value - locMinX.Value) * 0.05;
                            var dy = (locMaxY.Value - locMinY.Value) * 0.05;
                            var env = EnvelopeBuilderEx.CreateEnvelope(locMinX.Value - dx, locMinY.Value - dy, locMaxX.Value + dx, locMaxY.Value + dy, SpatialReferences.WGS84);
                            // Reproyectar si el mapa está en otro SR
                            if (sr != null && !sr.Equals(SpatialReferences.WGS84))
                            {
                                var proj = GeometryEngine.Instance.Project(env, sr) as Envelope;
                                if (proj != null) env = proj;
                            }
                            await mapView.ZoomToAsync(env, TimeSpan.FromSeconds(0.8));
                        }
                        else
                        {
                            await mapView.ZoomToAsync(_addressPointLayer.QueryExtent(), TimeSpan.FromSeconds(1));
                        }
                    }
                    catch
                    {
                        await mapView.ZoomToAsync(_addressPointLayer.QueryExtent(), TimeSpan.FromSeconds(1));
                    }
                }
            }
        }

        private static async Task EnsureFieldsExist(string gdbPath)
        {
            var required = new (string Name, string Type, string Length)[]
            {
                ("Identificador", "TEXT", "100"),
                ("Direccion", "TEXT", "255"),
                ("Poblacion", "TEXT", "100"),
                ("FullAdressEAAB", "TEXT", "255"),
                ("FullAdressUACD", "TEXT", "255"),
                ("Geocoder", "TEXT", "100"),
                ("Score", "DOUBLE", ""),
                ("ScoreText", "TEXT", "100"),
                ("FechaHora", "DATE", "")
            };

            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            HashSet<string> existingFieldNames;
            using (var gdb = new Geodatabase(gdbConnectionPath))
            using (var fc = gdb.OpenDataset<FeatureClass>(FeatureClassName))
            {
                existingFieldNames = fc
                    .GetDefinition()
                    .GetFields()
                    .Select(f => f.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var field in required)
            {
                if (existingFieldNames.Contains(field.Name))
                    continue;

                var tablePath = Path.Combine(gdbPath, FeatureClassName);
                var addFieldParams = Geoprocessing.MakeValueArray(
                    tablePath,
                    field.Name,
                    field.Type,
                    "",
                    "",
                    field.Length
                );
                var result = await Geoprocessing.ExecuteToolAsync(
                    "management.AddField",
                    addFieldParams,
                    null,
                    CancelableProgressor.None,
                    GPExecuteToolFlags.AddToHistory);
            }
        }

        #region Helpers Duplicados / Zoom
        private static string BuildSignature(double x, double y, string address)
        {
            var addrNorm = (address ?? string.Empty).Trim().ToUpperInvariant();
            // Redondear coords para agrupar duplicados casi idénticos
            var xr = Math.Round(x, 7); // ~1cm
            var yr = Math.Round(y, 7);
            return xr + "|" + yr + "|" + addrNorm;
        }

        private static bool SpatialFeatureExists(FeatureClass fc, MapPoint point, string address)
        {
            try
            {
                var def = fc.GetDefinition();
                var shapeField = def.GetShapeField();
                // Envelope muy pequeño (aprox 5 cm en grados) para buscar coincidencias cercanas
                const double delta = 0.0000005; // ~5 cm
                var env = EnvelopeBuilderEx.CreateEnvelope(point.X - delta, point.Y - delta, point.X + delta, point.Y + delta, point.SpatialReference);
                var sqf = new SpatialQueryFilter
                {
                    FilterGeometry = env,
                    SpatialRelationship = SpatialRelationship.Intersects,
                    SubFields = shapeField + ", FullAdressEAAB, Direccion"
                };
                var addrNorm = (address ?? string.Empty).Trim().ToUpperInvariant();
                using var cursor = fc.Search(sqf, false);
                while (cursor.MoveNext())
                {
                    using var row = cursor.Current;
                    var addrEx = (row["FullAdressEAAB"]?.ToString() ?? row["Direccion"]?.ToString() ?? string.Empty).Trim().ToUpperInvariant();
                    if (addrEx == addrNorm) return true;
                }
            }
            catch { }
            return false;
        }

        private static async Task _ZoomSingleAsync(MapView mv, MapPoint mp)
        {
            try
            {
                var sr = mv.Map?.SpatialReference ?? SpatialReferences.WGS84;
                MapPoint mpProj = mp;
                if (!mp.SpatialReference.Equals(sr)) mpProj = (MapPoint)GeometryEngine.Instance.Project(mp, sr);
                Envelope env;
                if (sr.IsGeographic)
                {
                    const double delta = 0.00045; // ~50m
                    env = EnvelopeBuilderEx.CreateEnvelope(mpProj.X - delta, mpProj.Y - delta, mpProj.X + delta, mpProj.Y + delta, sr);
                }
                else
                {
                    const double delta = 50; // 50 m
                    env = EnvelopeBuilderEx.CreateEnvelope(mpProj.X - delta, mpProj.Y - delta, mpProj.X + delta, mpProj.Y + delta, sr);
                }
                await mv.ZoomToAsync(env, TimeSpan.FromSeconds(0.6));
            }
            catch { }
        }
        #endregion

    }
}