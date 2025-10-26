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
        private double _multiplier;
        // Publicly accessible Sensitivity setting, to scale the output accordingly
        public double Sensitivity
        {
            get => _multiplier;
            set => _multiplier = value;
        }

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
                    _lastFFTMagnitudes[i] = GetMagnitudedB(_FFTBuffer[i]);
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

        private double GetMagnitudedB(Complex c)
        {
            const double maxHeight = 100.0;
            const double mindB = -60.0; //The "silence" threshold
            const double maxdB = 0.0; // The "max volume" threshold

            // 1. Calculate the raw lineaer magnitude
            double linearMagnitude = Math.Sqrt(c.X * c.X + c.Y * c.Y);

            // Prevent math error with log(0)
            if (linearMagnitude <= 0) return 0;

            // 2. Convert the linear magnitude to decibles
            // we use a reference value (_multiplier) to scale the input
            double dB = 20 * Math.Log10(linearMagnitude * _multiplier);

            // 3. Scale the dB value to our visual height (0-100)
            // this maps the range [-60dB, 0dB] to [0,100]
            double scaledMagnitude = ((dB - mindB) / (maxdB - mindB)) * maxHeight;

            // 4. Clamp the value to ensure its withtin the 0-100 range
            return Math.Max(0, Math.Min(maxHeight, scaledMagnitude));
        }
    }
}