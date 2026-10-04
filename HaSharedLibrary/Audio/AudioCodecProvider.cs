#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MapleLib.WzLib.WzProperties;
using NAudio.Wave;

namespace HaSharedLibrary.Audio;

public interface IAudioCodecProvider
{
    bool CanDecode(AudioSourceReference source);

    AudioClipMetadata ReadMetadata(Stream input, string extension,
        AudioClipMetadata? sourceMetadata = null);

    AudioClipMetadata ReadMetadata(WzBinaryProperty sound);

    ValueTask<AudioDecodeResult> DecodeAsync(AudioSourceReference source,
        CancellationToken cancellationToken = default);

    ValueTask<AudioDecodeResult> DecodeAsync(Stream input, string extension,
        AudioClipMetadata? sourceMetadata = null, CancellationToken cancellationToken = default);

    ValueTask<AudioDecodeResult> DecodeAsync(WzBinaryProperty sound,
        CancellationToken cancellationToken = default);

    ValueTask<AudioEncodeResult> EncodeAsync(AudioBuffer buffer, AudioEncodeSettings settings,
        CancellationToken cancellationToken = default);
}

public class AudioCodecException : Exception
{
    public AudioCodecException(AudioDiagnosticCode code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public AudioDiagnosticCode Code { get; }
}

/// <summary>
/// NAudio-backed MP3/PCM WAV codec provider. The provider keeps WZ metadata separate from
/// decoder metadata and never stores decoded samples in project files.
/// </summary>
public sealed class NAudioCodecProvider : IAudioCodecProvider
{
    public bool CanDecode(AudioSourceReference source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SourceKind == AudioSourceKind.NativeWz)
            return source.FormatMetadata?.OriginalEncoding is AudioEncoding.Mp3 or AudioEncoding.Pcm;
        if (!source.IsExternal || string.IsNullOrWhiteSpace(source.ExternalPath))
            return false;
        return IsSupportedExtension(Path.GetExtension(source.ExternalPath));
    }

    public AudioClipMetadata ReadMetadata(Stream input, string extension,
        AudioClipMetadata? sourceMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
            throw new ArgumentException("The audio stream must be readable.", nameof(input));

        var bytes = CopyToMemory(input);
        try
        {
            using var reader = OpenReader(bytes, extension);
            var originalFormat = GetOriginalFormat(reader, extension);
            var metadata = sourceMetadata?.Clone() ?? new AudioClipMetadata();
            metadata.OriginalFormat = originalFormat;
            metadata.DecodedFormat = AudioFormatDescriptor.FromWaveFormat(reader.WaveFormat,
                reader.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat ? AudioEncoding.Float32 : AudioEncoding.Float32);
            metadata.PayloadSizeBytes = bytes.Length;
            metadata.DecodedDurationMilliseconds = (long)Math.Round(reader.TotalTime.TotalMilliseconds,
                MidpointRounding.AwayFromZero);
            return metadata;
        }
        catch (AudioCodecException)
        {
            throw;
        }
        catch (EndOfStreamException exception)
        {
            throw new AudioCodecException(AudioDiagnosticCode.TruncatedPayload,
                "The audio payload ended before the declared data.", exception);
        }
        catch (InvalidDataException exception)
        {
            throw new AudioCodecException(AudioDiagnosticCode.MalformedHeader,
                "The audio header is malformed or unsupported.", exception);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new AudioCodecException(AudioDiagnosticCode.MalformedHeader,
                "The audio header is malformed or unsupported.", exception);
        }
    }

