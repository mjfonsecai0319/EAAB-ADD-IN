using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

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
    internal class AddressSearchViewModel : PanelViewModelBase
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

        public ICommand SearchCommand { get; }

        public AddressSearchViewModel()
        {
            Cities = new ObservableCollection<PtAddressGralEntity>();
            SelectedCity = null;
            SearchCommand = new AsyncRelayCommand(OnSearchAsync);
            _ = LoadCitiesAsync();
        }

        private async Task LoadCitiesAsync()
        {
            try
            {
                var engine = Module1.Settings.motor.ToDBEngine();

                var props = engine == DBEngine.Oracle
                    ? ConnectionPropertiesFactory.CreateOracleConnection(
                        instance: Module1.Settings.host,
                        user: Module1.Settings.usuario,
                        password: Module1.Settings.contraseña
                      )
                    : ConnectionPropertiesFactory.CreatePostgresConnection(
                        instance: Module1.Settings.host,
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

                Cities.Clear();
                ciudades.ForEach(Cities.Add);
                SelectedCity = ciudades?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar ciudades: {ex.Message}", "Error");
            }
        }

        private async Task OnSearchAsync()
        {
            await QueuedTask.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(AddressInput))
                {
                    MessageBox.Show("Por favor ingrese una dirección para buscar.", "Validación");
                    return;
                }
                if (SelectedCity is null)
                {
                    MessageBox.Show("Por favor seleccione una ciudad.", "Validación");
                    return;
                }
                try
                {
                    var engine = Module1.Settings.motor.ToDBEngine();

                    if (engine == DBEngine.Oracle)
                    {
                        HandleOracleConnection(
                            AddressInput, SelectedCity.CityCode, SelectedCity.CityDesc
                        );
                        return;
                    }
                    if (engine == DBEngine.PostgreSQL)
                    {
                        HandlePostgreSqlConnection(
                            AddressInput, SelectedCity.CityCode, SelectedCity.CityDesc
                        );
                        return;
                    }
                }
                catch (BusinessException bex)
                {
                    MessageBox.Show($"Error de negocio: {bex.Message}", "Error de Normalización");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ha ocurrido un error inesperado: {ex.Message}", "Error");
                }
            });
        }

        private void HandleOracleConnection(string input, string cityCode, string cityDesc)
        {
            var props = ConnectionPropertiesFactory.CreateOracleConnection(
                instance: Module1.Settings.host,
                user: Module1.Settings.usuario,
                password: Module1.Settings.contraseña
            );

            var addressNormalizer = new AddressNormalizer(DBEngine.Oracle, props);
            var addressSearch = new AddressSearchUseCase(DBEngine.Oracle, props);

            var model = new AddressNormalizerModel { Address = input };
            var address = addressNormalizer.Invoke(model);

            // Dirección a usar en la búsqueda
            var searchAddress = !string.IsNullOrWhiteSpace(address.AddressEAAB)
                ? address.AddressEAAB
                : (!string.IsNullOrWhiteSpace(address.AddressNormalizer)
                    ? address.AddressNormalizer
                    : input);

            var result = addressSearch.Invoke(searchAddress, cityCode, cityDesc);

            if (result == null || result.Count == 0)
            {
                MessageBox.Show(
                    $"No se encontró una coincidencia en {SelectedCity.CityDesc}. " +
                    $"Verifique la ciudad y la dirección {searchAddress}.",
                    "Validación"
                );
                return;
            }

            foreach (var addr in result)
            {
                if (addr.Latitud.HasValue && addr.Longitud.HasValue)
                {
                    addr.CityDesc = SelectedCity.CityDesc;
                    addr.FullAddressOld = AddressInput;
                    _ = ResultsLayerService.AddPointAsync(addr);
                }
            }

            AddressInput = string.Empty;
            MessageBox.Show("Dirección procesada con éxito.", "Resultado de Normalización");
        }

        private void HandlePostgreSqlConnection(string input, string cityCode, string cityDesc)
        {
            var props = ConnectionPropertiesFactory.CreatePostgresConnection(
                instance: Module1.Settings.host,
                user: Module1.Settings.usuario,
                password: Module1.Settings.contraseña,
                database: Module1.Settings.baseDeDatos
            );

            var addressNormalizer = new AddressNormalizer(DBEngine.PostgreSQL, props);
            var addressSearch = new AddressSearchUseCase(DBEngine.PostgreSQL, props);

            var model = new AddressNormalizerModel { Address = input };
            var address = addressNormalizer.Invoke(model);

            // Dirección a usar en la búsqueda
            var searchAddress = !string.IsNullOrWhiteSpace(address.AddressEAAB)
                ? address.AddressEAAB
                : (!string.IsNullOrWhiteSpace(address.AddressNormalizer)
                    ? address.AddressNormalizer
                    : input);

            var result = addressSearch.Invoke(searchAddress, cityCode, cityDesc);

            // 🔎 Doble verificación
            result = result.Where(r => r.CityCode == cityCode).ToList();

            if (result == null || result.Count == 0)
            {
                MessageBox.Show(
                    $"No se encontró una coincidencia en {SelectedCity.CityDesc}. " +
                    $"Verifique la ciudad y la dirección {searchAddress}.",
                    "Validación"
                );
                return;
            }

            foreach (var addr in result)
            {
                if (addr.Latitud.HasValue && addr.Longitud.HasValue)
                {
                    addr.CityDesc = SelectedCity.CityDesc;
                    addr.FullAddressOld = AddressInput;
                    _ = ResultsLayerService.AddPointAsync(addr);
                }
            }

            AddressInput = string.Empty;
            MessageBox.Show("Dirección procesada con éxito.", "Resultado de Normalización");
        }
    }
}
