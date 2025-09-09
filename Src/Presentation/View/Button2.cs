using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using ExcelDataReader;
using EAABAddIn.Src.UI;
using EAABAddIn.Src.Core;
using EAABAddIn.Src.Core.Entities;
using EAABAddIn.Src.Core.Map;
using EAABAddIn.Src.Domain.Repositories;

namespace EAABAddIn.Src.Presentation.View
{
    internal class Button2 : Button
    {
        protected override async void OnClick()
        {
            var dialog = new FileUploadDialog();
            bool? result = dialog.ShowDialog();

            if (result != true)
                return;

            string filePath = dialog.ViewModel.FilePath;

            // 🔹 Validar extensión
            if (!File.Exists(filePath) ||
                !(filePath.EndsWith(".xlsx") || filePath.EndsWith(".xls")))
            {
                MessageBox.Show("❌ Solo se permiten archivos Excel (.xlsx o .xls)", "Error");
                return;
            }

            List<RegistroDireccion> registros;
            try
            {
                registros = LeerDireccionesExcel(filePath);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error al leer el archivo Excel: {ex.Message}", "Error");
                return;
            }

            if (registros.Count == 0)
            {
                MessageBox.Show("⚠️ No se encontraron direcciones en el archivo.", "Información");
                return;
            }

            // 🔹 Determinar motor
            var engine = Module1.Settings.motor.ToDBEngine();
            IPtAddressGralEntityRepository repo = engine switch
            {
                DBEngine.Oracle => new PtAddressGralOracleRepository(),
                DBEngine.PostgreSQL => new PtAddressGralPostgresRepository(),
                _ => null
            };

            if (repo == null)
            {
                MessageBox.Show("❌ Motor de base de datos no soportado.", "Error");
                return;
            }

            int encontrados = 0, noEncontrados = 0;

            await QueuedTask.Run(async () =>
            {
                foreach (var registro in registros)
                {
                    try
                    {
                        var resultados = repo.FindByCityCodeAndAddresses(null, registro.Poblacion, registro.Direccion);

                        if (resultados.Count > 0)
                        {
                            var entidad = resultados[0];

                            if (entidad.Latitud.HasValue && entidad.Longitud.HasValue)
                            {
                                await ResultsLayerService.AddPointAsync(
                                    (decimal)entidad.Latitud.Value,
                                    (decimal)entidad.Longitud.Value
                                );
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
                $"✅ Se marcaron {encontrados} direcciones.\n⚠️ No se encontraron {noEncontrados}.",
                "Resultado geocodificación"
            );
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
    }

    public class RegistroDireccion
    {
        public string Identificador { get; set; }
        public string Direccion { get; set; }
        public string Poblacion { get; set; }
    }
}
