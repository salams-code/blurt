using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// Thin Win32 adapter over <see cref="TriggerResolver"/>: installs a
/// <c>WH_KEYBOARD_LL</c> hook, decodes each raw event into a <see cref="KeyInput"/>,
/// asks the resolver what to do, raises recognised triggers, and swallows the
/// keystroke when told to (so the AltGr character never reaches the focused app).
///
/// All testable decision logic lives in the core; this class only does the
/// platform plumbing, which is verified manually on Windows.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;   // keys pressed while Alt is held
    private const int WM_SYSKEYUP = 0x0105;

    private readonly TriggerResolver _resolver;
    private readonly LowLevelKeyboardProc _proc;   // held in a field so the GC never collects the callback
    private IntPtr _hookHandle;

    // Suspends trigger processing without uninstalling the hook (issue 20). While a
    // config window is open we suspend so the callback passes every keystroke through
    // unchanged — the trigger characters , . - (and AltGr chords) reach the focused
    // text field instead of being swallowed or firing a dictation behind the window.
    // Volatile because the low-level hook callback can run off the UI thread.
    private volatile bool _suspended;

    /// <summary>Raised on each recognised Blurt trigger (down and up), on the UI thread.</summary>
    public event Action<TriggerEvent>? TriggerObserved;

    /// <summary>Installs with the design-default AltGr bindings.</summary>
    public KeyboardHook() : this(new TriggerResolver())
    {
    }

    /// <summary>
    /// Installs with a specific <see cref="TriggerResolver"/>, so a settings change
    /// can re-create the hook with the user's remapped hotkeys: dispose the old
    /// hook and install a new one built from the updated bindings.
    /// </summary>
    public KeyboardHook(TriggerResolver resolver)
    {
        _resolver = resolver;
        _proc = HookCallback;
    }

    /// <summary>
    /// Suspends trigger handling while a configuration window is open (issue 20):
    /// the hook stays installed but the callback passes every keystroke through, so
    /// trigger characters can be typed into the settings fields and no dictation
    /// fires behind the window. Pair each call with <see cref="Resume"/> on close.
    /// </summary>
    public void Suspend() => _suspended = true;

    /// <summary>Re-enables trigger handling after a <see cref="Suspend"/>.</summary>
    public void Resume() => _suspended = false;

    public void Install()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        using var module = Process.GetCurrentProcess().MainModule!;
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install low-level keyboard hook.");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Suspended (a config window is open): forward every event untouched so the
        // trigger characters reach the focused field and nothing is swallowed.
        if (nCode >= 0 && !_suspended)
        {
            var message = (int)wParam;
            KeyEdge? edge = message switch
            {
                WM_KEYDOWN or WM_SYSKEYDOWN => KeyEdge.Down,
                WM_KEYUP or WM_SYSKEYUP => KeyEdge.Up,
                _ => null,
            };

            if (edge is { } keyEdge)
            {
                try
                {
                    var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                    var decision = _resolver.Process(new KeyInput((int)data.vkCode, keyEdge));

                    if (decision.Trigger is { } trigger)
                    {
                        TriggerObserved?.Invoke(trigger);
                    }

                    if (decision.Swallow)
                    {
                        return 1;   // non-zero return blocks the keystroke from the rest of the system
                    }
                }
                catch
                {
                    // Last-resort safety net: a low-level hook callback runs in a context
                    // where an escaping exception tears down the whole process (it bypasses
                    // WinForms' ThreadException). Diagnosable failures are caught and
                    // surfaced upstream (TrayApplicationContext.OnTriggerObserved); anything
                    // that still reaches here is swallowed so the app survives, and the key
                    // is let through (fall to CallNextHookEx) rather than silently eaten.
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
