using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Gomoku_Avalonia.Services;

public sealed class SoundService
{
    public Task PlayMoveAsync(bool isBlack = true)
    {
        return Task.Run(() => PlayWavSamples(GenerateMoveClickSamples(isBlack)));
    }

    public Task PlayWinAsync()
    {
        return Task.Run(() => PlayWavSamples(GenerateArpeggio(new[] { 523.25, 659.25, 783.99, 1046.50 }, 0.12, 0.65)));
    }

    public Task PlayLoseAsync()
    {
        return Task.Run(() => PlayWavSamples(GenerateArpeggio(new[] { 392.00, 349.23, 311.13, 261.63 }, 0.15, 0.85)));
    }

    public Task PlayErrorAsync()
    {
        return Task.Run(() => PlayWavSamples(GenerateDissonantChord(new[] { 135.0, 138.0 }, 0.2)));
    }

    private short[] GenerateMoveClickSamples(bool isBlack, int sampleRate = 44100)
    {
        var duration = 0.09;
        var sampleCount = (int)(sampleRate * duration);
        var samples = new short[sampleCount];
        
        var baseFreq = isBlack ? 260.0 : 360.0;

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)sampleRate;
            
            // Bass thump (exponential decay)
            var bassFreq = baseFreq * Math.Exp(-10.0 * (t / 0.08)); 
            var bass = Math.Sin(2.0 * Math.PI * bassFreq * t);
            var bassEnv = t < 0.08 ? Math.Exp(-40.0 * t) * 0.4 : 0;

            // Click transient (triangle)
            var clickFreq = 1400.0 * Math.Exp(-10.0 * (t / 0.02));
            var click = 2.0 * Math.Abs(2.0 * ((t * clickFreq) - Math.Floor(t * clickFreq + 0.5))) - 1.0;
            var clickEnv = t < 0.025 ? Math.Exp(-100.0 * t) * 0.12 : 0;

            var mixed = (bass * bassEnv) + (click * clickEnv);
            samples[i] = (short)Math.Clamp(mixed * short.MaxValue * 0.6, short.MinValue, short.MaxValue);
        }

        return samples;
    }

    private short[] GenerateArpeggio(double[] frequencies, double delayBetweenNotes, double noteDuration, int sampleRate = 44100)
    {
        var totalDuration = (frequencies.Length - 1) * delayBetweenNotes + noteDuration;
        var sampleCount = (int)(sampleRate * totalDuration);
        var samples = new short[sampleCount];

        for (int noteIdx = 0; noteIdx < frequencies.Length; noteIdx++)
        {
            var freq = frequencies[noteIdx];
            var startSample = (int)(noteIdx * delayBetweenNotes * sampleRate);
            var noteSampleCount = (int)(noteDuration * sampleRate);

            for (var i = 0; i < noteSampleCount; i++)
            {
                var globalIdx = startSample + i;
                if (globalIdx >= sampleCount) break;

                var t = i / (double)sampleRate;
                var envelope = 0.15 * Math.Exp(-10.0 * t); // Attack is instant, decay is exponential
                var wave = Math.Sin(2.0 * Math.PI * freq * t);
                var mixed = wave * envelope;

                var currentSample = samples[globalIdx] / (double)short.MaxValue;
                currentSample += mixed;
                samples[globalIdx] = (short)Math.Clamp(currentSample * short.MaxValue, short.MinValue, short.MaxValue);
            }
        }

        return samples;
    }

    private short[] GenerateDissonantChord(double[] frequencies, double duration, int sampleRate = 44100)
    {
        var sampleCount = (int)(sampleRate * duration);
        var samples = new short[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)sampleRate;
            var envelope = 0.12 * Math.Exp(-15.0 * t);
            
            double mixed = 0;
            foreach (var freq in frequencies)
            {
                // Sawtooth for first, Sine for second
                if (freq == frequencies[0]) 
                    mixed += 2.0 * (t * freq - Math.Floor(0.5 + t * freq));
                else 
                    mixed += Math.Sin(2.0 * Math.PI * freq * t);
            }

            samples[i] = (short)Math.Clamp(mixed * envelope * short.MaxValue * 0.5, short.MinValue, short.MaxValue);
        }

        return samples;
    }

    private static void PlayWavSamples(short[] samples, int sampleRate = 44100)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + samples.Length * 2);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(samples.Length * 2);

                foreach (var sample in samples)
                {
                    bw.Write(sample);
                }
            }

            ms.Position = 0;

            var soundPlayerType = Type.GetType("System.Media.SoundPlayer, System.Windows.Extensions") 
                               ?? Type.GetType("System.Media.SoundPlayer, System");
            if (soundPlayerType != null)
            {
                var player = Activator.CreateInstance(soundPlayerType, ms);
                soundPlayerType.GetMethod("Load")?.Invoke(player, null);
                soundPlayerType.GetMethod("Play")?.Invoke(player, null);
            }
        }
        catch { }
    }
}
