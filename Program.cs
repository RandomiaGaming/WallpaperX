using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using Microsoft.Win32;
using Microsoft.VisualBasic;

namespace WallpaperX
{
    public static class PInvoke
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        public const uint WM_SWITCH_TO_LAYERED_WALLPAPER_MODE = 0x052C;
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint wMsg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        public const int SPI_SETDESKWALLPAPER = 0x0014;
        public const int SPIF_SENDCHANGE = 0x02;
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        public const uint GW_HWNDNEXT = 2;
        public const uint GW_HWNDPREV = 3;
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    }
    public partial class WallpaperXForm : Form
    {
        private ChromiumWebBrowser browser = null;
        private NotifyIcon trayIcon = null;
        private IntPtr wallpaperWorkerW = IntPtr.Zero;
        public WallpaperXForm()
        {
            // Init wallpaper window layering
            IntPtr progman = PInvoke.FindWindow("Progman", null);
            PInvoke.SendMessage(progman, PInvoke.WM_SWITCH_TO_LAYERED_WALLPAPER_MODE, IntPtr.Zero, IntPtr.Zero);
            IntPtr iconsWorkerW = IntPtr.Zero;
            PInvoke.EnumWindows((enumHwnd, lParam) =>
            {
                IntPtr shellDllDefView = PInvoke.FindWindowEx(enumHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellDllDefView != IntPtr.Zero)
                {
                    iconsWorkerW = enumHwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            if (iconsWorkerW == IntPtr.Zero)
            {
                throw new Exception("IconWorkerW could not be found. Try restarting your PC.");
            }
            wallpaperWorkerW = PInvoke.GetWindow(iconsWorkerW, PInvoke.GW_HWNDNEXT);
            if (wallpaperWorkerW != PInvoke.GetWindow(progman, PInvoke.GW_HWNDPREV) || wallpaperWorkerW == IntPtr.Zero)
            {
                throw new Exception("WallpaperWorkerW could not be found. Try restarting your PC.");
            }
            PInvoke.SetParent(this.Handle, wallpaperWorkerW);

            // Init window
            this.Text = "WallpaperX Renderrer";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            PInvoke.RECT clientRect;
            PInvoke.GetClientRect(wallpaperWorkerW, out clientRect);
            this.Location = new Point(clientRect.Left, clientRect.Top);
            this.Size = new Size(clientRect.Right - clientRect.Left, clientRect.Bottom - clientRect.Top);

            // Init systray icon
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open Devtools", null, (object sender, EventArgs e) =>
            {
                if (browser.IsBrowserInitialized)
                {
                    browser.GetBrowser().ShowDevTools();
                }
            });
            trayMenu.Items.Add("Set Wallpaper Url", null, (object sender, EventArgs e) =>
            {
                if (!File.Exists("wallpaper_url.txt"))
                {
                    File.WriteAllText("wallpaper_url.txt", "file:///" + Path.GetFullPath("./example/wallpaper.html").Replace("\\", "/"));
                }
                string url = File.ReadAllText("wallpaper_url.txt");
                string newUrl = Interaction.InputBox("Enter new wallpaper url:", "", url);
                if (newUrl != null && newUrl != "")
                {
                    File.WriteAllText("wallpaper_url.txt", newUrl);
                    this.ReloadWallpaper();
                }
            });
            trayMenu.Items.Add("Reload", null, (object sender, EventArgs e) =>
            {
                this.ReloadWallpaper();
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

            // Init chromium browser
            CefSettings settings = new CefSettings();
            settings.CefCommandLineArgs.Add("autoplay-policy", "no-user-gesture-required");
            Cef.Initialize(settings);
            browser = new ChromiumWebBrowser()
            {
                Dock = DockStyle.Fill
            };
            this.Controls.Add(browser);

            ReloadWallpaper();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            PInvoke.SetParent(this.Handle, IntPtr.Zero);
            string originalWallpaper = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", "");
            PInvoke.SystemParametersInfo(PInvoke.SPI_SETDESKWALLPAPER, 0, originalWallpaper, PInvoke.SPIF_SENDCHANGE);

            Cef.Shutdown();

            trayIcon.Visible = false;
            trayIcon.Dispose();

            base.OnFormClosing(e);
        }
        public void ReloadWallpaper()
        {
            if (!File.Exists("wallpaper_url.txt"))
            {
                File.WriteAllText("wallpaper_url.txt", "file:///" + Path.GetFullPath("./example/wallpaper.html").Replace("\\", "/"));
            }
            string url = File.ReadAllText("wallpaper_url.txt");
            if (browser.IsBrowserInitialized)
            {
                browser.Load(url);
            }
            else
            {
                browser.IsBrowserInitializedChanged += (object sender, EventArgs e) =>
                {
                    browser.Load(url);
                };
            }
        }
    }
    public static class Program
    {
        public static void Main()
        {
            try
            {
                Application.Run(new WallpaperXForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "WallpaperX - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}