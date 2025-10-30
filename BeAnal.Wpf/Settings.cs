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

        private Color _peakColor = Colors.GhostWhite;

        // Time-based smoothing properties
        private int _barAttackTimeMs = 50;      //Time in Milliseconds
        private int _barReleaseTimeMs = 300;    //Time in Milliseconds
        private int _peakHoldTimeMs = 200;
        private double _peakReleaseTimeMs = 100;

        // Opacity Settings
        private double _backgroundOpacity = 1.0;
        private double _barOpacity = 1.0;

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
        
        public int PeakHoldTimeMs
        {
            get => _peakHoldTimeMs;
            set { if (_peakHoldTimeMs != value) { _peakHoldTimeMs = value; OnPropertyChanged(); } }
        }
        
        public double PeakReleaseTimeMs
        {
            get => _peakReleaseTimeMs;
            set { if (_peakReleaseTimeMs != value) { _peakReleaseTimeMs = value; OnPropertyChanged(); } }
        }
        
        public Color PeakColor
        {
            get => _peakColor;
            set { if (_peakColor != value) { _peakColor = value; OnPropertyChanged(); } }
        }

        public int BarAttackTimeMs
        {
            get => _barAttackTimeMs;
            set { if (_barAttackTimeMs != value) { _barAttackTimeMs = value; OnPropertyChanged(); } }
        }

        public int BarReleaseTimeMs
        {
            get => _barReleaseTimeMs;
            set { if (_barReleaseTimeMs != value) { _barReleaseTimeMs = value; OnPropertyChanged(); } }
        }

        public double BackgroundOpacity
        {
            get => _backgroundOpacity;
            set { if (_backgroundOpacity != value) { _backgroundOpacity = value; OnPropertyChanged(); } }
        }

        public double BarOpacity
        {
            get => _barOpacity;
            set { if (_barOpacity != value) { _barOpacity = value; OnPropertyChanged(); } }
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