namespace BeAnal.Wpf
{
    /// <summary>
    /// A simple data object to hold audio device info
    /// Used for presenting audio device information for settings/display
    /// </summary>
    public class AudioDevice
    {
        // Unique ID of the audio device 
        // A null ID is used for the "Follow Default Device" option <<-- Windows specific?
        public string? Id { get; set; }

        // User-friendly, hooman readible format
        public string Name{ get; set; }

        public AudioDevice(string? id, string name)
        {
            Id = id;
            Name = name;
        }

        // Helper function to make the device name actualy print in a friendly name
        public override string ToString()
        {
            return Name;
        }
    }

}