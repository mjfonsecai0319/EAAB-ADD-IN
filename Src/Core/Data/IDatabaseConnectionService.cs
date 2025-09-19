using System.Threading.Tasks;
using ArcGIS.Core.Data;

namespace EAABAddIn.Src.Core.Data
{
    public interface IDatabaseConnectionService
    {
        Task<bool> TestConnectionAsync(DatabaseConnectionProperties connectionProperties);
        Task<Geodatabase> CreateConnectionAsync(DatabaseConnectionProperties props);
        Task DisposeConnectionAsync();
    }
}
