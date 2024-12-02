using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dynamic_Lighting_Key_Indicator
{
    class RegistryHandler
    {
        private List<RegistryDeviceSettings> GetCurrentStatesFromRegistry()
        {
            // Look in devices at HKCU:\Software\Microsoft\Lighting\Devices\ for the current state of the devices
            List<string> deviceKeys = [];
            // Get all the subkeys of the Devices key
            string registryDevicesPath = @"Software\Microsoft\Lighting\Devices";
            deviceKeys = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryDevicesPath).GetSubKeyNames().ToList();

            // Create a dictionary containing the device key, and another dictionary containing the device's value names and values
            Dictionary<string, Dictionary<string, object>> deviceValues = [];
            foreach (string deviceKey in deviceKeys)
            {
                // Get the subkeys of the device key
                string registryDevicePath = registryDevicesPath + "\\" + deviceKey;
                string[] valueNames = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryDevicePath).GetValueNames();
                Dictionary<string, object> valueDict = [];
                foreach (string valueName in valueNames)
                {
                    object rawValue = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryDevicePath).GetValue(valueName);
                    // Convert Int32 to UInt32 right when we get it from registry
                    if (rawValue is Int32 intValue)
                    {
                        valueDict.Add(valueName, unchecked((UInt32)intValue));
                    }
                    else
                    {
                        valueDict.Add(valueName, rawValue);
                    }
                }

                deviceValues.Add(deviceKey, valueDict);
            }

            // Local function to check if a registry key exists in the dictionary and return the value if it does, otherwise return null
            T? GetRegDictValue<T>(Dictionary<string, object> regValues, string valueName) where T : struct
            {
                if (regValues.ContainsKey(valueName))
                {
                    var value = regValues[valueName];
                    if (value != null)
                    {
                        // Need to specially convert to UInt32. Even though registry data stored as REG_DWORD (unsigned int), it will return an Int32
                        // See: https://stackoverflow.com/a/60678875/17312053
                        if (typeof(T) == typeof(UInt32) && value is Int32 intValue)
                        {
                            return (T)(object)unchecked((UInt32)intValue);
                        }

                        return Convert.ChangeType(value, typeof(T)) is T result ? result : null;
                    }
                }
                return null;
            }

            // Put the data into RegistryDeviceSettings objects
            List<RegistryDeviceSettings> registrySettingsList = [];

            foreach (KeyValuePair<string, Dictionary<string, object>> device in deviceValues)
            {
                string regKey = device.Key;
                Dictionary<string, object> regValues = device.Value;

                UInt32? ambientLightingEnabled = GetRegDictValue<UInt32>(regValues, RegistryDeviceSettings.RegistryPropertyNames.AmbientLightingEnabled);
                UInt32? brightness = GetRegDictValue<UInt32>(regValues, RegistryDeviceSettings.RegistryPropertyNames.Brightness);
                UInt32? color = GetRegDictValue<UInt32>(regValues, RegistryDeviceSettings.RegistryPropertyNames.Color);
                UInt32? color2 = GetRegDictValue<UInt32>(regValues, RegistryDeviceSettings.RegistryPropertyNames.Color2);
                UInt32? controlledByForegroundApp = GetRegDictValue<UInt32>(regValues, RegistryDeviceSettings.RegistryPropertyNames.ControlledByForegroundApp);
                UInt32? effectMode = GetRegDictValue<UInt32>(regValues, RegistryDeviceSettings.RegistryPropertyNames.EffectMode);
                UInt32? speed = GetRegDictValue<UInt32>(regValues, RegistryDeviceSettings.RegistryPropertyNames.Speed);
                UInt32? useSystemAccentColor = GetRegDictValue<UInt32>(regValues, RegistryDeviceSettings.RegistryPropertyNames.UseSystemAccentColor);

                RegistryDeviceSettings regSettingsObj = new RegistryDeviceSettings(
                    regKey: regKey,
                    ambientLightingEnabled: (IntBool?)ambientLightingEnabled,
                    brightness: brightness,
                    color: color,
                    color2: color2,
                    controlledByForegroundApp: (IntBool?)controlledByForegroundApp,
                    effectMode: effectMode,
                    speed: speed,
                    useSystemAccentColor: (IntBool?)useSystemAccentColor
                );

                registrySettingsList.Add(regSettingsObj);
            }

            return registrySettingsList;

        }

        private class RegistryDeviceSettings
        {
            public RegistryDeviceSettings(string regKey, IntBool? ambientLightingEnabled, UInt32? brightness, UInt32? color, UInt32? color2, IntBool? controlledByForegroundApp, UInt32? effectMode, UInt32? speed, IntBool? useSystemAccentColor)
            {
                this._regKey = regKey;
                this.AmbientLightingEnabled = ambientLightingEnabled;
                this.Brightness = brightness;
                this.Color = color;
                this.Color2 = color2;
                this.ControlledByForegroundApp = controlledByForegroundApp;
                this.EffectMode = effectMode;
                this.Speed = speed;
                this.UseSystemAccentColor = useSystemAccentColor;
            }

            // Not actual registry properties, but used to store the registry key and display name
            private string _regKey;
            private string? _displayName;

            // Actual registry properties
            public IntBool? AmbientLightingEnabled;
            public UInt32? Brightness;
            public UInt32? Color;
            public UInt32? Color2;
            public IntBool? ControlledByForegroundApp;
            public UInt32? EffectMode;
            public EffectType? effectType;
            public UInt32? Speed;
            public IntBool? UseSystemAccentColor;

            public string GetRegKey()
            {
                return _regKey;
            }

            public string? GetDisplayName()
            {
                return _displayName;
            }

            public string GetID()
            {
                return @"\\\\?\\" + _regKey;
            }

            public void SetDisplayName(string displayName)
            {
                _displayName = displayName;
            }

            public void UpdateProperty(string propertyName, object value)
            {
                switch (propertyName)
                {
                    case RegistryPropertyNames.AmbientLightingEnabled:
                        AmbientLightingEnabled = (IntBool)value;
                        break;
                    case RegistryPropertyNames.Brightness:
                        Brightness = (UInt32)value;
                        break;
                    case RegistryPropertyNames.Color:
                        Color = (UInt32)value;
                        break;
                    case RegistryPropertyNames.Color2:
                        Color2 = (UInt32)value;
                        break;
                    case RegistryPropertyNames.ControlledByForegroundApp:
                        ControlledByForegroundApp = (IntBool)value;
                        break;
                    case RegistryPropertyNames.EffectMode:
                        EffectMode = (UInt32)value;
                        break;
                    case RegistryPropertyNames.Speed:
                        Speed = (UInt32)value;
                        break;
                    case RegistryPropertyNames.UseSystemAccentColor:
                        UseSystemAccentColor = (IntBool)value;
                        break;
                    default:
                        break;
                }
            }

            public enum EffectType : UInt32
            {
                Static = 0,
                Breathing = 1,
                Rainbow = 2,
                Wave = 4,
                Wheel = 5,
                Gradient = 6,
            }

            public enum GradientEffectMode : UInt32
            {
                Horizontal = 0,
                Vertical = 1,
                Outward = 2,
            }

            public enum RainbowEffectMode : UInt32
            {
                Forward = 0,
                Reverse = 1,
            }

            public enum WaveEffectMode : UInt32
            {
                Right = 0,
                Left = 1,
                Down = 2,
                Up = 3,
            }

            public enum WheelEffectMode : UInt32
            {
                Clockwise = 0,
                CounterClockwise = 1,
            }

            public static class RegistryPropertyNames
            {
                public const string AmbientLightingEnabled = "AmbientLightingEnabled";
                public const string Brightness = "Brightness";
                public const string Color = "Color";
                public const string Color2 = "Color2";
                public const string ControlledByForegroundApp = "ControlledByForegroundApp";
                public const string EffectMode = "EffectMode";
                public const string Speed = "Speed";
                public const string UseSystemAccentColor = "UseSystemAccentColor";
            }
        }

        private enum IntBool : UInt32
        {
            False = 0,
            True = 1,
        }
    }
}
