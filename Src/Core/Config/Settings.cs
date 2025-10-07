using System;
using System.IO;
using System.Text.Json;

namespace EAABAddIn
{
    internal class Settings
    {
        public string motor { get; set; } = string.Empty;
        public string usuario { get; set; } = string.Empty;
        public string contraseña { get; set; } = string.Empty;
        public string host { get; set; } = string.Empty;
        public string puerto { get; set; } = "5432"; // Puerto por defecto para PostgreSQL
        public string baseDeDatos { get; set; } = string.Empty;
        public string rutaArchivoGdb { get; set; } = string.Empty;
        public string rutaArchivoCredenciales { get; set; } = string.Empty; // Para archivos .SDE
        public bool permitirTresPuntos { get; set; } = false; // Permitir polígonos de solo 3 vértices

        // Ruta del archivo de configuración
        private static readonly string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EAABAddIn", "settings.json");

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? string.Empty);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                // Log o manejar error según sea necesario
                System.Diagnostics.Debug.WriteLine($"Error al guardar configuración: {ex.Message}");
                throw;
            }
        }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var settings = JsonSerializer.Deserialize<Settings>(json);
                    
                    // Asegurar que las propiedades no sean null
                    settings ??= new Settings();
                    settings.motor ??= string.Empty;
                    settings.usuario ??= string.Empty;
                    settings.contraseña ??= string.Empty;
                    settings.host ??= string.Empty;
                    settings.puerto ??= "5432";
                    settings.baseDeDatos ??= string.Empty;
                    settings.rutaArchivoGdb ??= string.Empty;
                    settings.rutaArchivoCredenciales ??= string.Empty;
                    // Nueva propiedad booleana
                    // Si es null (versiones anteriores del archivo) se mantiene false por defecto
                    // JsonSerializer asignará false si no existe la clave
                    
                    return settings;
                }
            }
            catch (Exception ex)
            {
                // Log o manejar error según sea necesario
                System.Diagnostics.Debug.WriteLine($"Error al cargar configuración: {ex.Message}");
            }
            
            return new Settings(); // Si no existe o hay error, devuelve valores vacíos
        }

        /// <summary>
        /// Valida que la configuración actual sea válida según el motor seleccionado
        /// </summary>
        /// <returns>True si la configuración es válida</returns>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(motor))
                return false;

            if (motor == "Oracle SDE" || motor == "Oracle (Archivo de credenciales)") // compatibilidad retro
            {
                return !string.IsNullOrWhiteSpace(rutaArchivoCredenciales) && 
                       File.Exists(rutaArchivoCredenciales);
            }
            else
            {
                return !string.IsNullOrWhiteSpace(host) &&
                       !string.IsNullOrWhiteSpace(usuario) &&
                       !string.IsNullOrWhiteSpace(contraseña) &&
                       !string.IsNullOrWhiteSpace(baseDeDatos) &&
                       !string.IsNullOrWhiteSpace(puerto);
            }
        }
    }
}