#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Application.UseCases;
using EAABAddIn.Src.Core;
using EAABAddIn.Src.Core.Map;
using EAABAddIn.Src.Domain.Repositories;
using EAABAddIn.Src.Presentation.Base;

using ExcelDataReader;

using Microsoft.Win32;

namespace EAABAddIn.Src.Presentation.ViewModel;

internal class DrawPolygonViewModel : BusyViewModelBase
{
    public override string DisplayName => "Crear Cierres";
    public override string Tooltip => "Herramienta para crear cierres en el mapa.";

    public ICommand WorkspaceCommand { get; private set; }
    public ICommand FeatureClassCommand { get; private set; }
    public ICommand NeighborhoodCommand { get; private set; }
    public ICommand ClientsAffectedCommand { get; private set; }
    public ICommand BuildPolygonsCommand { get; private set; }
    // Eliminado RefreshIdentifiersCommand (ya no se gestionan subcapas de identificadores)

    public DrawPolygonViewModel()
    {
        WorkspaceCommand = new RelayCommand(OnWorkspace);
        FeatureClassCommand = new RelayCommand(OnFeatureClass);
        NeighborhoodCommand = new RelayCommand(OnNeighborhood);
        ClientsAffectedCommand = new RelayCommand(OnClientsAffected);
        BuildPolygonsCommand = new RelayCommand(async () => await OnBuildPolygons(), () => !IsBusy);
    // RefreshIdentifiersCommand eliminado
    }

    private async Task OnBuildPolygons()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Construyendo polígonos...";
            var gdb = Workspace;
            var result = await GeocodedPolygonsLayerService.GenerateAsync(gdb);
            bool allow3 = Module1.Settings?.permitirTresPuntos == true;
            int minPoints = allow3 ? 3 : 4;
            if (result.Count == 0)
            {
                StatusMessage = $"No se generaron polígonos (necesitan >={minPoints} puntos por identificador).";
                return;
            }
            if (result.ContainsKey("__DIAGNOSTICO__") && result.Count == 1)
            {
                StatusMessage = $"No se generaron polígonos. Ver consola (Debug) para causas (<{minPoints}).";
                return;
            }
            var resumen = string.Join(", ", result.Keys.Where(k=>k!="__DIAGNOSTICO__").Take(10));
            var total = result.Keys.Count(k=>k!="__DIAGNOSTICO__");
            if (total > 10) resumen += $" (+{total - 10} más)";
            StatusMessage = $"Generados {total} polígonos (mínimo {minPoints}). IDs: {resumen}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Error construyendo polígonos";
            MessageBox.Show($"Error al construir polígonos: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Método OnRefreshIdentifiers eliminado

    private void OnWorkspace()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_geodatabases");

        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar Geodatabase",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            Workspace = item.Path;
        }
    }

    private void OnFeatureClass()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_featureClasses_all");

        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar la Feature Class",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            FeatureClass = item.Path;
        }
    }

    private void OnNeighborhood()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_featureClasses_all");

        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar la Feature Class",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            Neighborhood = item.Path;
        }
    }

    private void OnClientsAffected()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_featureClasses_all");

        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar la Feature Class",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            ClientsAffected = item.Path;
        }
    }

    #region Properties
    private string _workspace = Project.Current.DefaultGeodatabasePath;
    public string Workspace
    {
        get => _workspace;
        set
        {
            if (_workspace != value)
            {
                _workspace = value;
                NotifyPropertyChanged(nameof(Workspace));
            }
        }
    }

    private string? _featureClass = null;
    public string? FeatureClass
    {
        get => _featureClass;
        set
        {
            if (_featureClass != value)
            {
                _featureClass = value;
                NotifyPropertyChanged(nameof(FeatureClass));
            }
        }
    }

    private string? _neighborhood = null;
    public string? Neighborhood
    {
        get => _neighborhood;
        set
        {
            if (_neighborhood != value)
            {
                _neighborhood = value;
                NotifyPropertyChanged(nameof(Neighborhood));
            }
        }
    }

    private string? _clientsAffected = null;
    public string? ClientsAffected
    {
        get => _clientsAffected;
        set
        {
            if (_clientsAffected != value)
            {
                _clientsAffected = value;
                NotifyPropertyChanged(nameof(ClientsAffected));
            }
        }
    }

    private int _clientsAffectedCount = 0;
    public int ClientsAffectedCount
    {
        get => _clientsAffectedCount;
        set
        {
            if (_clientsAffectedCount != value)
            {
                _clientsAffectedCount = value;
                NotifyPropertyChanged(nameof(ClientsAffectedCount));
            }
        }
    }
    #endregion
}
