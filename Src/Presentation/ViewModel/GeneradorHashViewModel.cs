#nullable enable

using System;
using System.Collections.Generic;
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
    /// ViewModel para la funcionalidad de Generador de Hash
    /// </summary>
    internal class GeneradorHashViewModel : BusyViewModelBase
    {
        public override string DisplayName => "Generador de Hash";
        public override string Tooltip => "Generar y verificar hashes SHA256 de archivos y carpetas";

        private readonly GenerarHashUseCase _generarHashUseCase;

        // ==================== COMANDOS ====================
        public ICommand ExaminarCarpetaCommand { get; }
        public ICommand ExaminarArchivoVerificarCommand { get; }
        public ICommand GenerarHashCommand { get; }
        public ICommand VerificarHashCommand { get; }
        public ICommand LimpiarGenerarCommand { get; }
        public ICommand LimpiarVerificarCommand { get; }

        // ==================== PROPIEDADES - TAB GENERAR ====================
        
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

        // ==================== PROPIEDADES - TAB VERIFICAR ====================

        private string _archivoVerificar = string.Empty;
        public string ArchivoVerificar
        {
            get => _archivoVerificar;
            set
            {
                if (_archivoVerificar != value)
                {
                    _archivoVerificar = value;
                    NotifyPropertyChanged(nameof(ArchivoVerificar));
                    NotifyPropertyChanged(nameof(PuedeVerificarHash));
                    (VerificarHashCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                    
                    // Buscar automáticamente el archivo hash
                    BuscarArchivoHashAutomatico();
                }
            }
        }

        private string _archivoHash = string.Empty;
        public string ArchivoHash
        {
            get => _archivoHash;
            set
            {
                if (_archivoHash != value)
                {
                    _archivoHash = value;
                    NotifyPropertyChanged(nameof(ArchivoHash));
                }
            }
        }

        private string _resultadoVerificar = string.Empty;
        public string ResultadoVerificar
        {
            get => _resultadoVerificar;
            set
            {
                if (_resultadoVerificar != value)
                {
                    _resultadoVerificar = value;
                    NotifyPropertyChanged(nameof(ResultadoVerificar));
                }
            }
        }

        private bool _verificacionExitosa = false;
        public bool VerificacionExitosa
        {
            get => _verificacionExitosa;
            set
            {
                if (_verificacionExitosa != value)
                {
                    _verificacionExitosa = value;
                    NotifyPropertyChanged(nameof(VerificacionExitosa));
                }
            }
        }

        public bool PuedeVerificarHash => !string.IsNullOrWhiteSpace(ArchivoVerificar) && !IsBusy;

        // ==================== CONSTRUCTOR ====================

        public GeneradorHashViewModel()
        {
            _generarHashUseCase = new GenerarHashUseCase();

            ExaminarCarpetaCommand = new RelayCommand(OnExaminarCarpeta);
            ExaminarArchivoVerificarCommand = new RelayCommand(OnExaminarArchivoVerificar);
            GenerarHashCommand = new AsyncRelayCommand(OnGenerarHashAsync, () => PuedeGenerarHash);
            VerificarHashCommand = new AsyncRelayCommand(OnVerificarHashAsync, () => PuedeVerificarHash);
            LimpiarGenerarCommand = new RelayCommand(OnLimpiarGenerar);
            LimpiarVerificarCommand = new RelayCommand(OnLimpiarVerificar);

            ActualizarPlaceholder();
        }

        // ==================== MÉTODOS - EXAMINAR ====================

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

        private void OnExaminarArchivoVerificar()
        {
            var dlg = new OpenItemDialog
            {
                Title = "Seleccionar Archivo a Verificar",
                MultiSelect = false,
                InitialLocation = Project.Current?.HomeFolderPath,
                BrowseFilter = BrowseProjectFilter.GetFilter("esri_browseDialogFilters_all")
            };

            var ok = dlg.ShowDialog();
            if (ok == true && dlg.Items != null && dlg.Items.Count > 0)
            {
                var item = dlg.Items[0];
                ArchivoVerificar = item.Path;
            }
        }

        // ==================== MÉTODOS - GENERAR HASH ====================

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

        // ==================== MÉTODOS - VERIFICAR HASH ====================

        private async Task OnVerificarHashAsync()
        {
            if (string.IsNullOrWhiteSpace(ArchivoVerificar))
            {
                ResultadoVerificar = "❌ Por favor seleccione un archivo";
                return;
            }

            try
            {
                IsBusy = true;
                ResultadoVerificar = string.Empty;
                VerificacionExitosa = false;
                StatusMessage = "Verificando integridad...";

                var (ok, coinciden, hashEsperado, hashActual, message) = await _generarHashUseCase.VerificarIntegridadArchivo(ArchivoVerificar);
                
                ResultadoVerificar = message;
                VerificacionExitosa = ok && coinciden;
                StatusMessage = coinciden ? "✅ Verificación exitosa" : "❌ Verificación fallida";
            }
            catch (Exception ex)
            {
                ResultadoVerificar = $"❌ Error inesperado: {ex.Message}";
                StatusMessage = "❌ Error";
                VerificacionExitosa = false;
            }
            finally
            {
                IsBusy = false;
                (VerificarHashCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // ==================== MÉTODOS - LIMPIAR ====================

        private void OnLimpiarGenerar()
        {
            RutaCarpetaGenerar = string.Empty;
            ResultadoGenerar = string.Empty;
            StatusMessage = string.Empty;
            FuncionalidadSeleccionada = 0;
        }

        private void OnLimpiarVerificar()
        {
            ArchivoVerificar = string.Empty;
            ArchivoHash = string.Empty;
            ResultadoVerificar = string.Empty;
            StatusMessage = string.Empty;
            VerificacionExitosa = false;
        }

        // ==================== MÉTODOS AUXILIARES ====================

        private void ActualizarPlaceholder()
        {
            PlaceholderCarpeta = EsFuncionalidad1 
                ? "Seleccionar carpeta/GDB a comprimir..." 
                : "Seleccionar carpeta con archivos...";
        }

        private void BuscarArchivoHashAutomatico()
        {
            if (string.IsNullOrWhiteSpace(ArchivoVerificar) || !File.Exists(ArchivoVerificar))
            {
                ArchivoHash = string.Empty;
                return;
            }

            try
            {
                var hashPath = Application.Services.HashService.BuscarArchivoHashEnCarpeta(ArchivoVerificar);
                ArchivoHash = hashPath ?? "❌ No se encontró archivo HASH";
            }
            catch (Exception ex)
            {
                ArchivoHash = $"❌ Error: {ex.Message}";
            }
        }
    }
}
