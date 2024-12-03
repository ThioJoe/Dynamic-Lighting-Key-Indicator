using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using static Dynamic_Lighting_Key_Indicator.KeyStatesHandler;

namespace Dynamic_Lighting_Key_Indicator
{
    internal class UserConfig
    {
        private const string configFileName = "Key_Indicator_Config.json";

        public async Task WriteConfigurationFile_Async()
        {
            string configuration = System.Text.Json.JsonSerializer.Serialize(this);
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile configFile = await localFolder.CreateFileAsync(configFileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(configFile, configuration);
        }

        public async Task<UserConfig?> ReadConfigurationFile_Async()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile configFile = await localFolder.GetFileAsync(configFileName);
            var configString = await FileIO.ReadTextAsync(configFile);
            var config = System.Text.Json.JsonSerializer.Deserialize<UserConfig>(configString);
            return config;
        }

        public readonly static int DefaultBrightness = 100;
        public readonly static (int R, int G, int B) DefaultStandardKeyColor =         (R: 0, G: 0, B: 255);
        public readonly static (int R, int G, int B) DefaultMonitoredKeyActiveColor =  (R: 255, G: 0, B: 0);
        public readonly static List<MonitoredKey> DefaultMonitoredKeysAndColors = new List<MonitoredKey> {
            new MonitoredKey(ToggleAbleKeys.NumLock,    onColor: DefaultMonitoredKeyActiveColor, offColor: DefaultStandardKeyColor),
            new MonitoredKey(ToggleAbleKeys.CapsLock,   onColor: DefaultMonitoredKeyActiveColor, offColor: DefaultStandardKeyColor),
            new MonitoredKey(ToggleAbleKeys.ScrollLock, onColor: DefaultMonitoredKeyActiveColor, offColor: DefaultStandardKeyColor)
        };

        public int Brightness { get; set; }
        public (int R, int G, int B) StandardKeyColor { get; set; }
        public List<MonitoredKey>? MonitoredKeysAndColors { get; set; }

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

        // Setting Methods
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

        // ----------------- General Methods ------------------
        public void RestoreDefault()
        {
            Brightness = DefaultBrightness;
            StandardKeyColor = DefaultStandardKeyColor;
            MonitoredKeysAndColors = DefaultMonitoredKeysAndColors;
        }

        public string Serialize()
        {
            return System.Text.Json.JsonSerializer.Serialize(this);
        }

        public void SetFromViewModel(ColorSettings viewModelColorSettings)
        {
            int brightness = viewModelColorSettings.Brightness;
            (int R, int G, int B) standardKeyColor = (viewModelColorSettings.DefaultColor.R, viewModelColorSettings.DefaultColor.G, viewModelColorSettings.DefaultColor.B);
            List<MonitoredKey> monitoredKeysAndColors = new List<MonitoredKey> {
                new MonitoredKey(ToggleAbleKeys.NumLock,    onColor: (viewModelColorSettings.NumLockOnColor), offColor: (viewModelColorSettings.NumLockOffColor)),
                new MonitoredKey(ToggleAbleKeys.CapsLock,   onColor: (viewModelColorSettings.CapsLockOnColor), offColor: (viewModelColorSettings.CapsLockOffColor)),
                new MonitoredKey(ToggleAbleKeys.ScrollLock, onColor: (viewModelColorSettings.ScrollLockOnColor), offColor: (viewModelColorSettings.ScrollLockOffColor))
            };

            Brightness = brightness;
            StandardKeyColor = standardKeyColor;
            MonitoredKeysAndColors = monitoredKeysAndColors;
        }

    } // ----------------- End of UserConfig class ------------------

}
