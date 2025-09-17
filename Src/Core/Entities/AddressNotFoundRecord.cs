namespace EAABAddIn.Src.Core.Entities
{
    public readonly struct AddressNotFoundRecord
    {
        public AddressNotFoundRecord(string identificador, string direccion, string poblacion, string fullAddressEaab, string fullAddressUacd, string geocoder, double? score)
        {
            Identificador = identificador;
            Direccion = direccion;
            Poblacion = poblacion;
            FullAddressEaab = fullAddressEaab;
            FullAddressUacd = fullAddressUacd;
            Geocoder = geocoder;
            Score = score;
        }

        public string Identificador { get; }
        public string Direccion { get; }
        public string Poblacion { get; }
        public string FullAddressEaab { get; }
        public string FullAddressUacd { get; }
        public string Geocoder { get; }
        public double? Score { get; }
    }
}
