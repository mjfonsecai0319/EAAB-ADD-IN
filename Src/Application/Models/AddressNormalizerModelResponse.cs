namespace EAABAddIn.Src.Application.Models
{
    public class AddressNormalizerModelResponse : AddressNormalizerModel
    {
        public string AddressNormalizer { get; set; }
        public string Principal { get; set; }
        public string Generador { get; set; }
        public string Plate { get; set; }
        public string Complement { get; set; }
        public string CardinalidadPrincipal { get; set; }
        public string CardinalidadGenerador { get; set; }

        public static AddressNormalizerModelResponse FromAddressNormalizer(AddressNormalizerModel model)
        {
            return new AddressNormalizerModelResponse { Address = model.Address };
        }
    }
}
