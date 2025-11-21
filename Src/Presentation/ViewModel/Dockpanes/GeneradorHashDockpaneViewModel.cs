using System.Collections.Generic;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel.DockPanes
{
    /// <summary>
    /// DockPane ViewModel para el Generador de Hash
    /// </summary>
    internal class GeneradorHashDockpaneViewModel : DockPane
    {
        private static readonly string _dockPaneID = "EAABAddIn_Dockpane_GeneradorHash";
        private static string DockPaneID => _dockPaneID;

        private GeneradorHashGenerarViewModel _paneGenerarVM;
        private GeneradorHashVerificarViewModel _paneVerificarVM;

        protected GeneradorHashDockpaneViewModel()
        {
            _paneGenerarVM = new GeneradorHashGenerarViewModel();
            _paneVerificarVM = new GeneradorHashVerificarViewModel();
            
            SelectedPanelHeaderIndex = 0;
            
            PrimaryMenuList.Add(new TabControl()
            {
                Text = _paneGenerarVM.DisplayName,
                Tooltip = _paneGenerarVM.Tooltip
            });
            
            PrimaryMenuList.Add(new TabControl()
            {
                Text = _paneVerificarVM.DisplayName,
                Tooltip = _paneVerificarVM.Tooltip
            });
        }

        internal static void Show()
        {
            DockPane dockPane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);

            if (dockPane is not null)
            {
                var vm = dockPane as GeneradorHashDockpaneViewModel;
                vm?.SetGenerarTab();
                dockPane.Activate();
            }
        }

        internal static void ShowVerificarTab()
        {
            DockPane dockPane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);

            if (dockPane is not null)
            {
                var vm = dockPane as GeneradorHashDockpaneViewModel;
                vm?.SetVerificarTab();
                dockPane.Activate();
            }
        }

        internal void SetGenerarTab()
        {
            SelectedPanelHeaderIndex = 0; // Índice 0 corresponde a la pestaña Generar
        }

        internal void SetVerificarTab()
        {
            SelectedPanelHeaderIndex = 1; // Índice 1 corresponde a la pestaña Verificar
        }

        #region Properties
        
        private int _selectedPanelHeaderIndex = 0;
        public int SelectedPanelHeaderIndex
        {
            get => _selectedPanelHeaderIndex;
            set
            {
                SetProperty(ref _selectedPanelHeaderIndex, value, () => SelectedPanelHeaderIndex);

                switch (_selectedPanelHeaderIndex)
                {
                    case 0:
                        CurrentPage = _paneGenerarVM;
                        break;
                    case 1:
                        CurrentPage = _paneVerificarVM;
                        break;
                    default:
                        CurrentPage = _paneGenerarVM;
                        break;
                }
            }
        }

        private string _heading = "";
        public string Heading
        {
            get => _heading;
            set => SetProperty(ref _heading, value);
        }

        private PanelViewModelBase _currentPage;
        public PanelViewModelBase CurrentPage
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
}
