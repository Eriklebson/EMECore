using EMECore.Core.Models;
using EMECore.Core.Services;

namespace EMECore.Hardware.Services;

public class SaveDiscoveryService : ISaveDiscoveryService
{
    public async Task<List<GameSaveInfo>> DiscoverSavesAsync(Game game)
    {
        var result = new List<GameSaveInfo>();
        var locations = GetKnownSaveLocations(game);

        var saveInfo = new GameSaveInfo
        {
            GameId = game.Id,
            GameName = game.Name,
            Platform = game.Platform,
            ExecutablePath = game.ExecutablePath,
            SaveLocations = locations
        };

        result.Add(saveInfo);
        return result;
    }

    public async Task<List<SaveFile>> FindSaveFilesAsync(GameSaveInfo gameSave)
    {
        var files = new List<SaveFile>();

        foreach (var location in gameSave.SaveLocations)
        {
            if (!Directory.Exists(location.DirectoryPath)) continue;

            try
            {
                var searchOption = location.Recursive
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var foundFiles = Directory.GetFiles(location.DirectoryPath, location.FilePattern, searchOption);
                foreach (var filePath in foundFiles)
                {
                    try
                    {
                        var info = new FileInfo(filePath);
                        files.Add(new SaveFile
                        {
                            FullPath = filePath,
                            FileName = info.Name,
                            DirectoryPath = info.DirectoryName ?? "",
                            FileSize = info.Length,
                            LastModified = info.LastWriteTime,
                            Format = DetectFormat(filePath)
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        files.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));
        return files;
    }

    public List<SaveLocation> GetKnownSaveLocations(Game game)
    {
        var locations = new List<SaveLocation>();
        var exeDir = Path.GetDirectoryName(game.ExecutablePath) ?? "";

        if (!string.IsNullOrEmpty(exeDir))
        {
            locations.Add(new SaveLocation { Description = "Pasta do jogo", DirectoryPath = exeDir, FilePattern = "*.sav", Recursive = true, Priority = 1 });
            locations.Add(new SaveLocation { Description = "Pasta do jogo (save)", DirectoryPath = Path.Combine(exeDir, "SaveGames"), FilePattern = "*", Recursive = true, Priority = 2 });
            locations.Add(new SaveLocation { Description = "Pasta do jogo (saves)", DirectoryPath = Path.Combine(exeDir, "Saves"), FilePattern = "*", Recursive = true, Priority = 3 });
            locations.Add(new SaveLocation { Description = "Pasta do jogo (data)", DirectoryPath = Path.Combine(exeDir, "Data"), FilePattern = "*save*", Recursive = true, Priority = 4 });
            locations.Add(new SaveLocation { Description = "Pasta do jogo (content)", DirectoryPath = Path.Combine(exeDir, "Content"), FilePattern = "*save*", Recursive = true, Priority = 5 });
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var gameFolders = new[]
        {
            Path.Combine(localAppData, game.Name),
            Path.Combine(appData, game.Name),
            Path.Combine(documents, "My Games", game.Name),
            Path.Combine(documents, game.Name),
            Path.Combine(userProfile, "Saved Games", game.Name),
            Path.Combine(userProfile, "Documents", "My Games", game.Name),
            Path.Combine(userProfile, "Documents", game.Name)
        };

        foreach (var folder in gameFolders)
        {
            if (Directory.Exists(folder))
            {
                locations.Add(new SaveLocation { Description = $"AppData: {Path.GetFileName(folder)}", DirectoryPath = folder, FilePattern = "*", Recursive = true, Priority = 10 });
            }
        }

        return locations;
    }

    private static SaveFormat DetectFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".json" => SaveFormat.Json,
            ".xml" => SaveFormat.Xml,
            ".sav" => SaveFormat.Binary,
            ".save" => SaveFormat.Binary,
            ".dat" => SaveFormat.Binary,
            ".bin" => SaveFormat.Binary,
            ".cfg" or ".ini" or ".conf" => SaveFormat.Ini,
            ".db" or ".sqlite" or ".sqlite3" => SaveFormat.Sqlite,
            ".txt" or ".log" => SaveFormat.Text,
            ".csv" => SaveFormat.Csv,
            _ => SaveFormat.Unknown
        };
    }
}
