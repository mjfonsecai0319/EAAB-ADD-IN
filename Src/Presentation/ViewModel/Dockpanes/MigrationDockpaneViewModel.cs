using System.Collections.Generic;

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;

using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel.DockPanes;

internal class MigrationDockpaneViewModel : DockPane
{
    private static readonly string _dockPaneID = "EAABAddIn_Dockpane_Migracion";

    private MigrationViewModel _migrationVM;

    protected MigrationDockpaneViewModel()
    {
        _migrationVM = new MigrationViewModel();
        SelectedPanelHeaderIndex = 0;

        PrimaryMenuList.Add(new TabControl()
        {
            Text = _migrationVM.DisplayName,
            Tooltip = _migrationVM.Tooltip
        });

        CurrentPage = _migrationVM;
    }

    internal static void Show()
    {
        var dockPane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
        if (dockPane is not null)
        {
            dockPane.Activate();
        }
    }

    #region Properties
    private int _selectedPanelHeaderIndex = 0;
    public int SelectedPanelHeaderIndex
    {
        get => _selectedPanelHeaderIndex;
        set
        {
            SetProperty(ref _selectedPanelHeaderIndex, value, () => SelectedPanelHeaderIndex);
            CurrentPage = _migrationVM;
        }
    }

    private PanelViewModelBase _currentPage;
    public PanelViewModelBase CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value, () => CurrentPage);
    }

    private List<TabControl> _primaryMenuList = new();
    public List<TabControl> PrimaryMenuList
    {
        get => _primaryMenuList;
        private set => SetProperty(ref _primaryMenuList, value);
    }
    #endregion
}
