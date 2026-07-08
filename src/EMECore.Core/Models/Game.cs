namespace EMECore.Core.Models;

public class Game
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string CoverImage { get; set; } = string.Empty;
    public string Platform { get; set; } = "other";
    public DateTime? LastPlayed { get; set; }
    public int PlayTime { get; set; }
    public DateTime? LastSessionStart { get; set; }
    public string SteamAppId { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
