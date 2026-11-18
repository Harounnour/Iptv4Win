using System.Net;
using System.Net.Http;
using System.Text.Json;
using Iptv4Win.Models;

namespace Iptv4Win.Services;

public class StalkerClient
{
    readonly HttpClient _http = new(new HttpClientHandler
    {
        CookieContainer = new CookieContainer(),
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true,
        UseCookies = true
    }) { Timeout = TimeSpan.FromSeconds(60) };

    const string Ua = "Mozilla/5.0 (QtEmbedded; U; Linux; C) AppleWebKit/533.3 (KHTML, like Gecko) MAG250 stbapp ver: 2 rev: 250 Safari/533.3";
    readonly string _load, _mac, _referer, _origin;
    public string PlaybackReferer => _referer;
    public string PlaybackUserAgent => Ua;
    public string PlaybackCookie => $"mac={_mac}; stb_lang=en; timezone=Europe/Paris" + (_token.Length > 0 ? $"; token={_token}" : "");
    string _token = "";

    public StalkerClient(PlaylistSource s)
    {
        var raw=s.Url.Trim();
        if(!Uri.TryCreate(raw,UriKind.Absolute,out var portal)||(portal.Scheme!=Uri.UriSchemeHttp&&portal.Scheme!=Uri.UriSchemeHttps))
            throw new InvalidOperationException("Adresse du portail Stalker invalide.");

        var path=portal.AbsolutePath.TrimEnd('/');
        var marker=path.IndexOf("/server/load.php",StringComparison.OrdinalIgnoreCase);
        if(marker>=0)path=path[..marker];
        else if(path.EndsWith("/c",StringComparison.OrdinalIgnoreCase))path=path[..^2];

        var baseUri=new UriBuilder(portal){Path=path.TrimEnd('/')+"/",Query="",Fragment=""}.Uri;
        _load=new Uri(baseUri,"server/load.php").ToString();
        _referer=new Uri(baseUri,"c/").ToString();
        _origin=baseUri.GetLeftPart(UriPartial.Authority);
        _mac=s.Mac.Trim().ToUpperInvariant();
        if(!_mac.Contains(':')||_mac.Length!=17)
            throw new InvalidOperationException("Adresse MAC invalide. Format attendu : 00:1A:79:XX:XX:XX");
    }

