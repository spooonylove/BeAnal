using System;
using System.Windows;           // Required for WPF classes like windows
using System.Windows.Shapes;    //Required for Rectangle
using System.Windows.Media;     // Required for Brushes
using System.Windows.Controls;
using System.Formats.Asn1;


namespace BeAnal.Wpf
{
    public partial class MainWindow : Window
    {

        // --  Fields --
        private readonly AudioProcessor _audioProcessor;
        private Rectangle[] _barRectangles;
        private double[] _lastBarHeights;
        private (int Start, int End)[] _barFFTBinMap;
        private readonly Settings _settings;
        private bool _isWindowLoaded = false;


        public MainWindow()
        {
            InitializeComponent();

            _settings = new Settings();

            // Hook into the window's lifecycle events
            this.Loaded += OnWindowLoaded;
            this.Closing += OnWindowClosing;

            //Making the bordless window dragable
            this.MouseLeftButtonDown += (s, e) => DragMove();

            //Refresh the visualization when resized
            this.SizeChanged += OnWindowSizeChanged;

            //Create and prepare the audio engine
            _audioProcessor = new AudioProcessor(_settings.Sensitivity);
            _audioProcessor.FFTDataAvailable += OnFFTDataAvailable;

            //Create the Visual Bar objects
            _barRectangles = new Rectangle[_settings.NumberOfBars];
            _barFFTBinMap = new (int Start, int End)[_settings.NumberOfBars];
            _lastBarHeights = new double[_settings.NumberOfBars];

            this.Topmost = _settings.IsTopMost;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {

            CalculateLogarithmicMapping();
            CreateVisualBars();
            _audioProcessor.Start();

            _isWindowLoaded = true;
        }

        private void CreateVisualBars()
        {
            for (int i = 0; i < _settings.NumberOfBars; i++)
            {
                var rect = new Rectangle
                {
                    Height = 0,
                    Fill = CreateGradientBrush()
                };
                Canvas.SetBottom(rect, 0); // Anchor the bars to the bottom
                _barRectangles[i] = rect;
                SpectrumCanvas.Children.Add(rect);
            }

            UpdateBarLayout();

            // --- DIAGNOSTIC: Color the last bar blue ---
            if (_barRectangles.Length > 0)
            {
                _barRectangles[_settings.NumberOfBars - 1].Fill = Brushes.Blue;
            }
        }

        private void CalculateLogarithmicMapping()
        {
            //Redefine the map with the new type
            _barFFTBinMap = new (int Start, int End)[_settings.NumberOfBars];
            int maxFFTIndex = AudioProcessor.FFTSize / 2 - 1;

            // Define the frequency resolution once and give it a clear name.
            double frequencyResolution = 48000.0 / AudioProcessor.FFTSize;
            
            
            // --- Lin-Log Hybrid Calculation ---

            // 1. Define the "cross-over" point: how many bars are linear
            const double linearRatio = 0.4;  //40% of bars will be linear
            int lineareBarCount = (int)(_settings.NumberOfBars * linearRatio);

            // 2. Calculate the linear point
            int lastLinearBin = 0;

            for (int i = 0; i < lineareBarCount; i++)
            {
                // Simple one-to-one mapping for the bass
                _barFFTBinMap[i] = (i + 1, i + 2);
                lastLinearBin = i + 2;
            }

            // 3. Calculate the logarithmic part for the remaining bars
            if (_settings.NumberOfBars > lineareBarCount)
            {
                int logBarCount = _settings.NumberOfBars - lineareBarCount;

                // The log scale starts where the linear scale's last bin left off
                double minLogFreq = (lastLinearBin) * frequencyResolution;
                double maxLogFreq = 20000;

                for (int i = 0; i < logBarCount; i++)
                {
                    double freqStart = minLogFreq * Math.Pow(maxLogFreq / minLogFreq, (double)i / logBarCount);
                    double freqEnd = minLogFreq * Math.Pow(maxLogFreq / minLogFreq, (double)(i + 1) / logBarCount);

                    int binStartIndex = (int)(freqStart / frequencyResolution);
                    int binEndIndex = (int)(freqEnd / frequencyResolution);

                    // Safety Checks
                    if (binStartIndex > maxFFTIndex) binStartIndex = maxFFTIndex;
                    if (binEndIndex > maxFFTIndex) binEndIndex = maxFFTIndex;
                    if (binEndIndex <= binStartIndex) binEndIndex = binStartIndex + 1;
                    if (binEndIndex > maxFFTIndex) binEndIndex = maxFFTIndex;

                    _barFFTBinMap[i + lineareBarCount] = (binStartIndex, binEndIndex);
                }
            }
            #if DEBUG
                Console.WriteLine("--- Lin-Log Hybrid Mapping ---");
                
                for (int i = 0; i < _settings.NumberOfBars; i++)
                {
                    var (startBin, endBin) = _barFFTBinMap[i];
                    double startFreq = startBin * frequencyResolution;
                    double endFreq = endBin * frequencyResolution;
                    
                    Console.WriteLine($"Bar {i,3}: Bins {startBin,3}-{endBin,3} (~{startFreq,5:F0} - {endFreq,5:F0} Hz)");
                }
                Console.WriteLine("--- End of Mapping ---");
            #endif

            
    
        }

        // This method is called by the AudioProcessor's event
        private void OnFFTDataAvailable(double[] FFTData)
        {
            

            //Use the dispatcher to update the UI from the audio thread
            Dispatcher.BeginInvoke(() =>
            {
                // Attack Release settings for smoothing function
                double attack = 0.4;
                double release = 0.4;

                for (int i = 0; i < _settings.NumberOfBars; i++)
                {
                    // 1. Get the start and end bins for this bar from our map
                    var (startBin, endBin) = _barFFTBinMap[i];

                    // 2. Calculate the average magnitude within the range of bins
                    double sumMagnitude = 0;
                    int binCount = 0;

                    for (int j = startBin; j < endBin; j++)
                    {
                        if (j < FFTData.Length)
                        {
                            sumMagnitude += FFTData[j];
                            binCount++;
                        }
                    }
                    double averageMagnitude = (binCount > 0) ? sumMagnitude / binCount : 0;

                    // 3. Use that peak fro the smoothing and drawing logic
                    double targetHeight = (averageMagnitude / 100.0) * SpectrumCanvas.ActualHeight;

                    //Smothing functions, yo!
                    double lastHeight = _lastBarHeights[i];
                    double newHeight;
                    if (targetHeight > lastHeight)
                    {
                        newHeight = (targetHeight * attack) + (lastHeight * (1 - attack));
                    }
                    else
                    {
                        newHeight = (targetHeight * release) + (lastHeight * (1 - release));
                    }

                    _barRectangles[i].Height = Math.Min(newHeight, SpectrumCanvas.ActualHeight);
                    _lastBarHeights[i] = newHeight;
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
            // This is a cheap operation, lets do it each time
            _audioProcessor.Sensitivity = _settings.Sensitivity;

            // THis is an expensive operation, so we only do it if the bar count has changed
            if (_barRectangles.Length != _settings.NumberOfBars)
            {
                ReinitializeVisualizer();
            }
        }

        private void ReinitializeVisualizer()
        {
            // 1. Clear the old bars from the canvas
            SpectrumCanvas.Children.Clear();

            // 2. Resize our arrays to match the new settings
            Array.Resize(ref _barRectangles, _settings.NumberOfBars);
            Array.Resize(ref _barFFTBinMap, _settings.NumberOfBars);
            Array.Resize(ref _lastBarHeights, _settings.NumberOfBars);

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


        private void UpdateBarLayout()
        {
            // This method is the single source of truth for bar layout
            // don't do anything if the bars haven't been created yet
            if (_barRectangles is null || _barRectangles.Length == 0 || _barRectangles[0] is null || SpectrumCanvas.ActualWidth == 0)
            {
                return;
            }

            double barwidth = SpectrumCanvas.ActualWidth / _settings.NumberOfBars;

            for (int i = 0; i < _settings.NumberOfBars; i++)
            {
                _barRectangles[i].Width = barwidth;
                Canvas.SetLeft(_barRectangles[i], i * barwidth);
            }
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isWindowLoaded) return;
            
            UpdateBarLayout();
        }
        

    }
}