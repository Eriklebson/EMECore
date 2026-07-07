using System.Text.Json;

namespace EMECore.Hardware.Services;

public class CpuSensorMappingService
{
    private static readonly Lazy<CpuSensorMappingService> _instance = new(() => new CpuSensorMappingService());
    public static CpuSensorMappingService Instance => _instance.Value;

    private Dictionary<string, CpuArchitectureMapping> _architectures = new();
    private string? _matchedArchitecture;
    private bool _loaded;

    private CpuSensorMappingService() { }

    public void Load()
    {
        if (_loaded) return;

        try
        {
            var paths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "config", "cpu-sensors-mapping.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "config", "cpu-sensors-mapping.json"),
                @"config\cpu-sensors-mapping.json"
            };

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var doc = JsonSerializer.Deserialize<CpuMappingRoot>(json, options);
                    if (doc?.Architectures != null)
                    {
                        _architectures = doc.Architectures;
                        _loaded = true;
                        return;
                    }
                }
            }
        }
        catch { }
    }

    public void DetectCpuArchitecture(string cpuName)
    {
        if (_matchedArchitecture != null) return;

        foreach (var (archName, mapping) in _architectures)
        {
            if (mapping.Match?.NameContains == null) continue;

            var patterns = mapping.Match.NameContains;
            foreach (var pattern in patterns)
            {
                if (cpuName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    _matchedArchitecture = archName;
                    return;
                }
            }
        }

        // Fallback to generic
        if (cpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase) || 
            cpuName.Contains("Ryzen", StringComparison.OrdinalIgnoreCase))
        {
            _matchedArchitecture = "AMD_Generic";
        }
        else if (cpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase) || 
                 cpuName.Contains("Core", StringComparison.OrdinalIgnoreCase))
        {
            _matchedArchitecture = "Intel_Generic";
        }
    }

    public string GetTempSensorName()
    {
        return GetSensorValue("temp_sensor") ?? "CPU";
    }

    public string GetVoltageSensorName()
    {
        return GetSensorValue("voltage_sensor") ?? "Vcore";
    }

    public string GetPowerSensorName()
    {
        return GetSensorValue("power_sensor") ?? "CPU Package";
    }

    public List<string> GetTempFallbacks()
    {
        if (_matchedArchitecture == null || !_architectures.TryGetValue(_matchedArchitecture, out var mapping))
            return new List<string>();

        return mapping.Sensors?.TempFallback ?? new List<string>();
    }

    private string? GetSensorValue(string sensorType)
    {
        if (_matchedArchitecture == null || !_architectures.TryGetValue(_matchedArchitecture, out var mapping))
            return null;

        return sensorType switch
        {
            "temp_sensor" => mapping.Sensors?.TempSensor,
            "voltage_sensor" => mapping.Sensors?.VoltageSensor,
            "power_sensor" => mapping.Sensors?.PowerSensor,
            _ => null
        };
    }

    public string? DetectedArchitecture => _matchedArchitecture;
}

// JSON Models
public class CpuMappingRoot
{
    public Dictionary<string, CpuArchitectureMapping>? Architectures { get; set; }
}

public class CpuArchitectureMapping
{
    public CpuMatchCriteria? Match { get; set; }
    public CpuSensorNames? Sensors { get; set; }
}

public class CpuMatchCriteria
{
    public List<string>? NameContains { get; set; }
}

public class CpuSensorNames
{
    public string? TempSensor { get; set; }
    public string? VoltageSensor { get; set; }
    public string? PowerSensor { get; set; }
    public List<string>? TempFallback { get; set; }
}
