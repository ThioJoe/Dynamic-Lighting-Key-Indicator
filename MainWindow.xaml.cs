using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;
using Windows.Graphics;

#nullable enable
#pragma warning disable IDE0079 // Remove unnecessary suppression

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
        ObservableCollection<string> devicesListForDropdown = [];

        static UserConfig currentConfig = new();
        static UserConfig configSavedOnDisk = new();

        // Currently attached LampArrays
        private readonly ObservableCollection<LampArrayInfo> m_attachedLampArrays = [];
        private readonly ObservableCollection<DeviceInformation> availableDevices = [];


        private DeviceWatcher? m_deviceWatcher;
        private readonly Dictionary<int, string> deviceIndexDict = [];
        private readonly object _lock = new();

        public const string MainIconFileName = "Icon.ico";
        public const string MainWindowTitle = "Dynamic Lighting Key Indicator";
        public const string StartupTaskId = "Dynamic-Lighting-Key-Indicator-StartupTask";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, IntPtr lParam);

        public MainWindow(string[] args)
        {
            #if DEBUG
                DEBUGMODE = true;
            #endif

            InitializeComponent();
            this.Activated += MainWindow_Activated;
            this.Title = MainWindowTitle;

            Microsoft.Windows.AppLifecycle.AppInstance thisInstance = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent();
            thisInstance.Activated += MainInstance_Activated;

            // Initialize the system tray
            SystemTray systemTray = new SystemTray(this);
            systemTray.InitializeSystemTray();
            Debug.WriteLine("System tray initialized.");

            // Use WM_SETICON message to set the window title bar icon
            IntPtr hIcon;
            Icon? icon = SystemTray.LoadIconFromResource("Dynamic_Lighting_Key_Indicator.Assets.Icon.ico");
            if (icon == null)
                hIcon = SystemTray.GetDefaultIconHandle();
            else
                hIcon = icon.Handle;

            SendMessage((IntPtr)this.AppWindow.Id.Value, 0x0080, 1, hIcon); // WM_SETICON = 0x0080, ICON_BIG = 1

            // Initialize the ViewModel
            ViewModel = new MainViewModel(mainWindowPassIn: this)
            {
                DeviceStatusMessage = "Status: Waiting - Start device watcher to list available devices.",
                DeviceWatcherStatusMessage = "DeviceWatcher Status: Not started.",
                ColorSettings = new ColorSettings(),
            };

            // Load the user config from file
            currentConfig = UserConfig.ReadConfigurationFile() ?? new UserConfig();
            configSavedOnDisk = (UserConfig)currentConfig.Clone();

            ViewModel.ColorSettings.SetAllColorsFromUserConfig(currentConfig);
            ViewModel.ApplyAppSettingsFromUserConfig(currentConfig);
            ViewModel.CheckAndUpdateSaveButton();
            ColorSetter.DefineKeyboardMainColor_FromRGB(currentConfig.StandardKeyColor);

            // Set up keyboard hook
            #pragma warning disable IDE0028 // Use 'await' when calling this method.
            KeyStatesHandler.SetMonitoredKeys(keys: new List<MonitoredKey> {
                new(VK.NumLock,    onColor: currentConfig.GetVKOnColor(VK.NumLock),    offColor: currentConfig.GetVKOffColor(VK.NumLock)),
                new(VK.CapsLock,   onColor: currentConfig.GetVKOnColor(VK.CapsLock),   offColor: currentConfig.GetVKOffColor(VK.CapsLock)),
                new(VK.ScrollLock, onColor: currentConfig.GetVKOnColor(VK.ScrollLock), offColor: currentConfig.GetVKOffColor(VK.ScrollLock))
            });
            #pragma warning restore IDE0028 // Use 'await' when calling this method.

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

            // Set initial window size, and check if it should start minimized
            this.AppWindow.Resize(new SizeInt32(1200, 1250));
            if (currentConfig.StartMinimizedToTray)
            {
                systemTray.MinimizeToTray();
            }
            else
            {
                this.Activate();
            }

        }

        // ------------------------------- Getters / Setters -------------------------------
        // Getter and setter for user config
        internal UserConfig CurrentConfig
        {
            get => currentConfig;
            set => currentConfig = value;
        }

        internal UserConfig SavedConfig
        {
            get => configSavedOnDisk;
            set => configSavedOnDisk = value;
        }

        // ------------------------------- Methods -------------------------------

        public static StartupTaskState ChangWindowsStartupState(bool enableAtStartupRequestedState)
        {
            // TaskId is set in the Package.appxmanifest file under <uap5:StartupTask> extension
            StartupTask startupTask = StartupTask.GetAsync(StartupTaskId).GetAwaiter().GetResult();
            Debug.WriteLine("Original startup state: {0}", startupTask.State);

            // If the startup state already matches the desired state, return the current state
            if (MatchesStartupState(enableAtStartupRequestedState))
            {
                Debug.WriteLine($"Startup state ({startupTask.State}) already matches the desired state.");
                return startupTask.State;
            }

            // Change the startup state based on input parameter
            StartupTaskState newDesiredState;
            if (enableAtStartupRequestedState == true)
            {
                newDesiredState = StartupTaskState.Enabled;
                _ = startupTask.RequestEnableAsync(); // ensure that you are on a UI thread when you call RequestEnableAsync()
            }
            else
            {
                newDesiredState = StartupTaskState.Disabled;
                startupTask.Disable(); // ensure that you are on a UI thread when you call DisableAsync()
            }

            // Check again to see if it matches and print a debug message
            if (startupTask.State == newDesiredState)
                Debug.WriteLine($"Success: New startup state: {startupTask.State}");
            else
                Debug.WriteLine($"Failed to change the startup state to {newDesiredState}. Current state: {startupTask.State}");

            return startupTask.State;
        }

        public static StartupTaskState GetStartupTaskState_Async()
        {
            StartupTask startupTask = StartupTask.GetAsync(StartupTaskId).GetAwaiter().GetResult();
            return startupTask.State;
        }

        public static StartupTaskState GetStartupTaskState()
        {
            StartupTask startupTask = StartupTask.GetAsync(StartupTaskId).GetAwaiter().GetResult();
            return startupTask.State;
        }

        public static bool MatchesStartupState(bool desiredStartupState)
        {
            StartupTaskState currentStartupState = GetStartupTaskState_Async();
            List<StartupTaskState> possibleEnabledStates = [StartupTaskState.Enabled, StartupTaskState.EnabledByPolicy];
            List<StartupTaskState> possibleDisabledStates = [StartupTaskState.Disabled, StartupTaskState.DisabledByPolicy, StartupTaskState.DisabledByUser];

            if (
                   (desiredStartupState == true && possibleEnabledStates.Contains(currentStartupState))
                || (desiredStartupState == false && possibleDisabledStates.Contains(currentStartupState))
            )
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private async void AttachToSavedDevice()
        {
            // If current device ID is null or empty it probably means the user stopped watching so it reset, so don't try to attach or else it will throw an exception
            if (currentConfig.DeviceId == null || currentConfig.DeviceId == "")
            {
                return;
            }

            DeviceInformation? device = availableDevices.First(d => d.Id == currentConfig.DeviceId);

            if (device != null)
            {
                LampArrayInfo? lampArrayInfo = await AttachToDevice_Async(device);
                if (lampArrayInfo != null)
                {
                    ApplyLightingToDevice_AndSaveIdToConfig(lampArrayInfo);
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

            if (m_deviceWatcher != null && (m_deviceWatcher.Status == DeviceWatcherStatus.Started || m_deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
            {
                m_deviceWatcher.Stop();
                ViewModel.IsWatcherRunning = false;
            }
            else if (m_deviceWatcher == null)
            {
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
                message = $"Available Devices: {availableDevices.Count}";
            }

            int deviceIndex = 0;

            lock (_lock)
            {
                devicesListForDropdown = []; // Clear the list
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
            string message = "";

            lock (_lock)
            {
                lock (m_attachedLampArrays)
                {
                    if (m_attachedLampArrays.Count == 0)
                    {
                        message = "Attached To: None";
                    }
                    else
                    {
                        message = $"Attached To: ";
                        foreach (LampArrayInfo info in m_attachedLampArrays)
                        {
                            message += $"{info.displayName} ({info.lampArray.LampArrayKind}, {info.lampArray.LampCount} lights)";
                        }
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

        async Task<LampArrayInfo?> AttachSelectedDeviceFromDropdown_Async()
        {
            // Get the index of the selection from the GUI dropdown
            int selectedDeviceIndex = ViewModel.SelectedDeviceIndex;

            if (selectedDeviceIndex == -1 || deviceIndexDict.Count == 0 || selectedDeviceIndex > deviceIndexDict.Count)
            {
                ShowErrorMessage("Please select a device from the dropdown list.");
                return null;
            }

            string selectedDeviceID = deviceIndexDict[selectedDeviceIndex];
            DeviceInformation? selectedDeviceObj = availableDevices.First(device => device.Id == selectedDeviceID);

            if (selectedDeviceObj == null)
            {
                return null;
            }
            else
            {
                LampArrayInfo? device = await AttachToDevice_Async(selectedDeviceObj);
                return device;
            }
        }

        // Check if the currently attached device matches the selected device in the dropdown, to know whether to enable or disable the apply button
        // Return null if not applicable / no attached device, false if it doesn't match, true if it does
        public bool? AttachedDeviceMatchesDropdownSelection()
        {
            if (ColorSetter.CurrentDevice == null || availableDevices.Count == 0 || m_attachedLampArrays.Count == 0 || ViewModel.SelectedDeviceIndex == -1)
            {
                return null; // If there's no device attached, return null
            }

            // Currently there's only one attached but this is set up to handle multiple in the future
            List<string> attachedDeviceIds = m_attachedLampArrays.Select(info => info.id).ToList();
            // Get the current dropdown selection
            int selectedDeviceIndex = ViewModel.SelectedDeviceIndex;

            foreach (string id in attachedDeviceIds)
            {
                // Check if the attached device ID matches the selected device ID using deviceIndexDict
                if (deviceIndexDict[selectedDeviceIndex] == id)
                {
                    return true;
                }
            }

            // At this point it means none were found
            return false;
        }

        // Updates the dropdown list to reflect the current device ID if it was selected programatically, like on startup
        private void UpdateSelectedDeviceDropdown()
        {
            // Get the index of the device that matches the current device ID
            int deviceIndex = availableDevices.ToList().FindIndex(device => device.Id == currentConfig.DeviceId);

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
            ContentDialog errorDialog = new()
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot // Ensure the dialog is associated with the current window
            };

            await errorDialog.ShowAsync();
        }


        private async void ApplyLightingToDevice_AndSaveIdToConfig(LampArrayInfo lampArrayInfo)
        {
            LampArray lampArray = lampArrayInfo.lampArray;

            // Update the current and saved config to reflect the new device ID
            currentConfig.DeviceId = lampArrayInfo.id;
            if (configSavedOnDisk.DeviceId != currentConfig.DeviceId)
            {
                configSavedOnDisk.DeviceId = lampArrayInfo.id;
                await configSavedOnDisk.WriteConfigurationFile_Async();
            }

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

        // Determine whether to use white or black text based on the background color
        static SolidColorBrush DetermineGlyphColor(Button button)
        {
            var buttonBackgroundColorBrush = button.Background as SolidColorBrush;
            Windows.UI.Color? bgColor = buttonBackgroundColorBrush?.Color;

            // If it's null just return the original color
            if (bgColor == null || bgColor?.R == null || bgColor?.G == null || bgColor?.R == null)
                return (SolidColorBrush)button.Background;

            double luminance = (0.299 * bgColor.Value.R + 0.587 * bgColor.Value.G + 0.114 * bgColor.Value.B) / 255;
            bool useWhite = luminance < 0.5;
            SolidColorBrush newGlyphColor = useWhite ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Black);

            return newGlyphColor;
        }

        private void UpdateOtherSettingVisuals()
        {
            // Check if the app is set to start with windows
            bool isStartupEnabled = MatchesStartupState(true);
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
                SolidColorBrush iconColor = DetermineGlyphColor(button);

                if (fontIcon != null)
                {
                    Microsoft.UI.Xaml.Media.FontFamily glyphFont = new("Segoe MDL2 Assets");
                    fontIcon.FontFamily = glyphFont;
                    fontIcon.Foreground = iconColor;
                    fontIcon.Glyph = glyph;
                }
            }
        }

        // Find the FontIcon object within the button, which has the glyph
        private static FontIcon? GetButtonGlyphObject(Button button)
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

        internal async void ApplyAndSaveSettings(bool saveFile = true, UserConfig? newConfig = null)
        {
            ColorSettings colorSettings = ViewModel.ColorSettings;

            // Save the current color settings to the ViewModel
            if (newConfig == null)
            {
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
            List<MonitoredKey> monitoredKeysList = [
                new(VK.NumLock,    onColor: numOnColor,    offColor: numOffColor,      onColorTiedToStandard: colorSettings.SyncNumLockOnColor,    offColorTiedToStandard: colorSettings.SyncNumLockOffColor),
                new(VK.CapsLock,   onColor: capsOnColor,   offColor: capsOffColor,     onColorTiedToStandard: colorSettings.SyncCapsLockOnColor,   offColorTiedToStandard: colorSettings.SyncCapsLockOffColor),
                new(VK.ScrollLock, onColor: scrollOnColor, offColor: scrollOffColor,   onColorTiedToStandard: colorSettings.SyncScrollLockOnColor, offColorTiedToStandard: colorSettings.SyncScrollLockOffColor)
            ];

            KeyStatesHandler.SetMonitoredKeys(monitoredKeysList);

            KeyStatesHandler.UpdateMonitoredKeyColors(monitoredKeysList);
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

            // Ensures the minimized tray setting is updated even though the toggle should auto update from being bound
            currentConfig.StartMinimizedToTray = ViewModel.StartMinimizedToTray;

            if (saveFile)
            {
                configSavedOnDisk = (UserConfig)currentConfig.Clone(); // Save the current config to the saved config, then save the current config to the file
                bool result = await currentConfig.WriteConfigurationFile_Async();
                if (!result)
                {
                    ShowErrorMessage("Failed to save the color settings to the configuration file.");
                }
            }

            // Update the Save button enabled status
            ViewModel.ColorSettings.SetAllColorsFromUserConfig(currentConfig);
            ViewModel.CheckAndUpdateSaveButton();
        }

        // --------------------------------------------------- CLASSES AND ENUMS ---------------------------------------------------
        internal class LampArrayInfo(string id, string displayName, LampArray lampArray)
        {
            public readonly string id = id;
            public readonly string displayName = displayName;
            public readonly LampArray lampArray = lampArray;
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

        // ----------------------------------- GENERAL EVENT HANDLERS -----------------------------------

        private void MainWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine($"Window activation state: {args.WindowActivationState}");
        }

        // This will handle the redirected activation from the protocol (URL) activation of further instances
        private void MainInstance_Activated(object? sender, AppActivationArguments args)
        {
            // Capture the relevant data immediately
            var isProtocol = args.Kind == ExtendedActivationKind.Protocol;
            Uri? protocolUri = null;

            if (isProtocol)
            {
                var protocolArgs = args.Data as IProtocolActivatedEventArgs;
                if (protocolArgs?.Uri != null)
                {
                    protocolUri = protocolArgs.Uri;
                }
            }

            // Now use the dispatcher with our captured data
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                if (isProtocol && protocolUri != null)
                {
                    URLHandler.ProcessUri(protocolUri);
                }
            });
        }

        // -------------------------------------- GUI EVENT HANDLERS --------------------------------------

        private void ButtonStartWatch_Click(object sender, RoutedEventArgs e)
        {
            // Clear the current list of attached devices
            m_attachedLampArrays.Clear();
            availableDevices.Clear();
            UpdateAttachedLampArrayDisplayList();

            StartWatchingForLampArrays();
        }

        private void ButtonStopWatch_Click(object sender, RoutedEventArgs e)
        {
            m_attachedLampArrays.Clear();
            availableDevices.Clear();
            UpdateAttachedLampArrayDisplayList();
            UpdatAvailableLampArrayDisplayList();
            StopWatchingForLampArrays();
        }

        private async void ButtonApply_Click(object sender, RoutedEventArgs e)
        {
            // If there was a device attached, remove it from our current device variable
            if (ColorSetter.CurrentDevice != null)
            {
                ColorSetter.SetCurrentDevice(null);
            }

            // Reset the m_attachedLampArrays list. In the future might allow multiple devices
            m_attachedLampArrays.Clear();

            LampArrayInfo? selectedLampArrayInfo = await AttachSelectedDeviceFromDropdown_Async();

            if (selectedLampArrayInfo != null)
            {
                ApplyLightingToDevice_AndSaveIdToConfig(selectedLampArrayInfo);
            }

            // Upon applying the settings, it should trigger event handlers in MainViewModel for HasAttachedDevices, attachedDevicesMessage, EnableApplyButton etc.
        }

        private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            UserConfig.OpenConfigFolder();
        }

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            currentConfig.RestoreDefault();
            ViewModel.ColorSettings.SetAllColorsFromUserConfig(currentConfig);
            ForceUpdateButtonBackgrounds();
            ForceUpdateAllButtonGlyphs();
        }
        private void OpenLightingSettings_Click(object sender, RoutedEventArgs e)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ms-settings:personalization-lighting",
                UseShellExecute = true
            };
            Process.Start(processStartInfo);
        }

        private void ButtonSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndSaveSettings(saveFile: true);
        }

        // This is the button within the flyout  menu
        private void SyncToStandardColor_Click(string colorPropertyName, ColorPicker colorPicker, Button parentButton, Flyout colorPickerFlyout)
        {
            string defaultColorPropertyName = "DefaultColor";
            Windows.UI.Color? defaultColor = (Windows.UI.Color?)ViewModel?.GetType()?.GetProperty(defaultColorPropertyName)?.GetValue(ViewModel);
            if (defaultColor == null)
                return;

            colorPicker.Color = (Windows.UI.Color)defaultColor;

            colorPicker.ColorChanged += (s, args) =>
            {
                ViewModel?.GetType()?.GetProperty(colorPropertyName)?.SetValue(ViewModel, args.NewColor);
                parentButton.Background = new SolidColorBrush(args.NewColor); // Update the button color to reflect the default color
            };

            ViewModel?.UpdateSyncSetting(syncSetting: true, colorPropertyName: colorPropertyName);

            ForceUpdateAllButtonGlyphs();

            // Close the flyout
            colorPickerFlyout.Hide();
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
            {
                Debug.WriteLine("Button is null.");
                return;
            }
                

            if (button.Tag is not string colorPropertyName)
            {
                Debug.WriteLine("Color property name is null.");
                return;
            }

            ViewModel.UpdateSyncSetting(syncSetting: false, colorPropertyName: colorPropertyName); // Disabling syncing if the user opens the color picker
            ForceUpdateAllButtonGlyphs();

            // Update the button color from the settings as soon as the button is clicked. Also will be updated later
            SolidColorBrush? newBGColor = ViewModel?.GetType()?.GetProperty(colorPropertyName)?.GetValue(ViewModel) as SolidColorBrush;
            if (button != null && newBGColor != null)
                button.Background = newBGColor;

            // Next show the color picker flyout
            var flyout = new Flyout();
            // Add event handler for when the flyout is closed
            flyout.Closed += (s, args) => ViewModel.CheckAndUpdateSaveButton();

            var stackPanel = new StackPanel();
            var colorPicker = new ColorPicker
            {
                MinWidth = 300,
                MinHeight = 400
            };
            Windows.UI.Color? currentColor = (Windows.UI.Color?)ViewModel?.GetType()?.GetProperty(colorPropertyName)?.GetValue(ViewModel);
            if (currentColor == null)
            {
                Debug.WriteLine("Current color is null.");
                return;
            }

            colorPicker.Color = (Windows.UI.Color)currentColor;

            colorPicker.ColorChanged += (s, args) =>
            {
                if (button == null)
                    return;

                ViewModel?.GetType()?.GetProperty(colorPropertyName)?.SetValue(ViewModel, args.NewColor);
                button.Background = new SolidColorBrush(args.NewColor); // Update the button color to reflect the new color

                // If updating the DefaultColor, check the ColorSettings for which keys are set to sync to default, and update their buttons too
                if (colorPropertyName == "DefaultColor")
                {
                    var syncPropertiesButtons = new List<(string SyncPropertyName, Button Button)>
                    {
                        ("SyncNumLockOnColor", buttonNumLockOn),
                        ("SyncNumLockOffColor", buttonNumLockOff),
                        ("SyncCapsLockOnColor", buttonCapsLockOn),
                        ("SyncCapsLockOffColor", buttonCapsLockOff),
                        ("SyncScrollLockOnColor", buttonScrollLockOn),
                        ("SyncScrollLockOffColor", buttonScrollLockOff)
                    };

                    foreach (var (syncPropertyName, button) in syncPropertiesButtons) // Iterate through the list of tuples for each button
                    {
                        if (button == null)
                            continue;

                        // Use reflection to get the value of the sync property
                        var syncPropertyInfo = typeof(ColorSettings).GetProperty(syncPropertyName);
                        if (syncPropertyInfo == null)
                            continue;

                        bool isSynced = (bool)(syncPropertyInfo.GetValue(ViewModel?.ColorSettings) ?? false);
                        if (isSynced)
                        {
                            button.Background = new SolidColorBrush(args.NewColor); // Update the button color to reflect the new color
                            // Update the color of the icon / glyph based on the brightness of the new background color
                            FontIcon? fontIcon = GetButtonGlyphObject(button);
                            if (fontIcon != null)
                                fontIcon.Foreground = DetermineGlyphColor(button);
                        }
                    }
                }
            };

            // Create a button in the flyout to sync the color setting to the default/standard color
            var syncButton = new Button
            {
                Content = "Sync to default color",
                Margin = new Thickness(0, 10, 0, 0)
            };

            if (button == null)
                return;

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
