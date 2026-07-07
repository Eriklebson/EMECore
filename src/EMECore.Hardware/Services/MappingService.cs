using System;
using System.IO;
using System.Text.Json;
using EMECore.Hardware.Models;

namespace EMECore.Hardware.Services;

public class MappingService
{
    private HardwareMappingConfig? _config;
    private MotherboardMapping? _currentMotherboard;

    public bool IsLoaded => _config != null;
    public MotherboardMapping? CurrentMotherboard => _currentMotherboard;

    public void Load()
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "config", "hardware-mapping.json");
            if (!File.Exists(configPath))
            {
                System.Diagnostics.Debug.WriteLine($"[MappingService] Config not found: {configPath}");
                return;
            }

            var json = File.ReadAllText(configPath);
            _config = JsonSerializer.Deserialize<HardwareMappingConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            System.Diagnostics.Debug.WriteLine($"[MappingService] Config loaded: {_config?.Motherboards.Count ?? 0} motherboard(s)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MappingService] Error loading config: {ex.Message}");
        }
    }

    public void DetectMotherboard(string motherboardName)
    {
        if (_config == null)
        {
            Load();
        }

        if (_config?.Motherboards == null || _config.Motherboards.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[MappingService] No motherboard mappings available");
            return;
        }

        _currentMotherboard = _config.Motherboards.Find(m =>
            motherboardName.Contains(m.Name, StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains(motherboardName, StringComparison.OrdinalIgnoreCase));

        if (_currentMotherboard != null)
        {
            System.Diagnostics.Debug.WriteLine($"[MappingService] Detected motherboard: {_currentMotherboard.Name}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[MappingService] No mapping found for: {motherboardName}");
        }
    }

    public string MapFanName(string lhmName)
    {
        if (_currentMotherboard?.FanMapping != null &&
            _currentMotherboard.FanMapping.TryGetValue(lhmName, out var mapped))
        {
            return mapped;
        }

        return lhmName;
    }

    public string MapTemperatureName(string lhmName)
    {
        if (_currentMotherboard?.TemperatureMapping != null &&
            _currentMotherboard.TemperatureMapping.TryGetValue(lhmName, out var mapped))
        {
            return mapped;
        }

        return lhmName;
    }

    public string MapVoltageName(string lhmName)
    {
        if (_currentMotherboard?.VoltageMapping != null &&
            _currentMotherboard.VoltageMapping.TryGetValue(lhmName, out var mapped))
        {
            return mapped;
        }

        return lhmName;
    }

    public string GetFanCategory(string fanName)
    {
        if (_currentMotherboard?.FanCategories == null)
        {
            return _config?.DefaultMapping?.FanCategory ?? "Motherboard";
        }

        if (_currentMotherboard.FanCategories.CPU.Contains(fanName))
            return "CPU";

        if (_currentMotherboard.FanCategories.GPU.Contains(fanName))
            return "GPU";

        if (_currentMotherboard.FanCategories.Motherboard.Contains(fanName))
            return "Motherboard";

        return _config?.DefaultMapping?.FanCategory ?? "Motherboard";
    }

    public static MappingService Instance { get; } = new();
}
