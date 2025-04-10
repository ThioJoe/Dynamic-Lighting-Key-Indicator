using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

#nullable enable

//// Example Usage:
//SystemMuteNotifier notifier = new SystemMuteNotifier();
// notifier.MuteChanged += HandleMuteStateChange;
//private static void HandleMuteStateChange(object? sender, SystemMuteNotifier.MuteState state)
//{
//    // Whatever logic here
//}

public sealed class SystemMuteNotifier : IDisposable
{
    private IAudioEndpointVolume _endpointVolume;
    private AudioEndpointVolumeCallback _callback;
    private bool _isDisposed;

    // Queue for thread-safe signalling from callback to main thread
    private readonly ConcurrentQueue<object> _notificationQueue = new ConcurrentQueue<object>();
    // Sentinel object to put in the queue
    private static readonly object MuteChangeEvent = new object();

    // Create events other classes can subscribe to for mute changes
    public event EventHandler<MuteState>? MuteChanged;

    // -----------------------------------------------
    private MuteState _lastKnownState = MuteState.None;

    private void OnReceivedMuteState(MuteState newState)
    {
        //Console.WriteLine($"OnReceivedMuteState: {newState} (Previous: {_lastKnownState})");
        // Only invoke the event if the state has changed
        if (newState != _lastKnownState)
        {
            // Special case where new state is VolumeZeroMuted and last known state is VolumeZeroUnmuted.
            // This means it must have been an explicit mute.
            if (newState == MuteState.VolumeZeroMuted && _lastKnownState == MuteState.VolumeZeroUnmuted)
            {
                newState = MuteState.ExplicitMute;
            }

            _lastKnownState = newState;
            PrintChangedState(newState);
            MuteChanged?.Invoke(this, newState);
        }
    }

    public enum MuteState
    {
        ExplicitMute,
        VolumeZeroMuted,
        VolumeZeroUnmuted,
        Unmuted,
        None
    }

