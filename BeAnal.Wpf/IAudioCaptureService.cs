using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace BeAnal.Wpf
{
    /// <summary>
    /// Defines the contract for a platform-specifc audio capture service.
    /// </summar>
    public interface IAudioCaptureService : IDisposable
    {
        // Fires when a new buffer of raw (mono?) audio samples is available
        event Action<float[]>? SamplesAvailable;

        // Gets a list of all active audio output devices
        List<AudioDevice> EnumerateAudioDevices();

        // Starts captureing audio from the specific
        // If deviceID is null, it should capture from the default device
        void StartCapture(string? deviceID);

        // Stops all audio capture
        void StopCapture();
    }
}