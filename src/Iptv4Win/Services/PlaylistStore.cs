using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Iptv4Win.Models;

namespace Iptv4Win.Services;

public class PlaylistStore
{
    static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Iptv4Win", "playlists.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public List<PlaylistSource> Items { get; } = new();
    public Guid? LastUsedId { get; set; }

    class Stored
    {
        public Guid? LastUsedId { get; set; }
        public List<StoredSource> Items { get; set; } = new();
    }
    class StoredSource
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public PlaylistType Type { get; set; }
        public string Url { get; set; } = "";
        public string Username { get; set; } = "";
        public string EpgUrl { get; set; } = "";
        public string PasswordEnc { get; set; } = "";
        public string MacEnc { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public string DeviceId2 { get; set; } = "";
    }

    public static PlaylistStore Load()
    {
        var store = new PlaylistStore();
        try
        {
            if (!File.Exists(FilePath)) return store;
            var d = JsonSerializer.Deserialize<Stored>(File.ReadAllText(FilePath), Json);
            if (d == null) return store;
            store.LastUsedId = d.LastUsedId;
            foreach (var x in d.Items)
                store.Items.Add(new PlaylistSource { Id=x.Id, Name=x.Name, Type=x.Type, Url=x.Url, Username=x.Username,
                    EpgUrl=x.EpgUrl, Password=Dec(x.PasswordEnc), Mac=Dec(x.MacEnc), SerialNumber=x.SerialNumber, DeviceId=x.DeviceId, DeviceId2=x.DeviceId2 });
        }
        catch { }
        return store;
    }

    public void Save()
    {
        var d = new Stored { LastUsedId = LastUsedId };
        foreach (var s in Items)
            d.Items.Add(new StoredSource { Id=s.Id, Name=s.Name, Type=s.Type, Url=s.Url, Username=s.Username, EpgUrl=s.EpgUrl,
                PasswordEnc=Enc(s.Password), MacEnc=Enc(s.Mac), SerialNumber=s.SerialNumber, DeviceId=s.DeviceId, DeviceId2=s.DeviceId2 });
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(d, Json));
    }

    static string Enc(string v) => string.IsNullOrEmpty(v) ? "" :
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(v), null, DataProtectionScope.CurrentUser));
    static string Dec(string v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(v), null, DataProtectionScope.CurrentUser)); }
        catch { return ""; }
    }
}
