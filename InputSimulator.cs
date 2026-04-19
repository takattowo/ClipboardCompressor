using System.Runtime.InteropServices;
using static ClipboardCompressor.NativeMethods;

namespace ClipboardCompressor;

/// <summary>Simulates keyboard input without triggering our own hooks.</summary>
public static class InputSimulator
{
    public static void SendCtrlV()
    {
        var inputs = new INPUT[]
        {
            MakeKey(VK_CONTROL, down: true),
            MakeKey(VK_V,       down: true),
            MakeKey(VK_V,       down: false),
            MakeKey(VK_CONTROL, down: false),
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeKey(ushort vk, bool down)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            union = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = down ? 0u : KEYEVENTF_KEYUP,
                    dwExtraInfo = INJECTED_SENTINEL
                }
            }
        };
    }
}
