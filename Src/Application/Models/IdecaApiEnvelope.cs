namespace EAABAddIn.Src.Application.Models;

internal sealed class IdecaApiEnvelope
{
    public IdecaResponse Response { get; set; }
    public bool Status { get; set; }
}

internal sealed class IdecaResponse
{
    public IdecaData Data { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
}

internal sealed class IdecaData
{
    public string Estado { get; set; } // success
    public double? YInput { get; set; }
    public string Lotcodigo { get; set; }
    public string Latitude { get; set; }
    public string Diraprox { get; set; }
    public string Mancodigo { get; set; }
    public string Cpocodigo { get; set; }
    public double? XInput { get; set; }
    public string Codloc { get; set; }
    public string Dirtrad { get; set; }
    public string Nomupz { get; set; }
    public string Localidad { get; set; }
    public string Dirinput { get; set; }
    public string Codupz { get; set; }
    public string Nomseccat { get; set; }
    public string Tipo_Direccion { get; set; }
    public string Codseccat { get; set; }
    public string Longitude { get; set; }
}