using HaSharedLibrary.Audio;

namespace UnitTest_AudioEditor;

public sealed class AudioDspTests
{
    private static AudioBuffer CreateBuffer(params float[] samples)
        => new(new AudioFormatDescriptor(8, 1, 32, AudioEncoding.Float32), new[] { samples });

    [Fact]
    public void TrimSplitReverseAndNormalizeAreSampleAccurate()
    {
        var source = CreateBuffer(0.1f, -0.2f, 0.5f, -0.25f);
        var trimmed = AudioDsp.Trim(source, 1, 2);
        Assert.Equal(new[] { -0.2f, 0.5f }, trimmed.Samples[0]);
        var split = AudioDsp.Split(source, 2);
        Assert.Equal(new[] { 0.1f, -0.2f }, split.Left.Samples[0]);
        Assert.Equal(new[] { 0.5f, -0.25f }, split.Right.Samples[0]);
        Assert.Equal(new[] { -0.25f, 0.5f, -0.2f, 0.1f }, AudioDsp.Reverse(source).Samples[0]);
        Assert.Equal(1f, AudioDsp.Normalize(source).Samples[0][2]);
    }

    [Fact]
    public void FadeAndResamplePreserveChannelShape()
    {
        var source = CreateBuffer(1, 1, 1, 1);
        var faded = AudioDsp.FadeIn(AudioDsp.FadeOut(source, 2), 2);
        Assert.Equal(0, faded.Samples[0][0]);
        Assert.Equal(0, faded.Samples[0][3]);
        Assert.Equal(4, AudioDsp.Resample(source, 8).SampleCount);
    }

    [Fact]
    public void CoreEffectsPreserveFormatAndBoundSignal()
    {
        var source = new AudioBuffer(new AudioFormatDescriptor(8, 2, 32, AudioEncoding.Float32),
            new[] { new[] { 2f, -2f }, new[] { 1f, -1f } });
        var limited = AudioEffects.HardLimiter(source, .5f);
        Assert.Equal(2, limited.Format.ChannelCount);
        Assert.All(limited.Samples.SelectMany(x => x), value => Assert.InRange(value, -.5f, .5f));
        var mono = AudioEffects.ToMono(source);
        Assert.Equal(1, mono.Format.ChannelCount);
        Assert.Equal(1.5f, mono.Samples[0][0]);
    }
}
