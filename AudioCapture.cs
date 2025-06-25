using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;

namespace SoundRadar
{
    public partial class AudioCapture
    {
        private WasapiLoopbackCapture capture;

        public event Action<byte[]> OnAudioDataAvailable;

        public void Start()
        {
            capture = new WasapiLoopbackCapture();

            capture.DataAvailable += (s, a) =>
            {
                OnAudioDataAvailable?.Invoke(a.Buffer);
            };

            capture.StartRecording();
        }

        public void Stop()
        {
            if (capture != null)
            {
                capture.StopRecording();
                capture.Dispose();
                capture = null;
            }
        }
    }
}
