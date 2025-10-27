using System;
using System.Windows;           // Required for WPF classes like windows
using System.Windows.Shapes;    //Required for Rectangle
using System.Windows.Media;     // Required for Brushes
using System.Windows.Controls;


namespace BeAnal.Wpf
{
    public partial class MainWindow : Window
    {

        // --  Fields --
        private readonly AudioProcessor _audioProcessor;
        private Rectangle[] _barRectangles;
        private double[] _lastBarHeights;
        private (int Start, int End)[] _barFFTBinMap;
        private readonly Settings _settings;
        private bool _isWindowLoaded = false;


        public MainWindow()
        {
            InitializeComponent();

            _settings = new Settings();

            // Hook into the window's lifecycle events
            this.Loaded += OnWindowLoaded;
            this.Closing += OnWindowClosing;

            //Making the bordless window dragable
            this.MouseLeftButtonDown += (s, e) => DragMove();

            //Refresh the visualization when resized
            this.SizeChanged += OnWindowSizeChanged;

            //Create and prepare the audio engine
            _audioProcessor = new AudioProcessor(_settings.Sensitivity);
            _audioProcessor.FFTDataAvailable += OnFFTDataAvailable;

            //Create the Visual Bar objects
            _barRectangles = new Rectangle[_settings.NumberOfBars];
            _barFFTBinMap = new (int Start, int End)[_settings.NumberOfBars];
            _lastBarHeights = new double[_settings.NumberOfBars];

            this.Topmost = _settings.IsTopMost;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {

            CalculateLogarithmicMapping();
            CreateVisualBars();
            _audioProcessor.Start();

            _isWindowLoaded = true;
        }

        private void CreateVisualBars()
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
        }

        private void CalculateLogarithmicMapping()
        {
            //Redefine the map with the new type
            _barFFTBinMap = new (int Start, int End)[_settings.NumberOfBars];

            double maxFrequency = 20000; //Capped at a realistic 20KHz
            double minFrequency = 20;
            int maxFFTIndex = AudioProcessor.FFTSize / 2 - 1;
            double freqResolution = 48000 / AudioProcessor.FFTSize;

            for (int i=0; i < _settings.NumberOfBars; i++)
            {
                //Calculate the start and end frequencies for this bar on a log scale
                double freqStart = minFrequency * Math.Pow(maxFrequency / minFrequency, (double)i / _settings.NumberOfBars);
                double freqEnd = minFrequency * Math.Pow(maxFrequency / minFrequency, (double)(i + 1) / _settings.NumberOfBars);

                //Convert those frequencies to FFT bin indices
                int binStartIndex = (int)(freqStart / freqResolution);
                int binEndIndex = (int)(freqEnd / freqResolution);

                // Clamp the values to the valid range
                if (binStartIndex > maxFFTIndex) binStartIndex = maxFFTIndex;
                if (binEndIndex > maxFFTIndex) binEndIndex = maxFFTIndex;

                // Ensure we are always looking at least one bin
                if (binEndIndex <= binStartIndex) binEndIndex = binStartIndex + 1;
                if (binEndIndex > maxFFTIndex) binEndIndex = maxFFTIndex;

                _barFFTBinMap[i] = (binStartIndex, binEndIndex);
            }
           
        }

        // This method is called by the AudioProcessor's event
        private void OnFFTDataAvailable(double[] FFTData)
        {
            

            //Use the dispatcher to update the UI from the audio thread
            Dispatcher.BeginInvoke(() =>
            {
                // Attack Release settings for smoothing function
                double attack = 0.4;
                double release = 0.1;

                for (int i = 0; i < _settings.NumberOfBars; i++)
                {
                    // 1. Get the start and end bins for this bar from our map
                    var (startBin, endBin) = _barFFTBinMap[i];

                    // 2. Find the peak magnitude within that range of bins
                    double peakMagnitude = 0;

                    for (int j = startBin; j < endBin; j++)
                    {
                        if (j < FFTData.Length && FFTData[j] > peakMagnitude)
                        {
                            peakMagnitude = FFTData[j];
                        }
                    }

                    // 3. Use that peak fro the smoothing and drawing logic
                    double targetHeight = (peakMagnitude / 100.0) * SpectrumCanvas.ActualHeight;

                    //Smothing functions, yo!
                    double lastHeight = _lastBarHeights[i];
                    double newHeight;
                    if (targetHeight > lastHeight)
                    {
                        newHeight = (targetHeight * attack) + (lastHeight * (1 - attack));
                    }
                    else
                    {
                        newHeight = (targetHeight * release) + (lastHeight * (1 - release));
                    }

                    _barRectangles[i].Height = Math.Min(newHeight, SpectrumCanvas.ActualHeight);
                    _lastBarHeights[i] = newHeight;
                }

            });

        }
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cleaning shut down the audio engine
            _audioProcessor.Dispose();
        }

        private Brush CreateGradientBrush()
        {
            //define the colors for the gradient.
            // these could come from a seeting object in the future
            Color lowColor = Colors.Green;
            Color highColor = Colors.Red;

            //Create the gradient brush
            return new LinearGradientBrush(lowColor, highColor, 90);
        }

        private void OnSettingsChanged()
        {
            // This is a cheap operation, lets do it each time
            _audioProcessor.Sensitivity = _settings.Sensitivity;

            // THis is an expensive operation, so we only do it if the bar count has changed
            if (_barRectangles.Length != _settings.NumberOfBars)
            {
                ReinitializeVisualizer();
            }
        }

        private void ReinitializeVisualizer()
        {
            // 1. Clear the old bars from the canvas
            SpectrumCanvas.Children.Clear();

            // 2. Resize our arrays to match the new settings
            Array.Resize(ref _barRectangles, _settings.NumberOfBars);
            Array.Resize(ref _barFFTBinMap, _settings.NumberOfBars);
            Array.Resize(ref _lastBarHeights, _settings.NumberOfBars);

            // 3. Re-run the setup logic with the new settings
            CalculateLogarithmicMapping();
            CreateVisualBars();
        }
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var settingsWindow = new SettingsWindow(_settings);
                settingsWindow.SettingsChanged += OnSettingsChanged;

                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();
            }));
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
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

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isWindowLoaded) return;
            
            UpdateBarLayout();
        }
        

    }
}