using System.Data;

using Oracle.ManagedDataAccess.Client;

namespace EAABAddIn.Src.Core.Data
{
    public class OracleStrategy : IDatabaseStrategy
    {
        public IDbConnection GetConnection(string connectionString)
        {
            return new OracleConnection(connectionString);
        }
    }
}
