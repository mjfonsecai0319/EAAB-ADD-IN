using ArcGIS.Core.Data;

namespace EAABAddIn.Src.Core.Data
{
    public static class ConnectionPropertiesFactory
    {
        public static DatabaseConnectionProperties CreateOracleConnection(string instance, string user, string password)
        {
            return new DatabaseConnectionProperties(EnterpriseDatabaseType.Oracle)
            {
                AuthenticationMode = AuthenticationMode.DBMS,
                Instance = instance,
                User = user,
                Password = password
            };
        }

        public static DatabaseConnectionProperties CreatePostgresConnection(string instance, string user, string password, string database)
        {
            return new DatabaseConnectionProperties(EnterpriseDatabaseType.PostgreSQL)
            {
                AuthenticationMode = AuthenticationMode.DBMS,
                Instance = instance,
                User = user,
                Password = password,
                Database = database
            };
        }
    }
}
