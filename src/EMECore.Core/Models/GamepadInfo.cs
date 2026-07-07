namespace EMECore.Core.Models;

public class GamepadInfo
{
    public int PlayerIndex { get; set; }
    public bool IsConnected { get; set; }
    public string Name { get; set; } = "";
    public GamepadBatteryType BatteryType { get; set; }
    public GamepadBatteryLevel BatteryLevel { get; set; }
    public bool HasBattery => IsConnected && (BatteryType != GamepadBatteryType.Wired || BatteryLevel > GamepadBatteryLevel.Empty);

    public ushort Buttons { get; set; }
    public byte LeftTrigger { get; set; }
    public byte RightTrigger { get; set; }
    public short ThumbLX { get; set; }
    public short ThumbLY { get; set; }
    public short ThumbRX { get; set; }
    public short ThumbRY { get; set; }
    public double PollingRate { get; set; }
    public uint PacketNumber { get; set; }

    public bool IsPressed(GamepadButton btn) => (Buttons & (ushort)btn) != 0;
}

[Flags]
public enum GamepadButton : ushort
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000
}

public enum GamepadBatteryType
{
    Disconnected = 0,
    Wired = 1,
    Alkaline = 2,
    NiMH = 3,
    Unknown = 4
}

public enum GamepadBatteryLevel
{
    Empty = 0,
    Low = 1,
    Medium = 2,
    Full = 3
}
