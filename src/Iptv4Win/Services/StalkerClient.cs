using System.Text.Json;
using Iptv4Win.Models;

namespace Iptv4Win.Services;

public class StalkerClient
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    const string Ua = "Mozilla/5.0 (QtEmbedded; U; Linux; C) AppleWebKit/533.3 (KHTML, like Gecko) MAG250 stbapp ver: 2 rev: 250 Safari/533.3";

    readonly string _load, _mac;
    string _token = "";

    public StalkerClient(PlaylistSource s)
    {
        var b = s.Url.Trim().TrimEnd('/');
        if (b.EndsWith("/c", StringComparison.OrdinalIgnoreCase)) b = b[..^2];
        _load = b + "/server/load.php";
        _mac = s.Mac.Trim();
    }

    async Task<JsonElement> GetAsync(string query)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_load}?{query}&JsHttpRequest=1-xml");
        req.Headers.TryAddWithoutValidation("User-Agent", Ua);
        req.Headers.TryAddWithoutValidation("X-User-Agent", "Model: MAG250; Link: WiFi");
        req.Headers.TryAddWithoutValidation("Cookie", $"mac={_mac}; stb_lang=en; timezone=Europe/Paris");
        if (_token.Length > 0) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _token);

        using var resp = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("js").Clone();
    }

    static string Str(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return "";
        return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString();
    }

    async Task HandshakeAsync()
    {
        var js = await GetAsync("type=stb&action=handshake&token=");
        _token = Str(js, "token");
        if (_token.Length == 0) throw new InvalidOperationException("Handshake refusé (adresse du portail ou MAC incorrects ?)");
        try { await GetAsync("type=stb&action=get_profile"); } catch { /* optionnel selon les portails */ }
    }

    public async Task<List<Channel>> GetChannelsAsync()
    {
        await HandshakeAsync();

        var genres = new Dictionary<string, string>();
        var g = await GetAsync("type=itv&action=get_genres");
        if (g.ValueKind == JsonValueKind.Array)
            foreach (var x in g.EnumerateArray()) genres[Str(x, "id")] = Str(x, "title");

        var all = await GetAsync("type=itv&action=get_all_channels");
        var data = all.GetProperty("data");
        var list = new List<Channel>();
        var n = 1;
        foreach (var c in data.EnumerateArray())
        {
            genres.TryGetValue(Str(c, "tv_genre_id"), out var group);
            var logo = Str(c, "logo");
            list.Add(new Channel
            {
                Number = int.TryParse(Str(c, "number"), out var num) ? num : n,
                Name = Str(c, "name"),
                Group = group ?? "",
                LogoUrl = logo.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? logo : null,
                StalkerCmd = Str(c, "cmd")
            });
            n++;
        }
        return list;
    }

    public async Task<string> CreateLinkAsync(string cmd)
    {
        var js = await GetAsync($"type=itv&action=create_link&cmd={Uri.EscapeDataString(cmd)}&series=&forced_storage=0&disable_ad=0&download=0");
        var u = Str(js, "cmd").Trim();
        var i = u.LastIndexOf(' ');
        return i >= 0 ? u[(i + 1)..] : u;
    }
}
