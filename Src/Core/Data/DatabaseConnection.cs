using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

using EAABAddIn.Src.Core.Data.Common;

using Microsoft.EntityFrameworkCore;

namespace EAABAddIn.Src.Core.Data;

/// <summary>
/// Clase que maneja la conexión con la base de datos usando el patrón Strategy
/// </summary>
public class DatabaseConnection : IDisposable
{
    private readonly IDatabaseStrategy _databaseStrategy;
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Constructor que inicializa una nueva instancia de DatabaseConnection
    /// </summary>
    /// <param name="databaseStrategy">Estrategia de base de datos a utilizar</param>
    public DatabaseConnection(IDatabaseStrategy databaseStrategy)
    {
        _databaseStrategy = databaseStrategy ?? throw new ArgumentNullException(nameof(databaseStrategy));
        _dbContext = new DatabaseContext(_databaseStrategy);
    }

    /// <summary>
    /// Verifica si la conexión a la base de datos es válida
    /// </summary>
    /// <returns>True si la conexión es válida, de lo contrario False</returns>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync();
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Ejecuta una consulta SQL que no retorna resultados (INSERT, UPDATE, DELETE)
    /// </summary>
    /// <param name="sql">Consulta SQL a ejecutar</param>
    /// <param name="parameters">Parámetros de la consulta</param>
    /// <returns>Número de filas afectadas</returns>
    public async Task<int> ExecuteNonQueryAsync(string sql, params object[] parameters)
    {
        try
        {
            return await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters);
        }
        catch (Exception ex)
        {
            // Registrar la excepción o manejarla según sea necesario
            throw new Exception($"Error al ejecutar consulta: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Ejecuta una consulta SQL y retorna un único valor
    /// </summary>
    /// <typeparam name="T">Tipo de dato esperado</typeparam>
    /// <param name="sql">Consulta SQL a ejecutar</param>
    /// <param name="parameters">Parámetros de la consulta</param>
    /// <returns>El primer valor de la primera fila del resultado</returns>
    public async Task<T> ExecuteScalarAsync<T>(string sql, params object[] parameters)
    {
        try
        {
            using var command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;

            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = command.CreateParameter();
                    param.ParameterName = $"@p{i}";
                    param.Value = parameters[i] ?? DBNull.Value;
                    command.Parameters.Add(param);
                }
            }

            if (command.Connection.State != ConnectionState.Open)
            {
                await command.Connection.OpenAsync();
            }

            var result = await command.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result, typeof(T));
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al ejecutar consulta escalar: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Ejecuta una consulta SQL y retorna un DbDataReader
    /// </summary>
    /// <param name="sql">Consulta SQL a ejecutar</param>
    /// <param name="parameters">Parámetros de la consulta</param>
    /// <returns>DbDataReader con los resultados</returns>
    public async Task<DbDataReader> ExecuteReaderAsync(string sql, params object[] parameters)
    {
        try
        {
            var command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;

            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = command.CreateParameter();
                    param.ParameterName = $"@p{i}";
                    param.Value = parameters[i] ?? DBNull.Value;
                    command.Parameters.Add(param);
                }
            }

            if (command.Connection.State != ConnectionState.Open)
            {
                await command.Connection.OpenAsync();
            }

            // CommandBehavior.CloseConnection asegura que la conexión se cierre cuando se cierre el DataReader
            return await command.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al ejecutar consulta reader: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Ejecuta una consulta SQL y retorna un DataTable
    /// </summary>
    /// <param name="sql">Consulta SQL a ejecutar</param>
    /// <param name="parameters">Parámetros de la consulta</param>
    /// <returns>DataTable con los resultados</returns>
    public async Task<DataTable> ExecuteDataTableAsync(string sql, params object[] parameters)
    {
        try
        {
            var dataTable = new DataTable();
            using (var reader = await ExecuteReaderAsync(sql, parameters))
            {
                dataTable.Load(reader);
            }
            return dataTable;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al ejecutar consulta para DataTable: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Ejecuta una consulta SQL utilizando LINQ en Entity Framework
    /// </summary>
    /// <typeparam name="T">Tipo de entidad a consultar</typeparam>
    /// <param name="sqlQuery">Consulta SQL en formato de cadena</param>
    /// <param name="parameters">Parámetros para la consulta</param>
    /// <returns>Lista de entidades del tipo especificado</returns>
    public async Task<List<T>> ExecuteQueryAsync<T>(string sqlQuery, params object[] parameters) where T : class
    {
        return await _dbContext.Set<T>().FromSqlRaw(sqlQuery, parameters).ToListAsync();
    }

    /// <summary>
    /// Crea y devuelve un objeto DbParameter genérico
    /// </summary>
    /// <param name="name">Nombre del parámetro</param>
    /// <param name="value">Valor del parámetro</param>
    /// <returns>Un nuevo DbParameter</returns>
    public DbParameter CreateParameter(string name, object value)
    {
        var parameter = _dbContext.Database.GetDbConnection().CreateCommand().CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    /// <summary>
    /// Libera los recursos utilizados por el contexto de base de datos
    /// </summary>
    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

