using Windows.Devices.Enumeration;
using Windows.Devices.Power;
using Windows.Devices.HumanInterfaceDevice;
using EMECore.Core.Models;

namespace EMECore.WinUI.Services;

public class PeripheralBatteryService
{
    public async Task<List<PeripheralBatteryInfo>> GetBatteriesAsync()
    {
        var result = new List<PeripheralBatteryInfo>();

        try
        {
            var selector = Battery.GetDeviceSelector();
            var devices = await DeviceInformation.FindAllAsync(selector);
            foreach (var device in devices)
            {
                try
                {
                    var name = device.Name ?? "";
                    if (string.IsNullOrEmpty(name) || name.Contains("ACPI", StringComparison.OrdinalIgnoreCase)) continue;

                    var battery = await Battery.FromIdAsync(device.Id);
                    if (battery == null) continue;

                    var report = battery.GetReport();
                    int pct = -1;
                    if (report.RemainingCapacityInMilliwattHours.HasValue &&
                        report.FullChargeCapacityInMilliwattHours.HasValue &&
                        report.FullChargeCapacityInMilliwattHours.Value > 0)
                    {
                        pct = (int)Math.Round(
                            report.RemainingCapacityInMilliwattHours.Value * 100.0 /
                            report.FullChargeCapacityInMilliwattHours.Value);
                    }

                    var status = ((int)report.Status) switch
                    {
                        0 => "Nao presente", 1 => "Descarregando",
                        2 => "Em espera", 3 => "Carregando",
                        _ => "Desconhecido"
                    };

                    result.Add(new PeripheralBatteryInfo { DeviceName = name, BatteryPercent = pct, Status = status });
                }
                catch { }
            }
        }
        catch { }

        try
        {
            var hidBats = await GetBatteriesViaHid();
            foreach (var h in hidBats)
                if (!result.Any(r => r.DeviceName == h.DeviceName))
                    result.Add(h);
        }
        catch { }

        return result;
    }

    private static async Task<List<PeripheralBatteryInfo>> GetBatteriesViaHid()
    {
        var result = new List<PeripheralBatteryInfo>();
        try
        {
            var selector = HidDevice.GetDeviceSelector(0x0001, 0x0002);
            var devices = await DeviceInformation.FindAllAsync(selector);

            foreach (var device in devices)
            {
                try
                {
                    var props = device.Properties;
                    object? hwId = null;
                    props.TryGetValue("System.Devices.HardwareIds", out hwId);
                    if (!(hwId?.ToString() ?? "").Contains("046D", StringComparison.OrdinalIgnoreCase)) continue;

                    var name = device.Name ?? "";
                    if (name.Contains("Keyboard", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Consumer Control", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("System Control", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("definido", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var hidDevice = await HidDevice.FromIdAsync(device.Id, Windows.Storage.FileAccessMode.Read);
                    if (hidDevice == null) continue;

                    try
                    {
                        using var cts = new CancellationTokenSource(200);
                        var inputTask = hidDevice.GetInputReportAsync().AsTask(cts.Token);
                        var completed = await Task.WhenAny(inputTask, Task.Delay(250));
                        if (completed == inputTask && !inputTask.IsCanceled)
                        {
                            var report = inputTask.Result;
                            if (report?.Data != null && report.Data.Length > 1)
                            {
                                using var reader = Windows.Storage.Streams.DataReader.FromBuffer(report.Data);
                                var data = new byte[report.Data.Length];
                                reader.ReadBytes(data);

                                int pct = ParseLogitechBattery(data);
                                if (pct >= 0)
                                {
                                    result.Add(new PeripheralBatteryInfo
                                    {
                                        DeviceName = device.Name,
                                        BatteryPercent = pct,
                                        Status = "Conectado"
                                    });
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    private static int ParseLogitechBattery(byte[] data)
    {
        if (data.Length < 2) return -1;

        if (data[0] == 0x07 && data.Length >= 2)
        {
            var raw = data[1];
            if (raw <= 100) return raw;
            if (raw <= 7) return (int)(raw / 7.0 * 100);
            return data[1] & 0x7F;
        }
        if ((data[0] == 0x10 || data[0] == 0x11) && data.Length >= 3)
        {
            var raw = data[2];
            if (raw <= 100) return raw;
        }
        if (data[0] == 0x2A && data.Length >= 2)
        {
            var raw = data[1];
            if (raw <= 100) return raw;
        }
        return -1;
    }
}
