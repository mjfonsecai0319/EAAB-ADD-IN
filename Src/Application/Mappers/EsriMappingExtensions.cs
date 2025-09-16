using EAABAddIn.Src.Application.Models;
using EAABAddIn.Src.Core.Entities;

namespace EAABAddIn.Src.Application.Mappers.EsriMappingExtensions;

internal static class EsriMappingExtensions
{
    internal static PtAddressGralEntity ToPtAddressGral(this EsriCandidate candidate)
    {
        if (candidate == null || candidate.Location == null) return null;
        return new PtAddressGralEntity
        {
            Source = "ESRI GEOCODE",
            CityCode = null,
            CityDesc = null,
            FullAddressCadastre = candidate.Address,
            FullAddressEAAB = candidate.Address,
            FullAddressOld = candidate.Address,
            Longitud = (decimal?)candidate.Location.X,
            Latitud = (decimal?)candidate.Location.Y,
            NeighborhoodDesc = null,
            NeighborhoodCode = null,
            LocaDesc = null,
            LocaCode = null,
            ZoneDesc = null,
            ZoneCode = null,
            PointType = "GEOCODE_CANDIDATE",
            HouseNumber = "00",
            Score = candidate.Score ?? 0
        };
    }
}