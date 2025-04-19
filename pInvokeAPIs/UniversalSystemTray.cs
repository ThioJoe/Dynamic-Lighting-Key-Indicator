using System;
using System.Diagnostics;
using System.Drawing; // Required for Icon
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading; // Required for WNDPROC delegate registration context

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace ThioWinUtils // Change this to your desired namespace

{
    /// <summary>
    /// Manages a system tray icon using P/Invoke.
    /// Allows for use with or without an existing application window.
    /// </summary>
    public class SystemTray : IDisposable
    {
        // Configuration passed via constructor
        private readonly Icon _icon;
        private readonly string _tooltipText;
        private readonly Action? _restoreAction;
        private readonly Action<IntPtr, SystemTray>? _createContextMenuAction;
        private readonly IntPtr _hwndInput;

        // Internal state
        private NOTIFYICONDATAW _notifyIconData;
        private IntPtr _hwnd; // The handle of the window receiving messages (either input or created)
        private bool _isIconAdded = false;
        private bool _isHiddenWindowCreated = false;
        private WndProcDelegate? _newWndProc;
        private IntPtr _defaultWndProc = IntPtr.Zero;
        private uint _taskbarCreatedMessageId;
        private const string HiddenWindowClassName = "SystemTrayHiddenWindow";
        private GCHandle _wndProcGCHandle; // Keep the delegate alive

        // Delegates
        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam);

        #region PInvoke Declarations

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATAW
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public NIF uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion; // NOTIFYICON_VERSION_4 = 4
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public NIIF dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        // ShowWindow parameter values
        // See: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindow
        private enum nCmdShow : int
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_NORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_MAXIMIZE = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10
        }

        [Flags]
        private enum NIF : uint
        {
            MESSAGE = 0x00000001,
            ICON = 0x00000002,
            TIP = 0x00000004,
            STATE = 0x00000008,
            INFO = 0x00000010,
            GUID = 0x00000020,
            REALTIME = 0x00000040,
            SHOWTIP = 0x00000080
        }

        [Flags]
        private enum NIIF : uint
        {
            NONE = 0x00000000,
            INFO = 0x00000001,
            WARNING = 0x00000002,
            ERROR = 0x00000003,
            USER = 0x00000004,
            NOSOUND = 0x00000010,
            LARGE_ICON = 0x00000020,
            RESPECT_QUIET_TIME = 0x00000080,
            ICON_MASK = 0x0000000F
        }

        private enum NIM : uint
        {
            ADD = 0x00000000,
            MODIFY = 0x00000001,
            DELETE = 0x00000002,
            SETFOCUS = 0x00000003,
            SETVERSION = 0x00000004
        }

        private enum WM : uint
        {
            CLOSE = 0x0010,
            DESTROY = 0x0002,
            LBUTTONUP = 0x0202,
            RBUTTONUP = 0x0205,
            USER = 0x0400 // Application-defined messages start here
        }

        // Custom message for tray icon events
        private const uint WM_TRAYICON = (uint)WM.USER + 1;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ShowWindow(IntPtr hWnd, nCmdShow nCmdShow);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Shell_NotifyIcon(NIM dwMessage, ref NOTIFYICONDATAW lpData);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
           uint dwExStyle,
           string lpClassName,
           string lpWindowName,
           uint dwStyle,
           int x,
           int y,
           int nWidth,
           int nHeight,
           IntPtr hWndParent,
           IntPtr hMenu,
           IntPtr hInstance,
           IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        // Wrapper to handle 32/64 bit pointer size differences for SetWindowLong/GetWindowLong
        private const int GWLP_WNDPROC = -4;

        private static IntPtr SetWindowPointer(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) // 64-bit
                return SetWindowLongPtr(hWnd, nIndex, dwNewLong);
            else // 32-bit
                return new IntPtr(SetWindowLong(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private static IntPtr GetWindowPointer(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8) // 64-bit
                return GetWindowLongPtr(hWnd, nIndex);
            else // 32-bit
                return GetWindowLong(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", CharSet = CharSet.Auto)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc; // Needs to be IntPtr for GetFunctionPointerForDelegate
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        #endregion PInvoke Declarations

        /// <summary>
        /// Creates a new SystemTray instance.
        /// </summary>
        /// <param name="icon">The icon to display in the tray.</param>
        /// <param name="tooltipText">The tooltip text for the icon.</param>
        /// <param name="createContextMenuAction">Action to execute on right-click to show a context menu. Can be null.</param>
        /// /// <param name="restoreAction">Action to execute on left-click (e.g., show window). If null, will default to showing the hwndInput window if provided.</param>
        /// <param name="hwndInput">Optional handle of an existing window to receive messages. If IntPtr.Zero, a hidden window is created.</param>
        public SystemTray(
             Icon icon,
             string tooltipText,
             Action<IntPtr, SystemTray>? createContextMenuAction,
             Action? restoreAction = null,
             IntPtr hwndInput = default
            )
        {
            _icon = icon ?? throw new ArgumentNullException(nameof(icon));
            _tooltipText = tooltipText ?? string.Empty;
            _restoreAction = restoreAction;
            _createContextMenuAction = createContextMenuAction;
            _hwndInput = hwndInput; // Can be IntPtr.Zero
        }

        /// <summary>
        /// Initializes the system tray icon, creating or subclassing the window as needed.
        /// </summary>
        /// <exception cref="InvalidOperationException">If initialization fails.</exception>
        public void Initialize()
        {
            // Ensure we run this on an STA thread if creating a window
            // If called from a non-STA thread, window creation/messaging can fail.
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Debug.WriteLine("Warning: SystemTray.Initialize called from a non-STA thread. Unexpected behavior may occur.");
                // Consider throwing an exception or forcing STA if required by your app context.
                // Forcing STA usually involves starting a new thread.
            }


            // Prepare the shared delegate instance
            _newWndProc = WndProc;
            // Pin the delegate to prevent garbage collection until Dispose
            _wndProcGCHandle = GCHandle.Alloc(_newWndProc);


            if (_hwndInput == IntPtr.Zero)
            {
                // --- Create a hidden window ---
                IntPtr hInstance = GetModuleHandle(null);

                // Register the window class
                WNDCLASSEX wc = new WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_newWndProc),
                    hInstance = hInstance,
                    lpszClassName = HiddenWindowClassName,
                    hIcon = IntPtr.Zero, // No icon for hidden window
                    hCursor = IntPtr.Zero, // No cursor needed
                    hbrBackground = IntPtr.Zero, // No background needed
                    cbClsExtra = 0,
                    cbWndExtra = 0,
                    style = 0,
                    lpszMenuName = string.Empty,
                    hIconSm = IntPtr.Zero
                };

                ushort classAtom = RegisterClassEx(ref wc);
                if (classAtom == 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    // Error 1410: ERROR_CLASS_ALREADY_EXISTS - This is okay if another instance registered it.
                    if (error != 1410)
                    {
                        CleanUpResources(); // Unpin delegate if registered
                        throw new InvalidOperationException($"Failed to register hidden window class. Error code: {error}");
                    }
                    Debug.WriteLine("Hidden window class already registered by another instance.");
                }


                // Create the hidden window. Use WS_OVERLAPPED for a standard window that can receive messages.
                // Using HWND_MESSAGE (-3) for message-only windows sometimes has limitations with tray icons.
                _hwnd = CreateWindowEx(
                    0, // Optional window styles.
                    HiddenWindowClassName, // Class name
                    "SystemTrayHiddenWindow_" + Guid.NewGuid(), // Unique window name
                    0, // Window style (0 = no visual elements, not WS_VISIBLE)
                    0, 0, 0, 0, // Position and size (not relevant for hidden)
                    IntPtr.Zero, // Parent window (none) - changed from HWND_MESSAGE
                    IntPtr.Zero, // Menu (none)
                    hInstance, // Instance handle
                    IntPtr.Zero // Additional application data
                );


                if (_hwnd == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    CleanUpResources(); // Clean up GCHandle and potentially class registration
                    throw new InvalidOperationException($"Failed to create hidden window. Error code: {error}");
                }
                _isHiddenWindowCreated = true;
                Debug.WriteLine($"Hidden window created successfully. Handle: {_hwnd}");
            }
            else
            {
                // --- Subclass the existing window ---
                _hwnd = _hwndInput;
                Debug.WriteLine($"Using provided window handle: {_hwnd}");

                // Get the default window proc
                _defaultWndProc = GetWindowPointer(_hwnd, GWLP_WNDPROC);
                if (_defaultWndProc == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    CleanUpResources(); // Unpin delegate
                    throw new InvalidOperationException($"Failed to get default window procedure for handle {_hwnd}. Error code: {error}");
                }
                Debug.WriteLine($"Original WndProc pointer: {_defaultWndProc}");


                // Set the new window proc
                IntPtr newWndProcPtr = Marshal.GetFunctionPointerForDelegate(_newWndProc);
                IntPtr previousWndProc = SetWindowPointer(_hwnd, GWLP_WNDPROC, newWndProcPtr);
                if (previousWndProc != _defaultWndProc && previousWndProc != IntPtr.Zero)
                {
                    // This can happen if something else subclassed between Get and Set.
                    // Log it, but proceed. We might need to chain differently if this is common.
                    Debug.WriteLine($"Warning: Window procedure changed between GetWindowLongPtr and SetWindowLongPtr. Original: {_defaultWndProc}, Replaced: {previousWndProc}");
                    // We should ideally restore 'previousWndProc' on exit now, but we captured '_defaultWndProc'.
                    // For simplicity, we'll stick with restoring '_defaultWndProc', assuming it's the ultimate base.
                }
                else if (previousWndProc == IntPtr.Zero && Marshal.GetLastWin32Error() != 0) // SetWindowLongPtr returns 0 on failure
                {
                    int error = Marshal.GetLastWin32Error();
                    CleanUpResources(); // Unpin delegate
                    throw new InvalidOperationException($"Failed to set new window procedure for handle {_hwnd}. Error code: {error}");
                }
                Debug.WriteLine("Window subclassed successfully.");
            }


            // Register the TaskbarCreated message (needed if Explorer restarts)
            RegisterTaskbarCreatedMessage();

            // Initialize and add the notification icon
            InitializeAndAddNotifyIcon();
        }


        private void RegisterTaskbarCreatedMessage()
        {
            _taskbarCreatedMessageId = RegisterWindowMessage("TaskbarCreated");
            if (_taskbarCreatedMessageId == 0)
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Warning: Failed to register TaskbarCreated message. Icon might not reappear if explorer crashes. Error: {error}");
            }
            else
            {
                Debug.WriteLine($"TaskbarCreated message registered with ID: {_taskbarCreatedMessageId}");
            }
        }

        private void InitializeAndAddNotifyIcon()
        {
            _notifyIconData = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = _hwnd, // Use the determined window handle
                uID = 1, // Unique ID for this icon
                uFlags = NIF.ICON | NIF.MESSAGE | NIF.TIP,
                uCallbackMessage = WM_TRAYICON, // Custom message ID
                szTip = _tooltipText,
                hIcon = _icon.Handle // Use handle from provided Icon object
            };

            // Add the icon
            if (Shell_NotifyIcon(NIM.ADD, ref _notifyIconData))
            {
                _isIconAdded = true;
                // Set version 4 for modern features (required for correct message behavior)
                _notifyIconData.uVersion = 4; // NOTIFYICON_VERSION_4
                if (!Shell_NotifyIcon(NIM.SETVERSION, ref _notifyIconData))
                {
                    Debug.WriteLine($"Warning: Failed to set tray icon version 4. Error: {Marshal.GetLastWin32Error()}");
                }
                Debug.WriteLine("System tray icon added successfully.");
            }
            else
            {
                _isIconAdded = false;
                int error = Marshal.GetLastWin32Error();
                // ERROR_TIMEOUT (1460) can occur if the taskbar isn't ready.
                Debug.WriteLine($"Error: Failed to add tray icon initially. Error: {error}");
                // Consider adding retry logic here if needed
            }
        }

        private void RecreateNotifyIcon()
        {
            Debug.WriteLine("Taskbar created/restarted. Attempting to recreate tray icon.");
            if (_isIconAdded) // Only recreate if it was successfully added before
            {
                // Remove the old icon first (best practice) - ignore errors as it might already be gone
                Shell_NotifyIcon(NIM.DELETE, ref _notifyIconData);
                _isIconAdded = false; // Mark as removed

                // Re-initialize and add the icon
                InitializeAndAddNotifyIcon();
            }
            else
            {
                Debug.WriteLine("Skipping icon recreation as it wasn't successfully added previously.");
            }
        }

        /// <summary>
        /// Calls the exit action provided during construction.
        /// The caller is responsible for actual application shutdown logic.
        /// </summary>
        public void OnExit()
        {
            Debug.WriteLine("Exit triggered via SystemTray.");
            // Clean up resources (icon, window proc) before calling the external exit logic
            Dispose();
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam)
        {
            // Filter messages for our custom tray icon message
            if (msg == WM_TRAYICON && hwnd == _hwnd) // Ensure it's for our window
            {
                // Extract the mouse message from the lower word of lParam
                uint mouseMessage = (uint)lParam.ToInt64() & 0xFFFF;

                switch ((WM)mouseMessage)
                {
                    case WM.LBUTTONUP:
                        Debug.WriteLine("Tray icon left-clicked.");
                        // Call the restore action if provided
                        _restoreAction?.Invoke();
                        return IntPtr.Zero; // Handled

                    case WM.RBUTTONUP:
                        Debug.WriteLine("Tray icon right-clicked.");
                        // Call the context menu action if provided
                        _createContextMenuAction?.Invoke(hwnd, this); // Pass HWND and instance
                        return IntPtr.Zero; // Handled
                }
            }
            else if (_taskbarCreatedMessageId != 0 && msg == _taskbarCreatedMessageId)
            {
                // Handle TaskbarCreated message
                Debug.WriteLine("Received TaskbarCreated message.");
                RecreateNotifyIcon();
                // Fall through to default processing, as other apps might need this message too.
            }
            else if (_isHiddenWindowCreated && hwnd == _hwnd && msg == (uint)WM.DESTROY)
            {
                // Handle destruction of our hidden window if necessary
                Debug.WriteLine("Hidden window received WM_DESTROY.");
                // If the app didn't call Dispose explicitly, we might need cleanup here, but ideally Dispose() handles it before the window is destroyed externally.
                // PostQuitMessage(0); // Example if this window drove a message loop
                return IntPtr.Zero;
            }
            else if (_isHiddenWindowCreated && hwnd == _hwnd && msg == (uint)WM.CLOSE)
            {
                Debug.WriteLine("Hidden window received WM_CLOSE. Triggering exit sequence.");
                // The hidden window received a close request (e.g., from system shutdown or task manager).
                OnExit();
                return IntPtr.Zero; // We initiated shutdown.
            }

            // --- Default Processing ---
            // If we created the hidden window, use DefWindowProc for unhandled messages.
            // If we subclassed, call the original window procedure.
            if (_isHiddenWindowCreated && hwnd == _hwnd)
            {
                return DefWindowProc(hwnd, msg, wParam, lParam);
            }
            else if (_defaultWndProc != IntPtr.Zero) // Check if we subclassed
            {
                return CallWindowProc(_defaultWndProc, hwnd, msg, wParam, lParam);
            }
            else
            {
                // Should not happen if initialized correctly, but provide a fallback.
                return DefWindowProc(hwnd, msg, wParam, lParam);
            }
        }


        #region IDisposable Implementation

        private bool _disposed = false;

        /// <summary>
        /// Cleans up resources: removes tray icon, restores original window procedure,
        /// destroys hidden window if created, and disposes the icon.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // Prevent finalizer from running
            //CleanUpResources() // necessary?
        }

        /// <summary>
        /// Performs the actual cleanup.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            Debug.WriteLine($"Disposing SystemTray (disposing managed: {disposing})");


            // --- Cleanup unmanaged resources ---

            // 1. Remove the notification icon
            if (_isIconAdded && _hwnd != IntPtr.Zero)
            {
                Debug.WriteLine($"Removing notification icon (ID: {_notifyIconData.uID})");
                // Prepare data for delete
                var deleteData = new NOTIFYICONDATAW
                {
                    cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                    hWnd = _hwnd,
                    uID = _notifyIconData.uID,
                    uFlags = 0 // Flags not needed for delete
                };


                if (!Shell_NotifyIcon(NIM.DELETE, ref deleteData))
                {
                    // This can fail if explorer is not running or the icon was already removed.
                    int error = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"Warning: Failed to remove tray icon. Error: {error}");
                }
                _isIconAdded = false;
            }


            // 2. Restore the original window procedure if we subclassed
            if (_defaultWndProc != IntPtr.Zero && _hwndInput != IntPtr.Zero && _hwnd == _hwndInput)
            {
                Debug.WriteLine($"Restoring original window procedure for {_hwnd}");
                IntPtr currentWndProc = GetWindowPointer(_hwnd, GWLP_WNDPROC);
                // Only restore if our WndProc is still set
                if (currentWndProc == Marshal.GetFunctionPointerForDelegate(_newWndProc))
                {
                    SetWindowPointer(_hwnd, GWLP_WNDPROC, _defaultWndProc);
                }
                else
                {
                    Debug.WriteLine("Warning: Window procedure was not ours; skipping restoration.");
                }
                _defaultWndProc = IntPtr.Zero; // Mark as restored
            }


            // 3. Destroy the hidden window if we created it
            if (_isHiddenWindowCreated && _hwnd != IntPtr.Zero)
            {
                Debug.WriteLine($"Destroying hidden window {_hwnd}");
                if (!DestroyWindow(_hwnd))
                {
                    int error = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"Warning: Failed to destroy hidden window. Error: {error}");
                }
                _hwnd = IntPtr.Zero; // Mark as destroyed
                _isHiddenWindowCreated = false;


                // 4. Unregister the window class (only if we created the window)
                Debug.WriteLine($"Unregistering window class {HiddenWindowClassName}");
                if (!UnregisterClass(HiddenWindowClassName, GetModuleHandle(null)))
                {
                    int error = Marshal.GetLastWin32Error();
                    // Error 1411: ERROR_CLASS_DOES_NOT_EXIST (Maybe another instance unregistered it)
                    // Error 1412: ERROR_CLASS_HAS_WINDOWS (Shouldn't happen if DestroyWindow succeeded)
                    if (error != 1411)
                    {
                        Debug.WriteLine($"Warning: Failed to unregister hidden window class. Error: {error}");
                    }
                }
            }


            // --- Cleanup managed resources (only if disposing is true) ---
            if (disposing)
            {
                // Dispose the managed Icon object passed in constructor
                // Let the caller manage the icon lifecycle if they need it elsewhere.
                // _icon?.Dispose(); // Consider if the tray should own the icon disposal. Generally, no.
            }

            // --- Always cleanup GCHandle ---
            CleanUpResources();


            _disposed = true;
        }

        /// <summary>
        /// Hides the application window associated with this tray icon.
        /// This method only has an effect if a valid window handle (hwndInput)
        /// was provided during construction.
        /// </summary>
        public void MinimizeToTray()
        {
            // Only hide if we have a specific window handle from the user.
            if (_hwndInput != IntPtr.Zero)
            {
                Debug.WriteLine($"Minimizing window {_hwndInput} to tray (hiding).");
                // Use _hwndInput here, not _hwnd
                ShowWindow(_hwndInput, nCmdShow.SW_HIDE);
            }
            else
            {
                Debug.WriteLine("MinimizeToTray called, but no associated application window handle exists.");
            }
        }

        /// <summary>
        /// Shows and activates the application window associated with this tray icon.
        /// This method only has an effect if a valid window handle (hwndInput)
        /// was provided during construction.
        /// </summary>
        public void RestoreFromTray()
        {
            // Only restore if we have a specific window handle from the user.
            if (_hwndInput != IntPtr.Zero)
            {
                Debug.WriteLine($"Restoring window {_hwndInput} from tray.");
                ShowWindow(_hwndInput, nCmdShow.SW_SHOWNORMAL);
                SetForegroundWindow(_hwndInput); // Attempt to bring it to the foreground
            }
            else
            {
                Debug.WriteLine("RestoreFromTray called, but no associated application window handle exists.");
                // Consider invoking _restoreAction here if you want the left-click
                // behavior to be callable programmatically even without a window handle?
                _restoreAction?.Invoke();
            }
        }

        private void CleanUpResources()
        {
            // Unpin the delegate
            if (_wndProcGCHandle.IsAllocated)
            {
                Debug.WriteLine("Freeing GCHandle for WndProc delegate.");
                _wndProcGCHandle.Free();
            }
        }


        // Finalizer (just in case Dispose is not called)
        ~SystemTray()
        {
            Debug.WriteLine("SystemTray finalizer called. Dispose was likely not called.");
            Dispose(false); // Cleanup unmanaged resources
        }

        #endregion IDisposable Implementation
    }
}