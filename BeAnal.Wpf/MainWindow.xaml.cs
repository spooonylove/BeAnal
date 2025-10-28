using System;
using System.Windows;           // Required for WPF classes like windows
using System.Windows.Shapes;    //Required for Rectangle
using System.Windows.Media;     // Required for Brushes
using System.Windows.Controls;
using System.Formats.Asn1;
using NAudio.Wave.SampleProviders;


namespace BeAnal.Wpf
{
    public partial class MainWindow : Window
    {

        // --  Fields --
        private readonly Settings _settings;
        private readonly AudioProcessor _audioProcessor;
        private Rectangle[] _barRectangles;
        private bool _isWindowLoaded = false;


        public MainWindow()
        {
            InitializeComponent();

            _settings = new Settings();
            // Create and prepare the audio engine
            _audioProcessor = new AudioProcessor(_settings);
            _audioProcessor.ProcessedDataAvailable += OnProcessedDataAvailable;      

            // Create the Visual Bar objects
            _barRectangles = new Rectangle[_settings.NumberOfBars];      

            // Hook into the window's lifecycle events
            this.Loaded += OnWindowLoaded;
            this.Closing += OnWindowClosing;

            // Making the bordless window dragable
            this.MouseLeftButtonDown += (s, e) => DragMove();

            // Refresh the visualization when resized
            this.SizeChanged += OnWindowSizeChanged;

            // UI will sit on top
            this.Topmost = _settings.IsAlwaysOnTop;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            RebuildVisualizer();
            _audioProcessor.Start();
            _isWindowLoaded = true;
        }
        private void OnProcessedDataAvailable(double[] barHeights)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_barRectangles.Length != barHeights.Length) return;

                for (int i = 0; i < barHeights.Length; i++)
                {
                    // Appply the final, pre-calculated height. no Math! FAST
                    double finalHeight = (barHeights[i] / 100.0) * SpectrumCanvas.ActualHeight;
                    _barRectangles[i].Height = finalHeight;
                }
            });
        }
        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isWindowLoaded) return;

            UpdateBarLayout();
        }
        private void OnSettingsChanged()
        {
            this.Topmost = _settings.IsAlwaysOnTop;

            // This is an expensive operation, so we only do it if the bar count has changed
            if (_barRectangles.Length != _settings.NumberOfBars)
            {
                RebuildVisualizer();
            }
        }
        
        private void RebuildVisualizer()
        {
            // 1. Clear the old bars from the canvas
            SpectrumCanvas.Children.Clear();

            // 2. Resize our array to match the new settings
            Array.Resize(ref _barRectangles, _settings.NumberOfBars);

            // 3. Re-run the setup logic with the new settings
            CreateBars();
        }
        private void CreateBars()
        {
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

            UpdateBarLayout();

            // --- DIAGNOSTIC: Color the last bar blue ---
            if (_barRectangles.Length > 0)
            {
                _barRectangles[_settings.NumberOfBars - 1].Fill = Brushes.Blue;
            }
            // --- END DIAGNOSTIC --- 
        }

        private void UpdateBarLayout()
        {
            // This method is the single source of truth for bar layout
            // don't do anything if the bars haven't been created yet
            if (_barRectangles is null || _barRectangles.Length == 0 || _barRectangles[0] is null || SpectrumCanvas.ActualWidth == 0)
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

        private Brush CreateGradientBrush()
        {
            return new LinearGradientBrush(_settings.LowColor, _settings.HighColor, 90);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_settings);
            settingsWindow.SettingsChanged += OnSettingsChanged;
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();

        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }        
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cleaning shut down the audio engine
            _audioProcessor.Dispose();
        }

    }
}