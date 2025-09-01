using System.Data;

namespace EAABAddIn.Src.Core.Data
{
    public interface IDatabaseStrategy
    {
        IDbConnection GetConnection(string connectionString);
    }
}
