namespace EMECore.Hardware.Services;

public static class LocalizedPaths
{
    private static readonly string[] MyGamesNames = new[]
    {
        "My Games",
        "Meus Jogos",
        "Mes Jeux",
        "Mis Juegos",
        "Meine Spiele",
        "Mijn Spellen",
        "Moje Gry",
        "Moje Hry",
        "Мои игры",
        "マイ ゲーム",
        "我的游戏",
        "내 게임",
    };

    private static readonly string[] SavedGamesNames = new[]
    {
        "Saved Games",
        "Jogos Salvos",
        "Juegos Guardados",
        "Jeux Sauvegardés",
        "Gespeicherte Spiele",
        "Opgeslagen spellen",
        "Zapisane gry",
        "Uložené hry",
        "Сохраненные игры",
    };

    public static string? FindMyGamesSubPath(string gameFolder, string subPath = "")
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        foreach (var name in MyGamesNames)
        {
            var candidate = string.IsNullOrEmpty(subPath)
                ? Path.Combine(docs, name, gameFolder)
                : Path.Combine(docs, name, gameFolder, subPath);

            if (Directory.Exists(candidate))
                return candidate;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var name in MyGamesNames)
        {
            var candidate = string.IsNullOrEmpty(subPath)
                ? Path.Combine(userProfile, "AppData", "Roaming", name, gameFolder)
                : Path.Combine(userProfile, "AppData", "Roaming", name, gameFolder, subPath);

            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static string? FindSaveFile(string gameFolder, string filePattern)
    {
        var dir = FindMyGamesSubPath(gameFolder);
        if (dir == null) return null;

        try
        {
            var files = Directory.GetFiles(dir, filePattern)
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .ToArray();

            if (files.Length > 0) return files[0];
        }
        catch { }

        return null;
    }

    public static string[] GetAllSaveFiles(string gameFolder, string filePattern)
    {
        var dir = FindMyGamesSubPath(gameFolder);
        if (dir == null) return Array.Empty<string>();

        try
        {
            return Directory.GetFiles(dir, filePattern)
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .ToArray();
        }
        catch { }

        return Array.Empty<string>();
    }

    public static string? FindLocalAppDataSubPath(string appDataFolder, string subPath = "")
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var paths = new[]
        {
            Path.Combine(localAppData, appDataFolder),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", appDataFolder),
        };

        foreach (var basePath in paths)
        {
            var candidate = string.IsNullOrEmpty(subPath) ? basePath : Path.Combine(basePath, subPath);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static string? FindLocalAppDataFile(string appDataFolder, string filePattern)
    {
        var dir = FindLocalAppDataSubPath(appDataFolder);
        if (dir == null) return null;

        try
        {
            var files = Directory.GetFiles(dir, filePattern)
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .ToArray();

            if (files.Length > 0) return files[0];
        }
        catch { }

        return null;
    }

    public static string[] GetAllLocalAppDataFiles(string appDataFolder, string filePattern)
    {
        var dir = FindLocalAppDataSubPath(appDataFolder);
        if (dir == null) return Array.Empty<string>();

        try
        {
            return Directory.GetFiles(dir, filePattern)
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .ToArray();
        }
        catch { }

        return Array.Empty<string>();
    }

    public static string? FindAppDataSubPath(string appDataFolder, string subPath = "")
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var paths = new[]
        {
            Path.Combine(appData, appDataFolder),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming", appDataFolder),
        };

        foreach (var basePath in paths)
        {
            var candidate = string.IsNullOrEmpty(subPath) ? basePath : Path.Combine(basePath, subPath);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
