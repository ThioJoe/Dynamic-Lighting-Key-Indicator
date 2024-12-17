using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace Dynamic_Lighting_Key_Indicator
{
    public class MyDefinitions
    {
        internal enum StateColorApply
        {
            On,
            Off,
            Both
        }

        // Default system font so it will automatically use white/black for dark/light theme
        public static SolidColorBrush DefaultFontColor = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];


        // --------------------------- DeviceStatusInfo ---------------------------

        public class DeviceStatusInfo
        {
            public SolidColorBrush MsgColor { get; set; }
            public string MsgPrefix { get; set; }
            public string MsgBody { get; set; }
            public FontWeight PrefixWeight { get; set; } = FontWeights.Bold;

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
                internal const string Waiting = "Waiting - Start device watcher to list available devices.";
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
        }

    } // --------------------------- End of MyDefinitions ---------------------------
}
