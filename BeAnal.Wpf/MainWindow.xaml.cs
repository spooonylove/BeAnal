using System;
using System.Windows;           // Required for WPF classes like windows
using System.Windows.Shapes;    //Required for Rectangle
using System.Windows.Media;     // Required for Brushes
using System.Windows.Controls;
using System.ComponentModel;
using Accessibility;
using NAudio.Wave;
using System.Windows.Automation.Peers;


namespace BeAnal.Wpf
{
    public partial class MainWindow : Window
    {

        // --  Fields --
        private readonly Settings _settings;
        private readonly AudioProcessor _audioProcessor;
        private Rectangle[] _barRectangles;
        private Rectangle[] _peakRectangles;
        private readonly object _visualizerLock = new object();
        
        public MainWindow()
        {
            InitializeComponent();

            _settings = SettingsService.LoadSettings();
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

            // Initialize the rectangle arrays to empty to satisfy non-nullable rqeuirement.
            // The will be populated later in the 'Loaded' event.
            _barRectangles = Array.Empty<Rectangle>();
            _peakRectangles = Array.Empty<Rectangle>();
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
            //Snatch the window placement/size information before closing so we can remember for next time.
            if (this.WindowState == WindowState.Normal)
            {
                _settings.WindowHeight = this.Height;
                _settings.WindowWidth = this.Width;
                _settings.WindowTop = this.Top;
                _settings.WindowLeft = this.Left;
            }
            _settings.WindowState = this.WindowState;

            // Save current settings to file
            SettingsService.SaveSettings(_settings);

            // Cleaning shut down the audio engine
            _audioProcessor.Dispose();
        }

