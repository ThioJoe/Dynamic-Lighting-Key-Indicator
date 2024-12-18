using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static Dynamic_Lighting_Key_Indicator.WinEnums;

namespace Dynamic_Lighting_Key_Indicator
{
    internal static class KeyStatesHandler
    {
        public static List<MonitoredKey> monitoredKeys = [];
        public static bool rawInputWatcherActive = false;

        // Win32 API imports
        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(
            [MarshalAs(UnmanagedType.LPArray)] RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int keyCode);

        public static void InitializeRawInput(IntPtr hwnd)
        {
            if (rawInputWatcherActive)
                return;

            // First set up the window procedure
            SubclassWindow(hwnd);

            // Then register for raw input
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0] = new RAWINPUTDEVICE
            {
                usUsagePage = (ushort)HIDUsagePage.HID_USAGE_PAGE_GENERIC,
                usUsage = (ushort)HIDGenericDesktopUsage.HID_USAGE_GENERIC_KEYBOARD,
                dwFlags = (uint)RawInput_dwFlags.RIDEV_INPUTSINK,
                hwndTarget = hwnd
            };

            if (!RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                throw new Exception("Failed to register raw input device.");
            }

            rawInputWatcherActive = true;
        }

        public static void ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;

            // First call - get the size of the input data
            if (GetRawInputData(lParam, (uint)RawInput_dwFlags.RID_INPUT, IntPtr.Zero, ref dwSize,
                (uint)Marshal.SizeOf<RAWINPUTHEADER>()) == unchecked((uint)-1))
            {
                return; // Handle error
            }

            IntPtr rawDataPtr = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                // Second call - get the actual data
                if (GetRawInputData(lParam, (uint)RawInput_dwFlags.RID_INPUT, rawDataPtr, ref dwSize,
                    (uint)Marshal.SizeOf<RAWINPUTHEADER>()) == unchecked((uint)-1))
                {
                    return; // Handle error
                }

                RAWINPUT rawInput = Marshal.PtrToStructure<RAWINPUT>(rawDataPtr);

                if (rawInput.header.dwType == (uint)RawInput_dwFlags.RIM_TYPEKEYBOARD)
                {
                    int vkCode = rawInput.keyboard.VKey;
                    bool isKeyUp = (rawInput.keyboard.Flags & 0x01) != 0;

                    // Check if the key press was one of the monitored keys
                    foreach (var mk in monitoredKeys)
                    {
                        if ((int)mk.key == vkCode && isKeyUp)
                        {
                            Task.Run(() => ColorSetter.SetSingleMonitorKeyColor_ToKeyboard(mk));
                            break;
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(rawDataPtr);
            }
        }

        public static bool FetchKeyState(int vkCode)
        {
            return (GetKeyState(vkCode) & 1) == 1;
        }

        public static void CleanupInputWatcher()
        {
            if (rawInputWatcherActive)
            {
                // Unregister by registering with RIDEV_REMOVE
                RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
                rid[0] = new RAWINPUTDEVICE
                {
                    usUsagePage = (ushort)HIDUsagePage.HID_USAGE_PAGE_GENERIC,
                    usUsage = (ushort)HIDGenericDesktopUsage.HID_USAGE_GENERIC_KEYBOARD,
                    dwFlags = 0x00000001, // RIDEV_REMOVE
                    hwndTarget = IntPtr.Zero
                };

                RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
                rawInputWatcherActive = false;
            }
        }

        // -------------------------------------------------------------------------------

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, WinEnums.WM_MESSAGE msg, UIntPtr wParam, IntPtr lParam);
        private static WndProcDelegate? wndProcDelegate;
        private static IntPtr originalWndProc;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate newProc);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam);

        private const int GWLP_WNDPROC = -4;

        public static void SubclassWindow(IntPtr hwnd)
        {
            // Keep a reference to the delegate to prevent garbage collection
            wndProcDelegate = new WndProcDelegate(WndProc);
            originalWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, wndProcDelegate);
        }

        private static IntPtr WndProc(IntPtr hwnd, WinEnums.WM_MESSAGE msg, UIntPtr wParam, IntPtr lParam)
        {
            if (msg == WinEnums.WM_MESSAGE.WM_INPUT)
            {
                KeyStatesHandler.ProcessRawInput(lParam);
                return DefRawInputProc(lParam, 1, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            }
            return CallWindowProc(originalWndProc, hwnd, (uint)msg, wParam, lParam);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr DefRawInputProc(
            IntPtr paRawInput,
            Int32 nInput,
            UInt32 cbSizeHeader);

        // -------------------------------------------------------------------------------


    } // ------ End class ---------

} // ------ End namespace ---------