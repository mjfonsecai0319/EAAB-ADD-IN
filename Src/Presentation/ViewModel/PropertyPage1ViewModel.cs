﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using EAABAddIn.Src.Core.Data;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Contracts;
using System;
using EAABAddIn.Src.Presentation.Base;


namespace EAABAddIn.Src.Presentation.ViewModel
{
    public class PropertyPage1ViewModel : Page, INotifyPropertyChanged
    {
        private readonly Settings _settings;
        private readonly ConnectionValidatorService _validator;

        public new event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected new bool SetProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);

            // 🔹 Guardar automáticamente en settings cuando cambie una propiedad
            SaveSettings();
            return true;
        }

        public ObservableCollection<string> MotoresBD { get; set; } = new ObservableCollection<string> { "Oracle", "PostgreSQL" };

        private string _motorSeleccionado;
        public string MotorSeleccionado
        {
            get => _motorSeleccionado;
            set
            {
                if (SetProperty(ref _motorSeleccionado, value))
                {
                    if (value == "PostgreSQL" && string.IsNullOrWhiteSpace(Puerto)) Puerto = "5432";
                    else if (value == "Oracle" && string.IsNullOrWhiteSpace(Puerto)) Puerto = "1521";
                }
            }
        }

        private string _usuario;
        public string Usuario { get => _usuario; set => SetProperty(ref _usuario, value); }

        private string _contraseña;
        public string Contraseña { get => _contraseña; set => SetProperty(ref _contraseña, value); }

        private string _host;
        public string Host { get => _host; set => SetProperty(ref _host, value); }

        private string _puerto;
        public string Puerto { get => _puerto; set => SetProperty(ref _puerto, value); }

        private string _baseDeDatos;
        public string BaseDeDatos { get => _baseDeDatos; set => SetProperty(ref _baseDeDatos, value); }

        private string _oraclePath;
        public string OraclePath { get => _oraclePath; set => SetProperty(ref _oraclePath, value); }

        private string _mensajeConexion;
        public string MensajeConexion
        {
            get => _mensajeConexion;
            set => SetProperty(ref _mensajeConexion, value);
        }

        public ICommand ProbarConexionCommand { get; }

        public PropertyPage1ViewModel()
        {
            _settings = Module1.Settings;
            _validator = new ConnectionValidatorService();
            LoadSettings();

            ProbarConexionCommand = new RelayCommand(async () => await ProbarConexionAsync());
        }

        private void LoadSettings()
        {
            MotorSeleccionado = _settings.motor ?? "PostgreSQL";
            Usuario = _settings.usuario ?? string.Empty;
            Contraseña = _settings.contraseña ?? string.Empty;
            Host = _settings.host ?? "localhost";
            Puerto = _settings.puerto ?? (MotorSeleccionado == "Oracle" ? "1521" : "5432");
            OraclePath = _settings.oracle_path ?? string.Empty;
            BaseDeDatos = _settings.baseDeDatos ?? string.Empty;
        }

        private void SaveSettings()
        {
            _settings.motor = MotorSeleccionado;
            _settings.usuario = Usuario;
            _settings.contraseña = Contraseña;
            _settings.host = Host;
            _settings.puerto = Puerto;
            _settings.oracle_path = OraclePath;
            _settings.baseDeDatos = BaseDeDatos;
            _settings.Save();
        }

        public DatabaseConnectionProperties GetDatabaseConnectionProperties()
        {
            if (MotorSeleccionado == "Oracle")
                return ConnectionPropertiesFactory.CreateOracleConnection(Host, Usuario, Contraseña, BaseDeDatos, Puerto);
            else
                return ConnectionPropertiesFactory.CreatePostgresConnection(Host, Usuario, Contraseña, BaseDeDatos, Puerto);
        }

        public async Task ProbarConexionAsync()
        {
            var connectionProps = GetDatabaseConnectionProperties();
            var result = await _validator.TestConnectionAsync(connectionProps, MotorSeleccionado);

            MensajeConexion = result.IsSuccess
                ? "✅ Conexión exitosa"
                : $"❌ Error: {result.Message}";
        }
    }
}
