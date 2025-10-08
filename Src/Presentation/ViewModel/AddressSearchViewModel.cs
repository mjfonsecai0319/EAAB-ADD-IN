using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;

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

        private string _gdbPath;
        public string GdbPath
        {
            get => _gdbPath;
            set
            {
                if (_gdbPath != value)
                {
                    _gdbPath = value;
                    NotifyPropertyChanged(nameof(GdbPath));
                }
            }
        }

        public ICommand SearchCommand { get; }
        public ICommand RefreshCitiesCommand { get; }
        public ICommand BrowseGdbCommand { get; }

        public AddressSearchViewModel()
        {
            var path = Project.Current.DefaultGeodatabasePath;

            GdbPath = path;
            Debug.WriteLine("=== Inicializando AddressSearchViewModel ===");
            Cities = new ObservableCollection<PtAddressGralEntity>();
            SelectedCity = null;
            SearchCommand = new AsyncRelayCommand(OnSearchAsync);
            RefreshCitiesCommand = new AsyncRelayCommand(LoadCitiesAsync);
            BrowseGdbCommand = new RelayCommand(BrowseGdb);

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
                var engine = settings?.motor.ToDBEngine();

                if (engine == DBEngine.Unknown)
                {
                    Debug.WriteLine("Motor desconocido");
                    ConnectionStatus = "Motor desconocido";
                    StatusMessage = "Seleccione un motor válido en las opciones";
                    return;
                }

                // Sólo exigir host/usuario cuando NO es Oracle SDE
                if (engine != DBEngine.OracleSDE)
                {
                    if (string.IsNullOrWhiteSpace(settings?.host) || string.IsNullOrWhiteSpace(settings?.usuario))
                    {
                        Debug.WriteLine("Configuración incompleta (host/usuario)");
                        ConnectionStatus = "Configuración incompleta";
                        StatusMessage = "Configure la conexión en las opciones";
                        return;
                    }
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

                Debug.WriteLine($"Motor de BD: {engine}");
                if (engine != DBEngine.OracleSDE)
                {
                    try { var _ = GetDatabaseConnectionProperties(); Debug.WriteLine("Propiedades de conexión creadas"); } catch (Exception ex) { Debug.WriteLine($"No se pudieron crear props: {ex.Message}"); }
                }

                List<PtAddressGralEntity> ciudades = null;

                await QueuedTask.Run(() =>
                {
                    try
                    {
                        Debug.WriteLine("Creando repositorio...");
                        IPtAddressGralEntityRepository repo = engine switch
                        {
                            DBEngine.Oracle => new PtAddressGralOracleRepository(),
                            DBEngine.OracleSDE => new PtAddressGralOracleRepository(), // reutiliza implementación Oracle
                            DBEngine.PostgreSQL => new PtAddressGralPostgresRepository(),
                            _ => throw new NotSupportedException($"Motor {engine} no soportado")
                        };

                        Debug.WriteLine("Obteniendo ciudades del repositorio...");
                        ciudades = repo.GetAllCities();
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

        private void BrowseGdb()
        {
            var filter = new BrowseProjectFilter("esri_browseDialogFilters_geodatabases");

            var dlg = new OpenItemDialog
            {
                Title = "Seleccionar Geodatabase",
                BrowseFilter = filter,
                MultiSelect = false,
                InitialLocation = !string.IsNullOrWhiteSpace(GdbPath)
                    ? System.IO.Path.GetDirectoryName(GdbPath)
                    : Project.Current?.HomeFolderPath
            };

            var ok = dlg.ShowDialog();
            if (ok == true && dlg.Items != null && dlg.Items.Any())
            {
                var item = dlg.Items.First();
                // item.Path devolverá la ruta a la carpeta .gdb seleccionada
                GdbPath = item.Path;
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
                    Debug.WriteLine($"[OnSearchAsync] Engine: {engine}");
                    if (engine == DBEngine.Oracle)
                        HandleOracleConnection(AddressInput, SelectedCity.CityCode, SelectedCity.CityDesc);
                    else if (engine == DBEngine.OracleSDE)
                        HandleOracleSdeConnection(AddressInput, SelectedCity.CityCode, SelectedCity.CityDesc);
                    else if (engine == DBEngine.PostgreSQL)
                        HandlePostgreSqlConnection(AddressInput, SelectedCity.CityCode, SelectedCity.CityDesc);
                    else if (engine == DBEngine.PostgreSQLSDE)
                        HandlePostgreSqlSdeConnection(AddressInput, SelectedCity.CityCode, SelectedCity.CityDesc);
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

        private void HandleOracleSdeConnection(string input, string cityCode, string cityDesc)
        {
            try
            {
                Debug.WriteLine("[HandleOracleSdeConnection] inicio");
                DatabaseConnectionProperties props = null; // no requerido realmente
                var normalizer = new AddressNormalizer(DBEngine.Oracle, props);
                var search = new AddressSearchUseCase(DBEngine.Oracle);

                string searchAddress;
                try
                {
                    var model = new AddressNormalizerModel { Address = input };
                    var normalized = normalizer.Invoke(model);
                    searchAddress = !string.IsNullOrWhiteSpace(normalized.AddressEAAB)
                        ? normalized.AddressEAAB
                        : (!string.IsNullOrWhiteSpace(normalized.AddressNormalizer) ? normalized.AddressNormalizer : input);
                }
                catch (EAABAddIn.Src.Application.Errors.BusinessException bex)
                {
                    // Si falla el léxico (CODE_145 ó CODE_146) seguimos con la dirección original sin abortar.
                    if (bex.Message.StartsWith("CODE_145") || bex.Message.StartsWith("CODE_146"))
                    {
                        Debug.WriteLine($"[HandleOracleSdeConnection] Normalizador fallback por {bex.Message}. Uso dirección original.");
                        searchAddress = input;
                    }
                    else throw; // otros códigos se dejan propagar
                }

                var result = search.Invoke(
                    address: searchAddress,
                    cityCode: cityCode,
                    cityDesc: cityDesc,
                    gdbPath: GdbPath,
                    showNoResultsMessage: true
                );

                if (result == null || result.Count == 0)
                {
                    StatusMessage = $"Sin coincidencias en {SelectedCity.CityDesc}";
                    Debug.WriteLine("[HandleOracleSdeConnection] 0 resultados");
                    return;
                }

                foreach (var addr in result)
                {
                    if (addr.Latitud.HasValue && addr.Longitud.HasValue)
                    {
                        addr.CityDesc = SelectedCity.CityDesc;
                        addr.FullAddressOld = AddressInput;
                        ClasificarYNormalizar(addr);
                        _ = ResultsLayerService.AddPointAsync(addr, GdbPath);
                    }
                }
                AddressInput = string.Empty;
                StatusMessage = $" {result.Count} resultado(s) encontrado(s)";
                Debug.WriteLine($"[HandleOracleSdeConnection] {result.Count} resultados");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en búsqueda Oracle SDE: {ex.Message}");
                StatusMessage = $"Error Oracle SDE: {ex.Message}";
            }
        }

        private void HandlePostgreSqlSdeConnection(string input, string cityCode, string cityDesc)
        {
            try
            {
                Debug.WriteLine("[HandlePostgreSqlSdeConnection] inicio");
                DatabaseConnectionProperties props = null; // no requerido para .sde
                var normalizer = new AddressNormalizer(DBEngine.PostgreSQL, props);
                var search = new AddressSearchUseCase(DBEngine.PostgreSQL);

                string searchAddress;
                try
                {
                    var model = new AddressNormalizerModel { Address = input };
                    var normalized = normalizer.Invoke(model);
                    searchAddress = !string.IsNullOrWhiteSpace(normalized.AddressEAAB)
                        ? normalized.AddressEAAB
                        : (!string.IsNullOrWhiteSpace(normalized.AddressNormalizer)
                            ? normalized.AddressNormalizer
                            : input);
                }
                catch (EAABAddIn.Src.Application.Errors.BusinessException bex)
                {
                    if (bex.Message.StartsWith("CODE_145") || bex.Message.StartsWith("CODE_146"))
                    {
                        Debug.WriteLine($"[HandlePostgreSqlSdeConnection] Normalizador fallback por {bex.Message}. Uso dirección original.");
                        searchAddress = input;
                    }
                    else throw;
                }

                var result = search.Invoke(searchAddress, cityCode, cityDesc, GdbPath);
                if (result == null || result.Count == 0)
                {
                    StatusMessage = $"Sin coincidencias en {SelectedCity.CityDesc}";
                    Debug.WriteLine("[HandlePostgreSqlSdeConnection] 0 resultados");
                    return;
                }

                foreach (var addr in result)
                {
                    if (addr.Latitud.HasValue && addr.Longitud.HasValue)
                    {
                        addr.CityDesc = SelectedCity.CityDesc;
                        addr.FullAddressOld = AddressInput; // dirección original exacta
                        ClasificarYNormalizar(addr);
                        _ = ResultsLayerService.AddPointAsync(addr, GdbPath);
                    }
                }
                AddressInput = string.Empty;
                StatusMessage = $" {result.Count} resultado(s) encontrado(s)";
                Debug.WriteLine($"[HandlePostgreSqlSdeConnection] {result.Count} resultados");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en búsqueda PostgreSQL SDE: {ex.Message}");
                StatusMessage = $"Error PostgreSQL SDE: {ex.Message}";
            }
        }

        private void HandleOracleConnection(string input, string cityCode, string cityDesc)
        {
            try
            {
                var props = GetDatabaseConnectionProperties();

                var addressNormalizer = new AddressNormalizer(DBEngine.Oracle, props);
                var addressSearch = new AddressSearchUseCase(DBEngine.Oracle);

                string searchAddress;
                try
                {
                    var model = new AddressNormalizerModel { Address = input };
                    var address = addressNormalizer.Invoke(model);
                    searchAddress = !string.IsNullOrWhiteSpace(address.AddressEAAB)
                        ? address.AddressEAAB
                        : (!string.IsNullOrWhiteSpace(address.AddressNormalizer)
                            ? address.AddressNormalizer
                            : input);
                }
                catch (EAABAddIn.Src.Application.Errors.BusinessException bex)
                {
                    if (bex.Message.StartsWith("CODE_145") || bex.Message.StartsWith("CODE_146"))
                    {
                        Debug.WriteLine($"[HandleOracleConnection] Normalizador fallback por {bex.Message}. Uso dirección original.");
                        searchAddress = input;
                    }
                    else throw;
                }

                var result = addressSearch.Invoke(searchAddress, cityCode, cityDesc, GdbPath);

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

                        ClasificarYNormalizar(addr);

                        _ = ResultsLayerService.AddPointAsync(addr, GdbPath);
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
                var addressSearch = new AddressSearchUseCase(DBEngine.PostgreSQL);

                string searchAddress;
                try
                {
                    var model = new AddressNormalizerModel { Address = input };
                    var address = addressNormalizer.Invoke(model);
                    searchAddress = !string.IsNullOrWhiteSpace(address.AddressEAAB)
                        ? address.AddressEAAB
                        : (!string.IsNullOrWhiteSpace(address.AddressNormalizer)
                            ? address.AddressNormalizer
                            : input);
                }
                catch (EAABAddIn.Src.Application.Errors.BusinessException bex)
                {
                    if (bex.Message.StartsWith("CODE_145") || bex.Message.StartsWith("CODE_146"))
                    {
                        Debug.WriteLine($"[HandlePostgreSqlConnection] Normalizador fallback por {bex.Message}. Uso dirección original.");
                        searchAddress = input;
                    }
                    else throw;
                }

                var result = addressSearch.Invoke(searchAddress, cityCode, cityDesc, GdbPath);

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
                        ClasificarYNormalizar(addr);

                        _ = ResultsLayerService.AddPointAsync(addr, GdbPath);
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
                var engine = settings?.motor.ToDBEngine();
                if (engine == DBEngine.OracleSDE)
                {
                    if (string.IsNullOrWhiteSpace(settings?.rutaArchivoCredenciales)) return false;
                    return dbService?.Geodatabase != null;
                }
                if (string.IsNullOrWhiteSpace(settings?.motor) ||
                    string.IsNullOrWhiteSpace(settings?.host) ||
                    string.IsNullOrWhiteSpace(settings?.usuario) ||
                    string.IsNullOrWhiteSpace(settings?.baseDeDatos)) return false;
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

        // Lógica unificada de clasificación de origen / ScoreText y normalización de direccion mostrada
        private void ClasificarYNormalizar(PtAddressGralEntity entity)
        {
            if (entity is null)
            {
                return;
            }

            var source = entity.Source.ToLowerInvariant();

            if (source == "eaab")
            {
                entity.Source = "EAAB";
                entity.ScoreText = "Exacta";
                return;
            }

            if (string.IsNullOrWhiteSpace(entity.FullAddressOld))
            {
                entity.FullAddressOld = entity.MainStreet ?? entity.FullAddressEAAB ?? entity.FullAddressCadastre;
            }
        }
    }
}