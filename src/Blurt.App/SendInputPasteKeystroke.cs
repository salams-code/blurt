using System.Runtime.InteropServices;
using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// Simulates Ctrl+V via Win32 <c>SendInput</c> so the focused app pastes the
/// clipboard at its own caret — Blurt never has to know where the caret is.
/// The chord composition (including releasing an AltGr the user still holds,
/// which would otherwise turn the paste into Ctrl+Alt+V) lives unit-tested in
/// <see cref="PasteChord"/>; all events go in a single <c>SendInput</c> call
/// so no real keystroke can interleave with the chord.
///
/// Verified manually on Windows; decision logic lives in <see cref="TextInjector"/>.
/// </summary>
internal sealed class SendInputPasteKeystroke : IPasteKeystroke
{
    private const int InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;

    // Only the Alt keys: they are what the trigger gesture leaves physically
    // held (AltGr = right Alt), and an Alt-up for a key that is not down is a
    // no-op, so probing+releasing is safe.
    private static readonly int[] CorruptingModifiers = [VkLMenu, VkRMenu];

    public bool SendPaste()
    {
        var held = CorruptingModifiers.Where(vk => (GetAsyncKeyState(vk) & 0x8000) != 0).ToArray();

        var inputs = PasteChord.Build(held)
            .Select(key => new Input
            {
                type = InputKeyboard,
                union = new InputUnion
                {
                    ki = new KeyboardInput
                    {
                        wVk = (ushort)key.VirtualKeyCode,
                        dwFlags = key.Edge == KeyEdge.Up ? KeyEventFKeyUp : 0,
                    },
                },
            })
            .ToArray();

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        return sent == inputs.Length;   // partial delivery counts as failure
    }

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

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
