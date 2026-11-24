using MySqlConnector;

namespace BotArena.Persistence;

public static class DatabaseSettings
{
    public static string? ConnectionString(string envFile = "config/mysql.env")
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(envFile))
            foreach (string line in File.ReadAllLines(envFile))
            {
                string trimmed = line.Trim();
                int equals = trimmed.IndexOf('=');
                if (trimmed.Length == 0 || trimmed.StartsWith('#') || equals <= 0) continue;
                values[trimmed[..equals].Trim()] = trimmed[(equals + 1)..].Trim().Trim('"');
            }
        foreach (string key in new[] { "MYSQL_HOST", "MYSQL_PORT", "MYSQL_USER", "MYSQL_PASSWORD", "MYSQL_DATABASE", "MYSQL_POOLING" })
            if (Environment.GetEnvironmentVariable(key) is { Length: > 0 } value) values[key] = value;
        if (!values.TryGetValue("MYSQL_USER", out string? user) || !values.TryGetValue("MYSQL_DATABASE", out string? database)) return null;
        return new MySqlConnectionStringBuilder
        {
            Server = values.GetValueOrDefault("MYSQL_HOST", "127.0.0.1"),
            Port = uint.Parse(values.GetValueOrDefault("MYSQL_PORT", "3306")),
            UserID = user,
            Password = values.GetValueOrDefault("MYSQL_PASSWORD", ""),
            Database = database,
            ConnectionTimeout = 5,
            CharacterSet = "utf8mb4",
            Pooling = !string.Equals(values.GetValueOrDefault("MYSQL_POOLING", "true"), "false", StringComparison.OrdinalIgnoreCase),
        }.ConnectionString;
    }
}