    async Task<JsonElement> GetAsync(string query)
    {
        using var req=new HttpRequestMessage(HttpMethod.Get,$"{_load}?{query}&JsHttpRequest=1-xml");
        req.Headers.TryAddWithoutValidation("User-Agent",Ua);
        req.Headers.TryAddWithoutValidation("X-User-Agent","Model: MAG250; Link: WiFi");
        req.Headers.TryAddWithoutValidation("Referer",_referer);
        req.Headers.TryAddWithoutValidation("Origin",_origin);
        req.Headers.TryAddWithoutValidation("Accept","application/json, text/javascript, */*; q=0.01");
        req.Headers.TryAddWithoutValidation("Accept-Language","fr-FR,fr;q=0.9,en;q=0.8");
        req.Headers.TryAddWithoutValidation("Cookie",PlaybackCookie);
        if(_token.Length>0)req.Headers.TryAddWithoutValidation("Authorization","Bearer "+_token);

        using var resp=await _http.SendAsync(req,HttpCompletionOption.ResponseContentRead);
        var body=await resp.Content.ReadAsStringAsync();
        if(!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Portail Stalker : HTTP {(int)resp.StatusCode} ({resp.ReasonPhrase}).",null,resp.StatusCode);
        using var doc=JsonDocument.Parse(body);
        if(!doc.RootElement.TryGetProperty("js",out var js))
            throw new InvalidOperationException("Réponse Stalker invalide : champ js absent.");
        if(js.ValueKind==JsonValueKind.String)
        {
            var nested=js.GetString();
            if(!string.IsNullOrWhiteSpace(nested))
            {
                try{using var nestedDoc=JsonDocument.Parse(nested);return nestedDoc.RootElement.Clone();}
                catch(JsonException){}
            }
        }
        return js.Clone();
    }

    static string Str(JsonElement e,string name)
    {
        if(e.ValueKind!=JsonValueKind.Object||!e.TryGetProperty(name,out var v)||v.ValueKind==JsonValueKind.Null)return "";
        return v.ValueKind==JsonValueKind.String?v.GetString()??"":v.ToString();
    }

    async Task HandshakeAsync()
    {
        _token="";
        var js=await GetAsync("type=stb&action=handshake&token=");
        _token=Str(js,"token");
        if(_token.Length==0)
            throw new InvalidOperationException("Handshake Stalker refusé : vérifiez le portail, l'adresse MAC et l'autorisation de l'appareil.");
        try{await GetAsync("type=stb&action=get_profile");}catch{}
    }

    public async Task<List<Channel>> GetChannelsAsync()
    {
        await HandshakeAsync();
        var genres=new Dictionary<string,string>();
        var g=await GetAsync("type=itv&action=get_genres");
        if(g.ValueKind==JsonValueKind.Array)
            foreach(var item in g.EnumerateArray())genres[Str(item,"id")]=Str(item,"title");

        var all=await GetAsync("type=itv&action=get_all_channels");
        if(!all.TryGetProperty("data",out var data)||data.ValueKind!=JsonValueKind.Array)
            throw new InvalidOperationException("Le portail Stalker n'a pas retourné de liste de chaînes.");
        var list=new List<Channel>();var n=1;
        foreach(var ch in data.EnumerateArray())
        {
            genres.TryGetValue(Str(ch,"tv_genre_id"),out var group);
            var logo=Str(ch,"logo");
            list.Add(new Channel
            {
                Number=int.TryParse(Str(ch,"number"),out var num)?num:n,
                Name=Str(ch,"name"),
                Group=group??"",
                LogoUrl=Uri.TryCreate(logo,UriKind.Absolute,out var logoUri)&&(logoUri.Scheme=="http"||logoUri.Scheme=="https")?logo:null,
                StalkerCmd=Str(ch,"cmd")
            });
            n++;
        }
        return list;
    }

    public async Task<string> CreateLinkAsync(string cmd)
    {
        if(string.IsNullOrWhiteSpace(cmd))throw new InvalidOperationException("Commande Stalker vide.");
        if(_token.Length==0)await HandshakeAsync();
        JsonElement js;
        try
        {
            js=await GetAsync($"type=itv&action=create_link&cmd={Uri.EscapeDataString(cmd)}&series=&forced_storage=0&disable_ad=0&download=0");
        }
        catch(HttpRequestException ex) when(ex.StatusCode==HttpStatusCode.Unauthorized||ex.StatusCode==HttpStatusCode.Forbidden)
        {
            await HandshakeAsync();
            js=await GetAsync($"type=itv&action=create_link&cmd={Uri.EscapeDataString(cmd)}&series=&forced_storage=0&disable_ad=0&download=0");
        }

        var link=Str(js,"cmd");
        if(link.Length==0)link=Str(js,"url");
        if(link.Length==0)
        {
            var error=Str(js,"error") is {Length:>0} e?e:Str(js,"message");
            throw new InvalidOperationException(error.Length>0?"Stalker : "+error:"Le portail Stalker n'a pas fourni de lien de lecture.");
        }
        link=link.Trim();
        foreach(var prefix in new[]{"ffmpeg ","auto "})
            if(link.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){link=link[prefix.Length..].Trim();break;}
        link=link.Trim('"','\'');
        if(link.StartsWith("//",StringComparison.Ordinal))link="http:"+link;
        if(!Uri.TryCreate(link,UriKind.Absolute,out var streamUri)||(streamUri.Scheme!="http"&&streamUri.Scheme!="https"&&streamUri.Scheme!="rtsp"))
            throw new InvalidOperationException("Le portail a retourné une URL de flux invalide.");
        return streamUri.ToString();
    }
}
