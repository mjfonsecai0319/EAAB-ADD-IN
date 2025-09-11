using System.Collections.Generic;
using System.IO;
using System.Windows;

using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Core;
using EAABAddIn.Src.Core.Entities;
using EAABAddIn.Src.Core.Map;
using EAABAddIn.Src.Domain.Repositories;
using EAABAddIn.Src.Presentation.ViewModel;
using EAABAddIn.Src.UI;

using ExcelDataReader;

using Microsoft.Win32;

namespace EAABAddIn.Src.UI
{
    public partial class FileUploadDialog : Window
    {
        public FileUploadDialogViewModel ViewModel { get; private set; }

        public FileUploadDialog()
        {
            InitializeComponent();
            ViewModel = new FileUploadDialogViewModel();
            DataContext = ViewModel;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                ViewModel.FilePath = dialog.FileName;
            }
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ViewModel.FilePath))
            {
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Debe seleccionar un archivo.", "Advertencia");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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
