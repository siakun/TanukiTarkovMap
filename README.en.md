# TanukiTarkovMap

[한국어](README.md) | **English** | [日本語](README.ja.md)

<div align="center">
<img src="images/icon.png" alt="TanukiTarkovMap" width="120" />
</div>

> **An in-game minimap overlay tool for Escape from Tarkov.<br>Take a screenshot, and it detects your coordinates and shows your current position on the minimap.**

TanukiTarkovMap opens the interactive map from [tarkov-market.com](https://tarkov-market.com/pilot) in a window that always stays on top of the game, and shows your current position in real time using the coordinates recorded in your in-game screenshots. Online shows your position and direction over the site map; Local shows them on the bundled map. You can check the map with a single hotkey, without Alt+Tab.

![In-game view: the map overlay always shown on top of the game](images/screenshot.jpg)

---

## Installation

1. Download the latest Setup installer (`TanukiTarkovMap-Setup-<version>-x64.exe`) from the [Releases page](https://github.com/siakun/TanukiTarkovMap/releases/latest).
2. Run it to install. Updates are applied automatically from then on. You can also turn off automatic updates or roll back to the version you want in Settings.

If you would rather not install it, download the portable version (`TanukiTarkovMap-Portable-<version>-x64.zip`) from the same page, extract it, and run it. The portable version has no installer, so you need to install the VC++ Redistributable listed in the requirements below yourself.

> **Requirements**: Windows 10/11 (x64), [Microsoft Visual C++ 2015-2022 Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe)
>
> The Redistributable is the runtime used by the built-in browser (CefSharp). If it is missing, the Setup installer installs it for you during setup, so there is nothing to prepare; only the portable version needs it installed manually. Without it, the app fails to start with an error saying that `CefSharp.Core.Runtime.dll` could not be loaded.
>
> **Safety**: The app does not read game memory or interfere with the game process. It reads only two things: screenshot file names (only the coordinates in the name, not the image content) and the game logs that record which map you entered. It never modifies game files. See [Background](#background) for how it works.

---

## Usage

1. Launch TanukiTarkovMap. The game's screenshot folder is detected automatically, and you can set it yourself in Settings if needed.
2. When you start the game, the app switches to the map you entered automatically.
3. During the game, show or hide the map window with the hotkey (`F11` by default, configurable in Settings).
4. Press the in-game screenshot key (`PrtScn` by default) to update your position and direction in Online or Local. If Online reports that position tracking is unavailable, switch to Local in the top bar.

Choose a view mode in the top bar. `Online` opens tarkov-market.com focused on the map and overlays your position and direction, `Local` shows your current position and direction on the map bundled with the app, and `Origin` opens the site as it is, without the app's changes. In `Origin`, `PMC`/`SCAV` and "Hide UI elements" do not apply. The mode you choose is kept the next time you launch the app.

The show/hide hotkey hides the whole minimap window to the tray or shows it again; it does not close the app. To update your position, use the game's Screenshot key. To keep the map above the game on a single monitor, turn on the pin (always on top) in the top bar.

> **Note for Steam users**: Steam's `F12` screenshot is a Steam overlay feature, so it does not record coordinates. Always use the Screenshot key from the game's own settings (Controls). Some players report that the default `PrtScn` does not work; if that happens, rebind it to another key.

In Settings, you can change the hotkey, opacity, UI visibility, automatic map switching, automatic screenshot cleanup, and how updates are applied. The same window also lets you check how much space the browser cache takes up as map tiles pile up, and clear it. If you run into a problem or have a suggestion, please post it on [GitHub Issues](https://github.com/siakun/TanukiTarkovMap/issues).

---

## Features

| Feature | Description |
|---------|-------------|
| Map overlay | Interactive map that always stays on top of the game (Always-on-Top) |
| Real-time position tracking | Updates position and direction in Online and Local from in-game screenshots |
| View modes | Switches in the top bar between Online (the site with position and direction), Local (the bundled map with position and direction), and Origin (the original site) |
| Automatic map switching | Detects the map you entered from the game log and switches to it |
| Global hotkey toggle | Shows or hides the map window with one hotkey (`F11` by default); supports modifier and special keys |
| Opacity control | Makes the window semi-transparent while the top bar is hidden, so it blocks as little of your view as possible |
| UI cleanup | In Online, removes unnecessary elements from the web page so only the map is shown |
| Recent Goons sightings | Shows PvE sighting reports on the map |
| Raid position sharing | Exchanges screenshot positions with friends who entered the raid in your squad and shows them on your map (connects automatically) |
| Update management | Applies new versions automatically, and lets you turn off automatic updates or roll back to an earlier version |
| Display language | Follows the Windows display language by default; picking a language in Settings switches it immediately without a restart |

Recent Goons sightings are off by default and can be turned on in Settings. The Goons spawn by chance, so reports may differ from where they actually appear.

Raid position sharing connects automatically to a server run by the developer, with nothing to enter. People who entered the same raid in the same squad exchange screenshot positions, and your game account ID is sent to the server along with your position. Raids you enter alone are not shared, and other squads in the same raid do not receive your position. A friend's name tag uses the nickname found in your game's party notifications or supplied by someone in the room, or the last four digits of their account ID if none is found. The bottom left shows the sharing party, including yourself, as `InRaid: 3p (name, name, name)`, even before screenshots are taken. It is hidden in solo or local PvE raids, in the menu, or when disconnected.

---

## Background

In Escape from Tarkov, taking an in-game screenshot records the player's world coordinates (X, Y, Z) and camera rotation in the file name. This is an official feature that the developer, Battlestate Games, provides for bug reports, and the app only reads files saved on disk without accessing game memory or the game process.

```
2026-09-09[14-14]_179.10, 3.29, -717.65_0, 0.70710678, 0, 0.70710678_15.67 (0).png
└ date/time ┘     └ position X, Y, Z ┘  └ camera rotation (quaternion) ┘
```

The Pilot page on [tarkov-market.com](https://tarkov-market.com/pilot) takes these coordinates and shows your current position on the map. A web app opened in a browser, however, has structural limits. It cannot directly control operating system windows: it cannot pin itself on top of the game (Always-on-Top), make its window semi-transparent, or toggle with a global hotkey. That is why a desktop client is needed.

Several tools like this already exist. The original is [ggdiam/TarkovPilot](https://github.com/ggdiam/TarkovPilot) (.NET Framework, a tray-only helper), which tarkov-market.com officially detects, and [byeong1/Tarkov-Client](https://github.com/byeong1/Tarkov-Client) from the Korean community wrapped it in Edge WebView2 to show the map inside a window. This project started from the idea behind byeong1's version, but replaced the web view engine with CefSharp and redesigned window control, coordinate sync, and the build pipeline from scratch.

---

## Tech Stack

| Area | Technology |
|------|------------|
| Language, runtime | C#, .NET 8.0 |
| UI | WPF, MVVM (CommunityToolkit.Mvvm), Microsoft.Xaml.Behaviors |
| Web view | CefSharp.Wpf.NETCore (Chromium) |
| Native control | P/Invoke (user32.dll) |
| Web integration | CefSharp JavaScript injection (page cleanup, faction filter) |
| Local map | SVG and ES module minimap, CefSharp folder scheme handler |
| DI | Microsoft.Extensions.DependencyInjection |
| Tray | Hardcodet.NotifyIcon.Wpf |
| Position sharing | ASP.NET Core SignalR (server as a .NET 10 container, SignalR client in the app) |
| Distribution, automatic updates | Velopack, Siakun.AutoUpdate, GitHub Releases |

---

## Technical Implementation

The core of this project is "a native client doing what a web app cannot." This section explains which problem each feature solves and how.

### 1. Porting from Edge WebView2 to CefSharp

The tool this project started from used Edge WebView2, which is built into Windows, but this project replaced the web view engine with CefSharp (Chromium Embedded Framework). WebView2's runtime depends on the user's environment, whereas CefSharp ships Chromium with the app, so it does not depend on the environment and gives more direct control over the render process, script injection, zoom, and similar behavior. The app injects scripts when a page starts loading (`FrameLoadStart`) and when it finishes loading (`FrameLoadEnd`), and receives messages from the web page through `JavascriptMessageReceived`.

### 2. Native window control

Window control at the OS level, which a web app cannot do, is handled directly in the desktop window. Pinning and transparency use WPF window properties, while behavior WPF does not provide, such as the global hotkey and restoring from the tray, uses P/Invoke (user32.dll).

- **Always-on-Top**: Binds the window's `Topmost` to the pin button to keep it above the game. When restoring from the tray, `SetWindowPos` raises the window without stealing focus and keeps the pin state (`HWND_TOPMOST` or `HWND_TOP`)
- **Semi-transparent window**: Binds the content `Opacity` of a borderless transparent window (`AllowsTransparency`) to the opacity setting. It applies only while the top bar is hidden or the opacity slider is being dragged, so the window looks opaque while you operate it
- **Global hotkey**: A low-level keyboard hook (`SetWindowsHookEx`) handles the toggle even while the game has focus

Win32 declarations are gathered in `PInvoke.cs`. The exception is `Program.cs`, which keeps its own declarations for the single-instance check that brings an already running window to the front.

### 3. Real-time coordinate sync (FileSystemWatcher + built-in minimap)

The app watches the screenshot folder with `FileSystemWatcher` and ties the latest file name to the current map. In Online, the Core DLL calculates coordinates from the file name and resource constants, then a WPF Canvas draws the position circle and direction arrow over the browser using the site's live view state. The Local minimap reads coordinates and rotation from the file name to draw position and direction. Both modes select the floor from the position height. `LogsWatcher` switches maps when the player enters a raid.

The app retains one latest input for the current map. Switching modes on that map preserves it; changing maps expires it. Origin stores the input without displaying it. Online pings only when a file name first appears on screen and shows a log reason and Local fallback notice if view-state capture fails. Local checks and resends positions received during loading or lost after a reload.

Until 2026-08-17, the app ran a WebSocket server with ASP.NET Core Kestrel on port `5123`, and the site connected to that server to receive file names. That day, tarkov-market deployed Pilot v2, and the site switched to connecting only to its own server (`wss://tarkov-market.com/ws/pilot`) instead of the local app, so the server no longer had any clients. Following the server relay protocol would have meant implementing account authentication as well, while `window.pilot` did the same job without it, so the app dropped the WebSocket server and the ASP.NET Core dependency and switched to bridge calls.

On 2026-10-07, the site removed both the position function of `window.pilot` and the position input of the Pilot service. Only the "Where am i" input, where users paste a screenshot file name, remained, so the bridge found that input's handler in the Vue components being rendered on screen and passed the same file name to it. This relied on the fact that function names in the minified bundle change with every deployment, but prop names in templates remain.

On 2026-10-08, the site started drawing the map and your position on canvases and showing the direction arrow only to PRO users, which broke the Online direction display and position check again. The position input path and the site's own position drawing had changed with every deployment, while the map's coordinate transforms had stayed the same. So instead of passing the position to the site, the app drew the position and direction itself and borrowed only the coordinate transforms from the site.

The site moved map state out of its regular reactive registration, so the old WeakMap hook could no longer capture it. Online now records the required state when its Proxy is constructed, with coordinate and direction calculations in the Core DLL and marker rendering in WPF. Local remains independent of this site-state dependency.

### 4. Offline map (Local)

Choosing Local in the top bar opens the map from map data bundled with the app, without connecting to the site. It keeps only the map and its levels, the extracts of the selected faction, and your current position and direction, so what you need in a raid keeps working even if the site is down or changes its map structure.

At first, the app saved a full copy of the responses the site sent and served it back by intercepting requests. It worked, but the copy was a lump of minified bundles whose contents could not be read, and when the site changed its structure, the only option was to download everything again. Now the app collects only the site's map documents, converts them to SVG and JSON embedded in the Core DLL, and the minimap bundled with the app (`viewer/`) draws them to look like the site's map screen. The converted SVG is compared pixel by pixel, level by level, with what the collected site renderer draws, and the coordinate conversion is verified against the values the site computed.

Local opens in a separate temporary storage space so that it does not mix with the site's (Online, Origin) DB and cache. When you switch modes, the app reopens the same map in the new mode and passes the last position to Local. It reapplies the app's zoom in every mode, and the faction and "Hide UI elements" settings in Online and Local.

### 5. Automatic map switching by parsing game logs

Each time the game starts, it creates a new session folder under its log folder (`<game folder>\Logs` for the official launcher, `<game folder>\build\Logs` for the Steam version) and records its state in files such as `application.log`. The game folder is detected automatically from the registry (official launcher first, then Steam), and if detection fails, you can set it yourself in Settings.

- **Official launcher**: The `InstallLocation` value of `HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov` (used only if the folder actually exists)
- **Steam**: Parses `steamapps\libraryfolders.vdf` under the `InstallPath` of `HKLM\SOFTWARE\WOW6432Node\Valve\Steam`, then checks `steamapps\common\Escape from Tarkov` in every library folder it finds

`LogsWatcher` watches the most recent session folder (moving to a new folder automatically when one appears), and whenever a log file is updated, it reads only the newly added lines from where it last stopped (it opens files with `FileShare.ReadWrite`, so it does not conflict with the game's writes). Logs that had already accumulated before the app started never switch the view, which prevents switching to the map of a previous raid by mistake.

The map is identified from the scene load log line (`scene preset`) by extracting the preset name from `path:maps/<preset>.bundle`. The `Location:` value in the match creation log (`TRACE-NetworkGameCreate profileStatus`) carries the same information, but the scene load line always comes first, and in some raids it is the only one recorded, so only the scene load line is used.

The extracted preset name is resolved to a map right where it is detected. This keeps the code that interprets log strings in one place, `LogsWatcher`, and later stages exchange only map objects, not strings.

```
scene preset line detected
  -> LogsWatcher          extract the preset name
  -> MapConfiguration     resolve the preset to a map (stops here for unregistered values)
  -> MapEventService      publish a map change event
  -> MainWindowViewModel  assign SelectedMapInfo (where manual selection from the dropdown joins)
  -> WebBrowserViewModel  navigate CefSharp to the new address
```

Maps and presets have a 1:N relationship. Ground Zero has a separate preset for each player level range, and Factory for each time of day, so each map is registered in `MapConfiguration` with a list of presets. When a preset that is not in any list appears, the app does not switch and only writes that name to the app log. That line tells you what to register when a new map is added to the game.

Routing automatic switching through `SelectedMapInfo` is also intentional. Joining at the same point as when the user picks a map from the dropdown leaves only one copy of the code that changes the address and reinjects the scripts. Turning off automatic switching skips only this assignment, so manual selection keeps working.

If you launch the app after entering a raid, the entry log has already gone by, so real-time detection cannot catch it. So the app still reads map names from past logs and remembers the last one, then uses a screenshot, which can only be taken in game, as the signal to correct to that map. If you are already viewing that map, nothing happens, so once the map has switched correctly, taking screenshots does not make the view jump.

In addition, the BattlEye initialization log (`BEClient inited successfully`) serves as a raid boundary signal that triggers automatic screenshot cleanup.

### 6. Two-way communication between CefSharp and JavaScript

After the position and direction display is changed, the same failure conditions are checked again by automated tests, whether or not the site is reachable. The tests run in a windowless Chromium against the minimap and map resources bundled with the app, every time the installers are built. Whether Online state capture, map fitting, floor selection, UI hiding and faction switching still work on the current site is checked separately with a diagnostic tool that injects the same scripts as the app into the real site.

The logic that adapts the web UI to the app is injected as JavaScript. The scripts injected into the site and their execution contract are embedded in the Core DLL as EmbeddedResource. Core `SiteControl` handles call order and faction retries; `OnlineOverlay` handles site reports and position synchronization. The app owns CEF lifetime, document validity checks and WPF presentation. Scripts run at two points. When a page starts loading, the app first injects map-state capture and page-error scripts; when loading finishes, it runs the rest with `EvaluateScriptAsync` (removing the header and footer, toggling panels, applying the faction filter, and so on). In the other direction, the page reports its state (errors, whether the map rendered) with `postMessage`, which the app receives in `JavascriptMessageReceived` and writes to its log. ViewModels pass messages to each other through CommunityToolkit.Mvvm's `WeakReferenceMessenger`. Messages keep the boundary between C# and JS loosely coupled. Nothing is injected into the Local minimap; the app only calls the functions the minimap exposes on `window.tanukiViewer`. Origin shows the site as it is, so nothing is injected there either. Whether to inject is decided when a page opens, so switching between Online and Origin reopens the current page.

### 7. MVVM architecture and DI

The project's rule is to keep logic out of code-behind (`*.xaml.cs`); the initialization logic still left in the main window is tracked as cleanup work. UI interactions are separated into Behaviors based on Microsoft.Xaml.Behaviors (window dragging, minimizing to the tray, capturing hotkey input, and so on), and data and business logic live in ViewModels and Services. Services are registered with Microsoft.Extensions.DependencyInjection and accessed through `ServiceLocator`, and ViewModels communicate through the Messenger instead of direct references to reduce coupling.

### 8. Release automation (Velopack + GitHub Releases)

A version tag goes through build, checks, a self-contained publish and Velopack packaging, and the installers and update packages are uploaded to GitHub Releases. This repository receives one commit with that version's source for each release, tagged with the same name. The release body and the update notes inside the installer package come from the same file, and the release stops if the tag and the project version differ or if the notes are missing. Installed apps download updates after the main window opens and apply them during normal shutdown after CefSharp releases its files.

### 9. Choosing a version and rolling back

Automatic updates only move toward the latest version. That leaves a user who runs into a problem in a new version with no way out on their own, so Settings lets you turn off automatic updates and choose the version you want.

Velopack's default path cannot do this. `GithubSource` assumes that "all packages are gathered in the single latest release," so it reads only that release's `releases.win.json` and looks for download URLs only there. In this repository, a full package is hundreds of megabytes, so each tag uploads only its own version, and the default path shows only two versions: the latest and the one before it. So the app queries the release list directly with the GitHub Releases API, creates an `IUpdateSource` (`GitHubReleaseSource`) pinned to the chosen tag, and runs it through Velopack's download, checksum verification, and apply steps. This update code is split out into the [Siakun.AutoUpdate](https://github.com/siakun/Siakun.AutoUpdate) package so that other apps can use it.

This is also why the app parses the feed JSON the release already contains instead of assembling package metadata itself. Velopack checks the SHA of a downloaded package against the value in the feed, and hand-built metadata has no such hash, so there would be no way to pass the check.

When moving up, the app downloads the deltas of the versions in between and applies them in sequence. A full package is hundreds of megabytes, while each delta is a few hundred KB, so the difference is large. Rolling back always downloads the full package, because deltas are only created in the upward direction. The full package is also downloaded when an intermediate release has been deleted, when there are too many steps to skip, or when the deltas add up to a size that is large compared with the full package. Since this can take a while, progress is shown on screen. Even after the download finishes, the app does not kill its process; it shuts down normally so that CefSharp releases its cookie and IndexedDB files, and then applies the package.

Choosing a version other than the latest also turns off automatic updates. If they stayed on, the next launch would immediately go back to the latest version, and the version the user chose would be lost.

Turning on "Get beta versions" under "Experimental features" in Settings adds pre-release versions such as `v0.1.1-beta` to the list and to automatic updates. While it is off, they are excluded at the query stage, so what you see in the list and what automatic updates follow are always the same.

This blocking works only from the version that introduced the automatic update setting. If you go down to an older version, that version's update code ignores the setting and fetches the latest, so you cannot stay there. Code that has already been released cannot be changed, so this cannot be prevented by design; instead, the Settings screen warns about it in advance.

### 10. Raid position sharing (SignalR)

Even when you enter a raid together with friends, the app shows only your own position. To draw your friends' screenshot positions on your map as well, positions are exchanged through a server the developer runs. The app contains the server address and access key, so it connects automatically when the app starts.

Whether you entered the same raid in the same squad is decided from the game log. When the server creates a raid, the application log records the game server session the raid runs on, and when you return to the menu, a line that selects the profile again is recorded. The same log also gives the server mode (PvE or PvP) and your account ID. When a squad starts matchmaking, a notification in the push-notifications log carries the squad's group ID, and the assignment notification right after it carries the same server session. The app matches the two by server session, joins the room named `mode:squad group ID` on the server, and uploads the parsed coordinates each time you take a screenshot. At first the rooms were split by the raid ID shown at the bottom right of the screen (`shortId`), but its last character differs from player to player even in the same raid, so friends ended up in different rooms. The squad group ID is the same only for one squad in one raid and is never shown on screen, so other squads in the same raid and stream viewers cannot join the room. The server (an ASP.NET Core SignalR hub) pushes a position over WebSocket only to the other people in the same room, so a friend's screenshot appears on your map right away even if you take none yourself. Anyone who joins late receives the room's last positions. The server and the app read the hub contract from a single place in the Core DLL so that they cannot drift apart.

Party notifications record squad members' account IDs and nicknames. Your own nickname can also appear in inventory error profile tags or dogtags carried by others. The app matches profile tags against the profile and account IDs from the same session. When no name is saved for your account, a bounded background search checks previous sessions. Your name is saved together with your account ID and restored only for that account. Current session logs take priority over names supplied by squad members, followed by past session logs and the saved name. The app announces your known name when joining or learning a new name. If none is found, the last four digits of the account ID are shown, and the tag is replaced as soon as the nickname becomes known. Friend markers are red, orange, yellow, and green in the order they first appear in the raid, and your own marker is a light gray that does not clash with those colors.

Because the app is distributed publicly, the access key inside it is not a secret. The key keeps out connections that are not the app, and changing it cuts off older versions of the app. Filtering people is the job of the squad group ID, not the key. The server can turn off sharing for a mode by limiting the allowed modes.

Membership notifications and a member list keep the party display independent of screenshots. WPF shows it in every view mode, with yourself first and friends ordered by color slot. The server operator can see each connection, including those outside a room, with its IP, app version, User-Agent, account ID, nickname, room ID, and last position. Participation accounts, nicknames, modes, maps, join and end times, and position counts are stored in SQLite for statistics; IP addresses and coordinates are not persisted. The original hub contract stays intact so servers and apps can update independently. On an older server, the app skips member-list and name exchange calls and builds the party display from received positions.

Sharing is a secondary feature, so its path is kept separate from your own position display, which never waits even when the server is down or slow. The sharing service never lets an exception escape from the screenshot event and runs server calls separately, and friend markers are drawn in elements separate from your own marker.

---

## Development

Building requires Windows and the .NET 10 SDK or later. The app runs on .NET 8 and uses the latest C# compiler for the ViewModels' partial property generation (`LangVersion=preview`).

```powershell
dotnet build TanukiTarkovMap.csproj
dotnet publish TanukiTarkovMap.csproj -c Release -r win-x64 --self-contained true -o publish
```

Coordinate calculation, settings, file watching, position sharing and site control are provided by the TanukiTarkovMap.Core package in `packages/`. The project file pins its version, and `NuGet.Config` restores Core from the repository's `packages/` and every other dependency from NuGet.org, so no extra account or authentication is needed. The Core DLL is obfuscated with Obfuscar, and its public API and JSON contracts are unchanged. The installers on the Releases page are built from the source of the same tag.

---

## License

The source code is licensed under the [MIT License](LICENSE). The Core package includes the license for its binary; see each NuGet dependency's metadata and notices for its license.

> This project is an unofficial tool and is not affiliated with Battlestate Games or Tarkov Market. It does not read game memory or interfere with the game process; it reads only screenshot file names (without reading the image content) and the game logs that record map names.
