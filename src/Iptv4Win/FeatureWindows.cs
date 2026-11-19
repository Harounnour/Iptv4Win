using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LibVLCSharp.Shared;

namespace Iptv4Win;

public sealed class WeatherPanel : UserControl
{
    static readonly HttpClient Http = new() { Timeout=TimeSpan.FromSeconds(20) };
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
    readonly ComboBox _city=new(){Width=250,Margin=new Thickness(0,8,12,8)};
    readonly TextBlock _icon=new(){FontSize=76,HorizontalAlignment=HorizontalAlignment.Center};
    readonly TextBlock _result=new(){FontSize=20,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,12,0,18)};
    readonly TextBlock _weekly=new(){FontSize=15,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)};
    readonly TextBlock _updated=new(){Foreground=Brushes.LightGray,Margin=new Thickness(0,8,0,0)};
    readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromMinutes(30)};
    public event Action? CloseRequested;
    public WeatherPanel()
    {
        Background=new SolidColorBrush(Color.FromRgb(15,18,24));Foreground=Brushes.White;
        foreach(var c in Cities)_city.Items.Add(c.Name);
        _city.SelectedItem="Rabat";_city.SelectionChanged+=async(_,_)=>await RefreshWeather();
        var close=new Button{Content="← Retour au programme",Padding=new Thickness(14,8,14,8),HorizontalAlignment=HorizontalAlignment.Left};
        close.Click+=(_,_)=>CloseRequested?.Invoke();
        var refresh=new Button{Content="Actualiser",Padding=new Thickness(16,8,16,8),Margin=new Thickness(0,12,0,0)};
        refresh.Click+=async(_,_)=>await RefreshWeather();
        var panel=new StackPanel{Margin=new Thickness(32),MaxWidth=900,HorizontalAlignment=HorizontalAlignment.Center};
        panel.Children.Add(close);
        panel.Children.Add(new TextBlock{Text="MÉTÉO • MAROC",FontSize=28,FontWeight=FontWeights.Bold,Margin=new Thickness(0,24,0,18)});
        panel.Children.Add(_city);panel.Children.Add(_icon);panel.Children.Add(_result);
        panel.Children.Add(new TextBlock{Text="Prévisions des 7 prochains jours",FontSize=19,FontWeight=FontWeights.SemiBold});
        panel.Children.Add(_weekly);panel.Children.Add(refresh);panel.Children.Add(_updated);
        Content=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=panel};
        Loaded+=async(_,_)=>{_timer.Start();await RefreshWeather();};
        Unloaded+=(_,_)=>_timer.Stop();
        _timer.Tick+=async(_,_)=>await RefreshWeather();
    }
    async Task RefreshWeather()
    {
        var c=Cities.FirstOrDefault(x=>x.Name==(string?)_city.SelectedItem);if(string.IsNullOrEmpty(c.Name))return;
        try
        {
            var ci=CultureInfo.InvariantCulture;
            var url=$"https://api.open-meteo.com/v1/forecast?latitude={c.Lat.ToString(ci)}&longitude={c.Lon.ToString(ci)}&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max&forecast_days=7&timezone=auto";
            using var doc=JsonDocument.Parse(await Http.GetStringAsync(url));var root=doc.RootElement;var current=root.GetProperty("current");
            var(emoji,desc)=Describe(current.GetProperty("weather_code").GetInt32());_icon.Text=emoji;
            _result.Text=$"{c.Name}\n{current.GetProperty("temperature_2m").GetDouble():0} °C • {desc}\nRessenti {current.GetProperty("apparent_temperature").GetDouble():0} °C | Humidité {current.GetProperty("relative_humidity_2m").GetInt32()}%\nVent {current.GetProperty("wind_speed_10m").GetDouble():0} km/h";
            var daily=root.GetProperty("daily");var lines=new List<string>();
            for(var i=0;i<daily.GetProperty("time").GetArrayLength();i++)
            {
                var date=DateTime.Parse(daily.GetProperty("time")[i].GetString()!,ci).ToString("ddd dd/MM",CultureInfo.GetCultureInfo("fr-FR"));
                var(sym,_)=Describe(daily.GetProperty("weather_code")[i].GetInt32());
                var rain=daily.GetProperty("precipitation_probability_max")[i].GetInt32();
                lines.Add($"{date,-13} {sym}  {daily.GetProperty("temperature_2m_max")[i].GetDouble():0}° / {daily.GetProperty("temperature_2m_min")[i].GetDouble():0}° • Pluie {rain}%");
            }
            _weekly.Text=string.Join("\n",lines);_updated.Text="Mise à jour : "+DateTime.Now.ToString("HH:mm");
        }
        catch{_result.Text="Données météo indisponibles. Vérifiez votre connexion.";_weekly.Text="Prévisions hebdomadaires indisponibles.";}
    }
    static (string,string) Describe(int code)=>code switch
    {
        0=>("☀️","Ciel dégagé"),1 or 2=>("🌤️","Peu nuageux"),3=>("☁️","Couvert"),45 or 48=>("🌫️","Brouillard"),
        51 or 53 or 55 or 56 or 57=>("🌦️","Bruine"),61 or 63 or 65 or 66 or 67=>("🌧️","Pluie"),
        71 or 73 or 75 or 77=>("🌨️","Neige"),80 or 81 or 82=>("🌧️","Averses"),85 or 86=>("🌨️","Averses de neige"),
        95 or 96 or 99=>("⛈️","Orage"),_=>("🌡️","Conditions variables")
    };
}

