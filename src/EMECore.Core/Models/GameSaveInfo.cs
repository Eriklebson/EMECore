namespace EMECore.Core.Models;

public class GameSaveInfo
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public List<SaveLocation> SaveLocations { get; set; } = new();
    public string? AchievementDbKey { get; set; }
}

public class SaveLocation
{
    public string Description { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public string FilePattern { get; set; } = "*";
    public bool Recursive { get; set; }
    public SaveFormat ExpectedFormat { get; set; } = SaveFormat.Unknown;
    public int Priority { get; set; }
}

public class SaveFile
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public SaveFormat Format { get; set; } = SaveFormat.Unknown;
    public byte[]? RawData { get; set; }
    public Dictionary<string, object>? ParsedData { get; set; }
}

public enum SaveFormat
{
    Unknown,
    Json,
    Xml,
    Binary,
    Ini,
    Sqlite,
    Text,
    Csv
}
