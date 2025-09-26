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

namespace EAABAddIn.Src.Presentation.ViewModel
{
    internal class MassiveGeocodeViewModel : BusyViewModelBase
    {
        public override string DisplayName => "Geocodificación Masiva";
        public override string Tooltip => "Buscar múltiples direcciones a la vez desde un archivo .xlsx o .xls";

        private string _fileInput;
        public string FileInput
        {
            get => _fileInput;
            set
            {
                if (_fileInput != value)
                {
                    _fileInput = value;
                    NotifyPropertyChanged(nameof(FileInput));
                }
            }
        }

        private string _gdbPath;
        public string GdbPath
        {
            get => _gdbPath;
            set
            {
                if (_gdbPath != value)
                {
                    _gdbPath = value;
                    NotifyPropertyChanged(nameof(GdbPath));
                }
            }
        }

        public ICommand OpenFileDialogCommand { get; private set; }
        public ICommand SearchCommand { get; private set; }
        public ICommand BrowseGdbCommand { get; }

        public MassiveGeocodeViewModel()
        {
            var path = Project.Current.DefaultGeodatabasePath;

            GdbPath = path;
            FileInput = string.Empty;
            OpenFileDialogCommand = new RelayCommand(Browse_Click);
            SearchCommand = new AsyncRelayCommand(Search_Click);
            BrowseGdbCommand = new RelayCommand(BrowseGdb);
        }

        private void Browse_Click()
        {
            var dialog = new OpenFileDialog()
            {
                Filter = "Archivos Excel (*.xlsx;*.xls)|*.xlsx;*.xls",
                Title = "Seleccionar archivo de direcciones"
            };

            if (dialog.ShowDialog() == true)
            {
                FileInput = dialog.FileName;
            }
        }

