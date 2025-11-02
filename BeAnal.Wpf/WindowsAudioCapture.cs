using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BeAnal.Wpf
{
    /// <summary
    /// The windows-specific implementation of the audio capture service
    /// This class contains all the Naudio code that is not cross-platform!
    /// </summary>
    public class WindowsAudioCapture : IAudioCaptureService
    {
        public event Action<float[]>? SamplesAvailable;

        private readonly MMDeviceEnumerator _deviceEnumerator = new MMDeviceEnumerator();
        private WasapiLoopbackCapture? _capture;
        private float[] _monoSampleBuffer = Array.Empty<float>();

        public WindowsAudioCapture()
        {

        }

        // Get a list of all active audio output devices (eg, "Speakers", "Headphones")
        public List<AudioDevice> EnumerateAudioDevices()
        {
            var devices = new List<AudioDevice>();

            try
            {
                // Add the "Follow Default" option as the first item
                devices.Add(new AudioDevice(null, "Follow Default Device"));

                // Find all active "render" (ie, output) devices
                var renderDevices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                //load up whatever you found in the fresh enumeration
                devices.AddRange(renderDevices.Select(d => new AudioDevice(d.ID, d.FriendlyName)));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enumerating audio devices: {ex.Message}");
                if (!devices.Any())
                {
                    devices.Add(new AudioDevice(null, "Follow Default Device"));
                }
            }

            return devices;
        }

        // Starts the capture of audio on a specified device
        // if null (default), well, then it chooses the default option
        public void StartCapture(string? deviceId)
        {
            try
            {
                //Clean up any previously open captures
                StopCapture();

                MMDevice device;
                if (string.IsNullOrEmpty(deviceId))
                {
                    // User selected "Follow Default Device"
                    device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    Debug.WriteLine($"Starting capture of DEFAULT device: {device.FriendlyName}");
                }
                else
                {
                    //user selected a non-default device
                    device = _deviceEnumerator.GetDevice(deviceId);
                    Debug.WriteLine($"Starting capture on SPECIFIC device: {device.FriendlyName}");
                }

                _capture = new WasapiLoopbackCapture(device);
                _capture.DataAvailable += OnDataAvailable;
                _capture.StartRecording();
            }
            catch (Exception ex)
            {
                // Catching edge-case errors if the device is unplugged right as we start
                Debug.WriteLine($"Error starting audio capture: {ex.Message}");
                StopCapture();
            }
        }

        // Stops all audio capture and cleans up resources
        public void StopCapture()
        {
            _capture?.StopRecording();
            if (_capture != null)
            {
                _capture.DataAvailable -= OnDataAvailable;
            }
            _capture?.Dispose();
            _capture = null;
        }

        // NAudio's callback when a buffer of audio is available
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (SamplesAvailable == null) return;

            var buffer = new WaveBuffer(e.Buffer);
            int samplesRecorded = e.BytesRecorded;

            //this assumes stereo input, which is standard for loopback
            int monoSamples = samplesRecorded / 2;

            if (_monoSampleBuffer.Length < monoSamples)
            {
                _monoSampleBuffer = new float[monoSamples];
            }

            int outIndex = 0;
            for (int i = 0; i < samplesRecorded; i += 2) //Process in stereo pair
            {
                float leftSample = buffer.FloatBuffer[i];
                // Check to see if the buffer ain't overflowin! Don't go reaching for another man's bits...
                float rightSample = (i + 1 < buffer.FloatBuffer.Length) ? buffer.FloatBuffer[i + 1] : leftSample;
                _monoSampleBuffer[outIndex++] = (leftSample + rightSample) / 2.0f;
            }

            // Fire the event with only the valid samples
            if (outIndex > 0)
            {
                // Send a *new* array containing only valid samples.
                // This is safer than passing the reusable buffer.
                //The "What-If" Scenario (If we didn't copy):
                //
                //Imagine we just passed the buffer directly:
                //
                //SamplesAvailable(_monoSampleBuffer);
                //
                //The AudioProcessor would start its FFT (which takes time). But in the nanoseconds it takes to start, 
                // the NAudio thread fires OnDataAvailable again, and it immediately starts overwriting _monoSampleBuffer with new audio data.
                //The AudioProcessor would be trying to read from an array that is simultaneously being written to by another thread. 
                //This could corrupt the FFT calculation and cause the visualizer to be a glitchy, nonsensical mess.

                float[] samplesToSend = new float[outIndex];
                Array.Copy(_monoSampleBuffer, samplesToSend, outIndex);
                SamplesAvailable(samplesToSend);
            }

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

        // Cleans up all the NAudio stuff
        public void Dispose()
        {
            StopCapture();
            _deviceEnumerator.Dispose();
        }
    }

}
