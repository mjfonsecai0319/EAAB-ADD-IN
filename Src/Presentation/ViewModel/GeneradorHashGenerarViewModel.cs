#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using EAABAddIn.Src.Application.UseCases;
using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel
{
    /// <summary>
    /// ViewModel para la pestaña de Generar Hash
    /// </summary>
    internal class GeneradorHashGenerarViewModel : BusyViewModelBase
    {
        public override string DisplayName => "Generar Hash";
        public override string Tooltip => "Generar hashes SHA256 de archivos y carpetas";

        private readonly GenerarHashUseCase _generarHashUseCase;

        // ==================== COMANDOS ====================
        public ICommand ExaminarCarpetaCommand { get; }
        public ICommand GenerarHashCommand { get; }
        public ICommand LimpiarCommand { get; }

        // ==================== PROPIEDADES ====================
        
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

        public bool PuedeGenerarHash => !string.IsNullOrWhiteSpace(RutaCarpetaGenerar) && !IsBusy;

        // ==================== CONSTRUCTOR ====================

        public GeneradorHashGenerarViewModel()
        {
            _generarHashUseCase = new GenerarHashUseCase();

            ExaminarCarpetaCommand = new RelayCommand(OnExaminarCarpeta);
            GenerarHashCommand = new AsyncRelayCommand(OnGenerarHashAsync, () => PuedeGenerarHash);
            LimpiarCommand = new RelayCommand(OnLimpiar);

            ActualizarPlaceholder();
        }

        // ==================== MÉTODOS ====================

        private void OnExaminarCarpeta()
        {
            var dlg = new OpenItemDialog
            {
                Title = EsFuncionalidad1 ? "Seleccionar Carpeta o GDB" : "Seleccionar Carpeta",
                MultiSelect = false,
                InitialLocation = Project.Current?.HomeFolderPath,
                BrowseFilter = BrowseProjectFilter.GetFilter("esri_browseDialogFilters_folders")
            };

            var ok = dlg.ShowDialog();
            if (ok == true && dlg.Items != null && dlg.Items.Count > 0)
            {
                var item = dlg.Items[0];
                RutaCarpetaGenerar = item.Path;
            }
        }

        private async Task OnGenerarHashAsync()
        {
            if (string.IsNullOrWhiteSpace(RutaCarpetaGenerar))
            {
                ResultadoGenerar = "❌ Por favor seleccione una carpeta";
                return;
            }

            try
            {
                IsBusy = true;
                ResultadoGenerar = string.Empty;
                StatusMessage = "Procesando...";

                if (EsFuncionalidad1)
                {
                    // Funcionalidad 1: Comprimir GDB y Generar Hash
                    StatusMessage = "Comprimiendo carpeta...";
                    var (ok, zipPath, hashPath, message) = await _generarHashUseCase.ComprimirGdbYGenerarHash(RutaCarpetaGenerar);
                    
                    ResultadoGenerar = message;
                    StatusMessage = ok ? "✅ Completado" : "❌ Error";

                    if (ok)
                    {
                        // Abrir carpeta contenedora
                        var carpeta = Path.GetDirectoryName(zipPath);
                        if (!string.IsNullOrWhiteSpace(carpeta) && Directory.Exists(carpeta))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", carpeta);
                        }
                    }
                }
                else if (EsFuncionalidad2)
                {
                    // Funcionalidad 2: Generar Hash de Archivos en Carpeta
                    StatusMessage = "Calculando hashes de archivos...";
                    var (ok, resumenPath, hashes, message) = await _generarHashUseCase.GenerarHashArchivosEnCarpeta(RutaCarpetaGenerar);
                    
                    ResultadoGenerar = message;
                    StatusMessage = ok ? "✅ Completado" : "❌ Error";

                    if (ok)
                    {
                        // Abrir carpeta contenedora
                        if (Directory.Exists(RutaCarpetaGenerar))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", RutaCarpetaGenerar);
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
        }

        private void ActualizarPlaceholder()
        {
            PlaceholderCarpeta = EsFuncionalidad1 
                ? "Seleccionar carpeta/GDB a comprimir..." 
                : "Seleccionar carpeta con archivos...";
        }
    }
}
