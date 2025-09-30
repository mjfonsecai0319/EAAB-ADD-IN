using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core; // Project para gdb por defecto
using EAABAddIn.Src.Core.Entities;
using EAABAddIn.Src.Core.Map; // ResultsLayerService
using EAABAddIn.Src.Domain.Repositories;

namespace EAABAddIn.Src.Presentation.ViewModel;

public class POIsDockpaneViewModel : DockPane
{
    private const string _dockPaneID = "EAABAddIn_Src_Presentation_View_POIsDockpane";

    internal static void Show()
    {
        DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
        if (pane is not null)
        {
            pane.Activate();
        }
        return;
    }

    private string _searchInput = string.Empty;
    public string SearchInput
    {
        get => _searchInput;
        set
        {
            if (_searchInput != value)
            {
                _searchInput = value;
                NotifyPropertyChanged(nameof(SearchInput));
            }
        }
    }

    public ICommand SearchCommand { get; }
    public ICommand MarkAllCommand { get; }
    public ICommand MarkSelectedCommand { get; }
    // ClearLayerCommand eliminado (se retiró botón de UI)

    private ObservableCollection<PtPoisEaabEntity> _results = new();
    public ObservableCollection<PtPoisEaabEntity> Results
    {
        get => _results;
        set { _results = value; NotifyPropertyChanged(nameof(Results)); }
    }

    private PtPoisEaabEntity _selected;
    public PtPoisEaabEntity Selected
    {
        get => _selected;
        set { _selected = value; NotifyPropertyChanged(nameof(Selected)); }
    }

    private bool _isSearching;
    public bool IsSearching
    {
        get => _isSearching;
        set { _isSearching = value; NotifyPropertyChanged(nameof(IsSearching)); }
    }

    private string _status;
    public string Status
    {
        get => _status;
        set { _status = value; NotifyPropertyChanged(nameof(Status)); }
    }

    public POIsDockpaneViewModel()
    {
        SearchCommand = new RelayCommand(async () => await OnSearchAsync(), () => !IsSearching && !string.IsNullOrWhiteSpace(SearchInput));
        MarkAllCommand = new RelayCommand(async () => await MarkAllAsync(), () => Results.Any() && !IsSearching);
        MarkSelectedCommand = new RelayCommand(async () => await MarkSelectedAsync(), () => Selected?.Latitude != null && Selected?.Longitude != null && !IsSearching);
    }

    private async Task OnSearchAsync()
    {
        var term = SearchInput?.Trim();
        if (string.IsNullOrWhiteSpace(term)) return;
    IsSearching = true; Status = "Buscando..."; Results.Clear(); Selected = null;
        try
        {
            var repo = new PtPoisEaabRepository();
            // Llamamos directamente al método asíncrono (internamente usa QueuedTask)
            var list = await repo.FindByWordAsync(term, 25);
            if (list.Count == 0)
            {
                Status = "Sin resultados";
                MessageBox.Show("No se encontraron POIs.", "POIs");
                return;
            }
            foreach (var item in list) Results.Add(item);
            Selected = Results.FirstOrDefault(r => r.Latitude.HasValue && r.Longitude.HasValue) ?? Results.First();
            Status = $"{Results.Count} resultados. Seleccione un POI y marque (o 'Agregar Todos').";
        }
        catch (Exception ex)
        {
            Status = "Error";
            MessageBox.Show("Error en búsqueda POIs: " + ex.Message, "POIs");
        }
        finally
        {
            IsSearching = false;
            (SearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MarkAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MarkSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private async Task MarkAllAsync()
    {
        if (!Results.Any()) return;
        try
        {
            var valid = Results.Where(r => r.Latitude.HasValue && r.Longitude.HasValue).ToList();
            if (!valid.Any())
            {
                Status = "Sin POIs con coordenadas";
                return;
            }

            // Cargar en memoria y luego un solo commit batch
            ResultsLayerService.ClearPending();
            foreach (var poi in valid)
            {
                ResultsLayerService.AddPointToMemory(new PtAddressGralEntity
                {
                    Latitud = poi.Latitude.HasValue ? (decimal?)Convert.ToDecimal(poi.Latitude.Value) : null,
                    Longitud = poi.Longitude.HasValue ? (decimal?)Convert.ToDecimal(poi.Longitude.Value) : null,
                    FullAddressEAAB = poi.NamePoi,
                    FullAddressCadastre = poi.Address,
                    CityDesc = poi.CityDesc,
                    CityCode = poi.CityCode,
                    Source = "POI",
                    Score = poi.TotalScore,
                    ScoreText = poi.TotalScore.ToString("0.000")
                });
            }

            var gdbPath = Project.Current.DefaultGeodatabasePath;
            // Batch con zoom al conjunto insertado
            await ResultsLayerService.CommitPointsAsync(gdbPath, true);
            Status = $"Marcados {valid.Count} POIs (zoom conjunto)";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al marcar POIs: " + ex.Message, "POIs");
        }
    }

    private async Task MarkSelectedAsync()
    {
        var poi = Selected;
        if (poi == null || !poi.Latitude.HasValue || !poi.Longitude.HasValue) return;
        try
        {
            var entidad = new PtAddressGralEntity
            {
                Latitud = (decimal?)Convert.ToDecimal(poi.Latitude.Value),
                Longitud = (decimal?)Convert.ToDecimal(poi.Longitude.Value),
                FullAddressEAAB = poi.NamePoi,
                FullAddressCadastre = poi.Address,
                CityDesc = poi.CityDesc,
                CityCode = poi.CityCode,
                Source = "POI",
                Score = poi.TotalScore,
                ScoreText = poi.TotalScore.ToString("0.000")
            };
            await ResultsLayerService.AddPointAsync(entidad, skipDuplicates: true); // evita duplicar el mismo punto/dirección
            Status = $"Marcado POI: {poi.NamePoi}";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al marcar POI seleccionado: " + ex.Message, "POIs");
        }
        finally
        {
            (MarkSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

}
