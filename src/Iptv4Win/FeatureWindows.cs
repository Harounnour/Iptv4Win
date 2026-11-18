using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibVLCSharp.Shared;

namespace Iptv4Win;

public sealed class WeatherWindow : Window
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    static readonly (string Name,double Lat,double Lon)[] Cities =
    {
        ("Agadir",30.4278,-9.5981),("Al Hoceïma",35.2517,-3.9372),("Azrou",33.4344,-5.2213),
        ("Béni Mellal",32.3373,-6.3498),("Berkane",34.9204,-2.3194),("Casablanca",33.5731,-7.5898),
        ("Chefchaouen",35.1688,-5.2636),("Dakhla",23.6848,-15.9570),("El Jadida",33.2316,-8.5007),
        ("Errachidia",31.9314,-4.4266),("Essaouira",31.5085,-9.7595),("Fès",34.0331,-5.0003),
        ("Guelmim",28.9870,-10.0574),("Ifrane",33.5228,-5.1109),("Kénitra",34.2610,-6.5802),
        ("Khémisset",33.8240,-6.0663),("Khénifra",32.9391,-5.6675),("Khouribga",32.8811,-6.9063),
        ("Laâyoune",27.1536,-13.2033),("Larache",35.1932,-6.1561),("Marrakech",31.6295,-7.9811),
        ("Meknès",33.8935,-5.5473),("Nador",35.1681,-2.9335),("Ouarzazate",30.9335,-6.9370),
        ("Oujda",34.6814,-1.9086),("Rabat",34.0209,-6.8416),("Safi",32.2994,-9.2372),
        ("Salé",34.0531,-6.7985),("Tanger",35.7595,-5.8340),("Tétouan",35.5785,-5.3684),
        ("Tiflet",33.8940,-6.3060),("Tiznit",29.6974,-9.7316)
    };
    readonly ComboBox _city = new() { MinWidth=230, Margin=new Thickness(0,0,12,0) };
    readonly TextBlock _icon = new() { FontSize=64, HorizontalAlignment=HorizontalAlignment.Center };
    readonly TextBlock _result = new() { FontSize=17, TextWrapping=TextWrapping.Wrap, TextAlignment=TextAlignment.Center };
    readonly TextBlock _weekly = new() { FontSize=14, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,14,0,0) };
    readonly TextBlock _updated = new() { Foreground=Brushes.LightGray, Margin=new Thickness(0,12,0,0) };
    readonly DispatcherTimer _timer = new() { Interval=TimeSpan.FromMinutes(30) };
    public WeatherWindow()
    {
        Title="Météo du Maroc"; Width=600; Height=690; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(15,18,24)); Foreground=Brushes.White;
        foreach(var c in Cities) _city.Items.Add(c.Name);
        _city.SelectedItem="Rabat"; _city.SelectionChanged+=async (_,_)=>await RefreshWeather();
        var refresh=new Button { Content="Actualiser", Padding=new Thickness(18,9,18,9), Margin=new Thickness(0,12,0,0) };
        refresh.Click+=async (_,_)=>await RefreshWeather();
        var panel=new StackPanel { Margin=new Thickness(26) };
        panel.Children.Add(new TextBlock { Text="MÉTÉO • MAROC", FontSize=22, FontWeight=FontWeights.Bold, Margin=new Thickness(0,0,0,16) });
        panel.Children.Add(_city); panel.Children.Add(_icon); panel.Children.Add(_result);
        panel.Children.Add(new TextBlock { Text="Prévisions des 7 prochains jours", FontSize=17, FontWeight=FontWeights.SemiBold, Margin=new Thickness(0,18,0,4) });
        panel.Children.Add(_weekly); panel.Children.Add(refresh); panel.Children.Add(_updated);
        Content=new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto, Content=panel };
        Loaded+=async (_,_)=>{_timer.Tick+=async (_,_)=>await RefreshWeather();_timer.Start();await RefreshWeather();};
        Closed+=(_,_)=>_timer.Stop();
    }
    async Task RefreshWeather()
    {
        var c=Cities.FirstOrDefault(x=>x.Name==(string?)_city.SelectedItem);
        if(string.IsNullOrEmpty(c.Name)) return;
        try
        {
            var ci=CultureInfo.InvariantCulture;
            var url=$"https://api.open-meteo.com/v1/forecast?latitude={c.Lat.ToString(ci)}&longitude={c.Lon.ToString(ci)}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,wind_speed_10m_max&forecast_days=7&timezone=auto";
            using var doc=JsonDocument.Parse(await Http.GetStringAsync(url));
            var root=doc.RootElement; var current=root.GetProperty("current");
            var (emoji,desc)=Describe(current.GetProperty("weather_code").GetInt32());
            _icon.Text=emoji;
            _result.Text=$"{c.Name}\n{current.GetProperty("temperature_2m").GetDouble():0} °C • {desc}\nRessenti {current.GetProperty("apparent_temperature").GetDouble():0} °C | Humidité {current.GetProperty("relative_humidity_2m").GetInt32()}%\nVent {current.GetProperty("wind_speed_10m").GetDouble():0} km/h";
            var daily=root.GetProperty("daily");
            var dates=daily.GetProperty("time"); var codes=daily.GetProperty("weather_code");
            var max=daily.GetProperty("temperature_2m_max"); var min=daily.GetProperty("temperature_2m_min");
            var rain=daily.GetProperty("precipitation_probability_max");
            var lines=new List<string>();
            for(var i=0;i<dates.GetArrayLength();i++)
            {
                var day=DateTime.Parse(dates[i].GetString()!,ci).ToString("ddd dd/MM",CultureInfo.GetCultureInfo("fr-FR"));
                var (symbol,description)=Describe(codes[i].GetInt32());
                var rainValue=rain[i].ValueKind==JsonValueKind.Number?rain[i].GetInt32():0;
                lines.Add($"{day,-12} {symbol}  {max[i].GetDouble():0}° / {min[i].GetDouble():0}°  • Pluie {rainValue}%");
            }
            _weekly.Text=string.Join("\n",lines);
            _updated.Text="Mise à jour : "+DateTime.Now.ToString("HH:mm");
        }
        catch(Exception ex)
        {
            _result.Text="Données météo indisponibles. Vérifiez votre connexion.";
            _weekly.Text="Prévisions hebdomadaires indisponibles.";
            _updated.Text=ex is HttpRequestException?"Connexion au service météo impossible.":"Erreur lors de la lecture des prévisions.";
        }
    }
    static (string,string) Describe(int code)=>code switch
    {
        0=>("☀️","Ciel dégagé"),1 or 2=>("🌤️","Peu nuageux"),3=>("☁️","Couvert"),
        45 or 48=>("🌫️","Brouillard"),51 or 53 or 55 or 56 or 57=>("🌦️","Bruine"),
        61 or 63 or 65 or 66 or 67=>("🌧️","Pluie"),71 or 73 or 75 or 77=>("🌨️","Neige"),
        80 or 81 or 82=>("🌧️","Averses"),85 or 86=>("🌨️","Averses de neige"),
        95 or 96 or 99=>("⛈️","Orage"),_=>( "🌡️","Conditions variables")
    };
}

