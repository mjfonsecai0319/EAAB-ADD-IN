using System.Linq;
using System.Text;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Core;
using EAABAddIn.Src.Core.Map;

namespace EAABAddIn.Src.Presentation.View.Buttons;

internal class CreateIdentifierSublayersButton : Button
{
    protected override async void OnClick()
    {
        try
        {
            var gdbPath = Project.Current?.DefaultGeodatabasePath;
            var list = await IdentifierSublayersService.CreateIdentifierSublayersAsync(gdbPath);
            var sb = new StringBuilder();
            sb.AppendLine("Sublayers creadas (>=3 puntos):");
            foreach (var id in list.Take(30)) sb.AppendLine(" - " + id);
            if (list.Count > 30) sb.AppendLine($"... (+{list.Count - 30} más)");
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(sb.ToString(), "Identificadores");
        }
        catch (System.Exception ex)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Error: " + ex.Message, "Identificadores");
        }
    }
}
