using NAudio.Wave;
using NAudio.Dsp;
using System;
using System.Windows;           // Required for WPF classes like windows
using System.Windows.Shapes;    //Required for Rectangle
using System.Windows.Media;     // Required for Brushes
using System.Windows.Controls;
using System.Text.RegularExpressions;   //Requires for Canvas

namespace SpectrumUI
{
    public partial class MainWindow : Window, IDisposable
    {
        // -- Audio Processing Fields -- 
        private const int FFTSize = 1024;
        private int FFTIndex = 0;
        private Complex[] FFTBuffer = new Complex[FFTSize];
        private WasapiLoopbackCapture? capture;

        // -- Visualization Fields --
        private Rectangle[]? barRectangles;
        private const int NumberOfBars = 64;

        public MainWindow()
        {
            InitializeComponent();

            // Hook into the window's lifecycle events
            this.Loaded += OnWindowLoaded;
            this.Closing += OnWindowClosing;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // -- Create the visual bars -- 
            barRectangles = new Rectangle[NumberOfBars];
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
                barRectangles[i] = rect;
                SpectrumCanvas.Children.Add(rect);
            }
            // -- End of Bar Creation -- 

            // Start audio capture when the window is loaded
            capture = new WasapiLoopbackCapture();
            capture.DataAvailable += OnDataAvailable;
            capture.StartRecording();
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // stop and dispose of the capture object when the window closes
            Dispose();
        }

        // This is our audio processing method
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            //The incoming buffer is raw bytes, we need to convert it to samples
            var buffer = new WaveBuffer(e.Buffer);

            //Process samples in pairs (since its stereo, 32-bit float)
            for (int i = 0; i < e.BytesRecorded / 4; i += 2)
            {
                if (FFTIndex >= FFTSize) break;

                //average the left and right channels to get a mono sample
                float leftSample = buffer.FloatBuffer[i];
                float rightSample = (i + 1 < buffer.FloatBuffer.Length) ? buffer.FloatBuffer[i + 1] : leftSample;
                float monoSample = (leftSample + rightSample) / 2.0f;

                //Now, use the mono sample to fill our FFT buffer
                FFTBuffer[FFTIndex].X = (float)(monoSample * FastFourierTransform.HannWindow(FFTIndex, FFTSize));
                FFTBuffer[FFTIndex].Y = 0;
                FFTIndex++;
            }
            //When the FFT Buffre is full, we process it
            if (FFTIndex >= FFTSize)
            {
                FFTIndex = 0; // Reset for the next batch  

                //Do the FFT!
                FastFourierTransform.FFT(true, (int)Math.Log(FFTSize, 2.0), FFTBuffer);

                //Create a string to display
                //string outputString = $"Bins: [10]: {GetMagnitude(FFTBuffer[10]):F2} | [60] : {GetMagnitude(FFTBuffer[60]):F2}";


                // -- Drawing Logic -- 
                Dispatcher.BeginInvoke(() =>
                {
                    if (barRectangles is null) return;

                    for (int i = 0; i < NumberOfBars; i++)
                    {
                        int FFTBinIndex = i * (FFTSize / 2 / NumberOfBars);
                        double magnitude = GetMagnitude(FFTBuffer[FFTBinIndex]);
                        double barHeight = (magnitude / 100.0) * SpectrumCanvas.ActualHeight;
                        barRectangles[i].Height = Math.Min(barHeight, SpectrumCanvas.ActualHeight);
                    }
                });
            }   
        }
        private double GetMagnitude(Complex c)
        {
            // a direct multiplier for visual scaling. this is now our sensitivity.
            // we'll start writh a large value and tune it down if needed.
            const double multiplier = 8000.0;
            const double maxHeight = 100.0;

            double magnitude = Math.Sqrt(c.X * c.X + c.Y * c.Y);

            return Math.Min(maxHeight, magnitude * multiplier);
        }

        // this ensures our audio device is released properly
        public void Dispose()
        {
            // 1. Stop the Recording
            capture?.StopRecording();

            // 2. Important: Unsubscribe from the event to prevent a race condition
            if (capture != null)
            {
                capture.DataAvailable -= OnDataAvailable;
            }

            // 3. Now its safe to dispose the object.
            capture?.Dispose();
            
            capture = null;
        }
    }
}