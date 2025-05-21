using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Timers;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;

namespace WallpaperX
{
    public static class Program
    {
        #region Getting and throwing last error
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void SetLastError(int dwErrCode);
        public static void ClearLastError()
        {
            const int ERROR_SUCCESS = 0;

            SetLastError(ERROR_SUCCESS);
        }
        public static void ThrowLastError()
        {
            const int ERROR_SUCCESS = 0;

            int lastError = Marshal.GetLastWin32Error();
            if (lastError == ERROR_SUCCESS)
            {
                throw new Win32Exception(ERROR_SUCCESS, "An unknown Win32 error occured.");
            }
            else
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        #endregion
        #region Switching to and from layered wallpaper mode
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowExW(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeoutW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
        private delegate bool WNDENUMPROC(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(WNDENUMPROC lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        public static IntPtr SwitchToLayeredWallpaperMode()
        {
            const uint WM_SWITCH_TO_LAYERED_WALLPAPER_MODE = 0x052C;
            const int SMTO_NOTIMEOUTIFNOTHUNG = 0x0008;
            const uint GW_HWNDNEXT = 2;
            const uint GW_HWNDPREV = 3;

            // Hoisted shared definitions
            IntPtr progman = IntPtr.Zero;
            IntPtr iconsWorkerW = IntPtr.Zero;
            WNDENUMPROC iconsWorkerWLocatorProc = (enumHwnd, lParam) =>
            {
                IntPtr shellDllDefView = FindWindowExW(enumHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellDllDefView != IntPtr.Zero)
                {
                    IntPtr folderView = FindWindowExW(shellDllDefView, IntPtr.Zero, "SysListView32", null);
                    if (folderView != IntPtr.Zero)
                    {
                        iconsWorkerW = enumHwnd;
                    }
                }
                return true;
            };
            IntPtr wallpaperWorkerW = IntPtr.Zero;

            // Locate progman
            ClearLastError();
            progman = FindWindowExW(IntPtr.Zero, IntPtr.Zero, "Progman", null);
            if (progman == IntPtr.Zero)
            {
                if (Marshal.GetLastWin32Error() == 0)
                {
                    throw new Exception("Failed to locate progman. This should never happen. Try restarting your PC.");
                }
                else
                {
                    ThrowLastError();
                }
            }

            // Tell progman to switch to layered wallpaper mode
            ClearLastError();
            if (SendMessageTimeoutW(progman, WM_SWITCH_TO_LAYERED_WALLPAPER_MODE, IntPtr.Zero, IntPtr.Zero, SMTO_NOTIMEOUTIFNOTHUNG, 0, out IntPtr result) == IntPtr.Zero)
            {
                ThrowLastError();
            }

            // Search for IconsWorkerW
            ClearLastError();
            if (!EnumWindows(iconsWorkerWLocatorProc, IntPtr.Zero))
            {
                ThrowLastError();
            }
            if (iconsWorkerW == IntPtr.Zero)
            {
                goto RestartProgman;
            }

            // Search for WallpaperWorkerW
            wallpaperWorkerW = GetWindow(iconsWorkerW, GW_HWNDNEXT);
            if (wallpaperWorkerW != GetWindow(progman, GW_HWNDPREV))
            {
                wallpaperWorkerW = IntPtr.Zero;
            }
            if (wallpaperWorkerW == IntPtr.Zero)
            {
                goto RestartProgman;
            }

            // Return if successful so far otherwise restart progman.
            return wallpaperWorkerW;
        RestartProgman:
            MessageBox.Show("Restarting progman");

            // Get progman's process ID
            ClearLastError();
            if (GetWindowThreadProcessId(progman, out uint progmanPID) == 0)
            {
                ThrowLastError();
            }

            // Kill the progman process
            Process progmanProc = Process.GetProcessById((int)progmanPID);
            progmanProc.Kill();
            progmanProc.WaitForExit();
            progmanProc.Dispose();

            // Wait for progman to automatically restart
            progman = IntPtr.Zero;
            while (true)
            {
                progman = FindWindowExW(IntPtr.Zero, IntPtr.Zero, "Progman", null);
                if (progman != IntPtr.Zero)
                {
                    break;
                }
                Thread.Sleep(25);
            }

            // Tell progman to switch to layered wallpaper mode again
            ClearLastError();
            if (SendMessageTimeoutW(progman, WM_SWITCH_TO_LAYERED_WALLPAPER_MODE, IntPtr.Zero, IntPtr.Zero, SMTO_NOTIMEOUTIFNOTHUNG, 0, out IntPtr result3) == IntPtr.Zero)
            {
                ThrowLastError();
            }

            // Search for IconsWorkerW again
            ClearLastError();
            if (!EnumWindows(iconsWorkerWLocatorProc, IntPtr.Zero))
            {
                ThrowLastError();
            }
            if (iconsWorkerW == IntPtr.Zero)
            {
                throw new Exception("IconsWorkerW could not be found.");
            }

            // Search for WallpaperWorkerW again
            wallpaperWorkerW = GetWindow(iconsWorkerW, GW_HWNDNEXT);
            if (wallpaperWorkerW != GetWindow(progman, GW_HWNDPREV))
            {
                wallpaperWorkerW = IntPtr.Zero;
            }
            if (wallpaperWorkerW == IntPtr.Zero)
            {
                throw new Exception("WallpaperWorkerW could not be found.");
            }

            // Return output
            return wallpaperWorkerW;
        }
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
        private static string GetWallpaperSystemParameter()
        {
            const int MAX_PATH = 260;
            const uint SPI_GETDESKWALLPAPER = 0x0073;

            IntPtr buffer = Marshal.AllocHGlobal(MAX_PATH * sizeof(char));
            try
            {
                ClearLastError();
                if (!SystemParametersInfoW(SPI_GETDESKWALLPAPER, MAX_PATH, buffer, 0))
                {
                    ThrowLastError();
                }
                return Marshal.PtrToStringUni(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        private static void SetWallpaperSystemParameter(string wallpaperPath)
        {
            const uint SPI_SETDESKWALLPAPER = 0x0014;
            const uint SPIF_SENDCHANGE = 0x02;

            IntPtr ptr = Marshal.StringToHGlobalUni(wallpaperPath);
            try
            {
                ClearLastError();
                if (!SystemParametersInfoW(SPI_SETDESKWALLPAPER, 0, ptr, SPIF_SENDCHANGE))
                {
                    ThrowLastError();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        public static void LeaveLayeredWallpaperMode()
        {
            string originalWallpaper = GetWallpaperSystemParameter();
            SetWallpaperSystemParameter(originalWallpaper);
        }
        #endregion
        #region Docking and undocking wallpaper windows
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        public static void DockWindow(IntPtr hWnd, IntPtr wallpaperWorkerW)
        {
            SetParent(hWnd, wallpaperWorkerW);
        }
        public static void UndockWindow(IntPtr hWnd)
        {
            SetParent(hWnd, IntPtr.Zero);
        }
        #endregion
        #region Cleaning up child processes automatically when this process crashes
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string lpName);
        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, uint JobObjectInformationClass, IntPtr lpJobObjectInformation, uint cbJobObjectInformationLength);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);
        public static void KillChildOnClose(IntPtr childProcessHandle)
        {
            const uint JobObjectExtendedLimitInformation = 9;
            const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

            ClearLastError();
            IntPtr hJob = CreateJobObjectW(IntPtr.Zero, null);
            if (hJob == IntPtr.Zero)
            {
                ThrowLastError();
            }

            JOBOBJECT_EXTENDED_LIMIT_INFORMATION jobInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            jobInfo.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            int sizeofJobInfo = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr jobInfoPtr = Marshal.AllocHGlobal(sizeofJobInfo);
            try
            {
                Marshal.StructureToPtr(jobInfo, jobInfoPtr, false);

                ClearLastError();
                if (!SetInformationJobObject(hJob, JobObjectExtendedLimitInformation, jobInfoPtr, (uint)sizeofJobInfo))
                {
                    ThrowLastError();
                }

                ClearLastError();
                if (!AssignProcessToJobObject(hJob, childProcessHandle))
                {
                    ThrowLastError();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(jobInfoPtr);
            }
        }
        #endregion

        #region Getting and setting the wallpaper
        public static string GetWallpaperUrl()
        {
            //string DefaultWallpaperUrl = "file:///" + Path.GetFullPath(".\\example\\wallpaper.html").Replace("\\", "/");
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
        #region Notify icon
        public static void RunNotifyIconTillQuit()
        {
            bool quitRequested = false;

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Quit", null, (object sender, EventArgs e) =>
            {
                quitRequested = true;
            });
            contextMenu.Items.Add("Reload", null, (object sender, EventArgs e) =>
            {

            });

            NotifyIcon taskbarIcon = new NotifyIcon
            {
                Text = "WallpaperX",
                Icon = SystemIcons.Application,
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
            while (!quitRequested)
            {
                Application.DoEvents();
                Thread.Sleep(100);
            }
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;

            taskbarIcon.Visible = false;
            taskbarIcon.Dispose();
        }
        #endregion
        #region Launching and closing a chromium
        public static Process LaunchChromiumOnScreen(Screen screen)
        {
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
                    "--no-sandbox",
                    "--remote-debugging-port=59287",
                    $"--window-position={screen.Bounds.X},{screen.Bounds.Y}",
                    $"--window-size={screen.Bounds.Width},{screen.Bounds.Height}",
                    $"--user-data-dir={userDataDir}",
                    $"--app={GetWallpaperUrl()}"
                };
            string chromiumPath = Path.GetFullPath(chromiumBinPath);
            Process chromium = Process.Start(chromiumPath, string.Join(" ", args.Select(arg => "\"" + arg + "\"")));
            KillChildOnClose(chromium.Handle);
            while (chromium.MainWindowHandle == IntPtr.Zero)
            {
                Thread.Sleep(25);
            }
            return chromium;
        }
        public static void CloseChromium(Process chromium)
        {
            chromium.CloseMainWindow();
            chromium.Kill();
            chromium.WaitForExit();
            chromium.Dispose();
        }
        #endregion
        #region Connecting And Using The DevTools
        public static ClientWebSocket ConnectDevTools(int port)
        {

        }
        #endregion
        #region Connecting puppeteerSharp to chromium
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        public static async void ConnectToPuppet(int port, IntPtr hWnd)
        {
            string wss = "";
            using (HttpClient httpClient = new HttpClient())
            {
                wss = await httpClient.GetStringAsync("http://localhost:59287/json");
                wss = wss.Substring(wss.IndexOf("\"webSocketDebuggerUrl\": \"") + "\"webSocketDebuggerUrl\": \"".Length);
                wss = wss.Substring(0, wss.IndexOf("\""));
            }

            ClientWebSocket client = new ClientWebSocket();
            await client.ConnectAsync(new Uri(wss), CancellationToken.None);

            System.Timers.Timer timer = new System.Timers.Timer(10);
            timer.Elapsed += async (object sender, ElapsedEventArgs e) =>
            {
                Point cursorPosDotNet = Cursor.Position;
                POINT cursorPos = new POINT();
                cursorPos.X = cursorPosDotNet.X;
                cursorPos.Y = cursorPosDotNet.Y;
                ScreenToClient(hWnd, ref cursorPos);

                string message = "{ \"id\": 1, \"method\": \"Input.dispatchMouseEvent\", \"params\": { \"type\": \"mouseMoved\", \"x\": " + cursorPos.X.ToString() + ", \"y\": " + cursorPos.Y.ToString() + " } }";
                await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None);
            };
            timer.AutoReset = true;
            timer.Enabled = true;
        }
        #endregion
        public static void Main()
        {
            try
            {
                IntPtr wallpaperWorkerW = SwitchToLayeredWallpaperMode();
                List<Process> chromiums = new List<Process>();
                foreach (Screen screen in new Screen[] { Screen.PrimaryScreen })
                {
                    Process chromium = LaunchChromiumOnScreen(screen);
                    DockWindow(chromium.MainWindowHandle, wallpaperWorkerW);
                    ConnectToPuppet(59287, chromium.MainWindowHandle);
                    chromiums.Add(chromium);
                }
                RunNotifyIconTillQuit();
                foreach (Process chromium in chromiums)
                {
                    UndockWindow(chromium.MainWindowHandle);
                    CloseChromium(chromium);
                }
                LeaveLayeredWallpaperMode();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace, "WallpaperX - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}