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

        // GUI Related
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


        private void StartWatchingForLampArrays()
        {
            // Start watching for newly attached LampArrays.
            m_deviceWatcher = DeviceInformation.CreateWatcher(LampArray.GetDeviceSelector());

            m_deviceWatcher.Added += Watcher_Added;
            m_deviceWatcher.Removed += Watcher_Removed;
            m_deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;

            // Add event handler OnDeviceWatcherStopped to the Stopped event
            m_deviceWatcher.Stopped += OnDeviceWatcherStopped;

            m_deviceWatcher.Start();

            if (m_deviceWatcher.Status == DeviceWatcherStatus.Started)
            {
                ViewModel.DeviceWatcherStatusMessage = "DeviceWatcher Status: Started.";
                // Initialize the keyboard hook and callback to monitor key states
                KeyStatesHandler.InitializeHookAndCallback();
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
