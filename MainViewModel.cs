using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;
using System;

namespace Dynamic_Lighting_Key_Indicator
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherQueue _dispatcherQueue;

        public MainViewModel()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
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
        public bool HasNoAttachedDevices => !HasAttachedDevices;
        public bool HasAttachedDevices
        {
            get => _hasAttachedDevices;
            set
            {
                if (SetProperty(ref _hasAttachedDevices, value))
                {
                    // Notify that HasNoAttachedDevices has also changed
                    OnPropertyChanged(nameof(HasNoAttachedDevices));
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
            }
        }

        // Custom object for storing colors for scroll lock, caps lock, and num lock. Contains Hex strings for on and off status of each
        private ColorSettings _colorSettings;
        public ColorSettings ColorSettings
        {
            get => _colorSettings;
            set => SetProperty(ref _colorSettings, value);
        }


        // ----------------------- Event Handlers -----------------------

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
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

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class ColorSettings
    {
        // Properties as strings to store hex values for colors
        public Windows.UI.Color ScrollLockOnColor { get; set; }
        public Windows.UI.Color ScrollLockOffColor { get; set; }
        public Windows.UI.Color CapsLockOnColor { get; set; }
        public Windows.UI.Color CapsLockOffColor { get; set; }
        public Windows.UI.Color NumLockOnColor { get; set; }
        public Windows.UI.Color NumLockOffColor { get; set; }
        public Windows.UI.Color DefaultColor { get; set; }
        public int Brightness { get; set; }

        private Windows.UI.Color GetColorFromString(string color)
        {
            if (color.StartsWith("#"))
            {
                color = color.Substring(1);
            }
            byte r = Convert.ToByte(color.Substring(0, 2), 16);
            byte g = Convert.ToByte(color.Substring(2, 2), 16);
            byte b = Convert.ToByte(color.Substring(4, 2), 16);
            return Windows.UI.Color.FromArgb(255, r, g, b);
        }

        // Methods to set the colors. Accepts hex strings with or without the # symbol
        public void SetScrollLockOnColor(string color)
        {
            ScrollLockOnColor = GetColorFromString(color);
        }

        public void SetScrollLockOffColor(string color)
        {
            ScrollLockOffColor = GetColorFromString(color);
        }

        public void SetCapsLockOnColor(string color)
        {
            CapsLockOnColor = GetColorFromString(color);
        }

        public void SetCapsLockOffColor(string color)
        {
            CapsLockOffColor = GetColorFromString(color);
        }

        public void SetNumLockOnColor(string color)
        {
            NumLockOnColor = GetColorFromString(color);
        }

        public void SetNumLockOffColor(string color)
        {
            NumLockOffColor = GetColorFromString(color);
        }

        public void SetDefaultColor(string color)
        {
            DefaultColor = GetColorFromString(color);
        }

        public void SetBrightness(int brightness)
        {
            Brightness = brightness;
        }

    }

}