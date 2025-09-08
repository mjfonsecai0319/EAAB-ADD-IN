using Microsoft.Win32;
using System.Windows;
using EAABAddIn.Src.Presentation.ViewModel;

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
    }
}
