using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
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

        public bool DEBUGMODE = false;

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
            #if DEBUG
                DEBUGMODE = true;
            #endif

            InitializeComponent();
            ViewModel = new MainViewModel();
            ViewModel.DeviceStatusMessage = "Status: Waiting - Start device watcher to list available devices.";
            ViewModel.DeviceWatcherStatusMessage = "DeviceWatcher Status: Not started.";
            ViewModel.ColorSettings = new ColorSettings();

            // Load the user config from file
            currentConfig = currentConfig.ReadConfigurationFile() ?? new UserConfig();
            ViewModel.ColorSettings.SetAllColorsFromUserConfig(currentConfig);
            ColorSetter.DefineKeyboardMainColor_FromRGB(currentConfig.StandardKeyColor);

            // Set up keyboard hook
            KeyStatesHandler.SetMonitoredKeys(new List<MonitoredKey> {
                new MonitoredKey(VK.NumLock,    onColor: currentConfig.GetVKOnColor(VK.NumLock),    offColor: currentConfig.GetVKOffColor(VK.NumLock)),
                new MonitoredKey(VK.CapsLock,   onColor: currentConfig.GetVKOnColor(VK.CapsLock),   offColor: currentConfig.GetVKOffColor(VK.CapsLock)),
                new MonitoredKey(VK.ScrollLock, onColor: currentConfig.GetVKOnColor(VK.ScrollLock), offColor: currentConfig.GetVKOffColor(VK.ScrollLock))
            });

            ForceUpdateButtonBackgrounds();
            ForceUpdateAllButtonGlyphs();

            // If there's a device ID in the config, try to attach to it on startup, otherwise user will have to select a device
            if (!string.IsNullOrEmpty(currentConfig.DeviceId))
            {
                StartWatchingForLampArrays();
                // After this, the OnEnumerationCompleted event will try to attach to the saved device
            }

            URLHandler.ProvideUserConfig(currentConfig);
            URLHandler.ProvideWindow(this);
        }

        // Getter and setter for user config
        internal UserConfig CurrentConfig
        {
            get => currentConfig;
            set => currentConfig = value;
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

        // Forces the color buttons to update their backgrounds to reflect the current color settings. Normally they update by event, but this is needed for the initial load
        // This doesn't work when put in the viewmodel class for some reason
        private void ForceUpdateButtonBackgrounds()
        {
            buttonNumLockOn.Background = new SolidColorBrush(ViewModel.ColorSettings.NumLockOnColor);
            buttonNumLockOff.Background = new SolidColorBrush(ViewModel.ColorSettings.NumLockOffColor);
            buttonCapsLockOn.Background = new SolidColorBrush(ViewModel.ColorSettings.CapsLockOnColor);
            buttonCapsLockOff.Background = new SolidColorBrush(ViewModel.ColorSettings.CapsLockOffColor);
            buttonScrollLockOn.Background = new SolidColorBrush(ViewModel.ColorSettings.ScrollLockOnColor);
            buttonScrollLockOff.Background = new SolidColorBrush(ViewModel.ColorSettings.ScrollLockOffColor);
            buttonDefaultColor.Background = new SolidColorBrush(ViewModel.ColorSettings.DefaultColor);
        }

        private void ForceUpdateAllButtonGlyphs()
        {
            // Update the sync glpyhs
            foreach (var button in new[] { buttonNumLockOn, buttonNumLockOff, buttonCapsLockOn, buttonCapsLockOff, buttonScrollLockOn, buttonScrollLockOff })
            {
                if (button == null)
                    return;

                //string glyph = ManuallyGetGlyph((string)button.Tag);
                string glyph = ViewModel.GetSyncGlyph_ByButtonObject(button);
                FontIcon? fontIcon = GetButtonGlyphObject(button);

                if (fontIcon != null)
                {
                    Microsoft.UI.Xaml.Media.FontFamily glyphFont = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets");
                    fontIcon.FontFamily = glyphFont;
                    fontIcon.Glyph = glyph;
                }
            }
        }

        // Find the FontIcon object within the button, which has the glyph
        private FontIcon? GetButtonGlyphObject(Button button)
        {
            // If the FontIcon is nested in the button, find it
            if (button.Content is StackPanel stackPanel)
            {
                foreach (var child in stackPanel.Children)
                {
                    if (child is FontIcon fontIcon)
                    {
                        return fontIcon;
                    }
                }
            }
            // If the FontIcon is set directly as the button content
            else if (button.Content is FontIcon fontIcon)
            {
                return fontIcon;
            }

            // Otherwise not found
            return null;
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

        private void openConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            currentConfig.OpenConfigFolder();
        }

        private void restoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            currentConfig.RestoreDefault();
            ViewModel.ColorSettings.SetAllColorsFromUserConfig(currentConfig);
            ForceUpdateButtonBackgrounds();
            ForceUpdateAllButtonGlyphs();
        }

        internal async void ApplyAndSaveSettings(bool saveFile = true, UserConfig? newConfig = null)
        {
            ColorSettings colorSettings = ViewModel.ColorSettings;

            // Save the current color settings to the ViewModel
            if (newConfig == null) {
                colorSettings.SetAllColorsFromGUI(ViewModel);
                ColorSetter.DefineKeyboardMainColor_FromName(colorSettings.DefaultColor);
            }
            else
            {
                colorSettings.SetAllColorsFromUserConfig(newConfig);
                ColorSetter.DefineKeyboardMainColor_FromRGB(newConfig.StandardKeyColor);
            }

            // Update the key states to reflect the new color settings
            var scrollOnColor = (colorSettings.ScrollLockOnColor.R, colorSettings.ScrollLockOnColor.G, colorSettings.ScrollLockOnColor.B);
            var capsOnColor = (colorSettings.CapsLockOnColor.R, colorSettings.CapsLockOnColor.G, colorSettings.CapsLockOnColor.B);
            var numOnColor = (colorSettings.NumLockOnColor.R, colorSettings.NumLockOnColor.G, colorSettings.NumLockOnColor.B);
            var numOffColor = (colorSettings.NumLockOffColor.R, colorSettings.NumLockOffColor.G, colorSettings.NumLockOffColor.B);
            var capsOffColor = (colorSettings.CapsLockOffColor.R, colorSettings.CapsLockOffColor.G, colorSettings.CapsLockOffColor.B);
            var scrollOffColor = (colorSettings.ScrollLockOffColor.R, colorSettings.ScrollLockOffColor.G, colorSettings.ScrollLockOffColor.B);

            var defaultColor = (colorSettings.DefaultColor.R, colorSettings.DefaultColor.G, colorSettings.DefaultColor.B);

            // Sync to defaults if set to do so. Janky but whatever
            if (colorSettings.SyncNumLockOnColor)
                numOnColor = defaultColor;
            if (colorSettings.SyncNumLockOffColor)
                numOffColor = defaultColor;
            if (colorSettings.SyncCapsLockOnColor)
                capsOnColor = defaultColor;
            if (colorSettings.SyncCapsLockOffColor)
                capsOffColor = defaultColor;
            if (colorSettings.SyncScrollLockOnColor)
                scrollOnColor = defaultColor;
            if (colorSettings.SyncScrollLockOffColor)
                scrollOffColor = defaultColor;

            // TODO: Add binding to new settings to link on/off colors to standard color
            List<MonitoredKey> monitoredKeysList = new List<MonitoredKey> {
                new MonitoredKey(VK.NumLock,    onColor: numOnColor,    offColor: numOffColor,      onColorTiedToStandard: colorSettings.SyncNumLockOnColor,    offColorTiedToStandard: colorSettings.SyncNumLockOffColor),
                new MonitoredKey(VK.CapsLock,   onColor: capsOnColor,   offColor: capsOffColor,     onColorTiedToStandard: colorSettings.SyncCapsLockOnColor,   offColorTiedToStandard: colorSettings.SyncCapsLockOffColor),
                new MonitoredKey(VK.ScrollLock, onColor: scrollOnColor, offColor: scrollOffColor,   onColorTiedToStandard: colorSettings.SyncScrollLockOnColor, offColorTiedToStandard: colorSettings.SyncScrollLockOffColor)
            };

            KeyStatesHandler.SetMonitoredKeys(monitoredKeysList);

            KeyStatesHandler.UpdateMonitoredKeyColors(defaultColor, monitoredKeysList);
            currentConfig = new UserConfig(defaultColor, monitoredKeysList);

            // If there was a device attached, update the colors
            if (ColorSetter.CurrentDevice != null)
            {
                ColorSetter.SetInitialDefaultKeyboardColor(ColorSetter.CurrentDevice);
                ColorSetter.SetMonitoredKeysColor(KeyStatesHandler.monitoredKeys, ColorSetter.CurrentDevice);
            }

            ForceUpdateAllButtonGlyphs();

            if (ColorSetter.CurrentDevice != null)
            {
                currentConfig.DeviceId = ColorSetter.CurrentDevice.DeviceId;
            }

            if (saveFile)
            {
                bool result = await currentConfig.WriteConfigurationFile_Async();
                if (!result)
                {
                    ShowErrorMessage("Failed to save the color settings to the configuration file.");
                }
            }
        }

        private async void buttonSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndSaveSettings(saveFile: true);  
        }

        // This is the button within the flyout  menu
        private void SyncToStandardColor_Click(string colorPropertyName, ColorPicker colorPicker, Button parentButton, Flyout colorPickerFlyout)
        {
            string defaultColorPropertyName = "DefaultColor";
            var defaultColor = (Windows.UI.Color)ViewModel.GetType().GetProperty(defaultColorPropertyName).GetValue(ViewModel);
            colorPicker.Color = defaultColor;

            colorPicker.ColorChanged += (s, args) =>
            {
                ViewModel.GetType().GetProperty(colorPropertyName).SetValue(ViewModel, args.NewColor);
                parentButton.Background = new SolidColorBrush(args.NewColor); // Update the button color to reflect the default color
            };

            ViewModel.UpdateSyncSetting(syncSetting: true, colorPropertyName: colorPropertyName);

            ForceUpdateAllButtonGlyphs();

            // Close the flyout
            colorPickerFlyout.Hide();
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var colorPropertyName = button.Tag as string;
            ViewModel.UpdateSyncSetting(syncSetting: false, colorPropertyName: colorPropertyName); // Disabling syncing if the user opens the color picker
            ForceUpdateAllButtonGlyphs();

            // Update the button color from the settings as soon as the button is clicked. Also will be updated later
            button.Background = new SolidColorBrush((Windows.UI.Color)ViewModel.GetType().GetProperty(colorPropertyName).GetValue(ViewModel));

            var flyout = new Flyout();
            var stackPanel = new StackPanel();

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
                button.Background = new SolidColorBrush(args.NewColor); // Update the button color to reflect the new color

                // If updating the DefaultColor, check the ColorSettings for which keys are set to sync to default, and update their buttons too
                if (colorPropertyName == "DefaultColor")
                {
                    // This janky, should be refactored to a loop with a list of keys but this will work for now
                    if (ViewModel.ColorSettings.SyncNumLockOnColor)
                        buttonNumLockOn.Background = new SolidColorBrush(args.NewColor);
                    if (ViewModel.ColorSettings.SyncNumLockOffColor)
                        buttonNumLockOff.Background = new SolidColorBrush(args.NewColor);
                    if (ViewModel.ColorSettings.SyncCapsLockOnColor)
                        buttonCapsLockOn.Background = new SolidColorBrush(args.NewColor);
                    if (ViewModel.ColorSettings.SyncCapsLockOffColor)
                        buttonCapsLockOff.Background = new SolidColorBrush(args.NewColor);
                    if (ViewModel.ColorSettings.SyncScrollLockOnColor)
                        buttonScrollLockOn.Background = new SolidColorBrush(args.NewColor);
                    if (ViewModel.ColorSettings.SyncScrollLockOffColor)
                        buttonScrollLockOff.Background = new SolidColorBrush(args.NewColor);
                }
            };

            // Create a button in the flyout to sync the color setting to the default/standard color
            var syncButton = new Button
            {
                Content = "Sync to default color",
                Margin = new Thickness(0, 10, 0, 0)
            };

            // Attach the event handler to the button's Click event
            syncButton.Click += (s, args) => SyncToStandardColor_Click(colorPropertyName, colorPicker, button, flyout);

            stackPanel.Children.Add(colorPicker);

            // Add the sync button if it's not the default color button
            if (colorPropertyName != "DefaultColor")
                stackPanel.Children.Add(syncButton);

            flyout.Content = stackPanel;
            flyout.ShowAt(button);
        }


        // ---------------------------------------------------------------------------------------------------
    }
}
