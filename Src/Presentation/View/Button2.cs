using ArcGIS.Desktop.Framework.Contracts;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using ExcelDataReader;
using EAABAddIn.Src.UI;

namespace EAABAddIn.Src.Presentation.View
{
    internal class Button2 : Button
    {
        protected override void OnClick()
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

            try
            {
                var registros = LeerDireccionesExcel(filePath);

                if (registros.Count == 0)
                {
                    MessageBox.Show("⚠️ No se encontraron direcciones en el archivo.", "Información");
                    return;
                }

                // ✅ Mostrar un ejemplo
                MessageBox.Show(
                    $"Se leyeron {registros.Count} direcciones.\n" +
                    $"Ejemplo:\n{registros[0].Identificador} - {registros[0].Direccion} - {registros[0].Poblacion}",
                    "Lectura Excel"
                );

                // 🔹 Aquí después haces la lógica de marcar puntos usando repo + ResultsLayerService
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error al leer el archivo Excel: {ex.Message}", "Error");
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

                    if (!string.IsNullOrWhiteSpace(registro.Direccion))
                        lista.Add(registro);
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