public sealed class QuranPanel : UserControl, IDisposable
{
    static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(25)};
    static readonly string[] Surahs=("الفاتحة,البقرة,آل عمران,النساء,المائدة,الأنعام,الأعراف,الأنفال,التوبة,يونس,هود,يوسف,الرعد,إبراهيم,الحجر,النحل,الإسراء,الكهف,مريم,طه,الأنبياء,الحج,المؤمنون,النور,الفرقان,الشعراء,النمل,القصص,العنكبوت,الروم,لقمان,السجدة,الأحزاب,سبأ,فاطر,يس,الصافات,ص,الزمر,غافر,فصلت,الشورى,الزخرف,الدخان,الجاثية,الأحقاف,محمد,الفتح,الحجرات,ق,الذاريات,الطور,النجم,القمر,الرحمن,الواقعة,الحديد,المجادلة,الحشر,الممتحنة,الصف,الجمعة,المنافقون,التغابن,الطلاق,التحريم,الملك,القلم,الحاقة,المعارج,نوح,الجن,المزمل,المدثر,القيامة,الإنسان,المرسلات,النبأ,النازعات,عبس,التكوير,الانفطار,المطففين,الانشقاق,البروج,الطارق,الأعلى,الغاشية,الفجر,البلد,الشمس,الليل,الضحى,الشرح,التين,العلق,القدر,البينة,الزلزلة,العاديات,القارعة,التكاثر,العصر,الهمزة,الفيل,قريش,الماعون,الكوثر,الكافرون,النصر,المسد,الإخلاص,الفلق,الناس").Split(',');
    sealed record Reciter(int Id,string Name,string Style);
    sealed record TimedVerse(int Number,int Start,int End,string Text);
    readonly ComboBox _reciter=new(){MinWidth=310,Margin=new Thickness(0,6,12,12)};
    readonly ComboBox _surah=new(){MinWidth=310,Margin=new Thickness(0,6,12,12)};
    readonly TextBlock _verse=new(){FontSize=30,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(247,211,125)),Margin=new Thickness(24,28,24,22),FlowDirection=FlowDirection.RightToLeft};
    readonly TextBlock _verseNumber=new(){FontSize=16,TextAlignment=TextAlignment.Center,Foreground=Brushes.LightGray};
    readonly TextBlock _status=new(){FontSize=14,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0),Foreground=Brushes.LightGray};
    readonly LibVLC _lib=new("--no-video","--quiet","--network-caching=1500","--file-caching=1500");
    readonly MediaPlayer _audio;
    readonly DispatcherTimer _sync=new(){Interval=TimeSpan.FromMilliseconds(250)};
    Media? _media;
    List<Reciter> _reciters=new();
    List<TimedVerse> _verses=new();
    int _lastVerse=-1;
    bool _disposed;
    public event Action? CloseRequested;
    public QuranPanel()
    {
        _audio=new MediaPlayer(_lib){Volume=100,Mute=false};
        Background=new SolidColorBrush(Color.FromRgb(15,18,24));Foreground=Brushes.White;
        for(var i=0;i<Surahs.Length;i++)_surah.Items.Add($"{i+1:000} — {Surahs[i]}");
        _surah.SelectedIndex=0;
        var close=new Button{Content="← Retour au programme",Padding=new Thickness(14,8,14,8),HorizontalAlignment=HorizontalAlignment.Left};
        close.Click+=(_,_)=>CloseRequested?.Invoke();
        var buttons=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};
        AddButton(buttons,"◀ السابق",()=>Move(-1));AddButton(buttons,"▶ تشغيل",()=>PlaySelected());AddButton(buttons,"Ⅱ إيقاف مؤقت",()=>_audio.Pause());AddButton(buttons,"■ إيقاف",Stop);AddButton(buttons,"التالي ▶",()=>Move(1));
        var panel=new StackPanel{Margin=new Thickness(30),MaxWidth=1100,HorizontalAlignment=HorizontalAlignment.Center};
        panel.Children.Add(close);
        panel.Children.Add(new TextBlock{Text="القرآن الكريم",FontSize=30,FontWeight=FontWeights.Bold,Margin=new Thickness(0,22,0,18),TextAlignment=TextAlignment.Center});
        panel.Children.Add(new TextBlock{Text="القارئ",FontSize=16});panel.Children.Add(_reciter);
        panel.Children.Add(new TextBlock{Text="السورة",FontSize=16});panel.Children.Add(_surah);
        panel.Children.Add(_verseNumber);panel.Children.Add(_verse);panel.Children.Add(buttons);panel.Children.Add(_status);
        Content=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=panel};
        _reciter.SelectionChanged+=async(_,_)=>await LoadSelectedSurah();
        _surah.SelectionChanged+=async(_,_)=>{if(IsLoaded&&_reciter.SelectedItem!=null)await LoadSelectedSurah();};
        _sync.Tick+=(_,_)=>SyncVerse();
        _audio.Playing+=(_,_)=>Dispatcher.BeginInvoke(()=>_status.Text="التلاوة جارية — يتم تحديث الآية تلقائياً");
        _audio.EncounteredError+=(_,_)=>Dispatcher.BeginInvoke(()=>_status.Text="تعذر تشغيل التلاوة. تحقق من الاتصال أو جرّب قارئاً آخر.");
        Loaded+=async(_,_)=>await LoadReciters();
        Unloaded+=(_,_)=>Stop();
    }
    static void AddButton(Panel panel,string label,Action action){var b=new Button{Content=label,Padding=new Thickness(14,9,14,9),Margin=new Thickness(5)};b.Click+=(_,_)=>action();panel.Children.Add(b);}
    async Task LoadReciters()
    {
        try
        {
            using var doc=JsonDocument.Parse(await Http.GetStringAsync("https://api.quran.com/api/v4/resources/recitations?language=ar"));
            if(!doc.RootElement.TryGetProperty("recitations",out var arr)||arr.ValueKind!=JsonValueKind.Array)throw new InvalidOperationException("Réponse API inattendue");
            _reciters=arr.EnumerateArray().Select(x=>new Reciter(x.GetProperty("id").GetInt32(),x.TryGetProperty("reciter_name",out var n)?n.GetString()??"Récitant":"Récitant",x.TryGetProperty("style",out var s)?s.GetString()??"": "")).ToList();
            _reciter.ItemsSource=_reciters;_reciter.DisplayMemberPath=nameof(Reciter.Name);
            if(_reciters.Count>0)_reciter.SelectedIndex=0;
            _status.Text=$"{_reciters.Count} récitateurs disponibles • Synchronisation des versets par horodatage audio.";
        }
        catch(Exception ex){_status.Text="تعذر تحميل القراء من API القرآن: "+ex.Message;}
    }
    async Task LoadSelectedSurah()
    {
        if(_reciter.SelectedItem is not Reciter reciter||_surah.SelectedIndex<0)return;
        Stop();_status.Text="جاري تحميل الآيات وتوقيت التلاوة…";
        try
        {
            var chapter=_surah.SelectedIndex+1;
            using var verseDoc=JsonDocument.Parse(await Http.GetStringAsync($"https://api.quran.com/api/v4/verses/by_chapter/{chapter}?language=ar&fields=text_uthmani&per_page=300"));
            var verseArray=verseDoc.RootElement.GetProperty("verses");
            var texts=verseArray.EnumerateArray().ToDictionary(v=>v.GetProperty("verse_number").GetInt32(),v=>v.GetProperty("text_uthmani").GetString()??"");
            using var audioDoc=JsonDocument.Parse(await Http.GetStringAsync($"https://api.quran.com/api/v4/chapter_recitations/{reciter.Id}/{chapter}?segments=true"));
            var root=audioDoc.RootElement;
            var audioUrl=root.TryGetProperty("audio_file",out var file)&&file.TryGetProperty("audio_url",out var au)?au.GetString()??"":root.TryGetProperty("audio_url",out var alt)?alt.GetString()??"":"";
            if(!Uri.TryCreate(audioUrl,UriKind.Absolute,out var uri)||(uri.Scheme!="https"&&uri.Scheme!="http"))throw new InvalidOperationException("URL audio absente.");
            if(!root.TryGetProperty("verse_timings",out var timings)||timings.ValueKind!=JsonValueKind.Array)throw new InvalidOperationException("Ce flux ne fournit pas les horodatages des versets.");
            _verses=timings.EnumerateArray().Select(t=>
            {
                var key=t.GetProperty("verse_key").GetString()??"";
                var n=int.TryParse(key.Split(':').Last(),out var num)?num:0;
                return new TimedVerse(n,t.GetProperty("timestamp_from").GetInt32(),t.GetProperty("timestamp_to").GetInt32(),texts.GetValueOrDefault(n,""));
            }).Where(v=>v.Number>0&&!string.IsNullOrWhiteSpace(v.Text)).OrderBy(v=>v.Start).ToList();
            if(_verses.Count==0)throw new InvalidOperationException("Aucun horodatage de verset exploitable pour cette récitation.");
            _media?.Dispose();_media=new Media(_lib,uri);_media.AddOption(":network-caching=1500");_media.AddOption(":http-reconnect");
            _lastVerse=-1;_verse.Text="استعد للاستماع إلى التلاوة";_verseNumber.Text="";
            if(!_audio.Play(_media))throw new InvalidOperationException("Le lecteur audio a refusé le flux.");
            _sync.Start();_status.Text=$"التلاوة: {reciter.Name} — {Surahs[chapter-1]}";
        }
        catch(Exception ex){_sync.Stop();_status.Text="تعذر تحميل الآيات أو توقيت التلاوة: "+ex.Message;}
    }
    void SyncVerse()
    {
        if(_verses.Count==0||_audio.State!= VLCState.Playing)return;
        var time=(int)Math.Max(0,_audio.Time);
        var index=_verses.FindLastIndex(v=>v.Start<=time);
        if(index<0||index==_lastVerse)return;
        _lastVerse=index;var v=_verses[index];
        _verse.Text=v.Text;_verseNumber.Text=$"الآية {v.Number} من سورة {Surahs[_surah.SelectedIndex]}";
    }
    async void PlaySelected(){if(_media==null)await LoadSelectedSurah();else{_audio.Play();_sync.Start();}}
    void Move(int delta){var next=_surah.SelectedIndex+delta;if(next>=0&&next<Surahs.Length)_surah.SelectedIndex=next;}
    void Stop(){_sync.Stop();_audio.Stop();_lastVerse=-1;}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Stop();_media?.Dispose();_audio.Dispose();_lib.Dispose();
    }
}
