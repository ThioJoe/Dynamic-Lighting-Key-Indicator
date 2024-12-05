using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Dynamic_Lighting_Key_Indicator
{
    internal static class ProtocolMessage
    {
        private const string MessageIdentifier = "DLKI_PROTOCOL_MESSAGE";
        private const string UniqueWindowClass = "DLKI_MainWindow";
        private static uint WM_PROTOCOL_DATA;
        private static IntPtr mainWindowHandle;
        private static WinProc newWndProc;
        private static IntPtr oldWndProc;
        private static GCHandle delegateHandle;
        private static bool isInitialized;
        private static Window mainWindow;

        // Add shared memory for URI transfer
        private static IntPtr sharedMemHandle;
        private const int MAX_URI_LENGTH = 2048;

        public delegate IntPtr WinProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpAttributes,
            uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint
            dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow,
            uint dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int GWLP_WNDPROC = -4;
        private const uint FILE_MAP_WRITE = 0x0002;
        private const uint FILE_MAP_READ = 0x0004;
        private const uint PAGE_READWRITE = 0x04;

        static ProtocolMessage()
        {
            WM_PROTOCOL_DATA = RegisterWindowMessage(MessageIdentifier);
            Debug.WriteLine($"Registered message ID: {WM_PROTOCOL_DATA}");

            // Create shared memory
            sharedMemHandle = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, PAGE_READWRITE,
                0, MAX_URI_LENGTH, "DLKI_SharedMem");

            if (sharedMemHandle == IntPtr.Zero)
            {
                Debug.WriteLine($"Failed to create shared memory: {Marshal.GetLastWin32Error()}");
            }
        }

        public static void Initialize(Window window)
        {
            if (isInitialized) return;

            Debug.WriteLine("Initializing ProtocolMessage");
            mainWindow = window;
            mainWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);

            SetWindowText(mainWindowHandle, UniqueWindowClass);
            Debug.WriteLine($"Set window text for handle: {mainWindowHandle} to {UniqueWindowClass}");

            newWndProc = new WinProc(WindowProc);
            delegateHandle = GCHandle.Alloc(newWndProc);

            oldWndProc = SetWindowLongPtr(mainWindowHandle, GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(newWndProc));

            Debug.WriteLine($"Window procedure hooked. Old: {oldWndProc}, New: {Marshal.GetFunctionPointerForDelegate(newWndProc)}");
            isInitialized = true;
        }

        private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_PROTOCOL_DATA)
            {
                Debug.WriteLine("Received WM_PROTOCOL_DATA message");

                // Read URI from shared memory
                var viewPtr = MapViewOfFile(sharedMemHandle, FILE_MAP_READ, 0, 0, MAX_URI_LENGTH);
                if (viewPtr != IntPtr.Zero)
                {
                    try
                    {
                        var uri = Marshal.PtrToStringUni(viewPtr);
                        if (!string.IsNullOrEmpty(uri))
                        {
                            Debug.WriteLine($"Read URI from shared memory: {uri}");
                            mainWindow?.DispatcherQueue.TryEnqueue(() =>
                            {
                                ProcessProtocolUri(uri);
                            });
                        }
                    }
                    finally
                    {
                        UnmapViewOfFile(viewPtr);
                    }
                }

                return IntPtr.Zero;
            }

            return CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
        }

        private static void ProcessProtocolUri(string uri)
        {
            Debug.WriteLine($"Processing protocol URI: {uri}");
            try
            {
                URLHandler.ProcessUri(new Uri(uri));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing URI: {ex.Message}");
            }
        }

        public static void SendProtocolData(string uri)
        {
            Debug.WriteLine($"Sending protocol data: {uri}");

            // Write URI to shared memory
            var viewPtr = MapViewOfFile(sharedMemHandle, FILE_MAP_WRITE, 0, 0, MAX_URI_LENGTH);
            if (viewPtr != IntPtr.Zero)
            {
                try
                {
                    var bytes = System.Text.Encoding.Unicode.GetBytes(uri + "\0");
                    Marshal.Copy(bytes, 0, viewPtr, bytes.Length);

                    // Find and notify the main window
                    bool found = false;
                    EnumWindows((IntPtr hwnd, IntPtr lParam) =>
                    {
                        var windowText = new System.Text.StringBuilder(256);
                        GetWindowText(hwnd, windowText, windowText.Capacity);
                        Debug.WriteLine($"Checking window: {hwnd}, Text: {windowText}");

                        if (windowText.ToString() == UniqueWindowClass)
                        {
                            Debug.WriteLine($"Found main window, posting message to: {hwnd}");
                            bool success = PostMessage(hwnd, WM_PROTOCOL_DATA, IntPtr.Zero, IntPtr.Zero);
                            Debug.WriteLine($"PostMessage result: {success}");
                            found = true;
                            return false; // Stop enumeration
                        }
                        return true;
                    }, IntPtr.Zero);

                    if (!found)
                    {
                        Debug.WriteLine("Main window not found");
                    }
                }
                finally
                {
                    UnmapViewOfFile(viewPtr);
                }
            }
            else
            {
                Debug.WriteLine($"Failed to map shared memory view: {Marshal.GetLastWin32Error()}");
            }
        }

        public static void Cleanup()
        {
            if (mainWindowHandle != IntPtr.Zero && oldWndProc != IntPtr.Zero)
            {
                SetWindowLongPtr(mainWindowHandle, GWLP_WNDPROC, oldWndProc);
            }
            if (delegateHandle.IsAllocated)
            {
                delegateHandle.Free();
            }
            if (sharedMemHandle != IntPtr.Zero)
            {
                CloseHandle(sharedMemHandle);
            }
            isInitialized = false;
        }
    }
}