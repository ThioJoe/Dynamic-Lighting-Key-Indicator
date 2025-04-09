//--------------------------- Global Usings ------------------------------------------
// Set global aliases for commonly used types
global using DWORD = System.UInt32;        // 4 Bytes, aka uint, uint32
global using RGBTuple = (int R, int G, int B);

//global using VKey = System.Int32;         // VirtualKey

// Set default namespaces for certain types
global using Color = Windows.UI.Color;
global using DropShadow = Microsoft.UI.Composition.DropShadow;
global using Thickness = Microsoft.UI.Xaml.Thickness;
global using Colors = Microsoft.UI.Colors;
// -------------------------------
global using static Dynamic_Lighting_Key_Indicator.Definitions.WinEnums;
global using Dynamic_Lighting_Key_Indicator.Definitions;

// Global resource class
global using static Dynamic_Lighting_Key_Indicator.GlobalDefinitions;
//-------------------------------------------------------------------------------------

// ---- Local Standard Usings ----
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Text.Json.Serialization;
using System;
using Windows.UI.Text;
using System.Collections.Generic;
using Windows.System;
using System.Linq;
using System.Runtime.InteropServices;
// -------------------------------

// Set the default DllImport search paths to System32 to mitigate DLL hijacking attacks, though shouldn't be a
//      problem for system DLLs since well-known DLLs should already prioritize being loaded from System32. But rather be explicit
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace Dynamic_Lighting_Key_Indicator
{
    public class GlobalDefinitions
    {
        public static readonly SolidColorBrush DefaultFontColor = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
    }

    public static class Globals
    {
        private static bool _debugFileLoggingEnabled = false;
        public static bool DebugFileLoggingEnabled
        {
            get => _debugFileLoggingEnabled;
            set => _debugFileLoggingEnabled = value;
        }

        // Get the app's package version
        public static string AppVersion
        {
            get
            {
                var version = Windows.ApplicationModel.Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
        }
    }

    // ------------------ Global Enums ------------------
    public enum ToggleAbleKeys : Int32
    {
        NumLock = 0x90,
        CapsLock = 0x14,
        ScrollLock = 0x91,
    }

    public enum SpecialKeys : Int32
    {
        Playback = 0xB3,
        Null = 0x00,
    }

    public enum StateColorApply
    {
        On,
        Off,
        Both,
        Null
    }

    // ------------------ Global Classes ------------------
    public class DeviceStatusInfo
    {
        public SolidColorBrush MsgColor { get; set; }
        public string MsgPrefix { get; set; }
        public string MsgBody { get; set; }
        public FontWeight PrefixWeight { get; set; } = FontWeights.Bold;

        // Default system font so it will automatically use white/black for dark/light theme
        

        public DeviceStatusInfo(DeviceStatusInfo.Msg message, int? deviceCount = null, string? suffix = null)
        {
            switch (message)
            {
                case Msg.NoneFound:
                    MsgColor = new SolidColorBrush((Color)Colors.Red);
                    MsgPrefix = StatusMsgPrefix.Status;
                    MsgBody = StatusMsgBody.NoneFound;
                    break;

                case Msg.Waiting:
                    MsgColor = DefaultFontColor;
                    MsgPrefix = StatusMsgPrefix.Status;
                    MsgBody = StatusMsgBody.Waiting;
                    break;

                case Msg.NotAvailable:
                    MsgColor = new SolidColorBrush((Color)Colors.Red);
                    MsgPrefix = StatusMsgPrefix.Status;
                    MsgBody = StatusMsgBody.NotAvailable;
                    break;

                case Msg.Good:
                    MsgColor = new SolidColorBrush((Color)Colors.Green);
                    MsgPrefix = StatusMsgPrefix.Status;
                    MsgBody = StatusMsgBody.Good;
                    break;

                case Msg.Available:
                    MsgColor = DefaultFontColor;
                    MsgPrefix = StatusMsgPrefix.Status;
                    MsgBody = StatusMsgBody.Available;
                    MsgBody += deviceCount.ToString() ?? "Unknown";
                    break;

                case Msg.Empty:
                    MsgColor = DefaultFontColor;
                    MsgPrefix = StatusMsgPrefix.None;
                    MsgBody = StatusMsgBody.Empty;
                    break;

                case Msg.ErrorInitializing:
                    MsgColor = new SolidColorBrush((Color)Colors.Red);
                    MsgPrefix = StatusMsgPrefix.Status;
                    MsgBody = StatusMsgBody.ErrorInitializing;
                    MsgBody += suffix ?? "Unknown Device";
                    break;

                case Msg.NotKeyboard:
                    MsgColor = new SolidColorBrush((Color)Colors.Orange);
                    MsgPrefix = StatusMsgPrefix.Warning;
                    MsgBody = StatusMsgBody.NotKeyboard;
                    break;

                case Msg.AttachedNotConnected:
                    MsgColor = new SolidColorBrush((Color)Colors.Orange);
                    MsgPrefix = StatusMsgPrefix.Warning;
                    MsgBody = StatusMsgBody.AttachedNotConnected;
                    break;

                default:
                    MsgColor = DefaultFontColor;
                    MsgPrefix = StatusMsgPrefix.None;
                    MsgBody = StatusMsgBody.Empty;
                    break;
            }
        }

        public enum Msg
        {
            NoneFound, Waiting, Available, NotAvailable, Good, Empty, ErrorInitializing, NotKeyboard, AttachedNotConnected
        }

        public struct StatusMsgBody
        {
            internal const string NoneFound = "No elegible lighting devices found";
            internal const string Waiting = "Waiting - Click Enable to list available devices, then select one.";
            internal const string Available = $"Available Devices: "; // Append device names to this string
            internal const string NotAvailable = "Device attached but not controllable. See instructions below for how to enable background control.";
            internal const string Good = "Good";
            internal const string Empty = "";
            internal const string ErrorInitializing = "Error initializing LampArray: ";
            internal const string NotKeyboard = "Device is attached, but it doesn't seem to be a keyboard. This might not work.";
            internal const string AttachedNotConnected = "Saved device seems to have been disconnected. Try reconnecting.";
        }

        public struct StatusMsgPrefix
        {
            internal const string Status = "Status: ";
            internal const string Warning = "Warning: ";
            internal const string None = "";
        }

    } // End of DeviceStatusInfo class

    public class MonitoredKey
    {
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

        public bool IsOn() => KeyStatesHandler.FetchKeyState((int)key);

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

        public MonitoredKey(ToggleAbleKeys key)
        {
            this.key = key;
            this.offColor = UserConfig.DefaultStandardKeyColor;
            this.onColor = UserConfig.DefaultMonitoredKeyActiveColor;
            this.onColorTiedToStandard = false;
            this.offColorTiedToStandard = true;
        }

        public Color? GetColorObjCurrent()
        {
            return Color.FromArgb(255, (byte)(IsOn() ? onColor.R : offColor.R),
                                     (byte)(IsOn() ? onColor.G : offColor.G),
                                     (byte)(IsOn() ? onColor.B : offColor.B));
        }

        public Color? GetColorObjOff() => Color.FromArgb(255, (byte)offColor.R, (byte)offColor.G, (byte)offColor.B);

        public Color? GetColorObjOn()
        {
            return Color.FromArgb(255, (byte)onColor.R, (byte)onColor.G, (byte)onColor.B);
        }

        public static void DefineAllMonitoredKeysAndColors(List<MonitoredKey> monitorTogglekeys)
        {
            KeyStatesHandler.monitoredKeys = monitorTogglekeys;

            // Create a dictionary for faster lookups
            KeyStatesHandler.monitoredKeysDict = [];
            foreach (MonitoredKey key in monitorTogglekeys)
            {
                KeyStatesHandler.monitoredKeysDict.Add(key.key, key);
            }
        }

    } // End of MonitoredKey class

    //// Get list of all VirtualKey values, including custom ones that aren't in the standard enum, but are common
    //public static class ExtendedVirtualKeySet
    //{
    //    private static readonly HashSet<int> _allValues;

    //    static ExtendedVirtualKeySet()
    //    {
    //        _allValues = [];

    //        // Add all standard VirtualKey values
    //        foreach (VirtualKey vk in Enum.GetValues<VirtualKey>())
    //        {
    //            _allValues.Add((int)vk);
    //        }

    //        // Add custom OEM keys
    //        _allValues.Add(0xDB); // VK_OEM_4 - LeftBracket
    //        _allValues.Add(0xDD); // VK_OEM_6 - RightBracket
    //        _allValues.Add(0xDC); // VK_OEM_5 - Backslash
    //        _allValues.Add(0xBF); // VK_OEM_2 - ForwardSlash
    //        _allValues.Add(0xDE); // VK_OEM_7 - Quote
    //        _allValues.Add(0xE2); // VK_OEM_102 - Sometimes <> keys, sometimes \|
    //        _allValues.Add(0xBE); // VK_OEM_PERIOD  (On US keyboards, this is the .> key)
    //        _allValues.Add(0xBA); // VK_OEM_1 - Semicolon/Colon key
    //        _allValues.Add(0xBD); // VK_OEM_MINUS
    //        _allValues.Add(0xBB); // VK_OEM_PLUS
    //        _allValues.Add(0xC0); // VK_OEM_3 - Tilde/Grave/Backtick key
    //        _allValues.Add(0xBC); // VK_OEM_COMMA

    //    }

    //    public static IEnumerable<VirtualKey> GetAllKeys()
    //    {
    //        return _allValues.Select(v => (VirtualKey)v);
    //    }
    //}



} // End of Namespace

