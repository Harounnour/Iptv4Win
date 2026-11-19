using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;
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
        var back=new Button{Content="←",FontSize=22,FontWeight=FontWeights.Bold,Padding=new Thickness(16,7,16,7),MinWidth=52,HorizontalAlignment=HorizontalAlignment.Left,ToolTip="Retour à la liste des chaînes"};
        back.Click+=(_,_)=>CloseRequested?.Invoke();
        var header=new Grid{Margin=new Thickness(18,12,18,0)};
        header.Children.Add(back);
        var refresh=new Button{Content="Actualiser",Padding=new Thickness(16,8,16,8),Margin=new Thickness(0,12,0,0)};
        refresh.Click+=async(_,_)=>await RefreshWeather();
        var panel=new StackPanel{Margin=new Thickness(32,12,32,32),MaxWidth=900,HorizontalAlignment=HorizontalAlignment.Center};
        panel.Children.Add(new TextBlock{Text="MÉTÉO • MAROC",FontSize=28,FontWeight=FontWeights.Bold,Margin=new Thickness(0,12,0,18)});
        panel.Children.Add(_city);panel.Children.Add(_icon);panel.Children.Add(_result);
        panel.Children.Add(new TextBlock{Text="Prévisions des 7 prochains jours",FontSize=19,FontWeight=FontWeights.SemiBold});
        panel.Children.Add(_weekly);panel.Children.Add(refresh);panel.Children.Add(_updated);
        var layout=new Grid();
        layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
        layout.Children.Add(header);
        var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=panel};
        Grid.SetRow(scroll,1);layout.Children.Add(scroll);
        Content=layout;
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
    static readonly HttpClient Http = CreateHttpClient();
    static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IPTV4Win/1.0 (Windows; Quran audio player)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    static readonly string[] Surahs=("الفاتحة,البقرة,آل عمران,النساء,المائدة,الأنعام,الأعراف,الأنفال,التوبة,يونس,هود,يوسف,الرعد,إبراهيم,الحجر,النحل,الإسراء,الكهف,مريم,طه,الأنبياء,الحج,المؤمنون,النور,الفرقان,الشعراء,النمل,القصص,العنكبوت,الروم,لقمان,السجدة,الأحزاب,سبأ,فاطر,يس,الصافات,ص,الزمر,غافر,فصلت,الشورى,الزخرف,الدخان,الجاثية,الأحقاف,محمد,الفتح,الحجرات,ق,الذاريات,الطور,النجم,القمر,الرحمن,الواقعة,الحديد,المجادلة,الحشر,الممتحنة,الصف,الجمعة,المنافقون,التغابن,الطلاق,التحريم,الملك,القلم,الحاقة,المعارج,نوح,الجن,المزمل,المدثر,القيامة,الإنسان,المرسلات,النبأ,النازعات,عبس,التكوير,الانفطار,المطففين,الانشقاق,البروج,الطارق,الأعلى,الغاشية,الفجر,البلد,الشمس,الليل,الضحى,الشرح,التين,العلق,القدر,البينة,الزلزلة,العاديات,القارعة,التكاثر,العصر,الهمزة,الفيل,قريش,الماعون,الكوثر,الكافرون,النصر,المسد,الإخلاص,الفلق,الناس").Split(',');

    sealed record Reciter(int Id, string Name, string Style)
    {
        public override string ToString() => string.IsNullOrWhiteSpace(Style) ? Name : $"{Name} — {Style}";
    }
    sealed record TimedVerse(int Number, int Start, int End, string Text);

    readonly ComboBox _reciter = new() { MinWidth = 310, Margin = new Thickness(0, 6, 12, 12) };
    readonly ComboBox _surah = new() { MinWidth = 310, Margin = new Thickness(0, 6, 12, 12) };
    readonly TextBlock _verse = new()
    {
        FontSize = 34, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(Color.FromRgb(255, 220, 145)),
        Margin = new Thickness(24, 24, 24, 22), FlowDirection = FlowDirection.RightToLeft,
        LineHeight = 64
    };
    readonly TextBlock _verseNumber = new()
    {
        FontSize = 17, TextAlignment = TextAlignment.Center,
        Foreground = new SolidColorBrush(Color.FromRgb(255, 205, 105)),
        Margin = new Thickness(0, 10, 0, 0)
    };
    readonly TextBlock _status = new()
    {
        FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0),
        Foreground = Brushes.LightGray, TextAlignment = TextAlignment.Center
    };
    readonly Border _verseCard = new()
    {
        Background = new SolidColorBrush(Color.FromRgb(31, 28, 20)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(184, 143, 65)),
        BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(16),
        Padding = new Thickness(12), Margin = new Thickness(0, 18, 0, 18),
        MinHeight = 190
    };
    readonly LibVLC _lib = new("--no-video", "--quiet", "--network-caching=3000", "--file-caching=3000");
    readonly VlcMediaPlayer _audio;
    readonly DispatcherTimer _sync = new() { Interval = TimeSpan.FromMilliseconds(200) };
    Media? _media;
    List<Reciter> _reciters = new();
    List<TimedVerse> _verses = new();
    int _lastVerse = -1;
    bool _disposed;
    bool _loading;
    public event Action? CloseRequested;

    public QuranPanel()
    {
        _audio = new VlcMediaPlayer(_lib) { Volume = 100, Mute = false };
        Background = new SolidColorBrush(Color.FromRgb(15, 18, 24));
        Foreground = Brushes.White;

        for (var i = 0; i < Surahs.Length; i++) _surah.Items.Add($"{i + 1:000} — {Surahs[i]}");
        _surah.SelectedIndex = 0;

        var close = new Button
        {
            Content = "← Retour au programme", Padding = new Thickness(14, 8, 14, 8),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        close.Click += (_, _) => { Stop(); CloseRequested?.Invoke(); };

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        AddButton(buttons, "◀ السابق", () => Move(-1));
        AddButton(buttons, "▶ تشغيل", PlaySelected);
        AddButton(buttons, "Ⅱ إيقاف مؤقت", PauseAudio);
        AddButton(buttons, "■ إيقاف", Stop);
        AddButton(buttons, "التالي ▶", () => Move(1));

        var panel = new StackPanel
        {
            Margin = new Thickness(30), MaxWidth = 1100,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        panel.Children.Add(close);
        panel.Children.Add(new TextBlock
        {
            Text = "القرآن الكريم", FontSize = 30, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 22, 0, 18), TextAlignment = TextAlignment.Center
        });
        panel.Children.Add(new TextBlock { Text = "القارئ", FontSize = 16 });
        panel.Children.Add(_reciter);
        panel.Children.Add(new TextBlock { Text = "السورة", FontSize = 16 });
        panel.Children.Add(_surah);

        var verseContent = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        verseContent.Children.Add(new TextBlock
        {
            Text = "الآية التي تُتلى الآن", FontSize = 15, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(226, 190, 112)),
            TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 8, 0, 4)
        });
        verseContent.Children.Add(_verseNumber);
        verseContent.Children.Add(_verse);
        _verseCard.Child = verseContent;
        panel.Children.Add(_verseCard);
        panel.Children.Add(buttons);
        panel.Children.Add(_status);

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };

        _reciter.SelectionChanged += async (_, _) =>
        {
            if (!_loading && IsLoaded && _reciter.SelectedItem != null) await LoadSelectedSurah();
        };
        _surah.SelectionChanged += async (_, _) =>
        {
            if (!_loading && IsLoaded && _reciter.SelectedItem != null) await LoadSelectedSurah();
        };
        _sync.Tick += (_, _) => SyncVerse();
        _audio.Playing += Audio_Playing;
        _audio.Paused += Audio_Paused;
        _audio.Stopped += Audio_Stopped;
        _audio.EncounteredError += Audio_EncounteredError;
        Loaded += async (_, _) => await LoadReciters();
    }

    static void AddButton(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(5) };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    void SetStatus(string message)
    {
        if (_disposed) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => SetStatus(message)); return; }
        _status.Text = message;
    }

    void Audio_Playing(object? sender, EventArgs e)
    {
        if (_disposed) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (!_disposed) _status.Text = "التلاوة جارية — يتم إبراز الآية المقروءة تلقائياً";
        });
    }
    void Audio_Paused(object? sender, EventArgs e) => SetStatus("التلاوة متوقفة مؤقتاً.");
    void Audio_Stopped(object? sender, EventArgs e) => SetStatus("تم إيقاف التلاوة.");
    void Audio_EncounteredError(object? sender, EventArgs e)
    {
        if (_disposed) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (!_disposed) _status.Text = "تعذر تشغيل الملف الصوتي. تحقق من الاتصال أو اختر قارئاً آخر.";
        });
    }

    static string ReadNestedName(JsonElement item, string property)
    {
        return item.TryGetProperty(property, out var nested) &&
               nested.ValueKind == JsonValueKind.Object &&
               nested.TryGetProperty("name", out var name)
            ? name.GetString() ?? ""
            : "";
    }

    async Task LoadReciters()
    {
        try
        {
            SetStatus("جاري تحميل قائمة القراء…");
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(
                "https://api.quran.com/api/v4/resources/chapter_reciters?language=ar"));
            if (!doc.RootElement.TryGetProperty("reciters", out var array) || array.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("لم تُرجع واجهة القراء قائمة صالحة.");

            _reciters = array.EnumerateArray().Select(item =>
            {
                var name = ReadNestedName(item, "translated_name");
                if (string.IsNullOrWhiteSpace(name) && item.TryGetProperty("name", out var rawName))
                    name = rawName.GetString() ?? "قارئ";
                var style = ReadNestedName(item, "style");
                return new Reciter(item.GetProperty("id").GetInt32(), name, style);
            }).Where(reciter => reciter.Id > 0 && !string.IsNullOrWhiteSpace(reciter.Name))
              .OrderBy(reciter => reciter.Name).ToList();

            if (_reciters.Count == 0) throw new InvalidOperationException("لا توجد أسماء قراء متاحة حالياً.");
            _loading = true;
            _reciter.ItemsSource = _reciters;
            _reciter.SelectedIndex = 0;
            _loading = false;
            SetStatus($"تم تحميل {_reciters.Count} قارئاً. اختر السورة ثم اضغط تشغيل.");
            await LoadSelectedSurah();
        }
        catch (Exception ex)
        {
            _loading = false;
            SetStatus("تعذر تحميل القراء: " + ex.Message);
        }
    }

    async Task<Dictionary<int, string>> LoadSurahText(int chapter)
    {
        var result = new Dictionary<int, string>();
        const int pageSize = 50;
        for (var page = 1; page <= 6; page++)
        {
            var url = $"https://api.quran.com/api/v4/verses/by_chapter/{chapter}?language=ar&fields=text_uthmani&per_page={pageSize}&page={page}";
            using var response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("verses", out var verses) || verses.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("تعذر قراءة نص آيات السورة من واجهة القرآن.");
            foreach (var verse in verses.EnumerateArray())
            {
                var number = verse.GetProperty("verse_number").GetInt32();
                var text = verse.GetProperty("text_uthmani").GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(text)) result[number] = text;
            }
            if (verses.GetArrayLength() < pageSize) break;
        }
        if (result.Count == 0) throw new InvalidOperationException("لم يتم العثور على آيات لهذه السورة.");
        return result;
    }

    async Task LoadSelectedSurah()
    {
        if (_disposed || _loading || _reciter.SelectedItem is not Reciter reciter || _surah.SelectedIndex < 0) return;
        _loading = true;
        Stop();
        _status.Text = "جاري تحميل نص الآيات وملف التلاوة وتوقيت كل آية…";
        try
        {
            var chapter = _surah.SelectedIndex + 1;
            var texts = await LoadSurahText(chapter);

            // Chapter-reciter IDs must be used for a continuous chapter audio file.
            var audioEndpoint = $"https://api.quran.com/api/v4/chapter_recitations/{reciter.Id}/{chapter}?segments=true";
            using var audioResponse = await Http.GetAsync(audioEndpoint);
            audioResponse.EnsureSuccessStatusCode();
            using var audioDoc = JsonDocument.Parse(await audioResponse.Content.ReadAsStringAsync());
            var root = audioDoc.RootElement;

            if (!root.TryGetProperty("audio_file", out var audioFile) ||
                !audioFile.TryGetProperty("audio_url", out var audioUrlElement))
                throw new InvalidOperationException("لم تُرجع الواجهة رابط ملف التلاوة.");
            var audioUrl = audioUrlElement.GetString() ?? "";
            if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                throw new InvalidOperationException("رابط ملف التلاوة غير صالح.");

            // The documented response property is 'timestamps' (not 'verse_timings').
            if (!audioFile.TryGetProperty("timestamps", out var timestamps) || timestamps.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("هذه التلاوة لا تتضمن توقيتات الآيات؛ اختر قارئاً آخر.");

            _verses = timestamps.EnumerateArray().Select(timing =>
            {
                var key = timing.GetProperty("verse_key").GetString() ?? "";
                var number = int.TryParse(key.Split(':').Last(), out var parsed) ? parsed : 0;
                return new TimedVerse(
                    number,
                    timing.GetProperty("timestamp_from").GetInt32(),
                    timing.GetProperty("timestamp_to").GetInt32(),
                    texts.GetValueOrDefault(number, ""));
            })
            .Where(verse => verse.Number > 0 && verse.End > verse.Start && !string.IsNullOrWhiteSpace(verse.Text))
            .OrderBy(verse => verse.Start).ToList();

            if (_verses.Count == 0)
                throw new InvalidOperationException("لم يتم العثور على توقيتات متوافقة مع نص الآيات.");

            _media?.Dispose();
            _media = new Media(_lib, uri);
            _media.AddOption(":network-caching=3000");
            _media.AddOption(":http-reconnect");
            _media.AddOption(":http-user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");

            _lastVerse = -1;
            _verse.Text = "سيظهر هنا نص الآية بالتزامن مع التلاوة";
            _verseNumber.Text = $"سورة {Surahs[chapter - 1]}";
            _audio.Volume = 100;
            _audio.Mute = false;

            if (!_audio.Play(_media))
                throw new InvalidOperationException("رفض مشغل الصوت بدء ملف التلاوة.");

            _sync.Start();
            _status.Text = $"التلاوة: {reciter.Name} — {Surahs[chapter - 1]}";
        }
        catch (Exception ex)
        {
            _sync.Stop();
            _verses.Clear();
            _verse.Text = "تعذر تحميل التلاوة";
            _verseNumber.Text = "";
            _status.Text = "تعذر تشغيل القرآن أو تحميل توقيت الآيات: " + ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    void SyncVerse()
    {
        if (_disposed || _verses.Count == 0 || _audio.State != VLCState.Playing) return;
        var time = (int)Math.Max(0, _audio.Time);
        var index = _verses.FindLastIndex(verse => verse.Start <= time && time < verse.End);
        if (index < 0 || index == _lastVerse) return;

        _lastVerse = index;
        var verse = _verses[index];
        _verse.Text = verse.Text;
        _verseNumber.Text = $"الآية {verse.Number} من سورة {Surahs[_surah.SelectedIndex]}";
        _verseCard.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 184, 73));
        _verseCard.Background = new SolidColorBrush(Color.FromRgb(42, 35, 21));
    }

    async void PlaySelected()
    {
        if (_disposed || _loading) return;
        if (_media == null) { await LoadSelectedSurah(); return; }
        try
        {
            if (!_audio.Play()) throw new InvalidOperationException("تعذر استئناف ملف التلاوة.");
            _sync.Start();
        }
        catch (Exception ex) { SetStatus("تعذر تشغيل التلاوة: " + ex.Message); }
    }

    void PauseAudio()
    {
        if (_disposed) return;
        try { _audio.Pause(); }
        catch (Exception ex) { SetStatus("تعذر إيقاف التلاوة مؤقتاً: " + ex.Message); }
    }

    void Move(int delta)
    {
        var next = _surah.SelectedIndex + delta;
        if (next >= 0 && next < Surahs.Length) _surah.SelectedIndex = next;
    }

    void Stop()
    {
        _sync.Stop();
        _lastVerse = -1;
        if (_disposed) return;
        try { _audio.Stop(); } catch { }
        _verse.Text = "اختر القارئ والسورة ثم اضغط تشغيل";
        _verseNumber.Text = "";
        _verseCard.BorderBrush = new SolidColorBrush(Color.FromRgb(184, 143, 65));
        _verseCard.Background = new SolidColorBrush(Color.FromRgb(31, 28, 20));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sync.Stop();
        _audio.Playing -= Audio_Playing;
        _audio.Paused -= Audio_Paused;
        _audio.Stopped -= Audio_Stopped;
        _audio.EncounteredError -= Audio_EncounteredError;
        try { _audio.Stop(); } catch { }
        try { _media?.Dispose(); } catch { }
        _media = null;
        try { _audio.Dispose(); } catch { }
        try { _lib.Dispose(); } catch { }
    }
}
