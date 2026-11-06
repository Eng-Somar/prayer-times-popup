using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

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
            try { SetProcessDPIAware(); } catch { }
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp(Array.IndexOf(args, "--preview") >= 0, Array.IndexOf(args, "--settings") >= 0));
            GC.KeepAlive(mutex);
        }
    }
}