    public SystemMuteNotifier(bool useNotificationQueue=false)
    {
        // Constructor remains the same (without CoInitializeEx)
        IMMDeviceEnumerator deviceEnumerator = null;
        IMMDevice defaultDevice = null;
        try
        {
            Type enumeratorType = Type.GetTypeFromCLSID(ComGuids.MMDeviceEnumeratorCLSID);
            deviceEnumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType);
            int hr = deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out defaultDevice);
            Marshal.ThrowExceptionForHR(hr);
            Guid IID_IAudioEndpointVolume = ComGuids.IAudioEndpointVolumeIID;
            hr = defaultDevice.Activate(ref IID_IAudioEndpointVolume, (uint)CLSCTX.ALL, IntPtr.Zero, out object endpointVolumeObject);
            Marshal.ThrowExceptionForHR(hr);
            _endpointVolume = (IAudioEndpointVolume)endpointVolumeObject;
            _callback = new AudioEndpointVolumeCallback(this);
            hr = _endpointVolume.RegisterControlChangeNotify(_callback);
            Marshal.ThrowExceptionForHR(hr);
            Console.WriteLine("SystemMuteNotifier initialized. Monitoring mute changes...");
            HandleMuteChangeInternal(); // Initial state check
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Initialization failed: {ex.Message}");
            Dispose();
            throw;
        }
        finally
        {
            if (defaultDevice != null) Marshal.ReleaseComObject(defaultDevice);
            if (deviceEnumerator != null) Marshal.ReleaseComObject(deviceEnumerator);
        }
    }

    private void PrintChangedState(MuteState state)
    {
        switch (state)
        {
            case MuteState.ExplicitMute:
                Console.WriteLine("Explicitly Muted");
                break;
            case MuteState.VolumeZeroUnmuted:
                Console.WriteLine("Unmuted While Volume Zero");
                break;
            case MuteState.VolumeZeroMuted:
                Console.WriteLine("Volume Set to Zero"); // Indistinguishable from an explicit mute if the volume was already zero without knowing previous state
                break;
            case MuteState.Unmuted:
                Console.WriteLine("Unmuted");
                break;
            default:
                Console.WriteLine("Unknown State");
                break;
        }
    }

    // Called ONLY from the main STA thread via ProcessPendingNotifications
    private void HandleMuteChangeInternal()
    {
        if (_isDisposed || _endpointVolume == null) return;

        bool isMuted = false;
        float currentVolume = -1.0f;
        bool muteStatusRetrieved = false;
        bool volumeStatusRetrieved = false;

        try
        {
            // --- Check Mute Status ---
            int hrMute = _endpointVolume.GetMute(out isMuted);
            if (hrMute == 0) { muteStatusRetrieved = true; }
            else { Console.WriteLine($" >> Error getting mute state: HRESULT={hrMute:X}"); }

            // --- Check Volume Level ---
            int hrVol = _endpointVolume.GetMasterVolumeLevelScalar(out currentVolume);
            if (hrVol == 0) { volumeStatusRetrieved = true; }
            else { Console.WriteLine($" >> Error getting volume scalar: HRESULT={hrVol:X}"); }

            // --- Determine and Print Combined Status ---
            if (muteStatusRetrieved && volumeStatusRetrieved) // Process only if both checks succeeded
            {
                string currentVolumePercent = currentVolume.ToString("P0");
                if (isMuted)
                {
                    // Mute reported as TRUE. Now check volume to differentiate cause.
                    if (currentVolume == 0.0f)
                    {
                        // Muted is TRUE and Volume is 0 - Likely caused by setting volume to 0.
                        OnReceivedMuteState(MuteState.VolumeZeroMuted);
                    }
                    else
                    {
                        // Muted is TRUE but Volume is > 0 - Likely caused by explicit mute action.
                        OnReceivedMuteState(MuteState.ExplicitMute);
                    }
                }
                else // Mute reported as FALSE. It is still possible to be muted if volume is 0.
                {
                    if (currentVolume == 0.0f)
                    {
                        // Muted is FALSE but Volume is 0 - Likely caused by setting volume to 0.
                        OnReceivedMuteState(MuteState.VolumeZeroUnmuted);
                    }
                    else
                    {
                        // Muted is FALSE and Volume is > 0 - System is unmuted.
                        OnReceivedMuteState(MuteState.Unmuted);
                    }
                }
            }
            else
            {
                Console.WriteLine(">> Could not determine full audio status due to errors.");
            }
        }
        catch (Exception ex) // Catch potential exceptions during COM calls
        {
            Console.WriteLine($" >> Exception in HandleMuteChangeInternal: {ex.Message}");
        }
    }

    // Public method to get the current mute state on demand (must be called from STA thread)
    public bool GetCurrentMuteState()
    {
        if (_isDisposed || _endpointVolume == null)
        {
            throw new ObjectDisposedException(nameof(SystemMuteNotifier));
        }
        // Assuming this is called from the main STA thread, otherwise needs marshalling too.
        int hr = _endpointVolume.GetMute(out bool isMuted);
        Marshal.ThrowExceptionForHR(hr);
        return isMuted;
    }

    // Method called by the callback to signal an event occurred
    internal void SignalChange()
    {
        HandleMuteChangeInternal(); // Directly call the handler for simplicity
    }

    // Dispose method remains the same (without CoUninitialize)
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Clear the queue on dispose
        while (_notificationQueue.TryDequeue(out _)) { }

        if (_endpointVolume != null)
        {
            if (_callback != null)
            {
                try { _endpointVolume.UnregisterControlChangeNotify(_callback); }
                catch (Exception ex) { Console.WriteLine($"Error unregistering callback: {ex.Message}"); }
                _callback = null;
            }
            try { Marshal.ReleaseComObject(_endpointVolume); }
            catch (Exception ex) { Console.WriteLine($"Error releasing endpoint volume: {ex.Message}"); }
            _endpointVolume = null;
        }
        Console.WriteLine("SystemMuteNotifier disposed.");
        GC.SuppressFinalize(this);
    }

    ~SystemMuteNotifier() { Dispose(); }

    // --- Inner Class for Callback Implementation ---
    [ComVisible(true)]
    private class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
    {
        private readonly SystemMuteNotifier _parent;
        internal AudioEndpointVolumeCallback(SystemMuteNotifier parent) { _parent = parent; }

        public int OnNotify(IntPtr pNotifyData)
        {
            // DO NOT call GetMute or other COM methods here!
            // Just signal the parent object to handle it on the main thread.
            _parent?.SignalChange();
            return 0; // S_OK
        }
    }
}

// --- P/Invoke and COM Definitions ---

internal static class Ole32
{
    [Flags]
    public enum COINIT : uint
    {
        MULTITHREADED = 0x0,
        APARTMENTTHREADED = 0x2,
        DISABLE_OLE1DDE = 0x4,
        SPEED_OVER_MEMORY = 0x8
    }

    // Corrected CoInitializeEx definition:
    // - Returns int (HRESULT)
    // - PreserveSig = true (default, so explicitly stated or omitted)
    [DllImport("Ole32.dll", SetLastError = true, PreserveSig = true)]
    public static extern int CoInitializeEx(IntPtr pvReserved, COINIT dwCoInit);

    [DllImport("Ole32.dll", SetLastError = true)]
    public static extern void CoUninitialize();
}

// Minimal definitions needed for Core Audio APIs via COM Interop

internal enum EDataFlow
{
    eRender,
    eCapture,
    eAll,
    EDataFlow_enum_count
}

internal enum ERole
{
    eConsole,
    eMultimedia,
    eCommunications,
    ERole_enum_count
}