public sealed class QuranWindow : Window
{
    static readonly HttpClient Http = new() { Timeout=TimeSpan.FromSeconds(25) };
    static readonly string[] Surahs = ("Al-Fatiha,Al-Baqara,Aal-Imran,An-Nisa,Al-Maida,Al-Anam,Al-Araf,Al-Anfal,At-Tawba,Yunus,Hud,Yusuf,Ar-Rad,Ibrahim,Al-Hijr,An-Nahl,Al-Isra,Al-Kahf,Maryam,Taha,Al-Anbiya,Al-Hajj,Al-Muminun,An-Nur,Al-Furqan,Ash-Shuara,An-Naml,Al-Qasas,Al-Ankabut,Ar-Rum,Luqman,As-Sajda,Al-Ahzab,Saba,Fatir,Ya-Sin,As-Saffat,Sad,Az-Zumar,Ghafir,Fussilat,Ash-Shura,Az-Zukhruf,Ad-Dukhan,Al-Jathiya,Al-Ahqaf,Muhammad,Al-Fath,Al-Hujurat,Qaf,Adh-Dhariyat,At-Tur,An-Najm,Al-Qamar,Ar-Rahman,Al-Waqia,Al-Hadid,Al-Mujadila,Al-Hashr,Al-Mumtahana,As-Saff,Al-Jumua,Al-Munafiqun,At-Taghabun,At-Talaq,At-Tahrim,Al-Mulk,Al-Qalam,Al-Haqqa,Al-Maarij,Nuh,Al-Jinn,Al-Muzzammil,Al-Muddaththir,Al-Qiyama,Al-Insan,Al-Mursalat,An-Naba,An-Naziat,Abasa,At-Takwir,Al-Infitar,Al-Mutaffifin,Al-Inshiqaq,Al-Buruj,At-Tariq,Al-Ala,Al-Ghashiya,Al-Fajr,Al-Balad,Ash-Shams,Al-Lail,Ad-Duha,Ash-Sharh,At-Tin,Al-Alaq,Al-Qadr,Al-Bayyina,Az-Zalzala,Al-Adiyat,Al-Qaria,At-Takathur,Al-Asr,Al-Humaza,Al-Fil,Quraysh,Al-Maun,Al-Kawthar,Al-Kafirun,An-Nasr,Al-Masad,Al-Ikhlas,Al-Falaq,An-Nas").Split(',');
    sealed record Reciter(string Name,string Server,HashSet<int> Surahs,string Image);
    readonly ComboBox _reciter=new(){MinWidth=300,Margin=new Thickness(0,4,0,12)};
    readonly ComboBox _surah=new(){MinWidth=300,Margin=new Thickness(0,4,0,12)};
    readonly Image _photo=new(){Width=100,Height=100,Stretch=Stretch.UniformToFill,Margin=new Thickness(0,8,0,8)};
    readonly TextBlock _status=new(){Text="Chargement de la liste des récitateurs…",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)};
    readonly LibVLC _lib=new("--no-video","--quiet","--network-caching=1500","--file-caching=1500");
    readonly LibVLCSharp.Shared.MediaPlayer _audio;
    Media? _audioMedia;
    List<Reciter> _reciters=new();
    public QuranWindow()
    {
        _audio=new LibVLCSharp.Shared.MediaPlayer(_lib) { Volume=100, Mute=false };
        Title="Quran Karim";Width=590;Height=590;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(15,18,24));Foreground=Brushes.White;
        var panel=new StackPanel{Margin=new Thickness(28)};
        panel.Children.Add(new TextBlock{Text="القرآن الكريم  •  Quran Karim",FontSize=23,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,16)});
        panel.Children.Add(new TextBlock{Text="Récitateur"});panel.Children.Add(_reciter);panel.Children.Add(_photo);
        panel.Children.Add(new TextBlock{Text="Sourate"});foreach(var x in Surahs)_surah.Items.Add(x);_surah.SelectedIndex=0;panel.Children.Add(_surah);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
        foreach(var (label,action) in new (string,Action)[]{("◀ Précédent",()=>Move(-1)),("▶ Lecture",Play),("Ⅱ Pause",()=>_audio.Pause()),("■ Arrêt",Stop),("Suivant ▶",()=>Move(1))})
        {var b=new Button{Content=label,Margin=new Thickness(4),Padding=new Thickness(12,9,12,9)};b.Click+=(_,_)=>action();buttons.Children.Add(b);}
        panel.Children.Add(buttons);panel.Children.Add(_status);Content=panel;
        _reciter.SelectionChanged+=(_,_)=>SelectReciter();
        _audio.Playing+=(_,_)=>Dispatcher.BeginInvoke(()=>_status.Text="Lecture audio en cours");
        _audio.EncounteredError+=(_,_)=>Dispatcher.BeginInvoke(()=>_status.Text="Erreur audio : le serveur a refusé le flux ou le format est indisponible.");
        _audio.EndReached+=(_,_)=>Dispatcher.BeginInvoke(()=>_status.Text="Fin de la sourate.");
        Loaded+=async(_,_)=>await LoadReciters();
        Closed+=(_,_)=>{_audio.Stop();_audioMedia?.Dispose();_audio.Dispose();_lib.Dispose();};
    }
    async Task LoadReciters()
    {
        try
        {
            using var response=await Http.GetAsync("https://mp3quran.net/api/v3/reciters?language=ar");
            response.EnsureSuccessStatusCode();
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if(!doc.RootElement.TryGetProperty("reciters",out var arr)||arr.ValueKind!=JsonValueKind.Array)throw new InvalidOperationException("Réponse API invalide");
            foreach(var x in arr.EnumerateArray())
            {
                var name=x.TryGetProperty("name",out var n)?n.GetString()??"": "";
                if(!x.TryGetProperty("moshaf",out var m)||m.ValueKind!=JsonValueKind.Array)continue;
                foreach(var edition in m.EnumerateArray())
                {
                    var server=edition.TryGetProperty("server",out var sv)?sv.GetString()??"": "";
                    var ids=edition.TryGetProperty("surah_list",out var sl)?sl.GetString()??"": "";
                    var image=x.TryGetProperty("image",out var im)?im.GetString()??"": "";
                    if(name.Length==0||!Uri.TryCreate(server,UriKind.Absolute,out var baseUri)||(baseUri.Scheme!="https"&&baseUri.Scheme!="http"))continue;
                    var available=ids.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
                        .Select(v=>int.TryParse(v,out var number)?number:0).Where(v=>v>=1&&v<=114).ToHashSet();
                    if(available.Count==0&&edition.TryGetProperty("surah_total",out var total)&&total.GetInt32()==114)
                        available=Enumerable.Range(1,114).ToHashSet();
                    _reciters.Add(new Reciter(name,server.TrimEnd('/'),available,image));
                }
            }
            _reciter.ItemsSource=_reciters;_reciter.DisplayMemberPath=nameof(Reciter.Name);
            if(_reciter.Items.Count>0)_reciter.SelectedIndex=0;
            _status.Text=$"{_reciters.Count} versions de récitation disponibles • Catalogue MP3 Quran";
        }
        catch(Exception ex){_status.Text="Impossible de charger le catalogue : "+ex.Message;}
    }
    void SelectReciter()
    {
        if(_reciter.SelectedItem is not Reciter r)return;
        _photo.Source=null;
        try
        {
            if(Uri.TryCreate(r.Image,UriKind.Absolute,out var u))
            {var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=u;bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.EndInit();bitmap.Freeze();_photo.Source=bitmap;}
        }
        catch{_photo.Source=null;}
        _status.Text=r.Name;
    }
    void Move(int delta){var n=_surah.SelectedIndex+delta;if(n>=0&&n<Surahs.Length){_surah.SelectedIndex=n;Play();}}
    async void Play()
    {
        if(_reciter.SelectedItem is not Reciter r){_status.Text="Choisissez un récitant.";return;}
        var s=_surah.SelectedIndex+1;
        if(r.Surahs.Count>0&&!r.Surahs.Contains(s)){_status.Text="Cette sourate n'est pas disponible pour ce récitant.";return;}
        var url=$"{r.Server}/{s:000}.mp3";
        try
        {
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri))throw new InvalidOperationException("URL audio invalide.");
            _status.Text="Vérification et chargement de la sourate…";
            using var request=new HttpRequestMessage(HttpMethod.Get,uri);
            request.Headers.Range=new RangeHeaderValue(0,0);
            using var response=await Http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead);
            if(response.StatusCode!=HttpStatusCode.PartialContent&&!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Le serveur audio a répondu {(int)response.StatusCode}.");
            _audio.Stop();_audioMedia?.Dispose();
            _audioMedia=new Media(_lib,uri);
            _audioMedia.AddOption(":network-caching=1500");
            _audioMedia.AddOption(":http-reconnect");
            if(!_audio.Play(_audioMedia))throw new InvalidOperationException("Le lecteur VLC n'a pas accepté le fichier audio.");
            _status.Text=$"Chargement : {r.Name} — {Surahs[s-1]}";
        }
        catch(Exception ex){_status.Text="Échec du chargement audio : "+ex.Message;}
    }
    void Stop(){_audio.Stop();_status.Text="Lecture arrêtée.";}
}
