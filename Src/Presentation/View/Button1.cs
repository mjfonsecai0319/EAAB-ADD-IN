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
using EAABAddIn.Src.Core.Map;        

namespace EAABAddIn.Src.Presentation.View;

internal class Button1 : Button
{
    protected override void OnClick()
    {
        var dialog = new Src.UI.InputTextDialog();
        bool? result = dialog.ShowDialog();

        if (result == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var selectedCityCode = dialog.SelectedCityCode;
            QueuedTask.Run(() => ProcessInput(dialog.InputText, selectedCityCode));
        }
    }

    private async Task ProcessInput(string input, string cityCode)
    {
        await QueuedTask.Run(() =>
        {
            try
            {
                var engine = Module1.Settings.motor.ToDBEngine();

                if (engine == DBEngine.Oracle)
                {
                    HandleOracleConnection(input, cityCode);
                    return;
                }

                if (engine == DBEngine.PostgreSQL)
                {
                    HandlePostgreSqlConnection(input, cityCode);
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

    private void HandleOracleConnection(string input, string cityCode)
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

        // ✅ Fallback inteligente
        var searchAddress = !string.IsNullOrWhiteSpace(address.AddressEAAB)
            ? address.AddressEAAB
            : (!string.IsNullOrWhiteSpace(address.AddressNormalizer)
                ? address.AddressNormalizer
                : input);

        var result = addressSearch.Invoke(searchAddress, cityCode);

        if (result == null || result.Count == 0)
        {
            MessageBox.Show("No se encontró una coincidencia. Valide la ciudad y la dirección.",
                            "Validación");
            return;
        }

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
                    (decimal)addr.Latitud.Value,
                    (decimal)addr.Longitud.Value
                );
            }
        }

        MessageBox.Show(responseMessage.ToString(), "Resultado de Normalización");
    }

    private void HandlePostgreSqlConnection(string input, string cityCode)
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

        // ✅ Fallback inteligente
        var searchAddress = !string.IsNullOrWhiteSpace(address.AddressEAAB)
            ? address.AddressEAAB
            : (!string.IsNullOrWhiteSpace(address.AddressNormalizer)
                ? address.AddressNormalizer
                : input);

        var result = addressSearch.Invoke(searchAddress, cityCode);

        if (result == null || result.Count == 0)
        {
            MessageBox.Show("No se encontró una coincidencia. Valide la ciudad y la dirección.",
                            "Validación");
            return;
        }

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
                    (decimal)addr.Latitud.Value,
                    (decimal)addr.Longitud.Value
                );
            }
        }

        MessageBox.Show(responseMessage.ToString(), "Resultado de Normalización");
    }
}
