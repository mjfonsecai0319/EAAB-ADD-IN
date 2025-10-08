using System.Linq;
using System.Text;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Core;
using EAABAddIn.Src.Core.Map;

namespace EAABAddIn.Src.Presentation.View.Buttons;

internal class GeneratePolygonsButton : Button
{
    protected override async void OnClick()
    {
        try
        {
            // Llamada directa al servicio (el método antiguo Module1.GenerateGeocodedPolygonsAsync fue retirado)
            var gdbPath = Project.Current?.DefaultGeodatabasePath;
            var dict = await GeocodedPolygonsLayerService.GenerateAsync(gdbPath);
            if (dict == null || dict.Count == 0)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No se generaron polígonos (se requieren al menos 3 puntos por identificador).", "Generar polígonos");
                return;
            }
            var top = dict.OrderByDescending(k => k.Value).Take(10).ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"Polígonos creados: {dict.Count}");
            sb.AppendLine("Top identificadores:");
            foreach (var kv in top)
                sb.AppendLine($" - {kv.Key}: {kv.Value} puntos");
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(sb.ToString(), "Generar polígonos");
        }
        catch (System.Exception ex)
        {
            var detail = ex is System.NullReferenceException ? "(Referencia nula – verifique que la capa GeocodedAddresses exista y haya puntos con campo Identificador)" : string.Empty;
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"Error al generar polígonos: {ex.Message} {detail}", "Generar polígonos");
        }
    }
}
