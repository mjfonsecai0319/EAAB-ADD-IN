#nullable enable

using System.Collections.Generic;

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;

using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel.DockPanes;

internal class ClipFeatureDatasetDockpaneViewModel : DockPane
{
    private static readonly string _dockPaneID = "EAABAddIn_Dockpane_ClipFeatureDataset";
    private static string DockPaneID => _dockPaneID;

    private ClipFeatureDatasetViewModel _panelVM;

    protected ClipFeatureDatasetDockpaneViewModel()
    {
        _panelVM = new();
        SelectedPanelHeaderIndex = 0;
        PrimaryMenuList.Add(new TabControl()
        {
            Text = _panelVM.DisplayName,
            Tooltip = _panelVM.Tooltip
        });
    }

    internal static void Show()
    {
        DockPane dockPane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);

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
            CurrentPage = _panelVM;
        }
    }

    private PanelViewModelBase? _currentPage;
    public PanelViewModelBase? CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value, () => CurrentPage);
    }

    private List<TabControl> _primaryMenuList = new List<TabControl>();
    public List<TabControl> PrimaryMenuList
    {
        get => _primaryMenuList;
        private set => SetProperty(ref _primaryMenuList, value);
    }
    #endregion
}
