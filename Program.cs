using System.Threading;

namespace ClipboardCompressor;

static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        _mutex = new Mutex(true, "ClipboardCompressor_SingleInstance_v1", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Clipboard Compressor is already running.\nCheck the system tray.",
                "Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var mainForm = new MainForm();
        Application.Run(mainForm);

        _mutex.ReleaseMutex();
    }
}
