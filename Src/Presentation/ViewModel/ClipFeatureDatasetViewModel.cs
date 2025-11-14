#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel;

internal class ClipFeatureDatasetViewModel : BusyViewModelBase
{
    public override string DisplayName => "Clip Feature Dataset";
    public override string Tooltip => "Realiza un clip de un Feature Dataset sobre un polígono seleccionado.";

    public ICommand OutputGeodatabaseCommand { get; private set; }
    public ICommand FeatureDatasetCommand { get; private set; }
    public ICommand ExecuteClipCommand { get; private set; }
    public ICommand ClearFormCommand { get; private set; }
    public ICommand RefreshSelectionCommand { get; private set; }

    private Polygon? _selectedPolygon = null;
    private string? _acueductoPath = null;
    private string? _alcantarilladoSanitarioPath = null;
    private string? _alcantarilladoPluvalPath = null;

    public ClipFeatureDatasetViewModel()
    {
        OutputGeodatabaseCommand = new RelayCommand(OnOutputGeodatabase);
        FeatureDatasetCommand = new RelayCommand(OnFeatureDataset);
        ExecuteClipCommand = new RelayCommand(async () => await OnExecuteClip(), () => !IsBusy && CanExecuteClip);
        ClearFormCommand = new RelayCommand(OnClearForm);
        RefreshSelectionCommand = new AsyncRelayCommand(OnRefreshSelectionAsync);

        MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
        {
            await Task.Delay(500);
            await OnRefreshSelectionAsync();
        }));
    }

    private string _outputGeodatabase = Project.Current.DefaultGeodatabasePath;
    public string OutputGeodatabase
    {
        get => _outputGeodatabase;
        set
        {
            if (_outputGeodatabase != value)
            {
                _outputGeodatabase = value;
                NotifyPropertyChanged(nameof(OutputGeodatabase));
                NotifyPropertyChanged(nameof(CanExecuteClip));
            }
        }
    }

    private string? _featureDataset = null;
    public string? FeatureDataset
    {
        get => _featureDataset;
        set
        {
            if (_featureDataset != value)
            {
                _featureDataset = value;
                NotifyPropertyChanged(nameof(FeatureDataset));
                NotifyPropertyChanged(nameof(CanExecuteClip));
                _ = OnFeatureDatasetSelectedAsync();
            }
        }
    }

    private string _networksStatus = "No se han cargado las redes";
    public string NetworksStatus
    {
        get => _networksStatus;
        private set
        {
            if (_networksStatus != value)
            {
                _networksStatus = value;
                NotifyPropertyChanged(nameof(NetworksStatus));
            }
        }
    }

    private int _networksLoaded = 0;
    public int NetworksLoaded
    {
        get => _networksLoaded;
        private set
        {
            if (_networksLoaded != value)
            {
                _networksLoaded = value;
                NotifyPropertyChanged(nameof(NetworksLoaded));
            }
        }
    }

    private bool _isBufferEnabled = false;
    public bool IsBufferEnabled
    {
        get => _isBufferEnabled;
        set
        {
            if (_isBufferEnabled != value)
            {
                _isBufferEnabled = value;
                NotifyPropertyChanged(nameof(IsBufferEnabled));
                
                // Si se desactiva el buffer, resetear a 0
                if (!_isBufferEnabled)
                {
                    BufferMeters = 0;
                }
            }
        }
    }

    private double _bufferMeters = 0;
    public double BufferMeters
    {
        get => _bufferMeters;
        set
        {
            // Hacer opcional: clamp a cero si es negativo y permitir 0 sin bloquear
            var newVal = value < 0 ? 0 : value;
            if (Math.Abs(_bufferMeters - newVal) > 0.000001)
            {
                _bufferMeters = newVal;
                NotifyPropertyChanged(nameof(BufferMeters));
            }
        }
    }

    private string _selectionStatus = "No hay polígono seleccionado";
    public string SelectionStatus
    {
        get => _selectionStatus;
        private set
        {
            if (_selectionStatus != value)
            {
                _selectionStatus = value;
                NotifyPropertyChanged(nameof(SelectionStatus));
                NotifyPropertyChanged(nameof(CanExecuteClip));
            }
        }
    }

    public bool CanExecuteClip => (
        !string.IsNullOrWhiteSpace(OutputGeodatabase) &&
        !string.IsNullOrWhiteSpace(FeatureDataset) &&
        BufferMeters >= 0 &&
        _selectedPolygon != null &&
        NetworksLoaded >= 3
    );

    private void OnClearForm()
    {
        FeatureDataset = null;
        IsBufferEnabled = false;
        BufferMeters = 0;
        StatusMessage = string.Empty;
    }

    private void OnOutputGeodatabase()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_geodatabases");

        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar Geodatabase de Salida",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            OutputGeodatabase = item.Path;
        }
    }

    private void OnFeatureDataset()
    {
        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar Geodatabase con Redes",
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            FeatureDataset = item.Path;
        }
    }

    private async Task OnFeatureDatasetSelectedAsync()
    {
        if (string.IsNullOrWhiteSpace(FeatureDataset))
        {
            NetworksStatus = "No se han cargado las redes";
            NetworksLoaded = 0;
            return;
        }

        StatusMessage = "Cargando redes del Feature Dataset...";
        
        try
        {
            await QueuedTask.Run(async () =>
            {
                await LoadNetworksFromFeatureDataset();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando redes: {ex.Message}");
            NetworksStatus = $"❌ Error cargando redes: {ex.Message}";
            NetworksLoaded = 0;
        }
    }

    private async Task LoadNetworksFromFeatureDataset()
    {
        _acueductoPath = null;
        _alcantarilladoSanitarioPath = null;
        _alcantarilladoPluvalPath = null;
        NetworksLoaded = 0;

        try
        {
            var (gdbPath, datasetName) = ParseFeatureDatasetPath(FeatureDataset!);

            if (string.IsNullOrWhiteSpace(gdbPath) || !Directory.Exists(gdbPath))
            {
                NetworksStatus = "❌ Ruta de Geodatabase inválida";
                return;
            }

            var connPath = new ArcGIS.Core.Data.FileGeodatabaseConnectionPath(new Uri(gdbPath));
            using var gdb = new ArcGIS.Core.Data.Geodatabase(connPath);

            // Obtener todas las definiciones de feature classes en la GDB (incluyendo dentro de Feature Datasets)
            var fcDefinitions = gdb.GetDefinitions<ArcGIS.Core.Data.FeatureClassDefinition>();

            foreach (var fcDef in fcDefinitions)
            {
                var fcName = fcDef.GetName();
                var lower = fcName.ToLower();

                if (lower.StartsWith("acd_") && string.IsNullOrWhiteSpace(_acueductoPath))
                {
                    _acueductoPath = System.IO.Path.Combine(gdbPath, fcName);
                }
                else if (lower.StartsWith("als_") && string.IsNullOrWhiteSpace(_alcantarilladoSanitarioPath))
                {
                    _alcantarilladoSanitarioPath = System.IO.Path.Combine(gdbPath, fcName);
                }
                else if (lower.StartsWith("alp_") && string.IsNullOrWhiteSpace(_alcantarilladoPluvalPath))
                {
                    _alcantarilladoPluvalPath = System.IO.Path.Combine(gdbPath, fcName);
                }
            }

            var networks = new List<string>();

            if (!string.IsNullOrWhiteSpace(_acueductoPath))
                networks.Add("✓ Acueducto");

            if (!string.IsNullOrWhiteSpace(_alcantarilladoSanitarioPath))
                networks.Add("✓ Alcantarillado Sanitario");

            if (!string.IsNullOrWhiteSpace(_alcantarilladoPluvalPath))
                networks.Add("✓ Alcantarillado Pluvial");

            NetworksLoaded = networks.Count;

            if (NetworksLoaded == 0)
            {
                NetworksStatus = "❌ No se encontraron redes (acd_*, als_*, alp_*)";
            }
            else if (NetworksLoaded < 3)
            {
                var networksText = string.Join(", ", networks);
                NetworksStatus = $"⚠️ Redes cargadas: {networksText} ({NetworksLoaded}/3)";
            }
            else
            {
                var networksText = string.Join(", ", networks);
                NetworksStatus = $"✓ Redes cargadas: {networksText}";
            }

            StatusMessage = "Redes cargadas correctamente";
            NotifyPropertyChanged(nameof(CanExecuteClip));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en LoadNetworksFromFeatureDataset: {ex.Message}");
            NetworksStatus = $"❌ Error: {ex.Message}";
            NetworksLoaded = 0;
        }
    }

    private (string gdbPath, string datasetName) ParseFeatureDatasetPath(string featureDatasetPath)
    {
        // Si ya es una ruta GDB completa, devolver como está
        if (featureDatasetPath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
        {
            return (featureDatasetPath, "FeatureDataset");
        }

        var idx = featureDatasetPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);

        if (idx >= 0)
        {
            var gdbEnd = idx + 4;
            var gdbPath = featureDatasetPath.Substring(0, gdbEnd);
            var remainder = featureDatasetPath.Length > gdbEnd 
                ? featureDatasetPath.Substring(gdbEnd).TrimStart('\\', '/') 
                : string.Empty;
            var datasetName = string.IsNullOrWhiteSpace(remainder) 
                ? "FeatureDataset" 
                : Path.GetFileName(remainder);
            return (gdbPath, datasetName);
        }

        var datasetNameNoGdb = Path.GetFileName(featureDatasetPath);
        return (Path.GetDirectoryName(featureDatasetPath) ?? string.Empty, 
                string.IsNullOrWhiteSpace(datasetNameNoGdb) ? featureDatasetPath : datasetNameNoGdb);
    }

    private async Task OnExecuteClip()
    {
        if (_selectedPolygon == null)
        {
            StatusMessage = "❌ No hay polígono seleccionado";
            return;
        }

        StatusMessage = "Ejecutando clip...";
        IsBusy = true;

        try
        {
            // Crear GDB de salida con nombre basado en fecha/hora
            var parentFolder = Path.GetDirectoryName(OutputGeodatabase) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var gdbName = $"Clip_{timestamp}.gdb";
            var outputGdbPath = Path.Combine(parentFolder, gdbName);

            // Crear la GDB si no existe
            if (!Directory.Exists(outputGdbPath))
            {
                Directory.CreateDirectory(outputGdbPath);
            }

            var useCase = new EAABAddIn.Src.Application.UseCases.ClipFeatureDatasetUseCase();
            var (success, message) = await useCase.ExecuteAsync(
                outputGdbPath,
                _acueductoPath,
                _alcantarilladoSanitarioPath,
                _alcantarilladoPluvalPath,
                _selectedPolygon,
                BufferMeters);

            StatusMessage = success ? $"✓ {message}" : $"❌ {message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Exception: {ex.StackTrace}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
    {
        QueuedTask.Run(OnRefreshSelectionAsync);
    }

    private async Task OnRefreshSelectionAsync()
    {
        try
        {
            _selectedPolygon = await GetSelectedPolygonAsync();

            if (_selectedPolygon != null)
            {
                var area = _selectedPolygon.Area;
                var areaText = area >= 1 ? $"{area:N0} m²" : $"{area:N2} m²";
                SelectionStatus = $"✓ Polígono seleccionado (Área: {areaText})";
            }
            else
            {
                SelectionStatus = "❌ No hay polígono seleccionado";
            }

            NotifyPropertyChanged(nameof(CanExecuteClip));
            (ExecuteClipCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al actualizar selección: {ex.Message}");
            SelectionStatus = "⚠️ Error al detectar selección";
        }
    }

    private async Task<Polygon?> GetSelectedPolygonAsync()
    {
        return await QueuedTask.Run(() =>
        {
            var mv = MapView.Active;
            if (mv?.Map == null)
                return null;

            var selectionSet = mv.Map.GetSelection();
            if (selectionSet.Count == 0)
                return null;

            foreach (var layerSelection in selectionSet.ToDictionary())
            {
                var layer = layerSelection.Key;
                var oids = layerSelection.Value;

                if (layer is FeatureLayer featureLayer &&
                    featureLayer.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolygon)
                {
                    if (oids.Count != 1)
                        continue;

                    var queryFilter = new QueryFilter
                    {
                        ObjectIDs = oids
                    };

                    using (var featureCursor = featureLayer.Search(queryFilter))
                    {
                        if (featureCursor.MoveNext())
                        {
                            using (var feature = featureCursor.Current as Feature)
                            {
                                if (feature?.GetShape() is Polygon polygon)
                                {
                                    return polygon;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        });
    }
}
