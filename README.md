# iptv4win

Lecteur IPTV pour Windows (WPF, .NET 8) avec une interface inspirée de TiviMate :
groupes de chaînes à gauche, liste des chaînes au centre, lecteur à droite.

## Fonctions

- Abonnements **M3U** (URL ou fichier local), **Xtream Codes** et **Stalker Portal** (adresse MAC)
- Enregistrement des abonnements dans `%AppData%\Iptv4Win\playlists.json`
  (mots de passe et adresses MAC chiffrés avec Windows DPAPI)
- Groupes, recherche, lecture avec LibVLC, plein écran (F11 / Échap)

## Compiler

Prérequis : Windows 10/11 et le [SDK .NET 8](https://dotnet.microsoft.com/download).

```powershell
dotnet run --project src/Iptv4Win
```

Version autonome :

```powershell
dotnet publish src/Iptv4Win -c Release -r win-x64 --self-contained -o publish
```

Le workflow GitHub Actions (`.github/workflows/build.yml`) fait la même chose à chaque push.

## Structure

```
src/Iptv4Win/
  Models/Models.cs            Playlist, Channel
  Services/M3uParser.cs       lecture des fichiers M3U
  Services/XtreamClient.cs    API player_api.php
  Services/StalkerClient.cs   portail Stalker (handshake, chaînes, create_link)
  Services/PlaylistStore.cs   sauvegarde chiffrée des abonnements
  Services/ChannelLoader.cs   choisit le bon client selon le type
  MainWindow.xaml(.cs)        écran principal
  PlaylistsWindow.xaml(.cs)   gestion des abonnements
```

## À faire

- Guide TV (EPG XMLTV) et mini-lecteur flottant
- Films et séries (VOD) pour Xtream
- Favoris et chaînes récentes

## Remarques

- Stalker : il existe plusieurs variantes de portails, certaines demandent des paramètres
  supplémentaires (numéro de série, device id).
- Utilisez uniquement des abonnements dont vous avez les droits.
