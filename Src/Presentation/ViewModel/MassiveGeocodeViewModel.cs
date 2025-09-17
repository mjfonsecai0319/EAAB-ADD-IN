using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Core;
using EAABAddIn.Src.Core.Entities;
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

        public ICommand OpenFileDialogCommand { get; private set; }
        public ICommand SearchCommand { get; private set; }

        public MassiveGeocodeViewModel()
        {
            FileInput = string.Empty;
            OpenFileDialogCommand = new RelayCommand(Browse_Click);
            SearchCommand = new AsyncRelayCommand(Search_Click);
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
            IPtAddressGralEntityRepository repo = engine switch
            {
                DBEngine.Oracle => new PtAddressGralOracleRepository(),
                DBEngine.PostgreSQL => new PtAddressGralPostgresRepository(),
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

            await QueuedTask.Run(async () =>
            {
                var ciudades = repo.GetAllCities(null);
                var ciudadesDict = ciudades.ToDictionary(c => c.CityCode, c => c.CityDesc);

                foreach (var registro in registros)
                {
                    contador++;
                    StatusMessage = $"Procesando {contador}/{total}...";

                    try
                    {
                        var resultados = repo.FindByCityCodeAndAddresses(null, registro.Poblacion, registro.Direccion);

                        if (resultados.Count > 0)
                        {
                            var entidad = resultados[0];
                            if (entidad.Latitud.HasValue && entidad.Longitud.HasValue)
                            {
                                entidad.FullAddressOld = registro.Direccion;
                                entidad.CityDesc = ciudadesDict.TryGetValue(registro.Poblacion, out var nombreCiudad)
                                    ? nombreCiudad
                                    : registro.Poblacion;

                                await ResultsLayerService.AddPointAsync(entidad);
                                encontrados++;
                            }
                            else
                            {
                                noEncontrados++;
                            }
                        }
                        else
                        {
                            noEncontrados++;
                        }
                    }
                    catch
                    {
                        noEncontrados++;
                    }
                }
            });

            MessageBox.Show(
                $"Se marcaron {encontrados} direcciones.\nNo se encontraron {noEncontrados}.",
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
                        Poblacion = reader.GetValue(2)?.ToString() // aquí viene el código de ciudad
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

        public class RegistroDireccion
        {
            public string Identificador { get; set; }
            public string Direccion { get; set; }
            public string Poblacion { get; set; } 
        }
    }
}
