using EMECore.Core.Models;

namespace EMECore.Core.Services;

public interface IGameScannerService
{
    Task<List<ScannedGame>> ScanAllGamesAsync();
}
