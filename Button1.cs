using System;
using System.Text;
using System.Threading.Tasks;

using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Application.Errors;
using EAABAddIn.Src.Application.Models;
using EAABAddIn.Src.Application.UseCases;
using EAABAddIn.Src.Core.Data;
using EAABAddIn.Src.Domain.Repositories;

namespace EAABAddIn;

internal class Button1 : Button
{
    private readonly IDatabaseConnectionService _connectionService;

    public Button1()
    {
        _connectionService = new DatabaseConnectionService();
    }

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
                var connectionProps = ConnectionPropertiesFactory.CreateOracleConnection(
                    instance: "172.19.8.169:1548/SITIODEV",
                    user: "sgo",
                    password: "sgodev01"
                );

                IAddressLexEntityRepository repository = new AddressLexEntityOracleRepository();
                var addressNormalizer = new AddressNormalizer(repository, connectionProps);

                var model = new AddressNormalizerModel
                {
                    Address = input,
                    // Asigna otros valores si es necesario
                    ApplicationId = "EAAB-ADD-IN",
                    Secret = "some-secret"
                };

                var response = addressNormalizer.Invoke(model);

                var responseMessage = new StringBuilder();
                responseMessage.AppendLine($"Dirección Original: {response.Address}");
                responseMessage.AppendLine($"Normalizada: {response.AddressNormalizer}");
                responseMessage.AppendLine($"Principal: {response.Principal}");
                responseMessage.AppendLine($"Generador: {response.Generador}");
                responseMessage.AppendLine($"Placa: {response.Plate}");

                MessageBox.Show(responseMessage.ToString(), "Resultado de Normalización");
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
}

