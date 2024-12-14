using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.System;

namespace Dynamic_Lighting_Key_Indicator
{
    internal static class ColorSetter
    {
        private static Windows.UI.Color _keyboardMainColor;
        private static LampArray? _currentDevice;
        private static List<int>? _monitoredIndices;

        public static Windows.UI.Color KeyboardMainColor => _keyboardMainColor;
        public static LampArray? CurrentDevice => _currentDevice;
        public static List<int>? MonitoredIndices => _monitoredIndices;
        public static Dictionary<KeyStatesHandler.ToggleAbleKeys, int> MonitoredKeyIndicesDict { get; set; } = [];
        public static List<int> NonMonitoredKeyIndices { get; set; } = []; // Maybe make this an array later


        // ------------- Define Main Color with wither RGBTuple or Windows.UI.Color -------------
        public static void DefineKeyboardMainColor(RGBTuple color)
        {
            _keyboardMainColor = RGBTuple_To_ColorObj(color);
        }
        public static void DefineKeyboardMainColor(Windows.UI.Color color)
        {
            _keyboardMainColor = color;
        }
        //------------------------------------------------------------------------------------------

        public static void SetCurrentDevice(LampArray? device)
        {
            _currentDevice = device;
        }

        public static void SetInitialDefaultKeyboardColor(LampArray lampArray)
        {
            lampArray.SetColor(KeyboardMainColor);
            ColorSetter.SetMonitoredKeysColor(KeyStatesHandler.monitoredKeys);
        }

        public static void SetColorsToKeyboard(LampArray? lampArray = null) // Defaults to the current device
        {
            if (lampArray == null)
            {
                if (CurrentDevice == null)
                    return;
                else
                    lampArray = CurrentDevice;
            }

            SetInitialDefaultKeyboardColor(lampArray);
            SetMonitoredKeysColor(KeyStatesHandler.monitoredKeys);
        }

        public static void SetMonitoredKeysColor(List<KeyStatesHandler.MonitoredKey> monitoredKeys, LampArray? lampArray = null)
        {
            if (lampArray == null)
            {
                if (CurrentDevice == null)
                    throw new ArgumentNullException(nameof(lampArray), "LampArray must be defined.");
                else
                    lampArray = CurrentDevice;
            }

            Windows.UI.Color[] colors = new Windows.UI.Color[monitoredKeys.Count];
            VirtualKey[] keys = new VirtualKey[monitoredKeys.Count];

            // Build arrays of colors and keys
            foreach (var key in monitoredKeys)
            {
                VirtualKey vkCode = (VirtualKey)key.key;
                Windows.UI.Color color;

                if (key.IsOn())
                {
                    color = Windows.UI.Color.FromArgb(255, (byte)key.onColor.R, (byte)key.onColor.G, (byte)key.onColor.B);
                }
                else
                {
                    color = Windows.UI.Color.FromArgb(255, (byte)key.offColor.R, (byte)key.offColor.G, (byte)key.offColor.B);
                }

                int index = monitoredKeys.IndexOf(key);
                colors[index] = color;
                keys[index] = vkCode;
            }

            lampArray.SetColorsForKeys(colors, keys);
        }

        public static void SetNonMonitoredKeysColor(Windows.UI.Color color, LampArray? lampArray = null)
        {
            if (lampArray == null)
            {
                if (CurrentDevice == null)
                    throw new ArgumentNullException(nameof(lampArray), "LampArray must be defined.");
                else
                    lampArray = CurrentDevice;
            }

            // Build corresponding arrays of colors and keys to pass in.
            // The colors are all the same since these are all non-monitored keys so will always use the standard/default color
            Windows.UI.Color[] colors = Enumerable.Repeat(color, NonMonitoredKeyIndices.Count).ToArray(); // Repeat the color into an array
            int[] keys = NonMonitoredKeyIndices.ToArray();
            lampArray.SetColorsForIndices(colors, keys);
        }

        public static void BuildMonitoredKeyIndicesDict(LampArray lampArray)
        {
            MonitoredKeyIndicesDict = new Dictionary<KeyStatesHandler.ToggleAbleKeys, int>();
            NonMonitoredKeyIndices = new List<int>();

            // Build the arrays of colors and keys
            for (int i = 0; i < lampArray.LampCount; i++)
            {
                // Add all indices to the non-monitored list, then we'll remove the monitored ones
                NonMonitoredKeyIndices.Add(i);
            }

            // Get the indices of the monitored keys using lampArray's built in methods, and remove them from the non-monitored list
            foreach (var key in KeyStatesHandler.monitoredKeys)
            {
                VirtualKey vkCode = (VirtualKey)key.key;
                int[] indices = lampArray.GetIndicesForKey(vkCode);
                foreach (int index in indices)
                {
                    MonitoredKeyIndicesDict.Add(key.key, index);
                    NonMonitoredKeyIndices.Remove(index);
                }
            }
        }

        public static Windows.UI.Color RGBTuple_To_ColorObj(RGBTuple color)
        {
            return Windows.UI.Color.FromArgb(255, (byte)color.R, (byte)color.G, (byte)color.B);
        }

        // ------------------------------------------- Unused But Maybe Useful Later ------------------------------------------------------------

        #region Unused
        public static void SetSpecificKeysToColor(LampArray lampArray, VirtualKey[] keys, Windows.UI.Color color)
        {
            Windows.UI.Color[] colors = Enumerable.Repeat(color, keys.Length).ToArray();
            lampArray.SetColorsForKeys(colors, keys);
            lampArray.BrightnessLevel = 1.0f;
        }

        public static void SetKeyboardColorExceptMonitoredKeys(List<KeyStatesHandler.MonitoredKey> monitoredKeys, LampArray? lampArray = null)
        {
            if (lampArray == null)
            {
                if (CurrentDevice == null)
                {
                    throw new ArgumentNullException(nameof(lampArray), "LampArray must be defined.");
                }
                else
                {
                    lampArray = CurrentDevice;
                }
            }

            List<int>? monitoredKeyIndices = MonitoredIndices;
            monitoredKeyIndices ??= UpdateMonitoredLampArrayIndices(lampArray); // If the monitored indices are null, update them

            Windows.UI.Color[] colors = new Windows.UI.Color[lampArray.LampCount - monitoredKeys.Count];
            int[] keys = new int[lampArray.LampCount - monitoredKeys.Count];

            // Build the arrays of colors and keys
            for (int i = 0; i < lampArray.LampCount; i++)
            {
                if (!monitoredKeyIndices.Contains(i))
                {
                    colors[i] = KeyboardMainColor;
                    keys[i] = i;
                }
            }

            lampArray.SetColorsForIndices(colors, keys);
        }

        // Use this info to set the colors of all the other keys not being monitored, so we don't cause the monitored keys to flicker
        public static List<int> UpdateMonitoredLampArrayIndices(LampArray lampArray)
        {
            List<int> monitoredIndices = [];
            foreach (var key in KeyStatesHandler.monitoredKeys)
            {
                VirtualKey virtualKey = (VirtualKey)key.key;
                monitoredIndices.AddRange(lampArray.GetIndicesForKey(virtualKey));
            }
            _monitoredIndices = monitoredIndices;

            return monitoredIndices;

        }
        #endregion
    }
}
