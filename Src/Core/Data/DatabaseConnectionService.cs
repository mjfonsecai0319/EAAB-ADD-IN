using System;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace EAABAddIn.Src.Core.Data;

public class DatabaseConnectionService : IDatabaseConnectionService
{
    private static Geodatabase _geodatabase;

    public Geodatabase Geodatabase
    {
        get
        {
            if (_geodatabase == null)
            {
                throw new InvalidOperationException("La conexión a la base de datos no ha sido inicializada.");
            }
            return _geodatabase;
        }
    }

    public async Task<bool> TestConnectionAsync(DatabaseConnectionProperties props)
    {
        try
        {
            return await QueuedTask.Run(() =>
            {
                using (var test = new Geodatabase(props))
                {
                    return true;
                }
            });
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<Geodatabase> CreateConnectionAsync(DatabaseConnectionProperties props)
    {
        if (_geodatabase is not null)
            return _geodatabase;

        _geodatabase = await QueuedTask.Run(() => new Geodatabase(props));
        return _geodatabase;
    }

    public async Task DisposeConnectionAsync()
    {
        if (_geodatabase == null)
            return;

        await QueuedTask.Run(() =>
        {
            _geodatabase?.Dispose();
            _geodatabase = null;
        });
    }
}

