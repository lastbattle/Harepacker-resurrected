using System;

namespace HaSharedLibrary.Audio;

/// <summary>Allocation-free-in-place building blocks used by preview and offline render clients.</summary>
public static class AudioEffects
{
    public static AudioBuffer ApplyGain(AudioBuffer source, float gain)
    {
        var result = source.Clone();
        foreach (var channel in result.Samples)
            for (var i = 0; i < channel.Length; i++) channel[i] *= gain;
        return result;
    }

    public static AudioBuffer ApplyPan(AudioBuffer source, float pan)
    {
        if (source.Format.ChannelCount < 2) return source.Clone();
        var result = source.Clone();
        var left = MathF.Sqrt((1 - Math.Clamp(pan, -1, 1)) * .5f);
        var right = MathF.Sqrt((1 + Math.Clamp(pan, -1, 1)) * .5f);
        for (var i = 0; i < result.SampleCount; i++) { result.Samples[0][i] *= left; result.Samples[1][i] *= right; }
        return result;
    }

    public static AudioBuffer Fade(AudioBuffer source, long fadeInSamples, long fadeOutSamples)
    {
        var result = source.Clone();
        for (var i = 0; i < result.SampleCount; i++)
        {
            var factor = 1f;
            if (fadeInSamples > 0 && i < fadeInSamples) factor = MathF.Min(factor, i / (float)fadeInSamples);
            var tail = result.SampleCount - i - 1;
            if (fadeOutSamples > 0 && tail < fadeOutSamples) factor = MathF.Min(factor, tail / (float)fadeOutSamples);
            for (var c = 0; c < result.Format.ChannelCount; c++) result.Samples[c][i] *= factor;
        }
        return result;
    }

    public static AudioBuffer RemoveDcOffset(AudioBuffer source)
    {
        var result = source.Clone();
        for (var c = 0; c < result.Format.ChannelCount; c++)
        {
            double mean = 0; for (var i = 0; i < result.SampleCount; i++) mean += result.Samples[c][i];
            mean /= Math.Max(1, result.SampleCount);
            for (var i = 0; i < result.SampleCount; i++) result.Samples[c][i] -= (float)mean;
        }
        return result;
    }

    public static AudioBuffer ToMono(AudioBuffer source)
    {
        var format = source.Format.Clone(); format.ChannelCount = 1; format.ChannelLayout = "mono";
        var data = new[] { new float[(int)source.SampleCount] };
        for (var i = 0; i < source.SampleCount; i++) { float sum = 0; for (var c = 0; c < source.Format.ChannelCount; c++) sum += source.Samples[c][i]; data[0][i] = sum / source.Format.ChannelCount; }
        return new AudioBuffer(format, data, true);
    }

    public static AudioBuffer ToStereo(AudioBuffer source)
    {
        if (source.Format.ChannelCount == 2) return source.Clone();
        var format = source.Format.Clone(); format.ChannelCount = 2; format.ChannelLayout = "stereo";
        var left = new float[(int)source.SampleCount]; var right = new float[(int)source.SampleCount];
        for (var i = 0; i < source.SampleCount; i++) { float value = source.Samples[0][i]; left[i] = value; right[i] = value; }
        return new AudioBuffer(format, new[] { left, right }, true);
    }

    public static AudioBuffer HardLimiter(AudioBuffer source, float ceiling = 1f)
    {
        var result = source.Clone(); ceiling = MathF.Abs(ceiling);
        foreach (var channel in result.Samples) for (var i = 0; i < channel.Length; i++) channel[i] = Math.Clamp(channel[i], -ceiling, ceiling);
        return result;
    }

    public static AudioBuffer Compressor(AudioBuffer source, float threshold = .5f, float ratio = 4f)
    {
        var result = source.Clone(); threshold = MathF.Abs(threshold); ratio = MathF.Max(1, ratio);
        foreach (var channel in result.Samples) for (var i = 0; i < channel.Length; i++) { var sign = MathF.Sign(channel[i]); var a = MathF.Abs(channel[i]); if (a > threshold) a = threshold + (a - threshold) / ratio; channel[i] = sign * a; }
        return result;
    }

    public static AudioBuffer Delay(AudioBuffer source, int delaySamples, float feedback = 0f, float wet = .5f)
    {
        var result = source.Clone(); delaySamples = Math.Max(0, delaySamples); feedback = Math.Clamp(feedback, 0, .99f); wet = Math.Clamp(wet, 0, 1);
        if (delaySamples == 0) return result;
        foreach (var channel in result.Samples) for (var i = delaySamples; i < channel.Length; i++) channel[i] = channel[i] * (1 - wet) + channel[i - delaySamples] * wet + channel[i - delaySamples] * feedback;
        return result;
    }
}
