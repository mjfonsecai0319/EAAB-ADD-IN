using ArcGIS.Desktop.Framework.Contracts;

using EAABAddIn.Src.UI;

namespace EAABAddIn.Src.Presentation.View;

internal class Button2 : Button
{
    protected override void OnClick()
    {
        var dialog = new FileUploadDialog();
        bool? result = dialog.ShowDialog();
    }
}
