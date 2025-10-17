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
    public class MassiveGeocodeViewModel : BusyViewModelBase
    {
        public override string DisplayName => "Geocodificación Masiva";
        public override string Tooltip => "Buscar múltiples direcciones a la vez desde un archivo .xlsx o .xls";

        private string _fileInput;
        private string _gdbPath;
        private string _cityCodesPreview;

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

        public class CityCodePreviewItem
        {
            public string CityCode { get; set; }
            public string CityDesc { get; set; }
        }

        public System.Collections.ObjectModel.ObservableCollection<CityCodePreviewItem> CityCodes { get; } = new();

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

            _ = InitCityCodesPreview();
        }

        private async Task InitCityCodesPreview()
        {
            try
            {
                var engine = Module1.Settings.motor.ToDBEngine();
                IPtAddressGralEntityRepository repo = engine switch
                {
                    DBEngine.Oracle => new PtAddressGralOracleRepository(),
                    DBEngine.OracleSDE => new PtAddressGralOracleRepository(),
                    DBEngine.PostgreSQL => new PtAddressGralPostgresRepository(),
                    DBEngine.PostgreSQLSDE => new PtAddressGralPostgresRepository(),
                    _ => null
                };
                if (repo == null) return;
                var lista = await QueuedTask.Run(() => repo.GetAllCities()
                    .Where(c => !string.IsNullOrWhiteSpace(c.CityCode))
                    .GroupBy(c => c.CityCode.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(c => c.CityCode)
                    .Take(25)
                    .Select(c => new CityCodePreviewItem { CityCode = c.CityCode.Trim(), CityDesc = c.CityDesc })
                    .ToList());
                if (lista.Count == 0) return;
                CityCodes.Clear();
                foreach (var item in lista) CityCodes.Add(item);
            }
            catch { }
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

                if (registros.Count == 0)
                {
                    MessageBox.Show("El archivo no contiene filas de datos válidas (tras la cabecera).", "Archivo vacío");
                    return;
                }

                var filasSinDireccion = registros.Where(r => string.IsNullOrWhiteSpace(r.Direccion)).Select(r => r.Identificador).ToList();
                var filasSinCodigoCiudad = registros.Where(r => string.IsNullOrWhiteSpace(r.Poblacion)).Select(r => r.Identificador).ToList();
                if (filasSinDireccion.Any())
                {
                    MessageBox.Show($"Se encontraron {filasSinDireccion.Count} filas sin dirección en la columna 2. Corrija antes de continuar.", "Direcciones faltantes");
                    return;
                }
                if (filasSinCodigoCiudad.Any())
                {
                    MessageBox.Show($"Se encontraron {filasSinCodigoCiudad.Count} filas sin código de ciudad en la columna 3. Corrija antes de continuar.", "Códigos de ciudad faltantes");
                    return;
                }

                List<string> codigosValidos;
                try
                {
                    codigosValidos = await QueuedTask.Run(() =>
                    {
                        return repo.GetAllCities()
                            .Select(c => c.CityCode?.Trim())
                            .Where(c => !string.IsNullOrWhiteSpace(c))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudieron obtener los códigos de ciudad desde la base de datos: {ex.Message}", "Error");
                    return;
                }
                if (codigosValidos.Count == 0)
                {
                    MessageBox.Show("La base de datos no devolvió códigos de ciudad válidos.", "Error");
                    return;
                }

                var detallesCodigos = registros
                    .Select(r => r.Poblacion.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();

                var codigosInvalidos = detallesCodigos
                    .Where(c => !codigosValidos.Contains(c, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (codigosInvalidos.Count == detallesCodigos.Count)
                {
                    var muestra = string.Join(", ", codigosInvalidos.Take(10));
                    var algunosValidosEjemplo = string.Join(", ", codigosValidos.Take(10));
                    MessageBox.Show(
                        "ERROR DE FORMATO EN COLUMNA 3 (CÓDIGO CIUDAD)\n\n" +
                        "Todos los valores detectados son inválidos para el sistema.\n" +
                        $"Valores leídos (muestra): {muestra}\n\n" +
                        "Asegúrese de ingresar el CÓDIGO numérico (o alfanumérico) de la ciudad, no su nombre.\n" +
                        (string.IsNullOrWhiteSpace(algunosValidosEjemplo) ? string.Empty : $"Ejemplos válidos: {algunosValidosEjemplo}"),
                        "Códigos inválidos");
                    return;
                }

                if (codigosInvalidos.Count > 0)
                {
                    var muestraInv = string.Join(", ", codigosInvalidos.Take(15));
                    MessageBox.Show(
                        "Se detectaron códigos de ciudad NO válidos en la columna 3. Estas filas serán ignoradas al geocodificar.\n\n" +
                        $"Códigos inválidos (muestra): {muestraInv}\n" +
                        "Revise el archivo para corregirlos. Solo se procesarán las filas con códigos válidos.",
                        "Advertencia códigos inválidos");
                }

                registros = registros
                    .Where(r => codigosValidos.Contains(r.Poblacion.Trim(), StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (registros.Count == 0)
                {
                    MessageBox.Show("Después de filtrar códigos inválidos no quedó ninguna fila para procesar.", "Sin filas válidas");
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
                                addressId: registro.Identificador,
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
                            if (src.Contains("cat") || src.Contains("catastro"))
                            {
                                entidad.ScoreText = "Aproximada por Catastro";
                                entidad.Source = "CATASTRO";
                            }
                            else if (src.Contains("esri"))
                            {
                                if (entidad.Score.HasValue)
                                    entidad.ScoreText = $"ESRI {Math.Round(entidad.Score.Value, 2)}";
                                else
                                    entidad.ScoreText = "ESRI";
                                entidad.Source = "ESRI";
                            }
                            else 
                            {
                                entidad.ScoreText = string.IsNullOrWhiteSpace(entidad.ScoreText) ? "Exacta" : entidad.ScoreText;
                                entidad.Source = "EAAB";
                            }

                            if (!string.IsNullOrWhiteSpace(entidad.FullAddressEAAB))
                                entidad.FullAddressOld = entidad.FullAddressEAAB; 
                            else if (!string.IsNullOrWhiteSpace(entidad.FullAddressCadastre))
                                entidad.FullAddressOld = entidad.FullAddressCadastre;
                            else if (string.IsNullOrWhiteSpace(entidad.FullAddressOld))
                                entidad.FullAddressOld = entidad.MainStreet ?? string.Empty;

                            ResultsLayerService.AddPointToMemory(entidad);
                            encontrados++;
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
