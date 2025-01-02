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

        // The AvailabilityChanged event will fire when this calling process gains or loses control of RGB lighting
        // for the specified LampArray.
        private void LampArray_AvailabilityChanged(LampArray sender, object args)
        {
            Logging.WriteDebug("AvailabilityChanged event fired.");
            UpdateStatusMessage();
        }

        private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            if (AttachedDevice == null)
                return;

            lock (AttachedDevice)
            {
                AttachedDevice = null;
            }

            // Update UI on the UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdatAvailableLampArrayDisplayList();
            });
        }

        private void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            string addedArrayID = args.Id;
            string addedArrayName = args.Name;

            if (addedArrayID == null)
            {
                return;
            }

            availableDevices.Add(args);

            // Don't update the UI until enumeration is done to avoid interupting the creation of availableDevices
            if (m_deviceWatcher == null || m_deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted)
            {
                return;
            }

            // Update UI on the UI thread. Only update the available devices since we might not attach to it.
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdatAvailableLampArrayDisplayList();
            });
        }

        private void OnEnumerationCompleted(DeviceWatcher sender, object args)
        {
            Logging.WriteDebug("Device enumeration completed.");
            // Update UI on the UI thread. Only update the available devices since we might not attach to it.
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdatAvailableLampArrayDisplayList();
            });

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
