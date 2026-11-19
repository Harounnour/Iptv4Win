# IPTV4Win

Lecteur IPTV Windows inspiré de TiviMate, développé en **C# / .NET 8 / WPF**. WPF est conservé dans cette version pour préserver les fenêtres et le lecteur LibVLC déjà intégrés.

## Fonctions disponibles

- Abonnements multiples **Stalker / Ministra**, **M3U** et **Xtream Codes**.
- Formulaire Stalker : nom, URL du portail, MAC, numéro de série, Device ID et Device ID2 (trois derniers facultatifs).
- Validation HTTP(S) et MAC avant enregistrement.
- Handshake Stalker, profil, genres, chaînes, résolution de flux par `create_link`, reprise du handshake après refus d'accès, et maintien de session par `watchdog`.
- Repli automatique de `/server/load.php` vers `/portal.php`.
- En-têtes de session Stalker (MAC, langue, fuseau, Authorization Bearer et identifiants facultatifs).
- Abonnements enregistrés dans SQLite (`%AppData%\Iptv4Win\iptv4win.db`). Les mots de passe, MAC et identifiants de périphérique sont chiffrés avec Windows DPAPI.
- Migration automatique des anciennes données `playlists.json` vers SQLite.
- Lecture interne LibVLC, ouverture optionnelle dans mpv, recherche, groupes, tri et listes Live / Series / Films.
- Interface actuellement en français.

## Structure du projet

```
src/Iptv4Win/
  Models/Models.cs
  Services/
    StalkerClient.cs       # handshake, fallback, watchdog, create_link
    XtreamClient.cs        # Xtream API
    M3uParser.cs           # parsing M3U
    ChannelLoader.cs       # sélection du fournisseur
    PlaylistStore.cs       # SQLite + DPAPI + migration JSON
  MainWindow.xaml(.cs)
  PlaylistsWindow.xaml(.cs)
tests/Iptv4Win.Tests/
  StalkerClientTests.cs    # tests avec HttpMessageHandler simulé
.github/workflows/build.yml
```

## البناء على Windows

المتطلبات: Windows 10/11 و.NET 8 SDK.

تشغيل التطبيق:

```powershell
dotnet run --project src/Iptv4Win/Iptv4Win.csproj
```

تشغيل الاختبارات:

```powershell
dotnet test tests/Iptv4Win.Tests/Iptv4Win.Tests.csproj -c Release
```

نشر نسخة Windows x64 مستقلة:

```powershell
dotnet publish src/Iptv4Win/Iptv4Win.csproj -c Release -r win-x64 --self-contained -o publish
```

يُنفّذ GitHub Actions الاختبارات أولاً، ثم ينشر التطبيق ويرفعه كـ Artifact.

## ملاحظات وحدود

- نجاح الاختبارات والبناء لا يثبت عمل كل بوابات Stalker؛ تختلف البوابات في متطلبات MAC وبيانات الجهاز وطريقة المصادقة. يلزم اختبار بوابة واشتراك مصرح بهما فعلياً.
- بيانات `Serial Number / Device ID / Device ID2` تُرسل كـ headers اختيارية؛ بعض البوابات قد تتطلب أسماء headers أو قيماً مختلفة.
- Mini-player مستقل قابل لتغيير الحجم وEPG XMLTV والمفضلة/الأخيرة ليست مكتملة بعد.
- SQLite يحفظ بيانات الاشتراكات؛ لا توجد حالياً طبقة ViewModel كاملة بنمط MVVM.
- استخدم الاشتراكات ومصادر البث التي تملك حق الوصول إليها فقط.
