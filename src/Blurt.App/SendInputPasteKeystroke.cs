using System.Runtime.InteropServices;
using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// Simulates Ctrl+V via Win32 <c>SendInput</c> so the focused app pastes the
/// clipboard at its own caret — Blurt never has to know where the caret is.
/// All four events go in a single <c>SendInput</c> call so no real keystroke
/// can interleave with the chord. (AltGr still being physically held at this
/// point is a known follow-up concern, out of scope here.)
///
/// Verified manually on Windows; decision logic lives in <see cref="TextInjector"/>.
/// </summary>
internal sealed class SendInputPasteKeystroke : IPasteKeystroke
{
    private const int InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;

    public bool SendPaste()
    {
        var inputs = new[]
        {
            KeyInput(VkControl, up: false),
            KeyInput(VkV, up: false),
            KeyInput(VkV, up: true),
            KeyInput(VkControl, up: true),
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        return sent == inputs.Length;   // partial delivery counts as failure
    }

    private static Input KeyInput(ushort virtualKey, bool up) => new()
    {
        type = InputKeyboard,
        union = new InputUnion
        {
            ki = new KeyboardInput
            {
                wVk = virtualKey,
                dwFlags = up ? KeyEventFKeyUp : 0,
            },
        },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int type;
        public InputUnion union;
    }

    // Explicit layout because INPUT is a C union of keyboard/mouse/hardware
    // payloads; the struct must be union-sized for SendInput to accept it.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput mi;
        [FieldOffset(0)] public KeyboardInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);
}
