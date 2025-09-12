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

namespace EAABAddIn.Src.Core.Map
{
    public static class ResultsLayerService
    {
        private static FeatureLayer _addressPointLayer;
        private const string FeatureClassName = "GeocodedAddresses";

        public static Task AddPointAsync(decimal latitud, decimal longitud)
        {
            // The entire operation is wrapped in QueuedTask.Run to ensure it runs on the MCT.
            return QueuedTask.Run(() => _AddPointAsync((double)latitud, (double)longitud));
        }

        private static async Task _AddPointAsync(double latitud, double longitud)
        {
            var mapView = MapView.Active;
            if (mapView?.Map == null)
            {
                return;
            }

            // Find layer in the current map by its underlying feature class name
            _addressPointLayer = mapView.Map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .FirstOrDefault(l => l.GetTable()?.GetName() == FeatureClassName);

            // If layer is not in the map, create it
            if (_addressPointLayer == null)
            {
                var gdbPath = Project.Current.DefaultGeodatabasePath;
                if (string.IsNullOrEmpty(gdbPath) || !Directory.Exists(Path.GetDirectoryName(gdbPath)))
                {
                    // Handle case where default GDB is not available
                    return;
                }

                var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));

                // Create Feature Class if it doesn't exist
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

                        if (result.IsFailed)
                        {
                            // Handle geoprocessing error
                            return;
                        }
                    }
                }

                // Add the feature class to the map as a new layer
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

            // Create the point and add it to the feature class
            var createOperation = new EditOperation
            {
                Name = "Add Geocoded Point"
            };

            var mapPoint = MapPointBuilderEx.CreateMapPoint(longitud, latitud, SpatialReferences.WGS84);
            var attributes = new Dictionary<string, object>
            {
                { "Shape", mapPoint }
            };
            createOperation.Create(_addressPointLayer, attributes);

            var success = await createOperation.ExecuteAsync();

            if (success)
            {
                await mapView.ZoomToAsync(_addressPointLayer.QueryExtent(), TimeSpan.FromSeconds(1));
            }
        }
    }
}
