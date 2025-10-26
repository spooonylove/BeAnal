using System.Windows;

namespace BeAnal.Wpf
{
    public partial class SettingsWindow : Window
    {
        // The Settings object shared with MainWindow
        private readonly Settings _settings;

        // An event to notify MainWindow that settings have changed
        public event Action? SettingsChanged;

        public SettingsWindow(Settings settings)
        {
            //MessageBox.Show("1. Constructor Started"); // <-- LED #1
            InitializeComponent();
            //MessageBox.Show("2. InitializeComponent Finished"); // <-- LED #2

            _settings = settings;

            // Hook into this window's Loaded event
            this.Loaded += OnSettingsWindowLoaded;
        }
        
        private void OnSettingsWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Set the slider's initial value here, AFTER the window is fully loaded
            // and after MainWindow has subscribed to our event.
            BarsSlider.Value = _settings.NumberOfBars;

            //Set the initial text value
            BarsValueText.Text = _settings.NumberOfBars.ToString();

            //Set up Sensitivity Sliders, yo
            SensitivitySlider.Value = _settings.Sensitivity;
            SensitivityValueText.Text = _settings.Sensitivity.ToString("F0");
        }
        private void BarsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // this check prevents the event from firing during initialization
            if (!this.IsLoaded) return;

            int newValue = (int)e.NewValue;

            // Update the shared settings object
            _settings.NumberOfBars = newValue;

            //Update the text block with the new value
            BarsValueText.Text = newValue.ToString();
            // Raise the event to notify MainWindow
            SettingsChanged?.Invoke();
        }

        private void SensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded) return;

            double newValue = e.NewValue;
            _settings.Sensitivity = newValue;
            SensitivityValueText.Text = newValue.ToString("F0");

            SettingsChanged?.Invoke();
        }
    }
}