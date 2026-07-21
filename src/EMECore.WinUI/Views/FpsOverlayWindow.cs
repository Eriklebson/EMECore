using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using EMECore.Hardware.Services;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public class FpsOverlayWindow : IDisposable
{
    private readonly FpsMonitorService _fpsService;
    private System.Threading.Timer? _updateTimer;
    private IntPtr _hwnd;
    private bool _disposed;
    private bool _active;

    private IntPtr _hFontMain;
    private IntPtr _hFontDetail;

    private bool _showLow1 = true;
    private bool _showLow01 = true;
    private bool _showFrameTime = true;

    private static bool _classRegistered;
    private static string? _className;

    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int LWA_COLORKEY = 0x00000001;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_VISIBLE = 0x10000000;
    private const int WM_PAINT = 0x000F;
    private const int WM_CLOSE = 0x0010;
    private const int WM_DESTROY = 0x0002;
    private const int WM_ERASEBKGND = 0x0014;
    private const int SW_SHOWNOACTIVATE = 4;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateFont(int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight, uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet, uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality, uint fdwPitchAndFamily, string lpszFace);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool SetTextColor(IntPtr hdc, int crColor);
    [DllImport("gdi32.dll")] private static extern bool TextOut(IntPtr hdc, int x, int y, string lpString, int c);
    [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(int crColor);
    [DllImport("user32.dll")] private static extern int FillRect(IntPtr hdc, ref RECT lprc, IntPtr hbr);
    [DllImport("gdi32.dll")] private static extern void SetBkMode(IntPtr hdc, int mode);
    [DllImport("kernel32.dll")] private static extern int MulDiv(int nNumber, int nNumerator, int nDenominator);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, int crKey, byte bAlpha, int dwFlags);
    [DllImport("gdi32.dll")] private static extern bool ExtTextOut(IntPtr hdc, int x, int y, uint options, ref RECT lprect, string lpString, uint c, IntPtr lpDx);

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT { public IntPtr hdc; public bool fErase; public RECT rcPaint; public bool fRestore; public bool fIncUpdate; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved; }
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    private struct COLORREF { public static int FromRgb(byte r, byte g, byte b) => r | (g << 8) | (b << 16); }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS { public uint style; public IntPtr lpfnWndProc; public int cbClsExtra; public int cbWndExtra; public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground; public string? lpszMenuName; public string? lpszClassName; }

    private static WndProcDelegate? _staticWndProc;
    private static FpsOverlayWindow? _currentInstance;

    public FpsOverlayWindow(FpsMonitorService fpsService)
    {
        _fpsService = fpsService;
    }

    public bool IsActive => _active;

    public void UpdateDisplayOptions(bool showLow1, bool showLow01, bool showFrameTime)
    {
        _showLow1 = showLow1;
        _showLow01 = showLow01;
        _showFrameTime = showFrameTime;
        if (_active && _hwnd != IntPtr.Zero)
            InvalidateRect(_hwnd, IntPtr.Zero, false);
    }

    public void Start()
    {
        if (_active) return;
        _active = true;

        var hInstance = GetModuleHandle(null);

        if (_staticWndProc == null)
        {
            _staticWndProc = StaticWndProc;
            GC.KeepAlive(_staticWndProc);
        }

        _className ??= $"EMEFpsOverlay_{Process.GetCurrentProcess().Id}";

        if (!_classRegistered)
        {
            var wc = new WNDCLASS
            {
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_staticWndProc),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = hInstance,
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = _className
            };

            if (RegisterClass(ref wc) == 0) { _active = false; return; }
            _classRegistered = true;
            GC.KeepAlive(_staticWndProc);
        }

        _currentInstance = this;

        _hwnd = CreateWindowEx(
            WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED,
            _className!, "EME FPS",
            WS_POPUP | WS_VISIBLE,
            20, 20, 280, 70,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero) { _currentInstance = null; _active = false; return; }

        SetLayeredWindowAttributes(_hwnd, COLORREF.FromRgb(0, 0, 0), 0, LWA_COLORKEY);

        CreateFonts();
        ShowWindow(_hwnd, SW_SHOWNOACTIVATE);
        _updateTimer = new System.Threading.Timer(OnUpdate, null, 0, 500);
    }

    public void Stop()
    {
        if (!_active) return;
        _active = false;
        _currentInstance = null;
        _updateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _updateTimer?.Dispose();
        _updateTimer = null;
        DestroyFonts();
        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
        GC.KeepAlive(_staticWndProc);
    }

    private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var self = _currentInstance;
        if (self == null || !self._active)
            return DefWindowProc(hWnd, msg, wParam, lParam);

        switch (msg)
        {
            case WM_PAINT:
                self.Paint(hWnd);
                return IntPtr.Zero;
            case WM_ERASEBKGND:
                return new IntPtr(1);
            case WM_CLOSE:
                self.Stop();
                return IntPtr.Zero;
            case WM_DESTROY:
                return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void CreateFonts()
    {
        DestroyFonts();
        var hdc = GetDC(_hwnd);
        if (hdc == IntPtr.Zero) return;
        int logPixelsY = GetDeviceCaps(hdc, 90);
        _hFontMain = CreateFont(-MulDiv(22, logPixelsY, 72), 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 4, 0, "Segoe UI");
        _hFontDetail = CreateFont(-MulDiv(9, logPixelsY, 72), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 4, 0, "Segoe UI");
        ReleaseDC(_hwnd, hdc);
    }

    private void DestroyFonts()
    {
        if (_hFontMain != IntPtr.Zero) { DeleteObject(_hFontMain); _hFontMain = IntPtr.Zero; }
        if (_hFontDetail != IntPtr.Zero) { DeleteObject(_hFontDetail); _hFontDetail = IntPtr.Zero; }
    }

    private void OnUpdate(object? state)
    {
        if (!_active || _hwnd == IntPtr.Zero) return;
        InvalidateRect(_hwnd, IntPtr.Zero, false);
    }

    private void Paint(IntPtr hWnd)
    {
        var ps = new PAINTSTRUCT();
        var hdc = BeginPaint(hWnd, out ps);

        GetClientRect(hWnd, out var rect);

        var brush = CreateSolidBrush(COLORREF.FromRgb(0, 0, 0));
        FillRect(hdc, ref rect, brush);
        DeleteObject(brush);

        var fps = _fpsService.Current;
        string fpsText = fps > 0 ? $"{fps:F0} FPS" : "WAITING...";

        string line1 = fps > 0
            ? $"MIN {_fpsService.Min:F0}   AVG {_fpsService.Avg:F0}   MAX {_fpsService.Max:F0}"
            : (_fpsService.Source != "Off" ? _fpsService.Source : "");

        var extras = new List<string>();
        if (fps > 0)
        {
            if (_showLow1 && _fpsService.Low1 > 0) extras.Add($"1% LOW  {_fpsService.Low1:F0}");
            if (_showLow01 && _fpsService.Low01 > 0) extras.Add($"0.1% LOW  {_fpsService.Low01:F0}");
            if (_showFrameTime && _fpsService.FrameTimeMs > 0) extras.Add($"FRAME  {_fpsService.FrameTimeMs:F1} ms");
        }
        string line2 = extras.Count > 0 ? string.Join("   ", extras) : "";

        var valueColor = fps >= 55 ? ThemeManager.Current.Success
            : fps >= 30 ? ThemeManager.Current.Warning
            : fps > 0 ? ThemeManager.Current.Danger
            : ThemeManager.Current.TextMuted;
        var detailColor = ThemeManager.Current.TextSecondary;

        var oldFont = SelectObject(hdc, _hFontMain);
        SetBkMode(hdc, 1);

        DrawTextOutline(hdc, 4, 2, fpsText, valueColor.R, valueColor.G, valueColor.B);

        SelectObject(hdc, _hFontDetail);
        DrawTextOutline(hdc, 4, 32, line1, detailColor.R, detailColor.G, detailColor.B);
        if (line2.Length > 0)
            DrawTextOutline(hdc, 4, 48, line2, detailColor.R, detailColor.G, detailColor.B);

        SelectObject(hdc, oldFont);
        EndPaint(hWnd, ref ps);
    }

    private void DrawTextOutline(IntPtr hdc, int x, int y, string text, byte fr, byte fg, byte fb)
    {
        SetTextColor(hdc, COLORREF.FromRgb(0, 0, 0));
        TextOut(hdc, x - 1, y, text, text.Length);
        TextOut(hdc, x + 1, y, text, text.Length);
        TextOut(hdc, x, y - 1, text, text.Length);
        TextOut(hdc, x, y + 1, text, text.Length);
        SetTextColor(hdc, COLORREF.FromRgb(fr, fg, fb));
        TextOut(hdc, x, y, text, text.Length);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Stop();
        }
        GC.SuppressFinalize(this);
    }
}
