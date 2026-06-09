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

    private readonly TriggerResolver _resolver = new();
    private readonly LowLevelKeyboardProc _proc;   // held in a field so the GC never collects the callback
    private IntPtr _hookHandle;

    /// <summary>Raised on each recognised Blurt trigger (down and up), on the UI thread.</summary>
    public event Action<TriggerEvent>? TriggerObserved;

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

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
        if (nCode >= 0)
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