[Flags]
internal enum CLSCTX : uint
{
    INPROC_SERVER = 0x1,
    INPROC_HANDLER = 0x2,
    LOCAL_SERVER = 0x4,
    INPROC_SERVER16 = 0x8,
    REMOTE_SERVER = 0x10,
    INPROC_HANDLER16 = 0x20,
    RESERVED1 = 0x40,
    RESERVED2 = 0x80,
    RESERVED3 = 0x100,
    RESERVED4 = 0x200,
    NO_CODE_DOWNLOAD = 0x400,
    RESERVED5 = 0x800,
    NO_CUSTOM_MARSHAL = 0x1000,
    ENABLE_CODE_DOWNLOAD = 0x2000,
    NO_FAILURE_LOG = 0x4000,
    DISABLE_AAA = 0x8000,
    ENABLE_AAA = 0x10000,
    FROM_DEFAULT_CONTEXT = 0x20000,
    ACTIVATE_32_BIT_SERVER = 0x40000,
    ACTIVATE_64_BIT_SERVER = 0x80000,
    ENABLE_CLOAKING = 0x100000,
    APPCONTAINER = 0x400000,
    ACTIVATE_AAA_AS_IU = 0x800000,
    ACTIVATE_NATIVE_SERVER_ONLY = 0x10000000,
    PS_DLL = 0x80000000,
    ALL = INPROC_SERVER | INPROC_HANDLER | LOCAL_SERVER | REMOTE_SERVER
}

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    // We only need GetDefaultAudioEndpoint for this example
    [PreserveSig] // Keep HRESULT return value
    int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out /*IMMDeviceCollection*/ IntPtr ppDevices); // Avoid defining IMMDeviceCollection

    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);

    // Other methods omitted for brevity...
    [PreserveSig]
    int GetDevice(string pwstrId, out IMMDevice ppDevice);
    [PreserveSig]
    int RegisterEndpointNotificationCallback(/*IAudioEndpointNotificationCallback*/ IntPtr pClient);
    [PreserveSig]
    int UnregisterEndpointNotificationCallback(/*IAudioEndpointNotificationCallback*/ IntPtr pClient);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    // We only need Activate for this example
    [PreserveSig]
    int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

    // Other methods omitted for brevity...
    [PreserveSig]
    int OpenPropertyStore(uint stgmAccess, out /*IPropertyStore*/ IntPtr ppProperties);
    [PreserveSig]
    int GetId(out string ppstrId);
    [PreserveSig]
    int GetState(out uint pdwState);
}

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    // Method 1: RegisterControlChangeNotify
    [PreserveSig]
    int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);

    // Method 2: UnregisterControlChangeNotify
    [PreserveSig]
    int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);

    // Method 3: GetChannelCount
    [PreserveSig]
    int GetChannelCount(out uint pnChannelCount);

    // Method 4: SetMasterVolumeLevel
    [PreserveSig]
    int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);

    // Method 5: SetMasterVolumeLevelScalar
    [PreserveSig]
    int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);

    // Method 6: GetMasterVolumeLevel
    [PreserveSig]
    int GetMasterVolumeLevel(out float pfLevelDB);

    // Method 7: GetMasterVolumeLevelScalar
    [PreserveSig]
    int GetMasterVolumeLevelScalar(out float pfLevel);

    // Method 8: SetChannelVolumeLevel - ADDED PLACEHOLDER
    [PreserveSig]
    int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);

    // Method 9: SetChannelVolumeLevelScalar - ADDED PLACEHOLDER
    [PreserveSig]
    int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);

    // Method 10: GetChannelVolumeLevel - ADDED PLACEHOLDER
    [PreserveSig]
    int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);

    // Method 11: GetChannelVolumeLevelScalar - ADDED PLACEHOLDER
    [PreserveSig]
    int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);

    // Method 12: SetMute
    [PreserveSig]
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);

    // Method 13: GetMute - Now at the correct VTable slot
    [PreserveSig]
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);

    // Method 14: GetVolumeStepInfo - Not needed for GetMute, but good practice to include all
    [PreserveSig]
    int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);

    // Method 15: VolumeStepUp - Not needed for GetMute
    [PreserveSig]
    int VolumeStepUp(ref Guid pguidEventContext);

    // Method 16: VolumeStepDown - Not needed for GetMute
    [PreserveSig]
    int VolumeStepDown(ref Guid pguidEventContext);

    // Method 17: QueryHardwareSupport - Not needed for GetMute
    [PreserveSig]
    int QueryHardwareSupport(out uint pdwHardwareSupportMask);

    // Method 18: GetVolumeRange - Not needed for GetMute
    [PreserveSig]
    int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
}

[Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolumeCallback
{
    // The notification callback method
    [PreserveSig]
    int OnNotify(IntPtr pNotifyData); // Use IntPtr, AUDIO_VOLUME_NOTIFICATION_DATA* definition omitted for brevity
}

// CLSIDs and IIDs used
internal static class ComGuids
{
    // CLSID_MMDeviceEnumerator
    public static readonly Guid MMDeviceEnumeratorCLSID = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    // IID_IAudioEndpointVolume
    public static readonly Guid IAudioEndpointVolumeIID = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
    // Event context (can be Guid.Empty if we don't need to distinguish our changes)
    public static readonly Guid EventContext = Guid.Empty;
}
