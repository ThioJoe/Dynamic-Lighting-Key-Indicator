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
    internal class UserConfig
    {
        // --------------------------- Properties ---------------------------
        [JsonInclude]
        public int Brightness { get; set; }
        [JsonInclude]
        public (int R, int G, int B) StandardKeyColor { get; set; }
        [JsonInclude]
        public List<MonitoredKey>? MonitoredKeysAndColors { get; set; }

        public readonly static int DefaultBrightness = 100;
        public readonly static (int R, int G, int B) DefaultStandardKeyColor = (R: 0, G: 0, B: 255);
        public readonly static (int R, int G, int B) DefaultMonitoredKeyActiveColor = (R: 255, G: 0, B: 0);
        public readonly static List<MonitoredKey> DefaultMonitoredKeysAndColors = new List<MonitoredKey> {
                new MonitoredKey(ToggleAbleKeys.NumLock,    onColor: DefaultMonitoredKeyActiveColor, offColor: DefaultStandardKeyColor),
                new MonitoredKey(ToggleAbleKeys.CapsLock,   onColor: DefaultMonitoredKeyActiveColor, offColor: DefaultStandardKeyColor),
                new MonitoredKey(ToggleAbleKeys.ScrollLock, onColor: DefaultMonitoredKeyActiveColor, offColor: DefaultStandardKeyColor)
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
            Brightness = DefaultBrightness;
            StandardKeyColor = DefaultStandardKeyColor;
            MonitoredKeysAndColors = DefaultMonitoredKeysAndColors;
        }

        // Constructor with RGB values for standard key color
        public UserConfig(int brightness, (int R, int G, int B) standardKeyColor, List<MonitoredKey> monitoredKeysAndColors)
        {
            Brightness = brightness;
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
            try
            {
                var configFileTask = localFolder.GetFileAsync(configFileName).AsTask();
                configFileTask.Wait();
                var configFile = configFileTask.Result;

                var configStringTask = FileIO.ReadTextAsync(configFile).AsTask();
                configStringTask.Wait();
                var configString = configStringTask.Result;

                var config = System.Text.Json.JsonSerializer.Deserialize<UserConfig>(json: configString, options: jsonSerializerOptions);
                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return null;
            }
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

        public void RestoreDefault()
        {
            Brightness = DefaultBrightness;
            StandardKeyColor = DefaultStandardKeyColor;
            MonitoredKeysAndColors = DefaultMonitoredKeysAndColors;
        }

    } // ----------------- End of UserConfig class ------------------

}
