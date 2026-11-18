using System.IO;
using System.Net.Http;
using Iptv4Win.Models;

namespace Iptv4Win.Services;

public record LoadResult(List<Channel> Channels, StalkerClient? Stalker);

public static class ChannelLoader
{
    static readonly HttpClient Http = CreateHttp();

    static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        h.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "VLC/3.0.20 LibVLC/3.0.20");
        return h;
    }

    public static async Task<LoadResult> LoadAsync(PlaylistSource s)
    {
        switch (s.Type)
        {
            case PlaylistType.Xtream:
                return new LoadResult(await new XtreamClient(s, Http).GetLiveChannelsAsync(), null);
            case PlaylistType.Stalker:
            {
                var st = new StalkerClient(s);
                return new LoadResult(await st.GetChannelsAsync(), st);
            }
            default:
            {
                var text = File.Exists(s.Url) ? await File.ReadAllTextAsync(s.Url) : await Http.GetStringAsync(s.Url);
                return new LoadResult(M3uParser.Parse(text), null);
            }
        }
    }
}
