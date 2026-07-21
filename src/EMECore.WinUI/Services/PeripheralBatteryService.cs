using System.Runtime.InteropServices;
using EMECore.Core.Models;

namespace EMECore.WinUI.Services;

internal sealed class LegacyPeripheralBatteryService
{
    // Win32 HID imports (mínimo necessário)
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    // HID++ 2.0 constants
    private const byte REPORT_ID_SHORT = 0x10;
    private const byte ROOT_FEATURE_IDX = 0x00;
    private const byte SW_ID = 0x08;
    private const ushort FEATURE_BATTERY_LEVEL_STATUS = 0x1000;
    private const int SHORT_MSG_LEN = 7;

    public async Task<List<PeripheralBatteryInfo>> GetBatteriesAsync()
    {
        var result = new List<PeripheralBatteryInfo>();

        // Layer 1: Windows Battery API
        try
        {
            var selector = Windows.Devices.Power.Battery.GetDeviceSelector();
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(selector);
            foreach (var device in devices)
            {
                try
                {
                    var name = device.Name ?? "";
                    if (string.IsNullOrEmpty(name) || name.Contains("ACPI", StringComparison.OrdinalIgnoreCase)) continue;
                    var battery = await Windows.Devices.Power.Battery.FromIdAsync(device.Id);
                    if (battery == null) continue;
                    var report = battery.GetReport();
                    int pct = -1;
                    if (report.RemainingCapacityInMilliwattHours.HasValue &&
                        report.FullChargeCapacityInMilliwattHours.HasValue && report.FullChargeCapacityInMilliwattHours.Value > 0)
                        pct = (int)Math.Round(report.RemainingCapacityInMilliwattHours.Value * 100.0 / report.FullChargeCapacityInMilliwattHours.Value);
                    result.Add(new PeripheralBatteryInfo { DeviceName = name, BatteryPercent = pct, Status = "Conectado" });
                }
                catch { }
            }
        }
        catch { }

        // Layer 2: Logitech HID++ 2.0 via Win32
        try
        {
            var logPath = Path.Combine(Path.GetTempPath(), "eme_battery.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] HID++ scan start\n");

            var selector = Windows.Devices.HumanInterfaceDevice.HidDevice.GetDeviceSelector(0xFF00, 0x0001);
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(selector);
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] HID++ devices found: {devices.Count}\n");

            foreach (var device in devices)
            {
                try
                {
                    if (!device.Id.Contains("VID_046D", StringComparison.OrdinalIgnoreCase)) continue;
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Logitech: {device.Name}\n");

                    var devicePath = device.Id;
                    var handle = CreateFile(devicePath, GENERIC_READ | GENERIC_WRITE,
                        FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

                    if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                    {
                        File.AppendAllText(logPath, $"  -> CreateFile FAILED (err: {Marshal.GetLastWin32Error()})\n");
                        continue;
                    }
                    File.AppendAllText(logPath, $"  -> CreateFile OK\n");

                    try
                    {
                        for (byte devIdx = 1; devIdx <= 2; devIdx++)
                        {
                            int pct = QueryBattery(handle, devIdx);
                            if (pct >= 0)
                            {
                                File.AppendAllText(logPath, $"  -> BATTERY devIdx={devIdx}: {pct}%\n");
                                var existing = result.FirstOrDefault(r => r.DeviceName == device.Name);
                                if (existing != null) existing.BatteryPercent = pct;
                                else result.Add(new PeripheralBatteryInfo { DeviceName = device.Name, BatteryPercent = pct, Status = "Conectado" });
                                break;
                            }
                        }
                    }
                    finally { CloseHandle(handle); }
                }
                catch (Exception ex)
                {
                    var logPath2 = Path.Combine(Path.GetTempPath(), "eme_battery.log");
                    File.AppendAllText(logPath2, $"  -> EXCEPTION: {ex.Message}\n");
                }
            }
        }
        catch (Exception ex)
        {
            var logPath3 = Path.Combine(Path.GetTempPath(), "eme_battery.log");
            File.AppendAllText(logPath3, $"[{DateTime.Now:HH:mm:ss}] HID++ EXCEPTION: {ex.Message}\n");
        }

        return result;
    }

