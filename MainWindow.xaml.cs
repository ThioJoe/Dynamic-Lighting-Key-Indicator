using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;
using Windows.Graphics;
using Windows.System;

#nullable enable
#pragma warning disable IDE0079 // Remove unnecessary suppression

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Dynamic_Lighting_Key_Indicator
{
    //using static Dynamic_Lighting_Key_Indicator.KeyStatesHandler;

    public sealed partial class MainWindow : Window
    {
        // Null forgiving because it will immediatley be set in the constructor
        public MainViewModel ViewModel { get; private init; } = null!;

        private static bool _DEBUGMODE = false;
        public static bool DEBUGMODE => _DEBUGMODE;

        static UserConfig currentConfig = new();
        static UserConfig configSavedOnDisk = new();

        // Related to known / currently attached LampArrays
        public readonly ObservableCollection<DeviceInformation> availableDevices = [];
        private static LampArrayInfo? _attachedDevice = null;
        internal LampArrayInfo? AttachedDevice
        {
            get => _attachedDevice;
            private set
            {
                _attachedDevice = value;
                this.OnAttachedDeviceSet();
            }
        }

        private DeviceWatcher? m_deviceWatcher;
        private readonly System.Threading.Lock _lock = new();

        // Constants
        public const string MainIconFileName = "Icon.ico";
        public const string MainWindowTitle = "Dynamic Lighting Key Indicator";
        public const string StartupTaskId = "Dynamic-Lighting-Key-Indicator-StartupTask";
        public const string UpdatesURL = @"https://github.com/ThioJoe/Dynamic-Lighting-Key-Indicator/releases";

        // Other random crap
        public SolidColorBrush DefaultFontColor => GlobalDefinitions.DefaultFontColor; // Can't be static or else xaml binding won't work for some dumb reason
        bool alreadyShowedDebugLogMessage = false;
        public ComboBox DropdownDevices { get => dropdownDevices; }

        // Imported Windows API functions
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, IntPtr lParam);

        // --------------------------------------------------- CONSTRUCTOR ---------------------------------------------------
        public MainWindow(string[] args)
        {
            _DEBUGMODE = false; // To get rid of the warning to make it readonly
            #if DEBUG
                _DEBUGMODE = true;
            #endif

            ViewModel = new MainViewModel(mainWindowPassIn: this, debugMode: false)
            {
                DeviceStatusMessage = new DeviceStatusInfo(DeviceStatusInfo.Msg.Waiting),
                DeviceWatcherStatusMessage = "DeviceWatcher Status: Not started (Click Enable)",
            };
            MainViewModel.SetMainViewModelInstance(ViewModel);

            InitializeComponent();

            // Register event handlers
            this.Activated += MainWindow_Activated;
            this.Title = MainWindowTitle;
            this.Closed += OnAppClose;
            Application.Current.UnhandledException += OnUnhandledException;
            dropdownDevices.SelectionChanged += DropdownDevices_SelectionChanged;

            Microsoft.Windows.AppLifecycle.AppInstance thisInstance = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent();
            thisInstance.Activated += MainInstance_Activated;

            // Initialize the system tray
            SystemTray systemTray = new SystemTray(this);
            systemTray.InitializeSystemTray();
            Debug.WriteLine("System tray initialized.");

            // Use WM_SETICON message to set the window title bar icon
            IntPtr hIcon;
            System.Drawing.Icon? icon = SystemTray.LoadIconFromResource("Dynamic_Lighting_Key_Indicator.Assets.Icon.ico");
            if (icon == null)
                hIcon = SystemTray.GetDefaultIconHandle();
            else
                hIcon = icon.Handle;

            SendMessage((IntPtr)this.AppWindow.Id.Value, 0x0080, 1, hIcon); // WM_SETICON = 0x0080, ICON_BIG = 1

            // Load the user config from file
            currentConfig = UserConfig.ReadConfigurationFile() ?? new UserConfig();
            configSavedOnDisk = (UserConfig)currentConfig.Clone();

            ViewModel.SetAllColorSettingsFromUserConfig(config: currentConfig, window: this);
            ViewModel.ApplyAppSettingsFromUserConfig(currentConfig);
            ViewModel.CheckAndUpdateSaveButton_EnabledStatus();
            ViewModel.DebugFileLoggingEnabled = currentConfig.DebugLoggingEnabled;
            ColorSetter.DefineKeyboardMainColor(currentConfig.StandardKeyColor);

            // Set up keyboard hook
            #pragma warning disable IDE0028
            MonitoredKey.DefineAllMonitoredKeysAndColors(keys: new List<MonitoredKey> {
                new(VK.NumLock,    onColor: currentConfig.GetVKOnColor(VK.NumLock),    offColor: currentConfig.GetVKOffColor(VK.NumLock)),
                new(VK.CapsLock,   onColor: currentConfig.GetVKOnColor(VK.CapsLock),   offColor: currentConfig.GetVKOffColor(VK.CapsLock)),
                new(VK.ScrollLock, onColor: currentConfig.GetVKOnColor(VK.ScrollLock), offColor: currentConfig.GetVKOffColor(VK.ScrollLock))
            });
            #pragma warning restore IDE0028 // Disable message to simplify collection initialization. I want to keep the clarity of what type 'keys' is

            ForceUpdateButtonBackgrounds(); // TODO: These might not be necessary they're also called from SetAllColorSettingsFromUserConfig
            ForceUpdateAllButtonGlyphs();   // TODO: These might not be necessary they're also called from SetAllColorSettingsFromUserConfig

            // If there's a device ID in the config, try to attach to it on startup, otherwise user will have to select a device
            if (!string.IsNullOrEmpty(currentConfig.DeviceId))
            {
                StartWatchingForLampArrays();
                // After this, the OnEnumerationCompleted event will try to attach to the saved device
            }

            URLHandler.ProvideWindow(this);

            // Set initial window size, and check if it should start minimized
            this.AppWindow.Resize(new SizeInt32(1200, 1225));
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
        internal static UserConfig CurrentConfig
        {
            get => currentConfig;
            set => currentConfig = value;
        }

        internal static UserConfig SavedConfig
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

        

        private void UpdateStatusMessage()
        {
            // Capture the state we need to evaluate outside the UI thread
            int deviceCount = availableDevices.Count;
            DeviceWatcherStatus? watcherStatus = m_deviceWatcher?.Status;

            DispatcherQueue.TryEnqueue(() =>
            {
                DeviceStatusInfo statusInfo;

                // Now create all DeviceStatusInfo objects on the UI thread
                if (deviceCount == 0)
                {
                    // This means the watcher is running and has not yet found any devices
                    if (m_deviceWatcher != null &&
                        (watcherStatus == DeviceWatcherStatus.Started ||
                         watcherStatus == DeviceWatcherStatus.EnumerationCompleted))
                    {
                        statusInfo = new(DeviceStatusInfo.Msg.NoneFound);
                    }
                    else
                    {
                        statusInfo = new(DeviceStatusInfo.Msg.Waiting);
                    }
                }
                // If there are devices available, but nothing attached yet, show the number of devices
                else if (AttachedDevice == null || AttachedDevice.lampArray == null)
                {
                    statusInfo = new(DeviceStatusInfo.Msg.Available, deviceCount: deviceCount);
                }
                // If the attached device is set but not connected
                else if (AttachedDevice?.lampArray?.IsConnected == false)
                {
                    statusInfo = new(DeviceStatusInfo.Msg.AttachedNotConnected);
                }
                // If the device is attached but is not available, show warning
                else if (AttachedDevice?.lampArray?.IsAvailable == false)
                {
                    statusInfo = new(DeviceStatusInfo.Msg.NotAvailable);
                }
                // If the device is attached but is not a keyboard, show warning
                else if (AttachedDevice?.lampArray?.LampArrayKind != LampArrayKind.Keyboard)
                {
                    statusInfo = new(DeviceStatusInfo.Msg.NotKeyboard);
                }
                // If the device is attached and available, show good status
                else
                {
                    statusInfo = new(DeviceStatusInfo.Msg.Good);
                }

                ViewModel.DeviceStatusMessage = statusInfo;
            });
        }

        private void UpdateAttachedLampArrayDisplayName()
        {
            string message = "";

            lock (_lock)
            {
                if (AttachedDevice != null && AttachedDevice.lampArray != null)
                {
                    lock (AttachedDevice)
                    {
                        message = $"Attached To: {AttachedDevice.displayName} ({AttachedDevice.lampArray.LampArrayKind}, {AttachedDevice.lampArray.LampCount} lights)";
                    }
                }
                else
                {
                    message = "Attached To: None";
                }

                // Update ViewModel on UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.AttachedDevicesMessage = message;
                });
            }
            UpdateStatusMessage();
            Logging.WriteDebug("Updated attached device display: " + message);
        }

        async Task<LampArrayInfo?> AttachSelectedDeviceFromDropdown_Async()
        {
            Logging.WriteDebug("Attempting to attach to selected device from dropdown.");

            // Get the index of the selection from the GUI dropdown
            int selectedDeviceIndex = dropdownDevices.SelectedIndex;

            if (selectedDeviceIndex == -1)
            {
                _ = ShowErrorMessage("Please select a device from the dropdown list.");
                Logging.WriteDebug($"No device selected from dropdown. Selected device index was: {selectedDeviceIndex}. Number of devices was {availableDevices.Count}");
                return null;
            }

            // Get the device ID corresponding to the device in availableDevices
            DeviceInformation? selectedDeviceObj = dropdownDevices.SelectedItem as DeviceInformation;

            if (selectedDeviceObj == null)
            {
                Logging.WriteDebug("Failed to find the selected device object from the dropdown list using device ID: " + selectedDeviceObj);
                Logging.WriteDebug("   > Number of devices in availableDevices was: " + availableDevices.Count);
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

            DeviceInformation? selectedDevice = dropdownDevices.SelectedItem as DeviceInformation;

            if (ColorSetter.CurrentDevice == null || availableDevices.Count == 0 || AttachedDevice == null || dropdownDevices.SelectedIndex == -1 || selectedDevice == null)
            {
                return null; // If there's no device attached, return null
            }

            if (selectedDevice.Id == AttachedDevice.id)
                return true;
            else
                return false;
        }

        // Updates the dropdown list to reflect the current device ID if it was selected programatically, like on startup
        private void UpdateSelectedDeviceDropdownToCurrentDevice()
        {
            // Get the index of the device that matches the current device ID
            int deviceIndex = availableDevices.ToList().FindIndex(device => device.Id == currentConfig.DeviceId);

            if (deviceIndex > availableDevices.Count)
            {
                Logging.WriteDebug("Device index was greater than the number of available devices. Setting to -1 (no selection).");
                deviceIndex = -1;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                dropdownDevices.SelectedIndex = deviceIndex;
                ViewModel.CheckIfApplyButtonShouldBeEnabled(); // Putting this within the dispatcher to ensure it runs after the dropdown selection is set
            });
        }

        public async Task ShowErrorMessage(string message)
        {
            if (this.Content.XamlRoot == null)
            {
                Logging.WriteDebug("XamlRoot was null when trying to show error message.");
                return;
            }

            ContentDialog errorDialog = new()
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            await errorDialog.ShowAsync();
        }

        public async Task ShowInfoMessage(string message)
        {
            if (this.Content.XamlRoot == null)
            {
                Logging.WriteDebug("XamlRoot was null when trying to show info message.");
                return;
            }

            ContentDialog infoDialog = new()
            {
                Title = "Info",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await infoDialog.ShowAsync();
        }

        // Currently this gets called during initial attachment too
        private static async void ApplyLightingToDevice_AndSaveIdToConfig(LampArrayInfo lampArrayInfo)
        {
            if (lampArrayInfo.lampArray == null)
            {
                Logging.WriteDebug("Failed to apply lighting to device, lampArray was passed into ApplyLightingToDevice_AndSaveIdToConfig as null.");
                return;
            }

            LampArray lampArray = lampArrayInfo.lampArray;

            // Update the current and saved config to reflect the new device ID
            currentConfig.DeviceId = lampArrayInfo.id;

            Logging.WriteDebug("Beginning to apply lighting to device and saving device ID to config: " + lampArrayInfo.id);

            if (configSavedOnDisk.DeviceId != currentConfig.DeviceId)
            {
                // Update the device ID in the config file
                UserConfig.StandaloneSettings setting = UserConfig.StandaloneSettings.DeviceId;
                await UserConfig.UpdateConfigFile_SpecificSetting_Async(
                    setting: setting,
                    configSavedOnDisk: configSavedOnDisk,
                    currentConfig: currentConfig,
                    value: lampArrayInfo.id
                );
            }
            else
            {
                Logging.WriteDebug("Device ID in config file already matches the current device ID. Skipping writing to file.");
            }

            Logging.WriteDebug("Setting colors on device...");
            ColorSetter.DefineCurrentDevice(lampArray);
            ColorSetter.SetGlobalBrightness(currentConfig.Brightness);
            ColorSetter.SetProperColorsEveryKey_ToKeyboard(lampArray);
        }

        // Forces the color buttons to update their backgrounds to reflect the current color settings. Normally they update by event, but this is needed for the initial load
        // This doesn't work when put in the viewmodel class for some reason
        internal void ForceUpdateButtonBackgrounds()
        {
            buttonNumLockOn.Background = new SolidColorBrush(ViewModel.NumLockOnColor);
            buttonNumLockOff.Background = new SolidColorBrush(ViewModel.NumLockOffColor);
            buttonCapsLockOn.Background = new SolidColorBrush(ViewModel.CapsLockOnColor);
            buttonCapsLockOff.Background = new SolidColorBrush(ViewModel.CapsLockOffColor);
            buttonScrollLockOn.Background = new SolidColorBrush(ViewModel.ScrollLockOnColor);
            buttonScrollLockOff.Background = new SolidColorBrush(ViewModel.ScrollLockOffColor);
            buttonDefaultColor.Background = new SolidColorBrush(ViewModel.DefaultColor);
        }

        // Determine whether to use white or black text based on the background color
        static SolidColorBrush DetermineGlyphColor(Button button)
        {
            var buttonBackgroundColorBrush = button.Background as SolidColorBrush;
            Color? bgColor = buttonBackgroundColorBrush?.Color;

            // If it's null just return the original color
            if (bgColor == null || bgColor?.R == null || bgColor?.G == null || bgColor?.R == null)
                return (SolidColorBrush)button.Background;

            double luminance = (0.299 * bgColor.Value.R + 0.587 * bgColor.Value.G + 0.114 * bgColor.Value.B) / 255;
            bool useWhite = luminance < 0.5;
            SolidColorBrush newGlyphColor = useWhite ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Black);

            return newGlyphColor;
        }

        internal void ForceUpdateAllButtonGlyphs()
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

        internal async void ApplyAndSaveColorSettings(bool saveFile, UserConfig? newConfig = null)
        {
            //ColorSettings colorSettings = ViewModel.ColorSettings;

            // Save the current color settings from the GUI to the ViewModel if no specific config is passed in
            if (newConfig == null)
            {
                ViewModel.UpdateAllColorSettingsFromGUI();
            }
            // Instead of using the GUI, use the passed in premade config
            else
            {
                ViewModel.SetAllColorSettingsFromUserConfig(config: newConfig, window: this);
            }

            // TODO: Add binding to new settings to link on/off colors to standard color
            List<MonitoredKey> monitoredKeysList = [
                new(VK.NumLock,    onColor: ViewModel.NumLockOnColor,    offColor: ViewModel.NumLockOffColor,      onColorTiedToStandard: ViewModel.SyncNumLockOnColor,    offColorTiedToStandard: ViewModel.SyncNumLockOffColor),
                new(VK.CapsLock,   onColor: ViewModel.CapsLockOnColor,   offColor: ViewModel.CapsLockOffColor,     onColorTiedToStandard: ViewModel.SyncCapsLockOnColor,   offColorTiedToStandard: ViewModel.SyncCapsLockOffColor),
                new(VK.ScrollLock, onColor: ViewModel.ScrollLockOnColor, offColor: ViewModel.ScrollLockOffColor,   onColorTiedToStandard: ViewModel.SyncScrollLockOnColor, offColorTiedToStandard: ViewModel.SyncScrollLockOffColor)
            ];

            RGBTuple defaultColor = (ViewModel.DefaultColor.R, ViewModel.DefaultColor.G, ViewModel.DefaultColor.B);

            MonitoredKey.DefineAllMonitoredKeysAndColors(monitoredKeysList);
            //KeyStatesHandler.UpdateMonitoredKeyColorSettings(monitoredKeysList);
            currentConfig = new UserConfig(defaultColor, monitoredKeysList, ViewModel.Brightness);

            // If there was a device attached, update the colors
            if (ColorSetter.CurrentDevice != null)
            {
                ColorSetter.SetProperColorsEveryKey_ToKeyboard(ColorSetter.CurrentDevice);
                currentConfig.DeviceId = ColorSetter.CurrentDevice.DeviceId;
            }

            ForceUpdateAllButtonGlyphs(); // TODO: Might not be necessary they're also called from SetAllColorSettingsFromUserConfig, but might need to add to UpdateAllColorSettingsFromGUI

            // Ensures the minimized tray setting is updated even though the toggle should auto update from being bound. TODO: Maybe remove this, check if it's needed
            currentConfig.StartMinimizedToTray = ViewModel.StartMinimizedToTray;

            if (saveFile)
            {
                configSavedOnDisk = (UserConfig)currentConfig.Clone(); // Save the current config to the saved config, then save the current config to the file
                bool result = await currentConfig.WriteConfigurationFile_Async();
                if (!result)
                {
                    _ = ShowErrorMessage("Failed to save the color settings to the configuration file.");

                    Logging.WriteDebug("Failed to save the color settings to the configuration file.");
                }
                ViewModel.SetAllColorSettingsFromUserConfig(config: currentConfig, window: this);
            }

            // Update the Save button enabled status
            ViewModel.CheckAndUpdateSaveButton_EnabledStatus();
        }

        // This is called when a specific key color is changed via GUI. It inherently will not update the standard keys, since it is unlinked from the standard color when changed
        internal void ApplySpecificMonitoredKeyColor(KeyColorUpdateInfo colorUpdateInfo)
        {
            ViewModel.UpdateAllColorSettingsFromGUI();

            ColorSetter.SetSpecificKeysColor_ToKeyboard(colorUpdateInfo);
        }

        // This is called when the standard / default key color is changed via GUI.
        // Updates both standard keys and specified additional keys, specifically which ones are set to currently display the default color, to avoid flickering
        internal void ApplyNewDefaultColor(RGBTuple color, List<VK> additionalKeys)
        {
            ViewModel.UpdateAllColorSettingsFromGUI();

            ColorSetter.SetColorToDefaultAndAdditionalIndices(colorTuple: color, additionalKeys: additionalKeys, lampArray: ColorSetter.CurrentDevice);

        }

        private void SetInitialKeyStates()
        {
            bool numLockState = KeyStatesHandler.FetchKeyState((int)VK.NumLock);
            bool capsLockState = KeyStatesHandler.FetchKeyState((int)VK.CapsLock);
            bool scrollLockState = KeyStatesHandler.FetchKeyState((int)VK.ScrollLock);

            ViewModel.UpdateAllKeyStates(numLockState, capsLockState, scrollLockState);
        }

        public static void OpenUpdatesWebsite()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = UpdatesURL,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception e)
            {
                Logging.WriteDebug("Failed to open the updates website: " + e.Message);
            }
        }
        public static BitmapImage LoadSystemDllIcon(string rawResourcePathAndID)
        {
            // The resource path will look like: C:\Windows\System32\DDORes.dll,-3001

            BitmapImage bitmapImage = new BitmapImage();

            // Split on the last comma to separate the path from the ID
            string[] pathParts = rawResourcePathAndID.Split(',');
            string resourcePath = "";

            // Get the last part, parse and ensure it's a valid number within expected range (0-65535, though may be negative)
            string idString = pathParts.Last();
            int id;

            bool parseSuccess = int.TryParse(idString, out id);
            if (parseSuccess)
            {
                // Join the path parts back together, excluding the last part
                resourcePath = string.Join("", pathParts.Take(pathParts.Length - 1));
            }
            else
            {
                return bitmapImage; // Return an empty image
            }

            System.Drawing.Icon? icon;
            try
            {
                icon = System.Drawing.Icon.ExtractIcon(filePath: resourcePath, id: id, smallIcon: false);
                if (icon == null)
                {
                    return bitmapImage; // Return an empty image
                }

                // Convert Icon to BitmapImage
                using (var bitmap = icon.ToBitmap())
                using (var memory = new MemoryStream())
                {
                    bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;
                    bitmapImage.SetSource(memory.AsRandomAccessStream());

                    return bitmapImage;
                }
            }
            catch (Exception e)
            {
                Logging.WriteDebug($"Failed to load icon: {rawResourcePathAndID}. Error: {e.Message}");
                return bitmapImage; // Return an empty image
            }

        }

        // --------------------------------------------------- CLASSES AND ENUMS ---------------------------------------------------
        internal class LampArrayInfo(string id, string displayName, LampArray? lampArray)
        {
            public readonly string id = id;
            public readonly string displayName = displayName;
            public readonly LampArray? lampArray = lampArray;
        }

        // Stores info about the color a key is being updated to, for which state (on/off/both), etc. For more precise control over color updates
        internal class KeyColorUpdateInfo(VK key, RGBTuple color, StateColorApply forState)
        {
            public readonly VK key = key;
            public readonly RGBTuple color = color;
            public readonly StateColorApply forState = forState;
        }

        // ----------------------------------- GENERAL EVENT HANDLERS -----------------------------------
        private void MainWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            //System.Diagnostics.Debug.WriteLine($"Window activation state: {args.WindowActivationState}");
        }

        // This will handle the redirected activation from the protocol (URL) activation of further instances
        private void MainInstance_Activated(object? sender, AppActivationArguments? args)
        {
            // Capture the relevant data immediately
            bool isProtocol = args?.Kind == ExtendedActivationKind.Protocol;
            Uri? protocolUri = null;

            if (isProtocol)
            {
                IProtocolActivatedEventArgs? protocolArgs = args?.Data as IProtocolActivatedEventArgs;

                if (protocolArgs?.Uri != null)
                {
                    protocolUri = protocolArgs.Uri;

                    // Now use the dispatcher with our captured data
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        // Final null check just in case of a race condition or something changing it
                        if (protocolUri != null)
                        {
                            URLHandler.ProcessUri(protocolUri);
                        }
                    });
                }
            }
        }

        public void OnAppClose(object sender, WindowEventArgs args)
        {
            // Save the current color settings to the config file
            KeyStatesHandler.CleanupInputWatcher();
        }

        public void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logging.WriteCrashLog(e.Exception);

            // Close the app
            this.Close();
        }

        private void OnAttachedDeviceSet()
        {
            UpdateAttachedLampArrayDisplayName();
            ViewModel.UpdateAttachedDeviceStatus();
        }

        // -------------------------------------- GUI EVENT HANDLERS --------------------------------------

        private void DropdownDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.CheckIfApplyButtonShouldBeEnabled();
        }

        private void ButtonStartWatch_Click(object sender, RoutedEventArgs e)
        {
            // Clear the current list of attached devices
            AttachedDevice = null;
            availableDevices.Clear();

            StartWatchingForLampArrays();
        }

        private void ButtonStopWatch_Click(object sender, RoutedEventArgs e)
        {
            AttachedDevice = null;
            availableDevices.Clear();
            StopWatchingForLampArrays();
        }

        private async void ButtonApply_Click(object sender, RoutedEventArgs e)
        {
            // If there was a device attached, remove it from our current device variable
            if (ColorSetter.CurrentDevice != null)
            {
                ColorSetter.DefineCurrentDevice(null);
            }

            // Reset the m_attachedLampArrays list. In the future might allow multiple devices
            AttachedDevice = null;

            LampArrayInfo? selectedLampArrayInfo = await AttachSelectedDeviceFromDropdown_Async();

            if (selectedLampArrayInfo != null)
            {
                ApplyLightingToDevice_AndSaveIdToConfig(selectedLampArrayInfo);
            }

            // Upon applying the settings, it should trigger event handlers in MainViewModel for HasAttachedDevices, attachedDevicesMessage, EnableApplyButton etc.
        }

        private async void ToggleDebugLogging_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.Content.XamlRoot == null)
                return;

            if (sender is ToggleSwitch toggleSwitch)
            {
                if (toggleSwitch.IsOn && !alreadyShowedDebugLogMessage)
                {
                    await ShowInfoMessage("Debug text file log will appear on the desktop.");
                    alreadyShowedDebugLogMessage = true;
                }
            }
        }

        private void SwapOnOffColor_Click(object sender, RoutedEventArgs e)
        {
            // Get the button object
            Button button = (Button)sender;
            string keyName = (string)button.Tag;

            switch (keyName)
            {
                case "NumLock":
                    // Store the current color settings, then swapp them
                    Color numLockOnColor = ViewModel.NumLockOnColor;
                    Color numLockOffColor = ViewModel.NumLockOffColor;
                    ViewModel.NumLockOnColor = numLockOffColor;
                    ViewModel.NumLockOffColor = numLockOnColor;

                    // Swap their sync status
                    bool syncNumLockOnColor = ViewModel.SyncNumLockOnColor;
                    bool syncNumLockOffColor = ViewModel.SyncNumLockOffColor;
                    ViewModel.SyncNumLockOnColor = syncNumLockOffColor;
                    ViewModel.SyncNumLockOffColor = syncNumLockOnColor;

                    break;

                case "CapsLock":
                    Color capsLockOnColor = ViewModel.CapsLockOnColor;
                    Color capsLockOffColor = ViewModel.CapsLockOffColor;
                    ViewModel.CapsLockOnColor = capsLockOffColor;
                    ViewModel.CapsLockOffColor = capsLockOnColor;

                    bool syncCapsLockOnColor = ViewModel.SyncCapsLockOnColor;
                    bool syncCapsLockOffColor = ViewModel.SyncCapsLockOffColor;
                    ViewModel.SyncCapsLockOnColor = syncCapsLockOffColor;
                    ViewModel.SyncCapsLockOffColor = syncCapsLockOnColor;

                    break;

                case "ScrollLock":
                    Color scrollLockOnColor = ViewModel.ScrollLockOnColor;
                    Color scrollLockOffColor = ViewModel.ScrollLockOffColor;
                    ViewModel.ScrollLockOnColor = scrollLockOffColor;
                    ViewModel.ScrollLockOffColor = scrollLockOnColor;

                    bool syncScrollLockOnColor = ViewModel.SyncScrollLockOnColor;
                    bool syncScrollLockOffColor = ViewModel.SyncScrollLockOffColor;
                    ViewModel.SyncScrollLockOnColor = syncScrollLockOffColor;
                    ViewModel.SyncScrollLockOffColor = syncScrollLockOnColor;

                    break;

                default:
                    break;
            }

            ApplyAndSaveColorSettings(saveFile: false, newConfig: null);

        }

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndSaveColorSettings(saveFile: false, newConfig: new UserConfig());
        }

        private void UndoChanges_Click(object sender, RoutedEventArgs e)
        {
            currentConfig = (UserConfig)configSavedOnDisk.Clone();
            ApplyAndSaveColorSettings(saveFile: false, newConfig: currentConfig);
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
            ApplyAndSaveColorSettings(saveFile: true, newConfig: null);
        }

        private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            UserConfig.OpenConfigFolder();
        }

        private void OnBrightnessSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (sender is not Slider)
                return;

            ApplyAndSaveColorSettings(saveFile: false, newConfig: null);
            //ColorSetter.ProperlySetProperColorsAllKeys_ToKeyboard();
        }

        // This is the button within the flyout  menu
        private void SyncToStandardColor_Click(string colorPropertyName, ColorPicker colorPicker, Button parentButton, Flyout colorPickerFlyout)
        {
            string defaultColorPropertyName = "DefaultColor";
            Color? defaultColor = (Color?)ViewModel?.GetType()?.GetProperty(defaultColorPropertyName)?.GetValue(ViewModel);
            if (defaultColor == null)
                return;

            colorPicker.Color = (Color)defaultColor;

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

        private void Expander_SizeChange(object sender, SizeChangedEventArgs e) // When expanded or collapsed
        {
            if (this.Visible == false)
                return;

            // Resize the window to fit the new size with the expanded or collapsed expander element
            if (this.Content is FrameworkElement windowRoot && windowRoot.XamlRoot != null)
            {
                Double scale = windowRoot.XamlRoot.RasterizationScale;
                int width = (int)Math.Ceiling(windowRoot.ActualWidth * scale);
                int windowHeightToSet = (int)Math.Ceiling(windowRoot.ActualHeight * scale);

                AppWindow.ResizeClient(new SizeInt32(width, windowHeightToSet));
            }
        }

        private bool testState = false;
        private void TestButton_Click(object sender, object e)
        {
            // Get the text block object from various controls
            Debug.WriteLine("Test button clicked.");

            if (AttachedDevice == null || AttachedDevice.lampArray == null)
            {
                Debug.WriteLine("Attached device or lamp array was null.");
                return;
            }

            Color[] colors1 = [Colors.Green];
            Color[] colors2 = [Colors.Yellow];
            VirtualKey[] keys = [VirtualKey.NumberKeyLock];

            LampArray lampArray = AttachedDevice.lampArray;

            Dynamic_Lighting_Key_Indicator.Extras.Tests.GetIndicesPurposesAndUnknownKeys(lampArray);

            if (testState)
            {
                //AttachedDevice?.lampArray?.SetColor(Colors.Green);
                //Dynamic_Lighting_Key_Indicator.Extras.Tests.SetAllKeyColorsAsList(AttachedDevice.lampArray, Colors.Green);
                //lampArray.SetColorsForKey(Colors.Green, Windows.System.VirtualKey.NumberKeyLock);
                //lampArray.SetColorForIndex(50, Colors.Green);
                //lampArray.SetColorsForKeys(colors1, keys);
                //Dynamic_Lighting_Key_Indicator.Extras.Tests.SetAllKeysColorsAsPairedList(AttachedDevice.lampArray, Colors.Green); // Works
                //Dynamic_Lighting_Key_Indicator.Extras.Tests.SetOneKeyOneColorRestOtherKeyAnotherColor(AttachedDevice.lampArray, VirtualKey.Scroll, Colors.Green, Colors.White); // No Work
                //Dynamic_Lighting_Key_Indicator.Extras.Tests.SetAllColorsUsingVK(lampArray, Colors.Green);
                Dynamic_Lighting_Key_Indicator.Extras.Tests.SetOneKeyOneColorRestOtherKeyAnotherColor_UsingVK(AttachedDevice.lampArray, VirtualKey.Scroll, Colors.Green, Colors.White);
            }
            else
            {
                //AttachedDevice?.lampArray?.SetColor(Colors.Yellow);
                //Dynamic_Lighting_Key_Indicator.Extras.Tests.SetAllKeyColorsAsList(AttachedDevice.lampArray, Colors.Yellow);
                //lampArray.SetColorsForKey(Colors.Yellow, Windows.System.VirtualKey.NumberKeyLock);
                //lampArray.SetColorForIndex(50, Colors.Yellow);
                //lampArray.SetColorsForKeys(colors2, keys);
                //Dynamic_Lighting_Key_Indicator.Extras.Tests.SetAllKeysColorsAsPairedList(AttachedDevice.lampArray, Colors.Yellow); // Works
                //Dynamic_Lighting_Key_Indicator.Extras.Tests.SetOneKeyOneColorRestOtherKeyAnotherColor(AttachedDevice.lampArray, VirtualKey.Scroll, Colors.Yellow, Colors.White); // No Work
                //Dynamic_Lighting_Key_Indicator.Extras.Tests.SetAllColorsUsingVK(lampArray, Colors.Yellow);
                Dynamic_Lighting_Key_Indicator.Extras.Tests.SetOneKeyOneColorRestOtherKeyAnotherColor_UsingVK(AttachedDevice.lampArray, VirtualKey.Scroll, Colors.Red, Colors.White);
            }

            testState = !testState;

        }


        private void AutoSizeWindow_OnGridLoad(object sender, RoutedEventArgs e)
        {
            AutoSizeWindow_UponLoaded();
            SetInitialKeyStates();
        }

        private void AutoSizeWindow_UponLoaded()
        {
            if (this.Visible == false)
                return;

            MainWindow window = this;  // assuming this is in the Window class

            if (window.Content is FrameworkElement windowRoot && windowRoot.XamlRoot != null)
            {
                Double scale = windowRoot.XamlRoot.RasterizationScale;
                int width = (int)Math.Ceiling(windowRoot.ActualWidth * scale);
                int height = (int)Math.Ceiling(windowRoot.ActualHeight * scale);
                AppWindow.ResizeClient(new SizeInt32(width, height));
            }
        }

        private void AutoSizeWindow_OnAdvancedToggle(object sender, RoutedEventArgs e)
        {
            var window = this;  // assuming this is in the Window class
            if (window.Content is FrameworkElement windowRoot && windowRoot.XamlRoot != null)
            {
                // Get info about AdvancedInfoStack text block
                StackPanel? advancedInfoStack = windowRoot.FindName("AdvancedInfoStack") as StackPanel;

                if (advancedInfoStack == null)
                    return;

                //Expander expander = (Expander)sender;
                Double scale = windowRoot.XamlRoot.RasterizationScale;
                int width = (int)Math.Ceiling(windowRoot.ActualWidth * scale);

                // Desired size is the size before the toggle.
                Double previousRootContentHeight = windowRoot.ActualHeight;

                int windowHeightToSet;
                int extraBuffer = 0;

                // "Desired size" is actually the size before the toggle. If it's not zero, we know it is collapsing now
                // However the actual height is not reliable because the first time it's expanded it will be also be zero
                bool isExpanding = advancedInfoStack.DesiredSize.Height == 0;

                // First time it's expanded it will be zero, so we'll need to use our own calculation as a workaround
                int assumedTextLineHeight = 24;
                int assumedInfoStackHeight = assumedTextLineHeight * advancedInfoStack.Children.Count;

                // 
                Double infoStackHeight;
                if (advancedInfoStack.DesiredSize.Height != 0)
                    infoStackHeight = advancedInfoStack.DesiredSize.Height;
                else
                    infoStackHeight = assumedInfoStackHeight;


                if (isExpanding) // Expanding
                {
                    windowHeightToSet = (int)Math.Ceiling((previousRootContentHeight + infoStackHeight + extraBuffer) * scale);
                }
                else // Collapsing
                {
                    windowHeightToSet = (int)Math.Ceiling((previousRootContentHeight - infoStackHeight + extraBuffer) * scale);
                }

                AppWindow.ResizeClient(new SizeInt32(width, windowHeightToSet));
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            List<VK> monitoredKeysToPreviewDefaultColor = [];
            // Add keys where their current state matches their linked default state
            foreach (MonitoredKey key in KeyStatesHandler.monitoredKeys)
            {
                //bool keystate = KeyStatesHandler.FetchKeyState((int)key.key);
                bool keystate = key.IsOn();
                if (key.onColorTiedToStandard && keystate)
                {
                    monitoredKeysToPreviewDefaultColor.Add(key.key);
                }
                else if (key.offColorTiedToStandard && !keystate)
                {
                    monitoredKeysToPreviewDefaultColor.Add(key.key);
                }
            }


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
            //SolidColorBrush? newBGColor = ViewModel.GetType().GetProperty(colorPropertyName)?.GetValue(ViewModel) as SolidColorBrush;
            //if (button != null && newBGColor != null)
            //    button.Background = newBGColor;

            // Next show the color picker flyout
            var flyout = new Flyout();
            // Add event handler for when the flyout is closed
            flyout.Closed += (s, args) => ApplyAndSaveColorSettings(saveFile: false, newConfig: null);

            var stackPanel = new StackPanel();
            var colorPicker = new ColorPicker
            {
                MinWidth = 300,
                MinHeight = 400
            };

            Color? currentColor = (Color?)ViewModel.GetType()?.GetProperty(colorPropertyName)?.GetValue(ViewModel);
            if (currentColor == null)
            {
                Debug.WriteLine("Current color is null.");
                return;
            }

            colorPicker.Color = (Color)currentColor;

            colorPicker.ColorChanged += (s, args) =>
            {
                if (button == null)
                    return;

                ViewModel.GetType()?.GetProperty(colorPropertyName)?.SetValue(ViewModel, args.NewColor); // Update the color setting in the ViewModel
                button.Background = new SolidColorBrush(args.NewColor); // Update the button color to reflect the new color

                // If updating the DefaultColor, check the ColorSettings for which keys are set to sync to default, and update their buttons too
                if (colorPropertyName == "DefaultColor")
                {
                    List<Button> buttonsList = [buttonNumLockOn, buttonNumLockOff, buttonCapsLockOn, buttonCapsLockOff, buttonScrollLockOn, buttonScrollLockOff];

                    foreach (Button button in buttonsList) // Iterate through the list of tuples for each button
                    {
                        if (button == null)
                            continue;

                        bool isSynced = ViewModel.GetSyncSetting_ByButtonObject(button);

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
                //ApplyAndSaveColorSettings(saveFile: false, newConfig: null); // Works to continuously update the colors, but monitored keys flicker, so it's disabled for now

                // Create the KeyColorUpdateInfo object to pass to the ApplySpecificMonitoredKeyColor method, depending on which color is being updated
                if (colorPropertyName == "DefaultColor")
                {
                    ApplyNewDefaultColor((args.NewColor.R, args.NewColor.G, args.NewColor.B), monitoredKeysToPreviewDefaultColor);
                }
                else
                {
                    KeyColorUpdateInfo colorUpdateInfo = new KeyColorUpdateInfo(
                        key: MainViewModel.GetKeyByPropertyName(colorPropertyName),
                        color: (args.NewColor.R, args.NewColor.G, args.NewColor.B),
                        forState: MainViewModel.GetStateByPropertyName(colorPropertyName)
                    );

                    ApplySpecificMonitoredKeyColor(colorUpdateInfo);
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
