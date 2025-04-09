using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI; // Assuming Color is from here or Microsoft.UI

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace Dynamic_Lighting_Key_Indicator
{
    public class KeyIndicatorState : INotifyPropertyChanged
    {
        // Constants from MainViewModel (or define globally)
        public const string LinkedGlyph = "\uE71B";   // Chain link glyph
        public const string UnlinkedGlyph = "";      // No glyph if unlinked
        public static readonly Thickness ActiveThickness = new Thickness(1);
        public static readonly Thickness InactiveThickness = new Thickness(0);

        private readonly Action<string?> _notifyParentPropertyChanged; // Delegate to notify parent ViewModel

        private readonly DispatcherQueue _dispatcherQueue; // Add this field

        // Modify the constructor
        public KeyIndicatorState(DispatcherQueue dispatcherQueue) // Accept DispatcherQueue
        {
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
            // Set initial defaults
            _onBorderThickness = InactiveThickness;
            _offBorderThickness = InactiveThickness;
            _onColor = Colors.Transparent; // Or some default
            _offColor = Colors.Transparent; // Or some default
        }


        public KeyIndicatorState(Action<string?> notifyParentPropertyChanged)
        {
            _notifyParentPropertyChanged = notifyParentPropertyChanged ?? throw new ArgumentNullException(nameof(notifyParentPropertyChanged));
            // Set initial defaults
            _onBorderThickness = InactiveThickness;
            _offBorderThickness = InactiveThickness;
            _onColor = Colors.Transparent; // Or some default
            _offColor = Colors.Transparent; // Or some default
        }

        private bool _syncOnColor;
        public bool SyncOnColor
        {
            get => _syncOnColor;
            set
            {
                if (SetProperty(ref _syncOnColor, value))
                {
                    OnPropertyChanged(nameof(OnGlyph)); // Update Glyph when Sync changes
                }
            }
        }

        private bool _syncOffColor;
        public bool SyncOffColor
        {
            get => _syncOffColor;
            set
            {
                if (SetProperty(ref _syncOffColor, value))
                {
                    OnPropertyChanged(nameof(OffGlyph)); // Update Glyph when Sync changes
                }
            }
        }

        private Color _onColor;
        public Color OnColor
        {
            get => _onColor;
            set
            {
                if (SetProperty(ref _onColor, value))
                {
                    OnPropertyChanged(nameof(OnBrush)); // Update Brush when Color changes
                }
            }
        }

        private Color _offColor;
        public Color OffColor
        {
            get => _offColor;
            set
            {
                if (SetProperty(ref _offColor, value))
                {
                    OnPropertyChanged(nameof(OffBrush)); // Update Brush when Color changes
                }
            }
        }

        private Thickness _onBorderThickness;
        public Thickness OnBorderThickness
        {
            get => _onBorderThickness;
            private set => SetProperty(ref _onBorderThickness, value); // Setter should be private or internal
        }

        private Thickness _offBorderThickness;
        public Thickness OffBorderThickness
        {
            get => _offBorderThickness;
            private set => SetProperty(ref _offBorderThickness, value); // Setter should be private or internal
        }

        private bool _lastKnownState;
        public bool LastKnownState
        {
            get => _lastKnownState;
            set
            {
                // Use SetProperty for notification, but also update borders
                if (SetProperty(ref _lastKnownState, value))
                {
                    // Update border thickness based on the new state
                    OnBorderThickness = value ? ActiveThickness : InactiveThickness;
                    OffBorderThickness = value ? InactiveThickness : ActiveThickness;
                    // No need to call OnPropertyChanged for borders here, SetProperty in their setters handles it.
                }
            }
        }

        public Color GetStateColor(StateColorApply state)
        {
            return state switch
            {
                StateColorApply.On => OnColor,
                StateColorApply.Off => OffColor,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }

        public void SetStateColor(StateColorApply state, Color color)
        {
            switch (state)
            {
                case StateColorApply.On:
                    OnColor = color;
                    break;
                case StateColorApply.Off:
                    OffColor = color;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        // --------------------------------------------------------------------

        // Derived Properties (Read-only)
        public string OnGlyph => SyncOnColor ? LinkedGlyph : UnlinkedGlyph;
        public string OffGlyph => SyncOffColor ? LinkedGlyph : UnlinkedGlyph;
        public SolidColorBrush OnBrush => MainViewModel.GetBrushFromColor(OnColor); // Use static helper
        public SolidColorBrush OffBrush => MainViewModel.GetBrushFromColor(OffColor); // Use static helper

        // --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            // Check if we are already on the UI thread
            if (_dispatcherQueue.HasThreadAccess)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                // If not, enqueue the invocation onto the UI thread's queue
                _ = _dispatcherQueue.TryEnqueue(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
            }

            // The _notifyParentPropertyChanged delegate is likely unnecessary now,
            // as direct property change notifications are handled correctly.
            // You can probably remove it unless you have specific needs for it.
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}