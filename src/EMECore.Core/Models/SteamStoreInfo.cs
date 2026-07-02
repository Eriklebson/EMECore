namespace EMECore.Core.Models;

public class SteamStoreInfo
{
    public string Name { get; set; } = string.Empty;
    public string HeaderImage { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<SteamScreenshot> Screenshots { get; set; } = new();
}

public class SteamScreenshot
{
    public string PathFull { get; set; } = string.Empty;
    public string PathThumbnail { get; set; } = string.Empty;
}
