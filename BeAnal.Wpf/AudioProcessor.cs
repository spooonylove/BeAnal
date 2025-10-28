using NAudio.Wave;
using NAudio.Dsp;
using System;
using System.Windows.Media.Animation;
using System.Drawing;
using System.Windows.Documents;
using System.ComponentModel;


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
        private float[] _monoSampleBuffer = new float[FFTSize]; // RMS Test buffer
        private bool _rebuildBarMap = true;

        public AudioProcessor(Settings settings)
        {
            _settings = settings;
            _barToBinMap = Array.Empty<(int, int)>();
            _lastBarHeights = Array.Empty<double>();

            _settings.PropertyChanged += OnSettingsChanged;
        }

        public void Start()
        {
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
        }

        private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            // If a property that affects a bar layout changes, set the flag!
            if (e.PropertyName == nameof(Settings.NumberOfBars))
            {
                _rebuildBarMap = true;
            }
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

                //Store the raw mono sample for the RMS testing below
                _monoSampleBuffer[_FFTBufferIndex] = monoSample;

                //Now, use the mono sample to fill our FFT buffer
                _FFTBuffer[_FFTBufferIndex].X = (float)(monoSample * FastFourierTransform.HannWindow(_FFTBufferIndex, FFTSize));
                _FFTBuffer[_FFTBufferIndex].Y = 0;
                _FFTBufferIndex++;
            }
            //When the FFT Buffre is full, we process it
            if (_FFTBufferIndex >= FFTSize)
            {

                // --- DIAGNOSTIC --- RMS validation
                double rmsValue = CalculateRMS(_monoSampleBuffer);
                System.Diagnostics.Debug.WriteLine($"RMS Amplitude: {rmsValue:F6}");
                // --- END DIAGNOSTIC --- RMS validation

                _FFTBufferIndex = 0; // Reset for the next batch  
                FastFourierTransform.FFT(true, (int)Math.Log(FFTSize, 2.0), _FFTBuffer);
                ProcessFFTData();
            }
        }

        private void ProcessFFTData()
        {
            if (_rebuildBarMap)
            {
                UpdateBarToBinMapping();
                _rebuildBarMap = ;
            }

            double[] FFTMagnitudes = new double[FFTSize / 2];
            for (int i = 0; i < FFTMagnitudes.Length; i++)
            {
                // 1. Calculate the raw linear magnitude from the complex FFT signal
                double rawMagnitude = Math.Sqrt(_FFTBuffer[i].X * _FFTBuffer[i].X + _FFTBuffer[i].Y * _FFTBuffer[i].Y);

                // 2. Apply the amplitdue correction for the Hann Window
                double correctedMagnitude = rawMagnitude * 2.0;
                
                // 3. Pass the corercted mangtidue for dB conversion
                FFTMagnitudes[i] = ConvertToDB(correctedMagnitude);
            }

            double[] finalBarHeights = new double[_settings.NumberOfBars];
            double attack = 0.4;
            double release = 0.1;

            for (int i = 0; i < _settings.NumberOfBars; i++)
            {
                var (startBin, endBin) = _barToBinMap[i];

                // -- Peak Detection Logic --
                double peakMagnitude = 0; ;

                // Iterate through all the bins assigned to this bar
                for (int j = startBin; j < endBin; j++)
                {
                    //find the highest magnitude in the range  <-- this would be easy in python, yo
                    if (j < FFTMagnitudes.Length && FFTMagnitudes[j] > peakMagnitude)
                    {
                        peakMagnitude = FFTMagnitudes[j];
                    }
                }
                

                // This codeblock performs the smoothing function.
                // It determines the direction of change (up or down), and applies an appropriate
                // scaling factor.
                double lastHeight = _lastBarHeights[i];
                double newHeight = (peakMagnitude > lastHeight)
                    ? (peakMagnitude * attack) + (lastHeight * (1 - attack))
                    : (peakMagnitude * release) + (lastHeight * (1 - release));

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
                double minLogFreq = lastLinearBin * frequencyResolution;
                double maxLogFreq = 20000; // We are setting the max frequency. only dogs are hearing up there, anyway.

                // establish a clean handoff between linear and log
                int lastBin = lastLinearBin;

                for (int i = 0; i < logBarCount; i++)
                {
                    double freqStart = minLogFreq * Math.Pow(maxLogFreq / minLogFreq, (double)(i) / logBarCount);
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

        private double ConvertToDB(double linearMagnitude)
        {
            const double maxHeight = 100.0;
            const double mindB = -60.0; //The "silence" threshold
            const double maxdB = 0.0; // The "max volume" threshold

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

        public double CalculateRMS(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return 0.0;
            }

            // 1. Sum the square of the samples
            // Using the dobule for the sum to prevent potential overflow an precision losses
            double sumOfSquares = 0.0;
            for (int i = 0; i < samples.Length; i++)
            {
                sumOfSquares += samples[i] * samples[i];
            }

            // 2. Calculate the mean of the squares
            double meanSquare = sumOfSquares / samples.Length;

            // 3. Return the square root of the mean
            return Math.Sqrt(meanSquare);
        }
        
    }
}