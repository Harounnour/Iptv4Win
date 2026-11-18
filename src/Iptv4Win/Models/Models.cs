namespace Iptv4Win.Models;

public enum PlaylistType { M3U, Xtream, Stalker }

public class PlaylistSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public PlaylistType Type { get; set; } = PlaylistType.M3U;
    /// <summary>M3U : URL ou chemin de fichier. Xtream : adresse du serveur. Stalker : adresse du portail.</summary>
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
    public string? LogoUrl { get; set; }
    public string StreamUrl { get; set; } = "";
    /// <summary>Stalker : commande à résoudre avec create_link avant la lecture.</summary>
    public string? StalkerCmd { get; set; }

    public Uri? LogoUri =>
        Uri.TryCreate(LogoUrl, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https") ? u : null;

    public string Initials => Name.Length >= 2 ? Name[..2].ToUpperInvariant() : Name.ToUpperInvariant();
}
