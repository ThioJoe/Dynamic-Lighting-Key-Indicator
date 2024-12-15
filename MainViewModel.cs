using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;
using System;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using System.Diagnostics;

namespace Dynamic_Lighting_Key_Indicator
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherQueue _dispatcherQueue;

        public MainViewModel(MainWindow mainWindowPassIn, bool debugMode)
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // Set default values
            _scrollLockOffGlyph = UnlinkedGlyph;
            _capsLockOffGlyph = UnlinkedGlyph;
            _numLockOffGlyph = UnlinkedGlyph;
            _numLockOnGlyph = UnlinkedGlyph;
            _capsLockOnGlyph = UnlinkedGlyph;
            _scrollLockOnGlyph = UnlinkedGlyph;
            _capsLockOnGlyph = UnlinkedGlyph;
            _numLockOnGlyph = UnlinkedGlyph;
            _capsLockOnGlyph = UnlinkedGlyph;
            _scrollLockOnGlyph = UnlinkedGlyph;
            _capsLockOnGlyph = UnlinkedGlyph;
            _numLockOnGlyph = UnlinkedGlyph;
            _capsLockOnGlyph = UnlinkedGlyph;
            _startupSettingCanBeChanged = true;
            _startupSettingReason = "";
            _isStartupEnabled = false;
            _deviceStatusMessage = "";
            _attachedDevicesMessage = "";
            _deviceWatcherStatusMessage = "";
            mainWindow = mainWindowPassIn;

            InitializeStartupTaskStateAsync();

            Debug.WriteLine("MainViewModel created.");
            this.debugMode = debugMode;
        }

        private MainWindow mainWindow;
        

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

        private bool debugMode;
        public Visibility DebugMode_VisibilityBool
        {
            get
            {
                if (debugMode)
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

        private string _deviceStatusMessage;
        public string DeviceStatusMessage
        {
            get => _deviceStatusMessage;
            set => SetProperty(ref _deviceStatusMessage, value);
        }

        private string _attachedDevicesMessage;
        public string AttachedDevicesMessage
        {
            get => _attachedDevicesMessage;
            set => SetProperty(ref _attachedDevicesMessage, value);
        }

        private bool _hasAttachedDevices;
        public bool HasAttachedDevices
        {
            get => _hasAttachedDevices;
            set
            {
                SetProperty(ref _hasAttachedDevices, value);
                // Notify that EnableApplyButton has also changed when device attachment status changes
                OnPropertyChanged(nameof(EnableApplyButton));
            }
        }
        public bool EnableApplyButton
        {
            get
            {
                // If the watcher isn't running then always return false
                if (!IsWatcherRunning)
                    return false;

                // If the dropdown isn't currently selecting a device, return false
                if (SelectedDeviceIndex == -1)
                    return false;

                // If there aren't even any attached devices, return true, since we can attach to any device
                if (!HasAttachedDevices)
                    return true;

                bool? attachMatch = mainWindow.AttachedDeviceMatchesDropdownSelection();

                // If there are attached devices, only enable the apply button if the selected device is different from the attached device
                if (attachMatch == null)
                {
                    return false;
                }
                else
                {
                    return (bool)!attachMatch;
                }
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
                    OnPropertyChanged(nameof(EnableApplyButton));
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

        private int _selectedDeviceIndex;
        public int SelectedDeviceIndex
        {
            get => _selectedDeviceIndex;
            set
            {
                _selectedDeviceIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EnableApplyButton));
            }
        }

        private bool _startMinimizedToTray;
        public bool StartMinimizedToTray
        {
            get => _startMinimizedToTray;
            set
            {
                SetProperty(ref _startMinimizedToTray, value);
                mainWindow.CurrentConfig.StartMinimizedToTray = value;
                mainWindow.SavedConfig.StartMinimizedToTray = value;
                _ = mainWindow.SavedConfig.WriteConfigurationFile_Async();
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
        internal void CheckAndUpdateSaveButton_EnabledStatus()
        {
            // Enable it if the colors are not the same as the config
            bool newEnabledStatus = !IsColorSettingsSameAsConfig(config: mainWindow.SavedConfig);

            // Only update the property if it doesn't already match the new enabled status
            if (IsSaveButtonEnabled != newEnabledStatus)
            {
                IsSaveButtonEnabled = newEnabledStatus;
                OnPropertyChanged(nameof(IsSaveButtonEnabled));
            }
        }
        public bool IsUndoButtonEnabled => !IsSaveButtonEnabled;

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

            // If anything changes, check if the current configuration is different from the saved configuration, and if so, enable the save button
            // Don't check if it's from the save button itself to avoid infinite loop
            //if (propertyName != nameof(IsSaveButtonEnabled))
            //{
            //    CheckAndUpdateSaveButton();
            //}
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // ------------------------------------- Color Values From GUI -------------------------------------
        public string TextScrollLockOnColor
        {
            get => ScrollLockOnColorHex;
            set => ScrollLockOnColor = GetColorFromString(value);
        }

        public string TextScrollLockOffColor
        {
            get => ScrollLockOffColorHex;
            set => ScrollLockOffColor = GetColorFromString(value);
        }

        public string TextCapsLockOnColor
        {
            get => CapsLockOnColorHex;
            set => CapsLockOnColor = GetColorFromString(value);
        }

        public string TextCapsLockOffColor
        {
            get => CapsLockOffColorHex;
            set => CapsLockOffColor = GetColorFromString(value);
        }

        public string TextNumLockOnColor
        {
            get => NumLockOnColorHex;
            set => NumLockOnColor = GetColorFromString(value);
        }

        public string TextNumLockOffColor
        {
            get => NumLockOffColorHex;
            set => NumLockOffColor = GetColorFromString(value);
        }

        public string TextDefaultColor
        {
            get => DefaultColorHex;
            set => DefaultColor = GetColorFromString(value);
        }

        private bool _syncScrollLockOnColor;
        public bool SyncScrollLockOnColor
        {
            get => _syncScrollLockOnColor;
            set
            {
                if (SetProperty(ref _syncScrollLockOnColor, value))
                {
                    OnPropertyChanged(nameof(ScrollLockOnGlyph));
                }
            }
        }

        private bool _syncScrollLockOffColor;
        public bool SyncScrollLockOffColor
        {
            get => _syncScrollLockOffColor;
            set
            {
                if (SetProperty(ref _syncScrollLockOffColor, value))
                {
                    OnPropertyChanged(nameof(ScrollLockOffGlyph));
                }
            }
        }

        private bool _syncCapsLockOnColor;
        public bool SyncCapsLockOnColor
        {
            get => _syncCapsLockOnColor;
            set
            {
                if (SetProperty(ref _syncCapsLockOnColor, value))
                {
                    OnPropertyChanged(nameof(CapsLockOnGlyph));
                }
            }
        }

        private bool _syncCapsLockOffColor;
        public bool SyncCapsLockOffColor
        {
            get => _syncCapsLockOffColor;
            set
            {
                if (SetProperty(ref _syncCapsLockOffColor, value))
                {
                    OnPropertyChanged(nameof(CapsLockOffGlyph));
                }
            }
        }

        private bool _syncNumLockOnColor;
        public bool SyncNumLockOnColor
        {
            get => _syncNumLockOnColor;
            set
            {
                if (SetProperty(ref _syncNumLockOnColor, value))
                {
                    OnPropertyChanged(nameof(NumLockOnGlyph));
                }
            }
        }

        private bool _syncNumLockOffColor;
        public bool SyncNumLockOffColor
        {
            get => _syncNumLockOffColor;
            set
            {
                if (SetProperty(ref _syncNumLockOffColor, value))
                {
                    OnPropertyChanged(nameof(NumLockOffGlyph));
                }
            }
        }

        private string _scrollLockOnGlyph;
        public string ScrollLockOnGlyph
        {
            get => SyncScrollLockOnColor ? LinkedGlyph : UnlinkedGlyph;
            private set => SetProperty(ref _scrollLockOnGlyph, value);
        }

        private string _scrollLockOffGlyph;
        public string ScrollLockOffGlyph
        {
            get => SyncScrollLockOffColor ? LinkedGlyph : UnlinkedGlyph;
            private set => SetProperty(ref _scrollLockOffGlyph, value);
        }

        private string _capsLockOnGlyph;
        public string CapsLockOnGlyph
        {
            get => SyncCapsLockOnColor ? LinkedGlyph : UnlinkedGlyph;
            private set => SetProperty(ref _capsLockOnGlyph, value);
        }

        private string _capsLockOffGlyph;
        public string CapsLockOffGlyph
        {
            get => SyncCapsLockOffColor ? LinkedGlyph : UnlinkedGlyph;
            private set => SetProperty(ref _capsLockOffGlyph, value);
        }

        private string _numLockOnGlyph;
        public string NumLockOnGlyph
        {
            get => SyncNumLockOnColor ? LinkedGlyph : UnlinkedGlyph;
            private set => SetProperty(ref _numLockOnGlyph, value);
        }

        private string _numLockOffGlyph;
        public string NumLockOffGlyph
        {
            get => SyncNumLockOffColor ? LinkedGlyph : UnlinkedGlyph;
            private set => SetProperty(ref _numLockOffGlyph, value);
        }


        // Color properties

        public int Brightness { get; set; }
        public string ScrollLockOnColorHex => AsString(ScrollLockOnColor);
        public string ScrollLockOffColorHex => AsString(ScrollLockOffColor);
        public string CapsLockOnColorHex => AsString(CapsLockOnColor);
        public string CapsLockOffColorHex => AsString(CapsLockOffColor);
        public string NumLockOnColorHex => AsString(NumLockOnColor);
        public string NumLockOffColorHex => AsString(NumLockOffColor);
        public string DefaultColorHex => AsString(DefaultColor);

        private Windows.UI.Color _scrollLockOnColor;
        public Windows.UI.Color ScrollLockOnColor
        {
            get => _scrollLockOnColor;
            set
            {
                if (SetProperty(ref _scrollLockOnColor, value))
                {
                    OnPropertyChanged(nameof(ScrollLockOnColorHex));
                }
            }
        }

        private Windows.UI.Color _scrollLockOffColor;
        public Windows.UI.Color ScrollLockOffColor
        {
            get => _scrollLockOffColor;
            set
            {
                if (SetProperty(ref _scrollLockOffColor, value))
                {
                    OnPropertyChanged(nameof(ScrollLockOffColorHex));
                }
            }
        }
        public SolidColorBrush ScrollLockOffBrush => new SolidColorBrush(ScrollLockOffColor);

        private Windows.UI.Color _capsLockOnColor;
        public Windows.UI.Color CapsLockOnColor
        {
            get => _capsLockOnColor;
            set
            {
                if (SetProperty(ref _capsLockOnColor, value))
                {
                    OnPropertyChanged(nameof(CapsLockOnColorHex));
                }
            }
        }

        private Windows.UI.Color _capsLockOffColor;
        public Windows.UI.Color CapsLockOffColor
        {
            get => _capsLockOffColor;
            set
            {
                if (SetProperty(ref _capsLockOffColor, value))
                {
                    OnPropertyChanged(nameof(CapsLockOffColorHex));
                }
            }
        }

        private Windows.UI.Color _numLockOnColor;
        public Windows.UI.Color NumLockOnColor
        {
            get => _numLockOnColor;
            set
            {
                if (SetProperty(ref _numLockOnColor, value))
                {
                    OnPropertyChanged(nameof(NumLockOnColorHex));
                }
            }
        }

        private Windows.UI.Color _numLockOffColor;
        public Windows.UI.Color NumLockOffColor
        {
            get => _numLockOffColor;
            set
            {
                if (SetProperty(ref _numLockOffColor, value))
                {
                    OnPropertyChanged(nameof(NumLockOffColorHex));
                }
            }
        }

        private Windows.UI.Color _defaultColor;
        public Windows.UI.Color DefaultColor
        {
            get => _defaultColor;
            set
            {
                if (SetProperty(ref _defaultColor, value))
                {
                    OnPropertyChanged(nameof(DefaultColorHex));
                }
            }
        }




        // ------ Other methods ------
        public void UpdateSyncSetting(bool syncSetting, string colorPropertyName)
        {
            // Update the relevant sync setting in ViewModel based on the colorPropertyName
            switch (colorPropertyName)
            {
                case ColorPropName.NumLockOn:
                    SyncNumLockOnColor = syncSetting;
                    OnPropertyChanged(nameof(NumLockOnGlyph));
                    break;
                case ColorPropName.NumLockOff:
                    SyncNumLockOffColor = syncSetting;
                    OnPropertyChanged(nameof(NumLockOffGlyph));
                    break;
                case ColorPropName.CapsLockOn:
                    SyncCapsLockOnColor = syncSetting;
                    OnPropertyChanged(nameof(CapsLockOnGlyph));
                    break;
                case ColorPropName.CapsLockOff:
                    SyncCapsLockOffColor = syncSetting;
                    OnPropertyChanged(nameof(CapsLockOffGlyph));
                    break;
                case ColorPropName.ScrollLockOn:
                    SyncScrollLockOnColor = syncSetting;
                    OnPropertyChanged(nameof(ScrollLockOnGlyph));
                    break;
                case ColorPropName.ScrollLockOff:
                    SyncScrollLockOffColor = syncSetting;
                    OnPropertyChanged(nameof(ScrollLockOffGlyph));
                    break;
                default:
                    break;
            }
        }

        public bool GetSyncSetting_ByPropertyName(string colorPropertyName)
        {
            return colorPropertyName switch
            {
                ColorPropName.NumLockOn => SyncNumLockOnColor,
                ColorPropName.NumLockOff => SyncNumLockOffColor,
                ColorPropName.CapsLockOn  => SyncCapsLockOnColor,
                ColorPropName.CapsLockOff => SyncCapsLockOffColor,
                ColorPropName.ScrollLockOn  => SyncScrollLockOnColor,
                ColorPropName.ScrollLockOff => SyncScrollLockOffColor,
                _ => false,
            };
        }

        public bool GetSyncSetting_ByButtonName(string buttonName)
        {
            return buttonName switch
            {
                "buttonNumLockOn" => SyncNumLockOnColor,
                "buttonNumLockOff" => SyncNumLockOffColor,
                "buttonCapsLockOn" => SyncCapsLockOnColor,
                "buttonCapsLockOff" => SyncCapsLockOffColor,
                "buttonScrollLockOn" => SyncScrollLockOnColor,
                "buttonScrollLockOff" => SyncScrollLockOffColor,
                _ => false,
            };
        }

        public bool GetSyncSetting_ByButtonObject(Button button)
        {
            return GetSyncSetting_ByButtonName(button.Name);
        }

        //--------------------------------------------------------------

        public const string LinkedGlyph = "\uE71B";     // Chain link glyph
        public const string UnlinkedGlyph = "";         // No glyph if unlinked

        //public string GetSyncGlyph_ByPropertyName(string colorPropertyName)
        //{
        //    var glyph = GetSyncSetting_ByPropertyName(colorPropertyName) ? LinkedGlyph : UnlinkedGlyph;
        //    System.Diagnostics.Debug.WriteLine($"GetSyncGlyph_ByPropertyName({colorPropertyName}): {glyph}");
        //    return glyph;
        //}

        public string GetSyncGlyph_ByButtonObject(Button button)
        {
            return GetSyncSetting_ByButtonObject(button) ? LinkedGlyph : UnlinkedGlyph;
        }

        internal void ApplyAppSettingsFromUserConfig(UserConfig userConfig)
        {
            StartMinimizedToTray = userConfig.StartMinimizedToTray;
        }

        public Windows.UI.Color GetColorFromString(string color)
        {
            if (string.IsNullOrEmpty(color))
            {
                // If the color is null or empty, return the default color
                return DefaultColor;
            }

            if (color.StartsWith('#'))
            {
                color = color[1..]; // Remove the hash symbol / first character
            }
            byte r = Convert.ToByte(color[..2], 16);
            byte g = Convert.ToByte(color.Substring(startIndex: 2, length: 2), 16);
            byte b = Convert.ToByte(color.Substring(startIndex: 4, length: 2), 16);
            return Windows.UI.Color.FromArgb(a: 255, r, g, b);
        }

        public static string AsString(Windows.UI.Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        internal void SetAllColorSettingsFromUserConfig(UserConfig config, MainWindow window)
        {
            if (config == null || config.MonitoredKeysAndColors == null)
            {
                throw new ArgumentNullException(nameof(config), "UserConfig cannot be null.");
            }

            Brightness = config.Brightness;
            DefaultColor = Windows.UI.Color.FromArgb(255, (byte)config.StandardKeyColor.R, (byte)config.StandardKeyColor.G, (byte)config.StandardKeyColor.B);

            foreach (KeyStatesHandler.MonitoredKey monitoredKey in config.MonitoredKeysAndColors)
            {
                Windows.UI.Color onColor;
                Windows.UI.Color offColor;
                bool onColorTiedToStandard = monitoredKey.onColorTiedToStandard;
                bool offColorTiedToStandard = monitoredKey.offColorTiedToStandard;

                // Sync colors to default if applicable
                if (onColorTiedToStandard)
                    onColor = DefaultColor;
                else
                    onColor = Windows.UI.Color.FromArgb(255, (byte)monitoredKey.onColor.R, (byte)monitoredKey.onColor.G, (byte)monitoredKey.onColor.B);

                if (offColorTiedToStandard)
                    offColor = DefaultColor;
                else
                    offColor = Windows.UI.Color.FromArgb(255, (byte)monitoredKey.offColor.R, (byte)monitoredKey.offColor.G, (byte)monitoredKey.offColor.B);

                // Actually apply the colors to the correct properties in the view model after processing
                switch (monitoredKey.key)
                {
                    case VK.NumLock:
                        NumLockOnColor = onColor;
                        NumLockOffColor = offColor;
                        SyncNumLockOnColor = onColorTiedToStandard;
                        SyncNumLockOffColor = offColorTiedToStandard;
                        break;
                    case VK.CapsLock:
                        CapsLockOnColor = onColor;
                        CapsLockOffColor = offColor;
                        SyncCapsLockOnColor = onColorTiedToStandard;
                        SyncCapsLockOffColor = offColorTiedToStandard;
                        break;
                    case VK.ScrollLock:
                        ScrollLockOnColor = onColor;
                        ScrollLockOffColor = offColor;
                        SyncScrollLockOnColor = onColorTiedToStandard;
                        SyncScrollLockOffColor = offColorTiedToStandard;
                        break;
                }
            }

            ColorSetter.DefineKeyboardMainColor(DefaultColor);

            window.ForceUpdateButtonBackgrounds();
            window.ForceUpdateAllButtonGlyphs();
        }

        // Set all the colors from the text boxes in the GUI
        public void UpdateAllColorSettingsFromGUI()
        {
            ScrollLockOnColor = GetColorFromString(TextScrollLockOnColor);
            ScrollLockOffColor = GetColorFromString(TextScrollLockOffColor);
            CapsLockOnColor = GetColorFromString(TextCapsLockOnColor);
            CapsLockOffColor = GetColorFromString(TextCapsLockOffColor);
            NumLockOnColor = GetColorFromString(TextNumLockOnColor);
            NumLockOffColor = GetColorFromString(TextNumLockOffColor);
            DefaultColor = GetColorFromString(TextDefaultColor);

            SyncScrollLockOnColor = SyncScrollLockOnColor;
            SyncScrollLockOffColor = SyncScrollLockOffColor;
            SyncCapsLockOnColor = SyncCapsLockOnColor;
            SyncCapsLockOffColor = SyncCapsLockOffColor;
            SyncNumLockOnColor = SyncNumLockOnColor;
            SyncNumLockOffColor = SyncNumLockOffColor;

            // Sync to defaults if set to do so. Janky but whatever
            if (SyncNumLockOnColor)
                NumLockOnColor = DefaultColor;
            if (SyncNumLockOffColor)
                NumLockOffColor = DefaultColor;
            if (SyncCapsLockOnColor)
                CapsLockOnColor = DefaultColor;
            if (SyncCapsLockOffColor)
                CapsLockOffColor = DefaultColor;
            if (SyncScrollLockOnColor)
                ScrollLockOnColor = DefaultColor;
            if (SyncScrollLockOffColor)
                ScrollLockOffColor = DefaultColor;

            ColorSetter.DefineKeyboardMainColor(DefaultColor);
        }

        internal bool IsColorSettingsSameAsConfig(UserConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), "UserConfig cannot be null.");
            }

            // Local function to compare two colors
            bool ColorsAreDifferent(Windows.UI.Color color1, RGBTuple color2)
            {
                if (!color1.R.Equals((byte)color2.R))
                    return true;
                else if (!color1.G.Equals((byte)color2.G))
                    return true;
                else if (!color1.B.Equals((byte)color2.B))
                    return true;
                else
                    return false;
            }

            if (ColorsAreDifferent(ScrollLockOnColor, config.MonitoredKeysAndColors.Find(x => x.key == VK.ScrollLock).onColor))
                return false;
            if (ColorsAreDifferent(ScrollLockOffColor, config.MonitoredKeysAndColors.Find(x => x.key == VK.ScrollLock).offColor))
                return false;
            if (ColorsAreDifferent(CapsLockOnColor, config.MonitoredKeysAndColors.Find(x => x.key == VK.CapsLock).onColor))
                return false;
            if (ColorsAreDifferent(CapsLockOffColor, config.MonitoredKeysAndColors.Find(x => x.key == VK.CapsLock).offColor))
                return false;
            if (ColorsAreDifferent(NumLockOnColor, config.MonitoredKeysAndColors.Find(x => x.key == VK.NumLock).onColor))
                return false;
            if (ColorsAreDifferent(NumLockOffColor, config.MonitoredKeysAndColors.Find(x => x.key == VK.NumLock).offColor))
                return false;
            if (ColorsAreDifferent(DefaultColor, config.StandardKeyColor))
                return false;

            if (Brightness != config.Brightness)
                return false;
            if (SyncScrollLockOnColor != config.MonitoredKeysAndColors.Find(x => x.key == VK.ScrollLock).onColorTiedToStandard)
                return false;
            if (SyncScrollLockOffColor != config.MonitoredKeysAndColors.Find(x => x.key == VK.ScrollLock).offColorTiedToStandard)
                return false;
            if (SyncCapsLockOnColor != config.MonitoredKeysAndColors.Find(x => x.key == VK.CapsLock).onColorTiedToStandard)
                return false;
            if (SyncCapsLockOffColor != config.MonitoredKeysAndColors.Find(x => x.key == VK.CapsLock).offColorTiedToStandard)
                return false;
            if (SyncNumLockOnColor != config.MonitoredKeysAndColors.Find(x => x.key == VK.NumLock).onColorTiedToStandard)
                return false;
            if (SyncNumLockOffColor != config.MonitoredKeysAndColors.Find(x => x.key == VK.NumLock).offColorTiedToStandard)
                return false;

            return true;
        }

        internal static VK GetKeyByPropertyName(string propertyName)
        {
            return propertyName switch
            {
                nameof(NumLockOnColor) => VK.NumLock,
                nameof(NumLockOffColor) => VK.NumLock,
                nameof(CapsLockOnColor) => VK.CapsLock,
                nameof(CapsLockOffColor) => VK.CapsLock,
                nameof(ScrollLockOnColor) => VK.ScrollLock,
                nameof(ScrollLockOffColor) => VK.ScrollLock,
                _ => throw new ArgumentException("Invalid property name.", nameof(propertyName)),
            };
        }

        internal static MainWindow.StateColorApply GetStateByPropertyName(string propertyName)
        {
            return propertyName switch
            {
                nameof(NumLockOnColor) => MainWindow.StateColorApply.On,
                nameof(NumLockOffColor) => MainWindow.StateColorApply.Off,
                nameof(CapsLockOnColor) => MainWindow.StateColorApply.On,
                nameof(CapsLockOffColor) => MainWindow.StateColorApply.Off,
                nameof(ScrollLockOnColor) => MainWindow.StateColorApply.On,
                nameof(ScrollLockOffColor) => MainWindow.StateColorApply.Off,
                _ => throw new ArgumentException("Invalid property name.", nameof(propertyName)),
            };
        }

        // TODO: Maybe use the properties directory or an enum instead of strings at some point but this works
        internal class ColorPropName
        {
            public const string NumLockOn = "NumLockOnColor";
            public const string NumLockOff = "NumLockOffColor";
            public const string CapsLockOn = "CapsLockOnColor";
            public const string CapsLockOff = "CapsLockOffColor";
            public const string ScrollLockOn = "ScrollLockOnColor";
            public const string ScrollLockOff = "ScrollLockOffColor";
            public const string DefaultColor = "DefaultColor";
        }

    } // ----------------------- End of MainViewModel -----------------------

}