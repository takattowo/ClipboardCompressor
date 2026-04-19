using System.Diagnostics;
using System.Runtime.InteropServices;
using static ClipboardCompressor.NativeMethods;

namespace ClipboardCompressor;

public class KeyboardHookEventArgs : EventArgs
{
    public Keys Key { get; }
    public bool Control { get; }
    public bool Shift { get; }
    public bool Alt { get; }
    public bool Suppress { get; set; }

    public KeyboardHookEventArgs(uint vk, bool ctrl, bool shift, bool alt)
    {
        Key = (Keys)vk;
        Control = ctrl;
        Shift = shift;
        Alt = alt;
    }
}

public sealed class GlobalKeyboardHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly HookProc _proc;

    public event EventHandler<KeyboardHookEventArgs>? KeyDown;

    public GlobalKeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION &&
            (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // Skip keystrokes injected by us
            if (hookStruct.dwExtraInfo == INJECTED_SENTINEL)
                return CallNextHookEx(_hookId, nCode, wParam, lParam);

            bool ctrl = (Control.ModifierKeys & Keys.Control) != 0;
            bool shift = (Control.ModifierKeys & Keys.Shift) != 0;
            bool alt = (Control.ModifierKeys & Keys.Alt) != 0;

            var args = new KeyboardHookEventArgs(hookStruct.vkCode, ctrl, shift, alt);
            KeyDown?.Invoke(this, args);

            if (args.Suppress)
                return (IntPtr)1;
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
