using EMECore.Core.Models;

namespace EMECore.Core.Services;

public interface ISaveDiscoveryService
{
    Task<List<GameSaveInfo>> DiscoverSavesAsync(Game game);
    Task<List<SaveFile>> FindSaveFilesAsync(GameSaveInfo gameSave);
    List<SaveLocation> GetKnownSaveLocations(Game game);
}
