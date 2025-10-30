using System.Windows.Media;

namespace BeAnal.Wpf
{
    public class ColorScheme
    {
        public string Name { get; set; }
        public Color LowColor { get; set; }
        public Color HighColor { get; set; }
        public Color PeakColor { get; set; }

        public ColorScheme(string name, Color low, Color high, Color peak)
        {
            Name = name;
            LowColor = low;
            HighColor = high;
            PeakColor = peak;
        }

        public override string ToString() => Name;
       
    }
}