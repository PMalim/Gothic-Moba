using System.Security.Cryptography;

namespace BotArena.WebCenter;

public sealed record WebCenterSettings(string Password, int Port, bool AllInterfaces, bool TrustProxy = false)
{
    public const string FilePath = "config/webcenter.env";
    public const int DefaultPort = 8088;

    public static WebCenterSettings? Load(Action<string> log)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(FilePath) && Environment.GetEnvironmentVariable("WEBCENTER_PASSWORD") is not { Length: > 0 })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                string generated = RandomNumberGenerator.GetString("abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789", 16);
                File.WriteAllText(FilePath,
                    "# BotArena Web Center: strona do obserwowania gier, czatu, zgloszen i zasobow serwera.\n" +
                    "# PASSWORD - haslo do strony (puste = Web Center wylaczony). Wygenerowane losowo; zmien, jesli chcesz.\n" +
                    "# PORT     - port HTTP.\n" +
                    "# BIND     - localhost (tylko ten komputer) albo all (z sieci; wymaga zgody Windows: patrz docs/webcenter.md).\n" +
                    $"PASSWORD={generated}\nPORT={DefaultPort}\nBIND=localhost\n");
                log($"[WEB] utworzono {FilePath} z losowym haslem (otworz plik, zeby je zobaczyc).");
            }
            foreach (string line in File.Exists(FilePath) ? File.ReadAllLines(FilePath) : [])
            {
                string trimmed = line.Trim();
                int equals = trimmed.IndexOf('=');
                if (trimmed.Length == 0 || trimmed.StartsWith('#') || equals <= 0) continue;
                values[trimmed[..equals].Trim()] = trimmed[(equals + 1)..].Trim().Trim('"');
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            log($"[WEB] nie mozna odczytac {FilePath}: {error.Message}");
        }
        foreach (string key in new[] { "PASSWORD", "PORT", "BIND", "PROXY" })
            if (Environment.GetEnvironmentVariable("WEBCENTER_" + key) is { Length: > 0 } value) values[key] = value;

        string password = values.GetValueOrDefault("PASSWORD", "");
        if (password.Length == 0) { log("[WEB] Web Center wylaczony (PASSWORD jest pusty)."); return null; }
        if (password.Length < 8) log("[WEB] UWAGA: haslo Web Center jest krotsze niz 8 znakow.");
        int port = int.TryParse(values.GetValueOrDefault("PORT", ""), out int parsed) && parsed is > 0 and < 65536 ? parsed : DefaultPort;
        bool all = string.Equals(values.GetValueOrDefault("BIND", "localhost"), "all", StringComparison.OrdinalIgnoreCase);
        bool proxy = values.GetValueOrDefault("PROXY", "false") is "1" or "true" or "True" or "TRUE" or "yes";
        return new WebCenterSettings(password, port, all, proxy);
    }
}
