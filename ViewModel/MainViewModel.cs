using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel;
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
            _deviceStatusMessage = new DeviceStatusInfo(DeviceStatusInfo.Msg.Empty);
            _attachedDevicesMessage = "";
            _deviceWatcherStatusMessage = "";
            mainWindow = mainWindowPassIn;

            InitializeStartupTaskStateAsync();

            Debug.WriteLine("MainViewModel created.");
            this._showAdvancedInfo = debugMode;
        }

        public static void SetMainViewModelInstance(MainViewModel vieModelInstance)
        {
            mainViewModelInstance = vieModelInstance;
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
        internal void CheckAndUpdateSaveButton_EnabledStatus()
        {
            // Enable it if the colors are not the same as the config
            bool newEnabledStatus = !IsColorSettingsSameAsConfig(config: MainWindow.SavedConfig);

            // Only update the property if it doesn't already match the new enabled status
            if (IsSaveButtonEnabled != newEnabledStatus)
            {
                IsSaveButtonEnabled = newEnabledStatus;
                OnPropertyChanged(nameof(IsSaveButtonEnabled));
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

        // Color backgrounds for buttons
        public SolidColorBrush ScrollLockOnBrush => GetBrushFromColor(ScrollLockOnColor);
        public SolidColorBrush ScrollLockOffBrush => GetBrushFromColor(ScrollLockOffColor);
        public SolidColorBrush CapsLockOnBrush => GetBrushFromColor(CapsLockOnColor);
        public SolidColorBrush CapsLockOffBrush => GetBrushFromColor(CapsLockOffColor);
        public SolidColorBrush NumLockOnBrush => GetBrushFromColor(NumLockOnColor);
        public SolidColorBrush NumLockOffBrush => GetBrushFromColor(NumLockOffColor);
        public SolidColorBrush DefaultColorBrush => GetBrushFromColor(DefaultColor);


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

        private Color _scrollLockOnColor;
        public Color ScrollLockOnColor
        {
            get => _scrollLockOnColor;
            set
            {
                if (SetProperty(ref _scrollLockOnColor, value))
                {
                    OnPropertyChanged(nameof(ScrollLockOnBrush));

                }
            }
        }

        private Color _scrollLockOffColor;
        public Color ScrollLockOffColor
        {
            get => _scrollLockOffColor;
            set
            {
                if (SetProperty(ref _scrollLockOffColor, value))
                {
                    OnPropertyChanged(nameof(ScrollLockOffBrush));

                }
            }
        }

        private Color _capsLockOnColor;
        public Color CapsLockOnColor
        {
            get => _capsLockOnColor;
            set
            {
                if (SetProperty(ref _capsLockOnColor, value))
                {
                    OnPropertyChanged(nameof(CapsLockOnBrush));

                }
            }
        }

        private Color _capsLockOffColor;
        public Color CapsLockOffColor
        {
            get => _capsLockOffColor;
            set
            {
                if (SetProperty(ref _capsLockOffColor, value))
                {
                    OnPropertyChanged(nameof(CapsLockOffBrush));

                }
            }
        }

        private Color _numLockOnColor;
        public Color NumLockOnColor
        {
            get => _numLockOnColor;
            set
            {
                if (SetProperty(ref _numLockOnColor, value))
                {
                    OnPropertyChanged(nameof(NumLockOnBrush));

                }
            }
        }

        private Color _numLockOffColor;
        public Color NumLockOffColor
        {
            get => _numLockOffColor;
            set
            {
                if (SetProperty(ref _numLockOffColor, value))
                {
                    OnPropertyChanged(nameof(NumLockOffBrush));

                }
            }
        }

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
                ColorPropName.CapsLockOn => SyncCapsLockOnColor,
                ColorPropName.CapsLockOff => SyncCapsLockOffColor,
                ColorPropName.ScrollLockOn => SyncScrollLockOnColor,
                ColorPropName.ScrollLockOff => SyncScrollLockOffColor,
                _ => false,
            };
        }

        public bool GetSyncSetting_ByButtonName(string buttonName)
        {
            return buttonName switch
            {
                ButtonName.NumLockOn => SyncNumLockOnColor,
                ButtonName.NumLockOff => SyncNumLockOffColor,
                ButtonName.CapsLockOn => SyncCapsLockOnColor,
                ButtonName.CapsLockOff => SyncCapsLockOffColor,
                ButtonName.ScrollLockOn => SyncScrollLockOnColor,
                ButtonName.ScrollLockOff => SyncScrollLockOffColor,
                _ => false,
            };
        }

        public bool GetSyncSetting_ByButtonObject(Button button)
        {
            return GetSyncSetting_ByButtonName(button.Name);
        }

        public static readonly Thickness ActiveThickness = new Thickness(1);
        public static readonly Thickness InactiveThickness = new Thickness(0);

        private Thickness _scrollLockOnBorderThickness;
        private Thickness _scrollLockOffBorderThickness;
        private Thickness _capsLockOnBorderThickness;
        private Thickness _capsLockOffBorderThickness;
        private Thickness _numLockOnBorderThickness;
        private Thickness _numLockOffBorderThickness;

        public Thickness ScrollLockOnBorderThickness
        {
            get => _scrollLockOnBorderThickness;
            set => SetProperty(ref _scrollLockOnBorderThickness, value);
        }
        public Thickness ScrollLockOffBorderThickness
        {
            get => _scrollLockOffBorderThickness;
            set => SetProperty(ref _scrollLockOffBorderThickness, value);
        }
        public Thickness CapsLockOnBorderThickness
        {
            get => _capsLockOnBorderThickness;
            set => SetProperty(ref _capsLockOnBorderThickness, value);
        }
        public Thickness CapsLockOffBorderThickness
        {
            get => _capsLockOffBorderThickness;
            set => SetProperty(ref _capsLockOffBorderThickness, value);
        }
        public Thickness NumLockOnBorderThickness
        {
            get => _numLockOnBorderThickness;
            set => SetProperty(ref _numLockOnBorderThickness, value);
        }
        public Thickness NumLockOffBorderThickness
        {
            get => _numLockOffBorderThickness;
            set => SetProperty(ref _numLockOffBorderThickness, value);
        }

        private bool _LastKnownScrollLockState = false;
        private bool _LastKnownCapsLockState = false;
        private bool _LastKnownNumLockState = false;

        public bool LastKnownNumLockState
        {
            get => _LastKnownNumLockState;
            set
            {
                SetProperty(ref _LastKnownNumLockState, value);
                bool onIsActive = value;
                NumLockOnBorderThickness = onIsActive ? ActiveThickness : InactiveThickness;
                NumLockOffBorderThickness = onIsActive ? InactiveThickness : ActiveThickness;
                OnPropertyChanged(nameof(NumLockOnBorderThickness));
                OnPropertyChanged(nameof(NumLockOffBorderThickness));
            }
        }
        public bool LastKnownCapsLockState
        {
            get => _LastKnownCapsLockState;
            set
            {
                SetProperty(ref _LastKnownCapsLockState, value);
                bool onIsActive = value;
                CapsLockOnBorderThickness = onIsActive ? ActiveThickness : InactiveThickness;
                CapsLockOffBorderThickness = onIsActive ? InactiveThickness : ActiveThickness;
                OnPropertyChanged(nameof(CapsLockOnBorderThickness));
                OnPropertyChanged(nameof(CapsLockOffBorderThickness));
            }
        }
        public bool LastKnownScrollLockState
        {
            get => _LastKnownScrollLockState;
            set
            {
                SetProperty(ref _LastKnownScrollLockState, value);
                bool onIsActive = value;
                ScrollLockOnBorderThickness = onIsActive ? ActiveThickness : InactiveThickness;
                ScrollLockOffBorderThickness = onIsActive ? InactiveThickness : ActiveThickness;
                OnPropertyChanged(nameof(ScrollLockOnBorderThickness));
                OnPropertyChanged(nameof(ScrollLockOffBorderThickness));

            }
        }

        public void UpdateLastKnownKeyState(VK key, bool state)
        {
            switch (key)
            {
                case VK.NumLock:
                    LastKnownNumLockState = state;
                    break;
                case VK.CapsLock:
                    LastKnownCapsLockState = state;
                    break;
                case VK.ScrollLock:
                    LastKnownScrollLockState = state;
                    break;
            }
        }

        public void UpdateAllKeyStates(bool numLock, bool capsLock, bool scrollLock)
        {
            LastKnownNumLockState = numLock;
            LastKnownCapsLockState = capsLock;
            LastKnownScrollLockState = scrollLock;
        }

        public static void StaticUpdateLastKnownKeyState(MonitoredKey key)
        {
            if (mainViewModelInstance != null)
                mainViewModelInstance.UpdateLastKnownKeyState(key.key, key.IsOn());
        }

        //--------------------------------------------------------------

        public const string LinkedGlyph = "\uE71B";     // Chain link glyph
        public const string UnlinkedGlyph = "";         // No glyph if unlinked

        public string GetSyncGlyph_ByButtonObject(Button button)
        {
            return GetSyncSetting_ByButtonObject(button) ? LinkedGlyph : UnlinkedGlyph;
        }

        internal void ApplyAppSettingsFromUserConfig(UserConfig userConfig)
        {
            StartMinimizedToTray = userConfig.StartMinimizedToTray;
        }

        public Color GetColorFromString(string color)
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
            return Color.FromArgb(a: 255, r, g, b);
        }

        public static string AsString(Color color)
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
            DefaultColor = Color.FromArgb(255, (byte)config.StandardKeyColor.R, (byte)config.StandardKeyColor.G, (byte)config.StandardKeyColor.B);

            foreach (MonitoredKey monitoredKey in config.MonitoredKeysAndColors)
            {
                Color onColor;
                Color offColor;
                bool onColorTiedToStandard = monitoredKey.onColorTiedToStandard;
                bool offColorTiedToStandard = monitoredKey.offColorTiedToStandard;

                // Sync colors to default if applicable
                if (onColorTiedToStandard)
                    onColor = DefaultColor;
                else
                    onColor = Color.FromArgb(255, (byte)monitoredKey.onColor.R, (byte)monitoredKey.onColor.G, (byte)monitoredKey.onColor.B);

                if (offColorTiedToStandard)
                    offColor = DefaultColor;
                else
                    offColor = Color.FromArgb(255, (byte)monitoredKey.offColor.R, (byte)monitoredKey.offColor.G, (byte)monitoredKey.offColor.B);

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

        // Set all the colors from the text boxes in the GUI. This might be redundant now that the properties are bound to the text boxes.
        public void UpdateAllColorSettingsFromGUI()
        {
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

        public static SolidColorBrush GetBrushFromColor(Color color)
        {
            return new SolidColorBrush(color);
        }

        internal bool IsColorSettingsSameAsConfig(UserConfig config)
        {
            // ----------------------- Local Functions -----------------------
            bool ColorsAreDifferent(Color color1, RGBTuple color2)
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

            MonitoredKey? GetMonitoredKeyObj(VK key)
            {
                MonitoredKey? keyObj = config.MonitoredKeysAndColors.Find(x => x.key == key);
                return keyObj;
            }

            //  ---------------------------------------------------------------

            if (config == null)
            {
                Debug.WriteLine("IsColorSettingsSameAsConfig: Config is null.");
                return false;
            }

            MonitoredKey? CapsLockKey = GetMonitoredKeyObj(VK.CapsLock);
            MonitoredKey? NumLockKey = GetMonitoredKeyObj(VK.NumLock);
            MonitoredKey? ScrollLockKey = GetMonitoredKeyObj(VK.ScrollLock);

            // If any of the keys are null, return false
            if (CapsLockKey == null || NumLockKey == null || ScrollLockKey == null)
                return false;

            // Brightness
            if (Brightness != config.Brightness)
                return false;

            if (ColorsAreDifferent(ScrollLockOnColor, ScrollLockKey.onColor))
                return false;
            if (ColorsAreDifferent(ScrollLockOffColor, ScrollLockKey.offColor))
                return false;
            if (ColorsAreDifferent(CapsLockOnColor, CapsLockKey.onColor))
                return false;
            if (ColorsAreDifferent(CapsLockOffColor, CapsLockKey.offColor))
                return false;
            if (ColorsAreDifferent(NumLockOnColor, NumLockKey.onColor))
                return false;
            if (ColorsAreDifferent(NumLockOffColor, NumLockKey.offColor))
                return false;
            if (ColorsAreDifferent(DefaultColor, config.StandardKeyColor))
                return false;

            // Sync on/off each key
            if (SyncScrollLockOnColor != ScrollLockKey.onColorTiedToStandard)
                return false;
            if (SyncScrollLockOffColor != ScrollLockKey.offColorTiedToStandard)
                return false;
            if (SyncCapsLockOnColor != CapsLockKey.onColorTiedToStandard)
                return false;
            if (SyncCapsLockOffColor != CapsLockKey.offColorTiedToStandard)
                return false;
            if (SyncNumLockOnColor != NumLockKey.onColorTiedToStandard)
                return false;
            if (SyncNumLockOffColor != NumLockKey.offColorTiedToStandard)
                return false;

            // Everything matches at this point
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

        internal static StateColorApply GetStateByPropertyName(string propertyName)
        {
            return propertyName switch
            {
                nameof(NumLockOnColor) => StateColorApply.On,
                nameof(NumLockOffColor) => StateColorApply.Off,
                nameof(CapsLockOnColor) => StateColorApply.On,
                nameof(CapsLockOffColor) => StateColorApply.Off,
                nameof(ScrollLockOnColor) => StateColorApply.On,
                nameof(ScrollLockOffColor) => StateColorApply.Off,
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

        internal class ButtonName
        {
            public const string NumLockOn = "buttonNumLockOn";
            public const string NumLockOff = "buttonNumLockOff";
            public const string CapsLockOn = "buttonCapsLockOn";
            public const string CapsLockOff = "buttonCapsLockOff";
            public const string ScrollLockOn = "buttonScrollLockOn";
            public const string ScrollLockOff = "buttonScrollLockOff";
        }

    } // ----------------------- End of MainViewModel -----------------------

}