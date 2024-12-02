using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.System;

namespace Dynamic_Lighting_Key_Indicator
{
    internal static class ColorSetter
    {
        private static Windows.UI.Color _keyboardMainColor;
        private static LampArray _currentDevice;
        private static List<int> _monitoredIndices;

        public static Windows.UI.Color KeyboardMainColor => _keyboardMainColor;
        public static LampArray CurrentDevice => _currentDevice;
        public static List<int> MonitoredIndices => _monitoredIndices;

        public static void DefineKeyboardMainColor_FromRGB(int R, int G, int B)
        {
            _keyboardMainColor = Windows.UI.Color.FromArgb(255, (byte)R, (byte)G, (byte)B);
        }

        public static void DefineKeyboardMainColor_FromNameAndBrightness(Windows.UI.Color color, int brightnessPercent)
        {
            if (brightnessPercent < 0 || brightnessPercent > 100)
            {
                throw new ArgumentOutOfRangeException("Brightness must be between 0 and 100.");
            }

            int CalcNewChannelValue(int channelValue)
            {
                float newVal = channelValue * brightnessPercent / 100;
                int newValInt = (int)Math.Round(newVal); // Round to ensure within valid range to pass in to Color.FromArgb

                if (newValInt < 0)
                    return 0;
                else if (newValInt > 255)
                    return 255;
                else
                    return newValInt;
            }

            int r = CalcNewChannelValue(color.R);
            int g = CalcNewChannelValue(color.G);
            int b = CalcNewChannelValue(color.B);

            _keyboardMainColor = brightnessPercent == 100 ? color : Windows.UI.Color.FromArgb(255, (byte)r, (byte)g, (byte)b);

        }

        public static void SetCurrentDevice(LampArray device)
        {
            _currentDevice = device;
        }

        public static void SetWasdPatternToLampArray(LampArray lampArray)
        {
            // Set a background color of blue for the whole LampArray.
            lampArray.SetColor(Colors.Blue);

            // Highlight the WASD keys in white, if the LampArray supports addressing its Lamps using a virtual key to Lamp mapping.
            // This is typically found on keyboard LampArrays. Other LampArrays will not usually support virtual key based lighting.
            if (lampArray.SupportsVirtualKeys)
            {
                Windows.UI.Color[] colors = Enumerable.Repeat(Colors.White, 4).ToArray();
                VirtualKey[] virtualKeys = { VirtualKey.W, VirtualKey.A, VirtualKey.S, VirtualKey.D };

                lampArray.SetColorsForKeys(colors, virtualKeys);
            }
        }

        public static void SetInitialDefaultKeyboardColor(LampArray lampArray)
        {
            lampArray.SetColor(KeyboardMainColor);
        }

        public static void SetKeyboardColorExceptMonitoredKeys(List<KeyStatesHandler.MonitoredKey> monitoredKeys, LampArray lampArray = null)
        {

            if (lampArray == null)
            {
                if (CurrentDevice == null)
                {
                    throw new ArgumentNullException("LampArray must be defined.");
                }
                else
                {
                    lampArray = CurrentDevice;
                }
            }

            List<int> monitoredKeyIndices = MonitoredIndices;

            if (monitoredKeyIndices == null)
            {
                monitoredKeyIndices = UpdateMonitoredLampArrayIndices(lampArray);
            }


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

        public static void SetMonitoredKeysColor(List<KeyStatesHandler.MonitoredKey> monitoredKeys, LampArray lampArray = null)
        {
            if (lampArray == null)
            {
                if (CurrentDevice == null)
                {
                    throw new ArgumentNullException("LampArray must be defined.");
                }
                else
                {
                    lampArray = CurrentDevice;
                }
            }

            Windows.UI.Color[] colors = new Windows.UI.Color[monitoredKeys.Count];
            VirtualKey[] keys = new VirtualKey[monitoredKeys.Count];

            // Build arrays of colors and keys
            foreach (var key in monitoredKeys)
            {
                VirtualKey vkCode = (VirtualKey)key.key;
                Windows.UI.Color color;

                if (key.IsOn)
                {
                    if (key.onColor != null)
                        color = Windows.UI.Color.FromArgb(255, (byte)key.onColor?.R, (byte)key.onColor?.G, (byte)key.onColor?.B);
                    else
                        color = KeyboardMainColor;
                }
                else
                {
                    if (key.offColor != null)
                        color = Windows.UI.Color.FromArgb(255, (byte)key.offColor?.R, (byte)key.offColor?.G, (byte)key.offColor?.B);
                    else
                        color = KeyboardMainColor;
                }

                colors[monitoredKeys.IndexOf(key)] = color;
                keys[monitoredKeys.IndexOf(key)] = vkCode;
            }

            lampArray.SetColorsForKeys(colors, keys);
        }

        public static void SetKeysToColor(LampArray lampArray, VirtualKey[] keys, Windows.UI.Color color)
        {
            Windows.UI.Color[] colors = Enumerable.Repeat(color, keys.Length).ToArray();
            lampArray.SetColorsForKeys(colors, keys);
            lampArray.BrightnessLevel = 1.0f;
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
    }
}
