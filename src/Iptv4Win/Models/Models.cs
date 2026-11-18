namespace Iptv4Win.Models;

public enum PlaylistType { M3U, Xtream, Stalker }

public class PlaylistSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public PlaylistType Type { get; set; } = PlaylistType.M3U;
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Mac { get; set; } = "";
    public string EpgUrl { get; set; } = "";
    public string Display => $"{(string.IsNullOrWhiteSpace(Name) ? "Sans nom" : Name)}  ·  {Type}";
}

public class Channel
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string Group { get; set; } = "";
    public string ContentType { get; set; } = "Live";
    public string? SeriesId { get; set; }
    public string? LogoUrl { get; set; }
    public string StreamUrl { get; set; } = "";
    public string? StalkerCmd { get; set; }
    public Uri? LogoUri =>
        Uri.TryCreate(LogoUrl, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https") ? u : null;
    public string Initials => Name.Length >= 2 ? Name[..2].ToUpperInvariant() : Name.ToUpperInvariant();
}
