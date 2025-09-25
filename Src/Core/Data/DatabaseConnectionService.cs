using System;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace EAABAddIn.Src.Core.Data
{
    public class DatabaseConnectionService : IDatabaseConnectionService
    {
        private static Geodatabase _geodatabase;

        public Geodatabase Geodatabase
        {
            get
            {
                if (_geodatabase == null)
                    throw new InvalidOperationException("La conexión a la base de datos no ha sido inicializada.");
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
            catch
            {
                return false;
            }
        }

        public async Task<Geodatabase> CreateConnectionAsync(DatabaseConnectionProperties props)
        {
            if (_geodatabase != null)
                return _geodatabase;

            _geodatabase = await QueuedTask.Run(() => new Geodatabase(props));
            return _geodatabase;
        }

        /// <summary>
        /// Crea la conexión usando un archivo .sde (ruta directa)
        /// </summary>
        public async Task<Geodatabase> CreateConnectionAsync(string sdePath)
        {
            if (string.IsNullOrWhiteSpace(sdePath))
                throw new ArgumentException("Ruta SDE vacía", nameof(sdePath));
            if (!System.IO.File.Exists(sdePath))
                throw new System.IO.FileNotFoundException($"Archivo SDE no encontrado: {sdePath}", sdePath);

            if (_geodatabase != null)
                return _geodatabase;
            _geodatabase = await QueuedTask.Run(() =>
            {
                try
                {
                    var gdType = typeof(Geodatabase);

                    // (1) Constructor directo string
                    var ctorString = gdType.GetConstructor(new[] { typeof(string) });
                    if (ctorString != null)
                    {
                        try
                        {
                            return (Geodatabase)ctorString.Invoke(new object[] { sdePath });
                        }
                        catch (System.Reflection.TargetInvocationException tex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Falló ctor(string): {tex.InnerException?.Message ?? tex.Message}");
                        }
                    }

                    // (2) DatabaseConnectionFile (ArcGIS Pro >= algunas versiones)
                    var asm = gdType.Assembly;
                    var dbConnFileType = asm.GetType("ArcGIS.Core.Data.DatabaseConnectionFile");
                    if (dbConnFileType != null)
                    {
                        // a) ctor(Uri)
                        var uriCtor = dbConnFileType.GetConstructor(new[] { typeof(Uri) });
                        var geoCtorCF = gdType.GetConstructor(new[] { dbConnFileType });
                        if (uriCtor != null && geoCtorCF != null)
                        {
                            try
                            {
                                var connObj = uriCtor.Invoke(new object[] { new Uri(sdePath) });
                                return (Geodatabase)geoCtorCF.Invoke(new[] { connObj });
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Falló DatabaseConnectionFile(Uri): {ex.Message}");
                            }
                        }

                        // b) métodos estáticos FromFile / FromPath
                        var staticFactory = dbConnFileType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                            .FirstOrDefault(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
                        if (staticFactory != null && geoCtorCF != null)
                        {
                            try
                            {
                                var connObj = staticFactory.Invoke(null, new object[] { sdePath });
                                return (Geodatabase)geoCtorCF.Invoke(new[] { connObj });
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Falló factory estática DatabaseConnectionFile: {ex.Message}");
                            }
                        }
                    }

                    // (3) Cualquier ctor con 1 parámetro que contenga 'ConnectionFile'
                    var altCtor = gdType.GetConstructors().FirstOrDefault(c =>
                    {
                        var ps = c.GetParameters();
                        return ps.Length == 1 && ps[0].ParameterType.Name.Contains("ConnectionFile", StringComparison.OrdinalIgnoreCase);
                    });
                    if (altCtor != null)
                    {
                        var pType = altCtor.GetParameters()[0].ParameterType;
                        object connObj = null;
                        // método estático
                        var fm = pType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                            .FirstOrDefault(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
                        if (fm != null)
                        {
                            try
                            {
                                connObj = fm.Invoke(null, new object[] { sdePath });
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Falló método estático genérico ConnectionFile: {ex.Message}");
                            }
                        }
                        if (connObj == null)
                        {
                            var ctorCFStr = pType.GetConstructor(new[] { typeof(string) });
                            if (ctorCFStr != null)
                            {
                                try
                                {
                                    connObj = ctorCFStr.Invoke(new object[] { sdePath });
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Falló ctor(string) ConnectionFile alt: {ex.Message}");
                                }
                            }
                        }
                        if (connObj != null)
                        {
                            try
                            {
                                return (Geodatabase)altCtor.Invoke(new[] { connObj });
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Falló invocación altCtor: {ex.Message}");
                            }
                        }
                    }

                    throw new NotSupportedException("No se pudo abrir el archivo SDE con los constructores disponibles del SDK.");
                }
                catch (System.Reflection.TargetInvocationException tex)
                {
                    var innerMsg = tex.InnerException?.Message ?? tex.Message;
                    throw new InvalidOperationException($"Error interno al abrir SDE: {innerMsg}", tex.InnerException ?? tex);
                }
                catch
                {
                    throw;
                }
            });
            return _geodatabase;
        }

        public async Task DisposeConnectionAsync()
        {
            if (_geodatabase == null) return;

            await QueuedTask.Run(() =>
            {
                _geodatabase?.Dispose();
                _geodatabase = null;
            });
        }
    }
}
