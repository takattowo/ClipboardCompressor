using System.Runtime.InteropServices;

namespace ClipboardCompressor;

internal static class NativeMethods
{
    // Clipboard
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public const int WM_CLIPBOARDUPDATE = 0x031D;

    // Hooks
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    public const int WH_KEYBOARD_LL = 13;
    public const int HC_ACTION = 0;

    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    // SendInput
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_V = 0x56;

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public INPUTUNION union;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;
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
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // Window focus
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    // Extra info sentinel to prevent hook re-entry
    public static readonly IntPtr INJECTED_SENTINEL = new IntPtr(unchecked((int)0xCCBB1122));

    // ── Raw clipboard access (bypasses WinForms BinaryFormatter) ──────────────

    [DllImport("user32.dll")] public static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")] public static extern bool CloseClipboard();
    [DllImport("user32.dll")] public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll")] public static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterClipboardFormat(string lpszFormat);
    [DllImport("kernel32.dll")] public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll")] public static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] public static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")] public static extern IntPtr GlobalFree(IntPtr hMem);

    private const uint GMEM_MOVEABLE = 0x0002;
    private const string OurMarkerFormat = "ClipboardCompressor_v1";

    // Returns true when the clipboard image was already compressed by us.
    public static bool ClipboardHasOurMarker()
    {
        uint fmt = RegisterClipboardFormat(OurMarkerFormat);
        return IsClipboardFormatAvailable(fmt);
    }

    // Adds raw PNG bytes to the clipboard under the "PNG" format AND stamps our marker,
    // both in one OpenClipboard/CloseClipboard session so no extra WM_CLIPBOARDUPDATE fires.
    public static void AddRawClipboardData(string formatName, byte[] data)
    {
        uint fmt = RegisterClipboardFormat(formatName);
        IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)data.Length);
        if (hMem == IntPtr.Zero) return;
        IntPtr ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero) { GlobalFree(hMem); return; }
        Marshal.Copy(data, 0, ptr, data.Length);
        GlobalUnlock(hMem);

        // Marker: 1-byte block so IsClipboardFormatAvailable returns true
        uint markerFmt = RegisterClipboardFormat(OurMarkerFormat);
        IntPtr hMarker = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)1);
        if (hMarker != IntPtr.Zero)
        {
            IntPtr mp = GlobalLock(hMarker);
            if (mp != IntPtr.Zero) { Marshal.WriteByte(mp, 0); GlobalUnlock(hMarker); }
            else { GlobalFree(hMarker); hMarker = IntPtr.Zero; }
        }

        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                SetClipboardData(fmt, hMem);
                if (hMarker != IntPtr.Zero) SetClipboardData(markerFmt, hMarker);
                CloseClipboard();
                return;
            }
            Thread.Sleep(15);
        }
        GlobalFree(hMem);
        if (hMarker != IntPtr.Zero) GlobalFree(hMarker);
    }
}
