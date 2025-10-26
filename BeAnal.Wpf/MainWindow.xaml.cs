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
        private int[] _barFFTBinMap;
        private readonly Settings _settings;

        public MainWindow()
        {
            InitializeComponent();

            _settings = new Settings();

            // Hook into the window's lifecycle events
            this.Loaded += OnWindowLoaded;
            this.Closing += OnWindowClosing;

            //Making the bordless window dragable
            this.MouseLeftButtonDown += (s, e) => DragMove();

            //Create and prepare the audio engine
            _audioProcessor = new AudioProcessor(_settings.Sensitivity);
            _audioProcessor.FFTDataAvailable += OnFFTDataAvailable;

            //Create the Visual Bar objects
            _barRectangles = new Rectangle[_settings.NumberOfBars];
            _barFFTBinMap = new int[_settings.NumberOfBars];

            this.Topmost = _settings.IsTopMost;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {

            CalculateLogarithmicMapping();
            CreateVisualBars();
            _audioProcessor.Start();
        }

        private void CreateVisualBars()
        {
            double barWidth = SpectrumCanvas.ActualWidth / _settings.NumberOfBars;

            for (int i = 0; i < _settings.NumberOfBars; i++)
            {
                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = 0,
                    Fill = CreateGradientBrush()
                };
                Canvas.SetLeft(rect, i * barWidth);
                Canvas.SetBottom(rect, 0); // Anchor the bars to the bottom
                _barRectangles[i] = rect;
                SpectrumCanvas.Children.Add(rect);
            }
        }

        private void CalculateLogarithmicMapping()
        {
            // --- Calculate logarithmic FFT Bin Mapping ---
            double maxFrequency = 22050; // Max Frequencty to display (half of 44.1KHz sample rate)
            double minFrequency = 20;

            // Calculate the number of octaves
            double octaves = Math.Log(maxFrequency / minFrequency, 2);
            double binsPerOctave = _settings.NumberOfBars / octaves;

            // Determine the frequency of the first FFT bin we care about
            double firstBinFreq = (48000.0 / AudioProcessor.FFTSize);

            // --- Fix: Ensure unique and increasing bin mapping ---
            int lastBinIndex = 0;

            for (int i = 0; i < _settings.NumberOfBars; i++)
            {
                double barNum = i + 1;
                double octave = (barNum / binsPerOctave) - (1 / binsPerOctave);
                double freq = minFrequency * Math.Pow(2, octave);

                int currentBinIndex = (int)(freq / firstBinFreq);
                // Prevent clumping by ensuring each bar maps to a new bin
                if (currentBinIndex <= lastBinIndex)
                {
                    currentBinIndex = lastBinIndex + 1;
                }

                // Don't go past the end of the FFT data
                if (currentBinIndex >= AudioProcessor.FFTSize / 2)
                {
                    currentBinIndex = AudioProcessor.FFTSize / 2 - 1;
                }

                _barFFTBinMap[i] = currentBinIndex;
                lastBinIndex = currentBinIndex;
            }
        }

        // This method is called by the AudioProcessor's event
        private void OnFFTDataAvailable(double[] FFTData)
        {
            //Use the dispatcher to update the UI from the audio thread
            Dispatcher.BeginInvoke(() =>
            {
                for (int i = 0; i < _settings.NumberOfBars; i++)
                {
                    // Map the FFT datda to the bars
                    int FFTBinIndex = _barFFTBinMap[i];

                    //Ensure the index is within the bounds of the FFT data array
                    if (FFTBinIndex >= FFTData.Length)
                    {
                        FFTBinIndex = FFTData.Length - 1;
                    }


                    double magnitude = FFTData[FFTBinIndex];

                    // Scale the height and update the rectangle
                    double barHeight = (magnitude / 100.0) * SpectrumCanvas.ActualHeight;
                    _barRectangles[i].Height = Math.Min(barHeight, SpectrumCanvas.ActualHeight);
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
            //When settings change, we need to rebuild the visualizer
            ReinitializeVisualizer();
        }

        private void ReinitializeVisualizer()
        {
            // 1. Clear the old bars from the canvas
            SpectrumCanvas.Children.Clear();

            // 2. Resize our arrays to match the new settings
            Array.Resize(ref _barRectangles, _settings.NumberOfBars);
            Array.Resize(ref _barFFTBinMap, _settings.NumberOfBars);

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

    }
}