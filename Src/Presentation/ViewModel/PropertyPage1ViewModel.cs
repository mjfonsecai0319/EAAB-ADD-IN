﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;

using EAABAddIn.Src.Core.Data;
using EAABAddIn.Src.Presentation.Base;

using Microsoft.Win32;

namespace EAABAddIn.Src.Presentation.ViewModel
{
    public class PropertyPage1ViewModel : Page, INotifyPropertyChanged
    {
        private readonly Settings _settings;
        private readonly ConnectionValidatorService _validator;
        private bool _isConnecting = false;
        private bool _isLoading = false;
        private string _previousMotor;

        public new event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected new bool SetProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            if (!_isLoading)
            {
                SaveSettings();
            }
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
                    if (!_isLoading && _previousMotor != null && _previousMotor != value)
                    {
                        Debug.WriteLine($"Motor cambió de {_previousMotor} a {value} - Limpiando campos");
                        ClearFieldsOnMotorChange();
                    }

                    if (!_isLoading)
                    {
                        if (value == "PostgreSQL" && string.IsNullOrWhiteSpace(Puerto)) Puerto = "5432";
                        else if (value == "Oracle" && string.IsNullOrWhiteSpace(Puerto)) Puerto = "1521";
                    }

                    _previousMotor = value;
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

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        // ✅ Nueva propiedad
        private string _rutaArchivoGdb;
        public string RutaArchivoGdb
        {
            get => _rutaArchivoGdb;
            set => SetProperty(ref _rutaArchivoGdb, value);
        }

        public ICommand ProbarConexionCommand { get; }
        public ICommand GuardarYReconectarCommand { get; }

        // ✅ Nuevo comando
        public ICommand SeleccionarArchivoGdbCommand { get; }

        public PropertyPage1ViewModel()
        {
            _settings = Module1.Settings;
            _validator = new ConnectionValidatorService();
            LoadSettings();
            ProbarConexionCommand = new RelayCommand(async () => await ProbarConexionAsync(), () => !_isConnecting);
            GuardarYReconectarCommand = new RelayCommand(async () => await GuardarYReconectarAsync(), () => !_isConnecting && IsValidConfiguration());

            // ✅ Inicializar el nuevo comando
            SeleccionarArchivoGdbCommand = new RelayCommand(SeleccionarArchivoGdb);
        }

