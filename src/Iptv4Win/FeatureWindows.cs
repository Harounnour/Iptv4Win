using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Iptv4Win;

public sealed class WeatherWindow : Window
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
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
        ("Tiznit",29.6974,-9.7316)
    };
    readonly ComboBox _city = new() { MinWidth=230, Margin=new Thickness(0,0,12,0) };
    readonly TextBlock _icon = new() { FontSize=76, HorizontalAlignment=HorizontalAlignment.Center };
    readonly TextBlock _result = new() { FontSize=18, TextWrapping=TextWrapping.Wrap, TextAlignment=TextAlignment.Center };
    readonly TextBlock _updated = new() { Foreground=Brushes.LightGray, Margin=new Thickness(0,14,0,0) };
    readonly DispatcherTimer _timer = new() { Interval=TimeSpan.FromMinutes(30) };
    public WeatherWindow()
    {
        Title="Météo du Maroc"; Width=540; Height=430; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(15,18,24)); Foreground=Brushes.White;
        foreach(var c in Cities) _city.Items.Add(c.Name);
        _city.SelectedItem="Rabat"; _city.SelectionChanged+=async (_,_)=>await RefreshWeather();
        var refresh=new Button { Content="Actualiser", Padding=new Thickness(18,9,18,9), Margin=new Thickness(0,0,0,12) };
        refresh.Click+=async (_,_)=>await RefreshWeather();
        var panel=new StackPanel { Margin=new Thickness(28), VerticalAlignment=VerticalAlignment.Center };
        panel.Children.Add(new TextBlock { Text="MÉTÉO • MAROC", FontSize=22, FontWeight=FontWeights.Bold, Margin=new Thickness(0,0,0,20) });
        panel.Children.Add(_city); panel.Children.Add(_icon); panel.Children.Add(_result); panel.Children.Add(refresh); panel.Children.Add(_updated);
        Content=panel;
        Loaded+=async (_,_)=>{_timer.Tick+=async (_,_)=>await RefreshWeather();_timer.Start();await RefreshWeather();};
        Closed+=(_,_)=>_timer.Stop();
    }
    async Task RefreshWeather()
    {
        var c=Cities.First(x=>x.Name==(string?)_city.SelectedItem);
        try
        {
            var url=$"https://api.open-meteo.com/v1/forecast?latitude={c.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={c.Lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m&timezone=auto";
            using var doc=JsonDocument.Parse(await Http.GetStringAsync(url));
            var current=doc.RootElement.GetProperty("current");
            var code=current.GetProperty("weather_code").GetInt32();
            var (emoji,desc)=Describe(code);
            var temp=current.GetProperty("temperature_2m").GetDouble();
            var feel=current.GetProperty("apparent_temperature").GetDouble();
            var humidity=current.GetProperty("relative_humidity_2m").GetInt32();
            var wind=current.GetProperty("wind_speed_10m").GetDouble();
            _icon.Text=emoji;
            _result.Text=$"{cityName}\n{temp:0} °C  •  {desc}\nRessenti {feel:0} °C  |  Humidité {humidity}%\nVent {wind:0} km/h";
            _updated.Text="Mise à jour : "+DateTime.Now.ToString("HH:mm");
        }
        catch { _result.Text="Données météo indisponibles. Vérifiez votre connexion."; }
    }
    static (string,string) Describe(int code)=>code switch
    {
        0=>("☀️","Ciel dégagé"),1 or 2=>("🌤️","Peu nuageux"),3=>("☁️","Couvert"),
        45 or 48=>("🌫️","Brouillard"),51 or 53 or 55 or 56 or 57=>("🌦️","Bruine"),
        61 or 63 or 65 or 66 or 67=>("🌧️","Pluie"),71 or 73 or 75 or 77=>("🌨️","Neige"),
        80 or 81 or 82=>("🌧️","Averses"),85 or 86=>("🌨️","Averses de neige"),
        95 or 96 or 99=>("⛈️","Orage"),_=>("🌡️","Conditions variables")
    };
}

