# nullable enable
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using EAABAddIn.Src.Application.UseCases;

namespace EAABAddIn.Src.Presentation.View.Buttons;

public class UnionPolygonsButton : Button
{
    private readonly GetSelectedFeatureUseCase _getSelectedFeatureUseCase = new();

    private readonly SelectByLocationUseCase _selectByLocationUseCase = new();

    protected override void OnClick()
    {
        var mapView = MapView.Active;

        _ = QueuedTask.Run(() => this.OnClickAsync(mapView));
    }

    protected async Task OnClickAsync(MapView mapView)
    {
        var selectedFeature = await _getSelectedFeatureUseCase.Invoke(mapView);

        if (selectedFeature is null)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Could not retrieve the selected feature.");
            return;
        }

        var list = await _selectByLocationUseCase.Invoke(
            selectedFeature,
            @"C:\Users\molarte\Documents\Clientes EAAB\BARRIOS_SGO.gdb\BARRIOS_MUNICIPIO"
        );
    }
}
