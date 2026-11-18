using System.IO;
using System.Net.Http;
using Iptv4Win.Models;

namespace Iptv4Win.Services;

public record LoadResult(List<Channel> Channels, StalkerClient? Stalker, XtreamClient? Xtream = null);

public static class ChannelLoader
{
    static readonly HttpClient Http = CreateHttp();
    static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        h.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
        return h;
    }

    public static async Task<LoadResult> LoadAsync(PlaylistSource s)
    {
        switch (s.Type)
        {
            case PlaylistType.Xtream:
            {
                var xtream=new XtreamClient(s,Http);
                return new LoadResult(await xtream.GetAllContentAsync(),null,xtream);
            }
            case PlaylistType.Stalker:
            {
                var st = new StalkerClient(s);
                return new LoadResult(await st.GetChannelsAsync(), st);
            }
            default:
            {
                var text = File.Exists(s.Url) ? await File.ReadAllTextAsync(s.Url) : await Http.GetStringAsync(s.Url);
                var channels=M3uParser.Parse(text);
                foreach(var channel in channels)
                {
                    var label=(channel.Group+" "+channel.Name).ToLowerInvariant();
                    if(label.Contains("series")||label.Contains("série")||label.Contains("serie"))channel.ContentType="Series";
                    else if(label.Contains("film")||label.Contains("movie")||label.Contains("vod")||label.Contains("cinema"))channel.ContentType="Films";
                }
                return new LoadResult(channels,null);
            }
        }
    }
}
