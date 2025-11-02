using NAudio.Wave;
using NAudio.Dsp;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;


namespace BeAnal.Wpf
{

    /// <summary>
    /// Platform-agnostic audio processing class.
    /// Receives raw audio samples from an IAudioCaptureService and performs
    /// FFT, smoothing, and peak detection.
    /// </summary>
    public class AudioProcessor : IDisposable
    {
        public event Action<VisualizerData>? ProcessedDataAvailable;
        public const int FFTSize = 1024;

        private readonly Settings _settings;
        private readonly IAudioCaptureService _captureService;

        private int _FFTBufferIndex = 0;
        private Complex[] _FFTBuffer = new Complex[FFTSize];


        private (int Start, int End)[] _barToBinMap;
        private double[] _lastBarHeights;
        
        private float[] _monoSampleBuffer = new float[FFTSize];

        private int _targetNumberOfBars = -1;

        private double[] _peakLevels;
        private double[] _peakHoldTimers;

        private readonly Stopwatch _frameTimer = new Stopwatch();


        public AudioProcessor(Settings settings, IAudioCaptureService captureService)
        {
            _settings = settings;
            _captureService = captureService;

            // Initialize all arrays correctly from the start
            _barToBinMap = new (int, int)[settings.NumberOfBars];
            _lastBarHeights = new double[settings.NumberOfBars];
            _peakLevels = new double[settings.NumberOfBars];
            _peakHoldTimers = new double[settings.NumberOfBars];


            _settings.PropertyChanged += OnSettingsChanged;
            _captureService.SamplesAvailable += OnDataAvailable;
        }

        // Tell the capture service to start
        public void Start()
        {
            // Tell the capture service to start, using the saved Device ID
            _captureService.StartCapture(_settings.SelectedAudioDeviceId);
            _frameTimer.Start();
        }

        // This listens for settings changes from the user, and updates releated audio procesing stuffs
        private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.NumberOfBars))
            {
                // A new bar count has been requested.
                // We just save the target, ProcessFFTData will handle the change.
                //Atomic write, its thread-safe <--- Battling race conditions between threads, are we???
                _targetNumberOfBars = _settings.NumberOfBars;
            }
            
            if (e.PropertyName == nameof(Settings.SelectedAudioDeviceId))
            {
                Debug.WriteLine($"Audio device setting changed. Telling service to restart");
                _captureService.StartCapture(_settings.SelectedAudioDeviceId);
            }
        }


        // This is the entry point for audio data, recieving from the abstracted interface services
        private void OnDataAvailable(float[] monoSamples)
        {
            //Process samples in pairs (since its stereo, 32-bit float)
            for (int i = 0; i < monoSamples.Length; i++)
            {
                if (_FFTBufferIndex >= FFTSize) break;

                // Fill our FFT buffer
                _FFTBuffer[_FFTBufferIndex].X = (float)(monoSamples[i] * FastFourierTransform.HannWindow(_FFTBufferIndex, FFTSize));
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

        // Runs the FFT and processes the datda into visualizer bars.
        private void ProcessFFTData()
        {
            // -- Freeze the timer, grab the time, measure time since last frame --
            _frameTimer.Stop();
            double deltaTime = _frameTimer.Elapsed.TotalSeconds;
            _frameTimer.Restart();

            // Atomically read the target value of bins
            int currentTarget = _targetNumberOfBars;

            // If this is the first run, currentTarget is -1
            if (currentTarget <= 0)
            {
                //set the target to the default size (ie, 64)
                currentTarget = _barToBinMap.Length > 0 ? _barToBinMap.Length : 64;
                //Sync the target so this block doesn't run again
                _targetNumberOfBars = currentTarget;
                //Force the initial map build
                UpdateBarToBinMapping(currentTarget);
            }
            else if (currentTarget != _barToBinMap.Length)
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

            // Use the thread-safe array length
            double[] finalBarHeights = new double[_barToBinMap.Length];


            // -- Calculate smoothing factors based on elapsed time
            // Avoid division by zero if the settings is 0 (by mistake)
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

            }

            ProcessedDataAvailable?.Invoke(new VisualizerData(finalBarHeights, _peakLevels));
        }

        // Rebuilds the mapping of visual bars to FFT frequency bins
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

            const double linearRatio = 0.4; // this determines the knee at which we switch from linear to log

            int linearBarCount = (int)(newNumberOfBars * linearRatio);
            int lastLinearBin = 0;


            // For the linear section, we directly map FFT bins to bars
            for (int i = 0; i < linearBarCount; i++)
            {
                _barToBinMap[i] = (i + 1, i + 2);
                lastLinearBin = i + 2;
            }

            // The Log section determins the remaining number of bars we want to map, 
            // does some slick math to determine the bin seperation (freq start/end), 
            // 
            if (newNumberOfBars > linearBarCount)
            {
                int logBarCount = newNumberOfBars - linearBarCount;
                double minLogFreq = lastLinearBin * frequencyResolution;
                double maxLogFreq = 20000; // We are setting the max frequency. only dogs are hearing up there, anyway.

                for (int i = 0; i < logBarCount; i++)
                {
                    double freqStart = minLogFreq * Math.Pow(maxLogFreq / minLogFreq, (double)(i) / logBarCount);
                    double freqEnd = minLogFreq * Math.Pow(maxLogFreq / minLogFreq, (double)(i + 1) / logBarCount);

                    // Clamp the indexes to valid ranges
                    int binStartIndex = (int)(freqStart / frequencyResolution);
                    int binEndIndex = (int)(freqEnd / frequencyResolution);

                    ///////////////////////
                    // Error prone section
                    ///////////////////////
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
            // Unsubscribe from the service's event
            if (_captureService != null)
            {
                _captureService.SamplesAvailable -= OnDataAvailable;
            }
            // Note: We do *not* dispose the _captureService here.
            // MainWindow is responsible for that, as it created it.
        }

       
    }
}