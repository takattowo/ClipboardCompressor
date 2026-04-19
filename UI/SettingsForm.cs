namespace ClipboardCompressor;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Action? _onApply;

    private readonly RadioButton _rbAuto;
    private readonly RadioButton _rbManual;
    private readonly RadioButton _rbPng;
    private readonly RadioButton _rbJpeg;
    private readonly TrackBar _tbQuality;
    private readonly Label _lblQualityValue;
    private readonly Panel _qualityRow;
    private readonly CheckBox _cbPreserveAlpha;
    private readonly CheckBox _cbNotifications;
    private readonly CheckBox _cbStartWithWindows;

    public SettingsForm(AppSettings settings, Action? onApply = null)
    {
        _settings = settings;
        _onApply  = onApply;

        Text = "Clipboard Compressor - Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(400, 460);
        Font = new Font("Segoe UI", 9f);

        int y = 16;

        // ── Mode ────────────────────────────────────────────────────────────
        AddLabel("Mode", 14, ref y);
        var pnlMode = new Panel { Left = 0, Top = y, Width = ClientSize.Width, Height = 52 };
        int py = 0;
        _rbAuto   = AddRadio(pnlMode, "Auto - compress automatically on copy", ref py, settings.Mode == CompressorMode.Auto);
        _rbManual = AddRadio(pnlMode, "Manual - ask on each Ctrl+V",           ref py, settings.Mode == CompressorMode.Manual);
        Controls.Add(pnlMode);
        y += 52 + 8;

        // ── Format ──────────────────────────────────────────────────────────
        AddLabel("Format", 14, ref y);
        var pnlFormat = new Panel { Left = 0, Top = y, Width = ClientSize.Width, Height = 52 };
        py = 0;
        _rbPng  = AddRadio(pnlFormat, "PNG - lossless quality, smaller file via colour optimisation", ref py, settings.Format == CompressionFormat.Png);
        _rbJpeg = AddRadio(pnlFormat, "JPEG - lossy compression, choose quality below",               ref py, settings.Format == CompressionFormat.Jpeg);
        Controls.Add(pnlFormat);
        y += 52 + 4;

        // ── JPEG Quality (hidden when PNG selected) ──────────────────────────
        _qualityRow = new Panel { Left = 14, Top = y, Width = 370, Height = 30 };
        _tbQuality = new TrackBar
        {
            Minimum = 20, Maximum = 100, Value = settings.JpegQuality,
            TickFrequency = 10, SmallChange = 5, Width = 300, Top = 0, Left = 0
        };
        _lblQualityValue = new Label
        {
            Text = $"{settings.JpegQuality}%",
            Left = 308, Top = 6, Width = 55, Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _tbQuality.ValueChanged += (_, _) => _lblQualityValue.Text = $"{_tbQuality.Value}%";
        _qualityRow.Controls.Add(_tbQuality);
        _qualityRow.Controls.Add(_lblQualityValue);
        Controls.Add(_qualityRow);
        y += 38;

        var lblQuality = new Label
        {
            Text = "JPEG Quality (higher = better quality, larger file)",
            Left = 14, Top = y, Width = 370, AutoSize = true,
            Font = new Font("Segoe UI", 8.5f)
        };
        Controls.Add(lblQuality);
        y += 20;

        void UpdateQualityVisibility()
        {
            bool jpeg = _rbJpeg.Checked;
            _qualityRow.Enabled = jpeg;
            _qualityRow.Visible = jpeg;
            lblQuality.Visible  = jpeg;
        }
        _rbPng.CheckedChanged  += (_, _) => UpdateQualityVisibility();
        _rbJpeg.CheckedChanged += (_, _) => UpdateQualityVisibility();
        UpdateQualityVisibility();

        y += 8;

        // ── Options ──────────────────────────────────────────────────────────
        AddLabel("Options", 14, ref y);
        _cbPreserveAlpha    = AddCheck("Keep transparency (PNG mode only)", ref y, settings.PreserveTransparencyAsPng);
        _cbNotifications    = AddCheck("Show tray notifications",            ref y, settings.ShowTrayNotifications);
        _cbStartWithWindows = AddCheck("Start with Windows",                 ref y, settings.StartWithWindows);

        y += 16;

        // ── Buttons ──────────────────────────────────────────────────────────
        var btnOk = new Button
        {
            Text = "OK", DialogResult = DialogResult.OK,
            Left = ClientSize.Width - 250, Top = ClientSize.Height - 44,
            Width = 75, Height = 28
        };
        var btnApply = new Button
        {
            Text = "Apply",
            Left = ClientSize.Width - 170, Top = ClientSize.Height - 44,
            Width = 75, Height = 28
        };
        var btnCancel = new Button
        {
            Text = "Cancel", DialogResult = DialogResult.Cancel,
            Left = ClientSize.Width - 90, Top = ClientSize.Height - 44,
            Width = 75, Height = 28
        };
        btnApply.Click += (_, _) => ApplySettings();
        btnOk.Click    += (_, _) => ApplySettings();
        Controls.Add(btnApply);
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private void ApplySettings()
    {
        _settings.Mode        = _rbAuto.Checked ? CompressorMode.Auto : CompressorMode.Manual;
        _settings.Format      = _rbPng.Checked  ? CompressionFormat.Png : CompressionFormat.Jpeg;
        _settings.JpegQuality = _tbQuality.Value;
        _settings.PreserveTransparencyAsPng = _cbPreserveAlpha.Checked;
        _settings.ShowTrayNotifications     = _cbNotifications.Checked;
        _settings.StartWithWindows          = _cbStartWithWindows.Checked;
        _settings.Save();
        _onApply?.Invoke();
    }

    private void AddLabel(string text, int left, ref int y)
    {
        Controls.Add(new Label
        {
            Text = text, Left = left, Top = y,
            Width = ClientSize.Width - left * 2, AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        });
        y += 22;
    }

    private RadioButton AddRadio(Panel panel, string text, ref int y, bool check)
    {
        var rb = new RadioButton
        {
            Text = text, Left = 28, Top = y,
            Width = panel.Width - 40, AutoSize = true, Checked = check
        };
        panel.Controls.Add(rb);
        y += 24;
        return rb;
    }

    private CheckBox AddCheck(string text, ref int y, bool check)
    {
        var cb = new CheckBox
        {
            Text = text, Left = 28, Top = y,
            Width = ClientSize.Width - 40, AutoSize = true, Checked = check
        };
        Controls.Add(cb);
        y += 24;
        return cb;
    }
}
