using System;
using System.ComponentModel;
using System.Collections.Generic;
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

            // This one line connects your settings directly to the UI.
            // All sliders and color pickers will now update the settings object automatically.
            DataContext = settings;

            // -- Populate Color Schemes -- 
            _colorSchemes = new List<ColorScheme>
            {
                new ColorScheme("Classic Winamp", Colors.LimeGreen, Colors.Yellow, Colors.Red),
                new ColorScheme("Ocean Blue", Colors.DodgerBlue, Colors.Cyan, Colors.White),
                new ColorScheme("Sunset", Colors.OrangeRed, Colors.Yellow, Colors.White),
                new ColorScheme("Synthwave", Colors.Magenta, Colors.Cyan, Colors.Yellow),
                new ColorScheme("Custom", _settings.LowColor, _settings.HighColor, _settings.PeakColor)
            };

            ColorSchemeComboBox.ItemsSource = _colorSchemes;
            ColorSchemeComboBox.SelectedIndex = 0;
        }

        private void ColorSchemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorSchemeComboBox.SelectedItem is ColorScheme selectedScheme)
            {
                if (selectedScheme.Name == "Custom") return;

                _settings.LowColor = selectedScheme.LowColor;
                _settings.HighColor = selectedScheme.HighColor;
                _settings.PeakColor = selectedScheme.PeakColor;

                ColorSchemeComboBox.SelectedIndex = 0;           
            }
        }
    }
}