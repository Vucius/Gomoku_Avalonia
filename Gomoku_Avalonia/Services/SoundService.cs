using System;
using System.Threading.Tasks;

namespace Gomoku_Avalonia.Services;

public sealed class SoundService
{
    public Task PlayMoveAsync()
    {
        return PlayToneAsync(940, 35);
    }

    public Task PlayWinAsync()
    {
        return Task.Run(async () =>
        {
            await PlayToneAsync(660, 80);
            await PlayToneAsync(990, 120);
        });
    }

    public short[] GenerateMoveClickSamples(int sampleRate = 44100)
    {
        var sampleCount = (int)(sampleRate * 0.11);
        var samples = new short[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)sampleRate;
            var envelope = Math.Exp(-32.0 * t);
            var triangleFrequency = 1400.0 - 900.0 * Math.Min(t / 0.025, 1.0);
            var bassFrequency = 260.0 - 180.0 * Math.Min(t / 0.08, 1.0);
            var triangle = 2.0 * Math.Abs(2.0 * ((t * triangleFrequency) - Math.Floor(t * triangleFrequency + 0.5))) - 1.0;
            var bass = Math.Sin(2.0 * Math.PI * bassFrequency * t);
            var mixed = (triangle * 0.68 + bass * 0.32) * envelope;
            samples[i] = (short)Math.Clamp(mixed * short.MaxValue * 0.34, short.MinValue, short.MaxValue);
        }

        return samples;
    }

    private static Task PlayToneAsync(int frequency, int durationMs)
    {
        return Task.Run(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                Console.Beep(frequency, durationMs);
            }
            catch
            {
                // Audio output is optional; do not block gameplay when the device rejects beeps.
            }
        });
    }
}
