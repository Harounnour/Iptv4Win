using System.Text.Json;
using Iptv4Win.Models;

namespace Iptv4Win.Services;

public class XtreamClient
{
    readonly HttpClient _http;
    readonly string _base, _user, _pass;

    public XtreamClient(PlaylistSource s, HttpClient http)
    {
        _http = http;
        _base = s.Url.Trim().TrimEnd('/');
        _user = s.Username;
        _pass = s.Password;
    }

    static string Str(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return "";
        return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString();
    }

    public async Task<List<Channel>> GetLiveChannelsAsync()
    {
        var u = Uri.EscapeDataString(_user);
        var p = Uri.EscapeDataString(_pass);
        var api = $"{_base}/player_api.php?username={u}&password={p}";

        var cats = new Dictionary<string, string>();
        using (var d = JsonDocument.Parse(await _http.GetStringAsync(api + "&action=get_live_categories")))
        {
            if (d.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var c in d.RootElement.EnumerateArray())
                    cats[Str(c, "category_id")] = Str(c, "category_name");
        }

        using var s = JsonDocument.Parse(await _http.GetStringAsync(api + "&action=get_live_streams"));
        if (s.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Réponse invalide du serveur (identifiants ou adresse incorrects ?)");

        var list = new List<Channel>();
        var n = 1;
        foreach (var c in s.RootElement.EnumerateArray())
        {
            var id = Str(c, "stream_id");
            cats.TryGetValue(Str(c, "category_id"), out var group);
            var icon = Str(c, "stream_icon");
            list.Add(new Channel
            {
                Number = int.TryParse(Str(c, "num"), out var num) ? num : n,
                Name = Str(c, "name"),
                Group = group ?? "",
                LogoUrl = icon.Length > 0 ? icon : null,
                StreamUrl = $"{_base}/live/{u}/{p}/{id}.m3u8"
            });
            n++;
        }
        return list;
    }
}
