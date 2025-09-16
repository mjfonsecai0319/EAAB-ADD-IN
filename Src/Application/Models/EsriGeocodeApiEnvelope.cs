using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace EAABAddIn.Src.Application.Models;

public class EsriGeocodeApiEnvelope
{
    [JsonPropertyName("spatialReference")] public EsriSpatialReference SpatialReference { get; set; }
    [JsonPropertyName("candidates")] public List<EsriCandidate> Candidates { get; set; }
}

public class EsriSpatialReference
{
    [JsonPropertyName("wkid")] public int? Wkid { get; set; }
    [JsonPropertyName("latestWkid")] public int? LatestWkid { get; set; }
}

public class EsriCandidate
{
    [JsonPropertyName("address")] public string Address { get; set; }
    [JsonPropertyName("location")] public EsriLocation Location { get; set; }
    [JsonPropertyName("score")] public double? Score { get; set; }
    [JsonPropertyName("attributes")] public Dictionary<string, object> Attributes { get; set; }
    [JsonPropertyName("extent")] public EsriExtent Extent { get; set; }
}

public class EsriLocation
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
}

public class EsriExtent
{
    [JsonPropertyName("xmin")] public double? XMin { get; set; }
    [JsonPropertyName("ymin")] public double? YMin { get; set; }
    [JsonPropertyName("xmax")] public double? XMax { get; set; }
    [JsonPropertyName("ymax")] public double? YMax { get; set; }
}
