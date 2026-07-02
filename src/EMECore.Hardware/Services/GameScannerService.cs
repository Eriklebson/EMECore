using System.Diagnostics;
using EMECore.Core.Models;
using EMECore.Core.Services;

namespace EMECore.Hardware.Services;

public class GameScannerService : IGameScannerService
{
    private readonly ISteamStoreService _steamStore;

    public GameScannerService(ISteamStoreService steamStore)
    {
        _steamStore = steamStore;
    }

    public async Task<List<ScannedGame>> ScanAllGamesAsync()
    {
        var games = new List<ScannedGame>();
        games.AddRange(await ScanSteamAsync());
        games.AddRange(await ScanCommonDirsAsync());
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

            var content = await File.ReadAllTextAsync(libraryFolders);
            // Parse library folders and scan for appmanifest files
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
        return games;
    }

    private Task<List<ScannedGame>> ScanCommonDirsAsync()
    {
        var games = new List<ScannedGame>();
        var dirs = new[] { @"C:\Games", @"D:\Games", @"E:\Games" };
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var exe in Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories))
            {
                games.Add(new ScannedGame
                {
                    Name = Path.GetFileNameWithoutExtension(exe),
                    ExecutablePath = exe,
                    Platform = "other"
                });
            }
        }
        return Task.FromResult(games);
    }

    private static string ExtractVdfValue(string vdf, string key)
    {
        var pattern = $"\"{key}\"\\s+\"([^\"]+)\"";
        var match = System.Text.RegularExpressions.Regex.Match(vdf, pattern);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
