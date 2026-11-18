using System;
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

    readonly LibVLC _lib = new(
        "--network-caching=1500",
        "--live-caching=1500",
        "--http-reconnect",
        "--no-video-title-show");
    readonly MediaPlayer _player;
    Media? _currentMedia;
    readonly PlaylistStore _store = PlaylistStore.Load();
    List<Channel> _all = new();
    StalkerClient? _stalker;
    bool _full;

    public MainWindow()
    {
        InitializeComponent();
        _player = new MediaPlayer(_lib);

        _player.EncounteredError += (_, _) => Dispatcher.Invoke(() =>
            StatusText.Text = "Échec de lecture. Vérifiez le flux, le format ou les droits d'accès.");
        _player.Playing += (_, _) => Dispatcher.Invoke(() => StatusText.Text = "Lecture en cours");
        _player.Buffering += (_, value) => Dispatcher.Invoke(() =>
            StatusText.Text = value.Cache < 100 ? $"Mise en mémoire tampon… {value.Cache:0} %" : "Connexion…");

        Loaded += async (_, _) =>
        {
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

            var groups = new List<string> { AllGroups };
            groups.AddRange(_all.Select(c => c.Group).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct().OrderBy(g => g));
            GroupList.ItemsSource = groups;
            GroupList.SelectedIndex = 0;
            StatusText.Text = $"{src.Name} : {_all.Count} chaînes";
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
        IEnumerable<Channel> r = _all;
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

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != "rtsp"))
                throw new InvalidOperationException("Adresse de flux invalide.");

            _player.Stop();
            _currentMedia?.Dispose();

            var media = new Media(_lib, uri);
            media.AddOption(":network-caching=1500");
            media.AddOption(":http-reconnect");
            media.AddOption(":no-video-title-show");
            media.AddOption(":http-user-agent=VLC/3.0.20 LibVLC/3.0.20");
            _currentMedia = media;

            if (!_player.Play(media))
                throw new InvalidOperationException("Le lecteur a refusé ce flux.");
            StatusText.Text = "Connexion au flux…";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Échec de lecture : " + ex.Message;
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
        if (ChannelList.SelectedItem is Channel ch) await PlayAsync(ch);
    }

    async void BtnPlaylists_Click(object sender, RoutedEventArgs e) => await OpenPlaylistsAsync();
    void BtnMute_Click(object sender, RoutedEventArgs e) => _player.Mute = !_player.Mute;
    void BtnFull_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11 || (e.Key == Key.Escape && _full)) ToggleFullscreen();
    }
}
