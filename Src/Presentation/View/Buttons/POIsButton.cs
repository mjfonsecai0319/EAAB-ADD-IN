using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

using EAABAddIn.Src.Presentation.ViewModel.DockPanes;

namespace EAABAddIn.Src.Presentation.View.Buttons;

public class POIsButton : Button
{
    protected override void OnClick()
    {
        GeocoderDockpaneViewModel.Show();

        var pane = FrameworkApplication.DockPaneManager.Find("EAABAddIn_Dockpane_AddressGeocoder") as GeocoderDockpaneViewModel;
        if (pane != null)
        {
            pane.SelectedPanelHeaderIndex = 2;
        }
    }
}