public sealed class QuranWindow : Window
{
    static readonly HttpClient Http = new() { Timeout=TimeSpan.FromSeconds(25) };
    static readonly string[] Surahs = ("Al-Fatiha,Al-Baqara,Aal-Imran,An-Nisa,Al-Maida,Al-Anam,Al-Araf,Al-Anfal,At-Tawba,Yunus,Hud,Yusuf,Ar-Rad,Ibrahim,Al-Hijr,An-Nahl,Al-Isra,Al-Kahf,Maryam,Taha,Al-Anbiya,Al-Hajj,Al-Muminun,An-Nur,Al-Furqan,Ash-Shuara,An-Naml,Al-Qasas,Al-Ankabut,Ar-Rum,Luqman,As-Sajda,Al-Ahzab,Saba,Fatir,Ya-Sin,As-Saffat,Sad,Az-Zumar,Ghafir,Fussilat,Ash-Shura,Az-Zukhruf,Ad-Dukhan,Al-Jathiya,Al-Ahqaf,Muhammad,Al-Fath,Al-Hujurat,Qaf,Adh-Dhariyat,At-Tur,An-Najm,Al-Qamar,Ar-Rahman,Al-Waqia,Al-Hadid,Al-Mujadila,Al-Hashr,Al-Mumtahana,As-Saff,Al-Jumua,Al-Munafiqun,At-Taghabun,At-Talaq,At-Tahrim,Al-Mulk,Al-Qalam,Al-Haqqa,Al-Maarij,Nuh,Al-Jinn,Al-Muzzammil,Al-Muddaththir,Al-Qiyama,Al-Insan,Al-Mursalat,An-Naba,An-Naziat,Abasa,At-Takwir,Al-Infitar,Al-Mutaffifin,Al-Inshiqaq,Al-Buruj,At-Tariq,Al-Ala,Al-Ghashiya,Al-Fajr,Al-Balad,Ash-Shams,Al-Lail,Ad-Duha,Ash-Sharh,At-Tin,Al-Alaq,Al-Qadr,Al-Bayyina,Az-Zalzala,Al-Adiyat,Al-Qaria,At-Takathur,Al-Asr,Al-Humaza,Al-Fil,Quraysh,Al-Maun,Al-Kawthar,Al-Kafirun,An-Nasr,Al-Masad,Al-Ikhlas,Al-Falaq,An-Nas").Split(',');
    sealed record Reciter(string Name,string Server,string Surahs,string Image);
    readonly ComboBox _reciter=new(){MinWidth=300,Margin=new Thickness(0,4,0,12)};
    readonly ComboBox _surah=new(){MinWidth=300,Margin=new Thickness(0,4,0,12)};
    readonly Image _photo=new(){Width=112,Height=112,Stretch=Stretch.UniformToFill,Margin=new Thickness(0,8,0,8)};
    readonly TextBlock _status=new(){Text="Chargement de la liste des récitateurs…",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)};
    readonly MediaPlayer _audio=new();
    List<Reciter> _reciters=new();
    public QuranWindow()
    {
        Title="Quran Karim";Width=580;Height=560;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(15,18,24));Foreground=Brushes.White;
        var panel=new StackPanel{Margin=new Thickness(28)};
        panel.Children.Add(new TextBlock{Text="القرآن الكريم  •  Quran Karim",FontSize=23,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,16)});
        panel.Children.Add(new TextBlock{Text="Récitateur"});panel.Children.Add(_reciter);panel.Children.Add(_photo);
        panel.Children.Add(new TextBlock{Text="Sourate"});foreach(var x in Surahs)_surah.Items.Add(x);_surah.SelectedIndex=0;panel.Children.Add(_surah);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
        foreach(var (label,action) in new (string,Action)[]{("◀ Précédent",()=>Move(-1)),("▶ Lecture",Play),("Ⅱ Pause",()=>_audio.Pause()),("■ Arrêt",()=>_audio.Stop()),("Suivant ▶",()=>Move(1))})
        {var b=new Button{Content=label,Margin=new Thickness(4),Padding=new Thickness(12,9,12,9)};b.Click+=(_,_)=>action();buttons.Children.Add(b);}
        panel.Children.Add(buttons);panel.Children.Add(_status);Content=panel;
        _reciter.SelectionChanged+=(_,_)=>SelectReciter();
        Loaded+=async(_,_)=>await LoadReciters();
        Closed+=(_,_)=>{_audio.Stop();_audio.Close();};
    }
    async Task LoadReciters()
    {
        try
        {
            using var doc=JsonDocument.Parse(await Http.GetStringAsync("https://mp3quran.net/api/v3/reciters?language=ar"));
            if(!doc.RootElement.TryGetProperty("reciters",out var arr))throw new InvalidOperationException("Réponse API invalide");
            foreach(var x in arr.EnumerateArray())
            {
                var name=x.TryGetProperty("name",out var n)?n.GetString()??"": "";
                if(!x.TryGetProperty("moshaf",out var m)||m.GetArrayLength()==0)continue;
                var edition=m[0];var server=edition.TryGetProperty("server",out var sv)?sv.GetString()??"": "";
                var ids=edition.TryGetProperty("surah_list",out var sl)?sl.GetString()??"": "";
                var image=x.TryGetProperty("image",out var im)?im.GetString()??"": "";
                if(name.Length>0&&server.Length>0)_reciters.Add(new Reciter(name,server.TrimEnd('/'),ids,image));
            }
            _reciter.ItemsSource=_reciters.Select(x=>x.Name).ToList();
            if(_reciter.Items.Count>0)_reciter.SelectedIndex=0;
            _status.Text=$"{_reciters.Count} récitateurs disponibles • Catalogue en ligne";
        }
        catch{_status.Text="Impossible de charger le catalogue des récitateurs. Vérifiez votre connexion.";}
    }
    void SelectReciter()
    {
        var i=_reciter.SelectedIndex;if(i<0||i>=_reciters.Count)return;
        var image=_reciters[i].Image;
        try{if(Uri.TryCreate(image,UriKind.Absolute,out var u))_photo.Source=new BitmapImage(u);}
        catch{_photo.Source=null;}
        _status.Text=_reciters[i].Name;
    }
    void Move(int delta){var n=_surah.SelectedIndex+delta;if(n>=0&&n<Surahs.Length){_surah.SelectedIndex=n;Play();}}
    void Play()
    {
        var i=_reciter.SelectedIndex;var s=_surah.SelectedIndex+1;
        if(i<0){_status.Text="Choisissez un récitant.";return;}
        var r=_reciters[i];
        if(r.Surahs.Length>0&&!r.Surahs.Split(',').Contains(s.ToString())){_status.Text="Cette sourate n'est pas disponible pour ce récitant.";return;}
        var url=$"{r.Server}/{s:000}.mp3";
        try{_audio.Open(new Uri(url));_audio.Play();_status.Text=$"{r.Name} — {Surahs[s-1]}";}
        catch{_status.Text="Impossible de lancer cette récitation.";}
    }
}