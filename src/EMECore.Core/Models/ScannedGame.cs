namespace EMECore.Core.Models;

public class ScannedGame
{
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Platform { get; set; } = "other";
    public string SteamAppId { get; set; } = string.Empty;
    public string CoverImage { get; set; } = string.Empty;
}
