﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Foundation;
using System;
using WinRT;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;
using Windows.Storage.Streams;
using Windows.Storage.FileProperties;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using System.Reflection;
using System.Drawing;

namespace Dynamic_Lighting_Key_Indicator
{
    internal class SystemTray
    {
        private readonly MainWindow mainWindow;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATAW
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion;  // Changed from union to direct field
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATAW lpData);

        [DllImport("user32.dll")]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_TRAYICON = 0x800;
        private const int GWLP_WNDPROC = -4;
        private const uint NOTIFYICON_VERSION = 3; // Do not use 4, or else the context menu will not work
        private const uint NIM_SETVERSION = 4;

        private NOTIFYICONDATAW notifyIcon;
        private IntPtr hwnd;
        private WindowId windowId;
        private AppWindow appWindow;
        private bool isMinimizedToTray = false;
        private WndProcDelegate newWndProc;
        private IntPtr defaultWndProc;

        public SystemTray(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        public void InitializeSystemTray() // Whatever your main window class is called, in this case MainWindow
        {
            // Get the window handle
            hwnd = GetWindowHandle();
            windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            appWindow = AppWindow.GetFromWindowId(windowId);

            // Initialize tray icon
            InitializeNotifyIcon();

            // Handle window closing
            this.mainWindow.AppWindow.Closing += AppWindow_Closing;

            // Set up window message handling
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                newWndProc = new WndProcDelegate(WndProc);
                defaultWndProc = GetWindowLongPtr(hwnd, GWLP_WNDPROC);
                SetWindowLongPtr(hwnd, GWLP_WNDPROC,
                    Marshal.GetFunctionPointerForDelegate(newWndProc));
            });
        }

        [Flags]
        public enum NOTIFY_ICON_DATA_FLAGS : uint
        {
            NIF_MESSAGE = 0x00000001,
            NIF_ICON = 0x00000002,
            NIF_TIP = 0x00000004,
            NIF_STATE = 0x00000008,
            NIF_INFO = 0x00000010,
            NIF_GUID = 0x00000020,
            NIF_REALTIME = 0x00000040,
            NIF_SHOWTIP = 0x00000080
        }

        // See: https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw
        public enum NOTIFY_ICON_MESSAGE
        {
            NIM_ADD = 0x00000000,
            NIM_MODIFY = 0x00000001,
            NIM_DELETE = 0x00000002,
            NIM_SETFOCUS = 0x00000003,
            NIM_SETVERSION = 0x00000004
        }

        public IntPtr LoadhIconFromResource(string resourceName)
        {
            // Get the assembly containing the resource
            Assembly assembly = typeof(MainWindow).Assembly;

            // Get the resource stream
            using Stream iconStream = assembly.GetManifestResourceStream(resourceName);
            if (iconStream == null)
            {
                throw new Exception("Icon resource not found.");
            }

            System.Drawing.Icon icon = new Icon(iconStream);
            IntPtr hIcon = icon.Handle; // This gives you the HICON handle

            return hIcon;
        }

        public void InitializeNotifyIcon()
        {
            notifyIcon = new NOTIFYICONDATAW();
            notifyIcon.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATAW));
            notifyIcon.hWnd = hwnd;
            notifyIcon.uID = 1;
            notifyIcon.uFlags = (uint)(NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_TIP);
            notifyIcon.uCallbackMessage = WM_TRAYICON;

            // Set tooltip - using Marshal to avoid unsafe code
            string tip = "Dynamic Lighting Key Indicator";
            notifyIcon.szTip = tip;

            // Load icon from embedded resource
            IntPtr hIcon = LoadhIconFromResource("Dynamic_Lighting_Key_Indicator.Assets.Icon.ico");
            notifyIcon.hIcon = hIcon;

            // Add the icon
            if (!Shell_NotifyIcon((uint)NOTIFY_ICON_MESSAGE.NIM_ADD, ref notifyIcon))
            {
                // Handle error
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"Failed to add tray icon. Error: {error}");
            }

            // Set version (required for reliable operation)
            notifyIcon.uVersion = NOTIFYICON_VERSION;
            Shell_NotifyIcon((uint)NOTIFY_ICON_MESSAGE.NIM_SETVERSION, ref notifyIcon);
        }

        public void MinimizeToTray()
        {
            if (!isMinimizedToTray)
            {
                appWindow.Hide();
                isMinimizedToTray = true;
            }
        }

        public void RestoreFromTray()
        {
            if (isMinimizedToTray)
            {
                appWindow.Show();
                isMinimizedToTray = false;
            }
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            // Cancel the close
            args.Cancel = true;

            // Minimize to tray instead
            MinimizeToTray();
        }

        public void ExitApplication()
        {
            Shell_NotifyIcon(NIM_DELETE, ref notifyIcon);

            if (defaultWndProc != IntPtr.Zero)
            {
                SetWindowLongPtr(hwnd, GWLP_WNDPROC, defaultWndProc);
            }

            Application.Current.Exit();
        }

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const int WM_CLOSE = 0x0010;  // Add this to the constants

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAYICON)
            {
                uint lparam = (uint)lParam.ToInt64();
                if (lparam == WM_LBUTTONUP)
                {
                    RestoreFromTray();
                    return IntPtr.Zero;
                }
                else if (lparam == WM_RBUTTONUP)
                {
                    //ShowTrayContextMenu();
                    CustomContextMenu.CreateAndShowMenu(hwnd, this);
                    return IntPtr.Zero;
                }
            }
            else if (msg == WM_CLOSE)
            {
                // Intercept window close
                MinimizeToTray();
                return IntPtr.Zero;
            }

            return DefWindowProc(hwnd, msg, wParam, lParam);
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