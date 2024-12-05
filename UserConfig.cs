using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using static Dynamic_Lighting_Key_Indicator.KeyStatesHandler;

namespace Dynamic_Lighting_Key_Indicator
{
    using RGBTuple = (int R, int G, int B);

    internal class UserConfig
    {
        // --------------------------- Properties ---------------------------
        [JsonInclude]
        public int Brightness { get; set; }
        [JsonInclude]
        public (int R, int G, int B) StandardKeyColor { get; set; }
        [JsonInclude]
        public List<MonitoredKey> MonitoredKeysAndColors { get; set; }
        [JsonInclude]
        public string DeviceId { get; set; } = string.Empty;

        public readonly static int DefaultBrightness = 100;
        public readonly static (int R, int G, int B) DefaultStandardKeyColor = (R: 0, G: 0, B: 255);
        public readonly static (int R, int G, int B) DefaultMonitoredKeyActiveColor = (R: 255, G: 0, B: 0);
        public readonly static List<MonitoredKey> DefaultMonitoredKeysAndColors = new List<MonitoredKey> {
                new MonitoredKey(ToggleAbleKeys.NumLock,    onColor: DefaultMonitoredKeyActiveColor, offColor: DefaultStandardKeyColor, onColorTiedToStandard: false, offColorTiedToStandard: true),
                new MonitoredKey(ToggleAbleKeys.CapsLock,   onColor: DefaultMonitoredKeyActiveColor, offColor: DefaultStandardKeyColor, onColorTiedToStandard: false, offColorTiedToStandard: true),
                new MonitoredKey(ToggleAbleKeys.ScrollLock, onColor : DefaultMonitoredKeyActiveColor, offColor : DefaultStandardKeyColor, onColorTiedToStandard : false, offColorTiedToStandard : true)
        };

        // ------------ Private Variables ------------
        private const string configFileName = "Key_Indicator_Config.json";
        private static StorageFolder localFolder = ApplicationData.Current.LocalFolder;
        private static JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            MaxDepth = 10,
            IncludeFields = true // This is needed for tuples to be serialized
        };

        // --------------------------- Constructors ---------------------------
        // Default constructor
        public UserConfig()
        {
            Brightness = DefaultBrightness; // Remnant from old code
            StandardKeyColor = DefaultStandardKeyColor;
            MonitoredKeysAndColors = DefaultMonitoredKeysAndColors;
        }

        // Constructor with RGB values for standard key color
        public UserConfig((int R, int G, int B) standardKeyColor, List<MonitoredKey> monitoredKeysAndColors)
        {
            Brightness = DefaultBrightness; // Remnant from old code
            StandardKeyColor = standardKeyColor;
            MonitoredKeysAndColors = monitoredKeysAndColors;
        }

        // --------------------------- Read / Write Methods ---------------------------
        public async Task<bool> WriteConfigurationFile_Async()
        {
            string configuration = System.Text.Json.JsonSerializer.Serialize(value: this, options: jsonSerializerOptions);
            try
            {
                StorageFile configFile = await localFolder.CreateFileAsync(configFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(configFile, configuration);
            }
            catch (Exception)
            {
                Console.WriteLine(Console.Error);
                return false;
            }
            return true;
        }

        public UserConfig? ReadConfigurationFile()
        {
            UserConfig? config;
            try
            {
                var configFileTask = localFolder.GetFileAsync(configFileName).AsTask();
                configFileTask.Wait();
                var configFile = configFileTask.Result;

                var configStringTask = FileIO.ReadTextAsync(configFile).AsTask();
                configStringTask.Wait();
                var configString = configStringTask.Result;

                config = System.Text.Json.JsonSerializer.Deserialize<UserConfig>(json: configString, options: jsonSerializerOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return null;
            }

            if (config == null)
                return null;
            else
                return ValidateConfigSettings(config);
        }

        private static UserConfig ValidateConfigSettings(UserConfig config)
        {
            // Loop through each key
            if (config.MonitoredKeysAndColors != null)
            {
                foreach (var key in config.MonitoredKeysAndColors)
                {
                    // If the key is tied to the standard color, set the color to the standard color
                    if (key.onColorTiedToStandard)
                        key.onColor = config.StandardKeyColor;

                    if (key.offColorTiedToStandard)
                        key.offColor = config.StandardKeyColor;
                }
            }
            return config;
        }

        // ----------------- General Methods ------------------
        public (int R, int G, int B) GetVKOnColor(KeyStatesHandler.ToggleAbleKeys key)
        {
            // If the key is not in the list, return the standard key color
            if (MonitoredKeysAndColors == null || !MonitoredKeysAndColors.Any(mk => mk.key == key))
            {
                return StandardKeyColor;
            }
            return MonitoredKeysAndColors.First(mk => mk.key == key).onColor;
        }

        public (int R, int G, int B) GetVKOffColor(KeyStatesHandler.ToggleAbleKeys key)
        {
            // If the key is not in the list, return the standard key color
            if (MonitoredKeysAndColors == null || !MonitoredKeysAndColors.Any(mk => mk.key == key))
            {
                return StandardKeyColor;
            }
            return MonitoredKeysAndColors.First(mk => mk.key == key).offColor;
        }

        public MonitoredKey? GetMonitoredKey(ToggleAbleKeys key)
        {
            if (MonitoredKeysAndColors == null)
                return null;
            return MonitoredKeysAndColors.FirstOrDefault(mk => mk.key == key);
        }

        public async void OpenConfigFolder()
        {
            await Launcher.LaunchFolderPathAsync(localFolder.Path);
        }

        public void RestoreDefault()
        {
            Brightness = DefaultBrightness;
            StandardKeyColor = DefaultStandardKeyColor;
            MonitoredKeysAndColors = DefaultMonitoredKeysAndColors;
        }

        // Scale brightness of all key settings based on a brightness level
        public void UpdateBrightnessForAllKeys(int brightnessLevel)
        {
            UserConfig config = this; // Applies to the object on which the method is called

            if (brightnessLevel < 0)
                brightnessLevel = 0;
            if (brightnessLevel > 100)
                brightnessLevel = 100;

            config.Brightness = brightnessLevel; // Remnant from old code

            // -------- Local Function --------
            // Set the absolute scale of a color based on a brightness level. 100 is full brightness, 0 is off
            // Will use relative scaling of the largest or smallest value in the color
            RGBTuple ScaleColor(RGBTuple color, int brightness)
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

            List<MonitoredKey> keysList = KeyStatesHandler.monitoredKeys;
            foreach (var key in keysList)
            {
                RGBTuple onColor = ScaleColor(key.onColor, brightnessLevel);
                RGBTuple offColor = ScaleColor(key.offColor, brightnessLevel);

                key.onColor = onColor;
                key.offColor = offColor;
            }

            // Update the standard key color
            RGBTuple standardColor = ScaleColor(config.StandardKeyColor, brightnessLevel);
            config.StandardKeyColor = standardColor;
        }

    } // ----------------- End of UserConfig class ------------------

}
