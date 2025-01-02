using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static Dynamic_Lighting_Key_Indicator.Definitions.WinEnums.RAWKEYBOARD;

namespace Dynamic_Lighting_Key_Indicator
{
    internal static class KeyStatesHandler
    {
        public static List<MonitoredKey> monitoredKeys = [];
        public static Dictionary<VK, MonitoredKey> monitoredKeysDict = [];
        public static bool rawInputWatcherActive = false;

        // Win32 API imports
        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices([MarshalAs(UnmanagedType.LPArray)] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int keyCode);

        public static readonly uint rawInputHeaderSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        public static readonly uint rawInputSize = (uint)Marshal.SizeOf<RAWINPUT>();

        public static void InitializeRawInput(IntPtr hwnd)
        {
            if (originalWndProc == IntPtr.Zero)
            {
                // First set up the window procedure
                SubclassWindow(hwnd);

                // Then register for raw input
                RAWINPUTDEVICE[] rid =
                [
                    new() // Assign value to the first (and only) device in the array
                    {
                        usUsagePage = (ushort)HIDUsagePage.HID_USAGE_PAGE_GENERIC,
                        usUsage = (ushort)HIDGenericDesktopUsage.HID_USAGE_GENERIC_KEYBOARD,
                        dwFlags = RAWINPUTDEVICE._dwFlags.RIDEV_INPUTSINK,
                        hwndTarget = hwnd
                    },
                ]; // Create an array of 1 device

                if (!RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
                {
                    throw new Exception("Failed to register raw input device.");
                }
            }
        }

        public static RAWINPUT? GetRawInput(IntPtr lParam)
        {
            uint dwSize = rawInputSize;

            // We know it's a WM_INPUT message so we know the size of the data, so we can skip the first call to get the size
            IntPtr rawDataPtr = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                uint result = GetRawInputData(
                    hRawInput: lParam,
                    uiCommand: (uint)uiCommand.RID_INPUT,
                    pData: rawDataPtr,
                    pcbSize: ref dwSize,
                    cbSizeHeader: rawInputHeaderSize
                );

                if (result == 0xFFFFFFFF) // Error indicated by (UINT)-1
                    return null;

                return Marshal.PtrToStructure<RAWINPUT>(rawDataPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(rawDataPtr);
            }
        }

        public static bool FetchKeyState(int vkCode)
        {
            bool state = ((GetKeyState(vkCode) & 1) == 1);
            return state;
        }

        public static void CleanupInputWatcher()
        {
            if (rawInputWatcherActive)
            {
                // Unregister by registering with RIDEV_REMOVE
                RAWINPUTDEVICE[] rid =
                [
                    new()
                    {
                        usUsagePage = (ushort)HIDUsagePage.HID_USAGE_PAGE_GENERIC,
                        usUsage = (ushort)HIDGenericDesktopUsage.HID_USAGE_GENERIC_KEYBOARD,
                        dwFlags = RAWINPUTDEVICE._dwFlags.RIDEV_REMOVE,
                        hwndTarget = IntPtr.Zero
                    },
                ];
                RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
                rawInputWatcherActive = false;
            }
        }

        // --------------------------------- WndProc Handling ----------------------------------------

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam);


        [DllImport("user32.dll")]
        private static extern IntPtr DefRawInputProc(IntPtr paRawInput, Int32 nInput, UInt32 cbSizeHeader);

        [DllImport("user32.dll")]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam);

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, WinEnums.WM_MESSAGE msg, UIntPtr wParam, IntPtr lParam);
        private static WndProcDelegate? wndProcDelegate;

        private static IntPtr originalWndProc = IntPtr.Zero;
        private const int GWLP_WNDPROC = -4;

        public static void SubclassWindow(IntPtr hwnd)
        {
            if (originalWndProc == IntPtr.Zero)
            {
                // Keep a reference to the delegate to prevent garbage collection
                wndProcDelegate ??= WndProc;
                IntPtr wndProcPtr = Marshal.GetFunctionPointerForDelegate(wndProcDelegate);
                originalWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, wndProcPtr);
            }
        }

        //See: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nc-winuser-wndproc
        private static IntPtr WndProc(IntPtr hWnd, WinEnums.WM_MESSAGE uMsg, UIntPtr wParam, IntPtr lParam)
        {
            nint result = 0;

            // Check if the message is a raw input message, otherwise call the original window procedure so it still gets processed normally
            if (uMsg == WinEnums.WM_MESSAGE.WM_INPUT)
            {
                RAWINPUT? rawInputResult = GetRawInput(lParam);

                // See for wparam values: https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-input
                if (wParam == (UIntPtr)WM_INPUT_wParam.RIM_INPUTSINK) // Window was not in the foreground
                {
                    result = DefRawInputProc(lParam, 1, rawInputHeaderSize); // This might not actually do anything, apparently used as a black hole
                }
                else if (wParam == (UIntPtr)WM_INPUT_wParam.RIM_INPUT)
                {
                    // Goes to default window procedure
                    result = DefWindowProc(hWnd, (uint)uMsg, wParam, lParam);
                }

                // Now we can handle the raw input
                if (rawInputResult is RAWINPUT rawInput)
                {
                    // We already specified to only get keyboard input, so no need to check dwType, we can just check the keyboard data
                    if (rawInput.keyboard.Flags.HasFlag(_Flags.RI_KEY_BREAK))
                    {
                        // Check the dictionary
                        if (monitoredKeysDict.TryGetValue((VK)rawInput.keyboard.VKey, out MonitoredKey? mk))
                        {
                            Task.Run(() => ColorSetter.SetSingleMonitorKeyColor_ToKeyboard(mk));
                            //Task.Run(() => ColorSetter.SetProperColorsEveryKey_ToKeyboard());
                            Task.Run(() => MainViewModel.StaticUpdateLastKnownKeyState(mk));
                        }
                    }
                }
            }
            else
            {
                // Any other messages besides the raw input will be passed to the original window procedure
                result = CallWindowProc(originalWndProc, hWnd, (uint)uMsg, wParam, lParam);
            }

            return result; // Not actually used, but return anyway
        }

        // -------------------------------------------------------------------------------


    } // ------ End class ---------

} // ------ End namespace ---------