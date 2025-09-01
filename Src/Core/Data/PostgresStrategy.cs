using System.Data;

using Npgsql;

namespace EAABAddIn.Src.Core.Data
{
    public class PostgresStrategy : IDatabaseStrategy
    {
        public IDbConnection GetConnection(string connectionString)
        {
            return new NpgsqlConnection(connectionString);
        }
    }
}
