using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Win32; 
using System.Windows.Input;

namespace EAABAddIn
{
    internal class PropertyPage1ViewModel : Page
    {
        private readonly Settings _settings;

        public ObservableCollection<string> MotoresBD { get; set; } =
            new ObservableCollection<string> { "Oracle", "PostgreSQL" };

        private string _motorSeleccionado = string.Empty;
        public string MotorSeleccionado
        {
            get => _motorSeleccionado;
            set
            {
                if (SetProperty(ref _motorSeleccionado, value))
                {
                    IsModified = true;

                    if (value == "PostgreSQL")
                    {
                        if (string.IsNullOrWhiteSpace(Host))   Host = "localhost";
                        if (string.IsNullOrWhiteSpace(Puerto)) Puerto = "5432";
                    }
                    else if (value == "Oracle")
                    {
                    }
                }
            }
        }

        private string _oraclePath;
        public string OraclePath
        {
            get => _oraclePath;
            set { if (SetProperty(ref _oraclePath, value)) IsModified = true; }
        }
        public ICommand BrowseOracleCommand { get; 

}
        private string _usuario = string.Empty;
        public string Usuario
        {
            get => _usuario;
            set { if (SetProperty(ref _usuario, value)) IsModified = true; }
        }

        private string _contraseña = string.Empty;
        public string Contraseña
        {
            get => _contraseña;
            set { if (SetProperty(ref _contraseña, value)) IsModified = true; }
        }

        private string _host = string.Empty;
        public string Host
        {
            get => _host;
            set { if (SetProperty(ref _host, value)) IsModified = true; }
        }

        private string _puerto = string.Empty;
        public string Puerto
        {
            get => _puerto;
            set { if (SetProperty(ref _puerto, value)) IsModified = true; }
        }
        private string _baseDeDatos = string.Empty;
        public string BaseDeDatos
        {
            get => _baseDeDatos;
            set { if (SetProperty(ref _baseDeDatos, value)) IsModified = true; }
        }


        public PropertyPage1ViewModel()
        {
            _settings = Module1.Settings;

            // Inicializamos el comando
            BrowseOracleCommand = new RelayCommand(OnBrowseOracleExecute);

            LoadSettings();
        }
        private void OnBrowseOracleExecute()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Seleccionar Oracle Client Path",
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                OraclePath = openFileDialog.FileName;
            }
        }

        protected override Task CommitAsync()
        {
            // Guardar valores en settings
            _settings.motor = MotorSeleccionado;
            _settings.usuario = Usuario;
            _settings.contraseña = Contraseña;
            _settings.host = Host;
            _settings.puerto = Puerto;
            _settings.oracle_path = OraclePath;
            _settings.baseDeDatos = BaseDeDatos;

            _settings.Save();

            System.Diagnostics.Debug.WriteLine(
                $"Saving settings: motor={MotorSeleccionado}, usuario={Usuario}, host={Host}, puerto={Puerto}"
            );

            return Task.FromResult(0);
        }

        protected override Task InitializeAsync()
        {
            LoadSettings();
            return Task.FromResult(true);
        }

        protected override void Uninitialize()
        {
        }

        private void LoadSettings()
        {
            MotorSeleccionado = _settings.motor ?? "PostgreSQL"; // default
            Usuario = _settings.usuario ?? string.Empty;
            Contraseña = _settings.contraseña ?? string.Empty;
            Host = _settings.host ?? "localhost";
            Puerto = _settings.puerto ?? "5432";
            OraclePath = _settings.oracle_path ?? string.Empty;
            BaseDeDatos = _settings.baseDeDatos ?? string.Empty;



            System.Diagnostics.Debug.WriteLine(
                $"Loaded settings: motor={MotorSeleccionado}, usuario={Usuario}, host={Host}, puerto={Puerto}"
            );
        }
    }
}

    internal class PropertyPage1_ShowButton : Button
    {
        protected override void OnClick()
        {
            object[] data = ["Page UI content"];

            if (PropertySheet.IsVisible) return;

            PropertySheet.ShowDialog("EAABAddIn_PropertySheet1", "EAABAddIn_PropertyPage1", data);
        }
    }
