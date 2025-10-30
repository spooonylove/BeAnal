using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace BeAnal.Wpf
{
    public class Settings : INotifyPropertyChanged
    {
        private int _numberOfBars = 64;
        private double _sensitivity = 1.0;
        private bool _isAlwaysOnTop = true;
        private Color _lowColor = Colors.Yellow;
        private Color _highColor = Colors.Tomato;
        private int _peakHoldTime = 10;
        private double _peakDecayRate = 0.2;
        private Color _peakColor = Colors.GhostWhite;

        // Time-based smoothing properties
        private int _barAttackTime = 50;  //Time in Milliseconds
        private int _barReleaseTime = 300; //Time in Milliseconds

        //Window Properties
        private double _windowHeight = 450;
        private double _windowWidth = 800;
        private double _windowTop = 100;
        private double _windowLeft = 100;
        private WindowState _windowState = WindowState.Normal;

        public int NumberOfBars
        {
            get => _numberOfBars;
            set { if (_numberOfBars != value) { _numberOfBars = value; OnPropertyChanged(); } }
        }
        
        public double Sensitivity
        {
            get => _sensitivity;
            set { if (_sensitivity != value) { _sensitivity = value; OnPropertyChanged(); } }
        }

        public bool IsAlwaysOnTop
        {
            get => _isAlwaysOnTop;
            set { if (_isAlwaysOnTop != value) { _isAlwaysOnTop = value; OnPropertyChanged(); } }
        }

        public Color LowColor
        {
            get => _lowColor;
            set { if (_lowColor != value) { _lowColor = value; OnPropertyChanged(); } }
        }

        public Color HighColor
        {
            get => _highColor;
            set { if (_highColor != value) { _highColor = value; OnPropertyChanged(); } }
        }
        
        public int PeakHoldTime
        {
            get => _peakHoldTime;
            set { if (_peakHoldTime != value) { _peakHoldTime = value; OnPropertyChanged(); } }
        }
        
        public double PeakDecayRate
        {
            get => _peakDecayRate;
            set { if (_peakDecayRate != value) { _peakDecayRate = value; OnPropertyChanged(); } }
        }
        
        public Color PeakColor
        {
            get => _peakColor;
            set { if (_peakColor != value) { _peakColor = value; OnPropertyChanged(); } }
        }

        public int BarAttackTime
        {
            get => _barAttackTime;
            set { if (_barAttackTime != value) { _barAttackTime = value; OnPropertyChanged(); } }
        }

        public int BarReleaseTime
        {
            get => _barReleaseTime;
            set { if (_barReleaseTime != value) { _barReleaseTime = value; OnPropertyChanged(); } }
        }

        // --- Windows Property Accessors
        public double WindowHeight
        {
            get => _windowHeight;
            set { if (_windowHeight != value) { _windowHeight = value; OnPropertyChanged(); } }
        }

        public double WindowWidth
        {
            get => _windowWidth;
            set { if (_windowWidth != value) { _windowWidth = value; OnPropertyChanged(); } }
        }

        public double WindowTop
        {
            get => _windowTop;
            set { if (_windowTop != value) { _windowTop = value; OnPropertyChanged(); } }
        }

        public double WindowLeft
        {
            get => _windowLeft;
            set { if (_windowLeft != value) { _windowLeft = value; OnPropertyChanged(); } }
        }
        
        public WindowState WindowState
        {
            get => _windowState;
            set { if (_windowState != value) { _windowState = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}