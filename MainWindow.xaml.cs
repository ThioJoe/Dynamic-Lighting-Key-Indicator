using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using static Dynamic_Lighting_Key_Indicator.KeyStatesHandler;

#nullable enable


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Dynamic_Lighting_Key_Indicator
{
    using static Dynamic_Lighting_Key_Indicator.KeyStatesHandler;
    using VK = KeyStatesHandler.ToggleAbleKeys;

    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; set; }

        List<string> devicesListForDropdown = [];

        // Currently attached LampArrays
        private readonly List<LampArrayInfo> m_attachedLampArrays = new List<LampArrayInfo>();
        private DeviceWatcher m_deviceWatcher;
        private Dictionary<int, string> deviceIndexDict = new Dictionary<int, string>();

        private readonly object _lock = new object();

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            ViewModel.DeviceStatusMessage = "Status: Initializing...";

            // Set up keyboard hook
            KeyStatesHandler.SetMonitoredKeys(new List<MonitoredKey> {
                new MonitoredKey(VK.NumLock, onColor: (R:255, G:0, B:0), offColor: null),
                new MonitoredKey(VK.CapsLock, onColor: (R:255, G:0, B:0), offColor: null),
                new MonitoredKey(VK.ScrollLock, onColor: (R:255, G:0, B:0), offColor: null)
            });

            ColorSetter.DefineKeyboardMainColor_FromNameAndBrightness(color: Colors.Blue, brightnessPercent: 100);
            KeyStatesHandler.InitializeHookAndCallback();
        }


        private void StartWatchingForLampArrays()
        {
            // Start watching for newly attached LampArrays.
            m_deviceWatcher = DeviceInformation.CreateWatcher(LampArray.GetDeviceSelector());

            m_deviceWatcher.Added += Watcher_Added;
            m_deviceWatcher.Removed += Watcher_Removed;
            m_deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;

            m_deviceWatcher.Start();
        }

        private void StopWatchingForLampArrays()
        {
            if (m_deviceWatcher.Status == DeviceWatcherStatus.Started || m_deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
            {
                m_deviceWatcher.Stop();
                //m_deviceWatcher = null;
            }
        }

        private void UpdateLampArrayDisplayList()
        {
            string message = $"Attached LampArrays: {m_attachedLampArrays.Count}\n";
            int deviceIndex = 0;

            lock (_lock)
            {
                devicesListForDropdown = new List<string>(); // Clear the list
                deviceIndexDict.Clear();

                lock (m_attachedLampArrays)
                {
                    foreach (LampArrayInfo info in m_attachedLampArrays)
                    {
                        message += $"{deviceIndex + 1}: {info.displayName} ({info.lampArray.LampArrayKind.ToString()}, {info.lampArray.LampCount} lamps, " + $"{(info.lampArray.IsAvailable ? "Available" : "Unavailable")})\n";

                        // Add the device to the dropdown list and store its index in the dictionary
                        devicesListForDropdown.Add(info.displayName);
                        if (deviceIndexDict.ContainsKey(deviceIndex))
                        {
                            deviceIndexDict[deviceIndex] = info.id;
                        }
                        else
                        {
                            deviceIndexDict.Add(deviceIndex, info.id);
                        }
                        deviceIndex++;
                    }
                }

                // Update ViewModel on UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.DeviceStatusMessage = message;
                    dropdownDevices.ItemsSource = devicesListForDropdown; // Populate the ComboBox
                });
            }
        }

        LampArrayInfo? GetSelectedDeviceObject()
        {
            // Get the index of the selection from the GUI dropdown
            int selectedDevice = dropdownDevices.SelectedIndex;

            if (selectedDevice == -1)
            {
                //MessageBox.Show("Please select a device from the dropdown list.");
                return null;
            }
            
            string selectedDeviceID = deviceIndexDict[selectedDevice];
            return m_attachedLampArrays.Find(info => info.id == selectedDeviceID);
        }

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

        private void ApplyLightingToDevice(LampArrayInfo lampArrayInfo)
        {
            LampArray lampArray = lampArrayInfo.lampArray;
            ColorSetter.SetCurrentDevice(lampArray);
            ColorSetter.SetInitialDefaultKeyboardColor(lampArray);
            KeyStatesHandler.UpdateKeyStatus();
        }

        // If no device has been set, and a keyboard is attached, set the keyboard as the current device
        private void CheckForCurrentDeviceAndApply()
        {
            if (ColorSetter.CurrentDevice == null)
            {
                foreach (LampArrayInfo info in m_attachedLampArrays)
                {
                    if ((int)info.lampArray.LampArrayKind == (int)LampArrayKind.Keyboard)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            dropdownDevices.SelectedIndex = m_attachedLampArrays.IndexOf(info);
                            ApplyLightingToDevice(info);
                        });
                        break;
                    }
                }
            }
        }


        // --------------------------------------------------- CLASSES AND ENUMS ---------------------------------------------------
        internal class LampArrayInfo
        {
            public LampArrayInfo(string id, string displayName, LampArray lampArray)
            {
                this.id = id;
                this.displayName = displayName;
                this.lampArray = lampArray;
            }

            public readonly string id;
            public readonly string displayName;
            public readonly LampArray lampArray;
        }

        private enum IntBool : UInt32
        {
            False = 0,
            True = 1,
        }

        // See: https://learn.microsoft.com/en-us/uwp/api/windows.devices.lights.lamparraykind
        private enum LampArrayKind : int
        {
            Undefined = 0,
            Keyboard = 1,
            Mouse = 2,
            GameController = 3,
            Peripheral = 4,
            Scene = 5,
            Notification = 6,
            Chassis = 7,
            Wearable = 8,
            Furniture = 9,
            Art = 10,
            Headset = 11,
            Microphone = 12,
            Speaker = 13
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

        // -------------------------------------- CUSTOM EVENT HANDLERS --------------------------------------

        // The AvailabilityChanged event will fire when this calling process gains or loses control of RGB lighting
        // for the specified LampArray.
        private void LampArray_AvailabilityChanged(LampArray sender, object args)
        {
            // Update UI on the UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateLampArrayDisplayList();
            });
        }

        private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            lock (m_attachedLampArrays)
            {
                m_attachedLampArrays.RemoveAll(info => info.id == args.Id);
            }

            // Update UI on the UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateLampArrayDisplayList();
            });
        }

        private async void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            var lampArray = await LampArray.FromIdAsync(args.Id);
            var info = new LampArrayInfo(args.Id, args.Name, lampArray);

            if (info.lampArray == null)
            {
                // Update on UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.DeviceStatusMessage = $"Status: Error initializing LampArray: \"{info.displayName}\"";
                });
                return;
            }

            // Set up the AvailabilityChanged event callback
            info.lampArray.AvailabilityChanged += LampArray_AvailabilityChanged;

            // Add to the list (thread-safe)
            lock (m_attachedLampArrays)
            {
                m_attachedLampArrays.Add(info);
            }

            // Update UI on the UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateLampArrayDisplayList();
            });
        }

        private void OnEnumerationCompleted(DeviceWatcher sender, object args)
        {
            DispatcherQueue.TryEnqueue(() => CheckForCurrentDeviceAndApply());
        }

        // -------------------------------------- GUI EVENT HANDLERS --------------------------------------
        private void buttonStartWatch_Click(object sender, RoutedEventArgs e)
        {
            // Clear the current list of attached devices
            m_attachedLampArrays.Clear();

            StartWatchingForLampArrays();
        }

        private void buttonStopWatch_Click(object sender, RoutedEventArgs e)
        {
            StopWatchingForLampArrays();
        }

        private void buttonApply_Click(object sender, RoutedEventArgs e)
        {
            LampArrayInfo? selectedLampArrayInfo = GetSelectedDeviceObject();

            if (selectedLampArrayInfo != null)
            {
                ApplyLightingToDevice(selectedLampArrayInfo);
            }
        }

        // ---------------------------------------------------------------------------------------------------
    }
}
