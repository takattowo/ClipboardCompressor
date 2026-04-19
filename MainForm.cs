using static ClipboardCompressor.NativeMethods;

namespace ClipboardCompressor;

public sealed class MainForm : Form
{
    public AppSettings Settings { get; private set; }

    private readonly GlobalKeyboardHook _keyHook = new();
    private readonly TrayManager _tray;
    private bool _settingClipboard;
    private volatile bool _pasteInProgress;

    public MainForm()
    {
        Settings = AppSettings.Load();
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.None;
        Opacity = 0;
        Size = new Size(1, 1);
        _tray = new TrayManager(this);
        _keyHook.KeyDown += OnKeyDown;
        _keyHook.Install();
    }

    protected override void SetVisibleCore(bool value)
    {
        if (!IsHandleCreated) { CreateHandle(); OnLoad(EventArgs.Empty); }
        base.SetVisibleCore(false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        AddClipboardFormatListener(Handle);
        _tray.Initialize();
        BeginInvoke(_tray.OpenSettings);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE) OnClipboardUpdated();
        base.WndProc(ref m);
    }

    // ── Clipboard listener ──────────────────────────────────────────────────

    private void OnClipboardUpdated()
    {
        if (_settingClipboard || !Settings.Enabled) return;
        if (!ClipboardHasImage()) return;
        if (NativeMethods.ClipboardHasOurMarker()) return; // already compressed by us
        if (Settings.Mode == CompressorMode.Auto)
            ReplaceClipboardWithCompressed();
    }

    // ── Auto mode ──────────────────────────────────────────────────────────

    private void ReplaceClipboardWithCompressed()
    {
        try
        {
            var original = Clipboard.GetImage();
            if (original == null) return;

            var result = ImageCompressor.Compress(original,
                Settings.Format, Settings.JpegQuality, Settings.PreserveTransparencyAsPng);
            original.Dispose();

            _settingClipboard = true;
            try { SetCompressedToClipboard(result.Compressed, result.PngBytes); }
            finally { _settingClipboard = false; }

            result.Compressed.Dispose();

            if (Settings.ShowTrayNotifications)
            {
                _tray.ShowBalloon("Clipboard Compressor",
                    $"Compressed: {ImageCompressor.FormatBytes(result.OriginalBytes)} → {ImageCompressor.FormatBytes(result.CompressedBytes)}",
                    ToolTipIcon.None, 1500);
            }
        }
        catch { }
    }

    // ── Manual mode ────────────────────────────────────────────────────────

    private void OnKeyDown(object? sender, KeyboardHookEventArgs e)
    {
        if (!Settings.Enabled || Settings.Mode != CompressorMode.Manual) return;
        if (!e.Control || e.Key != Keys.V) return;
        if (!ClipboardHasImage()) return;
        // Already compressed or another paste in progress — let the key through normally
        if (NativeMethods.ClipboardHasOurMarker() || _pasteInProgress) return;
        e.Suppress = true;
        _pasteInProgress = true;
        var targetWindow = GetForegroundWindow();
        BeginInvoke(() => ShowPasteChoiceOverlay(targetWindow));
    }

    private void ShowPasteChoiceOverlay(IntPtr targetWindow)
    {
        var overlay = new PasteChoiceOverlay();
        overlay.FormClosed += (_, _) =>
        {
            _pasteInProgress = false;
            if (overlay.Result == PasteChoiceOverlay.Choice.Compressed)
                ExecuteCompressedPaste(targetWindow);
            else
                ExecuteOriginalPaste(targetWindow);
        };
        overlay.ShowNearCursor();
    }

    // ── Paste actions ───────────────────────────────────────────────────────

    private void ExecuteCompressedPaste(IntPtr targetWindow)
    {
        try
        {
            var original = Clipboard.GetImage();
            if (original == null) { ExecuteOriginalPaste(targetWindow); return; }

            var result = ImageCompressor.Compress(original,
                Settings.Format, Settings.JpegQuality, Settings.PreserveTransparencyAsPng);
            original.Dispose();

            _settingClipboard = true;
            try { SetCompressedToClipboard(result.Compressed, result.PngBytes); }
            finally { _settingClipboard = false; }

            FocusAndPaste(targetWindow);

            var compressed = result.Compressed;
            Task.Delay(400).ContinueWith(_ => BeginInvoke(() => compressed.Dispose()));

            if (Settings.ShowTrayNotifications)
            {
                _tray.ShowBalloon("Paste Compressed",
                    $"{ImageCompressor.FormatBytes(result.CompressedBytes)} (was {ImageCompressor.FormatBytes(result.OriginalBytes)})",
                    ToolTipIcon.None, 1200);
            }
        }
        catch { ExecuteOriginalPaste(targetWindow); }
    }

    private static void ExecuteOriginalPaste(IntPtr targetWindow) => FocusAndPaste(targetWindow);

    private static void FocusAndPaste(IntPtr targetWindow)
    {
        if (targetWindow != IntPtr.Zero && IsWindow(targetWindow))
            SetForegroundWindow(targetWindow);
        InputSimulator.SendCtrlV();
    }

    // ── Clipboard write ─────────────────────────────────────────────────────

    // Two-step write:
    //   1. WinForms SetImage  → registers CF_DIB / CF_BITMAP (for apps that need raw bitmap)
    //   2. Win32 raw write    → registers "PNG" format with actual PNG bytes
    //      WinForms DataObject.SetData() uses BinaryFormatter which makes data unreadable
    //      by non-.NET apps (Electron/Discord). Win32 SetClipboardData writes raw bytes.
    private static void SetCompressedToClipboard(Bitmap bitmap, byte[] pngBytes)
    {
        Clipboard.SetImage(bitmap);
        NativeMethods.AddRawClipboardData("PNG", pngBytes);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static bool ClipboardHasImage()
    {
        try { return Clipboard.ContainsImage(); }
        catch { return false; }
    }

    public void ReloadSettings()
    {
        Settings = AppSettings.Load();
        _tray.UpdateMenu();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        RemoveClipboardFormatListener(Handle);
        _keyHook.Dispose();
        _tray.Dispose();
        base.OnFormClosing(e);
    }
}
