using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using ArcGIS.Core.Data;

using EAABAddIn.Src.Application.Errors;
using EAABAddIn.Src.Application.Models;
using EAABAddIn.Src.Core;
using EAABAddIn.Src.Domain.Repositories;

namespace EAABAddIn.Src.Application.UseCases;

public class AddressNormalizer
{
    private readonly IAddressLexEntityRepository _addressLexRepository;
    private readonly DatabaseConnectionProperties _connectionProperties;

    public AddressNormalizer(DBEngine engine, DatabaseConnectionProperties connectionProperties)
    {
        this._addressLexRepository = this.GetRepository(engine);
        this._connectionProperties = connectionProperties;
    }

    public AddressNormalizerModelResponse Invoke(AddressNormalizerModel addressModel)
    {
        var response = AddressNormalizerModelResponse.FromAddressNormalizer(addressModel);
        ClearAddress(response);
        AddressIdentify(response);
        FindLex(response);
        return response;
    }

    private void ClearAddress(AddressNormalizerModelResponse addressResponse)
    {
        string address = $"{addressResponse.Address} ".ToUpper();
        address = Regex.Replace(address, @"[^A-Z0-9]", " ");
        address = Regex.Replace(address, "BIS", "zzzzz");
        address = Regex.Replace(address, @" +", " ");
        address = Regex.Replace(address, @"(\sESTE\s|\sEST\s|\sES\s|\sE\s)", "yyyyy");
        address = Regex.Replace(address, @"(\sSUR\s|\sSU\s|\sS\s)", "xxxxx");
        address = address.Replace("zzzzz", " BIS ");
        address = address.Replace("yyyyy", " ESTE ");
        address = address.Replace("xxxxx", " SUR ");
        address = Regex.Replace(address, @" +", " ").Trim();
        address = address.Replace(' ', '|') + "|";
        addressResponse.AddressNormalizer = address;
    }

    private void AddressIdentify(AddressNormalizerModelResponse addressModel)
    {
        string regex = @"^(?<ppal>\w+\|[0-9]{1,3}\|?(?:BIS\|)?(?:SUR)?\|?(?:BIS\|)?(SUR|ESTE)?\|?[A-Z]?\|?(?:BIS\|)?(SUR|ESTE)?\|?[A-Z]?\|?(SUR|ESTE)?\|?)(?<genera>[0-9]{1,3}\|?(SUR|ESTE)?[A-Z]?\|?(SUR|ESTE)?\|)(?<placa>[0-9]{1,3}\|?(SUR|ESTE)?\|)?(?<compl>(\w+\|))?";
        string address = $"{addressModel.AddressNormalizer} ";
        var pattern = new Regex(regex, RegexOptions.Multiline);
        var match = pattern.Match(address);

        if (match.Success)
        {
            var strings = GetStrings(address);
            addressModel.Principal = match.Groups["ppal"].Value;
            addressModel.Generador = match.Groups["genera"].Value;
            addressModel.Plate = !string.IsNullOrEmpty(match.Groups["placa"].Value) ? match.Groups["placa"].Value : "00";
            addressModel.Complement = match.Groups["compl"].Value ?? "";

            foreach (var cardinalidad in strings)
            {
                if (addressModel.Principal.Contains(cardinalidad))
                {
                    addressModel.CardinalidadPrincipal = cardinalidad;
                    addressModel.Principal = addressModel.Principal.Replace(cardinalidad, "");
                }
                if (addressModel.Generador.Contains(cardinalidad))
                {
                    addressModel.CardinalidadGenerador = cardinalidad;
                    addressModel.Generador = addressModel.Generador.Replace(cardinalidad, "");
                }
                if (addressModel.Plate.Contains(cardinalidad))
                {
                    addressModel.CardinalidadGenerador = cardinalidad; // Asumo que es generador por el código python
                    addressModel.Plate = addressModel.Plate.Replace(cardinalidad, "");
                }
            }

            CleanAddressModel(addressModel);
            return;
        }
        throw new BusinessException("CODE_146: Error identificando la dirección.");
    }

    private List<string> GetStrings(string address)
    {
        var exist = new HashSet<string>();
        var parts = address.Split('|');
        var strings = new List<string> { "SUR", "ESTE", "BIS" };

        foreach (var part in parts)
        {
            if (strings.Contains(part))
            {
                if (!exist.Add(part))
                {
                    throw new BusinessException("CODE_146: Cardinalidad duplicada.");
                }
            }
        }
        strings.Remove("BIS");
        return strings;
    }

    private void CleanAddressModel(AddressNormalizerModelResponse addressModel)
    {
        addressModel.Principal = Regex.Replace(addressModel.Principal, @"\|+", " ");
        addressModel.Principal = Regex.Replace(addressModel.Principal, @" +", " ").Trim();

        addressModel.Generador = Regex.Replace(addressModel.Generador, @"\|+", " ");
        addressModel.Generador = Regex.Replace(addressModel.Generador, @" +", " ");
        addressModel.Generador = Regex.Replace(addressModel.Generador, @"\s", "").Trim();

        addressModel.Plate = Regex.Replace(addressModel.Plate, @"\|+", " ");
        addressModel.Plate = Regex.Replace(addressModel.Plate, @" +", " ").Trim();

        addressModel.Complement = Regex.Replace(addressModel.Complement, @"\|+", " ");
        addressModel.Complement = Regex.Replace(addressModel.Complement, @" +", " ").Trim();
    }

    private void FindLex(AddressNormalizerModelResponse addressModel)
    {
        var regex = new Regex(@"([A-Z]+ )", RegexOptions.Multiline);
        var match = regex.Match(addressModel.Principal);

        if (match.Success)
        {
            string part = match.Groups[1].Value.Trim();
            var tAddressLexEntity = _addressLexRepository.FindByWord(_connectionProperties, part);

            if (tAddressLexEntity != null)
            {
                string principal = addressModel.Principal;
                string word = tAddressLexEntity.Word;
                string stdWord = tAddressLexEntity.StdWord;

                principal = Regex.Replace(principal, @"\s", "");
                principal = principal.Replace(word, $"{stdWord} ");

                addressModel.Principal = principal;
            }
            else
            {
                throw new BusinessException("CODE_145: No se encontró la palabra en el léxico.");
            }
        }
    }

    private IAddressLexEntityRepository GetRepository(DBEngine engine) => engine switch
    {
        DBEngine.Oracle => new AddressLexEntityOracleRepository(),
        DBEngine.PostgreSQL => new AddressLexEntityPostgresRepository(),
        _ => throw new NotSupportedException($"El motor de base de datos '{engine}' no es compatible.")
    };
}
