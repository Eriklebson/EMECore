using EMECore.Core.Models;

namespace EMECore.Core.Services;

public interface ISteamStoreService
{
    Task<SteamStoreInfo?> GetStoreInfoAsync(string appId);
}
