using ArcGIS.Core.Data;
using System;

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
                Instance = $"{host},{port}" // Formato original que funcionaba
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

        /// <summary>
        /// Valida y retorna la ruta del archivo SDE para conexión directa
        /// En ArcGIS Pro, los archivos .sde pueden usarse directamente como geodatabase
        /// </summary>
        /// <param name="sdeFilePath">Ruta al archivo .sde</param>
        /// <returns>String con la ruta del archivo validada para usar con Geodatabase constructor</returns>
        public static string CreateOracleConnectionFromFile(string sdeFilePath)
        {
            // Verificar que el archivo existe
            if (!System.IO.File.Exists(sdeFilePath))
            {
                throw new System.IO.FileNotFoundException($"El archivo SDE no existe: {sdeFilePath}");
            }

            // Verificar que tiene la extensión correcta
            if (!sdeFilePath.EndsWith(".sde", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("El archivo debe tener extensión .sde");
            }

            // Los archivos .sde se pueden usar directamente como string path
            // para el constructor de Geodatabase
            return sdeFilePath;
        }
    }
}