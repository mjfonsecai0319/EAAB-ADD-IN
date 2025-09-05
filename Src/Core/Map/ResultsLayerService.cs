using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System.Threading.Tasks;

namespace EAABAddIn.Map
{
    public static class ResultsLayerService
    {
        private static GraphicsLayer _resultsLayer;

        public static Task AddPointAsync(double latitud, double longitud)
        {
            return QueuedTask.Run(() =>
            {
                // Crear punto en WGS84
                MapPoint mapPoint = MapPointBuilderEx.CreateMapPoint(longitud, latitud, SpatialReferences.WGS84);

                // Obtener mapa activo
                var mapView = MapView.Active;
                if (mapView == null || mapView.Map == null)
                    return;

                var map = mapView.Map;

                // Crear la capa solo si aún no existe
                if (_resultsLayer == null)
                {
                    var layerParams = new GraphicsLayerCreationParams
                    {
                        Name = "Resultados"
                    };
                    _resultsLayer = LayerFactory.Instance.CreateLayer<GraphicsLayer>(layerParams, map);
                }

                // Crear símbolo 
                var symbol = SymbolFactory.Instance.ConstructPointSymbol(
                    ColorFactory.Instance.BlueRGB, 9, SimpleMarkerStyle.Circle);

                // Crear gráfico
                var graphic = new CIMPointGraphic
                {
                    Location = mapPoint,
                    Symbol = symbol.MakeSymbolReference()
                };

                // Agregar el gráfico a la capa ya existente
                _resultsLayer.AddElement(graphic);

                // Hacer zoom al punto
                mapView.ZoomTo(mapPoint);
            });
        }
    }
}
