using System.Text.Json;
using EMECore.Core.Models;
using EMECore.Core.Services;

namespace EMECore.Hardware.Services;

public class GameScannerService : IGameScannerService
{
    private readonly ISteamStoreService _steamStore;
    private static readonly SemaphoreSlim _steamApiSemaphore = new(3);
    private static readonly HttpClient _http = new();
    private static readonly Dictionary<string, string> _steamSearchCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] NonGameExePatterns =
    {
        "uninstall", "unins",
        "vc_redist", "vcredist",
        "dxwebsetup", "dxsetup", "directx",
        "unitycrashhandler",
        "crashreport", "bugsplat", "bugsplatrc",
        "crs-handler", "crs-uploader", "crsclient",
        "dotnet", "ndp", "windowsdesktop",
        "installer", "redist", "setup",
        "eosoverlay", "eos",
        "xna", "physx", "openal",
        "launcher", "updater", "patcher",
        "unrealcefsubprocess", "cefsubprocess",
        "downloadagent", "cleanup", "activation",
        "prerequisite", "prerequisites",
        "dxgi", "d3d", "vulkan",
        "touchup", "unins000", "unins001", "unins002",
        "installscript",
        "gameoverlay", "overlay",
        "steamservice", "steamerror",
        "galaxyclient", "galaxyupdater",
        "ubisoftconnect", "ubisoft",
        "origin", "eadesktop",
        "epiconlineservices", "epicgames"
    };

    private static readonly string[] NonGameDirPatterns =
    {
        "_CommonRedist", "Redist", "redist",
        "_Installer", "__Installer", "Installer",
        "DirectX", "DirectX9", "DirectX10", "DirectX11", "DirectX12",
        "Support", "Tools", "Tool",
        "Engine", "Binaries",
        "Launcher", "Launchers",
        "Resources", "Plugins",
        "DotNet", "dotnet", "VCRedist", "vcredist",
        "PhysX", "OpenAL", "XNA",
        "GameSave", "WindowsApps", "ModifiableWindowsApps"
    };

    private static readonly string[] NonGameSteamPatterns =
    {
        "redistributable", "steamworks common",
        "proton", "steam linux runtime",
        "steamworks shared", "dedicated server"
    };

    public static readonly string[] ToolNames =
    {
        "wallpaper engine", "creation kit", "creationkit",
        "sdk", "mod kit", "modkit",
        "world editor", "unreal engine",
        "radeon software", "geforce experience",
        "nvidia app", "msi afterburner",
        "rivatuner", "obs studio", "streamlabs",
        "discord", "teamspeak", "ventrilo",
        "mumble", "reshade", "enb series",
        "script hook", "scripthook",
        "open iv", "openiv",
        " blender", "gimp", "audacity",
        "voicemeeter", "sound card"
    };

    public static readonly string[] TrainingNames =
    {
        "aimlab", "aimlabs",
        "kovaaK", "kovaaKs",
        "aimtastic", "aimbeast",
        "osu!", "osu",
        "recoil trainer",
        "aim trainer",
        "aim ftw",
        "aiming.pro",
        "aimbooster"
    };

    private static readonly string[] CoverImagePatterns =
    {
        "SplashScreen*.png",
        "Square*Logo*.png",
        "*StoreLogo*.png",
        "*SplashScreen*.jpg",
        "*header*.jpg",
        "*cover*.png",
        "*cover*.jpg",
        "*logo*.png",
        "icon.png"
    };

    public static string GetTwitchBoxArtUrl(string gameName)
    {
        var encoded = Uri.EscapeDataString(gameName);
        return $"https://static-cdn.jtvnw.net/ttv-boxart/{encoded}-285x380.jpg";
    }

    public GameScannerService(ISteamStoreService steamStore)
    {
        _steamStore = steamStore;
    }

    public async Task<List<ScannedGame>> ScanAllGamesAsync()
    {
        var games = new List<ScannedGame>();
        var drives = GetAllFixedDrives();

        games.AddRange(await ScanSteamAsync(drives));
        games.AddRange(await ScanEpicGamesAsync(drives));
        games.AddRange(await ScanGogGamesAsync(drives));
        games.AddRange(await ScanRiotGamesAsync(drives));
        games.AddRange(await ScanUbisoftAsync(drives));
        games.AddRange(await ScanEaAppAsync(drives));
        games.AddRange(await ScanBattleNetAsync(drives));
        games.AddRange(await ScanRockstarAsync(drives));
        games.AddRange(await ScanBethesdaAsync(drives));
        games.AddRange(await ScanAmazonGamesAsync(drives));

        foreach (var drive in drives)
        {
            games.AddRange(await ScanDirectoryAsync(Path.Combine(drive, "XboxGames"), "xbox"));
            games.AddRange(await ScanDirectoryAsync(Path.Combine(drive, "Games"), "other"));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<ScannedGame>();
        foreach (var g in games)
        {
            var key = g.Name.Trim();
            if (!seen.Add(key))
                continue;

            deduped.Add(g);
        }

        return deduped;
    }

    public static string GetGameCategory(string gameName)
    {
        if (ToolNames.Any(t => gameName.Contains(t, StringComparison.OrdinalIgnoreCase)))
            return "tool";
        if (TrainingNames.Any(t => gameName.Contains(t, StringComparison.OrdinalIgnoreCase)))
            return "training";
        return "game";
    }

    public static List<string> GetAllFixedDrives()
    {
        var drives = new List<string>();
        foreach (var info in DriveInfo.GetDrives())
        {
            if (info.DriveType == DriveType.Fixed && info.IsReady)
            {
                var root = info.RootDirectory.FullName.TrimEnd('\\');
                if (!drives.Contains(root, StringComparer.OrdinalIgnoreCase))
                    drives.Add(root);
            }
        }
        return drives;
    }

    private async Task<List<ScannedGame>> ScanSteamAsync(List<string> drives)
    {
        var games = new List<ScannedGame>();
        var scannedLibraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var drive in drives)
        {
            var candidates = new[]
            {
                Path.Combine(drive, "Program Files (x86)", "Steam", "steamapps"),
                Path.Combine(drive, "SteamLibrary", "steamapps"),
                Path.Combine(drive, "Steam", "steamapps")
            };

            foreach (var steamPath in candidates)
            {
                if (!Directory.Exists(steamPath)) continue;
                if (!scannedLibraries.Add(steamPath)) continue;

                games.AddRange(await ScanSteamLibraryAsync(steamPath));

                var vdfPath = Path.Combine(steamPath, "libraryfolders.vdf");
                if (!File.Exists(vdfPath)) continue;

                try
                {
                    var vdfContent = await File.ReadAllTextAsync(vdfPath);
                    var lines = vdfContent.Split('\n');
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (!trimmed.StartsWith("\"path\"")) continue;
                        var pathValue = ExtractVdfValueFromLine(trimmed);
                        if (string.IsNullOrEmpty(pathValue)) continue;

                        var librarySteamApps = Path.Combine(pathValue, "steamapps");
                        if (!Directory.Exists(librarySteamApps)) continue;
                        if (!scannedLibraries.Add(librarySteamApps)) continue;

                        games.AddRange(await ScanSteamLibraryAsync(librarySteamApps));
                    }
                }
                catch { }
            }
        }

        await FetchSteamCoversAsync(games);
        return games;
    }

    private async Task<List<ScannedGame>> ScanSteamLibraryAsync(string steamAppsPath)
    {
        var games = new List<ScannedGame>();
        if (!Directory.Exists(steamAppsPath)) return games;

        foreach (var acf in Directory.GetFiles(steamAppsPath, "appmanifest_*.acf"))
        {
            try
            {
                var acfContent = await File.ReadAllTextAsync(acf);
                var name = ExtractVdfValue(acfContent, "name");
                var appId = ExtractVdfValue(acfContent, "appid");
                var installdir = ExtractVdfValue(acfContent, "installdir");
                var exePath = Path.Combine(steamAppsPath, "common", installdir);

                if (NonGameSteamPatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    continue;

                games.Add(new ScannedGame
                {
                    Name = name,
                    ExecutablePath = exePath,
                    Platform = "steam",
                    SteamAppId = appId
                });
            }
            catch { }
        }
        return games;
    }

    private async Task<List<ScannedGame>> ScanEpicGamesAsync(List<string> drives)
    {
        var games = new List<ScannedGame>();
        foreach (var drive in drives)
        {
            var manifestDir = Path.Combine(drive, "ProgramData", "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(manifestDir)) continue;

            foreach (var file in Directory.GetFiles(manifestDir, "*.item"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var name = root.GetProperty("DisplayName").GetString() ?? "";
                    var installLoc = root.GetProperty("InstallLocation").GetString() ?? "";
                    var exe = root.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() ?? "" : "";

                    if (string.IsNullOrEmpty(installLoc) || string.IsNullOrEmpty(name)) continue;

                    var exePath = string.IsNullOrEmpty(exe)
                        ? FindBestExecutable(installLoc) ?? installLoc
                        : Path.Combine(installLoc, exe);

                    games.Add(new ScannedGame
                    {
                        Name = name,
                        ExecutablePath = exePath,
                        Platform = "epic"
                    });
                }
                catch { }
            }
        }
        return games;
    }

    private async Task<List<ScannedGame>> ScanGogGamesAsync(List<string> drives)
    {
        var games = new List<ScannedGame>();
        foreach (var drive in drives)
        {
            var gogDir = Path.Combine(drive, "GOG Games");
            if (Directory.Exists(gogDir))
                games.AddRange(await ScanDirectoryAsync(gogDir, "gog"));

            var gogGalaxy = Path.Combine(drive, "Program Files (x86)", "GOG Galaxy", "Games");
            if (Directory.Exists(gogGalaxy))
                games.AddRange(await ScanDirectoryAsync(gogGalaxy, "gog"));
        }
        return games;
    }

    private async Task<List<ScannedGame>> ScanRiotGamesAsync(List<string> drives)
    {
        var games = new List<ScannedGame>();
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var installsJson = Path.Combine(programData, "Riot Games", "RiotClientInstalls.json");
        var metadataDir = Path.Combine(programData, "Riot Games", "Metadata");

        if (File.Exists(installsJson))
        {
            try
            {
                var json = await File.ReadAllTextAsync(installsJson);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("associated_client", out var clients))
                {
                    foreach (var prop in clients.EnumerateObject())
                    {
                        var path = prop.Name;
                        if (string.IsNullOrEmpty(path)) continue;
                        path = path.Replace('/', '\\').TrimEnd('\\');
                        if (!Directory.Exists(path)) continue;

                        var gameFolder = ExtractRiotGameNameFromPath(path);
                        if (string.IsNullOrEmpty(gameFolder)) continue;

                        var exe = FindBestExecutable(path);
                        if (exe != null)
                        {
                            games.Add(new ScannedGame
                            {
                                Name = FormatRiotGameName(gameFolder),
                                ExecutablePath = exe,
                                Platform = "riot"
                            });
                        }
                    }
                }
            }
            catch { }
        }

        if (Directory.Exists(metadataDir))
        {
            foreach (var metaDir in Directory.GetDirectories(metadataDir))
            {
                try
                {
                    var dirName = Path.GetFileName(metaDir);
                    if (!dirName.EndsWith(".live", StringComparison.OrdinalIgnoreCase)) continue;

                    var settingsFile = Path.Combine(metaDir, dirName + ".product_settings.yaml");
                    if (!File.Exists(settingsFile)) continue;

                    var yaml = await File.ReadAllTextAsync(settingsFile);
                    var installPath = ExtractYamlValue(yaml, "product_install_full_path");
                    if (string.IsNullOrEmpty(installPath)) continue;

                    installPath = installPath.Replace('/', '\\').Trim('"').TrimEnd('\\');
                    if (!Directory.Exists(installPath)) continue;

                    var gameName = dirName.Replace(".live", "");
                    if (gameName.Equals("Riot Client", StringComparison.OrdinalIgnoreCase)) continue;

                    var exe = FindBestExecutable(installPath);
                    if (exe != null && !games.Any(g => g.ExecutablePath.Equals(exe, StringComparison.OrdinalIgnoreCase)))
                    {
                        games.Add(new ScannedGame
                        {
                            Name = FormatRiotGameName(gameName),
                            ExecutablePath = exe,
                            Platform = "riot"
                        });
                    }
                }
                catch { }
            }
        }

        return games;
    }

    private static string FormatRiotGameName(string raw)
    {
        return raw switch
        {
            "valorant" => "VALORANT",
            "league_of_legends" => "League of Legends",
            "bacon" => "Legends of Runeterra",
            "teamfight_tactics" => "Teamfight Tactics",
            _ => raw.Replace('_', ' ')
        };
    }

    private static string ExtractRiotGameNameFromPath(string path)
    {
        var parts = path.TrimEnd('\\', '/').Split('\\', '/');
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            var part = parts[i];
            if (part.Equals("live", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("GameDeploy", StringComparison.OrdinalIgnoreCase))
                continue;

            if (parts.Any(p => p.Equals("Riot Games", StringComparison.OrdinalIgnoreCase)) &&
                !part.Equals("Riot Games", StringComparison.OrdinalIgnoreCase))
                return part;
        }
        return parts.Length >= 2 ? parts[^2] : "";
    }

    private static string? ExtractYamlValue(string yaml, string key)
    {
        foreach (var line in yaml.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(key + ":"))
            {
                var value = trimmed.Substring(key.Length + 1).Trim().Trim('"');
                return string.IsNullOrEmpty(value) ? null : value;
            }
        }
        return null;
    }

    private async Task<List<ScannedGame>> ScanUbisoftAsync(List<string> drives)
    {
        var games = new List<ScannedGame>();
        foreach (var drive in drives)
        {
            var dirs = new[]
            {
                Path.Combine(drive, "Program Files (x86)", "Ubisoft", "Ubisoft Game Launcher", "games"),
                Path.Combine(drive, "Ubisoft", "games")
            };
            foreach (var dir in dirs)
            {
                if (Directory.Exists(dir))
                    games.AddRange(await ScanDirectoryAsync(dir, "ubisoft"));
            }
        }
        return games;
    }

    private async Task<List<ScannedGame>> ScanEaAppAsync(List<string> drives)
    {
        var games = new List<ScannedGame>();
        foreach (var drive in drives)
        {
            var dirs = new[]
            {
                Path.Combine(drive, "Program Files", "EA Games"),
                Path.Combine(drive, "Program Files (x86)", "EA Games"),
                Path.Combine(drive, "Program Files (x86)", "Origin Games"),
                Path.Combine(drive, "Origin Games"),
                Path.Combine(drive, "EA Games")
            };
            foreach (var dir in dirs)
            {
                if (Directory.Exists(dir))
                    games.AddRange(await ScanDirectoryAsync(dir, "ea"));
            }
        }
        return games;
    }

    private async Task<List<ScannedGame>> ScanBattleNetAsync(List<string> drives)
    {
        var games = new List<ScannedGame>();
        var battleNetGames = new[]
        {
            "Overwatch", "Overwatch 2",
            "Diablo IV", "Diablo III",
            "World of Warcraft",
            "Call of Duty", "Call of Duty HQ",
            "Hearthstone",
            "StarCraft", "StarCraft II",
            "Heroes of the Storm",
            "Warcraft III", "Warcraft III Reforged"
        };

        foreach (var drive in drives)
        {
            foreach (var game in battleNetGames)
            {
                var gameDir = Path.Combine(drive, "Program Files (x86)", game);
                if (!Directory.Exists(gameDir)) continue;

                var exe = FindBestExecutable(gameDir);
                if (exe != null)
                {
                    games.Add(new ScannedGame
                    {
                        Name = game,
                        ExecutablePath = exe,
                        Platform = "battlenet"
                    });
                }
            }

            var genericBattleNet = Path.Combine(drive, "Program Files (x86)", "Battle.net", "Games");
            if (Directory.Exists(genericBattleNet))
                games.AddRange(await ScanDirectoryAsync(genericBattleNet, "battlenet"));

            var battleNetRoot = Path.Combine(drive, "Battle.net Games");
            if (Directory.Exists(battleNetRoot))
                games.AddRange(await ScanDirectoryAsync(battleNetRoot, "battlenet"));
        }
        return games;
    }

    private async Task<List<ScannedGame>> ScanRockstarAsync(List<string> drives)
    {
        var games = new List<ScannedGame>();
        foreach (var drive in drives)
        {
            var rockstarDir = Path.Combine(drive, "Program Files", "Rockstar Games");
            if (Directory.Exists(rockstarDir))
                games.AddRange(await ScanDirectoryAsync(rockstarDir, "rockstar"));

            var rockstarAlt = Path.Combine(drive, "Rockstar Games");
            if (Directory.Exists(rockstarAlt))
                games.AddRange(await ScanDirectoryAsync(rockstarAlt, "rockstar"));
        }
        return games;
    }

    private async Task<List<ScannedGame>> ScanBethesdaAsync(List<string> drives)
    {
        var games = new List<ScannedGame>();
        foreach (var drive in drives)
        {
            var dirs = new[]
            {
                Path.Combine(drive, "Program Files (x86)", "Bethesda.net Launcher", "games"),
                Path.Combine(drive, "Program Files", "Bethesda.net Launcher", "games"),
                Path.Combine(drive, "Bethesda.net Launcher", "games"),
                Path.Combine(drive, "Bethesda Games")
            };
            foreach (var dir in dirs)
            {
                if (Directory.Exists(dir))
                    games.AddRange(await ScanDirectoryAsync(dir, "bethesda"));
            }
        }
        return games;
    }

    private async Task<List<ScannedGame>> ScanAmazonGamesAsync(List<string> drives)
    {
        var games = new List<ScannedGame>();
        foreach (var drive in drives)
        {
            var dirs = new[]
            {
                Path.Combine(drive, "Program Files (x86)", "Amazon Games", "Games"),
                Path.Combine(drive, "Program Files", "Amazon Games", "Games"),
                Path.Combine(drive, "Amazon Games")
            };
            foreach (var dir in dirs)
            {
                if (Directory.Exists(dir))
                    games.AddRange(await ScanDirectoryAsync(dir, "amazon"));
            }
        }
        return games;
    }

    private static string? ExtractVdfValueFromLine(string line)
    {
        var firstQuote = line.IndexOf('"', 0);
        if (firstQuote < 0) return null;
        var secondQuote = line.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0) return null;
        var thirdQuote = line.IndexOf('"', secondQuote + 1);
        if (thirdQuote < 0) return null;
        var fourthQuote = line.IndexOf('"', thirdQuote + 1);
        if (fourthQuote < 0) return null;
        return line.Substring(thirdQuote + 1, fourthQuote - thirdQuote - 1);
    }

    private async Task FetchSteamCoversAsync(List<ScannedGame> games)
    {
        var tasks = games
            .Where(g => !string.IsNullOrEmpty(g.SteamAppId))
            .Select(async g =>
            {
                await _steamApiSemaphore.WaitAsync();
                try
                {
                    var info = await _steamStore.GetStoreInfoAsync(g.SteamAppId);
                    if (info != null && !string.IsNullOrEmpty(info.HeaderImage))
                        g.CoverImage = info.HeaderImage;
                }
                catch { }
                finally
                {
                    _steamApiSemaphore.Release();
                }
            });

        await Task.WhenAll(tasks);
    }

    private async Task<List<ScannedGame>> ScanDirectoryAsync(string rootDir, string platform)
    {
        var games = new List<ScannedGame>();
        if (!Directory.Exists(rootDir)) return games;

        var foundGames = new Dictionary<string, (string exePath, string gameDir)>(StringComparer.OrdinalIgnoreCase);

        var subDirs = Directory.GetDirectories(rootDir);
        foreach (var gameDir in subDirs)
        {
            var gameFolderName = Path.GetFileName(gameDir);

            if (NonGameDirPatterns.Any(p => gameFolderName.Equals(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            string? bestExe = FindBestExecutable(gameDir);
            if (bestExe != null)
                foundGames[gameFolderName] = (bestExe, gameDir);
        }

        foreach (var (folderName, (exePath, gameDir)) in foundGames)
        {
            var game = new ScannedGame
            {
                Name = folderName,
                ExecutablePath = exePath,
                Platform = platform
            };

            var localCover = FindLocalCoverImage(gameDir);
            if (localCover != null)
                game.CoverImage = localCover;

            games.Add(game);
        }

        await SearchCoversByGameNameAsync(games);
        return games;
    }

    private string? FindBestExecutable(string gameDir)
    {
        try
        {
            var exes = Directory.GetFiles(gameDir, "*.exe", SearchOption.AllDirectories);
            string? bestExe = null;
            int bestDepth = int.MaxValue;

            foreach (var exe in exes)
            {
                var exeName = Path.GetFileNameWithoutExtension(exe);
                if (NonGameExePatterns.Any(p => exeName.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var exeDir = Path.GetDirectoryName(exe) ?? "";
                var dirName = Path.GetFileName(exeDir);
                if (NonGameDirPatterns.Any(p => dirName.Equals(p, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var parentDir = Path.GetFileName(Path.GetDirectoryName(exeDir) ?? "");
                if (NonGameDirPatterns.Any(p => parentDir.Equals(p, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var depth = exe.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length
                            - gameDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;

                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    bestExe = exe;
                }
            }

            return bestExe;
        }
        catch
        {
            return null;
        }
    }

    private string? FindLocalCoverImage(string gameDir)
    {
        try
        {
            var candidates = new List<(string path, long size, int priority)>();

            foreach (var pattern in CoverImagePatterns)
            {
                int priority = pattern.Contains("Splash", StringComparison.OrdinalIgnoreCase) ? 1 :
                               pattern.Contains("Logo", StringComparison.OrdinalIgnoreCase) ? 2 :
                               pattern.Contains("cover", StringComparison.OrdinalIgnoreCase) ? 3 : 4;

                foreach (var file in Directory.GetFiles(gameDir, pattern, SearchOption.AllDirectories))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.Length < 10240) continue;
                        candidates.Add((file, info.Length, priority));
                    }
                    catch { }
                }
            }

            return candidates
                .OrderBy(c => c.priority)
                .ThenByDescending(c => c.size)
                .Select(c => "file:///" + c.path.Replace('\\', '/'))
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private async Task SearchCoversByGameNameAsync(List<ScannedGame> games)
    {
        foreach (var g in games)
        {
            if (!string.IsNullOrEmpty(g.CoverImage)) continue;

            try
            {
                var appId = await SearchSteamAppIdAsync(g.Name);
                if (!string.IsNullOrEmpty(appId))
                {
                    g.SteamAppId = appId;
                    var info = await _steamStore.GetStoreInfoAsync(appId);
                    if (info != null && !string.IsNullOrEmpty(info.HeaderImage))
                        g.CoverImage = info.HeaderImage;
                }
            }
            catch { }

            if (string.IsNullOrEmpty(g.CoverImage))
                g.CoverImage = GetTwitchBoxArtUrl(g.Name);
        }
    }

    private async Task<string> SearchSteamAppIdAsync(string gameName)
    {
        if (_steamSearchCache.TryGetValue(gameName, out var cached))
            return cached;

        try
        {
            var encoded = Uri.EscapeDataString(gameName);
            var url = $"https://store.steampowered.com/api/storesearch?term={encoded}&l=english&cc=us";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("items", out var items) &&
                items.GetArrayLength() > 0)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameEl))
                    {
                        var foundName = nameEl.GetString() ?? "";
                        if (IsMatch(gameName, foundName))
                        {
                            if (item.TryGetProperty("id", out var id))
                            {
                                var appId = id.GetInt32().ToString();
                                _steamSearchCache[gameName] = appId;
                                return appId;
                            }
                        }
                    }
                }
            }
        }
        catch { }

        _steamSearchCache[gameName] = string.Empty;
        return string.Empty;
    }

    private static bool IsMatch(string searchName, string foundName)
    {
        if (string.Equals(searchName, foundName, StringComparison.OrdinalIgnoreCase))
            return true;

        var sn = Normalize(searchName);
        var fn = Normalize(foundName);

        if (sn == fn)
            return true;

        var snWords = searchName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var fnWords = foundName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (snWords.Length >= 2 && fnWords.Length >= 2)
        {
            var matchCount = snWords.Count(sw =>
                fnWords.Any(fw => string.Equals(sw, fw, StringComparison.OrdinalIgnoreCase)));
            if (matchCount >= 2 && matchCount >= snWords.Length * 0.7)
                return true;
        }

        return sn.Contains(fn) || fn.Contains(sn);
    }

    private static string Normalize(string name)
    {
        return new string(name
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray())
            .ToLowerInvariant();
    }

    private static string ExtractVdfValue(string vdf, string key)
    {
        var pattern = $"\"{key}\"\\s+\"([^\"]+)\"";
        var match = System.Text.RegularExpressions.Regex.Match(vdf, pattern);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
