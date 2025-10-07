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

        public ObservableCollection<string> MotoresBD { get; set; } = new ObservableCollection<string> 
        { 
            "Oracle", 
            "PostgreSQL", 
            "Oracle SDE",
            "PostgreSQL SDE"
        };

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
                        if ((value == "PostgreSQL" || value == "PostgreSQL SDE") && string.IsNullOrWhiteSpace(Puerto))
                            Puerto = "5432";
                        else if ((value == "Oracle" || value == "Oracle SDE") && string.IsNullOrWhiteSpace(Puerto))
                            Puerto = "1521";
                    }

                    _previousMotor = value;
                    
                    OnPropertyChanged(nameof(MostrarCamposConexion));
                    OnPropertyChanged(nameof(MostrarArchivoCredenciales));
                    OnPropertyChanged(nameof(MostrarArchivoGdb));
                }
            }
        }

    public bool MostrarCamposConexion => MotorSeleccionado != "Oracle SDE" && MotorSeleccionado != "PostgreSQL SDE";
        
    public bool MostrarArchivoCredenciales => MotorSeleccionado == "Oracle SDE" || MotorSeleccionado == "PostgreSQL SDE";
        
    public bool MostrarArchivoGdb => MotorSeleccionado != "Oracle SDE" && MotorSeleccionado != "PostgreSQL SDE";

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

        private string _rutaArchivoGdb;
        public string RutaArchivoGdb
        {
            get => _rutaArchivoGdb;
            set => SetProperty(ref _rutaArchivoGdb, value);
        }

        private string _rutaArchivoCredenciales;
        public string RutaArchivoCredenciales
        {
            get => _rutaArchivoCredenciales;
            set => SetProperty(ref _rutaArchivoCredenciales, value);
        }

        // Permitir polígonos de 3 puntos (por defecto desactivado => se exige >=4)
        private bool _permitirTresPuntos;
        public bool PermitirTresPuntos
        {
            get => _permitirTresPuntos;
            set => SetProperty(ref _permitirTresPuntos, value);
        }

        public ICommand ProbarConexionCommand { get; }
        public ICommand GuardarYReconectarCommand { get; }
        public ICommand SeleccionarArchivoGdbCommand { get; }
        
        public ICommand SeleccionarArchivoCredencialesCommand { get; }

        public PropertyPage1ViewModel()
        {
            _settings = Module1.Settings;
            _validator = new ConnectionValidatorService();
            LoadSettings();
            ProbarConexionCommand = new RelayCommand(async () => await ProbarConexionAsync(), () => !_isConnecting);
            GuardarYReconectarCommand = new RelayCommand(async () => await GuardarYReconectarAsync(), () => !_isConnecting && IsValidConfiguration());
            SeleccionarArchivoGdbCommand = new RelayCommand(SeleccionarArchivoGdb);
            
            SeleccionarArchivoCredencialesCommand = new RelayCommand(SeleccionarArchivoCredenciales);
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
                    Puerto = MotorSeleccionado == "Oracle" || MotorSeleccionado == "Oracle SDE" ? "1521" : "5432";
                BaseDeDatos = _settings.baseDeDatos ?? string.Empty;
                RutaArchivoGdb = _settings.rutaArchivoGdb ?? string.Empty;
                
                RutaArchivoCredenciales = _settings.rutaArchivoCredenciales ?? string.Empty;

                PermitirTresPuntos = _settings.permitirTresPuntos; // false si no existía

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
            _settings.rutaArchivoGdb = RutaArchivoGdb;
            
            _settings.rutaArchivoCredenciales = RutaArchivoCredenciales;
            _settings.permitirTresPuntos = PermitirTresPuntos;

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
                Puerto = (MotorSeleccionado == "Oracle" || MotorSeleccionado == "Oracle SDE") ? "1521" : "5432";
                BaseDeDatos = string.Empty;
                OraclePath = string.Empty;
                RutaArchivoGdb = string.Empty;
                
                RutaArchivoCredenciales = string.Empty;

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
            else if (MotorSeleccionado == "Oracle SDE")
            {
                throw new NotSupportedException("Para archivos SDE, use GetSdeFilePath() en lugar de GetDatabaseConnectionProperties()");
            }
            else
                return ConnectionPropertiesFactory.CreatePostgresConnection(Host, Usuario, Contraseña, BaseDeDatos, Puerto);
        }

        public string GetSdeFilePath()
        {
            if (MotorSeleccionado == "Oracle SDE")
            {
                if (string.IsNullOrWhiteSpace(RutaArchivoCredenciales))
                {
                    throw new InvalidOperationException("No se ha seleccionado un archivo SDE.");
                }
                return ConnectionPropertiesFactory.CreateOracleConnectionFromFile(RutaArchivoCredenciales);
            }
            throw new InvalidOperationException("GetSdeFilePath solo es válido para Oracle con archivo de credenciales");
        }

        public async Task ProbarConexionAsync()
        {
            if (_isConnecting) return;
            _isConnecting = true;
            MensajeConexion = "🔄 Probando conexión...";
            try
            {
                if (MotorSeleccionado == "Oracle SDE")
                {
                    var sdeFilePath = GetSdeFilePath();
                    var result = await _validator.TestSdeConnectionAsync(sdeFilePath);
                    if (result.IsSuccess)
                    {
                        MensajeConexion = "✅ Conexión exitosa al archivo SDE";
                        IsConnected = true;
                    }
                    else
                    {
                        MensajeConexion = $"❌ Error: {result.Message}";
                        IsConnected = false;
                    }
                }
                else
                {
                    // Conexiones tradicionales
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
            if (MotorSeleccionado == "Oracle SDE" || MotorSeleccionado == "PostgreSQL SDE")
            {
                return !string.IsNullOrWhiteSpace(RutaArchivoCredenciales) &&
                       System.IO.File.Exists(RutaArchivoCredenciales);
            }
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

        private void SeleccionarArchivoGdb()
        {
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
                RutaArchivoGdb = item.Path;
            }
        }

        // Nuevo método para seleccionar archivo de conexión SDE
        private void SeleccionarArchivoCredenciales()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Seleccionar archivo de conexión SDE",
                Filter = "Archivos de conexión SDE (*.sde)|*.sde|Todos los archivos (*.*)|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                InitialDirectory = !string.IsNullOrWhiteSpace(RutaArchivoCredenciales)
                    ? System.IO.Path.GetDirectoryName(RutaArchivoCredenciales)
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                RutaArchivoCredenciales = openFileDialog.FileName;
            }
        }
    }
}