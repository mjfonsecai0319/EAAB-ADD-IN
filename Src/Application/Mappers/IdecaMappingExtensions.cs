using System;

using EAABAddIn.Src.Application.Models;
using EAABAddIn.Src.Core.Entities;

namespace EAABAddIn.Src.Application.Mappers.IdecaMappingExtensions;

// Clase estática para extensión de mapeo IDECA -> PtAddressGralEntity
internal static class IdecaMappingExtensions
{
    internal static PtAddressGralEntity ToPtAddressGral(this IdecaData data)
    {
        if (data == null) return null;
        decimal? lon = TryParseDecimal(data.Longitude) ?? (data.XInput.HasValue ? Convert.ToDecimal(data.XInput.Value) : null);
        decimal? lat = TryParseDecimal(data.Latitude) ?? (data.YInput.HasValue ? Convert.ToDecimal(data.YInput.Value) : null);
        if (lon == null || lat == null) return null;
        return new PtAddressGralEntity
        {
            Source = "IDECA API",
            CityCode = data.Codloc,
            CityDesc = "BOGOTA D.C.",
            FullAddressCadastre = data.Dirtrad ?? data.Diraprox ?? data.Dirinput,
            FullAddressEAAB = data.Dirinput,
            FullAddressOld = data.Dirinput,
            Longitud = lon,
            Latitud = lat,
            NeighborhoodDesc = data.Nomseccat,
            NeighborhoodCode = data.Codseccat,
            LocaDesc = data.Localidad,
            LocaCode = data.Codloc,
            ZoneDesc = data.Nomupz,
            ZoneCode = data.Codupz,
            PointType = data.Tipo_Direccion,
            HouseNumber = "00"
        };
    }

    private static decimal? TryParseDecimal(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            return v;
        return null;
    }
}