        private void LoadSettings()
        {
            _isLoading = true;
            try
            {
                MotorSeleccionado = _settings.motor ?? "PostgreSQL";
                Usuario = _settings.usuario ?? string.Empty;
                Contraseña = _settings.contraseña ?? string.Empty;
                Host = _settings.host ?? "localhost";
                if (!string.IsNullOrEmpty(_settings.puerto))
                    Puerto = _settings.puerto;
                else
                    Puerto = MotorSeleccionado == "Oracle" ? "1521" : "5432";
                BaseDeDatos = _settings.baseDeDatos ?? string.Empty;

                // ✅ Cargar la ruta desde settings
                RutaArchivoGdb = _settings.rutaArchivoGdb ?? string.Empty;

                Debug.WriteLine($"📥 Configuración cargada - Motor: {MotorSeleccionado}, Host: {Host}, Usuario: {Usuario}, DB: {BaseDeDatos}");
                _previousMotor = MotorSeleccionado;
                CheckConnectionStatus();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void SaveSettings()
        {
            if (_isLoading) return;
            _settings.motor = MotorSeleccionado;
            _settings.usuario = Usuario;
            _settings.contraseña = Contraseña;
            _settings.host = Host;
            _settings.puerto = Puerto;
            _settings.baseDeDatos = BaseDeDatos;

            // ✅ Guardar la ruta en settings
            _settings.rutaArchivoGdb = RutaArchivoGdb;

            _settings.Save();
            Debug.WriteLine("💾 Configuración guardada automáticamente");
        }

        private void ClearFieldsOnMotorChange()
        {
            _isLoading = true;
            try
            {
                Usuario = string.Empty;
                Contraseña = string.Empty;
                Host = string.Empty;
                Puerto = MotorSeleccionado == "Oracle" ? "1521" : "5432";
                BaseDeDatos = string.Empty;
                OraclePath = string.Empty;

                // ✅ Limpiar la ruta también
                RutaArchivoGdb = string.Empty;

                MensajeConexion = "Motor cambiado. Configure los nuevos parámetros de conexión.";
                IsConnected = false;
            }
            finally
            {
                _isLoading = false;
            }
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
            if (_isConnecting) return;
            _isConnecting = true;
            MensajeConexion = "🔄 Probando conexión...";
            try
            {
                var connectionProps = GetDatabaseConnectionProperties();
                var result = await _validator.TestConnectionInstanceAsync(connectionProps, MotorSeleccionado);
                if (result.IsSuccess)
                {
                    MensajeConexion = "✅ Conexión exitosa";
                    IsConnected = true;
                }
                else
                {
                    MensajeConexion = $"❌ Error: {result.Message}";
                    IsConnected = false;
                }
            }
            catch (Exception ex)
            {
                MensajeConexion = $"❌ Error inesperado: {ex.Message}";
                IsConnected = false;
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public async Task GuardarYReconectarAsync()
        {
            if (_isConnecting) return;
            _isConnecting = true;
            MensajeConexion = "🔄 Guardando configuración y conectando...";
            try
            {
                SaveSettings();
                await Module1.ReconnectDatabaseAsync();
                MensajeConexion = "✅ Configuración guardada y conexión establecida";
                IsConnected = true;
            }
            catch (Exception ex)
            {
                MensajeConexion = $"❌ Error al conectar: {ex.Message}";
                IsConnected = false;
            }
            finally
            {
                _isConnecting = false;
            }
        }

        private bool IsValidConfiguration()
        {
            return !string.IsNullOrWhiteSpace(MotorSeleccionado) &&
                   !string.IsNullOrWhiteSpace(Host) &&
                   !string.IsNullOrWhiteSpace(Usuario) &&
                   !string.IsNullOrWhiteSpace(Contraseña) &&
                   !string.IsNullOrWhiteSpace(BaseDeDatos);
        }

        private void CheckConnectionStatus()
        {
            try
            {
                var dbService = Module1.DatabaseConnection;
                if (dbService?.Geodatabase != null)
                {
                    IsConnected = true;
                    MensajeConexion = "✅ Conexión activa";
                }
                else
                {
                    IsConnected = false;
                    if (IsValidConfiguration())
                    {
                        MensajeConexion = "⚠️ Configuración válida pero no conectado. Haga clic en 'Guardar y Conectar'";
                    }
                    else
                    {
                        MensajeConexion = "❌ Configure los parámetros de conexión";
                    }
                }
            }
            catch (Exception ex)
            {
                IsConnected = false;
                MensajeConexion = "❌ Error al verificar el estado de conexión";
                Debug.WriteLine($"Error en CheckConnectionStatus: {ex.Message}");
            }
        }

        public void RefreshFromSettings()
        {
            LoadSettings();
        }

        // ✅ Nuevo método
        private void SeleccionarArchivoGdb()
        {
            // Usar el explorador de ArcGIS Pro: una File Geodatabase es una carpeta .gdb
            var filter = new BrowseProjectFilter("esri_browseDialogFilters_geodatabases");

            var dlg = new OpenItemDialog
            {
                Title = "Seleccionar Geodatabase",
                BrowseFilter = filter,
                MultiSelect = false,
                InitialLocation = !string.IsNullOrWhiteSpace(RutaArchivoGdb)
                    ? System.IO.Path.GetDirectoryName(RutaArchivoGdb)
                    : Project.Current?.HomeFolderPath
            };

            var ok = dlg.ShowDialog();
            if (ok == true && dlg.Items != null && dlg.Items.Any())
            {
                var item = dlg.Items.First();
                // item.Path devolverá la ruta a la carpeta .gdb seleccionada
                RutaArchivoGdb = item.Path;
            }
        }
    }
}
