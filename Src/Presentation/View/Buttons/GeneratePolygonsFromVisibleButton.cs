using System.Linq;
using System.Text;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core;
using EAABAddIn.Src.Core.Map;

namespace EAABAddIn.Src.Presentation.View.Buttons;

internal class GeneratePolygonsFromVisibleButton : Button
{
    protected override async void OnClick()
    {
        try
        {
            var mapView = MapView.Active;
            if (mapView?.Map == null) return;
            var group = mapView.Map.GetLayersAsFlattenedList().OfType<GroupLayer>()
                .FirstOrDefault(gl => gl.Name.Equals("Identificadores"));
            if (group == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Cree primero las subcapas de identificadores.");
                return;
            }
            var ids = group.Layers.OfType<FeatureLayer>()
                .Where(fl => fl.IsVisible)
                .Select(fl => fl.Name)
                .ToList();
            if (ids.Count == 0)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No hay subcapas visibles para generar.");
                return;
            }
            var gdbPath = Project.Current?.DefaultGeodatabasePath;
            var dict = await GeocodedPolygonsLayerService.GenerateAsync(ids, gdbPath);
            var sb = new StringBuilder();
            sb.AppendLine("Polígonos generados:");
            foreach (var kv in dict) sb.AppendLine($" - {kv.Key}: {kv.Value} puntos");
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(sb.ToString(), "Cierres");
        }
        catch (System.Exception ex)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Error: " + ex.Message, "Cierres");
        }
    }
}
