using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Linq;
using System.Threading.Tasks;

namespace EAABAddIn.Src.Presentation.View.Buttons;

public class UnionPolygonsButton : Button
{
    protected override void OnClick()
    {
        QueuedTask.Run(OnClickAsync);
    }

    protected async Task OnClickAsync()
    {
        // Get the active map view
        var mapView = ArcGIS.Desktop.Mapping.MapView.Active;
        
        if (mapView == null)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No active map view found.");
            return;
        }

        // Get selected features grouped by layer
        var selectedFeatures = mapView.Map.GetSelection();
        // Only proceed if exactly one layer is selected and it is a FeatureLayer
        if (selectedFeatures.Count != 1)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Please select features from a single FeatureClass layer.");
            return;
        }

        // Convert SelectionSet to Dictionary<Layer, List<long>>
        var dict = selectedFeatures.ToDictionary();
        var kvp = dict.ElementAt(0);
        var layer = kvp.Key as ArcGIS.Desktop.Mapping.FeatureLayer;
        if (layer == null)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Selected layer is not a FeatureClass.");
            return;
        }

        var oids = kvp.Value;
        if (oids == null || oids.Count != 1)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Please select exactly one feature.");
            return;
        }

        ArcGIS.Core.Data.Feature selectedFeature = null;
        await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
        {
            using var table = layer.GetTable();
            using var cursor = table.Search(new ArcGIS.Core.Data.QueryFilter { ObjectIDs = oids }, false);
            if (cursor.MoveNext())
            {
                selectedFeature = cursor.Current as ArcGIS.Core.Data.Feature;
            }
        });

        if (selectedFeature == null)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Could not retrieve the selected feature.");
            return;
        }

        // Call the use case
        var useCase = new EAABAddIn.Src.Application.UseCases.SelectByLocation();
        useCase.Invoke(selectedFeature);
    }
}
