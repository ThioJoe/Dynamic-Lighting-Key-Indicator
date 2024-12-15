using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.UI;
using static Dynamic_Lighting_Key_Indicator.WinEnums;

namespace Dynamic_Lighting_Key_Indicator
{
    internal static class KeyStatesHandler
    {
        public static List<MonitoredKey> monitoredKeys = [];

        public static void DefineAllMonitoredKeysAndColors(List<MonitoredKey> keys)
        {
            monitoredKeys = keys;
        }

        public class MonitoredKey
        {
            // ------ Properties ------
            [JsonInclude]
            public ToggleAbleKeys key;
            [JsonInclude]
            public RGBTuple offColor;
            [JsonInclude]
            public RGBTuple onColor;
            [JsonInclude]
            public bool onColorTiedToStandard = false;
            [JsonInclude]
            public bool offColorTiedToStandard = false;
            public bool IsOn() => FetchKeyState((int)key);

            // ------ Constructors ------
            [JsonConstructor]
            public MonitoredKey(ToggleAbleKeys key, RGBTuple onColor, RGBTuple offColor, bool onColorTiedToStandard = false, bool offColorTiedToStandard = false)
            {
                this.key = key;
                this.offColor = offColor;
                this.onColor = onColor;
                this.onColorTiedToStandard = onColorTiedToStandard;
                this.offColorTiedToStandard = offColorTiedToStandard;
            }

            public MonitoredKey(ToggleAbleKeys key, Color onColor, Color offColor, bool onColorTiedToStandard = false, bool offColorTiedToStandard = false)
            {
                this.key = key;
                this.onColor = (onColor.R, onColor.G, onColor.B);
                this.offColor = (offColor.R, offColor.G, offColor.B);
                this.onColorTiedToStandard = onColorTiedToStandard;
                this.offColorTiedToStandard = offColorTiedToStandard;
            }

            // Default constructor
            public MonitoredKey(ToggleAbleKeys key)
            {
                this.key = key;
                this.offColor = UserConfig.DefaultStandardKeyColor;
                this.onColor = UserConfig.DefaultMonitoredKeyActiveColor;
                this.onColorTiedToStandard = false;
                this.offColorTiedToStandard = true;
            }

            //------ Methods ------
            public Windows.UI.Color? GetColorObjCurrent()
            {
                return Windows.UI.Color.FromArgb(255, (byte)(IsOn() ? onColor.R : offColor.R), (byte)(IsOn() ? onColor.G : offColor.G), (byte)(IsOn() ? onColor.B : offColor.B));
            }

            public Windows.UI.Color GetColorObjOff()
            {
                return Windows.UI.Color.FromArgb(255, (byte)offColor.R, (byte)offColor.G, (byte)offColor.B);
            }

            public Windows.UI.Color GetColorObjOn()
            {
                return Windows.UI.Color.FromArgb(255, (byte)onColor.R, (byte)onColor.G, (byte)onColor.B);
            }
        }

        public enum ToggleAbleKeys : Int32
        {
            NumLock = 0x90,
            CapsLock = 0x14,
            ScrollLock = 0x91
        }

        // Win32 API imports
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hInstance, uint threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int keyCode);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelKeyboardProc? _proc;
        private static IntPtr _hookID = IntPtr.Zero;
        public static bool hookIsActive = false;

        public static void InitializeHookAndCallback()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);

            if (_hookID == IntPtr.Zero)
            {
                throw new Exception("Failed to set hook.");
            }
            else
            {
                hookIsActive = true;
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            // Get the current module, throw an exception if it fails
            using Process? curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using ProcessModule? curModule = (curProcess?.MainModule) ?? throw new Exception("Failed to get current module."); 

            return SetWindowsHookEx((int)KeyboardHook.WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Get the data from the struct as an object
                KBDLLHOOKSTRUCT kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vkCode = (int)kbd.vkCode;
                LowLevelKeyboardHookFlags flags = kbd.flags;

                // Check if the key presses was one of the monitored keys
                foreach (var mk in monitoredKeys)
                {
                    if ((int)mk.key == vkCode && flags.HasFlag(LowLevelKeyboardHookFlags.KeyUp))
                    {
                        Task.Run(() => ColorSetter.SetSingleMonitorKeyColor_ToKeyboard(mk));
                        break;
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public static bool FetchKeyState(int vkCode)
        {
            return (GetKeyState(vkCode) & 1) == 1;
        }

        // Function to stop the hook
        public static void StopHook()
        {
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
            hookIsActive = false;
        }

        // Returned as pointer in the lparam of the hook callback
        // See: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public DWORD vkCode;          // Virtual key code
            public DWORD scanCode;
            public LowLevelKeyboardHookFlags flags;
            public DWORD time;
            public IntPtr dwExtraInfo;
        }

        [Flags]
        private enum LowLevelKeyboardHookFlags : uint
        {
            Extended = 0x01,             // Bit 0: Extended key (e.g. function key or numpad)
            LowerILInjected = 0x02,      // Bit 1: Injected from lower integrity level process
            Injected = 0x10,             // Bit 4: Injected from any process
            AltDown = 0x20,              // Bit 5: ALT key pressed
            KeyUp = 0x80                 // Bit 7: Key being released (transition state)
                                         // Bits 2-3, 6 are reserved
        }
    }
}
