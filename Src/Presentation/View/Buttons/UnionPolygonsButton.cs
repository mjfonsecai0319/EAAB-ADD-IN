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
            // Common ArcGIS Pro selection tool command IDs; try a known ID and fallback to execute command
            // If your ArcGIS Pro version uses a different command id, replace with the correct one.
            var toolId = "esri_mapping_selectByRectangleTool";

            // Try to set the current tool (async). Some SDK versions return void Task.
            await FrameworkApplication.SetCurrentToolAsync(toolId);
        }
        catch (Exception ex)
        {
            // If activation failed, inform the user
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                $"No se pudo activar la herramienta de selección: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
        }
    }
}