    private static int QueryBattery(IntPtr handle, byte devIdx)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "eme_battery.log");

        try
        {
            // Step 1: Get feature index for Battery Level Status (0x1000)
            byte[] msg = new byte[SHORT_MSG_LEN];
            msg[0] = REPORT_ID_SHORT;
            msg[1] = devIdx;
            msg[2] = ROOT_FEATURE_IDX;
            msg[3] = (byte)(0x00 | SW_ID);
            msg[4] = (byte)(FEATURE_BATTERY_LEVEL_STATUS >> 8);
            msg[5] = (byte)(FEATURE_BATTERY_LEVEL_STATUS & 0xFF);
            msg[6] = 0x00;

            File.AppendAllText(logPath, $"[QueryBattery] devIdx={devIdx} sending GetFeature(0x1000)\n");

            uint written;
            if (!WriteFile(handle, msg, SHORT_MSG_LEN, out written, IntPtr.Zero))
            {
                int err = Marshal.GetLastWin32Error();
                File.AppendAllText(logPath, $"[QueryBattery] WriteFile FAILED (err={err})\n");
                return -1;
            }
            File.AppendAllText(logPath, $"[QueryBattery] WriteFile OK (written={written})\n");

            // Read response
            byte[] resp = new byte[SHORT_MSG_LEN];
            for (int i = 0; i < 15; i++)
            {
                Thread.Sleep(30);
                uint read;
                bool ok = ReadFile(handle, resp, (uint)resp.Length, out read, IntPtr.Zero);
                if (!ok)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (i == 0 || i == 14)
                        File.AppendAllText(logPath, $"[QueryBattery] ReadFile i={i} FAILED (err={err})\n");
                    continue;
                }
                if (read > 0)
                {
                    File.AppendAllText(logPath, $"[QueryBattery] ReadFile i={i} read={read} bytes=[{string.Join(",", resp.Select(b => $"0x{b:X2}"))}]\n");

                    if (read >= SHORT_MSG_LEN && resp[1] == devIdx && resp[2] == ROOT_FEATURE_IDX && (resp[3] & 0xF0) == 0x00)
                    {
                        byte featureIdx = resp[4];
                        File.AppendAllText(logPath, $"[QueryBattery] featureIdx={featureIdx}\n");
                        if (featureIdx == 0) return -1;

                        // Step 2: Query battery level
                        byte[] batMsg = new byte[SHORT_MSG_LEN];
                        batMsg[0] = REPORT_ID_SHORT;
                        batMsg[1] = devIdx;
                        batMsg[2] = featureIdx;
                        batMsg[3] = (byte)(0x00 | SW_ID);
                        batMsg[4] = 0x00; batMsg[5] = 0x00; batMsg[6] = 0x00;

                        if (!WriteFile(handle, batMsg, SHORT_MSG_LEN, out written, IntPtr.Zero))
                        {
                            File.AppendAllText(logPath, $"[QueryBattery] Step2 WriteFile FAILED (err={Marshal.GetLastWin32Error()})\n");
                            return -1;
                        }

                        byte[] batResp = new byte[SHORT_MSG_LEN];
                        for (int j = 0; j < 15; j++)
                        {
                            Thread.Sleep(30);
                            uint read2;
                            if (ReadFile(handle, batResp, (uint)batResp.Length, out read2, IntPtr.Zero) && read2 >= SHORT_MSG_LEN)
                            {
                                File.AppendAllText(logPath, $"[QueryBattery] Step2 j={j} bytes=[{string.Join(",", batResp.Select(b => $"0x{b:X2}"))}]\n");
                                if (batResp[1] == devIdx && batResp[2] == featureIdx && (batResp[3] & 0xF0) == 0x00)
                                {
                                    byte level = batResp[4];
                                    File.AppendAllText(logPath, $"[QueryBattery] BATTERY LEVEL: {level}%\n");
                                    if (level <= 100) return level;
                                    break;
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"[QueryBattery] EXCEPTION: {ex.Message}\n");
        }
        return -1;
    }
}
