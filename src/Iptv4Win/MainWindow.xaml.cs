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
        "--network-caching=5000",
        "--live-caching=5000",
        "--file-caching=5000",
        "--http-reconnect",
        "--rtsp-tcp",
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
        {
            BufferProgress.Visibility = Visibility.Collapsed;
            StatusText.Text = "Échec de lecture. Vérifiez l'URL du flux, le format ou les droits d'accès.";
        });
        _player.Playing += (_, _) => Dispatcher.Invoke(() =>
        {
            BufferProgress.Visibility = Visibility.Collapsed;
            StatusText.Text = "Lecture en cours";
        });
        _player.Buffering += (_, value) => Dispatcher.Invoke(() =>
        {
            var percent = Math.Clamp(value.Cache, 0, 100);
            BufferProgress.Visibility = percent >= 100 ? Visibility.Collapsed : Visibility.Visible;
            BufferProgress.Value = percent;
            StatusText.Text = percent < 100
                ? $"Vérifier la mise en tampon… {percent:0} %"
                : "Connexion au flux…";
        });
        _player.EndReached += (_, _) => Dispatcher.Invoke(() =>
        {
            BufferProgress.Visibility = Visibility.Collapsed;
            StatusText.Text = "Fin du flux.";
        });

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

    static bool IsAdultChannel(Channel ch)
    {
        var text = (ch.Name + " " + ch.Group).ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(text,
            @"(^|[^a-z0-9])(adult|adulte|18\+|xxx|porn|erotic|erotique|hot)([^a-z0-9]|$)");
    }

    async Task PlayAsync(Channel ch)
    {
        if (IsAdultChannel(ch) && !ParentControlWindow.Authorize(this))
        {
            StatusText.Text = "Chaîne bloquée par le contrôle parental.";
            ChannelList.SelectedItem = null;
            return;
        }
        NowName.Text = ch.Name;
        StatusText.Text = "Connexion au flux…";
        BufferProgress.Value = 0;
        BufferProgress.Visibility = Visibility.Visible;
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
            media.AddOption(":network-caching=5000");
            media.AddOption(":live-caching=5000");
            media.AddOption(":file-caching=5000");
            media.AddOption(":http-reconnect");
            media.AddOption(":rtsp-tcp");
            media.AddOption(":no-video-title-show");
            media.AddOption(":http-referrer=" + (ch.StalkerCmd != null ? "http://localhost/" : uri.GetLeftPart(UriPartial.Authority) + "/"));
            media.AddOption(":http-user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
            _currentMedia = media;

            if (!_player.Play(media))
                throw new InvalidOperationException("Le lecteur a refusé ce flux.");
            StatusText.Text = "Vérifier la mise en tampon…";
        }
        catch (Exception ex)
        {
            BufferProgress.Visibility = Visibility.Collapsed;
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
    void BtnWeather_Click(object sender, RoutedEventArgs e) => new WeatherWindow { Owner = this }.ShowDialog();
    void BtnQuran_Click(object sender, RoutedEventArgs e) => new QuranWindow { Owner = this }.ShowDialog();

    void PlayAdjacent(int delta)
    {
        var index = ChannelList.SelectedIndex;
        if (index < 0) index = delta > 0 ? -1 : ChannelList.Items.Count;
        var next = index + delta;
        if (next >= 0 && next < ChannelList.Items.Count)
            ChannelList.SelectedIndex = next;
    }

    void BtnPrevious_Click(object sender, RoutedEventArgs e) => PlayAdjacent(-1);
    void BtnNext_Click(object sender, RoutedEventArgs e) => PlayAdjacent(1);
    void BtnParental_Click(object sender, RoutedEventArgs e)
    {
        if (ParentControlWindow.Configure(this))
            StatusText.Text = "Contrôle parental configuré.";
    }

    void SetVolume(int volume)
    {
        _player.Volume = Math.Clamp(volume, 0, 100);
        VolumeText.Text = $"{_player.Volume} %";
        if (_player.Volume > 0) _player.Mute = false;
    }

    void BtnVolumeDown_Click(object sender, RoutedEventArgs e) => SetVolume(_player.Volume - 10);
    void BtnVolumeUp_Click(object sender, RoutedEventArgs e) => SetVolume(_player.Volume + 10);
    void BtnMute_Click(object sender, RoutedEventArgs e) => _player.Mute = !_player.Mute;
    void BtnFull_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11 || (e.Key == Key.Escape && _full)) ToggleFullscreen();
    }
}
