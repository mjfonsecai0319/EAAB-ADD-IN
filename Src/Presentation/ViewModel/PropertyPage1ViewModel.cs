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
using System.Diagnostics;

namespace EAABAddIn.Src.Presentation.ViewModel
{
    public class PropertyPage1ViewModel : Page, INotifyPropertyChanged
    {
        private readonly Settings _settings;
        private readonly ConnectionValidatorService _validator;
        private bool _isConnecting = false;
        private bool _isLoading = false; // ✅ Flag para evitar guardado durante carga

        public new event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected new bool SetProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);

            // ✅ Solo guardar si no estamos cargando los valores iniciales
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
                    // ✅ Solo cambiar puerto si no estamos cargando y el puerto está vacío
                    if (!_isLoading)
                    {
                        if (value == "PostgreSQL" && string.IsNullOrWhiteSpace(Puerto)) Puerto = "5432";
                        else if (value == "Oracle" && string.IsNullOrWhiteSpace(Puerto)) Puerto = "1521";
                    }
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

        public ICommand ProbarConexionCommand { get; }
        public ICommand GuardarYReconectarCommand { get; }

        public PropertyPage1ViewModel()
        {
            _settings = Module1.Settings;
            _validator = new ConnectionValidatorService();
            
            // ✅ Cargar configuración antes de crear los comandos
            LoadSettings();

            ProbarConexionCommand = new RelayCommand(async () => await ProbarConexionAsync(), () => !_isConnecting);
            GuardarYReconectarCommand = new RelayCommand(async () => await GuardarYReconectarAsync(), () => !_isConnecting && IsValidConfiguration());
        }

        private void LoadSettings()
        {
            _isLoading = true; // ✅ Marcar que estamos cargando

            try
            {
                // ✅ SIEMPRE cargar los valores guardados, sin importar el estado de conexión
                MotorSeleccionado = _settings.motor ?? "PostgreSQL";
                Usuario = _settings.usuario ?? string.Empty;
                Contraseña = _settings.contraseña ?? string.Empty;
                Host = _settings.host ?? "localhost";
                
                // ✅ Cargar puerto guardado, o usar default según motor
                if (!string.IsNullOrEmpty(_settings.puerto))
                {
                    Puerto = _settings.puerto;
                }
                else
                {
                    Puerto = MotorSeleccionado == "Oracle" ? "1521" : "5432";
                }
                
                OraclePath = _settings.oracle_path ?? string.Empty;
                BaseDeDatos = _settings.baseDeDatos ?? string.Empty;

                Debug.WriteLine($"📥 Configuración cargada - Motor: {MotorSeleccionado}, Host: {Host}, Usuario: {Usuario}, DB: {BaseDeDatos}");

                // ✅ Verificar estado de conexión después de cargar
                CheckConnectionStatus();
            }
            finally
            {
                _isLoading = false; // ✅ Terminar modo de carga
            }
        }

        private void SaveSettings()
        {
            if (_isLoading) return; // ✅ No guardar durante la carga inicial

            _settings.motor = MotorSeleccionado;
            _settings.usuario = Usuario;
            _settings.contraseña = Contraseña;
            _settings.host = Host;
            _settings.puerto = Puerto;
            _settings.oracle_path = OraclePath;
            _settings.baseDeDatos = BaseDeDatos;
            _settings.Save();

            Debug.WriteLine("💾 Configuración guardada automáticamente");
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
                // ✅ Usar método de instancia en lugar de static
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
                
                // ✅ Remover RaiseCanExecuteChanged - no es necesario con CommandManager
                // Los comandos se actualizan automáticamente
            }
        }

        public async Task GuardarYReconectarAsync()
        {
            if (_isConnecting) return;

            _isConnecting = true;
            MensajeConexion = "🔄 Guardando configuración y conectando...";

            try
            {
                // ✅ Forzar guardado de configuración actual
                SaveSettings();

                // ✅ Usar el nombre correcto del método (ReconnectDatabaseAsync)
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
                // ✅ Verificar si el servicio existe y tiene geodatabase
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

        // ✅ Método para refrescar manualmente los valores desde configuración
        public void RefreshFromSettings()
        {
            LoadSettings();
        }
    }
}