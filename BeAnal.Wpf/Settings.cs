using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Media;

namespace BeAnal.Wpf
{
    public class Settings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int _numberOfBars { get; set; } = 64;
        private double _sensitivity { get; set; } = 20.0;
        public Color LowColor { get; set; } = Colors.Green;
        public Color HighColor { get; set; } = Colors.Red;
        public bool IsAlwaysOnTop { get; set; } = true;


        public int NumberOfBars
        {
            get => _numberOfBars;
            set
            {
                if (_numberOfBars != value)
                {
                    _numberOfBars = value;
                    OnPropertyChanged();
                }

            }

        }

        public double Sensitivity
        {
            get => _sensitivity;
            set
            {
                if (_sensitivity != value)
                {
                    _sensitivity = value;
                    OnPropertyChanged();
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}