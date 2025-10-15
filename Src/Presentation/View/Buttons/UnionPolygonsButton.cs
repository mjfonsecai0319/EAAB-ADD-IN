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
        // var selectedFeature = await _getSelectedFeatureUseCase.Invoke(mapView);

        // if (selectedFeature is null)
        // {
        //     ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
        //         messageText: "No se encontró la entidad seleccionada. Por favor seleccione exactamente una entidad puntual en el mapa y vuelva a intentarlo.",
        //         caption: "Error - Selección inválida",
        //         button: System.Windows.MessageBoxButton.OK,
        //         icon: System.Windows.MessageBoxImage.Warning
        //     );
        //     return;
        // }

        // var neighborhoods = await _selectByLocationUseCase.Invoke(
        //     selectedFeature,
        //     "BARRIOS_MUNICIPIO"
        // );
    }
}
