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

            // Set up keyboard hook
            KeyStatesHandler.SetMonitoredKeys(new List<MonitoredKey> {
                new MonitoredKey(VK.NumLock, onColor: (R:255, G:0, B:0), offColor: null),
                new MonitoredKey(VK.CapsLock, onColor: (R:255, G:0, B:0), offColor: null),
                new MonitoredKey(VK.ScrollLock, onColor: (R:255, G:0, B:0), offColor: null)
            });

            ColorSetter.DefineKeyboardMainColor_FromNameAndBrightness(color: Colors.Blue, brightnessPercent: 100);

        }

        private async Task<string> GetKeyboardLampArrayDeviceSelectorAsync()
        {
            List<DeviceInformation> matchingDevices = await FindKeyboardLampArrayDevices();

            if (matchingDevices.Count == 0)
            {
                return ""; // No matching devices found
            }

            string lampArraySelector = LampArray.GetDeviceSelector();

            // Construct combination of lamparrayselector and container id
            int deviceIndex = 0;

            string newSelector = lampArraySelector + " AND System.Devices.ContainerId:(";
            foreach (var device in matchingDevices)
            {
                if (deviceIndex != 0)
                {
                    newSelector += " OR ";
                }
                newSelector += "={" + device.Properties["System.Devices.ContainerId"].ToString() + "}";
                deviceIndex++;
            }
            newSelector += ")";

            return newSelector;
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
                // Initialize the keyboard hook and callback to monitor key states
                KeyStatesHandler.InitializeHookAndCallback();
            }
            else
            {
                ViewModel.DeviceWatcherStatusMessage = "DeviceWatcher Status: Not started, something may have gone wrong.";
            }
        }

        private async Task<List<DeviceInformation>> FindKeyboardLampArrayDevices()
        {
            string keyboardSelector = HidDevice.GetDeviceSelector((ushort)HIDUsagePage.HID_USAGE_PAGE_GENERIC, (ushort)HIDGenericDesktopUsage.HID_USAGE_GENERIC_KEYBOARD);
            string lampArraySelector = LampArray.GetDeviceSelector();

            // Get both sets of devices
            var keyboardDevices = await DeviceInformation.FindAllAsync(keyboardSelector);
            var lampArrayDevices = await DeviceInformation.FindAllAsync(lampArraySelector);

            var keyboardDict = new Dictionary<string, DeviceInformation>();
            foreach (var keyboardDevice in keyboardDevices)
            {
                var containerId = keyboardDevice.Properties["System.Devices.ContainerId"].ToString();
                if (containerId != null)
                {
                    keyboardDict.Add(containerId, keyboardDevice);
                }
            }

            var lampArrayDevicesDict = new Dictionary<string, DeviceInformation>();
            foreach (var lampArrayDevice in lampArrayDevices)
            {
                var containerId = lampArrayDevice.Properties["System.Devices.ContainerId"].ToString();
                if (containerId != null && !lampArrayDevicesDict.ContainsKey(containerId)) // Check if it's not null and not already in the dictionary
                {
                    lampArrayDevicesDict.Add(containerId, lampArrayDevice);
                }
            }

            // Find devices that have both interfaces by comparing their container IDs
            var matchingDevices = new List<DeviceInformation>();
            foreach (var containerId in keyboardDict.Keys.Intersect(lampArrayDevicesDict.Keys))
            {
                matchingDevices.Add(lampArrayDevicesDict[containerId]);
            }

            return matchingDevices;
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

            ViewModel.HasAttachedDevices = false;
        }

        private void UpdatAvailableLampArrayDisplayList()
        {
            string message = $"Available LampArrays: {availableDevices.Count}";
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
            int deviceIndex = 0;

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

        async Task<LampArrayInfo?> AttachAndGetSelectedDeviceAsync()
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

            if (selectedDeviceObj == null) {
                return null;
            }
            else
            {
                LampArrayInfo? device = await Attach_To_DeviceAsync(selectedDeviceObj);
                return device;
            }
        }

        private async void ShowErrorMessage(string message)
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
            ColorSetter.SetCurrentDevice(lampArray);
            ColorSetter.SetInitialDefaultKeyboardColor(lampArray);
            KeyStatesHandler.UpdateKeyStatus();
        }

        private void Check_For_Keybords()
        {
            foreach (DeviceInformation device in availableDevices)
            {
                // Use enumeration APIs to get the exact kind of device. The 'Kind' property is too general
                // TESTING
                Console.WriteLine(device.Kind);

            }
        }

        //If no device has been set, and a keyboard is attached, set the keyboard as the current device
        private void CheckForCurrentDeviceAndApply()
        {
            if (ColorSetter.CurrentDevice == null)
            {
                foreach (var info in m_attachedLampArrays)
                {
                    if ((int)info.lampArray.LampArrayKind == (int)LampArrayKind.Keyboard)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            ViewModel.SelectedDeviceIndex = m_attachedLampArrays.IndexOf(info);
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

        // -------------------------------------- CUSTOM EVENT HANDLERS --------------------------------------

        // The AvailabilityChanged event will fire when this calling process gains or loses control of RGB lighting
        // for the specified LampArray.
        private void LampArray_AvailabilityChanged(LampArray sender, object args)
        {
            // Update UI on the UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateAttachedLampArrayDisplayList();
            });
        }

        private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            lock (m_attachedLampArrays)
            {
                // Remove devices from our array that match the ID of the device from the event
                m_attachedLampArrays.RemoveAll(info => info.id == args.Id);
            }

            // Update UI on the UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateAttachedLampArrayDisplayList();
                UpdatAvailableLampArrayDisplayList();
            });


        }

        private async void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            string addedArrayID = args.Id;
            string addedArrayName = args.Name;

            if (addedArrayID == null)
            {
                return;
            }

            availableDevices.Add(args);

            // Update UI on the UI thread. Only update the available devices since we might not attach to it.
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdatAvailableLampArrayDisplayList();
            });

        }

        private async Task<LampArrayInfo?> Attach_To_DeviceAsync(DeviceInformation args)
        {
            var lampArray = await LampArray.FromIdAsync(args.Id); // This actually takes control of the device
            var info = new LampArrayInfo(args.Id, args.Name, lampArray);

            if (info.lampArray == null)
            {
                // Update on UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.DeviceStatusMessage = $"Status: Error initializing LampArray: \"{info.displayName}\"";
                });
                return null;
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
                UpdateAttachedLampArrayDisplayList();
            });

            return info;
        }

        private void OnEnumerationCompleted(DeviceWatcher sender, object args)
        {
            //DispatcherQueue.TryEnqueue(() => CheckForCurrentDeviceAndApply());
        }

        private void OnDeviceWatcherStopped(DeviceWatcher sender, object args)
        {
            Console.WriteLine("DeviceWatcher stopped.");
            ViewModel.DeviceWatcherStatusMessage = "DeviceWatcher Status: Stopped.";

            if (KeyStatesHandler.hookIsActive == true)
            {
                KeyStatesHandler.StopHook(); // Stop the keyboard hook 
            }
        }

        // -------------------------------------- GUI EVENT HANDLERS --------------------------------------
        private void buttonStartWatch_Click(object sender, RoutedEventArgs e)
        {
            // Clear the current list of attached devices
            m_attachedLampArrays.Clear();
            availableDevices.Clear();

            StartWatchingForLampArrays();
        }

        private void buttonStopWatch_Click(object sender, RoutedEventArgs e)
        {
            StopWatchingForLampArrays();
        }

        private async void buttonApply_Click(object sender, RoutedEventArgs e)
        {
            // If there was a device attached, remove it
            if (ColorSetter.CurrentDevice != null)
            {
                ColorSetter.SetCurrentDevice(null);
            }

            LampArrayInfo? selectedLampArrayInfo = await AttachAndGetSelectedDeviceAsync();

            if (selectedLampArrayInfo != null)
            {
                ApplyLightingToDevice(selectedLampArrayInfo);
            }
        }

        // ---------------------------------------------------------------------------------------------------
    }
}
