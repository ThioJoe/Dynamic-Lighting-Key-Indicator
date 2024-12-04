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
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
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

        // GUI Related
        List<string> devicesListForDropdown = [];
        UserConfig currentConfig = new UserConfig();

        // Currently attached LampArrays
        private readonly List<LampArrayInfo> m_attachedLampArrays = new List<LampArrayInfo>();
        private readonly List<DeviceInformation> availableDevices = new List<DeviceInformation>();

        private DeviceWatcher m_deviceWatcher;
        private Dictionary<int, string> deviceIndexDict = new Dictionary<int, string>();
        private readonly object _lock = new object();
        

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            ViewModel.DeviceStatusMessage = "Status: Waiting - Start device watcher to list available devices.";
            ViewModel.DeviceWatcherStatusMessage = "DeviceWatcher Status: Not started.";
            ViewModel.ColorSettings = new ColorSettings();

            // Load the user config from file
            currentConfig = currentConfig.ReadConfigurationFile() ?? new UserConfig();
            ViewModel.ColorSettings.SetAllColorsFromUserConfig(currentConfig);
            ColorSetter.DefineKeyboardMainColor_FromRGB(currentConfig.StandardKeyColor.R, currentConfig.StandardKeyColor.G, currentConfig.StandardKeyColor.B);

            // Set up keyboard hook
            KeyStatesHandler.SetMonitoredKeys(new List<MonitoredKey> {
                new MonitoredKey(VK.NumLock,    onColor: currentConfig.GetVKOnColor(VK.NumLock),    offColor: currentConfig.GetVKOffColor(VK.NumLock)),
                new MonitoredKey(VK.CapsLock,   onColor: currentConfig.GetVKOnColor(VK.CapsLock),   offColor: currentConfig.GetVKOffColor(VK.CapsLock)),
                new MonitoredKey(VK.ScrollLock, onColor: currentConfig.GetVKOnColor(VK.ScrollLock), offColor: currentConfig.GetVKOffColor(VK.ScrollLock))
            });

            // If there's a device ID in the config, try to attach to it on startup, otherwise user will have to select a device
            if (!string.IsNullOrEmpty(currentConfig.DeviceId))
            {
                StartWatchingForLampArrays();
                // After this, the OnEnumerationCompleted event will try to attach to the saved device
            }
        }

        private async void AttachToSavedDevice()
        {
            var device = availableDevices.Find(d => d.Id == currentConfig.DeviceId);

            if (device != null)
            {
                LampArrayInfo? lampArrayInfo = await Attach_To_DeviceAsync(device);
                if (lampArrayInfo != null)
                {
                    ApplyLightingToDevice(lampArrayInfo);
                    UpdateSelectedDeviceDropdown();
                }
            }
        }

        private async void StartWatchingForLampArrays()
        {
            ViewModel.HasAttachedDevices = false;

            string combinedSelector = await GetKeyboardLampArrayDeviceSelectorAsync();

            m_deviceWatcher = DeviceInformation.CreateWatcher(combinedSelector);

            m_deviceWatcher.Added += Watcher_Added;
            m_deviceWatcher.Removed += Watcher_Removed;
            m_deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;

            // Add event handler OnDeviceWatcherStopped to the Stopped event
            m_deviceWatcher.Stopped += OnDeviceWatcherStopped;

            m_deviceWatcher.Start();

            if (m_deviceWatcher.Status == DeviceWatcherStatus.Started || m_deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
            {
                ViewModel.DeviceWatcherStatusMessage = "DeviceWatcher Status: Started.";
                ViewModel.IsWatcherRunning = true;
            }
            else
            {
                ViewModel.DeviceWatcherStatusMessage = "DeviceWatcher Status: Not started, something may have gone wrong.";
            }
        }

        private void StopWatchingForLampArrays()
        {
            if (KeyStatesHandler.hookIsActive == true)
            {
                KeyStatesHandler.StopHook(); // Stop the keyboard hook 
            }

            if (m_deviceWatcher.Status == DeviceWatcherStatus.Started || m_deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
            {
                m_deviceWatcher.Stop();
                ViewModel.IsWatcherRunning = false;
            }

            ColorSetter.SetCurrentDevice(null);
            ViewModel.HasAttachedDevices = false;
            currentConfig.DeviceId = "";

            UpdatAvailableLampArrayDisplayList();
        }

        private void UpdatAvailableLampArrayDisplayList()
        {
            string message;

            if (availableDevices.Count == 0)
            {
                message = "Status: Waiting - Start device watcher to list available devices.";
            }
            else
            {
                message = $"Available LampArrays: {availableDevices.Count}";
            }

            int deviceIndex = 0;

            lock (_lock)
            {
                devicesListForDropdown = new List<string>(); // Clear the list
                deviceIndexDict.Clear();

                lock (availableDevices)
                {
                    foreach (DeviceInformation device in availableDevices)
                    {
                        //message += $"\n{deviceIndex + 1}: {device.Name}";

                        // Add the device to the dropdown list and store its index in the dictionary
                        devicesListForDropdown.Add(device.Name);
                        if (deviceIndexDict.ContainsKey(deviceIndex))
                        {
                            deviceIndexDict[deviceIndex] = device.Id;
                        }
                        else
                        {
                            deviceIndexDict.Add(deviceIndex, device.Id);
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

        private void UpdateAttachedLampArrayDisplayList()
        {
            string message = $"Attached LampArrays: {m_attachedLampArrays.Count}";

            lock (_lock)
            {
                lock (m_attachedLampArrays)
                {
                    foreach (LampArrayInfo info in m_attachedLampArrays)
                    {
                        message += $"\n - {info.displayName} ({info.lampArray.LampArrayKind.ToString()}, {info.lampArray.LampCount} lamps, " + $"{(info.lampArray.IsAvailable ? "Available" : "Unavailable")})";
                    }
                }

                // Update ViewModel on UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.AttachedDevicesMessage = message;
                    ViewModel.HasAttachedDevices = m_attachedLampArrays.Count > 0;
                });
            }
        }

        async Task<LampArrayInfo?> AttachSelectedDevice_Async()
        {
            // Get the index of the selection from the GUI dropdown
            int selectedDeviceIndex = ViewModel.SelectedDeviceIndex;

            if (selectedDeviceIndex == -1 || deviceIndexDict.Count == 0 || selectedDeviceIndex > deviceIndexDict.Count)
            {
                ShowErrorMessage("Please select a device from the dropdown list.");
                return null;
            }

            string selectedDeviceID = deviceIndexDict[selectedDeviceIndex];
            DeviceInformation selectedDeviceObj = availableDevices.Find(device => device.Id == selectedDeviceID);

            if (selectedDeviceObj == null)
            {
                return null;
            }
            else
            {
                LampArrayInfo? device = await Attach_To_DeviceAsync(selectedDeviceObj);
                return device;
            }
        }

        private void UpdateSelectedDeviceDropdown()
        {
            // Get the index of the device that matches the current device ID
            int deviceIndex = availableDevices.FindIndex(device => device.Id == currentConfig.DeviceId);

            if (deviceIndex == -1 || deviceIndex > availableDevices.Count)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.SelectedDeviceIndex = -1;
                });
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.SelectedDeviceIndex = deviceIndex;
                });
            }
        }

        public async void ShowErrorMessage(string message)
        {
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot // Ensure the dialog is associated with the current window
            };

            await errorDialog.ShowAsync();
        }


        private void ApplyLightingToDevice(LampArrayInfo lampArrayInfo)
        {
            LampArray lampArray = lampArrayInfo.lampArray;

            currentConfig.DeviceId = lampArrayInfo.id;
            ColorSetter.SetCurrentDevice(lampArray);
            ColorSetter.SetInitialDefaultKeyboardColor(lampArray);
            KeyStatesHandler.UpdateKeyStatus();
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

        public enum HIDUsagePage : ushort
        {
            HID_USAGE_PAGE_GENERIC = 0x01,
            HID_USAGE_PAGE_GAME = 0x05,
            HID_USAGE_PAGE_LED = 0x08,
            HID_USAGE_PAGE_BUTTON = 0x09
        }

        public enum HIDGenericDesktopUsage : ushort
        {
            HID_USAGE_GENERIC_POINTER = 0x01,
            HID_USAGE_GENERIC_MOUSE = 0x02,
            HID_USAGE_GENERIC_JOYSTICK = 0x04,
            HID_USAGE_GENERIC_GAMEPAD = 0x05,
            HID_USAGE_GENERIC_KEYBOARD = 0x06,
            HID_USAGE_GENERIC_KEYPAD = 0x07,
            HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER = 0x08
        }


        // -------------------------------------- GUI EVENT HANDLERS --------------------------------------
        private void buttonStartWatch_Click(object sender, RoutedEventArgs e)
        {
            // Clear the current list of attached devices
            m_attachedLampArrays.Clear();
            availableDevices.Clear();
            UpdateAttachedLampArrayDisplayList();

            StartWatchingForLampArrays();
        }

        private void buttonStopWatch_Click(object sender, RoutedEventArgs e)
        {
            m_attachedLampArrays.Clear();
            availableDevices.Clear();
            UpdateAttachedLampArrayDisplayList();
            UpdatAvailableLampArrayDisplayList();
            StopWatchingForLampArrays();
        }

        private async void buttonApply_Click(object sender, RoutedEventArgs e)
        {
            // If there was a device attached, remove it
            if (ColorSetter.CurrentDevice != null)
            {
                ColorSetter.SetCurrentDevice(null);
            }

            LampArrayInfo? selectedLampArrayInfo = await AttachSelectedDevice_Async();

            if (selectedLampArrayInfo != null)
            {
                ApplyLightingToDevice(selectedLampArrayInfo);
            }
        }

        private async void buttonSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // Save the current color settings to the ViewModel
            ViewModel.ColorSettings.SetAllColorsFromGUI(ViewModel);

            ColorSetter.DefineKeyboardMainColor_FromNameAndBrightness(ViewModel.ColorSettings.DefaultColor, ViewModel.ColorSettings.Brightness);

            // Update the key states to reflect the new color settings
            var scrollOnColor = (ViewModel.ColorSettings.ScrollLockOnColor.R, ViewModel.ColorSettings.ScrollLockOnColor.G, ViewModel.ColorSettings.ScrollLockOnColor.B);
            var capsOnColor = (ViewModel.ColorSettings.CapsLockOnColor.R, ViewModel.ColorSettings.CapsLockOnColor.G, ViewModel.ColorSettings.CapsLockOnColor.B);
            var numOnColor = (ViewModel.ColorSettings.NumLockOnColor.R, ViewModel.ColorSettings.NumLockOnColor.G, ViewModel.ColorSettings.NumLockOnColor.B);
            var numOffColor = (ViewModel.ColorSettings.NumLockOffColor.R, ViewModel.ColorSettings.NumLockOffColor.G, ViewModel.ColorSettings.NumLockOffColor.B);
            var capsOffColor = (ViewModel.ColorSettings.CapsLockOffColor.R, ViewModel.ColorSettings.CapsLockOffColor.G, ViewModel.ColorSettings.CapsLockOffColor.B);
            var scrollOffColor = (ViewModel.ColorSettings.ScrollLockOffColor.R, ViewModel.ColorSettings.ScrollLockOffColor.G, ViewModel.ColorSettings.ScrollLockOffColor.B);

            KeyStatesHandler.SetMonitoredKeys(new List<MonitoredKey> {
                new MonitoredKey(VK.NumLock, onColor: numOnColor, offColor: numOffColor),
                new MonitoredKey(VK.CapsLock, onColor: capsOnColor, offColor: capsOffColor),
                new MonitoredKey(VK.ScrollLock, onColor: scrollOnColor, offColor: scrollOffColor)
            });

            Dictionary<ToggleAbleKeys, (Windows.UI.Color onColor, Windows.UI.Color offColor)> colorUpdateDict = new Dictionary<ToggleAbleKeys, (Windows.UI.Color onColor, Windows.UI.Color offColor)>
            {
                { VK.NumLock,       (onColor: ViewModel.ColorSettings.NumLockOnColor,       offColor: ViewModel.ColorSettings.NumLockOffColor)      },
                { VK.CapsLock,      (onColor: ViewModel.ColorSettings.CapsLockOnColor,      offColor: ViewModel.ColorSettings.CapsLockOffColor)     },
                { VK.ScrollLock,    (onColor: ViewModel.ColorSettings.ScrollLockOnColor,    offColor: ViewModel.ColorSettings.ScrollLockOffColor)   }
            };

            KeyStatesHandler.UpdateMonitoredKeyColors(colorUpdateDict);

            // If there was a device attached, update the colors
            if (ColorSetter.CurrentDevice != null)
            {
                ColorSetter.SetInitialDefaultKeyboardColor(ColorSetter.CurrentDevice);
                ColorSetter.SetMonitoredKeysColor(KeyStatesHandler.monitoredKeys, ColorSetter.CurrentDevice);
            }

            // Save the color settings to the configuration file
            bool result = await currentConfig.WriteConfigurationFile_Async();
            if (!result)
            {
                ShowErrorMessage("Failed to save the color settings to the configuration file.");
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var colorPropertyName = button.Tag as string;

            var flyout = new Flyout();
            var colorPicker = new ColorPicker
            {
                MinWidth = 300,
                MinHeight = 400
            };

            var currentColor = (Windows.UI.Color)ViewModel.GetType().GetProperty(colorPropertyName).GetValue(ViewModel);
            colorPicker.Color = currentColor;

            colorPicker.ColorChanged += (s, args) =>
            {
                ViewModel.GetType().GetProperty(colorPropertyName).SetValue(ViewModel, args.NewColor);
            };

            flyout.Content = colorPicker;
            flyout.ShowAt(button);
        }


        // ---------------------------------------------------------------------------------------------------
    }
}
