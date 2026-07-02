namespace EMECore.Hardware.Services;

public class StellarBladeParser
{
    public bool HasSave() => false;
    public Task<List<Core.Models.Achievement>> ParseSaveAsync() =>
        Task.FromResult(new List<Core.Models.Achievement>());
}
