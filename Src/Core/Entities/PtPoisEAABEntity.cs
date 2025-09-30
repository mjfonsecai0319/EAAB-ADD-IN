using System;

namespace EAABAddIn.Src.Core.Entities
{
    public class PtPoisEaabEntity
    {
        public long ID { get; set; }
        public string IdSig { get; set; }
        public string TechnicalLocation { get; set; }
        public string PoiType { get; set; }
        public string NamePoi { get; set; }
        public string Address { get; set; }
        public string ZoneCode { get; set; }
        public string ZoneDesc { get; set; }
        public string CityCode { get; set; }
        public string CityDesc { get; set; }
        public string LocalityCode { get; set; }
        public string LocalityDesc { get; set; }
        public string NeighborhoodCode { get; set; }
        public string NeighborhoodDesc { get; set; }
        public string HydraulicDistrictCode { get; set; }
        public string HydraulicDistrictDesc { get; set; }
        public string SanitaryUgaCode { get; set; }
        public string SanitaryUgaDesc { get; set; }
        public string StormUgaCode { get; set; }
        public string StormUgaDesc { get; set; }
        public string GridH3Index { get; set; }
        public string ZipCode { get; set; }
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }
        public string Active { get; set; }

        // Puntuaciones internas de similitud
        public double ScoreJaroWinkler { get; set; }
        public double ScoreLevenshtein { get; set; }
        public double TotalScore => ScoreJaroWinkler + ScoreLevenshtein;
    }
}
