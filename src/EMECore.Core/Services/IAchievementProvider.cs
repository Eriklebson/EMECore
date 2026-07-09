using EMECore.Core.Models;

namespace EMECore.Core.Services;

public interface IAchievementProvider
{
    string ProviderName { get; }
    bool CanHandle(Game game);
    Task<List<Achievement>> GetAchievementsAsync(Game game);
    Task<List<Achievement>> CheckAchievementsAsync(Game game);
    Task<List<Achievement>> RefreshAchievementsAsync(Game game);
}

public interface IAchievementCheckerService
{
    Task<List<IAchievementProvider>> GetProvidersAsync();
    Task<List<Achievement>> GetAllAchievementsAsync(Game game);
    Task<List<Achievement>> CheckAllAchievementsAsync(Game game);
    Task<bool> IsSaveBasedProviderAvailableAsync(Game game);
}
