using NAudio.Wave;
using NAudio.Dsp;
using System;
using System.Security.Permissions;

namespace BeAnal.Wpf
{
    public class AudioProcessor : IDisposable
    {

        // This event will be raised whenever new FFT Data is available
        public event Action<double[]>? FFTDataAvailable;

         // -- Audio Processing Fields -- 
        private const int FFTSize = 1024;
        private int FFTIndex = 0;
        private Complex[] FFTBuffer = new Complex[FFTSize];
        private WasapiLoopbackCapture? capture;
        private double[]? lastFFTMagnitudes;

        public void Start()
        {
            capture = new WasapiLoopbackCapture();
            capture.DataAvailable += OnDataAvailable;
            capture.StartRecording();
        }


        // This is our audio processing method
        //    .... where the rubber meets the road
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

                // Initialize the magnitude array if its null
                if (lastFFTMagnitudes is null)
                {
                    lastFFTMagnitudes = new double[FFTSize / 2];
                }

                //Calculate magnitudfes for all useful bins and store them
                for (int i = 0; i < FFTSize / 2; i++)
                {
                    lastFFTMagnitudes[i] = GetMagnitude(FFTBuffer[i]);
                }

                //Raise the event, sending a copy of the FFT data to any listeners
                FFTDataAvailable?.Invoke(lastFFTMagnitudes);
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