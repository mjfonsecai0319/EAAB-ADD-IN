using System;
using System.Collections.Generic;

namespace EAABAddIn.Src.Core.Entities
{
    public class PtAddressGralEntity
    {
        public long ID { get; set; }
        public string Source { get; set; }
        public string CityCode { get; set; }
        public string CityDesc { get; set; }
        public string LocaCode { get; set; }
        public string LocaDesc { get; set; }
        public string NeighborhoodCode { get; set; }
        public string NeighborhoodDesc { get; set; }
        public string MainStreet { get; set; }
        public string MainStreetSide { get; set; }
        public string MainStreetName { get; set; }
        public string SecondStreet { get; set; }
        public string SecondStreetSide { get; set; }
        public string SecondStreetName { get; set; }
        public string HouseNumber { get; set; }
        public string RoadSection { get; set; }
        public string PointType { get; set; }
        public string FullAddressEAAB { get; set; }
        public string FullAddressCadastre { get; set; }
        public string FullAddressOld { get; set; }
        public decimal? Longitud { get; set; }
        public decimal? Latitud { get; set; }
        public string HydraulicDistrictCode { get; set; }
        public string HydraulicDistrictDescription { get; set; }
        public string ZoneCode { get; set; }
        public string ZoneDesc { get; set; }
        public string SanitaryUgaCode { get; set; }
        public string SanitaryUgaDesc { get; set; }
        public string StormUgaCode { get; set; }
        public string StormUgaDesc { get; set; }
        public string GridH3Index { get; set; }
        public string ZipCode { get; set; }
        public double? Score { get; set; }
        public override bool Equals(object obj)
        {
            if (obj is not PtAddressGralEntity other)
            {
                return false;
            }

            return ID == other.ID &&
                   Source == other.Source &&
                   CityCode == other.CityCode &&
                   CityDesc == other.CityDesc &&
                   LocaCode == other.LocaCode &&
                   LocaDesc == other.LocaDesc &&
                   NeighborhoodCode == other.NeighborhoodCode &&
                   NeighborhoodDesc == other.NeighborhoodDesc &&
                   MainStreet == other.MainStreet &&
                   MainStreetSide == other.MainStreetSide &&
                   MainStreetName == other.MainStreetName &&
                   SecondStreet == other.SecondStreet &&
                   SecondStreetSide == other.SecondStreetSide &&
                   SecondStreetName == other.SecondStreetName &&
                   HouseNumber == other.HouseNumber &&
                   RoadSection == other.RoadSection &&
                   PointType == other.PointType &&
                   FullAddressEAAB == other.FullAddressEAAB &&
                   FullAddressCadastre == other.FullAddressCadastre &&
                   FullAddressOld == other.FullAddressOld &&
                   Longitud == other.Longitud &&
                   Latitud == other.Latitud &&
                   HydraulicDistrictCode == other.HydraulicDistrictCode &&
                   HydraulicDistrictDescription == other.HydraulicDistrictDescription &&
                   ZoneCode == other.ZoneCode &&
                   ZoneDesc == other.ZoneDesc &&
                   SanitaryUgaCode == other.SanitaryUgaCode &&
                   SanitaryUgaDesc == other.SanitaryUgaDesc &&
                   StormUgaCode == other.StormUgaCode &&
                   StormUgaDesc == other.StormUgaDesc &&
                   GridH3Index == other.GridH3Index &&
                   ZipCode == other.ZipCode;
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(ID);
            hashCode.Add(Source);
            hashCode.Add(CityCode);
            hashCode.Add(CityDesc);
            hashCode.Add(LocaCode);
            hashCode.Add(LocaDesc);
            hashCode.Add(NeighborhoodCode);
            hashCode.Add(NeighborhoodDesc);
            hashCode.Add(MainStreet);
            hashCode.Add(MainStreetSide);
            hashCode.Add(MainStreetName);
            hashCode.Add(SecondStreet);
            hashCode.Add(SecondStreetSide);
            hashCode.Add(SecondStreetName);
            hashCode.Add(HouseNumber);
            hashCode.Add(RoadSection);
            hashCode.Add(PointType);
            hashCode.Add(FullAddressEAAB);
            hashCode.Add(FullAddressCadastre);
            hashCode.Add(FullAddressOld);
            hashCode.Add(Longitud);
            hashCode.Add(Latitud);
            hashCode.Add(HydraulicDistrictCode);
            hashCode.Add(HydraulicDistrictDescription);
            hashCode.Add(ZoneCode);
            hashCode.Add(ZoneDesc);
            hashCode.Add(SanitaryUgaCode);
            hashCode.Add(SanitaryUgaDesc);
            hashCode.Add(StormUgaCode);
            hashCode.Add(StormUgaDesc);
            hashCode.Add(GridH3Index);
            hashCode.Add(ZipCode);
            return hashCode.ToHashCode();
        }
    }
}
