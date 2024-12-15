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
    internal class UserConfig: ICloneable
    {
        // --------------------------- Properties ---------------------------
        [JsonInclude]
        public int Brightness { get; set; }
        [JsonInclude]
        public RGBTuple StandardKeyColor { get; set; }
        [JsonInclude]
        public List<MonitoredKey> MonitoredKeysAndColors { get; set; }
        [JsonInclude]
        public string DeviceId { get; set; } = string.Empty;
        [JsonInclude]
        public bool StartMinimizedToTray { get; set; } = false;

        public readonly static int DefaultBrightness = 100;
        public readonly static RGBTuple DefaultStandardKeyColor = (R: 0, G: 0, B: 255);
        public readonly static RGBTuple DefaultMonitoredKeyActiveColor = (R: 255, G: 0, B: 0);

        public readonly static List<MonitoredKey> DefaultMonitoredKeysAndColors =
        [
                new(ToggleAbleKeys.NumLock,    onColor: DefaultMonitoredKeyActiveColor, offColor: DefaultStandardKeyColor, onColorTiedToStandard: false, offColorTiedToStandard: true),
                new(ToggleAbleKeys.CapsLock,   onColor: DefaultMonitoredKeyActiveColor, offColor: DefaultStandardKeyColor, onColorTiedToStandard: false, offColorTiedToStandard: true),
                new(ToggleAbleKeys.ScrollLock, onColor : DefaultMonitoredKeyActiveColor, offColor : DefaultStandardKeyColor, onColorTiedToStandard : false, offColorTiedToStandard : true)
        ];

        // ------------ Private Variables ------------
        private const string configFileName = "Key_Indicator_Config.json";
        private static readonly StorageFolder localFolder = ApplicationData.Current.LocalFolder;
        private static readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            WriteIndented = true,
            MaxDepth = 10,
            IncludeFields = true // This is needed for tuples to be serialized
        };
        private static readonly bool defaultMinimizedToTray = false;

        // --------------------------- Constructors ---------------------------
        // Constructors must set default values for all properties that aren't set at declaration
        // Default constructor
        public UserConfig()
        {
            Brightness = DefaultBrightness; // Remnant from old code
            StandardKeyColor = DefaultStandardKeyColor;
            MonitoredKeysAndColors = DefaultMonitoredKeysAndColors;
        }

        // Constructor with RGB values for standard key color
        public UserConfig(RGBTuple standardKeyColor, List<MonitoredKey> monitoredKeysAndColors)
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

        public static UserConfig? ReadConfigurationFile()
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

                // Deserializer will automatically set the default values for any missing properties
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
        public RGBTuple GetVKOnColor(KeyStatesHandler.ToggleAbleKeys key)
        {
            // If the key is not in the list, return the standard key color
            if (MonitoredKeysAndColors == null || !MonitoredKeysAndColors.Any(mk => mk.key == key))
            {
                return StandardKeyColor;
            }
            return MonitoredKeysAndColors.First(mk => mk.key == key).onColor;
        }

        public RGBTuple GetVKOffColor(KeyStatesHandler.ToggleAbleKeys key)
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

        public static async void OpenConfigFolder()
        {
            await Launcher.LaunchFolderPathAsync(localFolder.Path);
        }

        public void RestoreDefault()
        {
            Brightness = DefaultBrightness;
            StandardKeyColor = DefaultStandardKeyColor;
            MonitoredKeysAndColors = DefaultMonitoredKeysAndColors;
            StartMinimizedToTray = defaultMinimizedToTray;
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
            

            List<MonitoredKey> keysList = config.MonitoredKeysAndColors;
            foreach (var key in keysList)
            {
                RGBTuple onColor = ColorSetter.ScaleColorBrightness(key.onColor, brightnessLevel);
                RGBTuple offColor = ColorSetter.ScaleColorBrightness(key.offColor, brightnessLevel);

                key.onColor = onColor;
                key.offColor = offColor;
            }

            // Update the standard key color
            RGBTuple standardColor = ColorSetter.ScaleColorBrightness(config.StandardKeyColor, brightnessLevel);
            config.StandardKeyColor = standardColor;
        }

        public object Clone()
        {
            // Create a new UserConfig object
            UserConfig clonedConfig = new UserConfig
            {
                Brightness = this.Brightness,
                StandardKeyColor = this.StandardKeyColor,
                MonitoredKeysAndColors = this.MonitoredKeysAndColors.Select(mk => new MonitoredKey(mk.key, mk.onColor, mk.offColor, mk.onColorTiedToStandard, mk.offColorTiedToStandard)).ToList(),
                DeviceId = this.DeviceId,
                StartMinimizedToTray = this.StartMinimizedToTray
            };

            return clonedConfig;
        }

    } // ----------------- End of UserConfig class ------------------

}
