using LibreHardwareMonitor.Hardware;

namespace EMECore.Hardware.Services;

public class LhmHardwareService : IDisposable
{
    private readonly Computer _computer;
    private bool _disposed;

    public LhmHardwareService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsMemoryEnabled = true,
            IsNetworkEnabled = true
        };
        _computer.Open();
    }

    public IList<IHardware> Hardware => _computer.Hardware;

    public void Update()
    {
        foreach (var hw in _computer.Hardware)
        {
            hw.Update();
            foreach (var sub in hw.SubHardware)
                sub.Update();
        }
    }

    public IEnumerable<ISensor> GetAllSensors()
    {
        foreach (var hw in _computer.Hardware)
        {
            foreach (var sensor in hw.Sensors)
                yield return sensor;

            foreach (var sub in hw.SubHardware)
            {
                foreach (var sensor in sub.Sensors)
                    yield return sensor;
            }
        }
    }

    public IEnumerable<ISensor> GetSensors(SensorType type)
    {
        return GetAllSensors().Where(s => s.SensorType == type && s.Value.HasValue);
    }

    public IEnumerable<IHardware> GetHardware(HardwareType type)
    {
        return _computer.Hardware.Where(h => h.HardwareType == type);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _computer.Close();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
