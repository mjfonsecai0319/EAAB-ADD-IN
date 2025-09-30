using System;

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

using EAABAddIn.Src.Presentation.ViewModel;

namespace EAABAddIn.Src.Presentation.View.Buttons;

public class POIsButton : Button
{
    protected override void OnClick()
    {
        POIsDockpaneViewModel.Show();
        _ = FrameworkApplication.DockPaneManager.Find("EAABAddIn_Src_Presentation_View_POIsDockpane") as POIsDockpaneViewModel;
    }
}
