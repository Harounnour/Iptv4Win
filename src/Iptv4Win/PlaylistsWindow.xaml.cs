using System.Windows;
using System.Windows.Controls;
using Iptv4Win.Models;
using Iptv4Win.Services;

namespace Iptv4Win;

public partial class PlaylistsWindow : Window
{
    readonly PlaylistStore _store;
    PlaylistSource _cur = new();
    public PlaylistSource? SelectedSource { get; private set; }

    public PlaylistsWindow(PlaylistStore store)
    {
        InitializeComponent(); _store=store; RefreshList(); Bind(_cur);
    }
    void RefreshList() { SavedList.ItemsSource=null; SavedList.ItemsSource=_store.Items; }
    void Bind(PlaylistSource s)
    {
        _cur=s; TxtName.Text=s.Name; TxtUrl.Text=s.Url; TxtUser.Text=s.Username; TxtPass.Password=s.Password;
        TxtMac.Text=s.Mac; TxtEpg.Text=s.EpgUrl; TxtFootballApi.Password=s.FootballDataApiKey;
        RbM3u.IsChecked=s.Type==PlaylistType.M3U; RbXtream.IsChecked=s.Type==PlaylistType.Xtream; RbStalker.IsChecked=s.Type==PlaylistType.Stalker;
        ApplyType(); Status.Text="";
    }
    PlaylistType CurrentType() => RbXtream.IsChecked==true ? PlaylistType.Xtream : RbStalker.IsChecked==true ? PlaylistType.Stalker : PlaylistType.M3U;
    PlaylistSource Read() => new() { Id=_cur.Id, Name=TxtName.Text.Trim(), Type=CurrentType(), Url=TxtUrl.Text.Trim(),
        Username=TxtUser.Text.Trim(), Password=TxtPass.Password, Mac=TxtMac.Text.Trim(), EpgUrl=TxtEpg.Text.Trim(),
        FootballDataApiKey=TxtFootballApi.Password };
    void ApplyType()
    {
        if (XtreamPanel==null || StalkerPanel==null || EpgPanel==null || LblUrl==null) return;
        var t=CurrentType(); XtreamPanel.Visibility=t==PlaylistType.Xtream?Visibility.Visible:Visibility.Collapsed;
        StalkerPanel.Visibility=t==PlaylistType.Stalker?Visibility.Visible:Visibility.Collapsed;
        EpgPanel.Visibility=t==PlaylistType.Stalker?Visibility.Collapsed:Visibility.Visible;
        LblUrl.Text=t switch { PlaylistType.Xtream=>"Adresse du serveur (http://serveur:port)", PlaylistType.Stalker=>"Adresse du portail (http://serveur/c/)", _=>"URL de la playlist M3U ou chemin du fichier" };
    }
    bool Valid(PlaylistSource s)
    {
        if(s.Name.Length==0 || s.Url.Length==0){Status.Text="Le nom et l'adresse sont obligatoires.";return false;}
        if(s.Type==PlaylistType.Xtream && (s.Username.Length==0||s.Password.Length==0)){Status.Text="Nom d'utilisateur et mot de passe obligatoires.";return false;}
        if(s.Type==PlaylistType.Stalker && s.Mac.Length==0){Status.Text="L'adresse MAC est obligatoire.";return false;}
        return true;
    }
    bool SaveCurrent()
    {
        var s=Read(); if(!Valid(s)) return false;
        var i=_store.Items.FindIndex(x=>x.Id==s.Id); if(i>=0)_store.Items[i]=s;else _store.Items.Add(s);
        _cur=s; _store.Save(); RefreshList(); return true;
    }
    void Type_Checked(object sender,RoutedEventArgs e)=>ApplyType();
    void Saved_SelectionChanged(object sender,SelectionChangedEventArgs e){if(SavedList.SelectedItem is PlaylistSource s)Bind(s);}
    void Add_Click(object sender,RoutedEventArgs e){SavedList.SelectedItem=null;Bind(new PlaylistSource());}
    async void Test_Click(object sender,RoutedEventArgs e)
    {
        var s=Read();if(!Valid(s))return;Status.Text="Test en cours…";
        try{var r=await ChannelLoader.LoadAsync(s);Status.Text=$"OK : {r.Channels.Count} chaînes trouvées.";}
        catch(Exception ex){Status.Text="Échec : "+ex.Message;}
    }
    void Save_Click(object sender,RoutedEventArgs e){if(SaveCurrent())Status.Text="Enregistré.";}
    void Use_Click(object sender,RoutedEventArgs e){if(!SaveCurrent())return;SelectedSource=_cur;DialogResult=true;}
    void Delete_Click(object sender,RoutedEventArgs e){_store.Items.RemoveAll(x=>x.Id==_cur.Id);_store.Save();RefreshList();Bind(new PlaylistSource());}
}