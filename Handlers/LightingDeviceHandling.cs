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

            // Add to the list (thread-safe)
            AttachedDevice = info;

            // Set user config device ID
            currentConfig.DeviceId = device.Id;

            // Initialize the keyboard hook and callback to monitor key states
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            KeyStatesHandler.InitializeRawInput(hwnd);


            if (info != null)
                ColorSetter.BuildMonitoredKeyIndicesDict(info.lampArray);

            return info;
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
                    AttachToSavedDevice();
                }
            });
        }

        private void OnEnumerationCompleted(DeviceWatcher sender, object args)
        {
            Logging.WriteDebug("Device enumeration completed.");
            DispatcherQueue.TryEnqueue(() => AttachToSavedDevice());
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
