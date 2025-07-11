using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;


namespace SoundRadar
{
    public partial class AudioProcessor : IDisposable
    {
        private WasapiCapture capture;
        private int channelCount;
        private int sampleRate = 44100;
        private int bufferSize = 1024;
        private float backgroundNoise = 0;
        private float alpha = 0.05f;
        private volatile bool running = false;
        private Task processingTask;
        private Action<float[]> onVolumes;
        private string deviceName;

        public AudioProcessor(string deviceName, int channels, Action<float[]> onVolumes)
        {
            this.deviceName = deviceName;
            this.channelCount = channels;
            this.onVolumes = onVolumes;
        }

        public void Start()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

                var device = devices.FirstOrDefault(d => d.FriendlyName == deviceName);
                if (device == null)
                {
                    // Попробуем найти устройство по умолчанию, если указанное не найдено
                    device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);

                    if (device == null)
                        throw new Exception("Не найдено ни указанное устройство, ни устройство по умолчанию");

                    // Обновим имя устройства на найденное по умолчанию
                    deviceName = device.FriendlyName;
                }

                capture = new WasapiLoopbackCapture();
                capture.WaveFormat = new WaveFormat(sampleRate, 16, channelCount);
                capture.DataAvailable += Capture_DataAvailable;
                capture.StartRecording();
                running = true;
            }
            catch (Exception ex)
            {
                // Логирование ошибки
                Trace.WriteLine($"[{DateTime.Now}] AudioProcessor.Start error: {ex}");
                throw; // Перебросить исключение дальше или обработать
            }
        }

        private void Capture_DataAvailable(object sender, WaveInEventArgs e)
        {
            // Преобразуем байты в short
            int samples = e.BytesRecorded / 2;
            short[] buffer = new short[samples];
            Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);

            // Разделяем по каналам
            float[] volumes = new float[channelCount];
            for (int ch = 0; ch < channelCount; ch++)
            {
                float sum = 0;
                int count = 0;
                for (int i = ch; i < buffer.Length; i += channelCount)
                {
                    float sample = buffer[i] / 32768f;
                    sum += sample * sample;
                    count++;
                }
                volumes[ch] = (float)Math.Sqrt(sum / Math.Max(1, count));
            }

            // Фоновый шум и порог
            float mean = volumes.Average();
            backgroundNoise = alpha * mean + (1 - alpha) * backgroundNoise;
            float threshold = backgroundNoise * 1.5f;

            // Только вызов делегата:
            onVolumes?.Invoke(volumes.Select(v => v > threshold ? v : 0).ToArray());
        }

        public void Stop()
        {
            running = false;
            if (capture != null)
            {
                capture.StopRecording();
                capture.Dispose();
                capture = null;
            }
        }

        public void Dispose() => Stop();
    }
}