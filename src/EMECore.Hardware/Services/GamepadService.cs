using System.Diagnostics;
using System.Runtime.InteropServices;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class GamepadService : IDisposable
{
    [DllImport("xinput1_4.dll")]
    private static extern int XInputGetState(int dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput1_4.dll")]
    private static extern int XInputGetBatteryInformation(int dwUserIndex, byte devType, out XINPUT_BATTERY_INFORMATION pBatteryInformation);

    [DllImport("xinput1_4.dll")]
    private static extern int XInputGetCapabilities(int dwUserIndex, int dwFlags, out XINPUT_CAPABILITIES pCapabilities);

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_BATTERY_INFORMATION
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_CAPABILITIES
    {
        public byte Type;
        public byte SubType;
        public ushort Flags;
        public XINPUT_GAMEPAD Gamepad;
        public XINPUT_VIBRATION Vibration;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    private const int XINPUT_FLAG_GAMEPAD = 0x01;
    private const int ERROR_SUCCESS = 0;

    private static readonly string[] SubTypeNames =
    {
        "Gamepad", "Wheel", "Arcade Stick", "Flight Stick",
        "Dance Pad", "Guitar", "Guitar Alternate", "Drum Kit",
        "Guitar Bass", "Unknown", "Unknown", "Unknown",
        "Unknown", "Unknown", "Unknown", "Unknown",
        "Unknown", "Unknown", "Unknown", "Arcade Pad"
    };

    private readonly uint[] _lastPacketNumbers = new uint[4];
    private readonly int[] _packetChanges = new int[4];
    private readonly Stopwatch[] _pollingTimers = new Stopwatch[4];
    private readonly double[] _pollingRates = new double[4];

    public GamepadService()
    {
        for (int i = 0; i < 4; i++)
            _pollingTimers[i] = new Stopwatch();
    }

    public List<GamepadInfo> Collect()
    {
        var gamepads = new List<GamepadInfo>();

        for (int i = 0; i < 4; i++)
        {
            var info = new GamepadInfo { PlayerIndex = i };

            int stateResult = XInputGetState(i, out var state);
            info.IsConnected = stateResult == ERROR_SUCCESS;

            if (!info.IsConnected)
            {
                info.Name = $"Player {i + 1} (Desconectado)";
                _pollingRates[i] = 0;
                _packetChanges[i] = 0;
                _pollingTimers[i].Reset();
                gamepads.Add(info);
                continue;
            }

            int capsResult = XInputGetCapabilities(i, XINPUT_FLAG_GAMEPAD, out var caps);
            if (capsResult == ERROR_SUCCESS)
            {
                string subType = caps.SubType < SubTypeNames.Length ? SubTypeNames[caps.SubType] : "Unknown";
                info.Name = $"Player {i + 1} - {subType}";
            }
            else
            {
                info.Name = $"Player {i + 1}";
            }

            int batResult = XInputGetBatteryInformation(i, 0, out var battery);
            if (batResult == ERROR_SUCCESS)
            {
                info.BatteryType = (GamepadBatteryType)battery.BatteryType;
                info.BatteryLevel = (GamepadBatteryLevel)battery.BatteryLevel;
            }

            info.Buttons = state.Gamepad.wButtons;
            info.LeftTrigger = state.Gamepad.bLeftTrigger;
            info.RightTrigger = state.Gamepad.bRightTrigger;
            info.ThumbLX = state.Gamepad.sThumbLX;
            info.ThumbLY = state.Gamepad.sThumbLY;
            info.ThumbRX = state.Gamepad.sThumbRX;
            info.ThumbRY = state.Gamepad.sThumbRY;
            info.PacketNumber = state.dwPacketNumber;
            info.PollingRate = _pollingRates[i];

            gamepads.Add(info);
        }

        return gamepads;
    }

    public GamepadInfo GetState(int playerIndex)
    {
        var info = new GamepadInfo { PlayerIndex = playerIndex };

        int stateResult = XInputGetState(playerIndex, out var state);
        info.IsConnected = stateResult == ERROR_SUCCESS;

        if (!info.IsConnected)
        {
            info.Name = $"Player {playerIndex + 1} (Desconectado)";
            return info;
        }

        int capsResult = XInputGetCapabilities(playerIndex, XINPUT_FLAG_GAMEPAD, out var caps);
        if (capsResult == ERROR_SUCCESS)
        {
            string subType = caps.SubType < SubTypeNames.Length ? SubTypeNames[caps.SubType] : "Unknown";
            info.Name = $"Player {playerIndex + 1} - {subType}";
        }

        int batResult = XInputGetBatteryInformation(playerIndex, 0, out var battery);
        if (batResult == ERROR_SUCCESS)
        {
            info.BatteryType = (GamepadBatteryType)battery.BatteryType;
            info.BatteryLevel = (GamepadBatteryLevel)battery.BatteryLevel;
        }

        info.Buttons = state.Gamepad.wButtons;
        info.LeftTrigger = state.Gamepad.bLeftTrigger;
        info.RightTrigger = state.Gamepad.bRightTrigger;
        info.ThumbLX = state.Gamepad.sThumbLX;
        info.ThumbLY = state.Gamepad.sThumbLY;
        info.ThumbRX = state.Gamepad.sThumbRX;
        info.ThumbRY = state.Gamepad.sThumbRY;
        info.PacketNumber = state.dwPacketNumber;

        if (state.dwPacketNumber != _lastPacketNumbers[playerIndex])
        {
            if (!_pollingTimers[playerIndex].IsRunning)
            {
                _pollingTimers[playerIndex].Restart();
                _packetChanges[playerIndex] = 0;
            }
            _packetChanges[playerIndex]++;
            _lastPacketNumbers[playerIndex] = state.dwPacketNumber;

            if (_pollingTimers[playerIndex].ElapsedMilliseconds >= 1000)
            {
                _pollingRates[playerIndex] = _packetChanges[playerIndex] * 1000.0 / _pollingTimers[playerIndex].ElapsedMilliseconds;
                _pollingTimers[playerIndex].Restart();
                _packetChanges[playerIndex] = 0;
            }
        }

        info.PollingRate = _pollingRates[playerIndex];

        return info;
    }

    // Cached state from PollState for UI thread
    private GamepadInfo? _cachedState0;

    public void PollState(int playerIndex)
    {
        int result = XInputGetState(playerIndex, out var state);
        if (result != ERROR_SUCCESS)
        {
            _cachedState0 = null;
            return;
        }

        // Polling rate calculation
        if (state.dwPacketNumber != _lastPacketNumbers[playerIndex])
        {
            if (!_pollingTimers[playerIndex].IsRunning)
            {
                _pollingTimers[playerIndex].Restart();
                _packetChanges[playerIndex] = 0;
                _lastPacketNumbers[playerIndex] = state.dwPacketNumber;
            }
            else
            {
                uint diff = state.dwPacketNumber - _lastPacketNumbers[playerIndex];
                _packetChanges[playerIndex] += (int)Math.Min(diff, 1000);
                _lastPacketNumbers[playerIndex] = state.dwPacketNumber;
            }

            if (_pollingTimers[playerIndex].ElapsedMilliseconds >= 1000)
            {
                _pollingRates[playerIndex] = _packetChanges[playerIndex] * 1000.0 / _pollingTimers[playerIndex].ElapsedMilliseconds;
                _pollingTimers[playerIndex].Restart();
                _packetChanges[playerIndex] = 0;
            }
        }

        // Cache full state for UI
        var info = new GamepadInfo { PlayerIndex = playerIndex, IsConnected = true };
        int capsResult = XInputGetCapabilities(playerIndex, XINPUT_FLAG_GAMEPAD, out var caps);
        if (capsResult == ERROR_SUCCESS)
        {
            string subType = caps.SubType < SubTypeNames.Length ? SubTypeNames[caps.SubType] : "Unknown";
            info.Name = $"Player {playerIndex + 1} - {subType}";
        }
        info.Buttons = state.Gamepad.wButtons;
        info.LeftTrigger = state.Gamepad.bLeftTrigger;
        info.RightTrigger = state.Gamepad.bRightTrigger;
        info.ThumbLX = state.Gamepad.sThumbLX;
        info.ThumbLY = state.Gamepad.sThumbLY;
        info.ThumbRX = state.Gamepad.sThumbRX;
        info.ThumbRY = state.Gamepad.sThumbRY;
        info.PacketNumber = state.dwPacketNumber;
        info.PollingRate = _pollingRates[playerIndex];
        _cachedState0 = info;
    }

    public GamepadInfo? GetCachedState(int playerIndex) => _cachedState0;

    public void Dispose()
    {
        for (int i = 0; i < 4; i++)
            _pollingTimers[i].Stop();
        GC.SuppressFinalize(this);
    }
}
