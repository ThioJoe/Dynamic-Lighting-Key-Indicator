using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;
using static Dynamic_Lighting_Key_Indicator.MainWindow;

// BEWARE - THIS FILE IS A COMPLETE MESS. Properties and methods aren't really organized. 
// But it has a bunch of important stuff to connect the GUI to the backend.

namespace Dynamic_Lighting_Key_Indicator
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherQueue _dispatcherQueue;

        private readonly MainWindow mainWindow;
        private static MainViewModel? mainViewModelInstance;
        public Dictionary<VKey, KeyIndicatorGUI> KeyStates { get; } = [];


        public MainViewModel(MainWindow mainWindowPassIn, bool debugMode)
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // Set default values
            _startupSettingCanBeChanged = true;
            _startupSettingReason = "";
            _isStartupEnabled = false;
            _deviceStatusMessage = new DeviceStatusInfo(DeviceStatusInfo.Msg.Empty);
            _attachedDevicesMessage = "";
            _deviceWatcherStatusMessage = "";
            mainWindow = mainWindowPassIn;

            InitializeStartupTaskStateAsync();

            Debug.WriteLine("MainViewModel created.");
            this._showAdvancedInfo = debugMode;

            foreach (ToggleAbleKeys key in Enum.GetValues<ToggleAbleKeys>())
            {
                // Pass the MainViewModel's DispatcherQueue
                KeyStates.Add((VKey)key, new KeyIndicatorGUI(this._dispatcherQueue));
            }

            AvailableDevices.CollectionChanged += AvailableDevices_CollectionChanged;
        }

        public static void SetMainViewModelInstance(MainViewModel vieModelInstance)
        {
            mainViewModelInstance = vieModelInstance;
        }

        private void UpdateSyncedColorsToDefault()
        {
            foreach (var kvp in KeyStates)
            {
                if (kvp.Value.SyncOnColor && kvp.Value.OnColor != DefaultColor)
                {
                    kvp.Value.OnColor = DefaultColor; // This will trigger OnPropertyChanged for OnColor and OnBrush internally
                }
                if (kvp.Value.SyncOffColor && kvp.Value.OffColor != DefaultColor)
                {
                    kvp.Value.OffColor = DefaultColor; // This will trigger OnPropertyChanged for OffColor and OffBrush internally
                }
            }
            // Update the backend/main color if necessary
            ColorSetter.DefineKeyboardMainColor(DefaultColor);
        }

        public void UpdateKeySyncSetting(VKey key, bool isOnState, bool syncSetting)
        {
            if (KeyStates.TryGetValue(key, out var state))
            {
                if (isOnState)
                {
                    state.SyncOnColor = syncSetting; // Triggers internal OnPropertyChanged for SyncOnColor & OnGlyph
                    if (syncSetting) state.OnColor = DefaultColor; // Sync immediately if turned on
                }
                else
                {
                    state.SyncOffColor = syncSetting; // Triggers internal OnPropertyChanged for SyncOffColor & OffGlyph
                    if (syncSetting) state.OffColor = DefaultColor; // Sync immediately if turned on
                }
                CheckAndUpdateSaveButton_EnabledStatus(); // Sync setting change might affect save state
                //mainWindow.ForceUpdateSpecificButtonGlyph(key, isOnState); // Might need helper in MainWindow
            }
        }

        public bool GetKeySyncSetting(VKey key, bool isOnState)
        {
            if (KeyStates.TryGetValue(key, out var state))
            {
                return isOnState ? state.SyncOnColor : state.SyncOffColor;
            }
            return false; // Default or throw exception
        }

        public bool GetSyncSetting_ByButtonObject(Button button)
        {
            VKey key = ButtonParameters.GetKeyName(button);
            StateColorApply state = ButtonParameters.GetColorState(button);
            return state switch
            {
                StateColorApply.On => GetKeySyncSetting(key, true),
                StateColorApply.Off => GetKeySyncSetting(key, false),
                _ => false,
            };
        }

        // Update the border thickness based on the actual key state
        public void UpdateLastKnownKeyState(VKey key, bool state)
        {
            if (KeyStates.TryGetValue((VKey)key, out KeyIndicatorGUI? keyState))
            {
                keyState.LastKnownState = state; // Setter handles border thickness updates and notifications
            }
        }

        public void UpdateAllToggleKeyStates()
        {
            UpdateLastKnownKeyState(VKey.NumLock, KeyStatesHandler.FetchKeyState((int)VKey.NumLock));
            UpdateLastKnownKeyState(VKey.CapsLock, KeyStatesHandler.FetchKeyState((int)VKey.CapsLock));
            UpdateLastKnownKeyState(VKey.ScrollLock, KeyStatesHandler.FetchKeyState((int)VKey.ScrollLock));
        }

        // Static update method - REMAP VK to ToggleAbleKeys
        public static void StaticUpdateLastKnownKeyState(MonitoredKey key)
        {
            if (mainViewModelInstance != null)
            {
                mainViewModelInstance.UpdateLastKnownKeyState(key.key, key.IsOn());
            }
        }

        public string GetSyncGlyph_ByButtonObject(Button button)
        {
            // This might need adjusting based on how you map button names/objects back to ToggleAbleKeys and On/Off state
            return GetSyncSetting_ByButtonObject(button) ? KeyIndicatorGUI.LinkedGlyph : KeyIndicatorGUI.UnlinkedGlyph;
        }

        internal void ApplyAppSettingsFromUserConfig(UserConfig userConfig) { /* ... same logic ... */ }

        public static SolidColorBrush GetBrushFromColor(Color color) => new SolidColorBrush(color); // Keep static helper

        private Color _defaultColor;

        public Color DefaultColor
        {
            get => _defaultColor;
            set
            {
                if (SetProperty(ref _defaultColor, value))
                {
                    OnPropertyChanged(nameof(DefaultColorBrush));
                }
            }
        }

        public SolidColorBrush DefaultColorBrush => GetBrushFromColor(DefaultColor);


        internal void SetAllColorSettingsFromUserConfig(UserConfig config, MainWindow window)
        {
            if (config == null || config.MonitoredKeysAndColors == null)
            {
                throw new ArgumentNullException(nameof(config), "UserConfig cannot be null.");
            }

            // Use BeginUpdate/EndUpdate pattern if available and beneficial
            // bool initialSaveButtonState = IsSaveButtonEnabled; // Capture initial state if needed

            Brightness = config.Brightness; // Use property setter
            DefaultColor = Color.FromArgb(255, (byte)config.StandardKeyColor.R, (byte)config.StandardKeyColor.G, (byte)config.StandardKeyColor.B); // Use property setter

            foreach (MonitoredKey monitoredKey in config.MonitoredKeysAndColors)
            {
                if (KeyStates.TryGetValue((VKey)monitoredKey.key, out var state))
                {
                    bool onColorTiedToStandard = monitoredKey.onColorTiedToStandard;
                    bool offColorTiedToStandard = monitoredKey.offColorTiedToStandard;

                    // Set sync states first
                    state.SyncOnColor = onColorTiedToStandard;
                    state.SyncOffColor = offColorTiedToStandard;

                    // Set colors - use DefaultColor if synced
                    state.OnColor = onColorTiedToStandard
                        ? DefaultColor
                        : Color.FromArgb(255, (byte)monitoredKey.onColor.R, (byte)monitoredKey.onColor.G, (byte)monitoredKey.onColor.B);

                    state.OffColor = offColorTiedToStandard
                        ? DefaultColor
                        : Color.FromArgb(255, (byte)monitoredKey.offColor.R, (byte)monitoredKey.offColor.G, (byte)monitoredKey.offColor.B);
                }
            }

            ColorSetter.DefineKeyboardMainColor(DefaultColor); // Keep this

            window.ForceUpdateButtonBackgrounds(); // Keep this
            window.ForceUpdateAllButtonGlyphs();   // Keep this

            // Crucially, after loading, the current state *is* the saved state
            IsSaveButtonEnabled = false; // Explicitly disable save button after load/undo
            OnPropertyChanged(nameof(IsUndoButtonEnabled)); // Update undo button too
        }


        // This method might be less necessary if bindings are correct, but can be kept for explicit updates
        public void UpdateAllColorSettingsFromGUI()
        {
            // Sync to defaults if set to do so.
            UpdateSyncedColorsToDefault(); // Refactored this logic

            // This seems redundant if DefaultColor property setter handles it
            // ColorSetter.DefineKeyboardMainColor(DefaultColor);

            // Check save button status after potential changes
            CheckAndUpdateSaveButton_EnabledStatus();
        }


        internal bool IsColorSettingsSameAsConfig(UserConfig config)
        {
            if (config == null || config.MonitoredKeysAndColors == null)
            {
                Debug.WriteLine("IsColorSettingsSameAsConfig: Config is null.");
                return false; // Or throw? Or treat as different? Assuming different.
            }

            // Compare Brightness
            if (Brightness != config.Brightness) return false;

            // Compare DefaultColor
            if (ColorsAreDifferent(DefaultColor, config.StandardKeyColor)) return false;

            // Compare settings for each key defined in the config
            foreach (MonitoredKey monitoredKey in config.MonitoredKeysAndColors)
            {
                if (KeyStates.TryGetValue((VKey)monitoredKey.key, out var currentState))
                {
                    // Compare Sync settings
                    if (currentState.SyncOnColor != monitoredKey.onColorTiedToStandard) return false;
                    if (currentState.SyncOffColor != monitoredKey.offColorTiedToStandard) return false;

                    // Compare Colors (only if NOT synced, otherwise they should match DefaultColor already checked)
                    if (!currentState.SyncOnColor && ColorsAreDifferent(currentState.OnColor, monitoredKey.onColor)) return false;
                    if (!currentState.SyncOffColor && ColorsAreDifferent(currentState.OffColor, monitoredKey.offColor)) return false;
                }
                else
                {
                    // A key exists in config but not in UI state - counts as different
                    Debug.WriteLine($"IsColorSettingsSameAsConfig: Key {monitoredKey} not found in KeyStates dictionary.");
                    return false;
                }
            }

            // Optional: Check if UI state has keys not in config (might indicate difference depending on requirements)
            // foreach (var uiKey in KeyStates.Keys) { ... check if exists in config.MonitoredKeysAndColors ... }


            // Everything matches
            return true;
        }

        // Keep local helper function
        private bool ColorsAreDifferent(Color color1, RGBTuple color2)
        {
            return color1.R != (byte)color2.R || color1.G != (byte)color2.G || color1.B != (byte)color2.B;
        }


        internal void CheckAndUpdateSaveButton_EnabledStatus()
        {
            bool shouldBeEnabled = !IsColorSettingsSameAsConfig(MainWindow.SavedConfig);
            // Only update property if the value changes to avoid unnecessary notifications/cycles
            if (_isSaveButtonEnabled != shouldBeEnabled)
            {
                IsSaveButtonEnabled = shouldBeEnabled; // Use the property setter
                OnPropertyChanged(nameof(IsUndoButtonEnabled)); // Undo is opposite of Save
            }
        }

        private bool _isStartupEnabled;
        public bool IsStartupEnabled
        {
            get => _isStartupEnabled;
            set
            {
                if (SetProperty(ref _isStartupEnabled, value))
                {
                    // Change the startup task state when the property changes
                    UpdateStartupTaskStateAsync(value);
                }
            }
        }

        public bool DebugFileLoggingEnabled
        {
            get
            {
                //return _DebugFileLoggingEnabled;
                //bool mode = Logging.DebugFileLoggingEnabled;
                //// If the mode is different from the property, update the property
                //if (mode != _DebugFileLoggingEnabled)
                //{
                //    _DebugFileLoggingEnabled = mode;
                //    OnPropertyChanged(nameof(DebugFileLoggingEnabled));
                //}
                //return mode;
                return Logging.DebugFileLoggingEnabled;
            }
            set
            {
                if (value == Logging.DebugFileLoggingEnabled)
                    return;

                if (value == true)
                {
                    Logging.EnableDebugLog();
                }
                else
                {
                    Logging.DisableDebugLog();
                }

                OnPropertyChanged(nameof(DebugFileLoggingEnabled));

                UserConfig.StandaloneSettings setting = UserConfig.StandaloneSettings.DebugLoggingEnabled;
                _ = UserConfig.UpdateConfigFile_SpecificSetting_Async(
                    setting: setting,
                    configSavedOnDisk: MainWindow.SavedConfig,
                    currentConfig: MainWindow.CurrentConfig,
                    value: value
                );
            }
        }

        // Whether to show a special message if the startup setting is set by policy, or was manually disabled by the user and therefore can't be toggled.
        // Also whether to disable the toggle switch if it won't work because of the above reasons.
        private bool _startupSettingCanBeChanged;
        public bool StartupSettingCanBeChanged
        {
            get => _startupSettingCanBeChanged;
            set
            {
                if (SetProperty(ref _startupSettingCanBeChanged, value))
                {
                    OnPropertyChanged(nameof(StartupSettingCanBeChanged));
                    OnPropertyChanged(nameof(StartupSettingCanBeChanged_VisibilityBool));
                }
            }
        }

        private string _startupSettingReason;
        public string StartupSettingReason
        {
            get => _startupSettingReason;
            set => SetProperty(ref _startupSettingReason, value);
        }

        private void UpdateStartupTaskStateAsync(bool newDesiredStateBool)
        {
            StartupTaskState updatedState;

            // Get the original state of the startup task
            StartupTaskState originalState = MainWindow.GetStartupTaskState_Async();

            // Simple bool to represent the original state
            bool originalStateBool = (originalState == StartupTaskState.Enabled || originalState == StartupTaskState.EnabledByPolicy);

            // If the the new desired state matches the original state, just make sure the toggle is set to the correct value
            if (MainWindow.MatchesStartupState(newDesiredStateBool) == true)
            {
                updatedState = originalState;

                // Only update the property if it doesn't already match the new desired state
                if (IsStartupEnabled != newDesiredStateBool)
                {
                    _isStartupEnabled = newDesiredStateBool;
                    OnPropertyChanged(nameof(IsStartupEnabled));
                }
            }
            // Otherwise update the state of the startup task
            else
            {
                updatedState = MainWindow.ChangWindowsStartupState(newDesiredStateBool);

                // Check again if the new desired state matches the updated state. If it does, update the property
                if (MainWindow.MatchesStartupState(newDesiredStateBool) == true)
                {
                    _isStartupEnabled = newDesiredStateBool;
                    OnPropertyChanged(nameof(IsStartupEnabled));
                }
                // If it failed to update, ensure the property is set to the correct value if not already
                else
                {
                    if (IsStartupEnabled != originalStateBool)
                    {
                        _isStartupEnabled = originalStateBool;
                        OnPropertyChanged(nameof(IsStartupEnabled));
                    }
                }
            }

            List<StartupTaskState> statesNotAbleToChange = [StartupTaskState.EnabledByPolicy, StartupTaskState.DisabledByPolicy, StartupTaskState.DisabledByUser];

            if (statesNotAbleToChange.Contains(updatedState))
            {
                _startupSettingCanBeChanged = false;
                _startupSettingReason = GetReason(updatedState);
            }
            else
            {
                _startupSettingCanBeChanged = true;
                _startupSettingReason = "";
            }

            // Update the startup setting can be changed property if necessary
            if (StartupSettingCanBeChanged != _startupSettingCanBeChanged)
            {
                OnPropertyChanged(nameof(StartupSettingCanBeChanged));
                OnPropertyChanged(nameof(StartupSettingReason));
            }
        }

        public static string GetReason(StartupTaskState startupTaskState)
        {
            return startupTaskState switch
            {
                StartupTaskState.EnabledByPolicy => "Warning: This setting (Startup with Windows enabled) is currently managed by group policy and can't be changed.",
                StartupTaskState.DisabledByPolicy => "Warning: This setting (Startup with Windows disabled) is currently managed by group policy and can't be changed.",
                StartupTaskState.DisabledByUser => "Warning: Startup setting has been manually disabled elsewhere like the Task Manager or Startup settings, and therefore cannot be enabled from within the app, you must manually enable it again.",
                _ => "", // Empty string (no message) if the state is not one of the above, meaning the state can be changed
            };
        }

        public Visibility StartupSettingCanBeChanged_VisibilityBool
        {
            get
            {
                // If the startup setting can't be changed, show the warning message, so set to visible
                if (!StartupSettingCanBeChanged)
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }
        }

        private bool _showAdvancedInfo;
        public bool ShowAdvancedInfo
        {
            get => _showAdvancedInfo;
            set
            {
                SetProperty(ref _showAdvancedInfo, value);
                OnPropertyChanged(nameof(AdvancedInfo_VisibilityBool));

            }
        }
        public Visibility AdvancedInfo_VisibilityBool
        {
            get
            {
                if (ShowAdvancedInfo)
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }
        }
        // For the special debug mode testing toggle switch, which always shows in debug mode but allows disabling regular debug controls
        public Visibility DebugMode_VisibilityBool
        {
            get
            {
                if (MainWindow.DEBUGMODE)
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }
        }

        public void InitializeStartupTaskStateAsync()
        {
            var startupState = MainWindow.GetStartupTaskState_Async();
            IsStartupEnabled = (startupState == StartupTaskState.Enabled || startupState == StartupTaskState.EnabledByPolicy);
            StartupSettingCanBeChanged = (startupState != StartupTaskState.EnabledByPolicy && startupState != StartupTaskState.DisabledByPolicy && startupState != StartupTaskState.DisabledByUser);
            StartupSettingReason = GetReason(startupState);
        }

        private DeviceStatusInfo _deviceStatusMessage;
        public DeviceStatusInfo DeviceStatusMessage
        {
            get => _deviceStatusMessage;
            set
            {
                SetProperty(ref _deviceStatusMessage, value);
                OnPropertyChanged(nameof(InstructionHeader));
                OnPropertyChanged(nameof(AttachedDeviceName));
                OnPropertyChanged(nameof(DeviceIsAvailable));
                OnPropertyChanged(nameof(DeviceIsConnected));
                OnPropertyChanged(nameof(DeviceIsEnabled));
                Logging.WriteDebug("Device Status Changed To: " + value.MsgBody.ToString());
            }
        }

        public string InstructionHeader
        {
            get
            {
                // Show a warning symbol if the device needs to be set up
                if (DeviceStatusMessage.MsgBody == DeviceStatusInfo.StatusMsgBody.NotAvailable)
                {
                    return "⚠️ Instructions (Action Required)";
                }
                else
                {
                    return "Instructions";
                }
            }
        }

        private string _attachedDevicesMessage;
        public string AttachedDevicesMessage
        {
            get => _attachedDevicesMessage;
            set => SetProperty(ref _attachedDevicesMessage, value);
        }

        public double WatcherButtonEnable_GlyphOpacity => IsWatcherRunning ? 0.3 : 1.0;
        public double WatcherButtonDisable_GlyphOpacity => IsWatcherRunning ? 1.0 : 0.3;

        public void CheckIfApplyButtonShouldBeEnabled()
        {
            OnPropertyChanged(nameof(ShouldApplyButtonBeEnabled));
        }

        private bool _shouldApplyButtonBeEnabled_Previous = false;

        public bool ShouldApplyButtonBeEnabled
        {
            get
            {
                // If the watcher isn't running then always return false
                if (!IsWatcherRunning)
                    return false;

                // If the dropdown isn't currently selecting a device, return false
                if (mainWindow.DropdownDevices.SelectedIndex == -1)
                    return false;

                // If there aren't even any attached devices, return true, since we can attach to any device
                if (AttachedDevice == null)
                    return true;

                bool? attachMatch = mainWindow.AttachedDeviceMatchesDropdownSelection();

                // If there are attached devices, only enable the apply button if the selected device is different from the attached device
                bool returnValue;
                if (attachMatch == null)
                {
                    returnValue = false;
                }
                else
                {
                    returnValue = (bool)!attachMatch;
                }

                return returnValue;
            }
        }

        private string _deviceWatcherStatusMessage;
        public string DeviceWatcherStatusMessage
        {
            get => _deviceWatcherStatusMessage;
            set => SetProperty(ref _deviceWatcherStatusMessage, value);
        }

        private bool _isWatcherRunning;
        public bool IsWatcherStopped => !IsWatcherRunning;
        public bool IsWatcherRunning
        {
            get => _isWatcherRunning;
            set
            {
                if (SetProperty(ref _isWatcherRunning, value))
                {
                    // Notify that IsWatcherStopped has also changed
                    OnPropertyChanged(nameof(IsWatcherStopped));
                    OnPropertyChanged(nameof(WatcherRunningVisibilityBool));
                    CheckIfApplyButtonShouldBeEnabled();
                    OnPropertyChanged(nameof(WatcherButtonEnable_GlyphOpacity));
                    OnPropertyChanged(nameof(WatcherButtonDisable_GlyphOpacity));
                }
            }
        }

        public Visibility WatcherRunningVisibilityBool
        {
            get
            {
                if (IsWatcherRunning)
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }
        }

        public int SelectedDeviceIndex => mainWindow.DropdownDevices.SelectedIndex;

        public ObservableCollection<DeviceInformation> AvailableDevices => mainWindow.availableDevices;
        // Event handler
        private void AvailableDevices_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(AvailableDevices));
            CheckIfApplyButtonShouldBeEnabled();
        }

        private bool _startMinimizedToTray;
        public bool StartMinimizedToTray
        {
            get => _startMinimizedToTray;
            set
            {
                SetProperty(ref _startMinimizedToTray, value);
                UserConfig.StandaloneSettings setting = UserConfig.StandaloneSettings.StartMinimizedToTray;

                _ = UserConfig.UpdateConfigFile_SpecificSetting_Async(
                    setting: setting,
                    configSavedOnDisk: MainWindow.SavedConfig,
                    currentConfig: MainWindow.CurrentConfig,
                    value: value
                );
            }
        }

        private bool _isSaveButtonEnabled;
        public bool IsSaveButtonEnabled
        {
            get
            {
                return _isSaveButtonEnabled;
            }
            set
            {
                SetProperty(ref _isSaveButtonEnabled, value);
            }
        }

        public bool IsUndoButtonEnabled => !IsSaveButtonEnabled;

        // Info about the attached device
        internal LampArrayInfo? AttachedDeviceInfo => mainWindow.AttachedDevice;
        internal LampArray? AttachedDevice => mainWindow.AttachedDevice?.lampArray;
        internal string AttachedDeviceName => AttachedDeviceInfo?.displayName ?? "None";
        internal bool DeviceIsAvailable => AttachedDevice?.IsAvailable ?? false;
        internal bool DeviceIsConnected => AttachedDevice?.IsConnected ?? false;
        internal bool DeviceIsEnabled => AttachedDevice?.IsEnabled ?? false;

        // If the device is attached but not available, show a message to the user to set it up
        public Visibility DeviceNeedsToBeSetup_VisibilityBool
        {
            get
            {
                if (AttachedDevice != null && DeviceIsAvailable == false)
                {
                    return Visibility.Visible;
                }
                else
                {
                    return Visibility.Collapsed;
                }
            }
        }

        public void UpdateAttachedDeviceStatus()
        {
            OnPropertyChanged(nameof(AttachedDeviceName));
            OnPropertyChanged(nameof(DeviceIsAvailable));
            OnPropertyChanged(nameof(DeviceIsConnected));
            OnPropertyChanged(nameof(DeviceIsEnabled));
        }

        // ----------------------- Event Handlers -----------------------

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (_dispatcherQueue.HasThreadAccess)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                _ = _dispatcherQueue.TryEnqueue(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
            }
        }

        protected static void OnPropertyChanged_Static(MainViewModel viewModel, [CallerMemberName] string? propertyName = null)
        {
            viewModel.OnPropertyChanged(propertyName);
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // ------------------------------------- Color Values From GUI -------------------------------------

        private int _brightness;
        public int Brightness
        {
            get => _brightness;
            set
            {
                SetProperty(ref _brightness, value);
                //ColorSetter.SetGlobalBrightness(value);
                CurrentConfig.UpdateGlobalBrightness(value);
            }
        }


        // ------ Other methods ------
        public void UpdateSyncSetting(VKey? key, StateColorApply state, bool syncSetting)
        {
            if (key is VKey keyTry && KeyStates.TryGetValue(keyTry, out var keyState))
            {
                if (state == StateColorApply.On)
                {
                    keyState.SyncOnColor = syncSetting;
                }
                else
                {
                    keyState.SyncOffColor = syncSetting;
                }
            }
        }

        //--------------------------------------------------------------

        public const string LinkedGlyph = "\uE71B";     // Chain link glyph
        public const string UnlinkedGlyph = "";         // No glyph if unlinked

        public KeyIndicatorGUI? GetKeyIndicatorStateByKey(VKey key)
        {
            if (KeyStates.TryGetValue(key, out var state))
            {
                return state;
            }
            else
            {
                Debug.WriteLine($"Key {key} not found in KeyStates dictionary.");
                return null; // Or throw an exception, or handle as needed
            }
        }

        public KeyIndicatorGUI ScrollLockState => KeyStates[VKey.ScrollLock];
        public KeyIndicatorGUI CapsLockState => KeyStates[VKey.CapsLock];
        public KeyIndicatorGUI NumLockState => KeyStates[VKey.NumLock];
        public KeyIndicatorGUI PlaybackState => KeyStates[VKey.PlayPause];


    } // ----------------------- End of MainViewModel -----------------------

}