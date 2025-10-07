using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace EAABAddIn.Src.Presentation.View.Buttons;

internal class SettingsButton : Button
{
    protected override void OnClick()
    {
        var data = new object[] { "Configuración" };

        if (!PropertySheet.IsVisible)
        {
            PropertySheet.ShowDialog("EAABAddIn_PropertySheet1", "EAABAddIn_PropertyPage2", data);
        }
    }
}