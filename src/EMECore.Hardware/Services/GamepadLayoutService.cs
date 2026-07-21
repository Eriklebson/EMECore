using System.Text.Json;

namespace EMECore.Hardware.Services;

public class GamepadButtonLayout
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Radius { get; set; }
}

public class GamepadLayout
{
    public int ImageWidth { get; set; } = 360;
    public int ImageHeight { get; set; } = 256;
    public Dictionary<string, GamepadButtonLayout> Buttons { get; set; } = new();
}

public static class GamepadLayoutService
{
    private static readonly string LayoutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EMECore", "config", "gamepad-layout.json");

    private static GamepadLayout? _cached;

    public static GamepadLayout Load()
    {
        if (_cached != null) return _cached;

        try
        {
            if (File.Exists(LayoutPath))
            {
                var json = File.ReadAllText(LayoutPath);
                var loaded = JsonSerializer.Deserialize<GamepadLayout>(json);
                if (loaded?.Buttons?.Count > 0)
                {
                    _cached = loaded;
                    return loaded;
                }
            }
        }
        catch { }

        _cached = CreateDefault();
        return _cached;
    }

    private static GamepadLayout CreateDefault()
    {
        var layout = new GamepadLayout();
        var btns = layout.Buttons;
        btns["LeftStick"]  = new() { X = 0.220, Y = 0.580, Radius = 0.065 };
        btns["RightStick"] = new() { X = 0.390, Y = 0.580, Radius = 0.065 };
        btns["DPadUp"]    = new() { X = 0.220, Y = 0.260, Radius = 0.030 };
        btns["DPadDown"]  = new() { X = 0.220, Y = 0.450, Radius = 0.030 };
        btns["DPadLeft"]  = new() { X = 0.155, Y = 0.355, Radius = 0.030 };
        btns["DPadRight"] = new() { X = 0.285, Y = 0.355, Radius = 0.030 };
        btns["Y"]         = new() { X = 0.568, Y = 0.260, Radius = 0.030 };
        btns["A"]         = new() { X = 0.568, Y = 0.450, Radius = 0.030 };
        btns["X"]         = new() { X = 0.503, Y = 0.355, Radius = 0.030 };
        btns["B"]         = new() { X = 0.633, Y = 0.355, Radius = 0.030 };
        btns["LeftShoulder"]  = new() { X = 0.085, Y = 0.060, Radius = 0.025 };
        btns["RightShoulder"] = new() { X = 0.500, Y = 0.060, Radius = 0.025 };
        btns["Start"]     = new() { X = 0.470, Y = 0.330, Radius = 0.020 };
        btns["Back"]      = new() { X = 0.380, Y = 0.330, Radius = 0.020 };
        return layout;
    }

    public static void Save(GamepadLayout layout)
    {
        var dir = Path.GetDirectoryName(LayoutPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(layout, options);
        File.WriteAllText(LayoutPath, json);
        _cached = layout;
    }

    public static void InvalidateCache() => _cached = null;

    public static (double PixelX, double PixelY, double PixelRadius) ToPixels(
        GamepadButtonLayout btn, double canvasWidth, double canvasHeight)
    {
        return (btn.X * canvasWidth, btn.Y * canvasHeight, btn.Radius * Math.Min(canvasWidth, canvasHeight));
    }
}
