using System;
using System.Diagnostics;
using System.IO;
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
    const string LiveGroup = "LIVE • Toutes";
    const string SeriesGroup = "SERIES • Toutes";
    const string FilmsGroup = "FILMS • Tous";
    readonly LibVLC _lib = new("--network-caching=5000","--live-caching=5000","--file-caching=5000","--http-reconnect","--rtsp-tcp","--no-video-title-show");
    readonly MediaPlayer _player;
    Media? _currentMedia;
    readonly PlaylistStore _store = PlaylistStore.Load();
    List<Channel> _all = new();
    StalkerClient? _stalker;
    XtreamClient? _xtream;
    Channel? _currentChannel;
    string? _currentStreamUrl;
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
        _player.Playing += (_, _) => Dispatcher.Invoke(() => { BufferProgress.Visibility=Visibility.Collapsed;StatusText.Text="Lecture en cours"; });
        _player.Buffering += (_, value) => Dispatcher.Invoke(() =>
        {
            var percent=Math.Clamp(value.Cache,0,100);
            BufferProgress.Visibility=percent>=100?Visibility.Collapsed:Visibility.Visible;BufferProgress.Value=percent;
            StatusText.Text=percent<100?$"Vérifier la mise en tampon… {percent:0} %":"Connexion au flux…";
        });
        _player.EndReached += (_, _) => Dispatcher.Invoke(() => { BufferProgress.Visibility=Visibility.Collapsed;StatusText.Text="Fin du flux."; });
        Loaded += async (_, _) =>
        {
            Video.MediaPlayer=_player;
            var src=_store.Items.FirstOrDefault(x=>x.Id==_store.LastUsedId)??_store.Items.FirstOrDefault();
            if(src==null)await OpenPlaylistsAsync();else await LoadSourceAsync(src);
        };
        Closed += (_, _) => { _player.Stop();_currentMedia?.Dispose();_player.Dispose();_lib.Dispose(); };
    }

    async Task LoadSourceAsync(PlaylistSource src)
    {
        StatusText.Text="Chargement de la playlist…";
        try
        {
            var r=await ChannelLoader.LoadAsync(src);_all=r.Channels;_stalker=r.Stalker;_xtream=r.Xtream;
            var groups=new List<string>{AllGroups,LiveGroup,SeriesGroup,FilmsGroup};
            groups.AddRange(_all.Select(c=>c.Group).Where(g=>!string.IsNullOrWhiteSpace(g)).Distinct().OrderBy(g=>g));
            GroupList.ItemsSource=groups;GroupList.SelectedIndex=0;StatusText.Text=$"{src.Name} : {_all.Count} chaînes";
        }
        catch(Exception ex){StatusText.Text="Erreur : "+ex.Message;}
    }
    void Refresh()
    {
        if(ChannelList==null)return;
        var g=GroupList.SelectedItem as string;var q=SearchBox.Text.Trim();IEnumerable<Channel> r=_all;
        if(g==LiveGroup)r=r.Where(c=>c.ContentType=="Live");
        else if(g==SeriesGroup)r=r.Where(c=>c.ContentType=="Series");
        else if(g==FilmsGroup)r=r.Where(c=>c.ContentType=="Films");
        else if(g!=null&&g!=AllGroups)r=r.Where(c=>c.Group==g);
        if(q.Length>0)r=r.Where(c=>c.Name.Contains(q,StringComparison.CurrentCultureIgnoreCase));
        var sort=SortBox?.SelectedIndex??0;
        r=sort switch {1=>r.OrderBy(c=>c.Name,StringComparer.CurrentCultureIgnoreCase),2=>r.OrderByDescending(c=>c.Name,StringComparer.CurrentCultureIgnoreCase),3=>r.OrderBy(c=>c.Number),_=>r};
        var list=r.ToList();ChannelList.ItemsSource=list;CountText.Text=$"{list.Count} éléments";
    }
    static bool IsAdultChannel(Channel ch)
    {
        var text=(ch.Name+" "+ch.Group).ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(text,@"(^|[^a-z0-9])(adult|adulte|18\+|xxx|porn|erotic|erotique|hot)([^a-z0-9]|$)");
    }
    async Task<string> ResolveStreamAsync(Channel ch)
    {
        if(ch.StalkerCmd!=null&&_stalker!=null)return await _stalker.CreateLinkAsync(ch.StalkerCmd);
        return ch.StreamUrl;
    }
    async Task PlayAsync(Channel ch)
    {
        if(IsAdultChannel(ch)&&!ParentControlWindow.Authorize(this))
        {StatusText.Text="Chaîne bloquée par le contrôle parental.";ChannelList.SelectedItem=null;return;}
        if(ch.ContentType=="Series"&&!string.IsNullOrWhiteSpace(ch.SeriesId))
        {
            if(_xtream==null){StatusText.Text="La lecture des épisodes nécessite une connexion Xtream.";return;}
            try
            {
                StatusText.Text="Chargement des épisodes…";
                var episodes=await _xtream.GetSeriesEpisodesAsync(ch.SeriesId);
                var selected=ChooseEpisode(episodes,ch.Name);
                if(selected==null){StatusText.Text="Aucun épisode sélectionné.";return;}
                ch=selected;
            }
            catch(Exception ex){StatusText.Text="Impossible de charger les épisodes : "+ex.Message;return;}
        }
        if(string.IsNullOrWhiteSpace(ch.StreamUrl)&&string.IsNullOrWhiteSpace(ch.StalkerCmd))
        {StatusText.Text="Aucun flux disponible pour cet élément.";return;}
        _currentChannel=ch;_currentStreamUrl=null;NowName.Text=ch.Name;StatusText.Text="Connexion au flux…";
        BufferProgress.Value=0;BufferProgress.Visibility=Visibility.Visible;
        try
        {
            var url=await ResolveStreamAsync(ch);
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||(uri.Scheme!="http"&&uri.Scheme!="https"&&uri.Scheme!="rtsp"))
                throw new InvalidOperationException("Adresse de flux invalide.");
            _currentStreamUrl=url;_player.Stop();_currentMedia?.Dispose();
            var media=new Media(_lib,uri);
            media.AddOption(":network-caching=5000");media.AddOption(":live-caching=5000");media.AddOption(":file-caching=5000");
            media.AddOption(":http-reconnect");media.AddOption(":rtsp-tcp");media.AddOption(":no-video-title-show");
            if(ch.StalkerCmd!=null&&_stalker!=null)
            {media.AddOption(":http-referrer="+_stalker.PlaybackReferer);media.AddOption(":http-cookie="+_stalker.PlaybackCookie);}
            media.AddOption(":http-user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
            _currentMedia=media;
            if(!_player.Play(media))throw new InvalidOperationException("Le lecteur a refusé ce flux.");
            StatusText.Text="Connexion au flux…";
        }
        catch(Exception ex){BufferProgress.Visibility=Visibility.Collapsed;StatusText.Text="Échec de lecture : "+ex.Message;}
    }
    static string? FindMpv()
    {
        var candidates=new List<string>{Path.Combine(AppContext.BaseDirectory,"mpv.exe"),Path.Combine(AppContext.BaseDirectory,"tools","mpv","mpv.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"mpv","mpv.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"mpv","mpv.exe")};
        foreach(var dir in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries))candidates.Add(Path.Combine(dir,"mpv.exe"));
        return candidates.FirstOrDefault(File.Exists);
    }
    async void BtnExternalMpv_Click(object sender,RoutedEventArgs e)
    {
        var ch=_currentChannel;
        if(ch==null){StatusText.Text="Sélectionnez d'abord une chaîne.";return;}
        if(IsAdultChannel(ch)){StatusText.Text="Les chaînes classées comme adultes ne peuvent pas être ouvertes dans un lecteur externe.";return;}
        try
        {
            var url=await ResolveStreamAsync(ch);
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||(uri.Scheme!="http"&&uri.Scheme!="https"&&uri.Scheme!="rtsp"))throw new InvalidOperationException("Adresse de flux invalide.");
            var mpv=FindMpv();
            if(mpv==null){StatusText.Text="mpv introuvable. Installez mpv pour Windows ou placez mpv.exe à côté du programme.";return;}
            var start=new ProcessStartInfo(mpv){UseShellExecute=false};
            start.ArgumentList.Add("--cache=yes");start.ArgumentList.Add("--demuxer-readahead-secs=10");start.ArgumentList.Add("--network-timeout=30");start.ArgumentList.Add("--rtsp-transport=tcp");start.ArgumentList.Add("--force-window=yes");
            var ua=ch.StalkerCmd!=null&&_stalker!=null?_stalker.PlaybackUserAgent:"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36";
            var headers=new List<string>{"User-Agent: "+ua};
            if(ch.StalkerCmd!=null&&_stalker!=null){headers.Add("Referer: "+_stalker.PlaybackReferer);headers.Add("Cookie: "+_stalker.PlaybackCookie);}
            start.ArgumentList.Add("--http-header-fields="+string.Join(",",headers));
            start.ArgumentList.Add(url);Process.Start(start);StatusText.Text="Flux envoyé au lecteur mpv externe.";
        }
        catch(Exception ex){StatusText.Text="Impossible d'ouvrir mpv : "+ex.Message;}
    }
    Channel? ChooseEpisode(List<Channel> episodes,string seriesName)
    {
        if(episodes.Count==0)throw new InvalidOperationException("Cette série ne contient aucun épisode disponible.");
        var win=new Window{Title="Choisir un épisode — "+seriesName,Owner=this,Width=560,Height=620,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=System.Windows.Media.Brushes.White};
        var list=new ListBox{ItemsSource=episodes,DisplayMemberPath=nameof(Channel.Name),Margin=new Thickness(12)};
        var play=new Button{Content="Lire l'épisode",Padding=new Thickness(16,8,16,8),Margin=new Thickness(12),HorizontalAlignment=HorizontalAlignment.Right};
        var panel=new DockPanel();DockPanel.SetDock(play,Dock.Bottom);panel.Children.Add(play);panel.Children.Add(list);win.Content=panel;
        play.Click+=(_,_)=>{if(list.SelectedItem!=null)win.DialogResult=true;};
        list.MouseDoubleClick+=(_,_)=>{if(list.SelectedItem!=null)win.DialogResult=true;};
        return win.ShowDialog()==true?list.SelectedItem as Channel:null;
    }
    async Task OpenPlaylistsAsync()
    {
        var w=new PlaylistsWindow(_store){Owner=this};
        if(w.ShowDialog()==true&&w.SelectedSource!=null)
        {_store.LastUsedId=w.SelectedSource.Id;_store.Save();await LoadSourceAsync(w.SelectedSource);}
    }
    void ToggleFullscreen()
    {
        _full=!_full;TopBar.Visibility=InfoPanel.Visibility=_full?Visibility.Collapsed:Visibility.Visible;
        Col0.Width=new GridLength(_full?0:230);Col1.Width=new GridLength(_full?0:520);
        if(_full){WindowState=WindowState.Normal;WindowStyle=WindowStyle.None;WindowState=WindowState.Maximized;}
        else{WindowStyle=WindowStyle.SingleBorderWindow;WindowState=WindowState.Normal;}
    }
    void GroupList_SelectionChanged(object sender,SelectionChangedEventArgs e)=>Refresh();
    void SearchBox_TextChanged(object sender,TextChangedEventArgs e)=>Refresh();
    void SortBox_SelectionChanged(object sender,SelectionChangedEventArgs e)=>Refresh();
    async void ChannelList_SelectionChanged(object sender,SelectionChangedEventArgs e){if(ChannelList.SelectedItem is Channel ch)await PlayAsync(ch);}
    async void BtnPlaylists_Click(object sender,RoutedEventArgs e)=>await OpenPlaylistsAsync();
    void BtnWeather_Click(object sender,RoutedEventArgs e)=>new WeatherWindow{Owner=this}.ShowDialog();
    void BtnQuran_Click(object sender,RoutedEventArgs e)
    {
        _player.Stop();_currentMedia?.Dispose();_currentMedia=null;BufferProgress.Visibility=Visibility.Collapsed;
        StatusText.Text="Lecture TV arrêtée — lecture du Coran.";
        new QuranWindow{Owner=this}.ShowDialog();
        StatusText.Text="Lecture TV arrêtée.";
    }
    void PlayAdjacent(int delta)
    {
        var index=ChannelList.SelectedIndex;if(index<0)index=delta>0?-1:ChannelList.Items.Count;
        var next=index+delta;if(next>=0&&next<ChannelList.Items.Count)ChannelList.SelectedIndex=next;
    }
    void BtnPrevious_Click(object sender,RoutedEventArgs e)=>PlayAdjacent(-1);
    void BtnNext_Click(object sender,RoutedEventArgs e)=>PlayAdjacent(1);
    void BtnParental_Click(object sender,RoutedEventArgs e){if(ParentControlWindow.Configure(this))StatusText.Text="Contrôle parental configuré.";}
    void SetVolume(int volume){_player.Volume=Math.Clamp(volume,0,100);VolumeText.Text=$"{_player.Volume} %";if(_player.Volume>0)_player.Mute=false;}
    void BtnVolumeDown_Click(object sender,RoutedEventArgs e)=>SetVolume(_player.Volume-10);
    void BtnVolumeUp_Click(object sender,RoutedEventArgs e)=>SetVolume(_player.Volume+10);
    void BtnMute_Click(object sender,RoutedEventArgs e)=>_player.Mute=!_player.Mute;
    void BtnFull_Click(object sender,RoutedEventArgs e)=>ToggleFullscreen();
    void Window_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.F11||(e.Key==Key.Escape&&_full))ToggleFullscreen();}
}
