using System.Runtime.InteropServices;

namespace Formater.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length > 0 && args[0].Equals("--export", StringComparison.OrdinalIgnoreCase))
        {
            AttachConsole(-1); // print messages to the command prompt that started us
            return HeadlessExport.Run(args);
        }

        if (!PdfEngine.RuntimeInstalled())
        {
            var answer = MessageBox.Show(
                "Formater needs the Microsoft Edge WebView2 Runtime, which is part of Windows 10/11 " +
                "but is missing on this PC.\n\nOpen the download page now?",
                "Formater", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer == DialogResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(PdfEngine.RuntimeDownloadUrl)
                {
                    UseShellExecute = true,
                });
            return 1;
        }

        Application.Run(new MainForm());
        return 0;
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}
