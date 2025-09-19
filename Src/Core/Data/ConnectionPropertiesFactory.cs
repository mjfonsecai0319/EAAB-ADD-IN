using ArcGIS.Core.Data;

namespace EAABAddIn.Src.Core.Data
{
    internal static class ConnectionPropertiesFactory
    {
        public static DatabaseConnectionProperties CreatePostgresConnection(
            string host, string user, string password, string database, string port)
        {
            return new DatabaseConnectionProperties(EnterpriseDatabaseType.PostgreSQL)
            {
                AuthenticationMode = AuthenticationMode.DBMS,
                User = user,
                Password = password,
                Database = database,
                Instance = $"{host},{port}" // ✅ corregido
            };
        }

        public static DatabaseConnectionProperties CreatePostgresConnection(
            string host, string user, string password, string database)
        {
            return CreatePostgresConnection(host, user, password, database, "5432");
        }


        public static DatabaseConnectionProperties CreateOracleConnection(
            string host, string user, string password, string database, string port)
        {
            return new DatabaseConnectionProperties(EnterpriseDatabaseType.Oracle)
            {
                AuthenticationMode = AuthenticationMode.DBMS,
                User = user,
                Password = password,
                Database = database,
                Instance = $"{host}:{port}/{database}"
            };
        }

        public static DatabaseConnectionProperties CreateOracleConnection(
            string host, string user, string password, string database)
        {
            return CreateOracleConnection(host, user, password, database, "1521");
        }
    }
}
