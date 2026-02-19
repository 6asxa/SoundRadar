using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Linq;
using System.Numerics;

namespace SoundRadar
{
    public class AudioProcessor : IDisposable
    {
        private readonly WasapiLoopbackCapture capture;
        private readonly Action<float, float> onDirection;
        // float angleDegrees, float intensity

        private int channelCount;
        private float[] smoothedLevels;
        private float noiseFloor = 0f;

        private const float SmoothingFactor = 0.25f;
        private const float NoiseAdaptSpeed = 0.01f;
        private const float NoiseGateMultiplier = 1.5f;

        public AudioProcessor(MMDevice device, Action<float, float> onDirection)
        {
            this.onDirection = onDirection;

            capture = new WasapiLoopbackCapture(device);
            channelCount = capture.WaveFormat.Channels;
            smoothedLevels = new float[channelCount];

            capture.DataAvailable += OnDataAvailable;
        }

        public void Start() => capture.StartRecording();

        public void Stop() => capture.StopRecording();

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            int floatCount = e.BytesRecorded / 4;
            int samplesPerChannel = floatCount / channelCount;

            float[] rms = new float[channelCount];

            // === RMS расчет ===
            for (int i = 0; i < floatCount; i += channelCount)
            {
                for (int ch = 0; ch < channelCount; ch++)
                {
                    float sample = BitConverter.ToSingle(e.Buffer, (i + ch) * 4);
                    rms[ch] += sample * sample;
                }
            }

            for (int ch = 0; ch < channelCount; ch++)
                rms[ch] = (float)Math.Sqrt(rms[ch] / samplesPerChannel);

            // === Адаптивный шум ===
            float mean = rms.Average();
            noiseFloor = NoiseAdaptSpeed * mean + (1 - NoiseAdaptSpeed) * noiseFloor;
            float threshold = noiseFloor * NoiseGateMultiplier;

            // === Noise gate + сглаживание ===
            for (int ch = 0; ch < channelCount; ch++)
            {
                float level = rms[ch] > threshold ? rms[ch] : 0f;

                smoothedLevels[ch] =
                    SmoothingFactor * level +
                    (1 - SmoothingFactor) * smoothedLevels[ch];
            }

            // === Вектор направления ===
            Vector2 direction = CalculateDirection(smoothedLevels);

            float intensity = direction.Length();

            if (intensity < 0.0001f)
            {
                onDirection?.Invoke(0f, 0f);
                return;
            }

            direction = Vector2.Normalize(direction);

            float angleRad = MathF.Atan2(direction.X, direction.Y);
            float angleDeg = angleRad * (180f / MathF.PI);

            if (angleDeg < 0)
                angleDeg += 360f;

            onDirection?.Invoke(angleDeg, intensity);
        }

        private Vector2 CalculateDirection(float[] levels)
        {
            Vector2 result = Vector2.Zero;

            for (int ch = 0; ch < levels.Length; ch++)
            {
                Vector2 channelVector = GetChannelVector(ch);
                result += channelVector * levels[ch];
            }

            return result;
        }

        private Vector2 GetChannelVector(int ch)
        {
            // Универсальная логика по индексу канала
            // Работает для 2.0 / 5.1 / 7.1 стандартного порядка

            switch (ch)
            {
                case 0: return new Vector2(-1, 1);  // FL
                case 1: return new Vector2(1, 1);   // FR
                case 2: return new Vector2(0, 1);   // FC
                case 3: return Vector2.Zero;        // LFE игнорируем
                case 4: return new Vector2(-1, 0);  // SL
                case 5: return new Vector2(1, 0);   // SR
                case 6: return new Vector2(-1, -1); // RL
                case 7: return new Vector2(1, -1);  // RR
                default: return Vector2.Zero;
            }
        }

        public void Dispose()
        {
            Stop();
            capture?.Dispose();
        }
    }
}
