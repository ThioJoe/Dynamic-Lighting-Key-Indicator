using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using WinRT;

namespace Dynamic_Lighting_Key_Indicator
{
    internal class SystemTray(MainWindow mainWindow)
    {
        private readonly MainWindow mainWindow = mainWindow;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATAW
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public NOTIFYICONDATAA_uFlags uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public WinEnums.uVersion uVersion;  // Changed from union to direct field
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern uint RegisterWindowMessage(string lpString);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern bool Shell_NotifyIcon(WinEnums.NotifyIcon_dwMessage dwMessage, ref NOTIFYICONDATAW lpData);

        [DllImport("user32.dll")]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam);
        // For 64 Bit
        [DllImport("user32.dll")]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, WinEnums.nIndex nIndex, IntPtr dwNewLong);
        // For 32 Bit
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        // Auto switch between 32 and 64 bit
        private static IntPtr SetWindowLongPtrWrapper(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) // 64-bit
            {
                return SetWindowLongPtr(hWnd, (WinEnums.nIndex)nIndex, dwNewLong);
            }
            else // 32-bit
            {
                return new IntPtr(SetWindowLong(hWnd, nIndex, dwNewLong.ToInt32()));
            }
        }
        // For 32 Bit
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);
        // For 64 Bit
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        // Auto switch between 32 and 64 bit
        private static IntPtr GetWindowLongPtrWrapper(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8) // 64-bit
            {
                return GetWindowLongPtr(hWnd, nIndex);
            }
            else // 32-bit
            {
                return GetWindowLong(hWnd, nIndex);
            }
        }
        [DllImport("user32.dll")]
        static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        // Using this to pass on messages to the original default window procedure if not being processed by the custom one
        [DllImport("user32.dll")]
        static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam);

        private NOTIFYICONDATAW notifyIcon;
        private IntPtr hwnd;
        private WindowId windowId;
        private AppWindow appWindow = mainWindow.AppWindow;
        private WndProcDelegate? newWndProc;
        private IntPtr defaultWndProc;

        // For re-creation of the tray icon after a taskbar restart
        private uint _taskbarCreatedMessageId;

        private void RegisterTaskbarCreatedMessage()
        {
            // Register the TaskbarCreated message
            _taskbarCreatedMessageId = RegisterWindowMessage("TaskbarCreated");
            if (_taskbarCreatedMessageId == 0)
            {
                // Handle error: Could not register the message
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to register TaskbarCreated message. Error: {error}");
                // Depending on requirements, you might want to throw or log more severely
            }
            else
            {
                Debug.WriteLine($"TaskbarCreated message registered with ID: {_taskbarCreatedMessageId}");
            }
        }

        public void InitializeSystemTray()
        {
            // Get the window handle
            hwnd = GetWindowHandle();
            windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            appWindow = AppWindow.GetFromWindowId(windowId);

            // If Explorer restarts or the taskbar is re-created we need to re-create the tray icon, otherwise it will be gone
            RegisterTaskbarCreatedMessage();

            // Initialize and add tray icon
            InitializeAndAddNotifyIcon();

            // Handle window closing
            this.mainWindow.AppWindow.Closing += AppWindow_Closing;

            // Set up custom WndProc to intercept windows messages, process tray window closing and tray icon clicks, and forward the rest to the default window procedure
            Debug.WriteLine("Setting up WndProc");
            newWndProc = new WndProcDelegate(WndProc);
            defaultWndProc = GetWindowLongPtrWrapper(hwnd, (int)nIndex.GWLP_WNDPROC);
            SetWindowLongPtrWrapper(hwnd, (int)nIndex.GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(newWndProc));
        }

        public static System.Drawing.Icon? LoadIconFromResource(string resourceName)
        {
            // Get the assembly containing the resource
            Assembly assembly = typeof(MainWindow).Assembly;

            // Get the resource stream
            using Stream? iconStream = assembly.GetManifestResourceStream(resourceName);
            if (iconStream == null)
            {
                Debug.WriteLine($"Failed to load icon from resource: {resourceName}");
                return null;
            }

            System.Drawing.Icon icon = new System.Drawing.Icon(iconStream);

            return icon;
        }

        public static IntPtr GetDefaultIconHandle()
        {
            return LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
        }

        public void InitializeAndAddNotifyIcon()
        {
            notifyIcon = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = hwnd,
                uID = 1,
                uFlags = NOTIFYICONDATAA_uFlags.NIF_ICON | NOTIFYICONDATAA_uFlags.NIF_MESSAGE | NOTIFYICONDATAA_uFlags.NIF_TIP,
                uCallbackMessage = (uint)WM_MESSAGE.WM_TRAYICON,
                szTip = "Dynamic Lighting Key Indicator" // Tooltip
            };

            // Load icon from embedded resource
            IntPtr hIcon;
            using System.Drawing.Icon? icon = LoadIconFromResource("Dynamic_Lighting_Key_Indicator.Assets.Icon.ico");

            if (icon == null)
            {
                Debug.WriteLine("Failed to load custom icon, using default application icon.");
                hIcon = GetDefaultIconHandle();
            }
            else
            {
                hIcon = icon.Handle;
            }

            notifyIcon.hIcon = hIcon;

            // Add or Modify the icon
            // Use NIM_MODIFY if the icon might already exist (e.g., after TaskbarCreated), otherwise use NIM_ADD. NIM_ADD fails if the icon already exists.
            // A simple strategy is to try Add, and if it fails, try Modify. However, for the initial add, NIM_ADD is correct.
            if (Shell_NotifyIcon(NotifyIcon_dwMessage.NIM_ADD, ref notifyIcon))
            {
                notifyIcon.uVersion = uVersion.NOTIFYICON_VERSION_4; // Done after adding the icon
                if (!Shell_NotifyIcon(NotifyIcon_dwMessage.NIM_SETVERSION, ref notifyIcon))
                {
                    Debug.WriteLine($"Failed to set tray icon version. Error: {Marshal.GetLastWin32Error()}");
                }
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                // ERROR_TIMEOUT (1460) can occur if the taskbar isn't ready.
                // ERROR_OBJECT_NOT_FOUND (4312) might occur on NIM_MODIFY if icon doesn't exist.
                Debug.WriteLine($"Failed to add tray icon initially. Error: {error}");
                // Optionally try NIM_MODIFY here if add failed, though it shouldn't be needed on first run.
                // if (!Shell_NotifyIcon(WinEnums.NotifyIcon_dwMessage.NIM_MODIFY, ref notifyIcon)) { ... }
            }

            // IMPORTANT: Do NOT dispose the icon object if you are using its Handle (hIcon = icon.Handle).
            // The NOTIFYICONDATA needs the handle to remain valid. Manage the icon's lifetime appropriately.
            // If loading frequently (like in Recreate), consider caching the icon or handle.
        }

        private void RecreateNotifyIcon()
        {
            Debug.WriteLine("Taskbar created/restarted. Attempting to recreate tray icon.");

            // If the icon was previously visible (or should be), try adding it again.
            // The InitializeAndAddNotifyIcon handles the setup and NIM_ADD/NIM_SETVERSION logic.
            // We might need to NIM_DELETE first if NIM_ADD fails consistently after restart,
            //      but often just NIM_ADD/NIM_MODIFY after TaskbarCreated is sufficient. Let's retry the Add/SetVersion flow.

            // Remove the old icon first (best practice) - ignore errors as it might already be gone
            Shell_NotifyIcon(WinEnums.NotifyIcon_dwMessage.NIM_DELETE, ref notifyIcon);

            // Re-initialize and add the icon
            InitializeAndAddNotifyIcon();
        }

        public void MinimizeToTray()
        {
            Debug.WriteLine("Minimizing To Tray");
            appWindow.Hide();
        }

        public void RestoreFromTray()
        {
            appWindow.Show();
        }

        // Intercept window closing and minimize to tray instead
        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            // Cancel the close
            args.Cancel = true;

            // Minimize to tray instead
            MinimizeToTray();
        }

        public void ExitApplication()
        {
            // Remove the icon if it was added / we think it should be visible
            if (!Shell_NotifyIcon(NotifyIcon_dwMessage.NIM_DELETE, ref notifyIcon))
            {
                Debug.WriteLine($"Failed to remove tray icon on exit. Error: {Marshal.GetLastWin32Error()}");
            }

            // Restore the default window procedure
            if (defaultWndProc != IntPtr.Zero && hwnd != IntPtr.Zero)
            {
                SetWindowLongPtrWrapper(hwnd, (int)nIndex.GWLP_WNDPROC, defaultWndProc);
            }

            Application.Current.Exit(); // Will automatically trigger .closed event on MainWindow
        }

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, WinEnums.WM_MESSAGE msg, UIntPtr wParam, IntPtr lParam);

        private IntPtr WndProc(IntPtr hwnd, WinEnums.WM_MESSAGE msg, UIntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_MESSAGE.WM_TRAYICON)
            {
                // Extract the message code from the lower word of lParam
                // In commit b503283103032d52cabe232afd396ccd7591f094 this wasn't necessary because the higher word was always 0,
                //      but now it's 1 for some unknown reason
                uint mouseMessage = (uint)lParam.ToInt64() & 0xFFFF; // Extract lower word
                //uint upperWord = (uint)lParam.ToInt64() >> 16;          // Extract upper word. Not sure what this is for

                // Now we can properly identify and display the notification code
                Debug.WriteLine($"WM_TRAYICON: Trigger = {mouseMessage:X4} - {Enum.GetName(typeof(WinEnums.WM_MESSAGE), mouseMessage)}");

                // Use button_up instead of down because the user might click and hold the icon to drag it
                if (mouseMessage == (uint)WM_MESSAGE.WM_LBUTTONUP)
                {
                    RestoreFromTray();
                    return IntPtr.Zero;
                }
                else if (mouseMessage == (uint)WM_MESSAGE.WM_RBUTTONUP)
                {
                    CustomContextMenu.CreateAndShowMenu(hwnd, this);
                    return IntPtr.Zero;
                }

            }
            else if (msg == WM_MESSAGE.WM_CLOSE)
            {
                // Intercept window close
                MinimizeToTray();
                return IntPtr.Zero;
            }
            else if (_taskbarCreatedMessageId != 0 && msg == (WM_MESSAGE)_taskbarCreatedMessageId)
            {
                RecreateNotifyIcon();
                // We handle this message, but it's often broadcast, so returning DefWindowProc might be safer than Zero to allow other apps to receive it too.
                // However, for handling *our* icon recreation, Zero is technically correct. Let's pass it on just in case.
                return CallWindowProc(defaultWndProc, hwnd, (uint)msg, wParam, lParam); // Pass it on
            }

            // Pass on the other messages to the original window procedure if we didn't handle it ourselves
            // Important otherwise the window will not behave as expected
            return CallWindowProc(defaultWndProc, hwnd, (uint)msg, wParam, lParam);
        }

        private IntPtr GetWindowHandle()
        {
            var windowNative = this.mainWindow.As<IWindowNative>();
            return windowNative.WindowHandle;
        }
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
    internal interface IWindowNative
    {
        IntPtr WindowHandle { get; }
    }
}