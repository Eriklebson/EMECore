using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace EMECore.Hardware.Services;

public class FishingMacroService
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

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
    private struct RECT { public int Left, Top, Right, Bottom; }

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

    // Learning mode
    private bool _isLearning;
    private readonly List<string> _learnScreenshots = new();
    private int _learnFrameCount;

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

            // Ctrl+E to toggle
            if (vkCode == 0x45)
            {
                bool ctrl = (GetKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetKeyState(VK_RCONTROL) & 0x8000) != 0;
                if (ctrl && (DateTime.Now - _lastToggleTime).TotalMilliseconds > 500)
                {
                    _lastToggleTime = DateTime.Now;
                    ToggleRequested?.Invoke(this, EventArgs.Empty);
                }
            }

            // Ctrl+Shift+F to mark fish bite (for learning)
            if (vkCode == 0x46) // F key
            {
                bool ctrl = (GetKeyState(VK_LCONTROL) & 0x8000) != 0;
                bool shift = (GetKeyState(0xA0) & 0x8000) != 0 || (GetKeyState(0xA1) & 0x8000) != 0;
                if (ctrl && shift && _isLearning)
                {
                    MarkFishBite();
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
        StatusChanged?.Invoke(this, "Iniciando pesca automática...");
        Task.Run(() => FishingLoop(_cancellationTokenSource.Token));
    }

    public void StopFishing()
    {
        _isFishing = false;
        _cancellationTokenSource?.Cancel();
        StatusChanged?.Invoke(this, "Pesca automática parada.");
    }

    public void StartLearning()
    {
        if (_isLearning) return;
        _isLearning = true;
        _learnScreenshots.Clear();
        _learnFrameCount = 0;
        StatusChanged?.Invoke(this, "MODO APRENDIZADO: Pesque manualmente e pressione Ctrl+Shift+F quando o peixe morder");
        Task.Run(() => LearningLoop());
    }

    public void StopLearning()
    {
        _isLearning = false;
        StatusChanged?.Invoke(this, "Aprendizado finalizado. Analise os screenshots na pasta TEMP");
    }

    private void MarkFishBite()
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"eme_fishing_BITE_{_learnFrameCount}.png");
            SaveScreenshot("bite_marker");
            Log($"FISH BITE MARKED at frame {_learnFrameCount}");
            StatusChanged?.Invoke(this, $"Mordida marcada no frame {_learnFrameCount}!");
        }
        catch { }
    }

    private async Task LearningLoop()
    {
        try
        {
            while (_isLearning)
            {
                _learnFrameCount++;
                var frameName = $"learn_{_learnFrameCount:D4}";
                SaveScreenshot(frameName);
                _learnScreenshots.Add(frameName);
                Log($"Learning frame {_learnFrameCount}");
                await Task.Delay(200); // 5 fps
            }
        }
        catch { }
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

                // Wait and try to detect fish bite
                StatusChanged?.Invoke(this, "Esperando peixe morder...");
                bool fishCaught = false;

                for (int attempt = 0; attempt < 15 && !ct.IsCancellationRequested && _isFishing; attempt++)
                {
                    await Task.Delay(1500, ct);

                    // Try to hook by pressing E
                    Log($"Attempt {attempt + 1} - pressing E");
                    PressKey(VK_E);
                    await Task.Delay(300, ct);

                    // After a few attempts, try reeling
                    if (attempt >= 3)
                    {
                        StatusChanged?.Invoke(this, "Tentando recolher...");
                        Log("Trying to reel in");

                        HoldKey(VK_E);
                        await Task.Delay(2000, ct);
                        ReleaseKey(VK_E);

                        for (int i = 0; i < 8; i++)
                        {
                            PressKey(VK_E);
                            await Task.Delay(200, ct);
                        }

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
                    Log("No fish, retrying");
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

    private static IntPtr GetGameWindow()
    {
        var processes = Process.GetProcessesByName("SB-Win64-Shipping");
        if (processes.Length > 0) return processes[0].MainWindowHandle;
        return IntPtr.Zero;
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
            if (width <= 0 || height <= 0) return;

            using var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));

            var path = Path.Combine(Path.GetTempPath(), $"eme_fishing_{name}.png");
            bitmap.Save(path, ImageFormat.Png);
        }
        catch { }
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
        StopLearning();
        _cancellationTokenSource?.Dispose();
    }
}
