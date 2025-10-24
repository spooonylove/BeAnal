// Importing NAudio's library Wave namespace
using NAudio.Wave;
using NAudio.Dsp;
using System;

class Program
{

    // Definging FFT parameters
    private const int FFTSize = 1024;
    private static int FFTIndex = 0;
    private static Complex[] FFTBuffer = new Complex[FFTSize];

    static void Main(string[] args)
    {
        Console.WriteLine("Starting system audio capture with FFT...");
        Console.WriteLine("Yous hould see frequency magnitude data when audio is playing");
        Console.WriteLine("Press any key to stop.");

        //Use WasapiLoopbackCapture to capture system's output audio
        // it will create an event each time a chunk of audio is available
        using (var capture = new WasapiLoopbackCapture())
        {
            //Subscribe to DataAvailable event
            capture.DataAvailable += OnDataAvailable;

            //Start recording
            capture.StartRecording();

            // Keep the application runing until a key is pressed.
            Console.ReadKey();

            //Stop Recording
            capture.StopRecording();

        }

        Console.WriteLine("Capture stopped");
    }

    //Thios is the event handler that gets called when audio data is captured
    private static void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        //The incoming buffer is raw bytes, we need to convert it to samples
        var buffer = new WaveBuffer(e.Buffer);

        //Process samples in pairs (since its stereo, 32-bit float)
        for (int i = 0; i < e.BytesRecorded / 4; i++)
        {
            // We only need one channel for the FFT, so we'll avergae them if stereo
            // or just take the sample if mono. For simplicity, we'l take the first sample
            float sample = buffer.FloatBuffer[i];

            //fill out the FFTbuffer
            FFTBuffer[FFTIndex].X = (float)(sample * FastFourierTransform.HannWindow(FFTIndex, FFTSize));
            FFTBuffer[FFTIndex].Y = 0; // Imaginary part is 0
            FFTIndex++;

            //When the FFT Buffre is full, we process it
            if (FFTIndex >= FFTSize)
            {
                FFTIndex = 0; // Reset for the next batch

                //Do the FFT!
                FastFourierTransform.FFT(true, (int)Math.Log(FFTSize, 2.0), FFTBuffer);

                //For this console test, let's just print the magnitude of the first few frequency bins
                // a real visualizer would use them all (up to FFTSize/2)
                Console.WriteLine(
                    $"Bins: " +
                    $"[0] {GetMagnitude(FFTBuffer[0]):F2)} | " +
                    $"[10]: {GetMagnitude(FFTBuffer[10]):F2} |" +
                    $"[100]: {GetMagnitude(FFTBuffer[100]):F2} |" +
                    $"[400]: {GetMagnitude(FFTBuffer[400]):F2}"
                );

            }

        }
    }

    private static double GetMagnitude(Complex c)
    {
        return Math.Sqrt(c.X * c.X + c.Y * c.Y);
    }

}