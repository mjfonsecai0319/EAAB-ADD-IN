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

namespace EAABAddIn;

internal class Button1 : Button
{
    protected override void OnClick()
    {
        var dialog = new Src.UI.InputTextDialog();
        bool? result = dialog.ShowDialog();

        if (result == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            QueuedTask.Run(() => ProcessInput(dialog.InputText));
        }
    }

    private async Task ProcessInput(string input)
    {
        await QueuedTask.Run(() =>
        {
            try
            {
                var engine = Module1.Settings.motor.ToDBEngine();

                if (engine == DBEngine.Oracle)
                {
                    HandleOracleConnection(input);
                    return;
                }

                if (engine == DBEngine.PostgreSQL)
                {
                    HandlePostgreSqlConnection(input);
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

    private void HandleOracleConnection(string input)
    {
        var props = ConnectionPropertiesFactory.CreateOracleConnection(
            instance: Module1.Settings.host,
            user: Module1.Settings.usuario,
            password: Module1.Settings.contraseña
        );
        var repository = new AddressLexEntityOracleRepository();
        var addressNormalizer = new AddressNormalizer(repository, props);
        var model = new AddressNormalizerModel { Address = input };
        var response = addressNormalizer.Invoke(model);
        var responseMessage = new StringBuilder();

        responseMessage.AppendLine($"Dirección Original: {response.Address}");
        responseMessage.AppendLine($"Normalizada: {response.AddressNormalizer}");
        responseMessage.AppendLine($"Principal: {response.Principal}");
        responseMessage.AppendLine($"Generador: {response.Generador}");
        responseMessage.AppendLine($"Placa: {response.Plate}");
        MessageBox.Show(responseMessage.ToString(), "Resultado de Normalización");
    }

    private void HandlePostgreSqlConnection(string input)
    {
        var props = ConnectionPropertiesFactory.CreatePostgresConnection(
            instance: "",
            user: "",
            password: "",
            database: ""
        );
        var repository = new AddressLexEntityPostgresRepository();
        var addressNormalizer = new AddressNormalizer(repository, props);
        var model = new AddressNormalizerModel { Address = input };
        var response = addressNormalizer.Invoke(model);
        var responseMessage = new StringBuilder();

        responseMessage.AppendLine($"Dirección Original: {response.Address}");
        responseMessage.AppendLine($"Normalizada: {response.AddressNormalizer}");
        responseMessage.AppendLine($"Principal: {response.Principal}");
        responseMessage.AppendLine($"Generador: {response.Generador}");
        responseMessage.AppendLine($"Placa: {response.Plate}");
        MessageBox.Show(responseMessage.ToString(), "Resultado de Normalización");
    }
}

