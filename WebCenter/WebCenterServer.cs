using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.Persistence;
using BotArena.Persistence.Models;

namespace BotArena.WebCenter;

public sealed class WebCenterServer : IDisposable
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly string[] Statuses = ["new", "seen", "done", "rejected"];
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockTime = TimeSpan.FromMinutes(1), SessionIdle = TimeSpan.FromHours(2), SessionMax = TimeSpan.FromHours(12);

    private readonly WebCenterSettings settings;
    private readonly WebCenterSnapshot snapshot;
    private readonly ChatLog chat;
    private readonly Storage storage;
    private readonly Action<string> log;
    private readonly byte[] passwordHash;
    private readonly ConcurrentDictionary<string, (DateTime Created, DateTime Seen)> sessions = new();
    private readonly ConcurrentDictionary<string, (int Fails, DateTime LockedUntil)> failures = new();
    private readonly CancellationTokenSource stop = new();
    private HttpListener? listener;

    public WebCenterServer(WebCenterSettings settings, WebCenterSnapshot snapshot, ChatLog chat, Storage storage, Action<string> log)
    {
        this.settings = settings;
        this.snapshot = snapshot;
        this.chat = chat;
        this.storage = storage;
        this.log = log;
        passwordHash = SHA256.HashData(Encoding.UTF8.GetBytes(settings.Password));
    }

    public string? Address { get; private set; }

    public bool Start()
    {
        string[] hosts = settings.AllInterfaces ? ["+", "localhost"] : ["localhost"];
        foreach (string host in hosts)
        {
            var candidate = new HttpListener();
            candidate.Prefixes.Add($"http://{host}:{settings.Port}/");
            try
            {
                candidate.Start();
                listener = candidate;
                Address = $"http://{(host == "+" ? "<adres-serwera>" : host)}:{settings.Port}/";
                break;
            }
            catch (HttpListenerException error)
            {
                candidate.Close();
                log(host == "+"
                    ? $"[WEB] nie mozna nasluchiwac na wszystkich adresach ({error.Message}); jako administrator: netsh http add urlacl url=http://+:{settings.Port}/ user=Wszyscy. Probuje localhost."
                    : $"[WEB] nie mozna uruchomic Web Center na porcie {settings.Port}: {error.Message}");
            }
        }
        if (listener is null) return false;
        _ = Task.Run(AcceptLoop);
        log($"[WEB] Web Center dziala: {Address} (haslo w {WebCenterSettings.FilePath})");
        return true;
    }

    private async Task AcceptLoop()
    {
        while (!stop.IsCancellationRequested && listener is { IsListening: true })
        {
            try
            {
                HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => Handle(context));
            }
            catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { break; }
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        try { listener?.Stop(); listener?.Close(); } catch (Exception) {  }
    }

    private async Task Handle(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;
        try
        {
            response.Headers["X-Content-Type-Options"] = "nosniff";
            response.Headers["X-Frame-Options"] = "DENY";
            response.Headers["Referrer-Policy"] = "no-referrer";
            response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'";
            string path = request.Url?.AbsolutePath ?? "/";
            string method = request.HttpMethod;

            if (method == "GET" && path is "/" or "/index.html") { await Resource(response, "index.html", "text/html; charset=utf-8"); return; }
            if (method == "GET" && path == "/app.js") { await Resource(response, "app.js", "text/javascript; charset=utf-8"); return; }
            if (method == "GET" && path == "/style.css") { await Resource(response, "style.css", "text/css; charset=utf-8"); return; }
            if (method == "GET" && path is "/logo.svg" or "/emblem.svg") { await Resource(response, path[1..], "image/svg+xml"); return; }
            if (method == "POST" && path == "/api/login") { await Login(context); return; }

            response.Headers["Cache-Control"] = "no-store";
            if (!path.StartsWith("/api/", StringComparison.Ordinal)) { await Text(response, 404, "Nie znaleziono"); return; }
            if (!Authorized(request, out string token)) { await Json(response, 401, new { error = "login" }); return; }
            if (method == "POST" && request.Headers["X-Requested-With"] is not { Length: > 0 }) { await Json(response, 403, new { error = "forbidden" }); return; }

            switch (method, path)
            {
                case ("GET", "/api/state"): await Raw(response, 200, snapshot.Json, "application/json; charset=utf-8"); break;
                case ("GET", "/api/chat"): await Chat(request, response); break;
                case ("GET", "/api/reports"): await Reports(request, response); break;
                case ("GET", "/api/reports.csv"): await ReportsCsv(response); break;
                case ("POST", "/api/reports/status"): await ReportStatus(request, response); break;
                case ("POST", "/api/logout"):
                    sessions.TryRemove(token, out _);
                    response.Headers["Set-Cookie"] = "bc=; Path=/; HttpOnly; SameSite=Strict; Max-Age=0";
                    await Json(response, 200, new { ok = true });
                    break;
                default: await Text(response, 404, "Nie znaleziono"); break;
            }
        }
        catch (Exception error)
        {
            log($"[WEB] blad zadania {request.Url?.AbsolutePath}: {error.GetBaseException().Message}");
            try { await Json(response, 500, new { error = "server" }); } catch (Exception) {  }
        }
        finally
        {
            try { response.Close(); } catch (Exception) {  }
        }
    }

    private async Task Login(HttpListenerContext context)
    {
        string address = ClientAddress(context.Request);
        if (failures.TryGetValue(address, out var known) && DateTime.UtcNow < known.LockedUntil)
        {
            await Json(context.Response, 429, new { error = "locked", seconds = (int)Math.Ceiling((known.LockedUntil - DateTime.UtcNow).TotalSeconds) });
            return;
        }
        string password = "";
        try
        {
            using JsonDocument body = JsonDocument.Parse(await ReadBody(context.Request, 2048));
            if (body.RootElement.TryGetProperty("password", out JsonElement value) && value.ValueKind == JsonValueKind.String) password = value.GetString() ?? "";
        }
        catch (JsonException) {  }

        bool ok = CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(password)), passwordHash);
        if (!ok)
        {
            (int fails, DateTime until) = failures.GetValueOrDefault(address);
            fails++;
            if (fails >= MaxFailures) { until = DateTime.UtcNow + LockTime; fails = 0; log($"[WEB] zablokowano {address} na minute (bledne hasla)."); }
            failures[address] = (fails, until);
            await Task.Delay(400);
            await Json(context.Response, 401, new { error = "password" });
            return;
        }
        failures.TryRemove(address, out _);
        foreach (var (key, session) in sessions) if (Expired(session)) sessions.TryRemove(key, out _);
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        sessions[token] = (DateTime.UtcNow, DateTime.UtcNow);
        context.Response.Headers["Set-Cookie"] = $"bc={token}; Path=/; HttpOnly; SameSite=Strict; Max-Age={(int)SessionMax.TotalSeconds}{(settings.TrustProxy ? "; Secure" : "")}";
        log($"[WEB] zalogowano z {address}.");
        await Json(context.Response, 200, new { ok = true });
    }

    private string ClientAddress(HttpListenerRequest request)
    {
        if (settings.TrustProxy && request.Headers["X-Real-IP"] is { Length: > 0 and <= 64 } real && IPAddress.TryParse(real, out IPAddress? parsed)) return parsed.ToString();
        return request.RemoteEndPoint?.Address.ToString() ?? "?";
    }

    private static bool Expired((DateTime Created, DateTime Seen) session) =>
        DateTime.UtcNow - session.Created > SessionMax || DateTime.UtcNow - session.Seen > SessionIdle;

    private bool Authorized(HttpListenerRequest request, out string token)
    {
        token = request.Cookies["bc"]?.Value ?? "";
        if (token.Length != 64 || !sessions.TryGetValue(token, out var session)) return false;
        if (Expired(session)) { sessions.TryRemove(token, out _); return false; }
        sessions[token] = (session.Created, DateTime.UtcNow);
        return true;
    }

    private async Task Chat(HttpListenerRequest request, HttpListenerResponse response)
    {
        long after = long.TryParse(request.QueryString["after"], out long parsed) ? parsed : 0;
        int? match = int.TryParse(request.QueryString["match"], out int id) ? id : null;
        int limit = int.TryParse(request.QueryString["limit"], out int asked) ? Math.Clamp(asked, 1, 500) : 200;
        IReadOnlyList<ChatEntry> entries = chat.Since(after, match, limit);
        await Json(response, 200, new
        {
            last = chat.LastId,
            entries = entries.Select(e => new { id = e.Id, at = e.At.ToString("HH:mm:ss"), day = e.At.ToString("yyyy-MM-dd"), match = e.MatchId, title = e.Title, player = e.Player, text = e.Text, system = e.System }),
        });
    }

    private async Task Reports(HttpListenerRequest request, HttpListenerResponse response)
    {
        string? status = request.QueryString["status"] is { Length: > 0 } s && Statuses.Contains(s) ? s : null;
        int limit = int.TryParse(request.QueryString["limit"], out int asked) ? Math.Clamp(asked, 1, 500) : 200;
        await storage.Ready.ConfigureAwait(false);
        IReadOnlyList<FeedbackRecord> list = await storage.Feedback.ListAsync(limit, status).ConfigureAwait(false);
        await Json(response, 200, new { reports = list.Select(Describe), persistent = storage.Persistent });
    }

    private static object Describe(FeedbackRecord r) => new
    {
        id = r.Id, at = r.CreatedAt.ToString("yyyy-MM-dd HH:mm"), account = r.Account, player = r.PlayerName, kind = r.Kind, priority = r.Priority,
        title = r.Title, message = r.Message, context = r.Context, status = r.Status,
    };

    private async Task ReportsCsv(HttpListenerResponse response)
    {
        await storage.Ready.ConfigureAwait(false);
        IReadOnlyList<FeedbackRecord> list = await storage.Feedback.ListAsync(5000).ConfigureAwait(false);
        var csv = new StringBuilder("﻿id;data;konto;gracz;rodzaj;priorytet;status;kontekst;tytul;tresc\r\n");
        foreach (FeedbackRecord r in list)
            csv.Append(string.Join(';', new[] { r.Id.ToString(), r.CreatedAt.ToString("yyyy-MM-dd HH:mm"), r.Account, r.PlayerName, r.Kind, r.Priority.ToString(), r.Status, r.Context, r.Title, r.Message }.Select(Cell))).Append("\r\n");
        response.Headers["Content-Disposition"] = $"attachment; filename=\"zgloszenia-{DateTime.Now:yyyyMMdd-HHmm}.csv\"";
        await Raw(response, 200, csv.ToString(), "text/csv; charset=utf-8");
    }

    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private async Task ReportStatus(HttpListenerRequest request, HttpListenerResponse response)
    {
        long id = 0;
        string status = "";
        try
        {
            using JsonDocument body = JsonDocument.Parse(await ReadBody(request, 1024));
            if (body.RootElement.TryGetProperty("id", out JsonElement idValue) && idValue.TryGetInt64(out long parsed)) id = parsed;
            if (body.RootElement.TryGetProperty("status", out JsonElement statusValue) && statusValue.ValueKind == JsonValueKind.String) status = statusValue.GetString() ?? "";
        }
        catch (JsonException) {  }
        if (id <= 0 || !Statuses.Contains(status)) { await Json(response, 400, new { error = "bad" }); return; }
        await storage.Ready.ConfigureAwait(false);
        bool found = await storage.Feedback.SetStatusAsync(id, status).ConfigureAwait(false);
        log($"[WEB] zgloszenie #{id}: status {status}");
        await Json(response, found ? 200 : 404, new { ok = found });
    }

    private static async Task<string> ReadBody(HttpListenerRequest request, int maxBytes)
    {
        if (request.ContentLength64 > maxBytes) throw new JsonException("body too large");
        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        var buffer = new char[maxBytes];
        int read = await reader.ReadBlockAsync(buffer, 0, maxBytes).ConfigureAwait(false);
        return new string(buffer, 0, read);
    }

    private static Task Json(HttpListenerResponse response, int status, object value) =>
        Raw(response, status, JsonSerializer.Serialize(value, Options), "application/json; charset=utf-8");

    private static Task Text(HttpListenerResponse response, int status, string text) => Raw(response, status, text, "text/plain; charset=utf-8");

    private static async Task Raw(HttpListenerResponse response, int status, string body, string contentType)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static readonly Dictionary<string, byte[]> resources = [];

    private static async Task Resource(HttpListenerResponse response, string name, string contentType)
    {
        byte[]? bytes;
        lock (resources)
        {
            if (!resources.TryGetValue(name, out bytes))
            {
                using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("webcenter/" + name);
                if (stream is null) bytes = null;
                else
                {
                    using var copy = new MemoryStream();
                    stream.CopyTo(copy);
                    resources[name] = bytes = copy.ToArray();
                }
            }
        }
        if (bytes is null) { await Text(response, 404, "Brak zasobu " + name); return; }
        response.Headers["Cache-Control"] = "no-cache";
        response.StatusCode = 200;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }
}
