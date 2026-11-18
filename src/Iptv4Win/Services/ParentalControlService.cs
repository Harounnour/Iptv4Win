using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Iptv4Win.Models;

namespace Iptv4Win.Services;

/// <summary>
/// Local parental-control settings. The PIN is stored as a salted PBKDF2 hash,
/// never as plain text. Adult-category channels are excluded from all normal lists.
/// </summary>
public sealed class ParentalControlService
{
    const int Iterations = 210_000;
    const int SaltSize = 16;
    const int HashSize = 32;

    static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Iptv4Win", "parental-controls.json");

    sealed class Settings
    {
        public string Salt { get; set; } = "";
        public string PinHash { get; set; } = "";
    }

    Settings _settings;

    public ParentalControlService()
    {
        try
        {
            _settings = File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings()
                : new Settings();
        }
        catch
        {
            // A damaged settings file must fail closed: adult content remains hidden.
            _settings = new Settings();
        }
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_settings.Salt)
        && !string.IsNullOrEmpty(_settings.PinHash);

    public bool IsEnabled => _settings.Enabled;

    public void SetPin(string pin)
    {
        if (pin.Length is < 4 or > 6 || pin.Any(c => c < '0' || c > '9'))
            throw new ArgumentException("Le code PIN doit contenir de 4 à 6 chiffres.");

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        _settings.Salt = Convert.ToBase64String(salt);
        _settings.PinHash = Convert.ToBase64String(hash);
        Save();
    }

    public bool VerifyPin(string pin)
    {
        if (!IsConfigured) return false;
        try
        {
            var salt = Convert.FromBase64String(_settings.Salt);
            var expected = Convert.FromBase64String(_settings.PinHash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }

    public bool IsAdult(Channel channel)
    {
        return LooksAdult(channel.Name) || LooksAdult(channel.Group);
    }

    static bool LooksAdult(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.Trim().ToLowerInvariant();
        if (text.Contains("18+") || text.Contains("xxx") || text.Contains("🔞")) return true;

        string[] phrases =
        {
            "adult", "adults", "adultos", "adulte", "pour adultes",
            "erotic", "erotique", "erotica", "porn", "playboy",
            "hustler", "red light", "redlight", "blue movie"
        };
        if (phrases.Any(p => text.Contains(p, StringComparison.Ordinal))) return true;

        // Match standalone category words, avoiding accidental matches inside unrelated words.
        return System.Text.RegularExpressions.Regex.IsMatch(
            text, @"(?<![\p{L}\p{N}])(sex|xxx|x-rated)(?![\p{L}\p{N}])",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_settings,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