    public AudioClipMetadata ReadMetadata(WzBinaryProperty sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        var encoding = sound.SoundType switch
        {
            WzBinaryPropertyType.MP3 => AudioEncoding.Mp3,
            WzBinaryPropertyType.WAV => AudioEncoding.Pcm,
            _ => AudioEncoding.Unknown,
        };
        if (encoding == AudioEncoding.Unknown)
            throw new AudioCodecException(AudioDiagnosticCode.UnsupportedEncoding,
                "Only WZ MP3 and PCM WAV properties can be inspected.");
        var original = sound.WavFormat is null ? null : AudioFormatDescriptor.FromWaveFormat(sound.WavFormat, encoding);
        var metadata = new AudioClipMetadata
        {
            OriginalFormat = original,
            // WzBinaryProperty intentionally exposes payload bytes lazily. Do not
            // force a disk read for a metadata-only catalog operation.
            PayloadSizeBytes = 0,
            DeclaredDurationMilliseconds = sound.Length,
            IsNativeWz = true,
        };
        // A metadata-only read intentionally avoids decoding bytes. WZ's header is
        // authoritative for format and duration; decoded values are filled on open.
        return metadata;
    }

    public async ValueTask<AudioDecodeResult> DecodeAsync(AudioSourceReference source,
        CancellationToken cancellationToken = default)
        => await DecodeAsync(source, baseDirectory: null, cancellationToken).ConfigureAwait(false);

    /// <summary>Decodes an external source resolving relative paths against the project directory.</summary>
    public async ValueTask<AudioDecodeResult> DecodeAsync(AudioSourceReference source, string? baseDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        if (source.SourceKind == AudioSourceKind.NativeWz)
            throw new AudioCodecException(AudioDiagnosticCode.MissingSource,
                "A native WZ source must be decoded with DecodeAsync(WzBinaryProperty).");

        var path = source.ResolveExternalPath(baseDirectory);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new AudioCodecException(AudioDiagnosticCode.MissingSource,
                $"The external audio source '{source.ExternalPath}' could not be found.");

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var metadata = source.FormatMetadata?.Clone();
        var result = await DecodeAsync(stream, Path.GetExtension(path), metadata, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(source.ContentHash))
        {
            stream.Position = 0;
            var actualHash = Convert.ToHexString(SHA256.HashData(await ReadAllAsync(stream, cancellationToken)))
                .ToLowerInvariant();
            if (!string.Equals(actualHash, source.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                var diagnostics = result.Diagnostics.Concat(new[] { new AudioDiagnostic(
                    AudioDiagnosticCode.HashMismatch,
                    "The external file hash differs from the project reference.") }).ToArray();
                return new AudioDecodeResult(result.Buffer, result.Metadata, diagnostics);
            }
        }
        return result;
    }

    public ValueTask<AudioDecodeResult> DecodeAsync(Stream input, string extension,
        AudioClipMetadata? sourceMetadata = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Decode(input, extension, sourceMetadata, cancellationToken));
    }

    public AudioDecodeResult Decode(Stream input, string extension,
        AudioClipMetadata? sourceMetadata = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = CopyToMemory(input);
        using var reader = OpenReader(bytes, extension);
        var originalFormat = GetOriginalFormat(reader, extension);
        var decodedFormat = AudioFormatDescriptor.FromWaveFormat(reader.WaveFormat, AudioEncoding.Float32);
        var sampleProvider = reader.ToSampleProvider();
        var samples = ReadAllSamples(sampleProvider, decodedFormat.ChannelCount, cancellationToken);
        var buffer = new AudioBuffer(decodedFormat, samples, true);

        var metadata = sourceMetadata?.Clone() ?? new AudioClipMetadata();
        metadata.OriginalFormat = originalFormat;
        metadata.DecodedFormat = decodedFormat;
        metadata.PayloadSizeBytes = bytes.Length;
        metadata.DecodedDurationMilliseconds = (long)Math.Round(buffer.Duration.TotalMilliseconds,
            MidpointRounding.AwayFromZero);
        var diagnostics = new List<AudioDiagnostic>();
        if (metadata.DeclaredDurationMilliseconds is { } declared && metadata.DecodedDurationMilliseconds is { } decoded &&
            Math.Abs(declared - decoded) > 2)
        {
            diagnostics.Add(new AudioDiagnostic(AudioDiagnosticCode.DurationMismatch,
                $"Declared duration ({declared} ms) differs from decoded duration ({decoded} ms)."));
        }
        return new AudioDecodeResult(buffer, metadata, diagnostics);
    }