        private async Task Search_Click()
        {
            IsBusy = true;
            StatusMessage = "Procesando geocodificación masiva...";

            try
            {
                List<RegistroDireccion> registros;

                try
                {
                    registros = LeerDireccionesExcel(FileInput);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al leer el archivo Excel: {ex.Message}", "Error");
                    return;
                }

                if (registros.Count == 0)
                {
                    MessageBox.Show("No se encontraron direcciones en el archivo", "Información");
                    return;
                }

                var engine = Module1.Settings.motor.ToDBEngine();
                var usecase = new AddressSearchUseCase(engine);
                IPtAddressGralEntityRepository repo = engine switch
                {
                    DBEngine.Oracle => new PtAddressGralOracleRepository(),
                    DBEngine.OracleSDE => new PtAddressGralOracleRepository(),
                    DBEngine.PostgreSQL => new PtAddressGralPostgresRepository(),
                    DBEngine.PostgreSQLSDE => new PtAddressGralPostgresRepository(),
                    _ => null
                };
                if (repo == null)
                {
                    MessageBox.Show("Motor de base de datos no soportado.", "Error");
                    return;
                }

                int encontrados = 0, noEncontrados = 0;
                int total = registros.Count;
                int contador = 0;

                ResultsLayerService.ClearPending();

                await QueuedTask.Run(() =>
                {
                    var ciudadesDict = repo.GetAllCities().ToDictionary(c => c.CityCode, c => c.CityDesc);

                    foreach (var registro in registros)
                    {
                        StatusMessage = $"Procesando {++contador}/{total}...";

                        try
                        {
                            var resultados = usecase.Invoke(
                                address: registro.Direccion,
                                cityCode: registro.Poblacion,
                                cityDesc: ciudadesDict.TryGetValue(registro.Poblacion, out var cityDesc) ? cityDesc : null,
                                gdbPath: GdbPath,
                                showNoResultsMessage: false
                            );

                            if (resultados.Count == 0)
                            {
                                noEncontrados++;
                                continue;
                            }

                            if (!(resultados[0].Latitud.HasValue && resultados[0].Longitud.HasValue))
                            {
                                noEncontrados++;
                                continue;
                            }

                            var entidad = resultados[0];
                            var src = (entidad.Source ?? string.Empty).ToLowerInvariant();
                            // Clasificación unificada ScoreText / Geocoder
                            if (src.Contains("cat") || src.Contains("catastro"))
                            {
                                entidad.ScoreText = "Aproximada por Catastro";
                                entidad.Source = "CATASTRO";
                            }
                            else if (src.Contains("esri"))
                            {
                                // Score numérico con prefijo ESRI
                                if (entidad.Score.HasValue)
                                    entidad.ScoreText = $"ESRI {Math.Round(entidad.Score.Value, 2)}";
                                else
                                    entidad.ScoreText = "ESRI";
                                entidad.Source = "ESRI";
                            }
                            else // EAAB u otro => Exacta si no tiene marca previa
                            {
                                entidad.ScoreText = string.IsNullOrWhiteSpace(entidad.ScoreText) ? "Exacta" : entidad.ScoreText;
                                entidad.Source = "EAAB";
                            }

                            // Ajustar campo Direccion para almacenar la dirección encontrada (prioridad EAAB > Catastro > Original > MainStreet)
                            if (!string.IsNullOrWhiteSpace(entidad.FullAddressEAAB))
                                entidad.FullAddressOld = entidad.FullAddressEAAB; // reutilizamos FullAddressOld para visualización original previa si existiera
                            else if (!string.IsNullOrWhiteSpace(entidad.FullAddressCadastre))
                                entidad.FullAddressOld = entidad.FullAddressCadastre;
                            else if (string.IsNullOrWhiteSpace(entidad.FullAddressOld))
                                entidad.FullAddressOld = entidad.MainStreet ?? string.Empty;

                            ResultsLayerService.AddPointToMemory(entidad);
                            encontrados++;                          
                        }
                        catch
                        {
                            noEncontrados++;
                        }
                    }
                });

                await ResultsLayerService.CommitPointsAsync(GdbPath);

                MessageBox.Show(
                    $"Encontradas: {encontrados}\nNo encontradas: {noEncontrados}\nTotal procesadas: {encontrados + noEncontrados}",
                    "Resultado geocodificación"
                );
            }
            finally
            {
                IsBusy = false;
                StatusMessage = string.Empty;
            }
        }

        private List<RegistroDireccion> LeerDireccionesExcel(string filePath)
        {
            var lista = new List<RegistroDireccion>();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                // Saltar cabecera
                reader.Read();

                while (reader.Read())
                {
                    var registro = new RegistroDireccion
                    {
                        Identificador = reader.GetValue(0)?.ToString(),
                        Direccion = reader.GetValue(1)?.ToString(),
                        Poblacion = reader.GetValue(2)?.ToString()
                    };

                    if (!string.IsNullOrWhiteSpace(registro.Direccion) &&
                        !string.IsNullOrWhiteSpace(registro.Poblacion))
                    {
                        lista.Add(registro);
                    }
                }
            }

            return lista;
        }

        private void BrowseGdb()
        {
            var filter = new BrowseProjectFilter("esri_browseDialogFilters_geodatabases");

            var dlg = new OpenItemDialog
            {
                Title = "Seleccionar Geodatabase",
                BrowseFilter = filter,
                MultiSelect = false,
                InitialLocation = !string.IsNullOrWhiteSpace(GdbPath)
                    ? System.IO.Path.GetDirectoryName(GdbPath)
                    : Project.Current?.HomeFolderPath
            };

            var ok = dlg.ShowDialog();
            if (ok == true && dlg.Items != null && dlg.Items.Any())
            {
                var item = dlg.Items.First();
                // item.Path devolverá la ruta a la carpeta .gdb seleccionada
                GdbPath = item.Path;
            }
        }

        public class RegistroDireccion
        {
            public string Identificador { get; set; }
            public string Direccion { get; set; }
            public string Poblacion { get; set; }
        }
    }
}
