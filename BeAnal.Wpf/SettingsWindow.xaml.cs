using System;
using System.ComponentModel;
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
        public SettingsWindow(Settings settings)
        {
            InitializeComponent();
            
            // This one line connects your settings directly to the UI.
            // All sliders and color pickers will now update the settings object automatically.
            DataContext = settings;
        }
    }
}