    /// <summary>Decodes a native WZ sound property without changing or retaining its payload.</summary>
    public ValueTask<AudioDecodeResult> DecodeAsync(WzBinaryProperty sound,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sound);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Decode(sound, cancellationToken));
    }

    public AudioDecodeResult Decode(WzBinaryProperty sound,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sound);
        cancellationToken.ThrowIfCancellationRequested();
        var encoding = sound.SoundType switch
        {
            WzBinaryPropertyType.MP3 => AudioEncoding.Mp3,
            WzBinaryPropertyType.WAV => AudioEncoding.Pcm,
            _ => AudioEncoding.Unknown,
        };
        if (encoding == AudioEncoding.Unknown)
            throw new AudioCodecException(AudioDiagnosticCode.UnsupportedEncoding,
                "Only WZ MP3 and PCM WAV properties can be decoded.");

        var bytes = encoding == AudioEncoding.Pcm ? sound.GetBytesForWAVPlayback() : sound.GetBytes(false);
        if (bytes is null || bytes.Length == 0)
            throw new AudioCodecException(AudioDiagnosticCode.TruncatedPayload,
                "The WZ sound property has no payload.");

        var sourceMetadata = new AudioClipMetadata
        {
            OriginalFormat = sound.WavFormat is null
                ? null
                : AudioFormatDescriptor.FromWaveFormat(sound.WavFormat, encoding),
            DeclaredDurationMilliseconds = sound.Length,
            PayloadSizeBytes = bytes.Length,
            IsNativeWz = true,
        };
        using var stream = new MemoryStream(bytes, writable: false);
        return Decode(stream, encoding == AudioEncoding.Pcm ? ".wav" : ".mp3", sourceMetadata, cancellationToken);
    }

    public async ValueTask<AudioEncodeResult> EncodeAsync(AudioBuffer buffer, AudioEncodeSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        var output = ConvertBuffer(buffer, settings.ToFormat());
        return settings.Encoding switch
        {
            AudioEncoding.Pcm => new AudioEncodeResult(EncodeWave(output), output.Format),
            AudioEncoding.Mp3 => await EncodeMp3Async(output, settings.Mp3BitrateKbps, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new AudioCodecException(AudioDiagnosticCode.UnsupportedEncoding,
                $"Encoding '{settings.Encoding}' is not supported."),
        };
    }

    public AudioEncodeResult Encode(AudioBuffer buffer, AudioEncodeSettings settings)
        => EncodeAsync(buffer, settings).GetAwaiter().GetResult();

    public static bool IsSupportedExtension(string? extension)
        => string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase);

    private static WaveStream OpenReader(byte[] bytes, string extension)
    {
        var stream = new MemoryStream(bytes, writable: false);
        try
        {
            if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase))
                return new Mp3FileReader(stream);
            if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
            {
                var reader = new WaveFileReader(stream);
                if (reader.WaveFormat.Encoding is not (WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat))
                {
                    reader.Dispose();
                    throw new AudioCodecException(AudioDiagnosticCode.UnsupportedEncoding,
                        $"Only PCM WAV is supported; the file uses {reader.WaveFormat.Encoding}.");
                }
                return reader;
            }
            throw new AudioCodecException(AudioDiagnosticCode.UnsupportedEncoding,
                $"Unsupported audio extension '{extension}'.");
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static AudioFormatDescriptor GetOriginalFormat(WaveStream reader, string extension)
    {
        var format = reader.WaveFormat;
        var encoding = string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase)
            ? AudioEncoding.Mp3
            : format.Encoding == WaveFormatEncoding.IeeeFloat ? AudioEncoding.Float32 : AudioEncoding.Pcm;
        return AudioFormatDescriptor.FromWaveFormat(format, encoding);
    }

    private static float[][] ReadAllSamples(ISampleProvider provider, int channelCount,
        CancellationToken cancellationToken)
    {
        const int ChunkSamples = 32 * 1024;
        var channelData = Enumerable.Range(0, channelCount).Select(_ => new List<float>()).ToArray();
        var interleaved = new float[ChunkSamples * channelCount];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = provider.Read(interleaved, 0, interleaved.Length);
            if (read == 0)
                break;
            for (var i = 0; i < read; i++)
                channelData[i % channelCount].Add(interleaved[i]);
        }
        return channelData.Select(values => values.ToArray()).ToArray();
    }

    private static byte[] CopyToMemory(Stream input)
    {
        if (input is MemoryStream memory && memory.TryGetBuffer(out var segment))
            return segment.Array is null ? memory.ToArray() : segment.AsSpan().ToArray();
        if (input.CanSeek)
            input.Position = 0;
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static async Task<byte[]> ReadAllAsync(Stream input, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        return output.ToArray();
    }

    private static AudioBuffer ConvertBuffer(AudioBuffer input, AudioFormatDescriptor outputFormat)
    {
        outputFormat.Validate();
        var channelCount = outputFormat.ChannelCount;
        var sourceSamples = input.Samples;
        var targetCount = (long)Math.Round(input.SampleCount * outputFormat.SampleRate / (double)input.Format.SampleRate,
            MidpointRounding.AwayFromZero);
        if (targetCount > int.MaxValue)
            throw new InvalidOperationException("The converted audio is too large for an in-memory export.");
        var output = new float[channelCount][];
        for (var channel = 0; channel < channelCount; channel++)
            output[channel] = new float[(int)targetCount];

        for (var targetSample = 0; targetSample < targetCount; targetSample++)
        {
            var sourcePosition = targetSample * (double)input.Format.SampleRate / outputFormat.SampleRate;
            var sourceIndex = (int)Math.Min(input.SampleCount - 1, Math.Floor(sourcePosition));
            var nextIndex = Math.Min(sourceIndex + 1, (int)input.SampleCount - 1);
            var fraction = sourcePosition - sourceIndex;
            for (var channel = 0; channel < channelCount; channel++)
            {
                if (input.Format.ChannelCount == 1)
                    output[channel][targetSample] = sourceSamples[0][sourceIndex] * (float)(1 - fraction) +
                        sourceSamples[0][nextIndex] * (float)fraction;
                else if (channel < input.Format.ChannelCount)
                    output[channel][targetSample] = sourceSamples[channel][sourceIndex] * (float)(1 - fraction) +
                        sourceSamples[channel][nextIndex] * (float)fraction;
                else
                    output[channel][targetSample] = 0;
            }
        }
        return new AudioBuffer(outputFormat, output, true);
    }

    private static byte[] EncodeWave(AudioBuffer buffer)
    {
        var waveFormat = buffer.Format.Encoding == AudioEncoding.Float32
            ? WaveFormat.CreateIeeeFloatWaveFormat(buffer.Format.SampleRate, buffer.Format.ChannelCount)
            : new WaveFormat(buffer.Format.SampleRate, buffer.Format.BitsPerSample, buffer.Format.ChannelCount);
        var interleaved = buffer.ToInterleaved();
        using var stream = new MemoryStream();
        using (var writer = new WaveFileWriter(stream, waveFormat))
        {
            var bytes = FloatToPcm(interleaved, buffer.Format.BitsPerSample, buffer.Format.Encoding == AudioEncoding.Float32);
            writer.Write(bytes, 0, bytes.Length);
        }
        return stream.ToArray();
    }

    private static byte[] FloatToPcm(float[] values, int bitsPerSample, bool ieeeFloat)
    {
        if (ieeeFloat)
        {
            var bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }
        bitsPerSample = bitsPerSample switch { 8 or 16 or 24 or 32 => bitsPerSample, _ => 16 };
        var bytesPerSample = bitsPerSample / 8;
        var output = new byte[values.Length * bytesPerSample];
        for (var i = 0; i < values.Length; i++)
        {
            var sample = Math.Clamp(values[i], -1f, 1f);
            var max = (1L << (bitsPerSample - 1)) - 1;
            var min = -(1L << (bitsPerSample - 1));
            var integer = (long)Math.Round(sample * max, MidpointRounding.AwayFromZero);
            integer = Math.Clamp(integer, min, max);
            for (var byteIndex = 0; byteIndex < bytesPerSample; byteIndex++)
                output[i * bytesPerSample + byteIndex] = (byte)(integer >> (8 * byteIndex));
        }
        return output;
    }

    private static async ValueTask<AudioEncodeResult> EncodeMp3Async(AudioBuffer buffer, int bitrateKbps,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pcmFormat = new AudioFormatDescriptor(buffer.Format.SampleRate, buffer.Format.ChannelCount, 16,
            AudioEncoding.Pcm);
        var pcm = ConvertBuffer(buffer, pcmFormat);
        var bytes = FloatToPcm(pcm.ToInterleaved(), 16, false);
        var waveProvider = new ByteArrayWaveProvider(bytes,
            new WaveFormat(pcmFormat.SampleRate, pcmFormat.BitsPerSample, pcmFormat.ChannelCount));
        using var output = new MemoryStream();
        try
        {
            MediaFoundationEncoder.EncodeToMp3(waveProvider, output, Math.Clamp(bitrateKbps, 8, 320));
        }
        catch (Exception exception)
        {
            throw new AudioCodecException(AudioDiagnosticCode.Mp3EncoderUnavailable,
                "The Windows Media Foundation MP3 encoder is unavailable on this host.", exception);
        }
        await Task.CompletedTask.ConfigureAwait(false);
        return new AudioEncodeResult(output.ToArray(), new AudioFormatDescriptor(
            pcmFormat.SampleRate, pcmFormat.ChannelCount, 16, AudioEncoding.Mp3));
    }

    private sealed class ByteArrayWaveProvider : IWaveProvider
    {
        private readonly byte[] data;
        private int position;

        public ByteArrayWaveProvider(byte[] data, WaveFormat waveFormat)
        {
            this.data = data;
            WaveFormat = waveFormat;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(byte[] buffer, int offset, int count)
        {
            var available = Math.Min(count, data.Length - position);
            if (available <= 0)
                return 0;
            Buffer.BlockCopy(data, position, buffer, offset, available);
            position += available;
            return available;
        }
    }
}

/// <summary>Short name retained for callers that do not want to depend on NAudio's name.</summary>
public sealed class DefaultAudioCodecProvider : IAudioCodecProvider
{
    private readonly NAudioCodecProvider provider = new();

    public bool CanDecode(AudioSourceReference source) => provider.CanDecode(source);
    public AudioClipMetadata ReadMetadata(Stream input, string extension, AudioClipMetadata? sourceMetadata = null)
        => provider.ReadMetadata(input, extension, sourceMetadata);
    public AudioClipMetadata ReadMetadata(WzBinaryProperty sound) => provider.ReadMetadata(sound);
    public ValueTask<AudioDecodeResult> DecodeAsync(AudioSourceReference source, CancellationToken cancellationToken = default)
        => provider.DecodeAsync(source, cancellationToken);
    public ValueTask<AudioDecodeResult> DecodeAsync(AudioSourceReference source, string? baseDirectory,
        CancellationToken cancellationToken = default)
        => provider.DecodeAsync(source, baseDirectory, cancellationToken);
    public ValueTask<AudioDecodeResult> DecodeAsync(Stream input, string extension, AudioClipMetadata? sourceMetadata = null,
        CancellationToken cancellationToken = default)
        => provider.DecodeAsync(input, extension, sourceMetadata, cancellationToken);
    public ValueTask<AudioEncodeResult> EncodeAsync(AudioBuffer buffer, AudioEncodeSettings settings,
        CancellationToken cancellationToken = default)
        => provider.EncodeAsync(buffer, settings, cancellationToken);
    public AudioEncodeResult Encode(AudioBuffer buffer, AudioEncodeSettings settings)
        => provider.Encode(buffer, settings);

    public ValueTask<AudioDecodeResult> DecodeAsync(WzBinaryProperty sound,
        CancellationToken cancellationToken = default) => provider.DecodeAsync(sound, cancellationToken);

    public AudioDecodeResult Decode(WzBinaryProperty sound,
        CancellationToken cancellationToken = default) => provider.Decode(sound, cancellationToken);
}
