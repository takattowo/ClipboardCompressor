namespace ClipboardCompressor;

public sealed class TrayManager : IDisposable
{
    private readonly MainForm _main;
    private readonly NotifyIcon _trayIcon;
    private ContextMenuStrip _menu = null!;

    public TrayManager(MainForm main)
    {
        _main = main;
        _trayIcon = new NotifyIcon
        {
            Text = "Clipboard Compressor",
            Visible = false
        };
    }

    public void Initialize()
    {
        _trayIcon.Icon = CreateTrayIcon();
        RebuildMenu();
        _trayIcon.ContextMenuStrip = _menu;
        _trayIcon.DoubleClick += (_, _) => OpenSettings();
        _trayIcon.Visible = true;
    }

    public void UpdateMenu() => RebuildMenu();

    private void RebuildMenu()
    {
        _menu?.Dispose();
        _menu = new ContextMenuStrip();
        var s = _main.Settings;

        var header = new ToolStripLabel("Clipboard Compressor")
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.Gray
        };
        _menu.Items.Add(header);
        _menu.Items.Add(new ToolStripSeparator());

        var enableItem = new ToolStripMenuItem("Enabled", null, (_, _) =>
        {
            s.Enabled = !s.Enabled;
            s.Save();
            RebuildMenu();
        }) { Checked = s.Enabled };
        _menu.Items.Add(enableItem);

        _menu.Items.Add(new ToolStripSeparator());

        var modeMenu  = new ToolStripMenuItem("Mode");
        var autoItem  = new ToolStripMenuItem("Auto - always compress", null, (_, _) =>
        {
            s.Mode = CompressorMode.Auto;
            s.Save();
            RebuildMenu();
        }) { Checked = s.Mode == CompressorMode.Auto, CheckOnClick = false };

        var manualItem = new ToolStripMenuItem("Manual - ask on Ctrl+V / right-click", null, (_, _) =>
        {
            s.Mode = CompressorMode.Manual;
            s.Save();
            RebuildMenu();
        }) { Checked = s.Mode == CompressorMode.Manual, CheckOnClick = false };

        modeMenu.DropDownItems.Add(autoItem);
        modeMenu.DropDownItems.Add(manualItem);
        _menu.Items.Add(modeMenu);

        var fmtMenu = new ToolStripMenuItem($"Format: {(s.Format == CompressionFormat.Png ? "PNG" : "JPEG")}");
        foreach (var (label, fmt) in new[] { ("PNG", CompressionFormat.Png), ("JPEG", CompressionFormat.Jpeg) })
        {
            var captured = fmt; var captLabel = label;
            fmtMenu.DropDownItems.Add(new ToolStripMenuItem(captLabel, null, (_, _) =>
            {
                s.Format = captured; s.Save(); RebuildMenu();
            }) { Checked = s.Format == fmt });
        }
        _menu.Items.Add(fmtMenu);

        if (s.Format == CompressionFormat.Jpeg)
        {
            var qualityMenu = new ToolStripMenuItem($"JPEG Quality: {s.JpegQuality}%");
            foreach (int q in new[] { 20, 40, 60, 70, 80, 85, 90, 95 })
            {
                int captured = q;
                qualityMenu.DropDownItems.Add(new ToolStripMenuItem($"{q}%", null, (_, _) =>
                {
                    s.JpegQuality = captured; s.Save(); RebuildMenu();
                }) { Checked = s.JpegQuality == q });
            }
            _menu.Items.Add(qualityMenu);
        }

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Settings...", null, (_, _) => OpenSettings()));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => Application.Exit()));

        _trayIcon.ContextMenuStrip = _menu;
    }

    public void OpenSettings()
    {
        using var form = new SettingsForm(_main.Settings, _main.ReloadSettings);
        form.ShowDialog();
        _main.ReloadSettings();
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon, int ms)
    {
        _trayIcon.ShowBalloonTip(ms, title, text, icon);
    }

    private static Icon CreateTrayIcon()
    {
        const int S = 64;
        var bmp = new Bitmap(S, S, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode   = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // ── Blue rounded background ──────────────────────────────────────────
        using var bgPath  = RoundedRect(new RectangleF(1, 1, 62, 62), 13);
        using var bgBrush = new SolidBrush(Color.FromArgb(0, 120, 212));
        g.FillPath(bgBrush, bgPath);

        // ── Sun ──────────────────────────────────────────────────────────────
        using var wb = new SolidBrush(Color.White);
        g.FillEllipse(wb, 9, 9, 15, 15);

        // ── Mountain silhouette ──────────────────────────────────────────────
        // Back range (semi-transparent white)
        using var dimWb = new SolidBrush(Color.FromArgb(160, 255, 255, 255));
        g.FillPolygon(dimWb, new PointF[] { new(2,62), new(24,30), new(40,48), new(62,62) });
        // Front peak (solid white)
        g.FillPolygon(wb, new PointF[] { new(28,62), new(46,28), new(62,50), new(62,62) });

        // ── Compress badge (bottom-left: darker circle + down-arrow) ─────────
        using var badgeBrush = new SolidBrush(Color.FromArgb(0, 72, 153));
        g.FillEllipse(badgeBrush, 3, 39, 24, 24);
        // Arrow stem
        using var aPen = new Pen(Color.White, 3.5f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap   = System.Drawing.Drawing2D.LineCap.Round
        };
        g.DrawLine(aPen, 15f, 43f, 15f, 54f);
        // Chevron arrowhead
        g.DrawLine(aPen,  9f, 50f, 15f, 57f);
        g.DrawLine(aPen, 21f, 50f, 15f, 57f);

        var small = new Bitmap(bmp, 16, 16);
        var icon  = Icon.FromHandle(small.GetHicon());
        bmp.Dispose();
        small.Dispose();
        return icon;
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(r.X,         r.Y,          d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
        path.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _menu?.Dispose();
    }
}
