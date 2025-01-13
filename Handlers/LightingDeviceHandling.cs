using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Devices.Lights;

namespace Dynamic_Lighting_Key_Indicator
{
    public sealed partial class MainWindow : Window
    {
        private bool deviceWasConnected = false; // To track whether we should go through re-attach process when availability changes

        private readonly Lock _devicelock = new();

        private async Task<LampArrayInfo?> AttachToDevice_Async(DeviceInformation device)
        {
            Logging.WriteDebug("Attempting to attach to device: " + device.Id);

            // Clear anything that depends on the previous device
            ColorSetter.DefineCurrentDevice(null);

            LampArray? lampArray = null;
            try
            {
                lampArray = await LampArray.FromIdAsync(device.Id).AsTask(); // This actually takes control of the device
            }
            catch (Exception ex)
            {
                Logging.WriteDebug($"Failed to initialize lamp array. Exception: {ex.Message}");
            }

            LampArrayInfo? info = new LampArrayInfo(device.Id, device.Name, lampArray);

            if (lampArray == null || info?.lampArray == null)
            {
                Logging.WriteDebug("Failed to initialize lamp array. Null lampArray returned from 'FromIdAsync' method.");
                string displayName = info?.displayName ?? "Unknown";

                // Update on UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.DeviceStatusMessage = new DeviceStatusInfo(DeviceStatusInfo.Msg.ErrorInitializing, suffix: displayName);
                });
                return null;
            }

            // Usually the device is not available to attach if not connected. But there is a delay between disconnecting and when Windows realizes it's disconnected.
            // Therefore it's possible to attach to a device that is not connected.
            if (lampArray.IsConnected == true)
            {
                deviceWasConnected = true;
            }
            // It should probably not even get to this point if it's not connected, but just in case of odd timing, ensure it matches the actual state
            else if (lampArray.IsConnected == false)
            {
                deviceWasConnected = false;
            }

            Logging.WriteDebug("   > Successfully attached to device");

            // Set up the AvailabilityChanged event callback
            info.lampArray.AvailabilityChanged += LampArray_AvailabilityChanged;

            // Add to the list
            AttachedDevice = info;
            ColorSetter.DefineCurrentDevice(info.lampArray);

            // Set user config device ID
            currentConfig.DeviceId = device.Id;

            ViewModel.CheckIfApplyButtonShouldBeEnabled();

            // Initialize the keyboard hook and callback to monitor key states
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            KeyStatesHandler.InitializeRawInput(hwnd);

