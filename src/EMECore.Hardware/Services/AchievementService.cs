using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class AchievementService
{
    private readonly StellarBladeParser _stellarBladeParser;
    private readonly SteamStoreService _steamStore;

    public AchievementService()
    {
        _stellarBladeParser = new StellarBladeParser();
        _steamStore = new SteamStoreService();
    }

    public async Task<List<Achievement>> GetAchievementsAsync(Game game)
    {
        if (game.SteamAppId == "3489700")
        {
            return _stellarBladeParser.ParseAchievements();
        }

        if (!string.IsNullOrEmpty(game.SteamAppId))
        {
            return await GetSteamAchievementsAsync(game.SteamAppId);
        }

        return new List<Achievement>();
    }

    private async Task<List<Achievement>> GetSteamAchievementsAsync(string appId)
    {
        try
        {
            var storeInfo = await _steamStore.GetStoreInfoAsync(appId);
            if (storeInfo == null) return new List<Achievement>();

            return new List<Achievement>
            {
                new() { Name = "Steam Achievements", Description = "Conquistas Steam disponiveis", Achieved = false }
            };
        }
        catch
        {
            return new List<Achievement>();
        }
    }
}
