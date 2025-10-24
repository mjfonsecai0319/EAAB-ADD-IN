using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel;

internal class MigrationViewModel : BusyViewModelBase
{
    public override string DisplayName => "Migración";
    public override string Tooltip => "Migrar datos entre capas";

    public MigrationViewModel()
    {
        StatusMessage = "Seleccione el botón correspondiente para iniciar la migración.";
    }
}
