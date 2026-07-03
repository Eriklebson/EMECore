using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

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
            // Test: Type "Teste" to verify macro is working
            StatusChanged?.Invoke(this, "Testando macro...");
            await Task.Delay(1000, ct);
            TypeText("Teste");
            await Task.Delay(2000, ct);

            int cycle = 0;
            while (!ct.IsCancellationRequested && _isFishing)
            {
                cycle++;
                Log($"=== Cycle {cycle} ===");

                // Focus on game window
                var gameHwnd = GetGameWindow();
                if (gameHwnd != IntPtr.Zero)
                {
                    SetForegroundWindow(gameHwnd);
                    await Task.Delay(300, ct);
                }

                // Cast fishing line
                StatusChanged?.Invoke(this, "Lançando linha...");
                PressKey(VK_E);
                await Task.Delay(2000, ct);

                // Save screenshot after casting
                SaveScreenshot("after_cast");

                // Wait for fish to bite (try pressing E periodically)
                StatusChanged?.Invoke(this, "Esperando peixe morder...");
                bool fishCaught = false;

                for (int attempt = 0; attempt < 20 && !ct.IsCancellationRequested && _isFishing; attempt++)
                {
                    await Task.Delay(1000, ct);
                    SaveScreenshot($"wait_{attempt}");

                    // Try to hook by pressing E
                    Log($"Attempt {attempt + 1} - pressing E to hook");
                    PressKey(VK_E);
                    await Task.Delay(500, ct);

                    // Check if we're now in the reeling phase
                    // (we'll just try to reel and see what happens)
                    if (attempt >= 2) // After a few attempts, try reeling
                    {
                        StatusChanged?.Invoke(this, "Tentando recolher...");
                        Log("Trying to reel in");

                        // Try reeling
                        HoldKey(VK_E);
                        await Task.Delay(1500, ct);
                        ReleaseKey(VK_E);
                        await Task.Delay(500, ct);

                        // More reel attempts
                        for (int i = 0; i < 5; i++)
                        {
                            PressKey(VK_E);
                            await Task.Delay(200, ct);
                        }

                        SaveScreenshot("after_reel");
                        fishCaught = true;
                        break;
                    }
                }

                if (fishCaught)
                {
                    StatusChanged?.Invoke(this, "Peixe pescado!");
                    FishCaught?.Invoke(this, 1);
                    Log("Fish caught!");
                }
                else
                {
                    StatusChanged?.Invoke(this, "Nenhum peixe, tentando novamente...");
                    Log("No fish detected, retrying");
                }

                await Task.Delay(2000 + _random.Next(1000, 2000), ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Erro: {ex.Message}");
            Log($"Error: {ex}");
        }
        finally
        {
            _isFishing = false;
        }
    }

    private static void SaveScreenshot(string name)
    {
        try
        {
            var processes = Process.GetProcessesByName("SB-Win64-Shipping");
            if (processes.Length == 0) return;

            var hwnd = processes[0].MainWindowHandle;
            if (hwnd == IntPtr.Zero) return;

            RECT rect;
            if (!GetWindowRect(hwnd, out rect)) return;

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;

            using var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height));

            var path = Path.Combine(Path.GetTempPath(), $"eme_fishing_{name}.png");
            bitmap.Save(path, ImageFormat.Png);
            Log($"Screenshot saved: {path}");
        }
        catch (Exception ex)
        {
            Log($"Screenshot error: {ex.Message}");
        }
    }

    private async Task CalibrateAudio(CancellationToken ct)
    {
        var levels = new List<float>();
        using var capture = new WasapiLoopbackCapture();
        var waveFormat = capture.WaveFormat;
        bool isFloat = waveFormat.Encoding == WaveFormatEncoding.IeeeFloat;
        int bytesPerSample = waveFormat.BitsPerSample / 8;

        Log($"Audio format: {waveFormat.Encoding}, {waveFormat.BitsPerSample}bit, {waveFormat.Channels}ch, {waveFormat.SampleRate}Hz");

        capture.DataAvailable += (s, e) =>
        {
            if (e.BytesRecorded == 0) return;
            float maxSample = 0;
            for (int i = 0; i < e.BytesRecorded; i += waveFormat.BlockAlign)
            {
                for (int ch = 0; ch < waveFormat.Channels; ch++)
                {
                    int offset = i + ch * bytesPerSample;
                    if (offset + bytesPerSample <= e.BytesRecorded)
                    {
                        float sample;
                        if (isFloat && bytesPerSample == 4)
                        {
                            sample = Math.Abs(BitConverter.ToSingle(e.Buffer, offset));
                        }
                        else if (bytesPerSample == 2)
                        {
                            sample = Math.Abs(BitConverter.ToInt16(e.Buffer, offset) / 32768f);
                        }
                        else
                        {
                            sample = Math.Abs(BitConverter.ToInt16(e.Buffer, offset) / 32768f);
                        }
                        if (sample > maxSample) maxSample = sample;
                    }
                }
            }
            if (maxSample > 0.001f && maxSample < 0.99f) levels.Add(maxSample);
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
            _baselineAudioLevel = 0.005f;
            _baselineCalibrated = true;
            Log("No valid audio detected during calibration, using default baseline");
        }
    }

    private async Task<bool> WaitForFishBiteAudio(CancellationToken ct, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        bool spikeDetected = false;

        using var capture = new WasapiLoopbackCapture();
        var waveFormat = capture.WaveFormat;
        bool isFloat = waveFormat.Encoding == WaveFormatEncoding.IeeeFloat;
        int bytesPerSample = waveFormat.BitsPerSample / 8;

        Log($"Monitor audio: {waveFormat.Encoding}, {waveFormat.BitsPerSample}bit, {waveFormat.Channels}ch, {waveFormat.SampleRate}Hz");

        float maxLevelSinceCast = 0;
        int samplesSinceCast = 0;
        var recentLevels = new Queue<float>();

        // Collect raw audio bytes
        var audioChunks = new List<byte[]>();

        capture.DataAvailable += (s, e) =>
        {
            if (e.BytesRecorded == 0) return;

            // Save audio data
            var chunk = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, chunk, e.BytesRecorded);
            audioChunks.Add(chunk);

            if (spikeDetected) return;

            float maxSample = 0;
            for (int i = 0; i < e.BytesRecorded; i += waveFormat.BlockAlign)
            {
                for (int ch = 0; ch < waveFormat.Channels; ch++)
                {
                    int offset = i + ch * bytesPerSample;
                    if (offset + bytesPerSample <= e.BytesRecorded)
                    {
                        float sample;
                        if (isFloat && bytesPerSample == 4)
                            sample = Math.Abs(BitConverter.ToSingle(e.Buffer, offset));
                        else
                            sample = Math.Abs(BitConverter.ToInt16(e.Buffer, offset) / 32768f);
                        if (sample > maxSample) maxSample = sample;
                    }
                }
            }

            samplesSinceCast++;
            if (maxSample > maxLevelSinceCast) maxLevelSinceCast = maxSample;

            recentLevels.Enqueue(maxSample);
            if (recentLevels.Count > 10) recentLevels.Dequeue();

            if (samplesSinceCast % 20 == 0)
                Log($"Audio level: {maxSample:F4}, avg: {recentLevels.Average():F4}, baseline: {_baselineAudioLevel:F4}");

            if (_baselineCalibrated && samplesSinceCast > 5)
            {
                float avgRecent = recentLevels.Average();
                float spikeRatio = avgRecent / Math.Max(_baselineAudioLevel, 0.001f);
                if (spikeRatio > 3.0f && avgRecent > 0.01f)
                {
                    Log($"Audio spike detected! Ratio: {spikeRatio:F1}, Level: {avgRecent:F4}");
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
                SaveWavFile(audioChunks, waveFormat);
                return true;
            }
            await Task.Delay(50, ct);
        }

        capture.StopRecording();
        SaveWavFile(audioChunks, waveFormat);
        Log($"Audio wait timeout. Max level: {maxLevelSinceCast:F4}");
        return false;
    }

    private static void SaveWavFile(List<byte[]> audioChunks, WaveFormat waveFormat)
    {
        try
        {
            var wavPath = Path.Combine(Path.GetTempPath(), "eme_fishing_audio.wav");
            using var fs = new FileStream(wavPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            int totalDataSize = audioChunks.Sum(c => c.Length);

            // WAV header
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + totalDataSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write((short)waveFormat.Channels);
            bw.Write(waveFormat.SampleRate);
            bw.Write(waveFormat.AverageBytesPerSecond);
            bw.Write((short)waveFormat.BlockAlign);
            bw.Write((short)waveFormat.BitsPerSample);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(totalDataSize);

            foreach (var chunk in audioChunks)
                bw.Write(chunk);

            Log($"Audio saved to {wavPath} ({totalDataSize} bytes)");
        }
        catch (Exception ex)
        {
            Log($"Error saving audio: {ex.Message}");
        }
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

    private void TypeText(string text)
    {
        foreach (char c in text)
        {
            ushort vk = CharToVK(c);
            if (vk != 0) PressKey(vk);
            Thread.Sleep(50);
        }
    }

    private static ushort CharToVK(char c)
    {
        if (c >= 'a' && c <= 'z') c = (char)(c - 32);
        return c switch
        {
            'A' => 0x41, 'B' => 0x42, 'C' => 0x43, 'D' => 0x44, 'E' => 0x45,
            'F' => 0x46, 'G' => 0x47, 'H' => 0x48, 'I' => 0x49, 'J' => 0x4A,
            'K' => 0x4B, 'L' => 0x4C, 'M' => 0x4D, 'N' => 0x4E, 'O' => 0x4F,
            'P' => 0x50, 'Q' => 0x51, 'R' => 0x52, 'S' => 0x53, 'T' => 0x54,
            'U' => 0x55, 'V' => 0x56, 'W' => 0x57, 'X' => 0x58, 'Y' => 0x59,
            'Z' => 0x5A, ' ' => 0x20, _ => 0x00
        };
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
