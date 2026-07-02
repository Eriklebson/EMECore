namespace EMECore.Hardware.Services;

public class AchievementService
{
    public Task<List<Core.Models.Achievement>> CheckAchievementsAsync(string gameId) =>
        Task.FromResult(new List<Core.Models.Achievement>());
}