            return info;
        }

        private async void TryAttachToSavedDevice_Async()
        {
            Logging.WriteDebug("Attempting to attach to saved device, if any.");
            // If current device ID is null or empty it probably means the user stopped watching so it reset, so don't try to attach or else it will throw an exception
            if (configSavedOnDisk.DeviceId == null || configSavedOnDisk.DeviceId == "")
            {
                Logging.WriteDebug("Device ID from config file is null or empty, not attempting to attach.");
                return;
            }

            Logging.WriteDebug("Found device ID in config file: " + configSavedOnDisk.DeviceId);
            DeviceInformation? device = availableDevices.FirstOrDefault(d => d.Id == configSavedOnDisk.DeviceId);

            if (device != null)
            {
                Logging.WriteDebug("   > Found matching device to the one in the config.");
                LampArrayInfo? lampArrayInfo = await AttachToDevice_Async(device);

                if (lampArrayInfo != null)
                {
                    ApplyLightingToDevice_AndSaveIdToConfig(lampArrayInfo);
                    UpdateSelectedDeviceDropdownToCurrentDevice();
                }
                else
                {
                    Logging.WriteDebug("Failed to attach to the device from the config file.");
                }
            }
            else
            {
                Logging.WriteDebug("Device ID from config file not found in available devices.");
            }
        }
        private void StartWatchingForLampArrays()
        {
            Logging.WriteDebug("Starting to watch for lamp arrays.");

            //string combinedSelector = await GetKeyboardLampArrayDeviceSelectorAsync();
            string lampArraySelector = LampArray.GetDeviceSelector();

            m_deviceWatcher = DeviceInformation.CreateWatcher(lampArraySelector);

            m_deviceWatcher.Added += OnDeviceAdded;
            m_deviceWatcher.Removed += OnDeviceRemoved;
            m_deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;

            // Add event handler OnDeviceWatcherStopped to the Stopped event
            m_deviceWatcher.Stopped += OnDeviceWatcherStopped;

            m_deviceWatcher.Start();

            if (m_deviceWatcher.Status == DeviceWatcherStatus.Started || m_deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
            {
                ViewModel.DeviceWatcherStatusMessage = "DeviceWatcher Status: Started.";
                ViewModel.IsWatcherRunning = true;
                Logging.WriteDebug("DeviceWatcher started.");
            }
            else
            {
                ViewModel.DeviceWatcherStatusMessage = "DeviceWatcher Status: Not started, something may have gone wrong.";
                Logging.WriteDebug("DeviceWatcher not started. Something may have gone wrong.");
            }
        }
        private void StopWatchingForLampArrays()
        {
            Logging.WriteDebug("Stopping to watch for lamp arrays.");

            if (KeyStatesHandler.rawInputWatcherActive == true)
            {
                KeyStatesHandler.CleanupInputWatcher();
            }

            if (m_deviceWatcher != null && (m_deviceWatcher.Status == DeviceWatcherStatus.Started || m_deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
            {
                m_deviceWatcher.Stop();
                ViewModel.IsWatcherRunning = false;
                Logging.WriteDebug("Stopped device watcher.");
            }
            else if (m_deviceWatcher == null)
            {
                ViewModel.IsWatcherRunning = false;
                Logging.WriteDebug("Device watcher was already null.");
            }
            else
            {
                Logging.WriteDebug("Device watcher was not running. The status was: " + m_deviceWatcher.Status);
            }

            ColorSetter.DefineCurrentDevice(null);
            currentConfig.DeviceId = "";
        }

        // -------------------------------------- CUSTOM EVENT HANDLERS --------------------------------------


        // The AvailabilityChanged event will fire when this calling process gains or loses control of RGB lighting for the attached
        // Only applies to the attached device. Will fire in addition to OnDeviceRemoved and OnDeviceAdded
        private void LampArray_AvailabilityChanged(LampArray sender, object args)
        {
            // args is always null, sender is the LampArray object that changed availability
            Logging.WriteDebug("AvailabilityChanged event fired.");

            if (sender.IsConnected == false)
            {
                Logging.WriteDebug("Device is no longer connected.");
                deviceWasConnected = false;
            }
            // If the device was not connected and now it is, we need to re-do the attaching process otherwise it doesn't behave properly
            else if (deviceWasConnected == false && sender.IsConnected == true)
            {
                TryAttachToSavedDevice_Async();
                // Attaching will update deviceWasConnected so we don't need to change that here
            }

            UpdateStatusMessage();
        }

        // The 'Removed' event for the Watcher class.
        private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            Logging.WriteDebug("Device removal detected: " + args.Id);

            if (AttachedDevice != null && args.Id == AttachedDevice.id)
            {
                lock (_devicelock)
                {
                    AttachedDevice = null;
                }
                ColorSetter.DefineCurrentDevice(null);
            }

            // Update UI on the UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                // Remove the device from the list of available devices if it's there
                DeviceInformation? deviceToRemove = availableDevices.FirstOrDefault(existingDevice => existingDevice.Id == args.Id);
                if ( deviceToRemove != null )
                {
                    _ = availableDevices.Remove(deviceToRemove);
                }
            });
        }

        // The 'Added' event for the Watcher class.
        private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation args)
        {
            string addedArrayID = args.Id;
            string addedArrayName = args.Name;

            Logging.WriteDebug("Device added: " + addedArrayID + " - " + addedArrayName);

            if ( addedArrayID == null )
            {
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                // Add the device to the list of available devices if it's not already there
                if ( !availableDevices.Any(existingDevice => existingDevice.Id == addedArrayID) )
                    availableDevices.Add(args);

                // After this check will only occur if enumeration was already finished and now a new device was added later
                if ( m_deviceWatcher == null || m_deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted )
                {
                    return;
                }

                // If we aren't attached to a device, check if we should attach to this one if it's saved
                if ( AttachedDevice == null && configSavedOnDisk.DeviceId == addedArrayID )
                {
                    Logging.WriteDebug("Added device matches the saved device ID. Attempting to attach.");
                    TryAttachToSavedDevice_Async();
                }
            });
        }

        private void OnEnumerationCompleted(DeviceWatcher sender, object args)
        {
            Logging.WriteDebug("Device enumeration completed.");
            DispatcherQueue.TryEnqueue(() => TryAttachToSavedDevice_Async());
        }

        private void OnDeviceWatcherStopped(DeviceWatcher sender, object args)
        {
            Console.WriteLine("DeviceWatcher stopped.");
            ViewModel.DeviceWatcherStatusMessage = "DeviceWatcher Status: Stopped.";

            lock (_devicelock)
            {
                AttachedDevice = null;
            }
            ColorSetter.DefineCurrentDevice(null);
            deviceWasConnected = false;

            if (KeyStatesHandler.rawInputWatcherActive == true)
            {
                KeyStatesHandler.CleanupInputWatcher(); // Stop the keyboard hook 
            }
        }
    }
}
