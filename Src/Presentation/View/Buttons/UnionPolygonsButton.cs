using System;
using System.Threading.Tasks;

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Presentation.ViewModel.DockPanes;

namespace EAABAddIn.Src.Presentation.View.Buttons;

public class UnionPolygonsButton : Button
{
    protected override void OnClick()
    {
        QueuedTask.Run(ActivateSelectionToolAsync);
        CierresDockpaneViewModel.Show();

        var pane = FrameworkApplication.DockPaneManager.Find("EAABAddIn_Dockpane_Cierres") as CierresDockpaneViewModel;
        if (pane != null)
        {
            pane.SelectedPanelHeaderIndex = 2;
        }
    }

    private async Task ActivateSelectionToolAsync()
    {
        try
        {
            var toolId = "esri_mapping_selectByRectangleTool";

            await FrameworkApplication.SetCurrentToolAsync(toolId);
        }
        catch (Exception ex)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                $"No se pudo activar la herramienta de selección: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
        }
    }
}
