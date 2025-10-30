using NAudio.Wave;
using NAudio.Dsp;
using System;
using System.Windows.Media.Animation;
using System.Drawing;
using System.Windows.Documents;
using System.ComponentModel;
using System.Diagnostics;


namespace BeAnal.Wpf
{
    public class AudioProcessor : IDisposable
    {


        public event Action<VisualizerData>? ProcessedDataAvailable;
        public const int FFTSize = 1024;

        private readonly Settings _settings;
        private WasapiLoopbackCapture? _capture;

        private int _FFTBufferIndex = 0;
        private Complex[] _FFTBuffer = new Complex[FFTSize];


        private (int Start, int End)[] _barToBinMap;
        private double[] _lastBarHeights;
        
        private float[] _monoSampleBuffer = new float[FFTSize]; // RMS Test buffer

        private int _targetNumberOfBars = -1;

        private double[] _peakLevels;
        private double[] _peakHoldTimers;

        private readonly Stopwatch _frameTimer = new Stopwatch();


        public AudioProcessor(Settings settings)
        {
            _settings = settings;

            // Initialize all arrays correctly from the start
            _barToBinMap = new (int, int)[settings.NumberOfBars];
            _lastBarHeights = new double[settings.NumberOfBars];
            _peakLevels = new double[settings.NumberOfBars];
            _peakHoldTimers = new double[settings.NumberOfBars];

            
            _settings.PropertyChanged += OnSettingsChanged;
        }

        public void Start()
        {
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
            _frameTimer.Start();
        }

        private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            // If a property that affects a bar layout changes, set the flag!
            if (e.PropertyName == nameof(Settings.NumberOfBars))
            {
                //Atomic write, its thread-safe <--- Battling race conditions between threads, are we???
                _targetNumberOfBars = _settings.NumberOfBars;
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
                // DEBUG TEST 1: Is the audio buffer filling and are we triggering processing?
                System.Diagnostics.Debug.WriteLine($"--- FFT BUFFER FULL --- RMS: {rmsValue:F6} ---");
                // --- END DIAGNOSTIC --- RMS validation

                _FFTBufferIndex = 0; // Reset for the next batch  
                FastFourierTransform.FFT(true, (int)Math.Log(FFTSize, 2.0), _FFTBuffer);
                ProcessFFTData();
            }
        }

