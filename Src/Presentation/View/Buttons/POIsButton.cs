using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

using EAABAddIn.Src.Presentation.ViewModel;

namespace EAABAddIn.Src.Presentation.View.Buttons;

public class POIsButton : Button
{
    protected override void OnClick()
    {
        GeocoderDockpaneViewModel.Show();

        var pane = FrameworkApplication.DockPaneManager.Find("EAABAddIn_Src_Presentation_View_GeocoderDockpane") as GeocoderDockpaneViewModel;
        if (pane != null)
        {
            pane.SelectedPanelHeaderIndex = 2;
        }
    }
}
