using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

public static class Program
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    private static IntPtr GetWorkerW()
    {
        IntPtr workerW = IntPtr.Zero;
        EnumWindows((topHandle, lParam) =>
        {
            IntPtr shellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                workerW = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (workerW == IntPtr.Zero)
        {
            IntPtr progman = FindWindow("Progman", null);
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

            EnumWindows((topHandle, lParam) =>
            {
                IntPtr shellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    workerW = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                    return false;
                }
                return true;
            }, IntPtr.Zero);
        }

        if (workerW == IntPtr.Zero)
        {
            foreach (var proc in Process.GetProcessesByName("explorer"))
            {
                try { proc.Kill(); } catch { }
            }
            Thread.Sleep(1000);

            IntPtr progman = FindWindow("Progman", null);
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

            EnumWindows((topHandle, lParam) =>
            {
                IntPtr shellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    workerW = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                    return false;
                }
                return true;
            }, IntPtr.Zero);
        }

        if (workerW == IntPtr.Zero)
        {
            throw new Exception("WorkerW is cooked :(");
        }
        return workerW;
    }

    public partial class WallpaperForm : Form
    {
        private ChromiumWebBrowser browser;
        private NotifyIcon trayIcon;
        public WallpaperForm(string url)
        {
            CefSettings settings = new CefSettings();
            settings.CefCommandLineArgs.Add("autoplay-policy", "no-user-gesture-required");
            Cef.Initialize(settings);

            browser = new ChromiumWebBrowser()
            {
                Dock = DockStyle.Fill
            };
            this.Controls.Add(browser);

            browser.Load(url);

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open Devtools", null, (object sender, EventArgs e) =>
            {
                if (browser.IsBrowserInitialized)
                {
                    browser.GetBrowser().ShowDevTools();
                }
            });
            trayMenu.Items.Add("Reload", null, (object sender, EventArgs e) =>
            {
                if (browser.IsBrowserInitialized)
                {
                    browser.Load(url);
                }
            });
            trayMenu.Items.Add("Quit", null, (object sender, EventArgs e) =>
            {
                this.Close();
            });

            trayIcon = new NotifyIcon
            {
                Text = "WallpaperX",
                Icon = SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Visible = true
            };
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Cef.Shutdown();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            base.OnFormClosing(e);
        }
    }

    public static void Main()
    {
        if (!File.Exists("wallpaper_url.txt"))
        {
            File.WriteAllText("wallpaper_url.txt", Path.GetFullPath("./example/wallpaper.html"));
        }

        string wallpaperUrl = File.ReadAllText("wallpaper_url.txt");

        IntPtr workerW = GetWorkerW();

        WallpaperForm wallpaperForm = new WallpaperForm(wallpaperUrl)
        {
            FormBorderStyle = FormBorderStyle.None,
            Width = 1920,
            Height = 1080,
        };

        SetParent(wallpaperForm.Handle, workerW);

        Application.Run(wallpaperForm);
    }
}