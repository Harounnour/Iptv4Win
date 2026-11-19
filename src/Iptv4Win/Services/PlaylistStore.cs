using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Iptv4Win.Models;

namespace Iptv4Win.Services;

/// <summary>SQLite-backed playlist storage. Credentials and device identifiers are protected with Windows DPAPI.</summary>
public sealed class PlaylistStore
{
    private static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Iptv4Win");
    private static readonly string DbPath = Path.Combine(Root, "iptv4win.db");
    private static readonly string LegacyPath = Path.Combine(Root, "playlists.json");
    public List<PlaylistSource> Items { get; } = new();
    public Guid? LastUsedId { get; set; }

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Root);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DbPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS playlists (
              id TEXT PRIMARY KEY, name TEXT NOT NULL, type INTEGER NOT NULL, url TEXT NOT NULL,
              username TEXT NOT NULL, password_enc TEXT NOT NULL, mac_enc TEXT NOT NULL,
              epg_url TEXT NOT NULL, serial_enc TEXT NOT NULL, device_id_enc TEXT NOT NULL, device_id2_enc TEXT NOT NULL, ip_enc TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS app_state (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            """;
        cmd.ExecuteNonQuery();
        // Upgrade existing databases without losing saved playlists.
        using var columns = connection.CreateCommand();
        columns.CommandText = "PRAGMA table_info(playlists)";
        using var reader = columns.ExecuteReader();
        var hasIp = false;
        while (reader.Read()) if (reader.GetString(1) == "ip_enc") hasIp = true;
        reader.Close();
        if (!hasIp)
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE playlists ADD COLUMN ip_enc TEXT NOT NULL DEFAULT ''";
            alter.ExecuteNonQuery();
        }
        return connection;
    }

    public static PlaylistStore Load()
    {
        var store = new PlaylistStore();
        try
        {
            using var db = store.Open();
            using (var state = db.CreateCommand())
            {
                state.CommandText = "SELECT value FROM app_state WHERE key='last_used_id'";
                var value = state.ExecuteScalar()?.ToString();
                if (Guid.TryParse(value, out var id)) store.LastUsedId = id;
            }
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT id,name,type,url,username,password_enc,mac_enc,epg_url,serial_enc,device_id_enc,device_id2_enc,ip_enc FROM playlists ORDER BY rowid";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (!Guid.TryParse(reader.GetString(0), out var id)) continue;
                store.Items.Add(new PlaylistSource
                {
                    Id=id, Name=reader.GetString(1), Type=(PlaylistType)reader.GetInt32(2), Url=reader.GetString(3),
                    Username=reader.GetString(4), Password=Dec(reader.GetString(5)), Mac=Dec(reader.GetString(6)),
                    EpgUrl=reader.GetString(7), SerialNumber=Dec(reader.GetString(8)),
                    DeviceId=Dec(reader.GetString(9)), DeviceId2=Dec(reader.GetString(10)), IpAddress=Dec(reader.GetString(11))
                });
            }
            if (store.Items.Count == 0 && File.Exists(LegacyPath)) store.ImportLegacyJson();
        }
        catch { /* A damaged local database must not prevent the application from opening. */ }
        return store;
    }

    private void ImportLegacyJson()
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(LegacyPath));
            if (doc.RootElement.TryGetProperty("LastUsedId", out var last) && Guid.TryParse(last.GetString(), out var id)) LastUsedId = id;
            if (doc.RootElement.TryGetProperty("Items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var source = new PlaylistSource();
                    if (item.TryGetProperty("Id", out var v) && Guid.TryParse(v.GetString(), out var guid)) source.Id = guid;
                    if (item.TryGetProperty("Name", out v)) source.Name = v.GetString() ?? "";
                    if (item.TryGetProperty("Type", out v))
                    {
                        var t = v.ToString();
                        if (Enum.TryParse<PlaylistType>(t, true, out var parsed)) source.Type = parsed;
                    }
                    if (item.TryGetProperty("Url", out v)) source.Url = v.GetString() ?? "";
                    if (item.TryGetProperty("Username", out v)) source.Username = v.GetString() ?? "";
                    if (item.TryGetProperty("EpgUrl", out v)) source.EpgUrl = v.GetString() ?? "";
                    if (item.TryGetProperty("PasswordEnc", out v)) source.Password = Dec(v.GetString() ?? "");
                    if (item.TryGetProperty("MacEnc", out v)) source.Mac = Dec(v.GetString() ?? "");
                    Items.Add(source);
                }
            }
            Save();
        }
        catch { }
    }

    public void Save()
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        using (var clear = db.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM playlists";
            clear.ExecuteNonQuery();
        }
        foreach (var s in Items)
        {
            using var cmd = db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO playlists(id,name,type,url,username,password_enc,mac_enc,epg_url,serial_enc,device_id_enc,device_id2_enc,ip_enc)
                VALUES($id,$name,$type,$url,$username,$password,$mac,$epg,$serial,$device,$device2,$ip)
                """;
            cmd.Parameters.AddWithValue("$id", s.Id.ToString());
            cmd.Parameters.AddWithValue("$name", s.Name);
            cmd.Parameters.AddWithValue("$type", (int)s.Type);
            cmd.Parameters.AddWithValue("$url", s.Url);
            cmd.Parameters.AddWithValue("$username", s.Username);
            cmd.Parameters.AddWithValue("$password", Enc(s.Password));
            cmd.Parameters.AddWithValue("$mac", Enc(s.Mac));
            cmd.Parameters.AddWithValue("$epg", s.EpgUrl);
            cmd.Parameters.AddWithValue("$serial", Enc(s.SerialNumber));
            cmd.Parameters.AddWithValue("$device", Enc(s.DeviceId));
            cmd.Parameters.AddWithValue("$device2", Enc(s.DeviceId2));
            cmd.Parameters.AddWithValue("$ip", Enc(s.IpAddress));
            cmd.ExecuteNonQuery();
        }
        using (var state = db.CreateCommand())
        {
            state.Transaction = tx;
            state.CommandText = "INSERT INTO app_state(key,value) VALUES('last_used_id',$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            state.Parameters.AddWithValue("$value", LastUsedId?.ToString() ?? "");
            state.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static string Enc(string value) => string.IsNullOrEmpty(value) ? "" :
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    private static string Dec(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser)); }
        catch { return ""; }
    }
}
