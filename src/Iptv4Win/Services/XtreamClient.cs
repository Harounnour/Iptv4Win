using System.Net.Http;
using System.Text.Json;
using Iptv4Win.Models;

namespace Iptv4Win.Services;

public class XtreamClient
{
    readonly HttpClient _http;
    readonly string _base, _user, _pass;
    public XtreamClient(PlaylistSource s, HttpClient http)
    {
        _http = http; _base = s.Url.Trim().TrimEnd('/'); _user = s.Username; _pass = s.Password;
    }

    static string Str(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return "";
        return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString();
    }
    string Api => $"{_base}/player_api.php?username={Uri.EscapeDataString(_user)}&password={Uri.EscapeDataString(_pass)}";
    string AuthPath => $"{Uri.EscapeDataString(_user)}/{Uri.EscapeDataString(_pass)}";

    async Task<Dictionary<string,string>> CategoriesAsync(string action)
    {
        var result = new Dictionary<string,string>();
        using var doc = JsonDocument.Parse(await _http.GetStringAsync(Api + "&action=" + action));
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
            foreach (var item in doc.RootElement.EnumerateArray())
                result[Str(item,"category_id")] = Str(item,"category_name");
        return result;
    }

    public async Task<List<Channel>> GetAllContentAsync()
    {
        var list = new List<Channel>();
        var liveCats = await CategoriesAsync("get_live_categories");
        using (var doc = JsonDocument.Parse(await _http.GetStringAsync(Api + "&action=get_live_streams")))
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Réponse invalide du serveur Xtream.");
            var n=1;
            foreach(var item in doc.RootElement.EnumerateArray())
            {
                liveCats.TryGetValue(Str(item,"category_id"),out var group);
                var id=Str(item,"stream_id"); var icon=Str(item,"stream_icon");
                list.Add(new Channel { Number=int.TryParse(Str(item,"num"),out var num)?num:n++, Name=Str(item,"name"), Group=group??"Live", ContentType="Live", LogoUrl=icon.Length>0?icon:null, StreamUrl=$"{_base}/live/{AuthPath}/{id}.{(Str(item,"container_extension") is {Length:>0} ext?ext:"ts")}" });
            }
        }

        var vodCats = await CategoriesAsync("get_vod_categories");
        using (var doc = JsonDocument.Parse(await _http.GetStringAsync(Api + "&action=get_vod_streams")))
        {
            if(doc.RootElement.ValueKind==JsonValueKind.Array)
            {
                var n=1;
                foreach(var item in doc.RootElement.EnumerateArray())
                {
                    vodCats.TryGetValue(Str(item,"category_id"),out var group);
                    var id=Str(item,"stream_id");var ext=Str(item,"container_extension");
                    list.Add(new Channel { Number=n++,Name=Str(item,"name"),Group=group??"Films",ContentType="Films",LogoUrl=Str(item,"stream_icon"),StreamUrl=$"{_base}/movie/{AuthPath}/{id}.{(ext.Length>0?ext:"mp4")}" });
                }
            }
        }

        var seriesCats = await CategoriesAsync("get_series_categories");
        using (var doc = JsonDocument.Parse(await _http.GetStringAsync(Api + "&action=get_series")))
        {
            if(doc.RootElement.ValueKind==JsonValueKind.Array)
            {
                var n=1;
                foreach(var item in doc.RootElement.EnumerateArray())
                {
                    seriesCats.TryGetValue(Str(item,"category_id"),out var group);
                    list.Add(new Channel { Number=n++,Name=Str(item,"name"),Group=group??"Series",ContentType="Series",SeriesId=Str(item,"series_id"),LogoUrl=Str(item,"cover") });
                }
            }
        }
        return list;
    }

    public async Task<List<Channel>> GetSeriesEpisodesAsync(string seriesId)
    {
        using var doc=JsonDocument.Parse(await _http.GetStringAsync(Api+"&action=get_series_info&series_id="+Uri.EscapeDataString(seriesId)));
        if(!doc.RootElement.TryGetProperty("episodes",out var seasons)||seasons.ValueKind!=JsonValueKind.Object)
            throw new InvalidOperationException("Aucun épisode n'a été retourné pour cette série.");
        var result=new List<Channel>();var n=1;
        foreach(var season in seasons.EnumerateObject())
        {
            if(season.Value.ValueKind!=JsonValueKind.Array)continue;
            foreach(var ep in season.Value.EnumerateArray())
            {
                var id=Str(ep,"id");var ext=Str(ep,"container_extension");
                var title=Str(ep,"title");if(title.Length==0)title=$"Épisode {n}";
                result.Add(new Channel { Number=n++,Name=title,Group=$"Saison {season.Name}",ContentType="Live",StreamUrl=$"{_base}/series/{AuthPath}/{id}.{(ext.Length>0?ext:"mp4")}" });
            }
        }
        return result;
    }
}
