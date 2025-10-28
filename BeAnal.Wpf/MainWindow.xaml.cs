using System;
using System.Windows;           // Required for WPF classes like windows
using System.Windows.Shapes;    //Required for Rectangle
using System.Windows.Media;     // Required for Brushes
using System.Windows.Controls;
using System.ComponentModel;
using Accessibility;


namespace BeAnal.Wpf
{
    public partial class MainWindow : Window
    {

        // --  Fields --
        private readonly Settings _settings;
        private readonly AudioProcessor _audioProcessor;
        private Rectangle[] _barRectangles;
        
        public MainWindow()
        {
            InitializeComponent();

            _settings = new Settings();
            // Create and prepare the audio engine
            _audioProcessor = new AudioProcessor(_settings);
            

            // Hook into events
            _settings.PropertyChanged += OnSettingsChanged;
            _audioProcessor.ProcessedDataAvailable += OnProcessedDataAvailable;
            this.Loaded += OnWindowLoaded;
            this.Closing += OnWindowClosing;
            this.SizeChanged += OnWindowSizeChanged;

            // Making the bordless window dragable
            this.MouseLeftButtonDown += (s, e) => DragMove();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Perform intial setup nce the window is fully loaded
            UpdateVisualizerLayout();
            UpdateWindowSettings();

            _audioProcessor.Start();
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cleaning shut down the audio engine
            _audioProcessor.Dispose();
        }

        private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            // THis event handler is the central point for reacting to settings changes.
            // Using a switch ensure that we only update  whats necessary
            Dispatcher.Invoke(() =>
            {
                switch (e.PropertyName)
                {
                    //these properties require a full rebuild of the visual elements
                    case nameof(Settings.NumberOfBars):
                    case nameof(Settings.LowColor):
                    case nameof(Settings.HighColor):
                        // This is an expensive operation, so only do it when you need to!
                        UpdateVisualizerLayout();
                        break;
                    case nameof(Settings.IsAlwaysOnTop):
                        UpdateWindowSettings();
                        break;
                }
            });
        }
        
        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // A resize doesn't require rebuilding the bars, only repositioning them.
            // This is to safe call even during initiatization because of the null checks within
            UpdateBarPositionsAndWidths();
        }
        private void OnProcessedDataAvailable(double[] barHeights)
        {
            Dispatcher.BeginInvoke(() =>
            {
                // Safety check: if settings changed and the bar array is out of sync, abort the frame
                if (_barRectangles.Length != barHeights.Length) return;

                for (int i = 0; i < barHeights.Length; i++)
                {
                    // Appply the final, pre-calculated height. no Math! FAST
                    double finalHeight = (barHeights[i] / 100.0) * SpectrumCanvas.ActualHeight;
                    _barRectangles[i].Height = finalHeight;
                }
            });
        }
                
        private void UpdateVisualizerLayout()
        {
            // 1. Clear the old bars from the canvas
            SpectrumCanvas.Children.Clear();

            // 2. Resize our array to match the new settings
            Array.Resize(ref _barRectangles, _settings.NumberOfBars);

            // 3. Re-run the setup logic with the new settings
            for (int i = 0; i < _settings.NumberOfBars; i++)
            {
                var rect = new Rectangle
                {
                    Height = 0,
                    Fill = CreateGradientBrush()
                };
                Canvas.SetBottom(rect, 0); // Anchor the bars to the bottom
                _barRectangles[i] = rect;
                SpectrumCanvas.Children.Add(rect);
            }

            // --- DIAGNOSTIC: Color the last bar blue ---
            if (_barRectangles.Length > 0)
            {
                _barRectangles[_settings.NumberOfBars - 1].Fill = Brushes.Blue;
            }
            // --- END DIAGNOSTIC --- 

            // After creating the bars, update their positions and widths
            UpdateBarPositionsAndWidths();
        }

        private void UpdateBarPositionsAndWidths()
        {
            // This method is the single source of truth for bar layout
            // don't do anything if the bars haven't been created yet or the canvas isn't ready
            if (_barRectangles is null || _barRectangles.Length == 0 ||SpectrumCanvas.ActualWidth == 0)
            {
                return;
            }

            double barwidth = SpectrumCanvas.ActualWidth / _settings.NumberOfBars;

            for (int i = 0; i < _settings.NumberOfBars; i++)
            {
                _barRectangles[i].Width = barwidth;
                Canvas.SetLeft(_barRectangles[i], i * barwidth);
            }
        }

        private void UpdateWindowSettings()
        {
            // UI will sit on top (or not) based on the setting
            this.Topmost = _settings.IsAlwaysOnTop;
        }

        private Brush CreateGradientBrush()
        {
            return new LinearGradientBrush(_settings.LowColor, _settings.HighColor, 90);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_settings);
            
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();

        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }        

    }
}