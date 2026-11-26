//+:cnd:noEmit
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Boilerplate.Client.Windows.Infrastructure.Services;

/// <summary>
/// The lockdown a kiosk needs on top of <see cref="KioskModeManager"/>, which only takes the window full screen:
/// the key combinations that reach the desktop are swallowed, and the window refuses to close - the app's own exit
/// is the only way out. Ctrl+Alt+Del is out of reach of every user mode process - block it with the Windows policy
/// of the kiosk machine.
/// <para>
/// It holds while the app is still starting, too. The keyboard hook runs on a thread of its own that does nothing
/// else: Windows waits only briefly for a hook to answer before it lets the key through, and may drop the hook
/// altogether, while the UI thread is busy for seconds loading the WebView and the app. Once the window is shown and
/// again once the app is up, <see cref="Reassert"/> puts back whatever slipped meanwhile.
/// </para>
/// </summary>
public static partial class WindowsKioskGuard
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const uint WM_REINSTALL_HOOK = 0x8000; // WM_APP
    private const uint PM_NOREMOVE = 0;
    private const uint GA_ROOTOWNER = 3;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOPMOST = 0x00000008;

    private delegate nint HookProc(int code, nint wParam, nint lParam);

    private static readonly uint processId = (uint)Environment.ProcessId;

    // The field roots the delegate; a local would let the GC collect what the hook calls back into.
    private static readonly HookProc hookProc = OnKey;

    private static Form? kioskForm;
    private static KioskModeManager? kioskModeManager;

    private static uint hookThreadId;

    // Touched by the hook's thread only.
    private static nint hook;

    /// <summary>
    /// Locks <paramref name="form"/> down for as long as the process runs; Windows drops the hook when it exits.
    /// Call it before the window is shown, so the keys are swallowed from the first moment it is in front.
    /// </summary>
    public static void Apply(Form form, KioskModeManager manager)
    {
        kioskForm = form;
        kioskModeManager = manager;

        StartHookThread();

        // Windows itself still gets to shut down; only the person in front of the machine is refused.
        form.FormClosing += (_, e) => e.Cancel = e.CloseReason is CloseReason.UserClosing;

        // A key pressed before the window existed may have given the front to another one.
        form.Shown += (_, _) => Reassert();
    }

    /// <summary>
    /// Checks that the app really is in kiosk mode - in front, topmost, over its whole monitor, with the keyboard hook
    /// in place - and puts back whatever slipped. Call it on the UI thread. A minimized window is left as it is: the
    /// lockdown takes away every key that minimizes it, so whatever did was meant to.
    /// </summary>
    public static void Reassert()
    {
        // IsIconic rather than form.WindowState, which says Normal for a window minimized by anything but the form.
        if (kioskForm is not { IsDisposed: false, IsHandleCreated: true } form || kioskModeManager is null
            || IsIconic(form.Handle))
            return;

        // Windows does not tell when it drops a hook, so a fresh one is the only way to be sure one is there.
        PostThreadMessageW(hookThreadId, WM_REINSTALL_HOOK, 0, 0);

        List<string> slipped = [];

        if (kioskModeManager.FullScreen is false)
        {
            kioskModeManager.FullScreen = true;
            slipped.Add("full screen");
        }

        var screenBounds = Screen.FromHandle(form.Handle).Bounds;
        var isMaximized = IsZoomed(form.Handle);
        if (isMaximized || form.FormBorderStyle is not FormBorderStyle.None || form.Bounds != screenBounds)
        {
            if (isMaximized)
            {
                form.WindowState = FormWindowState.Normal;
            }

            form.FormBorderStyle = FormBorderStyle.None;
            form.Bounds = screenBounds;
            slipped.Add("covering the screen");
        }

        if ((GetWindowLongW(form.Handle, GWL_EXSTYLE) & WS_EX_TOPMOST) is 0)
        {
            slipped.Add("topmost");
        }

        // Set even when the style is there: it also lifts the window above a topmost one that came up after it.
        form.TopMost = true;

        var tookTheFrontBack = true;
        if (IsKioskInFront() is false)
        {
            BringToFront(form.Handle);
            tookTheFrontBack = IsKioskInFront();
            slipped.Add("in front");
        }

        if (slipped.Count is 0)
            return;

        var logger = Program.Services?.GetService<ILoggerFactory>()?.CreateLogger(typeof(WindowsKioskGuard));

        logger?.LogWarning("Kiosk mode had slipped and was put back: {Slipped}.", string.Join(", ", slipped));

        if (tookTheFrontBack is false)
        {
            // A locked desktop, for one, has no front to give.
            logger?.LogWarning("Windows refused the kiosk window the front.");
        }
    }

    private static void StartHookThread()
    {
        Exception? failure = null;
        using var installed = new ManualResetEventSlim();

        new Thread(() =>
        {
            // Creates the thread's message queue, which Reassert posts to.
            PeekMessageW(out _, 0, 0, 0, PM_NOREMOVE);
            hookThreadId = GetCurrentThreadId();

            hook = SetWindowsHookExW(WH_KEYBOARD_LL, hookProc, 0, 0);
            if (hook is 0)
            {
                failure = new Win32Exception();
            }

            installed.Set();

            if (failure is not null)
                return;

            // Windows calls the hook from within GetMessage, so waiting here is all the thread has to do.
            while (GetMessageW(out var message, 0, 0, 0) > 0)
            {
                if (message.message is WM_REINSTALL_HOOK)
                {
                    ReinstallHook();
                }
            }
        })
        {
            IsBackground = true,
            Name = "Kiosk keyboard hook",
            Priority = ThreadPriority.Highest
        }.Start();

        installed.Wait();

        if (failure is not null)
            throw failure;
    }

    /// <summary>The new hook goes in before the old one comes out, so no key slips through in between.</summary>
    private static void ReinstallHook()
    {
        var freshHook = SetWindowsHookExW(WH_KEYBOARD_LL, hookProc, 0, 0);

        // The hook in place, if Windows has not dropped it, keeps doing the job.
        if (freshHook is 0)
            return;

        UnhookWindowsHookEx(hook);
        hook = freshHook;
    }

    private static nint OnKey(int code, nint wParam, nint lParam)
    {
        // Only while the kiosk is in front: a global block would take the desktop away from whatever else runs.
        if (code >= 0 && wParam is WM_KEYDOWN or WM_SYSKEYDOWN
            && IsBlocked((Keys)Marshal.ReadInt32(lParam)) && IsKioskInFront())
            return 1;

        return CallNextHookEx(0, code, wParam, lParam);
    }

    private static bool IsBlocked(Keys key) => key switch
    {
        Keys.LWin or Keys.RWin => true,                                                  // the start menu and every Win+? combination
        Keys.Apps => true,                                                               // the context menu key
        Keys.Tab or Keys.Escape or Keys.F4 or Keys.Space when IsDown(Keys.Menu) => true, // Alt+Tab, Alt+Esc, Alt+F4, Alt+Space
        Keys.Escape when IsDown(Keys.ControlKey) => true,                                // Ctrl+Esc, Ctrl+Shift+Esc
        _ => false
    };

    // GetAsyncKeyState, not Control.ModifierKeys: in a low level hook the key has not reached the queue the latter reads.
    private static bool IsDown(Keys key) => (GetAsyncKeyState((int)key) & 0x8000) is not 0;

    /// <summary>
    /// Whether the window in front is the kiosk's, or one it owns, such as a dialog the WebView opens from its own
    /// process. Asked of Windows rather than of the form, so it holds from the moment the window exists, and off the
    /// UI thread.
    /// </summary>
    private static bool IsKioskInFront()
    {
        GetWindowThreadProcessId(GetAncestor(GetForegroundWindow(), GA_ROOTOWNER), out var ownerProcessId);

        return ownerProcessId == processId;
    }

    /// <summary>
    /// Windows lets a window take the front only from the process the user last gave input to, and a key pressed while
    /// the app started may have gone to another one. Sharing that window's input state for the moment of the call is
    /// what lets the kiosk take the front back.
    /// </summary>
    private static void BringToFront(nint window)
    {
        var foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var currentThreadId = GetCurrentThreadId();
        var attached = foregroundThreadId is not 0 && foregroundThreadId != currentThreadId
            && AttachThreadInput(currentThreadId, foregroundThreadId, true);

        try
        {
            SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    // hhk is documented as ignored.
    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessageW(uint idThread, uint msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetWindowLongW(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
