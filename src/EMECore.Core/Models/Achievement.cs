namespace EMECore.Core.Models;

public class Achievement
{
    public int Id { get; set; }
    public string GameId { get; set; } = string.Empty;
    public string Apiname { get; set; } = string.Empty;
    public bool Achieved { get; set; }
    public long Unlocktime { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Icongray { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
