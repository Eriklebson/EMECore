using System.Runtime.InteropServices;
using System.Text;
using EMECore.Core.Models;

namespace EMECore.WinUI.Services;

public sealed class PeripheralBatteryService
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;
    private const int ErrorIoPending = 997;
    private const uint WaitObject0 = 0;
    private const uint IoTimeoutMilliseconds = 250;
    private const byte ShortReportId = 0x10;
    private const byte LongReportId = 0x11;
    private const byte SoftwareId = 0x08;
    private const ushort DeviceNameFeature = 0x0005;
    private const ushort BatteryStatusFeature = 0x1000;
    private const ushort UnifiedBatteryFeature = 0x1004;
    private const ushort ReportRateFeature = 0x8060;
    private const ushort ReportRateFeatureV2 = 0x8061;
    private const int HidppMessageLength = 7;
    private const int MaxLogBytes = 512 * 1024;
    private const int ProtocolDiagnosticLimit = 120;
    private static int _protocolDiagnosticCount;

    private static readonly IntPtr InvalidHandleValue = new(-1);
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private readonly object _cacheLock = new();
    private List<PeripheralBatteryInfo> _cached = new();

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CancelIoEx(IntPtr hFile, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetOverlappedResult(IntPtr hFile, IntPtr lpOverlapped,
        out uint lpNumberOfBytesTransferred, bool bWait);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset,
        bool bInitialState, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(IntPtr hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;

        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    public async Task<List<PeripheralBatteryInfo>> GetBatteriesAsync()
    {
        if (!await _scanLock.WaitAsync(0)) return GetCachedCopy();

        try
        {
            var result = new List<PeripheralBatteryInfo>();
            WriteLog("Início da varredura de baterias.");

            var logitechResult = new List<PeripheralBatteryInfo>();
            try
            {
                await CollectLogitechHidppAsync(logitechResult).WaitAsync(TimeSpan.FromSeconds(8));
                result.AddRange(logitechResult);
            }
            catch (TimeoutException)
            {
                WriteLog("Tempo limite atingido no provedor Logitech HID++.");
            }

            var windowsResult = new List<PeripheralBatteryInfo>();
            try
            {
                await CollectWindowsBatteriesAsync(windowsResult).WaitAsync(TimeSpan.FromSeconds(3));
                result.AddRange(windowsResult);
            }
            catch (TimeoutException)
            {
                WriteLog("Tempo limite atingido no provedor Windows Battery.");
            }

            var normalized = result
                .GroupBy(item => string.IsNullOrWhiteSpace(item.DeviceId) ? item.DeviceName : item.DeviceId,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.BatteryPercent).First())
                .OrderBy(item => item.DeviceName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            lock (_cacheLock)
            {
                foreach (var item in normalized.Where(item => item.PollingRateHz <= 0))
                {
                    var cachedMatch = _cached.FirstOrDefault(cachedItem =>
                        cachedItem.DeviceId.Equals(item.DeviceId, StringComparison.OrdinalIgnoreCase));
                    if (cachedMatch?.PollingRateHz > 0)
                        item.PollingRateHz = cachedMatch.PollingRateHz;
                }

                foreach (var cachedItem in _cached.Where(cachedItem => cachedItem.BatteryPercent >= 0))
                {
                    if (normalized.All(item => !item.DeviceId.Equals(cachedItem.DeviceId, StringComparison.OrdinalIgnoreCase)))
                        normalized.Add(CloneItem(cachedItem));
                }
            }

            lock (_cacheLock) _cached = normalized;
            return Clone(normalized);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private static async Task CollectWindowsBatteriesAsync(List<PeripheralBatteryInfo> result)
    {
        try
        {
            var selector = Windows.Devices.Power.Battery.GetDeviceSelector();
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(selector);
            foreach (var device in devices)
            {
                try
                {
                    var name = device.Name ?? "";
                    if (string.IsNullOrWhiteSpace(name) || name.Contains("ACPI", StringComparison.OrdinalIgnoreCase)) continue;

                    var battery = await Windows.Devices.Power.Battery.FromIdAsync(device.Id);
                    if (battery == null) continue;
                    var report = battery.GetReport();
                    var percent = -1;
                    if (report.RemainingCapacityInMilliwattHours is int remaining &&
                        report.FullChargeCapacityInMilliwattHours is int full && full > 0)
                        percent = Math.Clamp((int)Math.Round(remaining * 100.0 / full), 0, 100);

                    result.Add(new PeripheralBatteryInfo
                    {
                        DeviceId = device.Id,
                        DeviceName = name,
                        BatteryPercent = percent,
                        Status = MapWindowsStatus(report.Status)
                    });
                }
                catch (Exception ex)
                {
                    WriteLog($"Windows Battery ignorou '{device.Name}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            WriteLog($"Falha ao enumerar Windows Battery: {ex.Message}");
        }
    }

    private static async Task CollectLogitechHidppAsync(List<PeripheralBatteryInfo> result)
    {
        try
        {
            var definitions = PeripheralDeviceCatalog.All
                .Where(definition => definition.Enabled &&
                    definition.Provider.StartsWith("logitech-", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var devices = new List<(Windows.Devices.Enumeration.DeviceInformation Device,
                PeripheralDeviceDefinition Definition)>();
            foreach (var group in definitions.GroupBy(definition => (definition.UsagePage, definition.UsageId)))
            {
                var selector = Windows.Devices.HumanInterfaceDevice.HidDevice.GetDeviceSelector(
                    group.Key.UsagePage, group.Key.UsageId);
                var matches = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(selector);
                foreach (var device in matches)
                {
                    var definition = group.FirstOrDefault(candidate => candidate.Matches(device.Id));
                    if (definition != null) devices.Add((device, definition));
                }
            }
            WriteLog($"Varredura HID++: {devices.Count} interface(s) proprietária(s).");

            foreach (var device in devices)
            {
                await Task.Run(() => CollectLogitechDevice(
                    device.Device.Id, device.Device.Name, device.Definition, result));
            }
        }
        catch (Exception ex)
        {
            WriteLog($"Falha na varredura Logitech HID++: {ex.Message}");
        }
    }

    private static void CollectLogitechDevice(string devicePath, string enumeratedName,
        PeripheralDeviceDefinition definition, List<PeripheralBatteryInfo> result)
    {
        var handle = CreateFile(devicePath, GenericRead | GenericWrite, FileShareRead | FileShareWrite,
            IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
        if (handle == IntPtr.Zero || handle == InvalidHandleValue)
        {
            WriteLog($"Não foi possível abrir '{enumeratedName}' (erro {Marshal.GetLastWin32Error()}).");
            return;
        }

        try
        {
            var (inputLength, outputLength) = GetReportLengths(handle);
            WriteLog($"Interface '{enumeratedName}': in={inputLength}, out={outputLength}.");
            if (inputLength < HidppMessageLength || outputLength < HidppMessageLength) return;

            if (definition.Provider.Equals("logitech-voltage", StringComparison.OrdinalIgnoreCase))
            {
                var battery = QueryDirectVoltageBattery(handle, inputLength, outputLength, definition);
                result.Add(new PeripheralBatteryInfo
                {
                    DeviceId = $"catalog:{ExtractVidPid(devicePath)}:{definition.DeviceIndex:X2}",
                    DeviceName = definition.Name,
                    BatteryPercent = battery.Percent,
                    Status = battery.Status
                });
                WriteLog(battery.Percent >= 0
                    ? $"Detectado Logitech PRO X Wireless Gaming Headset: {battery.Percent}% ({battery.Status}), {battery.Voltage} mV."
                    : "Logitech PRO X Wireless Gaming Headset detectado, mas sem leitura válida de tensão.");
                return;
            }

            var added = false;
            foreach (var deviceIndex in new[] { definition.DeviceIndex })
            {
                var battery = QueryBattery(handle, inputLength, outputLength, deviceIndex);
                if (battery.Percent < 0) continue;

                var resolvedName = definition.Name;
                var pollingRateHz = definition.FixedPollingRateHz > 0
                    ? definition.FixedPollingRateHz
                    : definition.SupportsPollingRate
                        ? QueryPollingRate(handle, inputLength, outputLength, deviceIndex)
                        : 0;
                result.Add(new PeripheralBatteryInfo
                {
                    DeviceId = $"logitech:{ExtractVidPid(devicePath)}:{deviceIndex:X2}",
                    DeviceName = resolvedName,
                    BatteryPercent = battery.Percent,
                    Status = battery.Status,
                    PollingRateHz = pollingRateHz,
                    SupportsPollingRate = definition.SupportsPollingRate
                });
                added = true;
                WriteLog($"Detectado {resolvedName}: {battery.Percent}% ({battery.Status}), índice 0x{deviceIndex:X2}.");
            }

            if (!added &&
                !devicePath.Contains("PID_C54D", StringComparison.OrdinalIgnoreCase) &&
                !devicePath.Contains("PID_C548", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new PeripheralBatteryInfo
                {
                    DeviceId = $"logitech:{ExtractVidPid(devicePath)}:interface",
                    DeviceName = ResolveDeviceName("", enumeratedName, devicePath),
                    BatteryPercent = -1,
                    Status = "Bateria indisponível"
                });
            }
        }
        catch (Exception ex)
        {
            WriteLog($"Falha ao consultar '{enumeratedName}': {ex.Message}");
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static (int Input, int Output) GetReportLengths(IntPtr handle)
    {
        if (!HidD_GetPreparsedData(handle, out var preparsedData)) return (HidppMessageLength, HidppMessageLength);
        try
        {
            return HidP_GetCaps(preparsedData, out var caps) >= 0
                ? (Math.Max(caps.InputReportByteLength, (ushort)HidppMessageLength),
                    Math.Max(caps.OutputReportByteLength, (ushort)HidppMessageLength))
                : (HidppMessageLength, HidppMessageLength);
        }
        finally
        {
            HidD_FreePreparsedData(preparsedData);
        }
    }

    private static IReadOnlyList<byte> GetDeviceIndices(string devicePath)
    {
        if (devicePath.Contains("PID_C54D", StringComparison.OrdinalIgnoreCase))
            return new byte[] { 1 };
        if (devicePath.Contains("PID_C548", StringComparison.OrdinalIgnoreCase))
            return new byte[] { 2 };
        if (devicePath.Contains("PID_0ABA", StringComparison.OrdinalIgnoreCase))
            return new byte[] { 0xFF };
        return new byte[] { 1, 0xFF, 2 };
    }

    private static (int Percent, string Status) QueryBattery(IntPtr handle, int inputLength,
        int outputLength, byte deviceIndex)
    {
        var unifiedIndex = QueryFeatureIndex(handle, inputLength, outputLength, deviceIndex, UnifiedBatteryFeature);
        if (unifiedIndex > 0)
        {
            var response = Exchange(handle, inputLength, outputLength, deviceIndex, unifiedIndex, 0x10);
            if (response != null)
            {
                var percent = response[4] <= 100 ? response[4] : -1;
                if (percent == 0) percent = ApproximateUnifiedLevel(response[5]);
                return (percent, MapHidppStatus(response[6]));
            }
        }

        if (deviceIndex is 1 or 2)
            return (-1, "Desconhecido");

        var statusIndex = QueryFeatureIndex(handle, inputLength, outputLength, deviceIndex, BatteryStatusFeature);
        if (statusIndex > 0)
        {
            var response = Exchange(handle, inputLength, outputLength, deviceIndex, statusIndex, 0x00);
            if (response != null && response[4] <= 100)
                return (response[4], MapHidppStatus(response[6]));
        }

        return (-1, "Desconhecido");
    }

    private static (int Percent, string Status, int Voltage) QueryDirectVoltageBattery(
        IntPtr handle, int inputLength, int outputLength, PeripheralDeviceDefinition definition)
    {
        byte[] command;
        try { command = Convert.FromHexString(definition.BatteryCommand); }
        catch { return (-1, "Bateria indisponível", 0); }
        if (command.Length < 2) return (-1, "Bateria indisponível", 0);

        var request = new byte[Math.Max(outputLength, 20)];
        request[0] = LongReportId;
        request[1] = definition.DeviceIndex;
        request[2] = command[0];
        request[3] = command[1];

        LogProtocol($"TX PRO X {FormatPacket(request)}");
        if (!WriteWithTimeout(handle, request))
        {
            LogProtocol($"Falha/timeout na escrita PRO X (erro {Marshal.GetLastWin32Error()}).");
            return (-1, "Bateria indisponível", 0);
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = new byte[Math.Max(inputLength, HidppMessageLength)];
            if (!ReadWithTimeout(handle, response, out var read)) continue;
            LogProtocol($"RX PRO X[{read}] {FormatPacket(response, (int)read)}");
            if (read < HidppMessageLength || response[1] != definition.DeviceIndex ||
                response[2] != command[0] || response[3] != command[1]) continue;

            var voltage = (response[4] << 8) | response[5];
            if (voltage < definition.MinimumVoltage || voltage > definition.MaximumVoltage)
                return (-1, "Bateria indisponível", voltage);

            return (EstimateCatalogBattery(definition, voltage),
                response[6] == 0x03 ? "Carregando" : "Descarregando", voltage);
        }

        return (-1, "Bateria indisponível", 0);
    }

    private static int EstimateCatalogBattery(PeripheralDeviceDefinition definition, int voltage)
    {
        if (definition.BatteryPolynomial.Length > 0)
        {
            var factor = 1.0;
            var percent = 0.0;
            foreach (var coefficient in definition.BatteryPolynomial)
            {
                percent += coefficient * factor;
                factor *= voltage;
            }
            return Math.Clamp((int)Math.Round(percent), 0, 100);
        }

        var percentages = definition.BatteryPercentages;
        var voltages = definition.BatteryVoltages;
        if (percentages.Length != voltages.Length || voltages.Length < 2) return -1;
        if (voltage >= voltages[0]) return percentages[0];
        if (voltage <= voltages[^1]) return percentages[^1];
        for (var index = 0; index < voltages.Length - 1; index++)
        {
            if (voltage > voltages[index] || voltage < voltages[index + 1]) continue;
            var ratio = (voltage - voltages[index + 1]) /
                        (double)(voltages[index] - voltages[index + 1]);
            return Math.Clamp((int)Math.Round(percentages[index + 1] +
                ratio * (percentages[index] - percentages[index + 1])), 0, 100);
        }
        return -1;
    }

    private static int QueryPollingRate(IntPtr handle, int inputLength, int outputLength, byte deviceIndex)
    {
        var basicFeatureIndex = QueryFeatureIndex(handle, inputLength, outputLength, deviceIndex, ReportRateFeature);
        if (basicFeatureIndex > 0)
        {
            var response = Exchange(handle, inputLength, outputLength, deviceIndex, basicFeatureIndex, 0x10);
            var intervalMilliseconds = response?[4] ?? 0;
            if (intervalMilliseconds is >= 1 and <= 8 && 1000 % intervalMilliseconds == 0)
                return 1000 / intervalMilliseconds;
        }

        var extendedFeatureIndex = QueryFeatureIndex(handle, inputLength, outputLength, deviceIndex, ReportRateFeatureV2);
        if (extendedFeatureIndex > 0)
        {
            const byte gamingWirelessConnection = 1;
            var response = Exchange(handle, inputLength, outputLength, deviceIndex,
                extendedFeatureIndex, 0x20, gamingWirelessConnection, 0x00, 0x00);
            if (response != null)
            {
                WriteLog($"Polling HID++ 0x{ReportRateFeatureV2:X4}: {FormatPacket(response)}");
                return response[4] switch
                {
                    0 => 125,
                    1 => 250,
                    2 => 500,
                    3 => 1000,
                    4 => 2000,
                    5 => 4000,
                    6 => 8000,
                    _ => 0
                };
            }
        }

        return 0;
    }

    private static string QueryDeviceName(IntPtr handle, int inputLength, int outputLength, byte deviceIndex)
    {
        var featureIndex = QueryFeatureIndex(handle, inputLength, outputLength, deviceIndex, DeviceNameFeature);
        if (featureIndex <= 0) return "";
        var lengthResponse = Exchange(handle, inputLength, outputLength, deviceIndex, featureIndex, 0x00);
        if (lengthResponse == null) return "";

        var expectedLength = Math.Min(lengthResponse[4], (byte)64);
        var nameBytes = new List<byte>(expectedLength);
        for (byte offset = 0; nameBytes.Count < expectedLength; offset += 3)
        {
            var fragment = Exchange(handle, inputLength, outputLength, deviceIndex, featureIndex, 0x10, offset);
            if (fragment == null) break;
            for (var index = 4; index < HidppMessageLength && nameBytes.Count < expectedLength; index++)
                if (fragment[index] != 0) nameBytes.Add(fragment[index]);
        }
        return Encoding.UTF8.GetString(nameBytes.ToArray()).Trim();
    }

    private static byte QueryFeatureIndex(IntPtr handle, int inputLength, int outputLength,
        byte deviceIndex, ushort featureId)
    {
        var response = Exchange(handle, inputLength, outputLength, deviceIndex, 0x00, 0x00,
            (byte)(featureId >> 8), (byte)featureId, 0x00);
        return response != null ? response[4] : (byte)0;
    }

    private static byte[]? Exchange(IntPtr handle, int inputLength, int outputLength, byte deviceIndex,
        byte featureIndex, byte function, params byte[] parameters)
    {
        var reportId = outputLength >= 20 ? LongReportId : ShortReportId;
        var minimumLength = reportId == LongReportId ? 20 : HidppMessageLength;
        var request = new byte[Math.Max(outputLength, minimumLength)];
        request[0] = reportId;
        request[1] = deviceIndex;
        request[2] = featureIndex;
        request[3] = (byte)(function | SoftwareId);
        Array.Copy(parameters, 0, request, 4, Math.Min(parameters.Length, request.Length - 4));

        LogProtocol($"TX {FormatPacket(request)}");
        if (!WriteWithTimeout(handle, request))
        {
            LogProtocol($"Falha/timeout na escrita (erro {Marshal.GetLastWin32Error()}).");
            return null;
        }
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = new byte[Math.Max(inputLength, minimumLength)];
            if (!ReadWithTimeout(handle, response, out var read))
            {
                LogProtocol($"RX timeout (tentativa {attempt + 1}, erro {Marshal.GetLastWin32Error()}).");
                continue;
            }
            LogProtocol($"RX[{read}] {FormatPacket(response, (int)read)}");
            if (read < HidppMessageLength) continue;
            if (response[0] != reportId || response[1] != deviceIndex) continue;
            if (response[2] == 0x8F)
            {
                LogProtocol($"HID++ recusou a consulta: recurso=0x{response[4]:X2}, erro=0x{response[5]:X2}.");
                return null;
            }
            if (response[2] == featureIndex && response[3] == request[3]) return response;
        }
        return null;
    }

    private static void LogProtocol(string message)
    {
        if (Interlocked.Increment(ref _protocolDiagnosticCount) <= ProtocolDiagnosticLimit)
            WriteLog($"Diagnóstico HID++: {message}");
    }

    private static string FormatPacket(byte[] packet, int? transferred = null)
    {
        var length = Math.Min(transferred ?? packet.Length, 20);
        return Convert.ToHexString(packet.AsSpan(0, length));
    }

    private static bool WriteWithTimeout(IntPtr handle, byte[] buffer)
    {
        return ExecuteOverlapped(handle, buffer, false, out var transferred) && transferred > 0;
    }

    private static bool ReadWithTimeout(IntPtr handle, byte[] buffer, out uint transferred)
    {
        return ExecuteOverlapped(handle, buffer, true, out transferred);
    }

    private static bool ExecuteOverlapped(IntPtr handle, byte[] buffer, bool isRead, out uint transferred)
    {
        transferred = 0;
        var eventHandle = CreateEvent(IntPtr.Zero, true, false, null);
        if (eventHandle == IntPtr.Zero) return false;

        var overlappedPointer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlapped>());
        try
        {
            var overlapped = new NativeOverlapped { EventHandle = eventHandle };
            Marshal.StructureToPtr(overlapped, overlappedPointer, false);

            var completed = isRead
                ? ReadFile(handle, buffer, (uint)buffer.Length, out transferred, overlappedPointer)
                : WriteFile(handle, buffer, (uint)buffer.Length, out transferred, overlappedPointer);

            if (!completed && Marshal.GetLastWin32Error() != ErrorIoPending) return false;
            if (completed) return true;

            if (WaitForSingleObject(eventHandle, IoTimeoutMilliseconds) != WaitObject0)
            {
                CancelIoEx(handle, overlappedPointer);
                WaitForSingleObject(eventHandle, IoTimeoutMilliseconds);
                return false;
            }

            return GetOverlappedResult(handle, overlappedPointer, out transferred, false);
        }
        finally
        {
            Marshal.FreeHGlobal(overlappedPointer);
            CloseHandle(eventHandle);
        }
    }

    private static string ResolveDeviceName(string protocolName, string enumeratedName, string devicePath)
    {
        if (!string.IsNullOrWhiteSpace(protocolName)) return protocolName;
        if (devicePath.Contains("PID_C54D", StringComparison.OrdinalIgnoreCase)) return "Logitech G PRO X SUPERLIGHT 2";
        if (devicePath.Contains("PID_0ABA", StringComparison.OrdinalIgnoreCase)) return "Logitech G PRO X Wireless Gaming Headset";
        if (devicePath.Contains("PID_C548", StringComparison.OrdinalIgnoreCase)) return "Dispositivo Logitech Bolt";
        return string.IsNullOrWhiteSpace(enumeratedName) ? "Periférico Logitech" : enumeratedName;
    }

    private static string ExtractVidPid(string devicePath)
    {
        var vidStart = devicePath.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
        var pidStart = devicePath.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
        var vid = vidStart >= 0 && devicePath.Length >= vidStart + 8 ? devicePath.Substring(vidStart + 4, 4) : "0000";
        var pid = pidStart >= 0 && devicePath.Length >= pidStart + 8 ? devicePath.Substring(pidStart + 4, 4) : "0000";
        return $"{vid}:{pid}";
    }

    private static int ApproximateUnifiedLevel(byte level) => level switch
    {
        8 => 100, 4 => 60, 2 => 20, 1 => 5, _ => 0
    };

    private static string MapHidppStatus(byte status) => status switch
    {
        0 => "Descarregando", 1 => "Carregando", 2 => "Carregando lentamente",
        3 => "Completa", 4 => "Carregando", _ => "Conectado"
    };

    private static string MapWindowsStatus(Windows.System.Power.BatteryStatus status) => status switch
    {
        Windows.System.Power.BatteryStatus.Charging => "Carregando",
        Windows.System.Power.BatteryStatus.Discharging => "Descarregando",
        Windows.System.Power.BatteryStatus.Idle => "Conectado",
        Windows.System.Power.BatteryStatus.NotPresent => "Desconectado",
        _ => "Desconhecido"
    };

    private List<PeripheralBatteryInfo> GetCachedCopy()
    {
        lock (_cacheLock) return Clone(_cached);
    }

    private static List<PeripheralBatteryInfo> Clone(IEnumerable<PeripheralBatteryInfo> source) => source
        .Select(CloneItem).ToList();

    private static PeripheralBatteryInfo CloneItem(PeripheralBatteryInfo item) => new()
    {
        DeviceId = item.DeviceId,
        DeviceName = item.DeviceName,
        BatteryPercent = item.BatteryPercent,
        Status = item.Status,
        PollingRateHz = item.PollingRateHz,
        SupportsPollingRate = item.SupportsPollingRate
    };

    private static void WriteLog(string message)
    {
        try
        {
            var directory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EMECore", "Logs");
            Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "peripheral-battery.log");
            if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnóstico nunca pode impedir a coleta de bateria.
        }
    }
}
