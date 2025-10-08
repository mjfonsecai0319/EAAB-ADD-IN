using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

using EAABAddIn.Src.Presentation.ViewModel.DockPanes;

namespace EAABAddIn.Src.Presentation.View.Buttons;

public class DrawPolygonButton : Button
{
    protected override void OnClick()
    {
        CierresDockpaneViewModel.Show();

        var pane = FrameworkApplication.DockPaneManager.Find("EAABAddIn_Dockpane_Cierres") as CierresDockpaneViewModel;
        if (pane != null)
        {
            pane.SelectedPanelHeaderIndex = 0;
        }
    }
}
