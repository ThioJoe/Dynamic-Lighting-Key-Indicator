using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Devices.Lights;

namespace Dynamic_Lighting_Key_Indicator
{
    public sealed partial class MainWindow : Window
    {
        private async Task<LampArrayInfo?> AttachToDevice_Async(DeviceInformation device)
        {
            Logging.WriteDebug("Attempting to attach to device: " + device.Id);

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


            if (info != null)
                ColorSetter.BuildMonitoredKeyIndicesDict(info.lampArray);

            return info;
        }

        private async void TryAttachToSavedDevice()
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


        // The AvailabilityChanged event will fire when this calling process gains or loses control of RGB lighting for the specified LampArray.
        private void LampArray_AvailabilityChanged(LampArray sender, object args)
        {
            // args is always null, sender is the LampArray object that changed availability
            Logging.WriteDebug("AvailabilityChanged event fired.");

            UpdateStatusMessage();
        }

        // The 'Removed' event for the Watcher class.
        private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            Logging.WriteDebug("Device removal detected: " + args.Id);

            if ( AttachedDevice != null )
            {
                lock ( AttachedDevice )
                {
                    AttachedDevice = null;
                }
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
                    TryAttachToSavedDevice();
                }
            });
        }

        private void OnEnumerationCompleted(DeviceWatcher sender, object args)
        {
            Logging.WriteDebug("Device enumeration completed.");
            DispatcherQueue.TryEnqueue(() => TryAttachToSavedDevice());
        }

        private void OnDeviceWatcherStopped(DeviceWatcher sender, object args)
        {
            Console.WriteLine("DeviceWatcher stopped.");
            ViewModel.DeviceWatcherStatusMessage = "DeviceWatcher Status: Stopped.";

            if (KeyStatesHandler.rawInputWatcherActive == true)
            {
                KeyStatesHandler.CleanupInputWatcher(); // Stop the keyboard hook 
            }
        }
    }
}
