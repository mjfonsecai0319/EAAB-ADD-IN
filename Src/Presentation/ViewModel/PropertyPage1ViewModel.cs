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

        // 🔹 Nueva propiedad para mostrar estado de conexión
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
            LoadSettings();

            ProbarConexionCommand = new RelayCommand(async () => await ProbarConexionAsync(), () => !_isConnecting);
            GuardarYReconectarCommand = new RelayCommand(async () => await GuardarYReconectarAsync(), () => !_isConnecting && IsValidConfiguration());
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

            // Verificar si hay una configuración válida guardada
            CheckConnectionStatus();
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
                var result = await _validator.TestConnectionAsync(connectionProps, MotorSeleccionado);

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

        /// <summary>
        /// Guarda la configuración y establece la conexión en el módulo principal
        /// </summary>
        public async Task GuardarYReconectarAsync()
        {
            if (_isConnecting) return;

            _isConnecting = true;
            MensajeConexion = "🔄 Guardando configuración y conectando...";

            try
            {
                // Primero guardar la configuración
                SaveSettings();

                // Luego reconectar el módulo principal
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

        /// <summary>
        /// Verifica si la configuración actual es válida
        /// </summary>
        private bool IsValidConfiguration()
        {
            return !string.IsNullOrWhiteSpace(MotorSeleccionado) &&
                   !string.IsNullOrWhiteSpace(Host) &&
                   !string.IsNullOrWhiteSpace(Usuario) &&
                   !string.IsNullOrWhiteSpace(Contraseña) &&
                   !string.IsNullOrWhiteSpace(BaseDeDatos);
        }

        /// <summary>
        /// Verifica el estado de la conexión actual
        /// </summary>
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
            catch
            {
                IsConnected = false;
                MensajeConexion = "❌ Error al verificar el estado de conexión";
            }
        }
    }
}