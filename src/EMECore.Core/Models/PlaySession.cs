namespace EMECore.Core.Models;

public class PlaySession
{
    public int Id { get; set; }
    public string GameId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationMinutes { get; set; }
}
