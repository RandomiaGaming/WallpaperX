The desktop wallpaper, desktop icons, taskbar and many other system UI elements are all powered by explorer.exe.
The window in charge of rendering the wallpaper and desktop icons is Progman.
Progman has two modes for rendering the wallpaper, normal mode and layered mode.
In normal mode the window structure looks like this:
Desktop (CSRSS.exe)
---> Other Apps With Windows Like
---> Settings
---> Google Chrome
---> Steam
---> Progman (Explorer.exe)
-------> SHELLDLL_DefView (Explorer.exe)
-----------> SysListView32 (Explorer.exe)
When in normal mode SysListView32 draws both the desktop icons and the wallpaper.
So you cannot place a window in between these two layers since they aren't separate layers at all but one single window.
But we can send the WM_SWITCH_TO_LAYERED_WALLPAPER_MODE message to Progman (sometimes called WM_SPAWN_WORKERW) with message code 0x052C.
After this switch the window structure will look like this:
Desktop (CSRSS.exe)
---> Other Apps With Windows Like
---> Settings
---> Google Chrome
---> Steam
---> WorkerW
-------> SHELLDLL_DefView (Explorer.exe)
-----------> SysListView32 (Explorer.exe)
---> WorkerW
---> Progman (Explorer.exe)
Now we can see Explorer.exe has shifted to layered wallpaper mode.
In this mode we have two new windows with the class WorkerW.
To distinguish them we will give them the following names.
The WorkerW with SHELLDLL_DefView and SysListView32 as children we will call IconsWorkerW.
And the WorkerW with no children we will call WallpaperWorkerW.
IconsWorkerW renders the desktop icons and handles user input when interacting with the desktop.
WallpaperWorkerW is where the wallpaper is drawn.
To draw custom graphics to the wallpaper we can simply create a child window under WallpaperWorkerW.
Now it is important to note if our window gets destroyed while it is a child of WallpaperWorkerW it will send a WM_PARENTNOTIFY message to WallpaperWorkerW with WParam = WM_DESTROY.
If this happens WallpaperWorkerW will also destroy itself.
This is not what we want because Explorer.exe will still be in layered wallpaper mode even if WallpaperWorkerW is destroyed.
As such it will ignore WM_SWITCH_TO_LAYERED_WALLPAPER_MODE requests since it thinks we are already in layered wallpaper mode.
But with WallpaperWorkerW gone it becomes impossible to use layered wallpaper mode.
As such we should always change parents to NULL before destroying a window or block the WM_PARENTNOTIFY message.
Additional note, Explorer.exe will take windows whose z-index is behind Progman and simply place them one layer in front of Progman.
This behavior is so developers can send their window to the back with HWND_BOTTOM without sending it behind the desktop wallpaper by mistake.
Additionally in layered wallpaper mode Explorer.exe will move windows placed behind IconsWorkerW to one layer in front of IconsWorkerW.