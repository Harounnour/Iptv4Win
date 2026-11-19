using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Iptv4Win.Models;

namespace Iptv4Win.Services;

/// <summary>Stalker/Ministra client. Each subscription owns its cookie jar and token.</summary>
public sealed class StalkerClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string[] _loadCandidates;
    private readonly string _mac, _referer, _origin, _serial, _deviceId, _deviceId2;
    private string _activeLoad;
    private string _token = "";
    private CancellationTokenSource? _keepAliveCts;
    private Task? _keepAliveTask;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private static readonly Regex MacPattern = new(@"^([0-9A-F]{2}:){5}[0-9A-F]{2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private const string Ua = "Mozilla/5.0 (QtEmbedded; U; Linux; C) AppleWebKit/533.3 (KHTML, like Gecko) MAG250 stbapp ver: 2 rev: 250 Safari/533.3";

    public string PlaybackReferer => _referer;
    public string PlaybackUserAgent => Ua;
    public string PlaybackCookie => $"mac={_mac}; stb_lang=en; timezone=Europe/Paris" + (_token.Length > 0 ? $"; token={_token}" : "");
    public string ActiveEndpoint => _activeLoad;

    public StalkerClient(PlaylistSource source, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Uri.TryCreate(source.Url.Trim(), UriKind.Absolute, out var portal) ||
            (portal.Scheme != Uri.UriSchemeHttp && portal.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Adresse du portail Stalker invalide.");
        _mac = source.Mac.Trim().ToUpperInvariant();
        if (!MacPattern.IsMatch(_mac))
            throw new InvalidOperationException("Adresse MAC invalide. Format attendu : 00:1A:79:XX:XX:XX");

        _serial = source.SerialNumber.Trim();
        _deviceId = source.DeviceId.Trim();
        _deviceId2 = source.DeviceId2.Trim();

        var path = portal.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/server/load.php", StringComparison.OrdinalIgnoreCase)) path = path[..^17].TrimEnd('/');
        else if (path.EndsWith("/portal.php", StringComparison.OrdinalIgnoreCase)) path = path[..^11].TrimEnd('/');
        else if (path.EndsWith("/c", StringComparison.OrdinalIgnoreCase)) path = path[..^2].TrimEnd('/');

        var baseUri = new UriBuilder(portal) { Path = path.TrimEnd('/') + "/", Query = "", Fragment = "" }.Uri;
        _loadCandidates = new[] { new Uri(baseUri, "server/load.php").ToString(), new Uri(baseUri, "portal.php").ToString() };
        _activeLoad = _loadCandidates[0];
        _referer = new Uri(baseUri, "c/").ToString();
        _origin = baseUri.GetLeftPart(UriPartial.Authority);

        if (httpClient is null)
        {
            var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = true,
                UseCookies = true
            };
            _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
            _ownsHttp = true;
        }
        else { _http = httpClient; _ownsHttp = false; }
    }

    private async Task<JsonElement> GetAsync(string query, CancellationToken ct)
    {
        Exception? last = null;
        foreach (var endpoint in new[] { _activeLoad }.Concat(_loadCandidates.Where(x => x != _activeLoad)).Distinct())
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}?{query}&JsHttpRequest=1-xml");
                req.Headers.TryAddWithoutValidation("User-Agent", Ua);
                req.Headers.TryAddWithoutValidation("X-User-Agent", "Model: MAG250; Link: WiFi");
                req.Headers.TryAddWithoutValidation("Referer", _referer);
                req.Headers.TryAddWithoutValidation("Origin", _origin);
                req.Headers.TryAddWithoutValidation("Accept", "application/json, text/javascript, */*; q=0.01");
                req.Headers.TryAddWithoutValidation("Accept-Language", "fr-FR,fr;q=0.9,en;q=0.8");
                req.Headers.TryAddWithoutValidation("Cookie", PlaybackCookie);
                if (_serial.Length > 0) req.Headers.TryAddWithoutValidation("X-Serial-Number", _serial);
                if (_deviceId.Length > 0) req.Headers.TryAddWithoutValidation("X-Device-ID", _deviceId);
                if (_deviceId2.Length > 0) req.Headers.TryAddWithoutValidation("X-Device-ID2", _deviceId2);
                if (_token.Length > 0) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _token);

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"Portail Stalker : HTTP {(int)resp.StatusCode} ({resp.ReasonPhrase}).", null, resp.StatusCode);
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("js", out var js))
                    throw new InvalidOperationException("Réponse Stalker invalide : champ js absent.");
                _activeLoad = endpoint;
                if (js.ValueKind == JsonValueKind.String)
                {
                    var nested = js.GetString();
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        try { using var nestedDoc = JsonDocument.Parse(nested); return nestedDoc.RootElement.Clone(); }
                        catch (JsonException) { }
                    }
                }
                return js.Clone();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { last = ex; }
        }
        throw new InvalidOperationException("Connexion au portail impossible via server/load.php et portal.php. Vérifiez l'adresse, le réseau et l'accès au portail.", last);
    }

    private static string Str(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return "";
        return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString();
    }

    private async Task HandshakeAsync(CancellationToken ct)
    {
        await _sessionLock.WaitAsync(ct);
        try
        {
            _token = "";
            var js = await GetAsync("type=stb&action=handshake&token=", ct);
            _token = Str(js, "token");
            if (_token.Length == 0)
                throw new InvalidOperationException("Handshake refusé : vérifiez le portail, l'adresse MAC et l'autorisation de l'appareil.");
            // Profile confirms that the issued token is accepted by the portal.
            await GetAsync("type=stb&action=get_profile", ct);
            StartKeepAlive();
        }
        finally { _sessionLock.Release(); }
    }

    private void StartKeepAlive()
    {
        _keepAliveCts?.Cancel();
        _keepAliveCts?.Dispose();
        _keepAliveCts = new CancellationTokenSource();
        var ct = _keepAliveCts.Token;
        _keepAliveTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    try { await GetAsync("type=stb&action=watchdog", ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                    catch { try { await HandshakeAsync(ct); } catch { } }
                }
            }
            catch (OperationCanceledException) { }
        }, ct);
    }

    public async Task<List<Channel>> GetChannelsAsync(CancellationToken ct = default)
    {
        await HandshakeAsync(ct);
        var genres = new Dictionary<string, string>();
        var g = await GetAsync("type=itv&action=get_genres", ct);
        if (g.ValueKind == JsonValueKind.Array)
            foreach (var item in g.EnumerateArray()) genres[Str(item, "id")] = Str(item, "title");

        var all = await GetAsync("type=itv&action=get_all_channels", ct);
        if (!all.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Le portail Stalker n'a pas retourné de liste de chaînes.");
        var list = new List<Channel>(); var n = 1;
        foreach (var ch in data.EnumerateArray())
        {
            genres.TryGetValue(Str(ch, "tv_genre_id"), out var group);
            var logo = Str(ch, "logo");
            list.Add(new Channel
            {
                Number = int.TryParse(Str(ch, "number"), out var num) ? num : n,
                Name = Str(ch, "name"),
                Group = group ?? "",
                LogoUrl = Uri.TryCreate(logo, UriKind.Absolute, out var logoUri) && (logoUri.Scheme == "http" || logoUri.Scheme == "https") ? logo : null,
                StalkerCmd = Str(ch, "cmd")
            });
            n++;
        }
        return list;
    }

    public async Task<string> CreateLinkAsync(string cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd)) throw new InvalidOperationException("Commande Stalker vide.");
        if (_token.Length == 0) await HandshakeAsync(ct);
        JsonElement js;
        try
        {
            js = await GetAsync($"type=itv&action=create_link&cmd={Uri.EscapeDataString(cmd)}&series=&forced_storage=0&disable_ad=0&download=0", ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized || ex.StatusCode == HttpStatusCode.Forbidden)
        {
            await HandshakeAsync(ct);
            js = await GetAsync($"type=itv&action=create_link&cmd={Uri.EscapeDataString(cmd)}&series=&forced_storage=0&disable_ad=0&download=0", ct);
        }
        var link = Str(js, "cmd");
        if (link.Length == 0) link = Str(js, "url");
        if (link.Length == 0)
        {
            var error = Str(js, "error") is { Length: > 0 } e ? e : Str(js, "message");
            throw new InvalidOperationException(error.Length > 0 ? "Stalker : " + error : "Le portail Stalker n'a pas fourni de lien de lecture.");
        }
        link = link.Trim();
        foreach (var prefix in new[] { "ffmpeg ", "auto " })
            if (link.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { link = link[prefix.Length..].Trim(); break; }
        link = link.Trim('"', '\'');
        if (link.StartsWith("//", StringComparison.Ordinal)) link = "http:" + link;
        if (!Uri.TryCreate(link, UriKind.Absolute, out var streamUri) ||
            (streamUri.Scheme != "http" && streamUri.Scheme != "https" && streamUri.Scheme != "rtsp"))
            throw new InvalidOperationException("Le portail a retourné une URL de flux invalide.");
        return streamUri.ToString();
    }

    public void Dispose()
    {
        _keepAliveCts?.Cancel();
        _keepAliveCts?.Dispose();
        if (_ownsHttp) _http.Dispose();
        _sessionLock.Dispose();
    }
}
