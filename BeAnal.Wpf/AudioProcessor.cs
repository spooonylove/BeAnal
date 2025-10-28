using NAudio.Wave;
using NAudio.Dsp;
using System;
using System.Windows.Media.Animation;
using System.Drawing;


namespace BeAnal.Wpf
{
    public class AudioProcessor : IDisposable
    {

        
        public event Action<double[]>? ProcessedDataAvailable;
        public const int FFTSize = 1024;

        private readonly Settings _settings;
        private WasapiLoopbackCapture? _capture;

        private int _FFTBufferIndex = 0;
        private Complex[] _FFTBuffer = new Complex[FFTSize];
        private (int Start, int End)[] _barToBinMap;
        private double[] _lastBarHeights;
        private int _lastNumberOfBars = 0;


        public AudioProcessor(Settings settings)
        {
            _settings = settings;
            _barToBinMap = Array.Empty<(int, int)>();
            _lastBarHeights = Array.Empty<double>();
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
                if (_FFTBufferIndex >= FFTSize) break;

                //average the left and right channels to get a mono sample
                float leftSample = buffer.FloatBuffer[i];
                float rightSample = (i + 1 < buffer.FloatBuffer.Length) ? buffer.FloatBuffer[i + 1] : leftSample;
                float monoSample = (leftSample + rightSample) / 2.0f;

                //Now, use the mono sample to fill our FFT buffer
                _FFTBuffer[_FFTBufferIndex].X = (float)(monoSample * FastFourierTransform.HannWindow(_FFTBufferIndex, FFTSize));
                _FFTBuffer[_FFTBufferIndex].Y = 0;
                _FFTBufferIndex++;
            }
            //When the FFT Buffre is full, we process it
            if (_FFTBufferIndex >= FFTSize)
            {
                _FFTBufferIndex = 0; // Reset for the next batch  
                FastFourierTransform.FFT(true, (int)Math.Log(FFTSize, 2.0), _FFTBuffer);
                ProcessFFTData();
            }
        }

        private void ProcessFFTData()
        {
            if (_lastNumberOfBars != _settings.NumberOfBars)
            {
                UpdateBarToBinMapping();
                _lastNumberOfBars = _settings.NumberOfBars;
            }

            double[] FFTMagnitudes = new double[FFTSize / 2];
            for (int i = 0; i < FFTMagnitudes.Length; i++)
            {
                FFTMagnitudes[i] = ConvertToDB(_FFTBuffer[i]);
            }

            double[] finalBarHeights = new double[_settings.NumberOfBars];
            double attack = 0.4;
            double release = 0.1;

            for (int i = 0; i < _settings.NumberOfBars; i++)
            {
                var (startBin, endBin) = _barToBinMap[i];

                //-- Optimized Averaging Logic ---
                double sumMagnitude = 0;
                int binCount = endBin - startBin;
                const int maxBinstoAverage = 10; // this is a performance tunable variable. lower is faster, higher is more accurate.

                if (binCount <= 0)
                {
                    // Safety check for empty ranges
                }
                else if (binCount <= maxBinstoAverage)
                {
                    // This is for lowcount bins.  Do all the math, because its fast
                    for (int j = startBin; j < endBin; j++)
                    {
                        if (j < FFTMagnitudes.Length) sumMagnitude += FFTMagnitudes[j];

                    }
                }
                else
                {
                    // When bin counts are higher, don't do all the math, its labor-intensive.
                    // ... instead, step througth the range equally, taking samples along the way
                    binCount = maxBinstoAverage;
                    for (int j = 0; j < maxBinstoAverage; j++)
                    {
                        int binIndex = startBin + (j * (endBin - startBin) / maxBinstoAverage);
                        if (binIndex < FFTMagnitudes.Length) sumMagnitude += FFTMagnitudes[binIndex];
                    }
                }

                double averageMagnitude = (binCount > 0) ? sumMagnitude / binCount : 0;

                // This codeblock performs the smoothing function.
                // It determines the direction of change (up or down), and applies an appropriate
                // scaling factor.
                double lastHeight = _lastBarHeights[i];
                double newHeight = (averageMagnitude > lastHeight)
                    ? (averageMagnitude * attack) + (lastHeight * (1 - attack))
                    : (averageMagnitude * release) + (lastHeight * (1 - release));

                finalBarHeights[i] = newHeight;
                _lastBarHeights[i] = newHeight;
            }

            ProcessedDataAvailable?.Invoke(finalBarHeights);
        }