        private void ProcessFFTData()
        {
            // -- Freeze the timer, grab the time, measure time since last frame --
            _frameTimer.Stop();
            double deltaTime = _frameTimer.Elapsed.TotalSeconds;
            _frameTimer.Restart();

            // Atomically read the target value of bins
            int currentTarget = _targetNumberOfBars;

            // If this is the first run, currentTarget is -1
            if (currentTarget == -1)
            {
                //set the target to the default size (ie, 64)
                currentTarget = _barToBinMap.Length;
                //Sync the target so this block doesn't run again
                _targetNumberOfBars = currentTarget;
                //Force the initial map build
                UpdateBarToBinMapping(currentTarget);
            }
            else if (currentTarget != _barToBinMap.Length && currentTarget > 0)
            {
                //Rebuild the map to the user's new target size
                UpdateBarToBinMapping(currentTarget);
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

            double[] finalBarHeights = new double[_barToBinMap.Length];

            // DEBUG TEST 2: Is the raw FFT data or dB conversion valid?
            // Let's check a few bins. If these are 0.00, the problem is in the FFT or ConvertToDB.
            System.Diagnostics.Debug.WriteLine($"AudioData: Raw Magnitudes (dB scaled) [10]={FFTMagnitudes[10]:F2} [50]={FFTMagnitudes[50]:F2} [100]={FFTMagnitudes[100]:F2}");

            // -- Calculate smoothing factors based on elapsed time
            // Avoid division by zer if the settings is 0 (by mistake)
            double attackFactor = (_settings.BarAttackTimeMs > 0) ? deltaTime / (_settings.BarAttackTimeMs / 1000.0) : 1.0;
            double releaseFactor = (_settings.BarReleaseTimeMs > 0) ? deltaTime / (_settings.BarReleaseTimeMs / 1000.0) : 1.0;

            // Clamp the factors to a mxax of 1.0 to prevent overshooting (can we say LAGGGG)
            attackFactor = Math.Min(1.0, attackFactor);
            releaseFactor = Math.Min(1.0, releaseFactor);

            for (int i = 0; i < _barToBinMap.Length; i++)
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
                    ? lastHeight + (peakMagnitude - lastHeight) * attackFactor
                    : lastHeight + (peakMagnitude - lastHeight) * releaseFactor;

                // DEBUG TEST 3: Is the smoothing logic working?
                if (i == 10) // Just check bar 10
                {
                    System.Diagnostics.Debug.WriteLine($"AudioData: Bar 10 Smoothing: peakMag={peakMagnitude:F2}, lastHeight={lastHeight:F2}, newHeight={newHeight:F2}");
                }


                finalBarHeights[i] = newHeight;
                _lastBarHeights[i] = newHeight;
            }

            // Peak Detection Logic Block
            double peakReleaseFactor = (_settings.PeakReleaseTimeMs > 0) ? deltaTime / (_settings.PeakReleaseTimeMs / 1000.0) : 1.0;
            peakReleaseFactor = Math.Min(1.0, peakReleaseFactor);
            
            for (int i = 0; i < finalBarHeights.Length; i++)
            {
                double currentBarHeight = finalBarHeights[i];

                if (currentBarHeight >= _peakLevels[i])
                {
                    _peakLevels[i] = currentBarHeight;
                    _peakHoldTimers[i] = _settings.PeakHoldTimeMs / 1000.0;
                }
                else
                {
                    if (_peakHoldTimers[i] > 0)
                    {
                        _peakHoldTimers[i] -= deltaTime;
                    }
                    else
                    {
                        // Decay the peak level based on the per-second rate
                        _peakLevels[i] += (0 - _peakLevels[i] * peakReleaseFactor);
                    }
                }

                //if (_peakLevels[i] < currentBarHeight)
                //{
                //    _peakLevels[i] = currentBarHeight;
                //}
            }

            ProcessedDataAvailable?.Invoke(new VisualizerData(finalBarHeights, _peakLevels));
        }

        private void UpdateBarToBinMapping(int newNumberOfBars)
        {
            int oldNumberOfBars = _barToBinMap.Length;

            Array.Resize(ref _barToBinMap, newNumberOfBars);
            Array.Resize(ref _lastBarHeights, newNumberOfBars);
            Array.Resize(ref _peakLevels, newNumberOfBars);
            Array.Resize(ref _peakHoldTimers, newNumberOfBars);

            // if we've added new bars, initialize their last heights to 0 
            // to prevent them from inheriting old values and spiking
            if (newNumberOfBars > oldNumberOfBars)
            {
                for (int i = oldNumberOfBars; i < newNumberOfBars; i++)
                {
                    _lastBarHeights[i] = 0;
                    _peakLevels[i] = 0;
                    _peakHoldTimers[i] = 0;
                }
            }

            int maxFFTIndex = FFTSize / 2 - 1;
            double frequencyResolution = 48000 / FFTSize;
            const double linearRatio = 0.4;
            int linearBarCount = (int)(newNumberOfBars * linearRatio);
            int lastLinearBin = 0;
            
            for (int i = 0; i < linearBarCount; i++)
            {
                _barToBinMap[i] = (i + 1, i + 2);
                lastLinearBin = i + 2;
            }

            if (newNumberOfBars > linearBarCount)
            {
                int logBarCount = newNumberOfBars - linearBarCount;
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
            System.Diagnostics.Debug.WriteLine("--- Lin-Log Hybrid Mapping ---");

            for (int i = 0; i < newNumberOfBars; i++)
            {
                if (i >= _barToBinMap.Length) break; // Safety check
                var (startBin, endBin) = _barToBinMap[i];
                double startFreq = startBin * frequencyResolution;
                double endFreq = endBin * frequencyResolution;

                System.Diagnostics.Debug.WriteLine($"Bar {i,3}: Bins {startBin,3}-{endBin,3} (~{startFreq,5:F0} - {endFreq,5:F0} Hz)");
            }
            System.Diagnostics.Debug.WriteLine("--- End of Mapping ---");
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