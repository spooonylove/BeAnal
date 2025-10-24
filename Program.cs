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

            // -- DIAGNNOSTIC 1: check the audio format -- 
            Console.WriteLine($"Capture WaveFormat: {capture.WaveFormat}");
            // -- END DIAGNOSTIC -- 

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
            //average the left and right channels to get a mono sample
            float leftSample = buffer.FloatBuffer[i];
            float rightSample = (i + 1 < buffer.FloatBuffer.Length) ? buffer.FloatBuffer[i + 1] : leftSample;
            float monoSample = (leftSample + rightSample) / 2.0f;

            // -- DIAGNOSTIC 2: Check a raw sample value -- 
            // We'll print the first sample of each new FFT block
            if (FFTIndex == 0)
            {
                Console.WriteLine($"First mono sample of block: {monoSample:F8}");
            }
            // -- END DIAGNOSTIC -- 

            //Now, use the mono sample to fill our FFT buffer
            FFTBuffer[FFTIndex].X = (float)(monoSample * FastFourierTransform.HannWindow(FFTIndex, FFTSize));
            FFTBuffer[FFTIndex].Y = 0;
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
                    $"[10]: {GetMagnitude(FFTBuffer[0]):F4} | " +
                    $"[30]: {GetMagnitude(FFTBuffer[30]):F4} |" +
                    $"[60]: {GetMagnitude(FFTBuffer[60]):F4} |" +
                    $"[120]: {GetMagnitude(FFTBuffer[120]):F4}"
                );

            }

        }
    }

    private static double GetMagnitude(Complex c)
    {
        return Math.Sqrt(c.X * c.X + c.Y * c.Y);
    }

}