using ArcGIS.Core.Data;
using System.Threading.Tasks;

namespace EAABAddIn.Src.Core.Data
{
    public interface IDatabaseConnectionService
    {
        Task<bool> TestConnectionAsync(DatabaseConnectionProperties connectionProperties);
    }
}
