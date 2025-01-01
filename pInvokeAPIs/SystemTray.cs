﻿using Microsoft.UI;
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

        public void InitializeSystemTray()
        {
            // Get the window handle
            hwnd = GetWindowHandle();
            windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            appWindow = AppWindow.GetFromWindowId(windowId);

            // Initialize tray icon
            InitializeNotifyIcon();

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

        public void InitializeNotifyIcon()
        {
            notifyIcon = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATAW)),
                hWnd = hwnd,
                uID = 1,
                uFlags = NOTIFYICONDATAA_uFlags.NIF_ICON | NOTIFYICONDATAA_uFlags.NIF_MESSAGE | NOTIFYICONDATAA_uFlags.NIF_TIP,
                uCallbackMessage = (uint)WM_MESSAGE.WM_TRAYICON
            };

            // Set tooltip - using Marshal to avoid unsafe code
            string tip = "Dynamic Lighting Key Indicator";
            notifyIcon.szTip = tip;

            // Load icon from embedded resource
            IntPtr hIcon;
            System.Drawing.Icon? icon = LoadIconFromResource("Dynamic_Lighting_Key_Indicator.Assets.Icon.ico");

            if (icon == null)
            {
                hIcon = GetDefaultIconHandle();
            }
            else
            {
                hIcon = icon.Handle; // This gives you the HICON handle
            }

            // Load default icon if failed to load from resource


            notifyIcon.hIcon = hIcon;

            // Add the icon
            if (!Shell_NotifyIcon((uint)NotifyIcon_dwMessage.NIM_ADD, ref notifyIcon))
            {
                // Handle error
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"Failed to add tray icon. Error: {error}");
            }

            // Set version (required for reliable operation)
            notifyIcon.uVersion = uVersion.NOTIFYICON_VERSION;
            Shell_NotifyIcon(NotifyIcon_dwMessage.NIM_SETVERSION, ref notifyIcon);
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
            Shell_NotifyIcon(NotifyIcon_dwMessage.NIM_DELETE, ref notifyIcon);

            if (defaultWndProc != IntPtr.Zero)
            {
                SetWindowLongPtr(hwnd, nIndex.GWLP_WNDPROC, defaultWndProc);
            }
            Application.Current.Exit(); // This will automatically trigger the .closed event on MainWindow
        }

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, WinEnums.WM_MESSAGE msg, UIntPtr wParam, IntPtr lParam);

        private IntPtr WndProc(IntPtr hwnd, WinEnums.WM_MESSAGE msg, UIntPtr wParam, IntPtr lParam)
        {
            // Look up the debug in WinEnums.WM_MESSAGE for the name
            //Debug.WriteLine($"WndProc: {Enum.GetName(typeof(WinEnums.WM_MESSAGE), msg)} ({msg:X4})");

            if (msg == WM_MESSAGE.WM_TRAYICON)
            {
                uint lparam = (uint)lParam.ToInt64();
                if (lparam == (uint)WM_MESSAGE.WM_LBUTTONUP)
                {
                    RestoreFromTray();
                    return IntPtr.Zero;
                }
                else if (lparam == (uint)WM_MESSAGE.WM_RBUTTONUP)
                {
                    //ShowTrayContextMenu();
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