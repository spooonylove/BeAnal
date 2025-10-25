using NAudio.Wave;
using NAudio.Dsp;
using System;


namespace BeAnal.Wpf
{
    public class AudioProcessor : IDisposable
    {

        // This event will be raised whenever new FFT Data is available
        public event Action<double[]>? FFTDataAvailable;

        // -- Audio Processing Fields -- 
        public const int FFTSize = 1024;
        private int _FFTIndex = 0;
        private Complex[] _FFTBuffer = new Complex[FFTSize];
        private WasapiLoopbackCapture? _capture;
        private double[]? _lastFFTMagnitudes;
        private readonly double _multiplier;

        public AudioProcessor(double multiplier)
        {
            _multiplier = multiplier;
        }

        public void Start()
        {
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
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
                if (_FFTIndex >= FFTSize) break;

                //average the left and right channels to get a mono sample
                float leftSample = buffer.FloatBuffer[i];
                float rightSample = (i + 1 < buffer.FloatBuffer.Length) ? buffer.FloatBuffer[i + 1] : leftSample;
                float monoSample = (leftSample + rightSample) / 2.0f;

                //Now, use the mono sample to fill our FFT buffer
                _FFTBuffer[_FFTIndex].X = (float)(monoSample * FastFourierTransform.HannWindow(_FFTIndex, FFTSize));
                _FFTBuffer[_FFTIndex].Y = 0;
                _FFTIndex++;
            }
            //When the FFT Buffre is full, we process it
            if (_FFTIndex >= FFTSize)
            {
                _FFTIndex = 0; // Reset for the next batch  

                //Do the FFT!
                FastFourierTransform.FFT(true, (int)Math.Log(FFTSize, 2.0), _FFTBuffer);

                // Initialize the magnitude array if its null
                if (_lastFFTMagnitudes is null)
                {
                    _lastFFTMagnitudes = new double[FFTSize / 2];
                }

                //Calculate magnitudfes for all useful bins and store them
                for (int i = 0; i < FFTSize / 2; i++)
                {
                    _lastFFTMagnitudes[i] = GetMagnitude(_FFTBuffer[i]);
                }

                //Raise the event, sending a copy of the FFT data to any listeners
                FFTDataAvailable?.Invoke(_lastFFTMagnitudes);
            }
        }
        private double GetMagnitude(Complex c)
        {
            const double maxHeight = 100.0;
            double magnitude = Math.Sqrt(c.X * c.X + c.Y * c.Y);

            return Math.Min(maxHeight, magnitude * _multiplier);
        }
        // this ensures our audio device is released properly
        public void Dispose()
        {
            // 1. Stop the Recording
            _capture?.StopRecording();

            // 2. Important: Unsubscribe from the event to prevent a race condition
            if (_capture != null)
            {
                _capture.DataAvailable -= OnDataAvailable;
            }

            // 3. Now its safe to dispose the object.
            _capture?.Dispose();

            _capture = null;
        }
    }
}