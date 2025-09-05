using System;
using System.Text;
using System.Threading.Tasks;

using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Application.Errors;
using EAABAddIn.Src.Application.Models;
using EAABAddIn.Src.Application.UseCases;
using EAABAddIn.Src.Core;
using EAABAddIn.Src.Core.Data;
using EAABAddIn.Src.Domain.Repositories;
using EAABAddIn.Map;

namespace EAABAddIn;

internal class Button1 : Button
{
    protected override void OnClick()
    {
        var dialog = new Src.UI.InputTextDialog();
        bool? result = dialog.ShowDialog();

        if (result == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var selectedCity = dialog.SelectedCity;

            QueuedTask.Run(() => ProcessInput(dialog.InputText, selectedCity));
        }
    }
    private string GetCityCode(string city)
    {
        return city switch
        {
            "Bogotá"     => "11001",
            "Soacha"     => "25754",
            "Gachancipá" => "25317",
            _ => ""
        };
    }

        private async Task ProcessInput(string input, string city)
    {
        await QueuedTask.Run(() =>
        {
            try
            {
                var engine = Module1.Settings.motor.ToDBEngine();

                if (engine == DBEngine.Oracle)
                {
                    HandleOracleConnection(input, city);
                    return;
                }

                if (engine == DBEngine.PostgreSQL)
                {
                    HandlePostgreSqlConnection(input, city);
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


    private void HandleOracleConnection(string input, string city)
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
        var responseMessage = new StringBuilder();
        var cityCode = GetCityCode(city);
        var result = addressSearch.Invoke(address.AddressEAAB, cityCode);

        responseMessage.AppendLine($"Dirección Original: {address.Address}");
        responseMessage.AppendLine($"Normalizada: {address.AddressNormalizer}");
        responseMessage.AppendLine($"Principal: {address.Principal}");
        responseMessage.AppendLine($"Generador: {address.Generador}");
        responseMessage.AppendLine($"Placa: {address.Plate}");

        foreach (var addr in result)
        {
            responseMessage.AppendLine($"--- Resultado de Búsqueda ---");
            responseMessage.AppendLine($"ID: {addr.ID}");
            responseMessage.AppendLine($"Dirección: {addr.FullAddressEAAB}");
            responseMessage.AppendLine($"Código Ciudad: {addr.CityCode}");
            responseMessage.AppendLine($"Latitud: {addr.Latitud}");
            responseMessage.AppendLine($"Longitud: {addr.Longitud}");

            if (addr.Latitud.HasValue && addr.Longitud.HasValue)
            {
            _ = ResultsLayerService.AddPointAsync(
                (double)addr.Latitud.Value,
                (double)addr.Longitud.Value
            );
            }

            
        }
        MessageBox.Show(responseMessage.ToString(), "Resultado de Normalización");
    }

    private void HandlePostgreSqlConnection(string input, string city)
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
        var cityCode = GetCityCode(city);
        var result = addressSearch.Invoke(address.AddressEAAB, cityCode);
        var responseMessage = new StringBuilder();

        responseMessage.AppendLine($"Dirección Original: {address.Address}");
        responseMessage.AppendLine($"Normalizada: {address.AddressNormalizer}");
        responseMessage.AppendLine($"Principal: {address.Principal}");
        responseMessage.AppendLine($"Generador: {address.Generador}");
        responseMessage.AppendLine($"Placa: {address.Plate}");

        foreach (var addr in result)
        {
            responseMessage.AppendLine($"--- Resultado de Búsqueda ---");
            responseMessage.AppendLine($"ID: {addr.ID}");
            responseMessage.AppendLine($"Dirección: {addr.FullAddressEAAB}");
            responseMessage.AppendLine($"Código Ciudad: {addr.CityCode}");
            responseMessage.AppendLine($"Latitud: {addr.Latitud}");
            responseMessage.AppendLine($"Longitud: {addr.Longitud}");

            if (addr.Latitud.HasValue && addr.Longitud.HasValue)
            {
                _ = ResultsLayerService.AddPointAsync(
                    (double)addr.Latitud.Value,
                    (double)addr.Longitud.Value
                );
            }

        }
        MessageBox.Show(responseMessage.ToString(), "Resultado de Normalización");
        
    }
}

