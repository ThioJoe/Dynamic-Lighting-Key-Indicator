using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI; // Assuming Color is from here or Microsoft.UI

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

        // Derived Properties (Read-only)
        public string OnGlyph => SyncOnColor ? LinkedGlyph : UnlinkedGlyph;
        public string OffGlyph => SyncOffColor ? LinkedGlyph : UnlinkedGlyph;
        public SolidColorBrush OnBrush => MainViewModel.GetBrushFromColor(OnColor); // Use static helper
        public SolidColorBrush OffBrush => MainViewModel.GetBrushFromColor(OffColor); // Use static helper

        // --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            // Use the parent's dispatcher queue if needed for thread safety,
            // assuming this class might be updated from background threads.
            // If updates always happen on the UI thread, direct invoke is fine.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // Also notify the parent ViewModel that a property *within* this object changed,
            // which might be necessary if the parent binds to the entire KeyIndicatorState object
            // or uses converters that depend on multiple internal properties.
            // This might not always be needed, depending on exact binding setup.
            // Example: _notifyParentPropertyChanged($"KeyStates[{_associatedKey}].{propertyName}"); // Requires knowing the key
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