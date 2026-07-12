using System.Text.Json.Serialization;

namespace EMECore.Hardware.Services;

public class GameConfigRoot
{
    [JsonPropertyName("games")]
    public Dictionary<string, GameConfig> Games { get; set; } = new();
}

public class GameConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("namePatterns")]
    public List<string> NamePatterns { get; set; } = new();

    [JsonPropertyName("steamAppIds")]
    public List<string> SteamAppIds { get; set; } = new();

    [JsonPropertyName("saveLocations")]
    public List<SaveLocationConfig> SaveLocations { get; set; } = new();

    [JsonPropertyName("parserType")]
    public string ParserType { get; set; } = "none";

    [JsonPropertyName("achievementDb")]
    public string AchievementDb { get; set; } = "";

    [JsonPropertyName("note")]
    public string Note { get; set; } = "";
}

public class SaveLocationConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "*";

    [JsonPropertyName("recursive")]
    public bool Recursive { get; set; } = false;
}
