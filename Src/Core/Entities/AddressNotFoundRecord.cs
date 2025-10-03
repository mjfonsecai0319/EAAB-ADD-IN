namespace EAABAddIn.Src.Core.Entities;

public readonly struct AddressNotFoundRecord
{
    public AddressNotFoundRecord(string id, string address, string cityCode, string fullAddressEaab, string fullAddressUacd, string geocoder)
    {
        Id = id;
        Address = address;
        CityCode = cityCode;
        FullAddressEaab = fullAddressEaab;
        FullAddressUacd = fullAddressUacd;
        Geocoder = geocoder;
    }

    public string Id { get; }
    public string Address { get; }
    public string CityCode { get; }
    public string FullAddressEaab { get; }
    public string FullAddressUacd { get; }
    public string Geocoder { get; }
}

