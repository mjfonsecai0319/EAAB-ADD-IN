#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using EAABAddIn.Src.Application.UseCases;
using EAABAddIn.Src.Application.Services;
using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel
{

    internal class GeneradorHashVerificarViewModel : BusyViewModelBase
    {
        public override string DisplayName => "Verificar Hash";
        public override string Tooltip => "Verificar integridad de archivos usando hashes SHA256";

        private readonly GenerarHashUseCase _generarHashUseCase;

        public ICommand ExaminarArchivoCommand { get; }
        public ICommand VerificarHashCommand { get; }
        public ICommand LimpiarCommand { get; }


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
        private System.Windows.Media.Brush _resultadoForeground = System.Windows.Media.Brushes.Black;

        public System.Windows.Media.Brush ResultadoForeground
        {
            get => _resultadoForeground;
            set
            {
                if (_resultadoForeground != value)
                {
                    _resultadoForeground = value;
                    NotifyPropertyChanged(nameof(ResultadoForeground));
                }
            }
        }

        private string _resultadoMain = string.Empty;
        public string ResultadoMain
        {
            get => _resultadoMain;
            set
            {
                if (_resultadoMain != value)
                {
                    _resultadoMain = value;
                    NotifyPropertyChanged(nameof(ResultadoMain));
                }
            }
        }

        private string _resultadoWarnings = string.Empty;
        public string ResultadoWarnings
        {
            get => _resultadoWarnings;
            set
            {
                if (_resultadoWarnings != value)
                {
                    _resultadoWarnings = value;
                    NotifyPropertyChanged(nameof(ResultadoWarnings));
                    NotifyPropertyChanged(nameof(HasWarnings));
                }
            }
        }

        public bool HasWarnings => !string.IsNullOrWhiteSpace(ResultadoWarnings);

        public string ResultadoVerificar
        {
            get => _resultadoVerificar;
            set
            {
                if (_resultadoVerificar != value)
                {
                    _resultadoVerificar = value;
                    NotifyPropertyChanged(nameof(ResultadoVerificar));
                    NotifyPropertyChanged(nameof(TieneResultado));
                    // Separar solo líneas de archivo/hashes y advertencias (sin encabezados)
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(_resultadoVerificar))
                        {
                            var lines = _resultadoVerificar.Replace("\r\n", "\n").Split('\n');
                            var mainLines = new System.Collections.Generic.List<string>();
                            var warningLines = new System.Collections.Generic.List<string>();

                            foreach (var raw in lines)
                            {
                                var line = raw?.Trim() ?? string.Empty;
                                if (string.IsNullOrEmpty(line))
                                    continue;

                                var low = line.ToLowerInvariant();
                                // Detectar líneas de advertencia/error
                                if (low.StartsWith("⚠") || low.Contains("⚠️") || low.Contains("los hashes no coinciden") || low.Contains("el archivo puede estar corrupto"))
                                {
                                    warningLines.Add(line);
                                }
                                // Saltarse encabezados de éxito/error
                                else if (low.Contains("integridad verificada") || low.Contains("error de integridad") || low.Contains("✅") || low.Contains("❌"))
                                {
                                    continue;
                                }
                                // Incluir líneas de información (archivo, hashes, etc)
                                else
                                {
                                    mainLines.Add(line);
                                }
                            }

                            ResultadoMain = string.Join("\n", mainLines);
                            ResultadoWarnings = warningLines.Count > 0 ? string.Join("\n", warningLines) : string.Empty;
                        }
                        else
                        {
                            ResultadoMain = string.Empty;
                            ResultadoWarnings = string.Empty;
                        }
                    }
                    catch
                    {
                        ResultadoMain = _resultadoVerificar;
                        ResultadoWarnings = string.Empty;
                    }
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
        
        public bool TieneResultado => !string.IsNullOrWhiteSpace(ResultadoVerificar);
        public bool TieneMensajeEstado => !string.IsNullOrWhiteSpace(StatusMessage);

        private string _statusMessageLocal = string.Empty;
        public new string StatusMessage
        {
            get => base.StatusMessage;
            set
            {
                if (base.StatusMessage != value)
                {
                    base.StatusMessage = value;
                    NotifyPropertyChanged(nameof(TieneMensajeEstado));
                }
            }
        }


        public GeneradorHashVerificarViewModel()
        {
            _generarHashUseCase = new GenerarHashUseCase();

            ExaminarArchivoCommand = new RelayCommand(OnExaminarArchivo);
            VerificarHashCommand = new AsyncRelayCommand(OnVerificarHashAsync, () => PuedeVerificarHash);
            LimpiarCommand = new RelayCommand(OnLimpiar);
            
            System.Diagnostics.Debug.WriteLine("GeneradorHashVerificarViewModel: Constructor ejecutado");
        }


        private void OnExaminarArchivo()
        {
            var filter = new BrowseProjectFilter("esri_browseDialogFilters_all");
            var dlg = new OpenItemDialog
            {
                Title = "Seleccionar Archivo a Verificar",
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
                
                ArchivoVerificar = path;
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

                System.Diagnostics.Debug.WriteLine($"Iniciando verificación de: {ArchivoVerificar}");

                var (ok, coinciden, hashEsperado, hashActual, message) = await _generarHashUseCase.VerificarIntegridadArchivo(ArchivoVerificar);
                
                System.Diagnostics.Debug.WriteLine($"Verificación completada: ok={ok}, coinciden={coinciden}");
                System.Diagnostics.Debug.WriteLine($"Mensaje: {message}");

                ResultadoVerificar = message;
                VerificacionExitosa = ok && coinciden;
                StatusMessage = coinciden ? "✅ Verificación exitosa" : "❌ Verificación fallida";
            }
            catch (Exception ex)
            {
                var errorCompleto = $"❌ Error inesperado: {ex.Message}\n\nStackTrace:\n{ex.StackTrace}";
                System.Diagnostics.Debug.WriteLine(errorCompleto);
                ResultadoVerificar = errorCompleto;
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
                var hashPath = HashService.BuscarArchivoHashEnCarpeta(ArchivoVerificar);
                ArchivoHash = hashPath ?? "❌ No se encontró archivo HASH";
            }
            catch (Exception ex)
            {
                ArchivoHash = $"❌ Error: {ex.Message}";
            }
        }
    }
}
