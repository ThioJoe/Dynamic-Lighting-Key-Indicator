using System.ComponentModel;

namespace Dynamic_Lighting_Key_Indicator
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _deviceStatusMessage;
        public string DeviceStatusMessage
        {
            get { return _deviceStatusMessage; }
            set
            {
                if (_deviceStatusMessage != value)
                {
                    _deviceStatusMessage = value;
                    OnPropertyChanged(nameof(DeviceStatusMessage));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
