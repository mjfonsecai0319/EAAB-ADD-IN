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

            var attributes = new Dictionary<string, object>
            {
                { "Shape", mapPoint },
                { "Identificador", entidad.ID.ToString() },
                { "Direccion", entidad.FullAddressOld ?? entidad.MainStreet },
                { "Poblacion", entidad.CityDesc ?? entidad.CityCode },
                { "FullAdressEAAB", entidad.FullAddressEAAB },
                { "FullAdressUACD", entidad.FullAddressCadastre },
                { "Geocoder", entidad.Source },
                { "Score", entidad.Score } 
            };

            var createOperation = new EditOperation { Name = "Add Geocoded Point" };
            createOperation.Create(_addressPointLayer, attributes);

            var success = await createOperation.ExecuteAsync();
            if (success)
                await mapView.ZoomToAsync(_addressPointLayer.QueryExtent(), TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Asegura que los campos requeridos existan en la FeatureClass.
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
                new { Name = "Score", Type = "DOUBLE", Length = "" }
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

                // Intenta agregar campo, si ya existe ignora el error
                await Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams,
                    null, CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            }
        }
    }
}
