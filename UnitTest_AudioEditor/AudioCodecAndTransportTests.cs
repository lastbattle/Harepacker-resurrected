using System.IO;
using HaSharedLibrary.Audio;
using NAudio.Wave;

namespace UnitTest_AudioEditor;

public sealed class AudioCodecAndTransportTests
{
    [Fact]
    public async Task PcmWavRoundTripPreservesMetadataAndSamples()
    {
        var format = new AudioFormatDescriptor(8000, 1, 16, AudioEncoding.Pcm);
        var source = new AudioBuffer(format, new[] { new[] { -1f, 0f, 1f, 0f } });
        var provider = new NAudioCodecProvider();
        var encoded = await provider.EncodeAsync(source, new AudioEncodeSettings
        {
            Encoding = AudioEncoding.Pcm,
            SampleRate = 8000,
            ChannelCount = 1,
            BitsPerSample = 16,
        });
        await using var stream = new MemoryStream(encoded.Data);
        var decoded = await provider.DecodeAsync(stream, ".wav");
        Assert.Equal(8000, decoded.Metadata.OriginalFormat!.SampleRate);
        Assert.Equal(1, decoded.Metadata.OriginalFormat.ChannelCount);
        Assert.Equal(4, decoded.Buffer.SampleCount);
        Assert.InRange(decoded.Buffer.Samples[0][2], 0.99f, 1f);
    }

    [Fact]
    public async Task Mp3EncodeProducesDecodableAudio()
    {
        const int sampleRate = 44100;
        float[] samples = Enumerable.Range(0, sampleRate)
            .Select(index => (float)(Math.Sin(index * 2 * Math.PI * 440 / sampleRate) * 0.25)).ToArray();
        var source = new AudioBuffer(new AudioFormatDescriptor(sampleRate, 2, 32, AudioEncoding.Float32),
            new[] { samples, samples.ToArray() });
        var provider = new NAudioCodecProvider();
        AudioEncodeResult encoded = await provider.EncodeAsync(source, new AudioEncodeSettings
        {
            Encoding = AudioEncoding.Mp3,
            SampleRate = sampleRate,
            ChannelCount = 2,
            Mp3BitrateKbps = 192,
        });

        Assert.True(encoded.Data.Length > 1024);
        await using var stream = new MemoryStream(encoded.Data);
        AudioDecodeResult decoded = await provider.DecodeAsync(stream, ".mp3");
        Assert.Equal(sampleRate, decoded.Metadata.SampleRate);
        Assert.Equal(2, decoded.Metadata.ChannelCount);
        Assert.InRange(decoded.Buffer.Duration.TotalSeconds, 0.9, 1.1);
    }

    [Fact]
    public async Task NullTransportSupportsSeekLoopAndDispose()
    {
        using var transport = new NullAudioPlaybackTransport();
        var buffer = new AudioBuffer(new AudioFormatDescriptor(8, 1, 32, AudioEncoding.Float32), new[] { new float[8] });
        transport.Load(buffer);
        await transport.SeekAsync(3);
        Assert.Equal(3, transport.PositionSamples);
        await transport.PlayAsync();
        Assert.Equal(AudioTransportState.Playing, transport.State);
        await transport.PauseAsync();
        Assert.Equal(AudioTransportState.Paused, transport.State);
    }
}
