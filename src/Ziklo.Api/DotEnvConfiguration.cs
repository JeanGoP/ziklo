using Microsoft.Extensions.Configuration;

namespace Ziklo.Api;

internal static class DotEnvConfiguration
{
    public static void Load(ConfigurationManager configuration, string contentRootPath)
    {
        string path = Path.Combine(contentRootPath, ".env");
        if (!File.Exists(path)) return;

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (string rawLine in File.ReadLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            int separator = line.IndexOf('=');
            if (separator <= 0)
                throw new InvalidOperationException($"Línea inválida en {path}: se esperaba CLAVE=valor.");

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            if (key.Length == 0)
                throw new InvalidOperationException($"La configuración en {path} contiene una clave vacía.");
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];

            values[key.Replace("__", ":", StringComparison.Ordinal)] = value;
        }

        configuration.AddInMemoryCollection(values);
        // Las variables del servidor prevalecen sobre el archivo local.
        configuration.AddEnvironmentVariables();
    }
}
