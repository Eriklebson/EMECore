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

    public GameScannerService(ISteamStoreService steamStore)
    {
        _steamStore = steamStore;
    }

    public async Task<List<ScannedGame>> ScanAllGamesAsync()
    {
        var games = new List<ScannedGame>();
        games.AddRange(await ScanSteamAsync());
        games.AddRange(await ScanDirectoryAsync(@"C:\XboxGames", "xbox"));
        games.AddRange(await ScanDirectoryAsync(@"C:\Games", "other"));
        games.AddRange(await ScanDirectoryAsync(@"D:\Games", "other"));
        games.AddRange(await ScanDirectoryAsync(@"E:\Games", "other"));
        return games;
    }

    private async Task<List<ScannedGame>> ScanSteamAsync()
    {
        var games = new List<ScannedGame>();
        var steamPaths = new[]
        {
            @"C:\Program Files (x86)\Steam\steamapps",
            @"D:\Steam\steamapps",
            @"E:\Steam\steamapps",
            @"C:\SteamLibrary\steamapps"
        };

        foreach (var steamPath in steamPaths)
        {
            var libraryFolders = Path.Combine(steamPath, "libraryfolders.vdf");
            if (!File.Exists(libraryFolders)) continue;

            var dirs = new List<string> { steamPath };

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var acf in Directory.GetFiles(dir, "appmanifest_*.acf"))
                {
                    try
                    {
                        var acfContent = await File.ReadAllTextAsync(acf);
                        var name = ExtractVdfValue(acfContent, "name");
                        var appId = ExtractVdfValue(acfContent, "appid");
                        var installdir = ExtractVdfValue(acfContent, "installdir");
                        var exePath = Path.Combine(dir, "common", installdir);

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
            }
        }

        await FetchSteamCoversAsync(games);
        return games;
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
        var tasks = games
            .Where(g => string.IsNullOrEmpty(g.CoverImage))
            .Select(async g =>
        {
            await _steamApiSemaphore.WaitAsync();
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
            finally
            {
                _steamApiSemaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
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
