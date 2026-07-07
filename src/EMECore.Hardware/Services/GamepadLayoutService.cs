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
        AppContext.BaseDirectory, "config", "gamepad-layout.json");

    private static GamepadLayout? _cached;

    public static GamepadLayout Load()
    {
        if (_cached != null) return _cached;

        try
        {
            if (File.Exists(LayoutPath))
            {
                var json = File.ReadAllText(LayoutPath);
                _cached = JsonSerializer.Deserialize<GamepadLayout>(json) ?? new GamepadLayout();
                return _cached;
            }
        }
        catch { }

        _cached = new GamepadLayout();
        return _cached;
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
