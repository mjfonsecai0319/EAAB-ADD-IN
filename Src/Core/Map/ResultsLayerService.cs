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
            var gdbPath = Project.Current.DefaultGeodatabasePath;

            if (mapView?.Map == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No hay un mapa activo.");
                return;
            }
            // Validar que el path de la GDB exista (la carpeta .gdb)
            if (string.IsNullOrEmpty(gdbPath) || !Directory.Exists(gdbPath))
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("La geodatabase predeterminada no está configurada o es inválida.");
                return;
            }
            if (!await _CreateFeatureClassIfNotExist(gdbPath))
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No se pudo crear la clase de entidad para los resultados.");
                return;
            }

            await EnsureFieldsExist(gdbPath);
            _AddFeatureClassToMapView(gdbPath, mapView);

            if (_addressPointLayer is not null)
            {
                if (!entidad.Latitud.HasValue || !entidad.Longitud.HasValue)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("La entidad no tiene coordenadas válidas.");
                    return;
                }
                // Crear geometría
                var mapPoint = MapPointBuilderEx.CreateMapPoint(
                    (double)entidad.Longitud.Value,
                    (double)entidad.Latitud.Value,
                    SpatialReferences.WGS84
                );

                var featureClass = _addressPointLayer.GetTable() as FeatureClass;
                if (featureClass == null)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No se pudo acceder a la FeatureClass de la capa.");
                    return;
                }
                try
                {
                    using (var rowBuffer = featureClass.CreateRowBuffer())
                    {
                        var def = featureClass.GetDefinition();
                        rowBuffer[def.GetShapeField()] = mapPoint;
                        rowBuffer["Identificador"] = entidad.ID.ToString();
                        rowBuffer["Direccion"] = entidad.FullAddressOld ?? entidad.MainStreet ?? string.Empty;
                        rowBuffer["Poblacion"] = entidad.CityDesc ?? entidad.CityCode ?? string.Empty;
                        // Alinear con los nombres de campo creados por EnsureFieldsExist
                        rowBuffer["FullAdressEAAB"] = entidad.FullAddressEAAB ?? string.Empty;
                        rowBuffer["FullAdressUACD"] = entidad.FullAddressCadastre ?? string.Empty;
                        rowBuffer["Geocoder"] = string.IsNullOrWhiteSpace(entidad.Source) ? "EAAB" : entidad.Source;
                        rowBuffer["Score"] = entidad.Score.HasValue ? entidad.Score.Value : null;
                        rowBuffer["ScoreText"] = entidad.ScoreText ?? string.Empty;

                        using (var row = featureClass.CreateRow(rowBuffer)) { }
                    }
                }
                catch (Exception ex)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"Error insertando fila: {ex.Message}");
                    return;
                }

                await mapView.ZoomToAsync(_addressPointLayer.QueryExtent(), TimeSpan.FromSeconds(1));
            }
            else
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("La capa de resultados no está disponible o no es editable.");
            }
        }

        private async static Task<bool> _CreateFeatureClassIfNotExist(string path)
        {
            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(path));
            var exists = false;

            // Verificar existencia y liberar la conexión antes de crear para evitar schema locks
            using (var geodatabase = new Geodatabase(gdbConnectionPath))
            {
                exists = geodatabase
                    .GetDefinitions<FeatureClassDefinition>()
                    .Any(d => string.Equals(d.GetName(), FeatureClassName, StringComparison.OrdinalIgnoreCase));
            }

            if (exists)
                return true;

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
                GPExecuteToolFlags.AddToHistory);

            return !gpResult.IsFailed;
        }

        private static void _AddFeatureClassToMapView(string path, MapView mapView)
        {
            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(path));

            // Validación extra: intentar encontrar la capa ya cargada en el mapa
            if (_addressPointLayer is null && mapView?.Map is not null)
            {
                var existing = mapView.Map
                    .GetLayersAsFlattenedList()
                    .OfType<FeatureLayer>()
                    .FirstOrDefault(fl =>
                    {
                        // Coincidencia por nombre
                        if (string.Equals(fl.Name, FeatureClassName, StringComparison.OrdinalIgnoreCase))
                            return true;

                        // Coincidencia por dataset (FeatureClass) subyacente
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
            // Validar que exista la ruta de la GDB (no solo la carpeta padre)
            if (string.IsNullOrEmpty(gdbPath) || !Directory.Exists(gdbPath))
                return;

            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));

            // Crear FeatureClass si no existe (evitar schema locks)
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

            // Inserción por Core Data en lote (sin EditOperation)
            var batchFc = _addressPointLayer.GetTable() as FeatureClass;
            if (batchFc != null)
            {
                try
                {
                    foreach (var entidad in toInsert)
                    {
                        if (!entidad.Latitud.HasValue || !entidad.Longitud.HasValue) continue;
                        var mapPoint = MapPointBuilderEx.CreateMapPoint(
                            (double)entidad.Longitud.Value,
                            (double)entidad.Latitud.Value,
                            SpatialReferences.WGS84
                        );
                        using (var rowBuffer = batchFc.CreateRowBuffer())
                        {
                            var def = batchFc.GetDefinition();
                            rowBuffer[def.GetShapeField()] = mapPoint;
                            rowBuffer["Identificador"] = entidad.ID.ToString();
                            rowBuffer["Direccion"] = entidad.FullAddressOld ?? entidad.MainStreet ?? string.Empty;
                            rowBuffer["Poblacion"] = entidad.CityDesc ?? entidad.CityCode ?? string.Empty;
                            rowBuffer["FullAdressEAAB"] = entidad.FullAddressEAAB ?? string.Empty;
                            rowBuffer["FullAdressUACD"] = entidad.FullAddressCadastre ?? string.Empty;
                            rowBuffer["Geocoder"] = string.IsNullOrWhiteSpace(entidad.Source) ? "EAAB" : entidad.Source;
                            if (entidad.Score.HasValue) rowBuffer["Score"] = entidad.Score.Value; else rowBuffer["Score"] = null;
                            rowBuffer["ScoreText"] = entidad.ScoreText ?? string.Empty;
                            using (var row = batchFc.CreateRow(rowBuffer)) { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"Error en inserción en lote: {ex.Message}");
                    return;
                }
                await mapView.ZoomToAsync(_addressPointLayer.QueryExtent(), TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// Asegura que los campos requeridos existan en la FeatureClass.
        /// Nota: Score ahora es TEXT para aceptar texto o números.
        /// </summary>
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
                ("ScoreText", "TEXT", "100")
            };

            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            // Revisar campos existentes evitando GP si no es necesario
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
                // Si falla, continuar con los demás para intentar crear el resto
            }
        }

    }
}