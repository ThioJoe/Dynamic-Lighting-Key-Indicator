//--------------------------- Global Usings ------------------------------------------
// Set global aliases for commonly used types
global using DWORD = System.UInt32;        // 4 Bytes, aka uint, uint32
global using RGBTuple = (int R, int G, int B);
global using VK = Dynamic_Lighting_Key_Indicator.ToggleAbleKeys;

global using Color = Windows.UI.Color;
global using DropShadow = Microsoft.UI.Composition.DropShadow;
// -------------------------------
global using static Dynamic_Lighting_Key_Indicator.Definitions.WinEnums;
global using Dynamic_Lighting_Key_Indicator.Definitions;

// Global resource class
global using static Dynamic_Lighting_Key_Indicator.GlobalDefinitions;
//-------------------------------------------------------------------------------------

// ---- Local Standard Usings ----
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Text.Json.Serialization;
using System;
using Windows.UI.Text;
using System.Collections.Generic;
// -------------------------------

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
    }

    // ------------------ Global Enums ------------------
    public enum ToggleAbleKeys : Int32
    {
        NumLock = 0x90,
        CapsLock = 0x14,
        ScrollLock = 0x91
    }
    public enum StateColorApply
    {
        On,
        Off,
        Both
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
                    MsgColor = new SolidColorBrush((Windows.UI.Color)Colors.Red);
                    MsgPrefix = StatusMsgPrefix.Status;
                    MsgBody = StatusMsgBody.NoneFound;
                    break;

                case Msg.Waiting:
                    MsgColor = DefaultFontColor;
                    MsgPrefix = StatusMsgPrefix.Status;
                    MsgBody = StatusMsgBody.Waiting;
                    break;

                case Msg.NotAvailable:
                    MsgColor = new SolidColorBrush((Windows.UI.Color)Colors.Red);
                    MsgPrefix = StatusMsgPrefix.Status;
                    MsgBody = StatusMsgBody.NotAvailable;
                    break;

                case Msg.Good:
                    MsgColor = new SolidColorBrush((Windows.UI.Color)Colors.Green);
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
                    MsgColor = new SolidColorBrush((Windows.UI.Color)Colors.Red);
                    MsgPrefix = StatusMsgPrefix.Status;
                    MsgBody = StatusMsgBody.ErrorInitializing;
                    MsgBody += suffix ?? "Unknown Device";
                    break;

                case Msg.NotKeyboard:
                    MsgColor = new SolidColorBrush((Windows.UI.Color)Colors.Orange);
                    MsgPrefix = StatusMsgPrefix.Warning;
                    MsgBody = StatusMsgBody.NotKeyboard;
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
            NoneFound, Waiting, Available, NotAvailable, Good, Empty, ErrorInitializing, NotKeyboard
        }

        public struct StatusMsgBody
        {
            internal const string NoneFound = "No elegible lighting devices found";
            internal const string Waiting = "Waiting - Click Enable to list available devices.";
            internal const string Available = $"Available Devices: "; // Append device names to this string
            internal const string NotAvailable = "Device attached but not controllable. See instructions below for how to enable background control.";
            internal const string Good = "Good";
            internal const string Empty = "";
            internal const string ErrorInitializing = "Error initializing LampArray: ";
            internal const string NotKeyboard = "Device is attached, but it doesn't seem to be a keyboard. This might not work.";
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

        public MonitoredKey(ToggleAbleKeys key, Windows.UI.Color onColor, Windows.UI.Color offColor, bool onColorTiedToStandard = false, bool offColorTiedToStandard = false)
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

        public Windows.UI.Color? GetColorObjCurrent()
        {
            return Windows.UI.Color.FromArgb(255, (byte)(IsOn() ? onColor.R : offColor.R),
                                     (byte)(IsOn() ? onColor.G : offColor.G),
                                     (byte)(IsOn() ? onColor.B : offColor.B));
        }

        public Windows.UI.Color? GetColorObjOff()
        {
            return Windows.UI.Color.FromArgb(255, (byte)offColor.R, (byte)offColor.G, (byte)offColor.B);
        }

        public Windows.UI.Color? GetColorObjOn()
        {
            return Windows.UI.Color.FromArgb(255, (byte)onColor.R, (byte)onColor.G, (byte)onColor.B);
        }

        public static void DefineAllMonitoredKeysAndColors(List<MonitoredKey> keys)
        {
            KeyStatesHandler.monitoredKeys = keys;

            // Create a dictionary for faster lookups
            KeyStatesHandler.monitoredKeysDict = [];
            foreach (MonitoredKey key in keys)
            {
                KeyStatesHandler.monitoredKeysDict.Add(key.key, key);
            }
        }

    } // End of MonitoredKey class

} // End of Namespace
