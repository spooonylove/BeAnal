using System.Windows.Media;

namespace BeAnal.Wpf
{
    public class Settings
    {
        public int NumberOfBars { get; set; } = 64;
        public double Sensitivity { get; set; } = 8000.0;
        public Color LowColor { get; set; } = Colors.Green;
        public Color HighColor { get; set; } = Colors.Red;
        public bool IsTopMost { get; set; } = true;
    }
}