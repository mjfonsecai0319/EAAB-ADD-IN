using System;
using System.IO;
using System.Text.Json;

namespace EAABAddIn
{
    internal class Settings
    {
        public string motor { get; set; }
        public string usuario { get; set; }
        public string contraseña { get; set; }
        public string host { get; set; }
        public string puerto { get; set; }
        public string baseDeDatos { get; set; } = string.Empty;
        public string rutaArchivoGdb { get; set; }


        // Ruta del archivo de configuración
        private static string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EAABAddIn", "settings.json");

        public void Save()
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            File.WriteAllText(configPath, json);
        }

        public static Settings Load()
        {
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<Settings>(json);
            }
            return new Settings(); // Si no existe, devuelve valores vacíos
        }
    }
}
