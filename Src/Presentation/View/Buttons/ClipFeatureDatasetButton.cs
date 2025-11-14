using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

using EAABAddIn.Src.Presentation.ViewModel.DockPanes;

namespace EAABAddIn.Src.Presentation.View.Buttons;

public class ClipFeatureDatasetButton : Button
{
    protected override void OnClick()
    {
        ClipFeatureDatasetDockpaneViewModel.Show();

        var pane = FrameworkApplication.DockPaneManager.Find("EAABAddIn_Dockpane_ClipFeatureDataset") as ClipFeatureDatasetDockpaneViewModel;
        if (pane != null)
        {
            pane.SelectedPanelHeaderIndex = 0;
        }
    }
}
