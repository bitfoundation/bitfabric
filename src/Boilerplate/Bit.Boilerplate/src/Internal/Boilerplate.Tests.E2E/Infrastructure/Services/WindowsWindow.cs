using System.Drawing;
using System.Runtime.InteropServices;

namespace Boilerplate.Tests.E2E.Infrastructure.Services;

/// <summary>
/// The window an installed Client.Windows app draws its WebView in. Most of what the kiosk lockdown does is out there
/// rather than in the page - a borderless, topmost window over the whole screen that refuses the user's own close - so
/// Windows is the only one that can be asked about it.
/// </summary>
public static class WindowsWindow
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const uint WM_SYSCOMMAND = 0x0112;
    private const nint SC_CLOSE = 0xF060;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint GA_ROOTOWNER = 3;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_TAB = 0x09;
    private const ushort VK_MENU = 0x12;

    /// <summary>
    /// The running process of <paramref name="windowsAppId"/>, once it has a window.
    /// <see cref="IPlaywrightExtensions.LaunchWindowsApp"/> kills every Client.Windows app before starting one, so
    /// there is never a second candidate.
    /// </summary>
    public static Process Of(string windowsAppId)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1);

        while (true)
        {
            var app = Process.GetProcessesByName(windowsAppId).FirstOrDefault();

            if (app is not null)
            {
                app.Refresh();

                if (app.MainWindowHandle is not 0)
                {
                    _ = app.SafeHandle; // Opened while it is alive, or ExitCode is refused on a process we did not start.
                    return app;
                }
            }

            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException($"'{windowsAppId}' had no window within a minute of the test attaching to its WebView.");

            Thread.Sleep(TimeSpan.FromMilliseconds(250));
        }
    }

    /// <summary>The title bar and the resize border - the one place a kiosk user could reach a Close button.</summary>
    public static bool HasCaption(Process app) => (GetWindowLongW(app.MainWindowHandle, GWL_STYLE) & (WS_CAPTION | WS_THICKFRAME)) is not 0;

    /// <summary>Above the taskbar and everything else, which is what <c>TopMostInFullScreen</c> asks for.</summary>
    public static bool IsTopMost(Process app) => (GetWindowLongW(app.MainWindowHandle, GWL_EXSTYLE) & WS_EX_TOPMOST) is not 0;

    /// <summary>
    /// The bounds the window has when it is not minimized. Read through the placement rather than
    /// <c>GetWindowRect</c> because a run that is not headed starts the apps minimized (see
    /// <see cref="IPlaywrightExtensions.LaunchWindowsApp"/>).
    /// </summary>
    public static Rectangle RestoredBounds(Process app)
    {
        var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };

        if (GetWindowPlacement(app.MainWindowHandle, ref placement) is false)
            throw new InvalidOperationException($"Windows did not report the placement of '{app.ProcessName}' ({Marshal.GetLastWin32Error()}).");

        return ToRectangle(placement.rcNormalPosition);
    }

    /// <summary>The monitor the window is on, which is what KioskModeManager sizes a full screen window to.</summary>
    public static Rectangle ScreenBounds(Process app)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        if (GetMonitorInfoW(MonitorFromWindow(app.MainWindowHandle, MONITOR_DEFAULTTONEAREST), ref info) is false)
            throw new InvalidOperationException($"Windows did not report the monitor of '{app.ProcessName}' ({Marshal.GetLastWin32Error()}).");

        return ToRectangle(info.rcMonitor);
    }

    /// <summary>
    /// The close a person performs - Alt+F4, or the title bar's X. WinForms turns SC_CLOSE into
    /// <c>CloseReason.UserClosing</c>, the one reason <c>WindowsKioskGuard</c> refuses; a plain WM_CLOSE arrives as
    /// <c>TaskManagerClosing</c> and is let through deliberately, as is a Windows shutdown.
    /// </summary>
    public static void RequestUserClose(Process app)
    {
        if (PostMessageW(app.MainWindowHandle, WM_SYSCOMMAND, SC_CLOSE, 0) is false)
            throw new InvalidOperationException($"Windows did not deliver the close request to '{app.ProcessName}' ({Marshal.GetLastWin32Error()}).");
    }

    /// <summary>
    /// Whether the window in front, the one that gets the keyboard, is the app's or one it owns - what
    /// <c>WindowsKioskGuard</c> itself asks before it swallows a key.
    /// </summary>
    public static bool IsInFront(Process app)
    {
        GetWindowThreadProcessId(GetAncestor(GetForegroundWindow(), GA_ROOTOWNER), out var processId);

        return processId == app.Id;
    }

    /// <summary>Whether any window is in front - none is while the desktop is locked.</summary>
    public static bool IsDesktopInteractive() => GetForegroundWindow() is not 0;

    /// <summary>
    /// Alt+Tab as a person presses it: into the input Windows reads from the keyboard, where a keyboard hook sees it,
    /// in one call so nothing else typed lands in between. False when Windows refused it - a locked or otherwise
    /// non-interactive desktop.
    /// </summary>
    public static bool PressAltTab()
    {
        INPUT[] inputs = [Key(VK_MENU, up: false), Key(VK_TAB, up: false), Key(VK_TAB, up: true), Key(VK_MENU, up: true)];

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;

        static INPUT Key(ushort virtualKey, bool up) => new()
        {
            type = INPUT_KEYBOARD,
            union = new() { ki = new() { wVk = virtualKey, dwFlags = up ? KEYEVENTF_KEYUP : 0 } }
        };
    }

    private static Rectangle ToRectangle(RECT rect) => Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION union;
    }

    // MOUSEINPUT is the largest member, so it is here for the size SendInput checks, not to be sent.
    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point ptMinPosition;
        public Point ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLongW(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(nint hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hWnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
