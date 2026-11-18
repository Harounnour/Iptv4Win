# iptv4win

Lecteur IPTV pour Windows (WPF, .NET 8) avec une interface en français :
groupes de chaînes à gauche, liste des chaînes au centre, lecteur à droite.

## Fonctions

- Abonnements **M3U** (URL ou fichier local), **Xtream Codes** et **Stalker Portal** (adresse MAC)
- Lecture avec LibVLC, cache réseau adapté aux flux en direct, plein écran (F11 / Échap)
- Groupes et recherche de chaînes
- Contrôle parental avec code PIN ; les chaînes reconnues comme adultes sont filtrées
- Enregistrement local des abonnements dans `%AppData%\Iptv4Win\playlists.json`
  (mots de passe et adresses MAC chiffrés avec Windows DPAPI)
- Interface et culture de l'application fixées au français

## Installer et lancer

L'édition légère publiée par GitHub Actions est **framework-dependent** pour réduire sa taille.
Sur le PC Windows, installer au préalable le **.NET 8 Desktop Runtime x64** :
https://dotnet.microsoft.com/en-us/download/dotnet/8.0

Télécharger et extraire l'archive `iptv4win-win-x64`, puis lancer `Iptv4Win.exe`.
Une connexion Internet est nécessaire pour les playlists distantes et les flux en ligne.

## Compiler

Prérequis : Windows 10/11 et le SDK .NET 8.

```powershell
dotnet run --project src/Iptv4Win
```

Publication légère :

```powershell
dotnet publish src/Iptv4Win -c Release -r win-x64 --self-contained false -p:DebugType=None -p:DebugSymbols=false -o publish
```

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

## Remarques

- La lecture dépend de la validité de l'abonnement et de la compatibilité du flux avec VLC.
- Stalker : certains portails demandent des paramètres supplémentaires (numéro de série ou identifiant d'appareil).
- Le filtrage parental se base sur les noms et catégories fournis par la playlist ; il ne peut pas identifier tous les flux mal classés.
- Utilisez uniquement des abonnements dont vous avez les droits.
