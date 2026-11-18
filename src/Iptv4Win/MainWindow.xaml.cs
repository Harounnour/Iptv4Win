using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Iptv4Win.Models;
using Iptv4Win.Services;
using LibVLCSharp.Shared;

namespace Iptv4Win;

public partial class MainWindow : Window
{
    const string AllGroups = "Toutes les chaînes";

    readonly LibVLC _lib = new();
    readonly MediaPlayer _player;
    Media? _currentMedia;
    readonly PlaylistStore _store = PlaylistStore.Load();
    readonly ParentalControlService _parental = new();
    List<Channel> _all = new();
    StalkerClient? _stalker;
    bool _full;

    public MainWindow()
    {
        InitializeComponent();
        _player = new MediaPlayer(_lib);
        _player.EncounteredError += (_, _) => Dispatcher.Invoke(() =>
            StatusText.Text = "Échec de lecture du flux. Vérifiez l'abonnement, l'URL et la disponibilité du serveur.");
        _player.EndReached += (_, _) => Dispatcher.Invoke(() =>
            StatusText.Text = "Le flux est terminé ou le serveur a interrompu la diffusion.");

        Loaded += async (_, _) =>
        {
            if (!_parental.IsConfigured)
            {
                var firstPin = PinDialog.Show(this, "Contrôle parental",
                    "Créez le code PIN parental (4 à 6 chiffres). Les chaînes classées adultes seront masquées.", true);
                if (firstPin == null) { Close(); return; }
                _parental.SetPin(firstPin);
            }
            Video.MediaPlayer = _player;
            var src = _store.Items.FirstOrDefault(x => x.Id == _store.LastUsedId) ?? _store.Items.FirstOrDefault();
            if (src == null) await OpenPlaylistsAsync();
            else await LoadSourceAsync(src);
        };
        Closed += (_, _) =>
        {
            _player.Stop();
            _currentMedia?.Dispose();
            _player.Dispose();
            _lib.Dispose();
        };
    }

    async Task LoadSourceAsync(PlaylistSource src)
    {
        StatusText.Text = "Chargement de la playlist…";
        try
        {
            var r = await ChannelLoader.LoadAsync(src);
            _all = r.Channels;
            _stalker = r.Stalker;

            var allowed = _all.Where(c => !_parental.IsAdult(c)).ToList();
            var groups = new List<string> { AllGroups };
            groups.AddRange(allowed.Select(c => c.Group).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct().OrderBy(g => g));
            GroupList.ItemsSource = groups;
            GroupList.SelectedIndex = 0;
            StatusText.Text = $"{src.Name} : {allowed.Count} chaînes disponibles (contenu adulte filtré)";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Erreur : " + ex.Message;
        }
    }

    void Refresh()
    {
        if (ChannelList == null) return;
        var g = GroupList.SelectedItem as string;
        var q = SearchBox.Text.Trim();
        IEnumerable<Channel> r = _all.Where(c => !_parental.IsAdult(c));
        if (g != null && g != AllGroups) r = r.Where(c => c.Group == g);
        if (q.Length > 0) r = r.Where(c => c.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase));
        var list = r.ToList();
        ChannelList.ItemsSource = list;
        CountText.Text = $"{list.Count} chaînes";
    }

    async Task PlayAsync(Channel ch)
    {
        NowName.Text = ch.Name;
        StatusText.Text = "Connexion…";
        try
        {
            var url = ch.StreamUrl;
            if (ch.StalkerCmd != null && _stalker != null)
                url = await _stalker.CreateLinkAsync(ch.StalkerCmd);

            if (string.IsNullOrWhiteSpace(url) ||
                !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var streamUri) ||
                !new[] { "http", "https", "rtsp", "rtmp", "rtp", "udp", "mms", "mmsh", "file" }.Contains(streamUri.Scheme, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("L'adresse du flux reçue de la playlist est invalide.");

            // Garder l'objet Media vivant pendant toute la lecture évite sa libération
            // immédiate après l'appel à Play().
            _player.Stop();
            _currentMedia?.Dispose();
            _currentMedia = new Media(_lib, streamUri);
            _currentMedia.AddOption(":network-caching=1500");
            _currentMedia.AddOption(":live-caching=1500");
            _currentMedia.AddOption(":http-user-agent=Mozilla/5.0");
            _currentMedia.AddOption(":http-reconnect");

            if (!_player.Play(_currentMedia))
                throw new InvalidOperationException("VLC n'a pas pu démarrer ce flux.");

            StatusText.Text = string.IsNullOrWhiteSpace(ch.Group) ? "Lecture en cours…" : ch.Group;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Erreur : " + ex.Message;
        }
    }

    async Task OpenPlaylistsAsync()
    {
        var w = new PlaylistsWindow(_store) { Owner = this };
        if (w.ShowDialog() == true && w.SelectedSource != null)
        {
            _store.LastUsedId = w.SelectedSource.Id;
            _store.Save();
            await LoadSourceAsync(w.SelectedSource);
        }
    }

    void ToggleFullscreen()
    {
        _full = !_full;
        TopBar.Visibility = InfoPanel.Visibility = _full ? Visibility.Collapsed : Visibility.Visible;
        Col0.Width = new GridLength(_full ? 0 : 230);
        Col1.Width = new GridLength(_full ? 0 : 520);
        if (_full)
        {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
        }
    }

    void GroupList_SelectionChanged(object sender, SelectionChangedEventArgs e) => Refresh();
    void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    async void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelList.SelectedItem is Channel ch)
        {
            if (_parental.IsAdult(ch))
            {
                StatusText.Text = "Accès bloqué par le contrôle parental.";
                ChannelList.SelectedItem = null;
                return;
            }
            await PlayAsync(ch);
        }
    }

    async void BtnPlaylists_Click(object sender, RoutedEventArgs e) => await OpenPlaylistsAsync();
    void BtnMute_Click(object sender, RoutedEventArgs e) => _player.Mute = !_player.Mute;

    void BtnParental_Click(object sender, RoutedEventArgs e)
    {
        var current = PinDialog.Show(this, "Vérification parentale", "Saisissez le code PIN actuel.");
        if (current == null) return;
        if (!_parental.VerifyPin(current))
        {
            StatusText.Text = "Code PIN incorrect. Les protections restent actives.";
            return;
        }
        var next = PinDialog.Show(this, "Modifier le code PIN", "Choisissez un nouveau code PIN (4 à 6 chiffres).", true);
        if (next == null) return;
        _parental.SetPin(next);
        StatusText.Text = "Code PIN parental mis à jour. Le filtrage reste activé.";
    }
    void BtnFull_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11 || (e.Key == Key.Escape && _full)) ToggleFullscreen();
    }
}
