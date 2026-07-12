using EMECore.Core.Models;
using EMECore.Core.Services;

namespace EMECore.Hardware.Services;

public class AchievementCheckerService : IAchievementCheckerService
{
    private readonly IDatabaseService _database;
    private readonly List<IAchievementProvider> _providers = new();
    private readonly SaveBasedAchievementProvider _saveProvider;

    public AchievementCheckerService(SaveBasedAchievementProvider saveProvider, IDatabaseService database)
    {
        _saveProvider = saveProvider;
        _database = database;
        _providers.Add(saveProvider);
    }

    public Task<List<IAchievementProvider>> GetProvidersAsync()
    {
        return Task.FromResult(_providers);
    }

    public async Task<List<Achievement>> GetAllAchievementsAsync(Game game)
    {
        var allAchievements = new List<Achievement>();

        foreach (var provider in _providers.Where(p => p.CanHandle(game)))
        {
            try
            {
                var achievements = await provider.GetAchievementsAsync(game);
                foreach (var a in achievements)
                {
                    if (!allAchievements.Any(x => x.Apiname == a.Apiname))
                    {
                        allAchievements.Add(a);
                    }
                }
            }
            catch { }
        }

        return allAchievements;
    }

    public async Task<List<Achievement>> CheckAllAchievementsAsync(Game game)
    {
        var allAchievements = new List<Achievement>();

        foreach (var provider in _providers.Where(p => p.CanHandle(game)))
        {
            try
            {
                var achievements = await provider.CheckAchievementsAsync(game);
                foreach (var a in achievements)
                {
                    var existing = allAchievements.FirstOrDefault(x => x.Apiname == a.Apiname);
                    if (existing == null)
                    {
                        allAchievements.Add(a);
                    }
                    else if (a.Achieved && !existing.Achieved)
                    {
                        existing.Achieved = true;
                        existing.Unlocktime = a.Unlocktime;
                        existing.Progress = a.Progress;
                    }
                }
            }
            catch { }
        }

        if (allAchievements.Count > 0)
        {
            try { await _database.SaveAchievementsAsync(game.Id, allAchievements); } catch { }
            return allAchievements;
        }

        try
        {
            var saved = await _database.GetAchievementsAsync(game.Id);
            if (saved.Count > 0) return saved;
        }
        catch { }

        return new List<Achievement>();
    }

    public async Task<bool> IsSaveBasedProviderAvailableAsync(Game game)
    {
        return await Task.FromResult(_saveProvider.CanHandle(game));
    }
}
