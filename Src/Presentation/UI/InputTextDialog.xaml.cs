using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Core.Entities;
using EAABAddIn.Src.Core;
using EAABAddIn.Src.Core.Data;
using EAABAddIn.Src.Domain.Repositories;

namespace EAABAddIn.Src.UI
{
    public partial class InputTextDialog : Window
    {
        public string InputText { get; private set; }
        public string SelectedCity { get; private set; }
        public string SelectedCityCode { get; private set; }

        public InputTextDialog()
        {
            InitializeComponent();
            this.Loaded += InputTextDialog_Loaded;
        }

        private async void InputTextDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCitiesAsync();
        }

        private async Task LoadCitiesAsync()
        {
            try
            {
                var engine = Module1.Settings.motor.ToDBEngine();

                // Construir props con la firma correcta (usa host, no server)
                var props = engine == DBEngine.Oracle
                    ? ConnectionPropertiesFactory.CreateOracleConnection(
                        host: Module1.Settings.host,
                        user: Module1.Settings.usuario,
                        password: Module1.Settings.contraseña,
                        database: Module1.Settings.baseDeDatos
                      )
                    : ConnectionPropertiesFactory.CreatePostgresConnection(
                        host: Module1.Settings.host,
                        user: Module1.Settings.usuario,
                        password: Module1.Settings.contraseña,
                        database: Module1.Settings.baseDeDatos
                      );

                List<PtAddressGralEntity> ciudades = null;

                await QueuedTask.Run(() =>
                {
                    IPtAddressGralEntityRepository repo = engine switch
                    {
                        DBEngine.Oracle => new PtAddressGralOracleRepository(),
                        DBEngine.PostgreSQL => new PtAddressGralPostgresRepository(),
                        _ => throw new NotSupportedException("Motor no soportado")
                    };

                    ciudades = repo.GetAllCities(props);
                });

                CityComboBox.ItemsSource = ciudades;
                CityComboBox.DisplayMemberPath = "CityDesc";
                CityComboBox.SelectedValuePath = "CityCode";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar ciudades: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputBox.Text;

            if (CityComboBox.SelectedItem is PtAddressGralEntity ciudad)
            {
                SelectedCity = ciudad.CityDesc;
                SelectedCityCode = ciudad.CityCode;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
