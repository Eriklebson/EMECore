using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace EMECore.Hardware.Services;

public class FishingMacroService
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const ushort VK_E = 0x45;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;

    private bool _isFishing;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly Random _random = new();
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private DateTime _lastToggleTime = DateTime.MinValue;

    private float _baselineAudioLevel = 0;
    private bool _baselineCalibrated = false;
    private readonly List<float> _audioSamples = new();

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<int>? FishCaught;
    public event EventHandler? ToggleRequested;
    public bool IsRunning => _isFishing;

    public void InstallHook()
    {
        _proc = HookCallback;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    public void UninstallHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            if (vkCode == 0x45) // E key
            {
                bool ctrlPressed = (GetKeyState(VK_LCONTROL) & 0x8000) != 0 ||
                                   (GetKeyState(VK_RCONTROL) & 0x8000) != 0;
                if (ctrlPressed && (DateTime.Now - _lastToggleTime).TotalMilliseconds > 500)
                {
                    _lastToggleTime = DateTime.Now;
                    ToggleRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void StartFishing()
    {
        if (_isFishing) return;
        _isFishing = true;
        _cancellationTokenSource = new CancellationTokenSource();
        _baselineCalibrated = false;
        _audioSamples.Clear();
        StatusChanged?.Invoke(this, "Iniciando pesca automática...");
        Task.Run(() => FishingLoop(_cancellationTokenSource.Token));
    }

    public void StopFishing()
    {
        _isFishing = false;
        _cancellationTokenSource?.Cancel();
        StatusChanged?.Invoke(this, "Pesca automática parada.");
    }

    private async Task FishingLoop(CancellationToken ct)
    {
        try
        {
            // Calibrate baseline audio level
            StatusChanged?.Invoke(this, "Calibrando áudio...");
            await CalibrateAudio(ct);

            while (!ct.IsCancellationRequested && _isFishing)
            {
                // Focus on game window
                var gameHwnd = GetGameWindow();
                if (gameHwnd != IntPtr.Zero)
                {
                    SetForegroundWindow(gameHwnd);
                    await Task.Delay(200, ct);
                }

                // Cast fishing line
                StatusChanged?.Invoke(this, "Lançando linha...");
                PressKey(VK_E);
                await Task.Delay(1500 + _random.Next(500, 1000), ct);

                // Wait for fish bite sound
                StatusChanged?.Invoke(this, "Esperando peixe morder...");
                bool fishDetected = await WaitForFishBiteAudio(ct, 60000);

                if (ct.IsCancellationRequested || !_isFishing) break;

                if (fishDetected)
                {
                    // Hook the fish
                    StatusChanged?.Invoke(this, "Pescando!");
                    PressKey(VK_E);
                    await Task.Delay(300 + _random.Next(100, 200), ct);

                    // Reel in
                    StatusChanged?.Invoke(this, "Recolhendo...");
                    await ReelIn(ct);

                    StatusChanged?.Invoke(this, "Peixe pescado!");
                    FishCaught?.Invoke(this, 1);

                    await Task.Delay(1500 + _random.Next(500, 1000), ct);
                }
                else
                {
                    StatusChanged?.Invoke(this, "Nenhum peixe detectado...");
                    await Task.Delay(1000 + _random.Next(500, 1000), ct);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Erro: {ex.Message}");
        }
        finally
        {
            _isFishing = false;
        }
    }

    private async Task CalibrateAudio(CancellationToken ct)
    {
        var levels = new List<float>();
        using var capture = new WasapiLoopbackCapture();
        var waveFormat = capture.WaveFormat;

        capture.DataAvailable += (s, e) =>
        {
            if (e.BytesRecorded == 0) return;
            float maxSample = 0;
            for (int i = 0; i < e.BytesRecorded; i += waveFormat.BlockAlign)
            {
                for (int ch = 0; ch < waveFormat.Channels; ch++)
                {
                    int offset = i + ch * (waveFormat.BitsPerSample / 8);
                    if (offset + 2 <= e.BytesRecorded)
                    {
                        float sample = Math.Abs(BitConverter.ToInt16(e.Buffer, offset) / 32768f);
                        if (sample > maxSample) maxSample = sample;
                    }
                }
            }
            if (maxSample > 0.001f) levels.Add(maxSample);
        };

        capture.StartRecording();
        await Task.Delay(3000, ct); // 3 seconds calibration
        capture.StopRecording();

        if (levels.Count > 0)
        {
            _baselineAudioLevel = levels.Average();
            _baselineCalibrated = true;
            Log($"Baseline audio level: {_baselineAudioLevel:F4} (samples: {levels.Count})");
        }
        else
        {
            _baselineAudioLevel = 0.01f;
            _baselineCalibrated = true;
            Log("No audio detected during calibration, using default baseline");
        }
    }

    private async Task<bool> WaitForFishBiteAudio(CancellationToken ct, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        bool spikeDetected = false;

        using var capture = new WasapiLoopbackCapture();
        var waveFormat = capture.WaveFormat;

        float maxLevelSinceCast = 0;
        int samplesSinceCast = 0;
        var recentLevels = new Queue<float>();

        capture.DataAvailable += (s, e) =>
        {
            if (e.BytesRecorded == 0 || spikeDetected) return;

            float maxSample = 0;
            for (int i = 0; i < e.BytesRecorded; i += waveFormat.BlockAlign)
            {
                for (int ch = 0; ch < waveFormat.Channels; ch++)
                {
                    int offset = i + ch * (waveFormat.BitsPerSample / 8);
                    if (offset + 2 <= e.BytesRecorded)
                    {
                        float sample = Math.Abs(BitConverter.ToInt16(e.Buffer, offset) / 32768f);
                        if (sample > maxSample) maxSample = sample;
                    }
                }
            }

            samplesSinceCast++;
            if (maxSample > maxLevelSinceCast) maxLevelSinceCast = maxSample;

            recentLevels.Enqueue(maxSample);
            if (recentLevels.Count > 10) recentLevels.Dequeue();

            // Detect sudden spike: current level is much higher than baseline
            if (_baselineCalibrated && samplesSinceCast > 5)
            {
                float avgRecent = recentLevels.Average();
                float spikeRatio = avgRecent / Math.Max(_baselineAudioLevel, 0.001f);

                if (spikeRatio > 3.0f && avgRecent > 0.02f)
                {
                    Log($"Audio spike detected! Ratio: {spikeRatio:F1}, Level: {avgRecent:F4}, Baseline: {_baselineAudioLevel:F4}");
                    spikeDetected = true;
                }
            }
        };

        capture.StartRecording();

        while (sw.ElapsedMilliseconds < timeoutMs && !ct.IsCancellationRequested && _isFishing)
        {
            if (spikeDetected)
            {
                capture.StopRecording();
                return true;
            }
            await Task.Delay(50, ct);
        }

        capture.StopRecording();
        Log($"Audio wait timeout. Max level: {maxLevelSinceCast:F4}, Samples: {samplesSinceCast}");
        return false;
    }

    private async Task ReelIn(CancellationToken ct)
    {
        HoldKey(VK_E);
        await Task.Delay(2000 + _random.Next(500, 1500), ct);
        ReleaseKey(VK_E);

        for (int i = 0; i < 10 && !ct.IsCancellationRequested && _isFishing; i++)
        {
            PressKey(VK_E);
            await Task.Delay(150 + _random.Next(50, 100), ct);
        }
    }

    private static IntPtr GetGameWindow()
    {
        var processes = Process.GetProcessesByName("SB-Win64-Shipping");
        if (processes.Length > 0) return processes[0].MainWindowHandle;
        return IntPtr.Zero;
    }

    private void PressKey(ushort keyCode)
    {
        Log($"PressKey: 0x{keyCode:X2}");
        keybd_event((byte)keyCode, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        keybd_event((byte)keyCode, 0, 2, UIntPtr.Zero);
    }

    private void HoldKey(ushort keyCode)
    {
        Log($"HoldKey: 0x{keyCode:X2}");
        keybd_event((byte)keyCode, 0, 0, UIntPtr.Zero);
    }

    private void ReleaseKey(ushort keyCode)
    {
        Log($"ReleaseKey: 0x{keyCode:X2}");
        keybd_event((byte)keyCode, 0, 2, UIntPtr.Zero);
    }

    private static void Log(string message)
    {
        try
        {
            var logFile = Path.Combine(Path.GetTempPath(), "eme_fishing_log.txt");
            File.AppendAllText(logFile, $"{DateTime.Now:HH:mm:ss.fff} - {message}{Environment.NewLine}");
        }
        catch { }
    }

    public void Dispose()
    {
        UninstallHook();
        StopFishing();
        _cancellationTokenSource?.Dispose();
    }
}
