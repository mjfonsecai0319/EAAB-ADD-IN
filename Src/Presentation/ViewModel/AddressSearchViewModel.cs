using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Diagnostics;

using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Application.Errors;
using EAABAddIn.Src.Application.Models;
using EAABAddIn.Src.Application.UseCases;
using EAABAddIn.Src.Core;
using EAABAddIn.Src.Core.Data;
using EAABAddIn.Src.Core.Entities;
using EAABAddIn.Src.Core.Map;
using EAABAddIn.Src.Domain.Repositories;
using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel
{
    internal class AddressSearchViewModel : BusyViewModelBase
    {
        public override string DisplayName => "Buscar Dirección";
        public override string Tooltip => "Buscar una dirección específica en el mapa";

        public ObservableCollection<PtAddressGralEntity> Cities { get; }

        private PtAddressGralEntity _selectedCity;
        public PtAddressGralEntity SelectedCity
        {
            get => _selectedCity;
            set
            {
                if (_selectedCity != value)
                {
                    _selectedCity = value;
                    NotifyPropertyChanged(nameof(SelectedCity));
                    Debug.WriteLine($"Ciudad seleccionada: {value?.CityDesc}");
                }
            }
        }

        private string _addressInput;
        public string AddressInput
        {
            get => _addressInput;
            set
            {
                if (_addressInput != value)
                {
                    _addressInput = value;
                    NotifyPropertyChanged(nameof(AddressInput));
                }
            }
        }

        private string _connectionStatus;
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    NotifyPropertyChanged(nameof(ConnectionStatus));
                }
            }
        }

        public ICommand SearchCommand { get; }
        public ICommand RefreshCitiesCommand { get; }

        public AddressSearchViewModel()
        {
            Debug.WriteLine("=== Inicializando AddressSearchViewModel ===");
            Cities = new ObservableCollection<PtAddressGralEntity>();
            SelectedCity = null;
            SearchCommand = new AsyncRelayCommand(OnSearchAsync);
            RefreshCitiesCommand = new AsyncRelayCommand(LoadCitiesAsync);
            

            CheckInitialConnectionStatus();
            
    
            if (IsConnectionReady())
            {
                _ = LoadCitiesAsync();
            }
            else
            {
                ConnectionStatus = " Configure la conexión primero";
                StatusMessage = "Configure la conexión a la base de datos en las opciones";
            }
        }

        private void CheckInitialConnectionStatus()
        {
            try
            {
                var settings = Module1.Settings;
                var dbService = Module1.DatabaseConnection;
                
                Debug.WriteLine($"Settings motor: {settings?.motor}");
                Debug.WriteLine($"Settings host: {settings?.host}");
                Debug.WriteLine($"Settings usuario: {settings?.usuario}");
                Debug.WriteLine($"Settings baseDeDatos: {settings?.baseDeDatos}");
                Debug.WriteLine($"DatabaseConnection service: {(dbService != null ? "EXISTS" : "NULL")}");
                Debug.WriteLine($"Geodatabase: {(dbService?.Geodatabase != null ? "CONNECTED" : "NOT CONNECTED")}");

                if (dbService?.Geodatabase == null)
                {
                    ConnectionStatus = "Sin conexión a BD";
                }
                else
                {
                    ConnectionStatus = "Conectado a BD";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking connection status: {ex.Message}");
                ConnectionStatus = "Error de conexión";
            }
        }

        private async Task LoadCitiesAsync()
        {
            Debug.WriteLine("=== Iniciando carga de ciudades ===");
            IsBusy = true;
            StatusMessage = "Cargando ciudades...";

            try
            {
                var settings = Module1.Settings;
                if (string.IsNullOrWhiteSpace(settings?.motor) || 
                    string.IsNullOrWhiteSpace(settings?.host) ||
                    string.IsNullOrWhiteSpace(settings?.usuario))
                {
                    Debug.WriteLine("Configuración incompleta");
                    ConnectionStatus = "Configuración incompleta";
                    StatusMessage = "Configure la conexión en las opciones";
                    return;
                }

                var dbService = Module1.DatabaseConnection;
                if (dbService?.Geodatabase == null)
                {
                    Debug.WriteLine("No hay conexión a la base de datos");
                    ConnectionStatus = "Sin conexión a BD";
                    StatusMessage = "No hay conexión a la base de datos";
                    
                    Debug.WriteLine("Intentando reconectar...");
                    try
                    {
                        await Module1.ReconnectDatabaseAsync();
                        Debug.WriteLine("Reconexión exitosa");
                        ConnectionStatus = "Reconectado";
                    }
                    catch (Exception reconnectEx)
                    {
                        Debug.WriteLine($"Fallo al reconectar: {reconnectEx.Message}");
                        ConnectionStatus = "Fallo al reconectar";
                        StatusMessage = "Configure la conexión en las opciones";
                        return;
                    }
                }

                var engine = settings.motor.ToDBEngine();
                var props = GetDatabaseConnectionProperties();
                
                Debug.WriteLine($"Motor de BD: {engine}");
                Debug.WriteLine($"Propiedades de conexión creadas");
                
                List<PtAddressGralEntity> ciudades = null;

                await QueuedTask.Run(() =>
                {
                    try
                    {
                        Debug.WriteLine("Creando repositorio...");
                        IPtAddressGralEntityRepository repo = engine switch
                        {
                            DBEngine.Oracle => new PtAddressGralOracleRepository(),
                            DBEngine.PostgreSQL => new PtAddressGralPostgresRepository(),
                            _ => throw new NotSupportedException($"Motor {engine} no soportado")
                        };

                        Debug.WriteLine("Obteniendo ciudades del repositorio...");
                        ciudades = repo.GetAllCities(props);
                        Debug.WriteLine($"Ciudades obtenidas: {ciudades?.Count ?? 0}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($" Error en QueuedTask: {ex.Message}");
                        Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                        throw;
                    }
                });

                Cities.Clear();
                if (ciudades != null && ciudades.Any())
                {
                    foreach (var ciudad in ciudades)
                    {
                        Cities.Add(ciudad);
                        Debug.WriteLine($"Ciudad agregada: {ciudad.CityDesc} ({ciudad.CityCode})");
                    }
                    
                    SelectedCity = ciudades.FirstOrDefault();
                    ConnectionStatus = $" {ciudades.Count} ciudades cargadas";
                    StatusMessage = string.Empty;
                    Debug.WriteLine($"Se cargaron {ciudades.Count} ciudades exitosamente");
                }
                else
                {
                    Debug.WriteLine("No se encontraron ciudades");
                    ConnectionStatus = "Sin ciudades disponibles";
                    StatusMessage = "No se encontraron ciudades en la base de datos";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al cargar ciudades: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                ConnectionStatus = "Error al cargar ciudades";
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                if (string.IsNullOrEmpty(StatusMessage))
                    StatusMessage = string.Empty;
            }
        }

        private async Task OnSearchAsync()
        {
            IsBusy = true;
            StatusMessage = "Buscando dirección...";

            try
            {
                if (string.IsNullOrWhiteSpace(AddressInput))
                {
                    StatusMessage = "Ingrese una dirección para buscar";
                    return;
                }
                if (SelectedCity is null)
                {
                    StatusMessage = "Seleccione una ciudad";
                    return;
                }

                var dbService = Module1.DatabaseConnection;
                if (dbService?.Geodatabase == null)
                {
                    StatusMessage = "Sin conexión a BD - Configure en opciones";
                    return;
                }

                await QueuedTask.Run(() =>
                {
                    var engine = Module1.Settings.motor.ToDBEngine();
                    
                    if (engine == DBEngine.Oracle)
                        HandleOracleConnection(AddressInput, SelectedCity.CityCode, SelectedCity.CityDesc);
                    else if (engine == DBEngine.PostgreSQL)
                        HandlePostgreSqlConnection(AddressInput, SelectedCity.CityCode, SelectedCity.CityDesc);
                    else
                        StatusMessage = "Motor de BD no configurado";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error durante la búsqueda: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void HandleOracleConnection(string input, string cityCode, string cityDesc)
        {
            try
            {
                var props = GetDatabaseConnectionProperties();
                
                var addressNormalizer = new AddressNormalizer(DBEngine.Oracle, props);
                var addressSearch = new AddressSearchUseCase(DBEngine.Oracle, props);

                var model = new AddressNormalizerModel { Address = input };
                var address = addressNormalizer.Invoke(model);

                var searchAddress = !string.IsNullOrWhiteSpace(address.AddressEAAB)
                    ? address.AddressEAAB
                    : (!string.IsNullOrWhiteSpace(address.AddressNormalizer)
                        ? address.AddressNormalizer
                        : input);

                var result = addressSearch.Invoke(searchAddress, cityCode, cityDesc);

                if (result == null || result.Count == 0)
                {
                    StatusMessage = $"Sin coincidencias en {SelectedCity.CityDesc}";
                    return;
                }

                foreach (var addr in result)
                {
                    if (addr.Latitud.HasValue && addr.Longitud.HasValue)
                    {
                        addr.CityDesc = SelectedCity.CityDesc;
                        addr.FullAddressOld = AddressInput;

                        var src = (addr.Source ?? string.Empty).ToLowerInvariant();

                        if (src.Contains("cat") || src.Contains("catastro"))
                        {
                            addr.ScoreText = "Aproximada por Catastro";
                        }
                        else if (string.IsNullOrWhiteSpace(addr.Source) || src.Contains("eaab") || src.Contains("bd") || src.Contains("base"))
                        {
                            addr.ScoreText = "Exacta";
                        }
                        else if (src.Contains("esri"))
                        {
                            addr.ScoreText = addr.Score?.ToString() ?? "ESRI";
                        }
                        else
                        {
                            addr.ScoreText = addr.Score?.ToString() ?? "N/A";
                        }

                        _ = ResultsLayerService.AddPointAsync(addr);
                    }
                }

                AddressInput = string.Empty;
                StatusMessage = $" {result.Count} resultado(s) encontrado(s)";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en búsqueda Oracle: {ex.Message}");
                StatusMessage = $"Error Oracle: {ex.Message}";
            }
        }

        private void HandlePostgreSqlConnection(string input, string cityCode, string cityDesc)
        {
            try
            {
                var props = GetDatabaseConnectionProperties();
                
                var addressNormalizer = new AddressNormalizer(DBEngine.PostgreSQL, props);
                var addressSearch = new AddressSearchUseCase(DBEngine.PostgreSQL, props);

                var model = new AddressNormalizerModel { Address = input };
                var address = addressNormalizer.Invoke(model);

                var searchAddress = !string.IsNullOrWhiteSpace(address.AddressEAAB)
                    ? address.AddressEAAB
                    : (!string.IsNullOrWhiteSpace(address.AddressNormalizer)
                        ? address.AddressNormalizer
                        : input);

                var result = addressSearch.Invoke(searchAddress, cityCode, cityDesc);

                if (result == null || result.Count == 0)
                {
                    StatusMessage = $" Sin coincidencias en {SelectedCity.CityDesc}";
                    return;
                }

                foreach (var addr in result)
            {
                if (addr.Latitud.HasValue && addr.Longitud.HasValue)
                {
                    addr.CityDesc = SelectedCity.CityDesc;
                    addr.FullAddressOld = AddressInput;

                    // --- Asignar ScoreText según origen ---
                    var src = (addr.Source ?? string.Empty).ToLowerInvariant();

                    if (src.Contains("cat") || src.Contains("catastro"))
                    {
                        addr.ScoreText = "Aproximada por Catastro";
                    }
                    else if (string.IsNullOrWhiteSpace(addr.Source) || src.Contains("eaab") || src.Contains("bd") || src.Contains("base"))
                    {
                        addr.ScoreText = "Exacta";
                    }
                    else if (src.Contains("esri"))
                    {
                        addr.ScoreText = addr.Score?.ToString() ?? "ESRI";
                    }
                    else
                    {
                        addr.ScoreText = addr.Score?.ToString() ?? "N/A";
                    }

                    _ = ResultsLayerService.AddPointAsync(addr);
                }
            }

                AddressInput = string.Empty;
                StatusMessage = $" {result.Count} resultado(s) encontrado(s)";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Error en búsqueda PostgreSQL: {ex.Message}");
                StatusMessage = $" Error PostgreSQL: {ex.Message}";
            }
        }

        private bool IsConnectionReady()
        {
            try
            {
                var settings = Module1.Settings;
                var dbService = Module1.DatabaseConnection;

                if (string.IsNullOrWhiteSpace(settings?.motor) ||
                    string.IsNullOrWhiteSpace(settings?.host) ||
                    string.IsNullOrWhiteSpace(settings?.usuario) ||
                    string.IsNullOrWhiteSpace(settings?.baseDeDatos))
                {
                    return false;
                }

                return dbService?.Geodatabase != null;
            }
            catch
            {
                return false;
            }
        }


        private ArcGIS.Core.Data.DatabaseConnectionProperties GetDatabaseConnectionProperties()
        {
            var settings = Module1.Settings;
            var engine = settings.motor.ToDBEngine();

            return engine switch
            {
                DBEngine.Oracle => ConnectionPropertiesFactory.CreateOracleConnection(
                    settings.host, settings.usuario, settings.contraseña, 
                    settings.baseDeDatos, settings.puerto ?? "1521"),
                DBEngine.PostgreSQL => ConnectionPropertiesFactory.CreatePostgresConnection(
                    settings.host, settings.usuario, settings.contraseña, 
                    settings.baseDeDatos, settings.puerto ?? "5432"),
                _ => throw new NotSupportedException($"Motor {settings.motor} no soportado")
            };
        }
    }
}