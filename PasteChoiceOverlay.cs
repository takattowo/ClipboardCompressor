namespace ClipboardCompressor;

/// <summary>
/// Small borderless popup shown when Ctrl+V is pressed in Manual mode with an image in clipboard.
/// Lets the user choose "Paste Original" or "Paste Compressed".
/// </summary>
public sealed class PasteChoiceOverlay : Form
{
    public enum Choice { None, Original, Compressed }

    private Choice _result = Choice.None;
    private readonly System.Windows.Forms.Timer _dismissTimer;

    public Choice Result => _result;

    public PasteChoiceOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(45, 45, 48);
        Padding = new Padding(6);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };

        var btnOriginal = MakeButton("Paste Original", Color.FromArgb(70, 70, 75));
        var btnCompressed = MakeButton("Paste Compressed \u26a1", Color.FromArgb(0, 122, 204));

        btnOriginal.Click += (_, _) => { _result = Choice.Original; Close(); };
        btnCompressed.Click += (_, _) => { _result = Choice.Compressed; Close(); };

        layout.Controls.Add(btnOriginal);
        layout.Controls.Add(btnCompressed);
        Controls.Add(layout);

        // Auto-dismiss after 4 seconds with no interaction → treat as Original
        _dismissTimer = new System.Windows.Forms.Timer { Interval = 4000 };
        _dismissTimer.Tick += (_, _) => { _result = Choice.Original; Close(); };

        Deactivate += (_, _) => { if (_result == Choice.None) { _result = Choice.Original; Close(); } };
    }

    private static Button MakeButton(string text, Color backColor)
    {
        return new Button
        {
            Text = text,
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Padding = new Padding(8, 4, 8, 4),
            AutoSize = true,
            Cursor = Cursors.Hand,
            Margin = new Padding(4),
            FlatAppearance = { BorderSize = 0 }
        };
    }

    public void ShowNearCursor()
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor).WorkingArea;

        // Force layout so we know the size
        PerformLayout();

        int x = cursor.X + 10;
        int y = cursor.Y - Height - 10;
        if (y < screen.Top) y = cursor.Y + 20;
        if (x + Width > screen.Right) x = screen.Right - Width - 4;

        Location = new Point(x, y);
        Show();
        Activate();
        _dismissTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _dismissTimer.Stop();
        _dismissTimer.Dispose();
        base.OnFormClosed(e);
    }

    protected override bool ShowWithoutActivation => false;
}
