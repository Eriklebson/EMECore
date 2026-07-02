namespace EMECore.Core.Models;

public class StellarBladeSaveData
{
    public string SteamId { get; set; } = string.Empty;
    public string SavePath { get; set; } = string.Empty;
    public string LastModified { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public List<StellarBladeTrophy> Trophies { get; set; } = new();
    public List<string> QuestCompletions { get; set; } = new();
    public bool KillElderEnding { get; set; }
    public bool KillLilyEnding { get; set; }
    public bool SaveLilyEnding { get; set; }
    public int NewGamePlusCount { get; set; }
}

public class StellarBladeTrophy
{
    public string Name { get; set; } = string.Empty;
    public string SteamAchievement { get; set; } = string.Empty;
    public bool BCompleted { get; set; }
    public int ProgressValue { get; set; }
}
