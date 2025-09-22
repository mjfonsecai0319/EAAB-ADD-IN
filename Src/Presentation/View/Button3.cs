using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

using EAABAddIn.Src.Presentation.ViewModel;

namespace EAABAddIn.Src.Presentation.View;


internal class Button3 : Button
{
    protected override void OnClick()
    {
        var data = new object[] { "Configuración" };

        if(!PropertySheet.IsVisible)
        {
            PropertySheet.ShowDialog("EAABAddIn_PropertySheet1", "EAABAddIn_PropertyPage2", data);
        }
    }
}