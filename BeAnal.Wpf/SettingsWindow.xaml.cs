using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BeAnal.Wpf
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {

        private readonly Settings _settings;
        private readonly List<ColorScheme> _colorSchemes;
        
        public SettingsWindow(Settings settings)
        {
            InitializeComponent();

            _settings = settings;
            _settings.PropertyChanged += OnSettingsPropertyChanged;

            // This one line connects your settings directly to the UI.
            // All sliders and color pickers will now update the settings object automatically.
            DataContext = settings;

            // -- Populate Color Schemes -- 
            _colorSchemes = new List<ColorScheme>
            {
                // Custom scheme must be present for the logic to work
                new ColorScheme("Custom", _settings.LowColor, _settings.HighColor, _settings.PeakColor),

                // Curated Presets
                new ColorScheme("Classic Winamp", Colors.LimeGreen, Colors.Yellow, Colors.Red),
                new ColorScheme("Ocean Blue", Colors.DodgerBlue, Colors.Cyan, Colors.White),
                new ColorScheme("Sunset", Colors.OrangeRed, Colors.Yellow, Colors.White),
                new ColorScheme("Synthwave", Colors.Magenta, Colors.Cyan, Colors.Yellow),
                new ColorScheme("Spy Black", Colors.Black, Colors.DarkSlateGray, Colors.Crimson),

                // Additional Ideas
                new ColorScheme("Forest Canopy", Colors.DarkGreen, Colors.Lime, Colors.Yellow),
                new ColorScheme("Molten Core", Colors.DarkRed, Colors.Orange, Colors.White),
                new ColorScheme("Arctic Night", Colors.Indigo, Colors.LightBlue, Colors.White),
                new ColorScheme("Matrix", Colors.Black, Colors.Lime, Colors.WhiteSmoke),
                new ColorScheme("Bubblegum", Colors.DeepPink, Colors.Aqua, Colors.Yellow),
                new ColorScheme("Monochrome", Colors.DimGray, Colors.LightGray, Colors.White),
                new ColorScheme("Vintage VU", Colors.DarkGoldenrod, Colors.Gold, Colors.IndianRed),
                new ColorScheme("Deep Space", Colors.Black, Colors.DarkViolet, Colors.Cyan),
                new ColorScheme("8-Bit Blueberry", Colors.Navy, Colors.RoyalBlue, Colors.White),
                new ColorScheme("Desert Heat", Colors.Maroon, Colors.OrangeRed, Colors.Khaki)
            };

            ColorSchemeComboBox.ItemsSource = _colorSchemes;
            ColorSchemeComboBox.SelectedIndex = 0;
        }

        private void ColorSchemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // A quick safety check. If for some reason nothing is selected,
            //   or if the user selects "Custom" themselves, we don't need to do anything.
            if (ColorSchemeComboBox.SelectedItem is not ColorScheme selectedScheme || selectedScheme.Name == "Custom")
            {
                return;
            }

            // Temporarily unsubscribe from the even to prevent a feedback loop
            _settings.PropertyChanged -= OnSettingsPropertyChanged;


            _settings.LowColor = selectedScheme.LowColor;
            _settings.HighColor = selectedScheme.HighColor;
            _settings.PeakColor = selectedScheme.PeakColor;

            _settings.PropertyChanged += OnSettingsPropertyChanged;

        }
        
        private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // If the color was changed manually via a ColorPicker, 
            //  set the dropdown to "Custom"
            if (e.PropertyName == nameof(Settings.LowColor) ||
                e.PropertyName == nameof(Settings.HighColor) ||
                e.PropertyName == nameof(Settings.PeakColor))
            {
                // unsubscribe temporarily to prevent this change from retrigggering the SelectionChanged event
                ColorSchemeComboBox.SelectionChanged -= ColorSchemeComboBox_SelectionChanged;

                ColorSchemeComboBox.SelectedItem = _colorSchemes.FirstOrDefault(c => c.Name == "Custom");

                // Re-subscribe
                ColorSchemeComboBox.SelectionChanged += ColorSchemeComboBox_SelectionChanged;
            }


        }
    }
}