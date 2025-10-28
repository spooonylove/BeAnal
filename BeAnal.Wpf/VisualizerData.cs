// VisualizerData.cs

namespace BeAnal.Wpf
{
    /// <summary>
    /// A data tranfer object that holds all the information needed
    /// by the UI to render a single frame of the visualizer
    /// </summary>
    public class VisualizerData
    {
        /// <summary>
        /// The height values for th main visualizer bars (0-100)
        /// </summar>
        public double[] BarHeights { get; }
        
        /// <summary>
        /// The height values for the peak indicators (0-100)
        /// </sumamry>
        public double[] PeakLevels { get; }
        
        public VisualizerData(double[] barHeights, double[] peakLevels)
        {
            BarHeights = barHeights;
            PeakLevels = peakLevels;
        }
    }
}