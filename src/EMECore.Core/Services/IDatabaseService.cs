namespace EMECore.Core.Services;

public interface IDatabaseService
{
    Task InitializeAsync(string dbPath);
    Task<List<Models.Game>> GetGamesAsync();
    Task<Models.Game?> GetGameAsync(string id);
    Task UpsertGameAsync(Models.Game game);
    Task DeleteGameAsync(string id);
    Task DeleteAllGamesAsync();
    Task UpdateGamePlayTimeAsync(string id, int playTime, DateTime? lastSessionStart);
    Task RecordPlaySessionAsync(string id, DateTime startTime, int durationMinutes);
    Task<List<Models.PlaySession>> GetPlaySessionsAsync(string gameId);
    Task SaveAchievementsAsync(string gameId, List<Models.Achievement> achievements);
    Task<List<Models.Achievement>> GetAchievementsAsync(string gameId);
    Task<int> GetTotalPlayTimeAsync();
    Task<Dictionary<string, int>> GetGameCountAsync();
    Task CloseAsync();
}
