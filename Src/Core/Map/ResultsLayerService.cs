using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        // Lista temporal para acumular resultados en memoria
        private static readonly List<PtAddressGralEntity> _pendingEntities = new();
        private static readonly object _pendingLock = new();

        // --- Compatibilidad: método original (inserta un único punto inmediatamente) ---
        public static Task AddPointAsync(PtAddressGralEntity entidad)
        {
            return QueuedTask.Run(() => _AddPointAsync(entidad));
        }

        private static async Task _AddPointAsync(PtAddressGralEntity entidad)
        {
            var mapView = MapView.Active;
            if (mapView?.Map == null) return;

            // Buscar capa existente en el mapa
            _addressPointLayer = mapView.Map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .FirstOrDefault(l => l.GetTable()?.GetName() == FeatureClassName);

            var gdbPath = Project.Current.DefaultGeodatabasePath;
            if (string.IsNullOrEmpty(gdbPath) || !Directory.Exists(Path.GetDirectoryName(gdbPath)))
                return;

            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));

            // Crear FeatureClass si no existe
            using (var geodatabase = new Geodatabase(gdbConnectionPath))
            {
                if (geodatabase.GetDefinitions<FeatureClassDefinition>().All(d => d.GetName() != FeatureClassName))
                {
                    var parameters = Geoprocessing.MakeValueArray(
                        gdbPath,
                        FeatureClassName,
                        "POINT",
                        "",
                        "DISABLED",
                        "DISABLED",
                        SpatialReferences.WGS84
                    );
                    var result = await Geoprocessing.ExecuteToolAsync("management.CreateFeatureclass", parameters);
                    if (result.IsFailed) return;
                }
            }

            // Validar y agregar campos si faltan
            await EnsureFieldsExist(gdbPath);

            // Agregar la capa al mapa si no está
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

            // Crear punto y atributos
            var mapPoint = MapPointBuilderEx.CreateMapPoint(
                (double)entidad.Longitud.Value,
                (double)entidad.Latitud.Value,
                SpatialReferences.WGS84
            );

            var scoreText = "Desconocido";

            if (entidad.Source == "CATASTRO")
                scoreText = "Aproximada por Catastro";
            else if (entidad.Source == "EAAB")
                scoreText = "Exacta";
            else if (entidad.Source == "ESRI")
                scoreText = $"ESRI {entidad.Score}";

            var attributes = new Dictionary<string, object>
            {
                { "Shape", mapPoint },
                { "Identificador", entidad.ID.ToString() },
                { "Direccion", entidad.FullAddressOld ?? entidad.MainStreet },
                { "Poblacion", entidad.CityDesc ?? entidad.CityCode },
                { "FullAdressEAAB", entidad.FullAddressEAAB },
                { "FullAdressUACD", entidad.FullAddressCadastre },
                { "Geocoder", string.IsNullOrWhiteSpace(entidad.Source) ? "EAAB" : entidad.Source },
                { "Score", entidad.Score ?? (object)DBNull.Value },
                { "ScoreText", entidad.ScoreText ?? string.Empty }

            };


            var createOperation = new EditOperation { Name = "Add Geocoded Point" };
            createOperation.Create(_addressPointLayer, attributes);

            var success = await createOperation.ExecuteAsync();
            if (success)
                await mapView.ZoomToAsync(_addressPointLayer.QueryExtent(), TimeSpan.FromSeconds(1));
        }

        // ------------------- Batching en memoria -------------------

        /// <summary> Borra la lista pendiente (usar antes de un nuevo proceso masivo). </summary>
        public static void ClearPending()
        {
            lock (_pendingLock)
            {
                _pendingEntities.Clear();
            }
        }

        /// <summary> Agrega una entidad a la lista en memoria (thread-safe). </summary>
        public static void AddPointToMemory(PtAddressGralEntity entidad)
        {
            if (entidad == null) return;
            lock (_pendingLock)
            {
                _pendingEntities.Add(entidad);
            }
        }

        /// <summary> Agrega múltiples entidades a la lista en memoria (thread-safe). </summary>
        public static void AddPointsToMemory(IEnumerable<PtAddressGralEntity> entidades)
        {
            if (entidades == null) return;
            lock (_pendingLock)
            {
                _pendingEntities.AddRange(entidades);
            }
        }

        /// <summary> Inserta todos los puntos acumulados en una sola operación (rápido). </summary>
        public static Task CommitPointsAsync()
        {
            return QueuedTask.Run(() => _CommitPointsInternal());
        }

        private static async Task _CommitPointsInternal()
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

            var gdbPath = Project.Current.DefaultGeodatabasePath;
            if (string.IsNullOrEmpty(gdbPath) || !Directory.Exists(Path.GetDirectoryName(gdbPath)))
                return;

            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));

            // Crear FeatureClass si no existe
            using (var geodatabase = new Geodatabase(gdbConnectionPath))
            {
                if (geodatabase.GetDefinitions<FeatureClassDefinition>().All(d => d.GetName() != FeatureClassName))
                {
                    var parameters = Geoprocessing.MakeValueArray(
                        gdbPath,
                        FeatureClassName,
                        "POINT",
                        "",
                        "DISABLED",
                        "DISABLED",
                        SpatialReferences.WGS84
                    );
                    var result = await Geoprocessing.ExecuteToolAsync("management.CreateFeatureclass", parameters);
                    if (result.IsFailed) return;
                }
            }

            // Validar campos
            await EnsureFieldsExist(gdbPath);

            // Crear capa si no existe
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

            // Crear una sola operación para todos los puntos
            var createOperation = new EditOperation { Name = "Add Geocoded Points Batch" };

            foreach (var entidad in toInsert)
            {
                // seguridad por si alguno quedó sin coordenadas (evitar exception)
                if (!entidad.Latitud.HasValue || !entidad.Longitud.HasValue) continue;

                var mapPoint = MapPointBuilderEx.CreateMapPoint(
                    (double)entidad.Longitud.Value,
                    (double)entidad.Latitud.Value,
                    SpatialReferences.WGS84
                );

                var attributes = new Dictionary<string, object>
                {
                    { "Shape", mapPoint },
                    { "Identificador", entidad.ID.ToString() },
                    { "Direccion", entidad.FullAddressOld ?? entidad.MainStreet },
                    { "Poblacion", entidad.CityDesc ?? entidad.CityCode },
                    { "FullAdressEAAB", entidad.FullAddressEAAB },
                    { "FullAdressUACD", entidad.FullAddressCadastre },
                    { "Geocoder", string.IsNullOrWhiteSpace(entidad.Source) ? "EAAB" : entidad.Source },
                    { "Score", entidad.Score ?? (object)DBNull.Value },
                    { "ScoreText", entidad.ScoreText ?? string.Empty }

                };

                createOperation.Create(_addressPointLayer, attributes);
            }

            var success = await createOperation.ExecuteAsync();
            if (success)
            {
                // Zoom una sola vez después de insertar todos los puntos
                await mapView.ZoomToAsync(_addressPointLayer.QueryExtent(), TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// Asegura que los campos requeridos existan en la FeatureClass.
        /// Nota: Score ahora es TEXT para aceptar texto o números.
        /// </summary>
        private static async Task EnsureFieldsExist(string gdbPath)
        {
            var fields = new[]
            {
                new { Name = "Identificador", Type = "TEXT", Length = "100" },
                new { Name = "Direccion", Type = "TEXT", Length = "255" },
                new { Name = "Poblacion", Type = "TEXT", Length = "100" },
                new { Name = "FullAdressEAAB", Type = "TEXT", Length = "255" },
                new { Name = "FullAdressUACD", Type = "TEXT", Length = "255" },
                new { Name = "Geocoder", Type = "TEXT", Length = "100" },
                new { Name = "Score", Type = "DOUBLE", Length = "" },   // numérico
                new { Name = "ScoreText", Type = "TEXT", Length = "100" } // alfanumérico nuevo
            };

            var tablePath = Path.Combine(gdbPath, FeatureClassName);

            foreach (var field in fields)
            {
                var addFieldParams = Geoprocessing.MakeValueArray(
                    tablePath,
                    field.Name,
                    field.Type,
                    "",
                    "",
                    field.Length
                );

                await Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams,
                    null, CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            }
        }

    }
}
