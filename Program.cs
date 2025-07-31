using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace WallpaperX
{
    public static class PInvoke
    {
        public const uint ERROR_SUCCESS = 0;
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void SetLastError(uint dwErrCode);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowExW(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        public const uint WM_SWITCH_TO_LAYERED_WALLPAPER_MODE = 0x052C;
        public const int SMTO_NOTIMEOUTIFNOTHUNG = 0x0008;
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessageTimeoutW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out uint lpdwResult);

        public delegate bool WNDENUMPROC(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumWindows(WNDENUMPROC lpEnumFunc, IntPtr lParam);

        public const uint GW_HWNDNEXT = 2;
        public const uint GW_HWNDPREV = 3;
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public const int MAX_PATH = 260;
        public const uint SPI_GETDESKWALLPAPER = 0x0073;
        public const uint SPI_SETDESKWALLPAPER = 0x0014;
        public const uint SPIF_SENDCHANGE = 0x02;
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string lpName);

        public const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        public const uint JobObjectExtendedLimitInformation = 9;
        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public IntPtr MinimumWorkingSetSize;
            public IntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public IntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public IntPtr ProcessMemoryLimit;
            public IntPtr JobMemoryLimit;
            public IntPtr PeakProcessMemoryUsed;
            public IntPtr PeakJobMemoryUsed;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetInformationJobObject(IntPtr hJob, uint JobObjectInformationClass, IntPtr lpJobObjectInformation, uint cbJobObjectInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    }
    public static class SafeNative
    {
        private static void ClearLastError()
        {
            PInvoke.SetLastError(PInvoke.ERROR_SUCCESS);
        }
        private static void ThrowLastError(bool ignoreZero = false)
        {
            int lastError = Marshal.GetLastWin32Error();
            if (lastError != PInvoke.ERROR_SUCCESS)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            else if (lastError == PInvoke.ERROR_SUCCESS && !ignoreZero)
            {
                throw new Win32Exception((int)PInvoke.ERROR_SUCCESS, "An unknown Win32 error occured.");
            }
        }

        public static IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow, bool throwIfNotFound = false)
        {
            ClearLastError();
            IntPtr output = PInvoke.FindWindowExW(hWndParent, hWndChildAfter, lpszClass, lpszWindow);
            if (output == IntPtr.Zero)
            {
                ThrowLastError(ignoreZero: true);
            }
            if (output == IntPtr.Zero && throwIfNotFound)
            {
                throw new Exception("FindWindowEx could not locate the specified window.");
            }
            return output;
        }
        public static uint SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout)
        {
            ClearLastError();
            if (PInvoke.SendMessageTimeoutW(hWnd, Msg, wParam, lParam, fuFlags, uTimeout, out uint output) == IntPtr.Zero)
            {
                ThrowLastError();
            }
            return output;
        }
        public static void EnumWindows(PInvoke.WNDENUMPROC lpEnumFunc, IntPtr lParam)
        {
            ClearLastError();
            if (!PInvoke.EnumWindows(lpEnumFunc, lParam))
            {
                ThrowLastError(ignoreZero: true);
            }
        }
        public static IntPtr GetWindow(IntPtr hWnd, uint uCmd, bool throwIfNotFound = false)
        {
            ClearLastError();
            IntPtr output = PInvoke.GetWindow(hWnd, uCmd);
            if (output == IntPtr.Zero)
            {
                ThrowLastError(ignoreZero: true);
            }
            if (output == IntPtr.Zero && throwIfNotFound)
            {
                throw new Exception("GetWindow could not locate the specified window.");
            }
            return output;
        }
        public static uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwThreadId)
        {
            uint lpdwProcessId = 0;
            ClearLastError();
            uint output = PInvoke.GetWindowThreadProcessId(hWnd, out lpdwProcessId);
            if (output == 0)
            {
                ThrowLastError();
            }
            lpdwThreadId = output;
            return lpdwProcessId;
        }
        public static void SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni)
        {
            ClearLastError();
            if (!PInvoke.SystemParametersInfoW(uiAction, uiParam, pvParam, fWinIni))
            {
                ThrowLastError();
            }
        }
        public static IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent)
        {
            ClearLastError();
            IntPtr output = PInvoke.SetParent(hWndChild, hWndNewParent);
            if (output == IntPtr.Zero)
            {
                ThrowLastError(ignoreZero: true);
            }
            return output;
        }
        public static IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName)
        {
            ClearLastError();
            IntPtr output = PInvoke.CreateJobObjectW(lpJobAttributes, lpName);
            if (output == IntPtr.Zero)
            {
                ThrowLastError();
            }
            return output;
        }
        public static void SetInformationJobObject(IntPtr hJob, uint JobObjectInformationClass, IntPtr lpJobObjectInformation, uint cbJobObjectInformationLength)
        {
            ClearLastError();
            if (!PInvoke.SetInformationJobObject(hJob, JobObjectInformationClass, lpJobObjectInformation, cbJobObjectInformationLength))
            {
                ThrowLastError();
            }
        }
        public static void AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess)
        {
            ClearLastError();
            if (!PInvoke.AssignProcessToJobObject(hJob, hProcess))
            {
                ThrowLastError();
            }
        }
        public static System.Drawing.Point ScreenToClient(IntPtr hWnd, System.Drawing.Point lpPoint)
        {
            PInvoke.POINT point = new PInvoke.POINT();
            point.X = lpPoint.X;
            point.Y = lpPoint.Y;
            ClearLastError();
            if (!PInvoke.ScreenToClient(hWnd, ref point))
            {
                ThrowLastError();
            }
            return new System.Drawing.Point(point.X, point.Y);
        }
        public static string GetClassName(IntPtr hWnd, int nMaxCount = 1024)
        {
            StringBuilder lpClassName = new StringBuilder(nMaxCount);
            ClearLastError();
            int output = PInvoke.GetClassNameW(hWnd, lpClassName, nMaxCount);
            if (output == 0)
            {
                ThrowLastError();
            }
            return lpClassName.ToString();
        }
    }
    public static class NativeHelpers
    {
        public static string GetWallpaperSystemParameter()
        {
            IntPtr buffer = Marshal.AllocHGlobal(PInvoke.MAX_PATH * sizeof(char));
            try
            {
                SafeNative.SystemParametersInfo(PInvoke.SPI_GETDESKWALLPAPER, PInvoke.MAX_PATH, buffer, 0);
                return Marshal.PtrToStringUni(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        public static void SetWallpaperSystemParameter(string wallpaperPath)
        {
            IntPtr ptr = Marshal.StringToHGlobalUni(wallpaperPath);
            try
            {
                SafeNative.SystemParametersInfo(PInvoke.SPI_SETDESKWALLPAPER, 0, ptr, PInvoke.SPIF_SENDCHANGE);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        public static void SetJobObjectExtendedLimitInformation(IntPtr hJob, PInvoke.JOBOBJECT_EXTENDED_LIMIT_INFORMATION jobObjectExtendedLimitInformation)
        {
            int size = Marshal.SizeOf(typeof(PInvoke.JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(jobObjectExtendedLimitInformation, ptr, false);
            try
            {
                SafeNative.SetInformationJobObject(hJob, PInvoke.JobObjectExtendedLimitInformation, ptr, (uint)size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    public static class Program
    {
        #region Switching to and from layered wallpaper mode
        public static IntPtr SwitchToLayeredWallpaperMode(bool noRestartProgman = false)
        {
            // Locate progman
            IntPtr progman = SafeNative.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null, throwIfNotFound: true);

            try
            {
                // Tell progman to switch to layered wallpaper mode
                SafeNative.SendMessageTimeout(progman, PInvoke.WM_SWITCH_TO_LAYERED_WALLPAPER_MODE, IntPtr.Zero, IntPtr.Zero, PInvoke.SMTO_NOTIMEOUTIFNOTHUNG, 0);

                // Search for IconsWorkerW and WallpaperWorkerW
                IntPtr iconsWorkerW = IntPtr.Zero;
                IntPtr wallpaperWorkerW = IntPtr.Zero;
                PInvoke.WNDENUMPROC iconsWorkerWLocatorProc = (enumhWnd, lParam) =>
                {
                    if (iconsWorkerW == IntPtr.Zero)
                    {
                        // Check if this window is IconsWorkerW
                        try
                        {
                            IntPtr shellDllDefView = SafeNative.FindWindowEx(enumhWnd, IntPtr.Zero, "SHELLDLL_DefView", null, throwIfNotFound: true);
                            IntPtr folderView = SafeNative.FindWindowEx(shellDllDefView, IntPtr.Zero, "SysListView32", null, throwIfNotFound: true);
                            iconsWorkerW = enumhWnd;
                        }
                        catch { }
                    }
                    else
                    {
                        // Check if this window is WallpaperWorkerW
                        try
                        {
                            if (SafeNative.GetClassName(enumhWnd) == "WorkerW") {
                                wallpaperWorkerW = enumhWnd;
                                return false;
                            }
                        }
                        catch { }
                    }
                    return true;
                };
                SafeNative.EnumWindows(iconsWorkerWLocatorProc, IntPtr.Zero);
                if (iconsWorkerW == IntPtr.Zero)
                {
                    throw new Exception("Could not locate IconWorkerW.");
                }
                if (wallpaperWorkerW == IntPtr.Zero)
                {
                    throw new Exception("Could not locate WallpaperWorkerW.");
                }

                // Return if successful so far otherwise restart progman.
                return wallpaperWorkerW;
            }
            catch (Exception ex)
            {
                if (noRestartProgman)
                {
                    throw ex;
                }

                // Get progman's process ID
                uint progmanPid = SafeNative.GetWindowThreadProcessId(progman, out _);

                // Kill the progman process
                Process progmanProc = Process.GetProcessById((int)progmanPid);
                progmanProc.Kill();
                progmanProc.WaitForExit();
                progmanProc.Dispose();

                // Wait for progman to automatically restart
                progman = IntPtr.Zero;
                while (true)
                {
                    progman = SafeNative.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null, throwIfNotFound: false);
                    if (progman != IntPtr.Zero)
                    {
                        break;
                    }
                    Thread.Sleep(5);
                }

                return SwitchToLayeredWallpaperMode(noRestartProgman = true);
            }
        }
        public static void RedrawOriginalWallpaper()
        {
            string originalWallpaper = NativeHelpers.GetWallpaperSystemParameter();
            NativeHelpers.SetWallpaperSystemParameter(originalWallpaper);
        }
        #endregion
        #region Cleaning up child processes automatically when this process crashes
        public static void KillChildOnClose(IntPtr childProcessHandle)
        {
            IntPtr hJob = SafeNative.CreateJobObject(IntPtr.Zero, null);

            PInvoke.JOBOBJECT_EXTENDED_LIMIT_INFORMATION jobInfo = new PInvoke.JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            jobInfo.BasicLimitInformation.LimitFlags = PInvoke.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            NativeHelpers.SetJobObjectExtendedLimitInformation(hJob, jobInfo);

            SafeNative.AssignProcessToJobObject(hJob, childProcessHandle);
        }
        #endregion
        #region Wallpaper X Config Helpers
        public static string GetWallpaperUrl()
        {
            string DefaultWallpaperUrl = "file:///" + Path.GetFullPath(".\\example\\lilguy.html").Replace("\\", "/");
            string WallpaperUrlFilePath = Path.GetFullPath(".\\wallpaperx.conf");

            if (!File.Exists(WallpaperUrlFilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(WallpaperUrlFilePath));
                File.WriteAllText(WallpaperUrlFilePath, DefaultWallpaperUrl);
            }
            return File.ReadAllText(WallpaperUrlFilePath);
        }
        public static void SetWallpaperUrl(string wallpaperUrl)
        {
            string WallpaperUrlFilePath = Path.GetFullPath(".\\wallpaperx.conf");

            if (!File.Exists(WallpaperUrlFilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(WallpaperUrlFilePath));
            }
            File.WriteAllText(WallpaperUrlFilePath, wallpaperUrl);
        }
        #endregion
        #region Working with chromiums
        public sealed class ContainerWindow : NativeWindow
        {
            private const int CS_HREDRAW = 0x0002;
            private const int CS_PARENTDC = 0x0080;
            private const int CS_VREDRAW = 0x0001;

            private const int WS_CHILD = 0x40000000;
            private const int WS_VISIBLE = 0x10000000;

            private const int WS_EX_NOACTIVATE = 0x08000000;
            private const int WS_EX_NOPARENTNOTIFY = 0x00000004;
            public ContainerWindow(int x, int y, IntPtr hWndParent)
            {
                CreateParams createParams = new CreateParams();
                createParams.Caption = "WallpaperX Container Window";
                createParams.Width = int.MaxValue;
                createParams.Height = int.MaxValue;
                createParams.ClassStyle = CS_HREDRAW | CS_PARENTDC | CS_VREDRAW;
                createParams.Style = WS_CHILD | WS_VISIBLE;
                createParams.ExStyle = WS_EX_NOACTIVATE | WS_EX_NOPARENTNOTIFY;
                createParams.X = x;
                createParams.Y = y;
                createParams.Parent = hWndParent;
                this.CreateHandle(createParams);
            }
            ~ContainerWindow()
            {
                this.DestroyHandle();
            }
            protected override void WndProc(ref Message m)
            {
                DefWndProc(ref m);
            }
        }
        public sealed class Chromium
        {
            public Screen screen;
            public int devtoolsPort;
            public Process process;
            public NativeWindow parentWindow;
            public string devtoolsUrl;
            public ClientWebSocket devTools;
        }
        public static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
        public static Chromium LaunchChromium(Screen screen, IntPtr wallpaperWorkerW)
        {
            // Create instance of chromium class
            Chromium chromium = new Chromium();
            chromium.screen = screen;

            // Locate a free port
            chromium.devtoolsPort = GetFreePort();

            // Launch chromium
            string userDataDir = Path.GetFullPath(".\\userdata");
            string chromiumBinPath = Path.GetFullPath(".\\chromium\\chrome.exe");
            string[] args = new string[] {
                    "--no-default-browser-check",
                    "--no-first-run",
                    "--disable-sync",
                    "--disable-extensions",
                    "--disable-plugins",
                    "--disable-background-networking",
                    "--disable-component-update",
                    "--disable-translate",
                    "--disable-features=TranslateUI,AutofillServerCommunication",
                    "--autoplay-policy=no-user-gesture-required",
                    "--mute-audio",
                    "--kiosk",
                    $"--remote-debugging-port={chromium.devtoolsPort}",
                    $"--user-data-dir={userDataDir}{chromium.devtoolsPort}",
                    $"--app={GetWallpaperUrl()}"
            };
            string chromiumPath = Path.GetFullPath(chromiumBinPath);
            chromium.process = Process.Start(chromiumPath, string.Join(" ", args.Select(arg => "\"" + arg + "\"")));

            // Set chromium as child
            KillChildOnClose(chromium.process.Handle);

            // Wait for chromium to initialize it's main window
            chromium.process.WaitForInputIdle();
            while (chromium.process.MainWindowHandle == IntPtr.Zero)
            {
                Thread.Sleep(5);
            }

            // Dock chromium
            Point location = SafeNative.ScreenToClient(wallpaperWorkerW, chromium.screen.Bounds.Location);
            chromium.parentWindow = new ContainerWindow(location.X, location.Y, wallpaperWorkerW);
            SafeNative.SetParent(chromium.process.MainWindowHandle, chromium.parentWindow.Handle);

            // Locate the devtoolsUrl
            using (HttpClient httpClient = new HttpClient())
            {
                chromium.devtoolsUrl = httpClient.GetStringAsync($"http://localhost:{chromium.devtoolsPort}/json").GetAwaiter().GetResult();
                chromium.devtoolsUrl = chromium.devtoolsUrl.Substring(chromium.devtoolsUrl.IndexOf("\"webSocketDebuggerUrl\": \"") + "\"webSocketDebuggerUrl\": \"".Length);
                chromium.devtoolsUrl = chromium.devtoolsUrl.Substring(0, chromium.devtoolsUrl.IndexOf("\""));
            }

            // Connect the devtools websocket
            chromium.devTools = new ClientWebSocket();
            chromium.devTools.ConnectAsync(new Uri(chromium.devtoolsUrl), CancellationToken.None).Wait();

            // Set viewport size with devtools
            string message = "{ \"id\": 1, \"method\": \"Emulation.setDeviceMetricsOverride\", \"params\": { \"width\": " + 100.ToString() + ", \"height\": " + 100.ToString() + ", \"deviceScaleFactor\": 1, \"mobile\": false, \"fitWindow\": false } }";
            chromium.devTools.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();

            string response = "";
            byte[] buffer = new byte[4096];
            ArraySegment<byte> arraySegment = new ArraySegment<byte>(buffer);
            WebSocketReceiveResult result = null;
            do
            {
                result = chromium.devTools.ReceiveAsync(arraySegment, CancellationToken.None).GetAwaiter().GetResult();
                response += Encoding.UTF8.GetString(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            // Return finished chromium
            return chromium;
        }
        public static void CloseChromium(Chromium chromium)
        {
            chromium.devTools.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal Closure", CancellationToken.None).GetAwaiter().GetResult();
            chromium.devTools.Dispose();

            chromium.process.CloseMainWindow();
            chromium.process.WaitForExit();
            chromium.process.Dispose();
        }
        #endregion
        #region Sending input to chromiums
        public static void SendInputEvent(Chromium chromium)
        {
            Point cursorPos = SafeNative.ScreenToClient(chromium.process.MainWindowHandle, Cursor.Position);
            string message = "{ \"id\": 1, \"method\": \"Input.dispatchMouseEvent\", \"params\": { \"type\": \"mouseMoved\", \"x\": " + cursorPos.X.ToString() + ", \"y\": " + cursorPos.Y.ToString() + " } }";
            chromium.devTools.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();

            string response = "";
            byte[] buffer = new byte[4096];
            ArraySegment<byte> arraySegment = new ArraySegment<byte>(buffer);
            WebSocketReceiveResult result = null;
            do
            {
                result = chromium.devTools.ReceiveAsync(arraySegment, CancellationToken.None).GetAwaiter().GetResult();
                response += Encoding.UTF8.GetString(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
        }
        #endregion
        #region Notify icon
        public static NotifyIcon StartNotifyIcon()
        {

            ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
            contextMenuStrip.Items.Add("Quit", null, (object sender, EventArgs e) =>
            {
                QuitRequested = true;
            });
            contextMenuStrip.Items.Add("Reload", null, (object sender, EventArgs e) =>
            {

            });

            NotifyIcon notifyIcon = new NotifyIcon
            {
                Text = "WallpaperX",
                Icon = SystemIcons.Application,
                ContextMenuStrip = contextMenuStrip,
                Visible = true
            };

            return notifyIcon;
        }
        public static void StopNotifyIcon(NotifyIcon notifyIcon)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        }
        #endregion
        public static bool QuitRequested = false;
        public static void Main()
        {
            try
            {
                IntPtr wallpaperWorkerW = SwitchToLayeredWallpaperMode();
                RedrawOriginalWallpaper();
                List<Chromium> chromiums = new List<Chromium>();
                foreach (Screen screen in Screen.AllScreens)
                {
                    chromiums.Add(LaunchChromium(screen, wallpaperWorkerW));
                }
                NotifyIcon notifyIcon = StartNotifyIcon();
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
                while (!QuitRequested)
                {
                    Application.DoEvents();
                    foreach (Chromium chromium in chromiums)
                    {
                        SendInputEvent(chromium);
                    }
                    Thread.Sleep(25);
                }
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
                StopNotifyIcon(notifyIcon);
                foreach (Chromium chromium in chromiums)
                {
                    SafeNative.SetParent(chromium.parentWindow.Handle, IntPtr.Zero);
                    SafeNative.SetParent(chromium.process.MainWindowHandle, IntPtr.Zero);
                    CloseChromium(chromium);
                }
                RedrawOriginalWallpaper();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace, "WallpaperX - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}