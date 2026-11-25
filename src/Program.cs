using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Prayer Times Popup")]
[assembly: AssemblyProduct("Prayer Times Popup")]
[assembly: AssemblyVersion(UpdateCheck.Version)]
[assembly: AssemblyFileVersion(UpdateCheck.Version)]

static class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string name);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);

    // Broadcast by a second launch so the running instance opens its settings window.
    public static readonly int OpenSettingsMsg = RegisterWindowMessage("PrayerTimesPopup_OpenSettings");

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--export-icon") { IconArt.SaveIco(args[1]); return; }

        bool created;
        using (var mutex = new Mutex(true, "PrayerTimesPopup_SingleInstance_8f2c", out created))
        {
            if (!created)
            {
                PostMessage((IntPtr)0xFFFF, OpenSettingsMsg, IntPtr.Zero, IntPtr.Zero);
                return;
            }
            // A reminder app should keep running: log unexpected errors instead of showing a crash dialog.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (o, e) => ErrorLog.Write(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (o, e) => ErrorLog.Write(e.ExceptionObject as Exception);

            try { SetProcessDPIAware(); } catch (EntryPointNotFoundException) { }
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp(args));
            GC.KeepAlive(mutex);
        }
    }
}

public static class ErrorLog
{
    const long MaxBytes = 64 * 1024;

    public static void Write(Exception ex)
    {
        try
        {
            string path = Path.Combine(Settings.Dir, "errors.log");
            Directory.CreateDirectory(Settings.Dir);
            if (File.Exists(path) && new FileInfo(path).Length > MaxBytes) File.Delete(path);
            File.AppendAllText(path, DateTime.Now.ToString("s") + " " + ex + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
