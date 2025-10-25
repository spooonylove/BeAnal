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
        private readonly Rectangle[] _barRectangles;
        private const int NumberOfBars = 64;

        public MainWindow()
        {
            InitializeComponent();

            // Hook into the window's lifecycle events
            this.Loaded += OnWindowLoaded;
            this.Closing += OnWindowClosing;

            //Making the bordless window dragable
            this.MouseLeftButtonDown += (s, e) => DragMove();

            //Create and prepare the audio engine
            _audioProcessor = new AudioProcessor();
            _audioProcessor.FFTDataAvailable += OnFFTDataAvailable;

            //Create the Visual Bar objects
            _barRectangles = new Rectangle[NumberOfBars];
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // -- Create the visual bars -- 
            double barWidth = SpectrumCanvas.ActualWidth / NumberOfBars;

            for (int i = 0; i < NumberOfBars; i++)
            {
                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = 0,
                    Fill = Brushes.LawnGreen
                };
                Canvas.SetLeft(rect, i * barWidth);
                Canvas.SetBottom(rect, 0); // Anchor the bars to the bottom
                _barRectangles[i] = rect;
                SpectrumCanvas.Children.Add(rect);
            }
            // -- End of Bar Creation -- 

            // Start audio capture when the window is loaded
            _audioProcessor.Start();
        }

        // This method is called by the AudioProcessor's event
        private void OnFFTDataAvailable(double[] FFTData)
        {
            //Use the dispatcher to update the UI from the audio thread
            Dispatcher.BeginInvoke(() =>
            {
                for (int i = 0; i < NumberOfBars; i++)
                {
                    // Map the FFT datda to the bars
                    int FFTBinIndex = i * (FFTData.Length / NumberOfBars);
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




    }
}