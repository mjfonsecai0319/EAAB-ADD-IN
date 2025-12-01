#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Diagnostics;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using EAABAddIn.Src.Application.UseCases;
using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel
{
    internal class GeneradorHashGenerarViewModel : BusyViewModelBase
    {
        public override string DisplayName => "Generar Hash";
        public override string Tooltip => "Generar hashes SHA256 de archivos y carpetas";

        private readonly GenerarHashUseCase _generarHashUseCase;

        public ICommand ExaminarCarpetaCommand { get; }
        public ICommand GenerarHashCommand { get; }
        public ICommand LimpiarCommand { get; }

        
        private int _funcionalidadSeleccionada = 0;
        public int FuncionalidadSeleccionada
        {
            get => _funcionalidadSeleccionada;
            set
            {
                if (_funcionalidadSeleccionada != value)
                {
                    _funcionalidadSeleccionada = value;
                    NotifyPropertyChanged(nameof(FuncionalidadSeleccionada));
                    NotifyPropertyChanged(nameof(EsFuncionalidad1));
                    NotifyPropertyChanged(nameof(EsFuncionalidad2));
                    ActualizarPlaceholder();
                }
            }
        }

        public bool EsFuncionalidad1 => FuncionalidadSeleccionada == 0;
        public bool EsFuncionalidad2 => FuncionalidadSeleccionada == 1;

        private string _rutaCarpetaGenerar = string.Empty;
        public string RutaCarpetaGenerar
        {
            get => _rutaCarpetaGenerar;
            set
            {
                if (_rutaCarpetaGenerar != value)
                {
                    _rutaCarpetaGenerar = value;
                    NotifyPropertyChanged(nameof(RutaCarpetaGenerar));
                    NotifyPropertyChanged(nameof(PuedeGenerarHash));
                    (GenerarHashCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _placeholderCarpeta = "Seleccionar carpeta/GDB...";
        public string PlaceholderCarpeta
        {
            get => _placeholderCarpeta;
            set
            {
                if (_placeholderCarpeta != value)
                {
                    _placeholderCarpeta = value;
                    NotifyPropertyChanged(nameof(PlaceholderCarpeta));
                }
            }
        }

        private string _resultadoGenerar = string.Empty;
        public string ResultadoGenerar
        {
            get => _resultadoGenerar;
            set
            {
                if (_resultadoGenerar != value)
                {
                    _resultadoGenerar = value;
                    NotifyPropertyChanged(nameof(ResultadoGenerar));
                }
            }
        }

        private string _outputLocation = string.Empty;
        public string OutputLocation
        {
            get => _outputLocation;
            private set
            {
                if (_outputLocation != value)
                {
                    _outputLocation = value;
                    NotifyPropertyChanged(nameof(OutputLocation));
                    HasOutputLocation = !string.IsNullOrWhiteSpace(_outputLocation);
                    (OpenOutputLocationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _hasOutputLocation = false;
        public bool HasOutputLocation
        {
            get => _hasOutputLocation;
            private set
            {
                if (_hasOutputLocation != value)
                {
                    _hasOutputLocation = value;
                    NotifyPropertyChanged(nameof(HasOutputLocation));
                }
            }
        }

        public bool PuedeGenerarHash => !string.IsNullOrWhiteSpace(RutaCarpetaGenerar) && !IsBusy;


        public GeneradorHashGenerarViewModel()
        {
            _generarHashUseCase = new GenerarHashUseCase();

            ExaminarCarpetaCommand = new RelayCommand(OnExaminarCarpeta);
            GenerarHashCommand = new AsyncRelayCommand(OnGenerarHashAsync, () => PuedeGenerarHash);
            LimpiarCommand = new RelayCommand(OnLimpiar);

            ActualizarPlaceholder();
        }


        private void OnExaminarCarpeta()
        {
            var filterName = EsFuncionalidad1 
                ? "esri_browseDialogFilters_all"  
                : "esri_browseDialogFilters_folders"; 
            
            var filter = new BrowseProjectFilter(filterName);
            var dlg = new OpenItemDialog
            {
                Title = EsFuncionalidad1 ? "Seleccionar Carpeta o GDB" : "Seleccionar Carpeta",
                BrowseFilter = filter,
                MultiSelect = false,
                InitialLocation = Project.Current?.HomeFolderPath
            };

            if (dlg.ShowDialog() == true && dlg.Items != null && dlg.Items.Count > 0)
            {
                var path = dlg.Items[0].Path;
                
                if (Uri.TryCreate(path, UriKind.Absolute, out Uri uri))
                {
                    if (uri.IsFile)
                    {
                        path = uri.LocalPath;
                    }
                }
                
                RutaCarpetaGenerar = path;
            }
        }

        private async Task OnGenerarHashAsync()
        {
            if (string.IsNullOrWhiteSpace(RutaCarpetaGenerar))
            {
                ResultadoGenerar = "❌ Por favor seleccione una carpeta";
                return;
            }

            if (!Directory.Exists(RutaCarpetaGenerar))
            {
                ResultadoGenerar = $"❌ La carpeta no existe:\n{RutaCarpetaGenerar}";
                return;
            }

            try
            {
                IsBusy = true;
                ResultadoGenerar = string.Empty;
                StatusMessage = "Procesando...";

                if (EsFuncionalidad1)
                {
                    StatusMessage = "Comprimiendo carpeta...";
                    var (ok, zipPath, hashPath, message) = await _generarHashUseCase.ComprimirGdbYGenerarHash(RutaCarpetaGenerar);
                    
                    ResultadoGenerar = message;
                    StatusMessage = ok ? "✅ Completado" : "❌ Error";

                    if (ok)
                    {
                        // Establecer la ubicación de salida (carpeta contenedora)
                        var carpeta = Path.GetDirectoryName(zipPath) ?? string.Empty;
                        OutputLocation = carpeta;

                        // Abrir carpeta contenedora (comportamiento previo)
                        if (!string.IsNullOrWhiteSpace(carpeta) && Directory.Exists(carpeta))
                        {
                            var psi = new ProcessStartInfo { FileName = carpeta, UseShellExecute = true };
                            Process.Start(psi);
                        }
                    }
                }
                else if (EsFuncionalidad2)
                {
                    StatusMessage = "Calculando hashes de archivos...";
                    var (ok, resumenPath, hashes, message) = await _generarHashUseCase.GenerarHashArchivosEnCarpeta(RutaCarpetaGenerar);
                    
                    ResultadoGenerar = message;
                    StatusMessage = ok ? "✅ Completado" : "❌ Error";

                    if (ok)
                    {
                        // Establecer la ubicación de salida (carpeta usada)
                        OutputLocation = RutaCarpetaGenerar;

                        // Abrir carpeta contenedora (comportamiento previo)
                        if (Directory.Exists(RutaCarpetaGenerar))
                        {
                            var psi = new ProcessStartInfo { FileName = RutaCarpetaGenerar, UseShellExecute = true };
                            Process.Start(psi);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ResultadoGenerar = $"❌ Error inesperado: {ex.Message}";
                StatusMessage = "❌ Error";
            }
            finally
            {
                IsBusy = false;
                (GenerarHashCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void OnLimpiar()
        {
            RutaCarpetaGenerar = string.Empty;
            ResultadoGenerar = string.Empty;
            StatusMessage = string.Empty;
            FuncionalidadSeleccionada = 0;
            OutputLocation = string.Empty;
        }

        private void ActualizarPlaceholder()
        {
            PlaceholderCarpeta = EsFuncionalidad1 
                ? "Seleccionar carpeta/GDB a comprimir..." 
                : "Seleccionar carpeta con archivos...";
        }

        private ICommand? _openOutputLocationCommandBacking;
        public ICommand OpenOutputLocationCommand => _openOutputLocationCommandBacking ??= new RelayCommand(OnOpenOutputLocation, () => HasOutputLocation);

        private void OnOpenOutputLocation()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(OutputLocation))
                {
                    StatusMessage = "❌ No hay ubicación de salida";
                    return;
                }

                var path = OutputLocation;
                if (!Directory.Exists(path))
                {
                    StatusMessage = $"❌ La carpeta no existe: {path}";
                    return;
                }

                var psi = new ProcessStartInfo { FileName = path, UseShellExecute = true };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error abriendo carpeta: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }
}
