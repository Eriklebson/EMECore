using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace EMECore.Hardware.Services;

public class FishingMacroService
{
    // Windows API for input simulation
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public INPUTUNION union;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    // Constants
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_E = 0x45;
    private const ushort VK_SPACE = 0x20;
    private const ushort VK_ENTER = 0x0D;

    // Fishing state
    private bool _isFishing;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly Random _random = new();

    // Events
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<int>? FishCaught;

    public bool IsRunning => _isFishing;

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

    private async Task FishingLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _isFishing)
            {
                StatusChanged?.Invoke(this, "Lançando linha...");

                // Press E to cast fishing line
                PressKey(VK_E);
                await Task.Delay(500 + _random.Next(200, 500), ct);

                StatusChanged?.Invoke(this, "Esperando peixe morder...");

                // Wait for fish to bite (simulate with random delay 3-8 seconds)
                await Task.Delay(3000 + _random.Next(0, 5000), ct);

                if (ct.IsCancellationRequested) break;

                StatusChanged?.Invoke(this, "Pescando!");

                // Press E to hook the fish
                PressKey(VK_E);
                await Task.Delay(200 + _random.Next(100, 300), ct);

                // Simulate reeling in (press E multiple times)
                for (int i = 0; i < 3; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    PressKey(VK_E);
                    await Task.Delay(300 + _random.Next(100, 400), ct);
                }

                StatusChanged?.Invoke(this, "Peixe pescado!");
                FishCaught?.Invoke(this, 1);

                // Wait before next cast
                await Task.Delay(2000 + _random.Next(1000, 3000), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Erro: {ex.Message}");
        }
        finally
        {
            _isFishing = false;
        }
    }

    private void PressKey(ushort keyCode)
    {
        var inputs = new INPUT[2];

        // Key down
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].union.ki.wVk = keyCode;
        inputs[0].union.ki.dwFlags = 0;

        // Key up
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].union.ki.wVk = keyCode;
        inputs[1].union.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public void Dispose()
    {
        StopFishing();
        _cancellationTokenSource?.Dispose();
    }
}