        private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            // This event handler is the central point for reacting to settings changes.
            // Using a switch ensure that we only update  whats necessary
            Dispatcher.Invoke(() =>
            {
                switch (e.PropertyName)
                {
                    //these properties require a full rebuild of the visual elements
                    case nameof(Settings.NumberOfBars):
                    case nameof(Settings.LowColor):
                    case nameof(Settings.HighColor):
                    case nameof(Settings.PeakColor):
                        // This is an expensive operation, so only do it when you need to!
                        lock (_visualizerLock)
                        {
                            UpdateVisualizerLayout();    
                        }
                        break;
                    case nameof(Settings.IsAlwaysOnTop):
                        UpdateWindowSettings();
                        break;
                    case nameof(Settings.BackgroundOpacity):
                        CanvasBackgroundBrush.Opacity = _settings.BackgroundOpacity;
                        break;
                    case nameof(Settings.BarOpacity):
                        lock (_visualizerLock)
                        {
                            //update all  existing bars with the new opacity
                            for (int i = 0; i < _barRectangles.Length; i++)
                            {
                                _barRectangles[i].Opacity = _settings.BarOpacity;
                                _peakRectangles[i].Opacity = _settings.BarOpacity;
                            }
                        }
                        break;
                }
            });
        }
        
        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            lock (_visualizerLock)
            {
                // A resize doesn't require rebuilding the bars, only repositioning them.
                // This is to safe call even during initiatization because of the null checks within
                UpdateBarPositionsAndWidths();
            }
        }
        private void OnProcessedDataAvailable(VisualizerData data)
        {
            
            Dispatcher.BeginInvoke(() =>
            {

                lock (_visualizerLock)
                {
                    // CHANGE: Modified the loop condition to be more robust.
                    // Instead of an exact length check, this now iterates through the minimum of the two collections.
                    // This prevents an IndexOutOfRangeException and stops the visual freeze if the arrays are
                    // temporarily out of sync during a rapid slider change.

                    int barsToRender = Math.Min(_barRectangles.Length, data.BarHeights.Length);

                   
                    for (int i = 0; i < barsToRender; i++)
                    {
                        // Appply the final, pre-calculated bar height. no Math! FAST
                        double barHeight = (data.BarHeights[i] / 100.0) * SpectrumCanvas.ActualHeight;
                        _barRectangles[i].Height = Math.Max(0, barHeight);

                        double peakPosition = (data.PeakLevels[i] / 100.0) * SpectrumCanvas.ActualHeight;
                        // Ensure that the peak indicator is at least its own height from the top
                        double finalPeakPosition = Math.Max(0, peakPosition - _peakRectangles[i].Height);
                        Canvas.SetBottom(_peakRectangles[i], finalPeakPosition);
                    }
                }
            });
        }

        /// <summary>
        ///  This code must be called from a lock
        /// </summary>
        private void UpdateVisualizerLayout()
        {
            int newNumberOfBars = _settings.NumberOfBars;
            int oldNumberOfBars = _barRectangles.Length;

            // If we needc more bars that we currently have
            if (newNumberOfBars > oldNumberOfBars)
            {
                // Resize the arrays to the new size
                Array.Resize(ref _barRectangles, newNumberOfBars);
                Array.Resize(ref _peakRectangles, newNumberOfBars);

                // Create and add only the new rectangles
                for (int i = oldNumberOfBars; i < newNumberOfBars; i++)
                {
                    var barRect = new Rectangle
                    {
                        Fill = CreateGradientBrush(),
                        Opacity = _settings.BarOpacity
                    };

                    Canvas.SetBottom(barRect, 0);
                    _barRectangles[i] = barRect;
                    SpectrumCanvas.Children.Add(barRect);

                    var peakRect = new Rectangle
                    {
                        Height = 2,  // Peak Indicator height
                        Fill = new SolidColorBrush(_settings.PeakColor),
                        Opacity = _settings.BarOpacity
                    };
                    _peakRectangles[i] = peakRect;
                    SpectrumCanvas.Children.Add(peakRect);

                }
            }
            else if (newNumberOfBars < oldNumberOfBars)
            {
                //Remove the excess rectanges from the canvas
                for (int i = oldNumberOfBars - 1; i >= newNumberOfBars; i--)
                {
                    SpectrumCanvas.Children.Remove(_barRectangles[i]);
                    SpectrumCanvas.Children.Remove(_peakRectangles[i]);
                }
                // Resize the arrays down to the new size
                Array.Resize(ref _barRectangles, newNumberOfBars);
                Array.Resize(ref _peakRectangles, newNumberOfBars);
            }

            // Update the colors for all the existing bars, in case the theme changed
            for (int i = 0; i < newNumberOfBars; i++)
            {
                _barRectangles[i].Fill = CreateGradientBrush();
                _barRectangles[i].Opacity = _settings.BarOpacity;
                _peakRectangles[i].Fill = new SolidColorBrush(_settings.PeakColor);
                _peakRectangles[i].Opacity = _settings.BarOpacity;

            }

            // After creating/removing bars, update their positions and widths
            UpdateBarPositionsAndWidths();

        }

        /// <summary>
        /// This code must be called from a lock
        /// </summary>
        private void UpdateBarPositionsAndWidths()
        {
            // This method is the single source of truth for bar layout
            // don't do anything if the bars haven't been created yet or the canvas isn't ready
            if (_barRectangles is null || _barRectangles.Length == 0 || SpectrumCanvas.ActualWidth == 0) return;

            // Defin the spacing you want between the bars
            double barSpacing = 2.0;

            // Calculate the total width available for each bar slot
            double totalSlotWidth = SpectrumCanvas.ActualWidth / _barRectangles.Length;

            // The actual width of the bar is the slot width minus the spacing
            //      Ensure itsr not less than zero
            double barwidth = Math.Max(0, totalSlotWidth - barSpacing);

            for (int i = 0; i < _barRectangles.Length; i++)
            {
                // Position each bar at the start of its slot. the empty space will be created by the 
                //  reduced width
                _barRectangles[i].Width = barwidth;
                Canvas.SetLeft(_barRectangles[i], i * totalSlotWidth);

                _peakRectangles[i].Width = barwidth;
                Canvas.SetLeft(_peakRectangles[i], i * totalSlotWidth);
            }
        }

        private void UpdateWindowSettings()
        {
            // UI will sit on top (or not) based on the setting
            this.Topmost = _settings.IsAlwaysOnTop;

            // apply any new windows position and size information
            this.Height = _settings.WindowHeight;
            this.Width = _settings.WindowWidth;
            this.Top = _settings.WindowTop;
            this.Left = _settings.WindowLeft;
            this.WindowState = _settings.WindowState;

            CanvasBackgroundBrush.Opacity = _settings.BackgroundOpacity;
        }

        private Brush CreateGradientBrush()
        {
            return new LinearGradientBrush(_settings.HighColor, _settings.LowColor, 90);
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