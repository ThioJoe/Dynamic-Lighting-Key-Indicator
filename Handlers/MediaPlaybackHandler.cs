using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Control;
using System.Runtime.InteropServices;
using Windows.Foundation; // For Marshal
using System.Diagnostics; // For Debug

namespace Dynamic_Lighting_Key_Indicator;
internal static class MediaPlaybackHandler
{
    // Media related objects:
    // See: https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession?view=winrt-26100
    private static GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private static GlobalSystemMediaTransportControlsSession? _currentSession;
    private static TypedEventHandler<GlobalSystemMediaTransportControlsSession, PlaybackInfoChangedEventArgs>? _playbackInfoChangedHandler;

    public static async Task Initialize()
    {
        // Get the session manager once
        _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

        // Create the event handler for playback info changes (create once, reuse many times)
        _playbackInfoChangedHandler = OnPlaybackInfoChanged;

        // Register for session changes - Like the media player app changes
        _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;

        // Initialize with current session if available
        InitializeNewSession();
    }

    private static void InitializeNewSession()
    {
        // Remove event handler from old session if exists
        if (_currentSession != null && _playbackInfoChangedHandler != null)
        {
            _currentSession.PlaybackInfoChanged -= _playbackInfoChangedHandler;
        }

        // Get the new session
        _currentSession = _sessionManager?.GetCurrentSession();

        // Add event handler to new session if exists
        if (_currentSession != null && _playbackInfoChangedHandler != null)
        {
            _currentSession.PlaybackInfoChanged += _playbackInfoChangedHandler;

            // You could also get initial media info here if needed
            //_ = PrintCurrentMediaPlaying();
        }
    }

    public static async Task PrintCurrentMediaPlaying()
    {
        if (_currentSession != null)
        {
            try
            {
                var mediaProperties = await _currentSession.TryGetMediaPropertiesAsync();
                var playbackInfo = _currentSession.GetPlaybackInfo();

                Debug.WriteLine($"{mediaProperties.Artist} - {mediaProperties.Title}");
                Debug.WriteLine($"Playback State: {playbackInfo.PlaybackStatus}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating media info: {ex.Message}");
            }
        }
    }

    // Cleanup method to unregister events when your app is shutting down
    public static void Cleanup()
    {
        if (_sessionManager != null)
        {
            _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }

        if (_currentSession != null && _playbackInfoChangedHandler != null)
        {
            _currentSession.PlaybackInfoChanged -= _playbackInfoChangedHandler;
        }
    }

    // Events
    private static void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        // Handle playback info changes here
        //Debug.WriteLine("Playback info changed");
        GlobalSystemMediaTransportControlsSessionPlaybackInfo info = sender.GetPlaybackInfo();
        Debug.WriteLine($"Playback status: {info.PlaybackStatus}");
    }

    private static void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        InitializeNewSession();
    }

}
