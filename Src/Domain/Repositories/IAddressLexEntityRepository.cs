using ArcGIS.Core.Data;
using EAABAddIn.Src.Core.Entities;

namespace EAABAddIn.Src.Domain.Repositories
{
    public interface IAddressLexEntityRepository
    {
        AddressLexEntity FindByWord(DatabaseConnectionProperties connectionProperties, string word);
    }
}
