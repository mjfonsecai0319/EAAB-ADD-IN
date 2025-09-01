using System;
using System.Threading.Tasks;

using ArcGIS.Core.Data;

namespace EAABAddIn.Src.Core.Data
{
    public class DatabaseConnectionService : IDatabaseConnectionService
    {
        public async Task<bool> TestConnectionAsync(DatabaseConnectionProperties connectionProperties)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (new Geodatabase(connectionProperties))
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }
    }
}