        private void UpdateBarToBinMapping()
        {
            Array.Resize(ref _barToBinMap, _settings.NumberOfBars);
            Array.Resize(ref _lastBarHeights, _settings.NumberOfBars);

            int maxFFTIndex = FFTSize / 2 - 1;
            double frequencyResolution = 48000 / FFTSize;
            const double linearRatio = 0.4;
            int linearBarCount = (int)(_settings.NumberOfBars * linearRatio);
            int lastLinearBin = 0;
            for (int i = 0; i < linearBarCount; i++)
            {
                _barToBinMap[i] = (i + 1, i + 2);
                lastLinearBin = i + 2;
            }

            if (_settings.NumberOfBars > linearBarCount)
            {
                int logBarCount = _settings.NumberOfBars - linearBarCount;
                double minLogFreq = lastLinearBin + frequencyResolution;
                double maxLogFreq = 20000; // We are setting the max frequency. only dogs are hearing up there, anyway.

                for (int i = 0; i < logBarCount; i++)
                {
                    double freqStart = minLogFreq * Math.Pow(maxLogFreq / minLogFreq, (double)i / logBarCount);
                    double freqEnd = minLogFreq * Math.Pow(maxLogFreq / minLogFreq, (double)(i + 1) / logBarCount);

                    int binStartIndex = (int)(freqStart / frequencyResolution);
                    int binEndIndex = (int)(freqEnd / frequencyResolution);
                    if (binStartIndex > maxFFTIndex) binStartIndex = maxFFTIndex;
                    if (binEndIndex > maxFFTIndex) binEndIndex = maxFFTIndex;
                    if (binEndIndex <= binStartIndex) binEndIndex = binStartIndex + 1;
                    if (binEndIndex > maxFFTIndex) binEndIndex = maxFFTIndex;
                    _barToBinMap[i + linearBarCount] = (binStartIndex, binEndIndex);
                }
            }
            
            #if DEBUG
            Console.WriteLine("--- Lin-Log Hybrid Mapping ---");

            for (int i = 0; i < _settings.NumberOfBars; i++)
            {
                var (startBin, endBin) = _barToBinMap[i];
                double startFreq = startBin * frequencyResolution;
                double endFreq = endBin * frequencyResolution;

                Console.WriteLine($"Bar {i,3}: Bins {startBin,3}-{endBin,3} (~{startFreq,5:F0} - {endFreq,5:F0} Hz)");
            }
            Console.WriteLine("--- End of Mapping ---");
            #endif
        }

        private double ConvertToDB(Complex c)
        {
            const double maxHeight = 100.0;
            const double mindB = -60.0; //The "silence" threshold
            const double maxdB = 0.0; // The "max volume" threshold

            // 1. Calculate the raw lineaer magnitude
            double linearMagnitude = Math.Sqrt(c.X * c.X + c.Y * c.Y);

            // Prevent math error with log(0)
            if (linearMagnitude <= 0) return 0;

            // 2. Convert the linear magnitude to decibles
            // Apply the user-facing sensitivty scaler here
            double dB = 20 * Math.Log10(linearMagnitude * _settings.Sensitivity);

            // 3. Scale the dB value to our visual height (0-100)
            // this maps the range [-60dB, 0dB] to [0,100]
            double scaledMagnitude = ((dB - mindB) / (maxdB - mindB)) * maxHeight;

            // 4. Clamp the value to ensure its withtin the 0-100 range
            return Math.Max(0, Math.Min(maxHeight, scaledMagnitude));
        }


        // this ensures our audio device is released properly
        public void Dispose()
        {
            // 1. Stop the Recording
            _capture?.StopRecording();

            // 2. Important: Unsubscribe from the event to prevent a race condition
            if (_capture != null) _capture.DataAvailable -= OnDataAvailable;
            
            // 3. Now its safe to dispose the object.
            _capture?.Dispose();

            _capture = null;
        }
        
    }
}