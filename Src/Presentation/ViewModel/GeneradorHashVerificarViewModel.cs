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
    /// ViewModel para la pestaña de Verificar Hash
    /// </summary>
    internal class GeneradorHashVerificarViewModel : BusyViewModelBase
    {
        public override string DisplayName => "Verificar Hash";
        public override string Tooltip => "Verificar integridad de archivos usando hashes SHA256";

        private readonly GenerarHashUseCase _generarHashUseCase;

        // ==================== COMANDOS ====================
        public ICommand ExaminarArchivoCommand { get; }
        public ICommand VerificarHashCommand { get; }
        public ICommand LimpiarCommand { get; }

        // ==================== PROPIEDADES ====================

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

        public GeneradorHashVerificarViewModel()
        {
            _generarHashUseCase = new GenerarHashUseCase();

            ExaminarArchivoCommand = new RelayCommand(OnExaminarArchivo);
            VerificarHashCommand = new AsyncRelayCommand(OnVerificarHashAsync, () => PuedeVerificarHash);
            LimpiarCommand = new RelayCommand(OnLimpiar);
        }

        // ==================== MÉTODOS ====================

        private void OnExaminarArchivo()
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

        private void OnLimpiar()
        {
            ArchivoVerificar = string.Empty;
            ArchivoHash = string.Empty;
            ResultadoVerificar = string.Empty;
            StatusMessage = string.Empty;
            VerificacionExitosa = false;
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
