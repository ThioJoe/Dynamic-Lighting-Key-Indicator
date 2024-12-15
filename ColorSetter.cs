using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.System;
using static Dynamic_Lighting_Key_Indicator.MainWindow;

#pragma warning disable IDE0305 // Simplify collection initialization

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

        public static void DefineCurrentDevice(LampArray? device)
        {
            _currentDevice = device;
        }

        public static LampArray? DetermineLampArray(LampArray? lampArray)
        {
            if (lampArray == null)
            {
                if (CurrentDevice == null)
                    //MainWindow.ShowErrorMessage("LampArray must be defined.");
                    return null;
                else
                    lampArray = CurrentDevice;
            }
            return lampArray;
        }

        public static Windows.UI.Color DetermineDefaultColor(Windows.UI.Color? color)
        {
            return color ?? KeyboardMainColor;
        }

        public static void SetInitialDefaultColor_ToKeyboard(LampArray lampArray)
        {
            lampArray.SetColor(KeyboardMainColor);
            ColorSetter.SetAllMonitoredKeyColors_ToKeyboard(KeyStatesHandler.monitoredKeys);
        }

        public static void SetAllColors_ToKeyboard(LampArray? lampArray = null) // Defaults to the current device
        {
            // If it's null, it will show an error then we can return
            if (DetermineLampArray(lampArray) is not LampArray lampArrayToUse)
                return;

            SetInitialDefaultColor_ToKeyboard(lampArrayToUse);
            SetAllMonitoredKeyColors_ToKeyboard(KeyStatesHandler.monitoredKeys);
        }

        // For when the monitored key is toggled, this applies the set color to the key
        public static void SetSingleMonitorKeyColor_ToKeyboard(KeyStatesHandler.MonitoredKey key, LampArray? lampArray = null)
        {
            // If it's null, it will show an error then we can return
            if (DetermineLampArray(lampArray) is not LampArray lampArrayToUse)
                return;

            VirtualKey vkCode = (VirtualKey)key.key;
            Windows.UI.Color color;

            if (key.IsOn())
                color = RGBTuple_To_ColorObj(key.onColor);
            else
                color = RGBTuple_To_ColorObj(key.offColor);

            lampArrayToUse.SetColorsForKey(color, vkCode);
        }

        // For when the monitored keys are toggled, this applies the set colors to all the monitored keys
        public static void SetAllMonitoredKeyColors_ToKeyboard(List<KeyStatesHandler.MonitoredKey> monitoredKeys, LampArray? lampArray = null)
        {
            // If it's null, it will show an error then we can return
            if (DetermineLampArray(lampArray) is not LampArray lampArrayToUse)
                return;

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

            lampArrayToUse.SetColorsForKeys(colors, keys);
        }

        public static void ProperlySetProperColorsAllKeys_ToKeyboard(LampArray? lampArray = null)
        {
            if (DetermineLampArray(lampArray) is not LampArray lampArrayToUse)
                return;

            // Make arrays of each monitored key's color, the default color for all others, put into combined arrays
            List<Windows.UI.Color> colors = [];
            List<int> keyIndices = [];

            // Add non-monitored keys and applicable monitored keys to the list of indices
            foreach (var key in KeyStatesHandler.monitoredKeys)
            {
                keyIndices.Add(MonitoredKeyIndicesDict[key.key]);
                colors.Add(key.IsOn() ? RGBTuple_To_ColorObj(key.onColor) : RGBTuple_To_ColorObj(key.offColor));
            }
            foreach (int index in NonMonitoredKeyIndices)
            {
                keyIndices.Add(index);
                colors.Add(KeyboardMainColor);
            }

            int[] keyIndicesArray = keyIndices.ToArray();
            Windows.UI.Color[] colorsArray = colors.ToArray();

            lampArrayToUse.SetColorsForIndices(colorsArray, keyIndicesArray);

        }

        // For when user is changing the color of a specific key in the UI
        public static void SetSpecificKeysColor_ToKeyboard(KeyColorUpdateInfo colorUpdateInfo, LampArray? lampArray = null, bool noStateCheck = false)
        {
            if (DetermineLampArray(lampArray) is not LampArray lampArrayToUse)
                return;

            KeyStatesHandler.ToggleAbleKeys key = colorUpdateInfo.key;
            RGBTuple color = colorUpdateInfo.color;
            StateColorApply forState = colorUpdateInfo.forState;

            if (noStateCheck == true || forState == StateColorApply.Both)
            {
                lampArrayToUse.SetColorsForKey(RGBTuple_To_ColorObj(color), (VirtualKey)key);
            }
            else
            {
                bool currentKeyState = KeyStatesHandler.FetchKeyState((int)key);
                // Do nothing if the key's state doesn't match the state to which the color applies
                if ((forState == StateColorApply.On && currentKeyState == true) || (forState == StateColorApply.Off && currentKeyState == false))
                    lampArrayToUse.SetColorsForKey(RGBTuple_To_ColorObj(color), (VirtualKey)key);
                else
                    return;
            }
        }

        public static void SetColorToDefaultAndAdditionalIndices(RGBTuple colorTuple, List<VK> additionalKeys, LampArray lampArray)
        {
            if (lampArray == null)
                return;

            Windows.UI.Color colorToUse = Windows.UI.Color.FromArgb(255, (byte)colorTuple.R, (byte)colorTuple.G, (byte)colorTuple.B);

            List<int> keyIndices = [];
            keyIndices.AddRange(NonMonitoredKeyIndices);
            keyIndices.AddRange(additionalKeys.Select(key => MonitoredKeyIndicesDict[key]));

            lampArray.SetSingleColorForIndices(colorToUse, keyIndices.ToArray());
        }

        // For when the user is changing the color of the default color in the UI, which may include linked colors of monitored keys
        public static void SetDefaultAndApplicableKeysColor_ToKeyboard(RGBTuple colorTuple, LampArray? lampArray = null, bool noStateCheck = false, Dictionary<VK,bool>? assumedStatesDict = null)
        {
            if (DetermineLampArray(lampArray) is not LampArray lampArrayToUse)
                return;
            Windows.UI.Color colorToUse = DetermineDefaultColor(RGBTuple_To_ColorObj(colorTuple));

            // Check which keys have on or off colors linked to the standard color, and also whether the color is on or off, to decide which colors need to be updated on the keyboard
            List<Windows.UI.Color> colors = [];
            List<int> keyIndices = [];

            // Add non-monitored keys and applicable monitored keys to the list of indices
            foreach (var key in KeyStatesHandler.monitoredKeys)
            {
                bool keyState;
                if (assumedStatesDict != null)
                    keyState = assumedStatesDict[key.key];
                else
                    keyState = key.IsOn(); // TODO: This probably causes a lot of calls, maybe only check it once when the flyout opens
                
                if ((keyState && key.onColorTiedToStandard) || (!keyState && key.offColorTiedToStandard) || noStateCheck) 
                {
                    keyIndices.Add(MonitoredKeyIndicesDict[key.key]);
                }
            }
            keyIndices.AddRange(NonMonitoredKeyIndices);

            int[] keyIndicesArray = keyIndices.ToArray();
            Windows.UI.Color[] colorsArray = Enumerable.Repeat(colorToUse, keyIndices.Count).ToArray(); // Same color for all so just fill it to the same size as the keyIndices

            lampArrayToUse.SetColorsForIndices(colorsArray, keyIndicesArray);
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

        public static Windows.UI.Color ScaleColorBrightness(Windows.UI.Color color, int brightness)
        {
            RGBTuple colorTuple = (color.R, color.G, color.B);
            RGBTuple newColorTuple = ScaleColorBrightness(colorTuple, brightness);
            return RGBTuple_To_ColorObj(newColorTuple);
        }

        // Set the absolute scale of a color based on a brightness level. 100 is full brightness, 0 is off
        // Will use relative scaling of the largest or smallest value in the color
        public static RGBTuple ScaleColorBrightness(RGBTuple color, int brightness)
        {
            // Clamp brightness to the 0-100 range
            brightness = Math.Max(0, Math.Min(100, brightness));

            int R = color.R;
            int G = color.G;
            int B = color.B;

            // Find the maximum RGB component
            int maxComponent = Math.Max(R, Math.Max(G, B));

            if (maxComponent == 0)
            {
                return (0, 0, 0);
            }

            // Calculate the relative proportions of each component
            double rProportion = R / (double)maxComponent;
            double gProportion = G / (double)maxComponent;
            double bProportion = B / (double)maxComponent;

            // Calculate the desired maximum component value based on brightness
            double desiredMaxComponent = (brightness / 100.0) * 255.0;

            // Compute new RGB values by scaling the proportions
            int newR = (int)Math.Round(rProportion * desiredMaxComponent);
            int newG = (int)Math.Round(gProportion * desiredMaxComponent);
            int newB = (int)Math.Round(bProportion * desiredMaxComponent);

            // Clamp the values to the 0-255 range
            newR = Math.Min(255, Math.Max(0, newR));
            newG = Math.Min(255, Math.Max(0, newG));
            newB = Math.Min(255, Math.Max(0, newB));

            // Update the color tuple
            color = (newR, newG, newB);
            return color;
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
