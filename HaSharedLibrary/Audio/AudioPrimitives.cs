#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using NAudio.Wave;

namespace HaSharedLibrary.Audio;

/// <summary>Encoding used by an audio source or an exported file.</summary>
public enum AudioEncoding
{
    Unknown = 0,
    Pcm = 1,
    Mp3 = 2,
    Float32 = 3,
    Raw = 4,
}

/// <summary>Origin of a source referenced by an Audio Studio project.</summary>
public enum AudioSourceKind
{
    Unknown = 0,
    NativeWz = 1,
    ExternalFile = 2,
    ProjectMedia = 3,
}

/// <summary>Describes the encoded or decoded sample format without owning any audio data.</summary>
public sealed class AudioFormatDescriptor : IEquatable<AudioFormatDescriptor>
{
    public AudioFormatDescriptor()
    {
    }

    public AudioFormatDescriptor(int sampleRate, int channelCount, int bitsPerSample, AudioEncoding encoding,
        string? channelLayout = null)
    {
        SampleRate = sampleRate;
        ChannelCount = channelCount;
        BitsPerSample = bitsPerSample;
        Encoding = encoding;
        ChannelLayout = channelLayout;
        Validate();
    }

    public int SampleRate { get; set; }

    public int ChannelCount { get; set; }

    [JsonIgnore]
    public int Channels { get => ChannelCount; set => ChannelCount = value; }

    [JsonIgnore]
    public int SampleRateHz { get => SampleRate; set => SampleRate = value; }

    /// <summary>Bits in each PCM sample. MP3 sources normally report the decoder's output depth.</summary>
    public int BitsPerSample { get; set; }

    [JsonIgnore]
    public int BitDepth { get => BitsPerSample; set => BitsPerSample = value; }

    public AudioEncoding Encoding { get; set; }

    /// <summary>Optional human-readable layout (for example, "stereo" or "5.1").</summary>
    public string? ChannelLayout { get; set; }

    [JsonIgnore]
    public bool IsPcm => Encoding is AudioEncoding.Pcm or AudioEncoding.Float32;

    [JsonIgnore]
    public int BlockAlign => Math.Max(1, (ChannelCount * Math.Max(1, BitsPerSample)) / 8);

    public static AudioFormatDescriptor FromWaveFormat(WaveFormat format, AudioEncoding? sourceEncoding = null)
    {
        ArgumentNullException.ThrowIfNull(format);
        var encoding = sourceEncoding ?? format.Encoding switch
        {
            WaveFormatEncoding.MpegLayer3 => AudioEncoding.Mp3,
            WaveFormatEncoding.IeeeFloat => AudioEncoding.Float32,
            WaveFormatEncoding.Pcm => AudioEncoding.Pcm,
            _ => AudioEncoding.Unknown,
        };

        return new AudioFormatDescriptor(format.SampleRate, format.Channels, format.BitsPerSample, encoding,
            format.Channels switch
            {
                1 => "mono",
                2 => "stereo",
                6 => "5.1",
                8 => "7.1",
                _ => null,
            });
    }

    public WaveFormat ToWaveFormat()
    {
        Validate();
        return Encoding == AudioEncoding.Float32
            ? WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount)
            : new WaveFormat(SampleRate, Math.Max(8, BitsPerSample), ChannelCount);
    }

    public void Validate()
    {
        if (SampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(SampleRate), "Sample rate must be positive.");
        if (ChannelCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ChannelCount), "Channel count must be positive.");
        if (BitsPerSample is < 0 or > 64)
            throw new ArgumentOutOfRangeException(nameof(BitsPerSample));
    }

    public bool Equals(AudioFormatDescriptor? other)
        => other is not null && SampleRate == other.SampleRate && ChannelCount == other.ChannelCount &&
           BitsPerSample == other.BitsPerSample && Encoding == other.Encoding &&
           string.Equals(ChannelLayout, other.ChannelLayout, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as AudioFormatDescriptor);

    public override int GetHashCode()
        => HashCode.Combine(SampleRate, ChannelCount, BitsPerSample, Encoding,
            ChannelLayout is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(ChannelLayout));

    public AudioFormatDescriptor Clone() => new(SampleRate, ChannelCount, BitsPerSample, Encoding, ChannelLayout);

    public override string ToString()
        => $"{SampleRate} Hz, {ChannelCount} ch, {BitsPerSample} bit, {Encoding}";
}

/// <summary>Metadata retained independently from the decoded sample buffer.</summary>
public sealed class AudioClipMetadata
{
    public AudioFormatDescriptor? OriginalFormat { get; set; }

    public AudioFormatDescriptor? DecodedFormat { get; set; }

    public AudioEncoding OriginalEncoding
    {
        get => OriginalFormat?.Encoding ?? AudioEncoding.Unknown;
        set
        {
            OriginalFormat ??= new AudioFormatDescriptor();
            OriginalFormat.Encoding = value;
        }
    }

    public int SampleRate => (DecodedFormat ?? OriginalFormat)?.SampleRate ?? 0;

    public int ChannelCount => (DecodedFormat ?? OriginalFormat)?.ChannelCount ?? 0;

    public int BitsPerSample => (DecodedFormat ?? OriginalFormat)?.BitsPerSample ?? 0;

    public long PayloadSizeBytes { get; set; }

    /// <summary>Duration declared by WZ, in milliseconds. Null means that no declaration exists.</summary>
    public long? DeclaredDurationMilliseconds { get; set; }

    /// <summary>Duration measured by the decoder, in milliseconds.</summary>
    public long? DecodedDurationMilliseconds { get; set; }

    [JsonIgnore]
    public long? DeclaredDurationMs { get => DeclaredDurationMilliseconds; set => DeclaredDurationMilliseconds = value; }

    [JsonIgnore]
    public long? DecodedDurationMs { get => DecodedDurationMilliseconds; set => DecodedDurationMilliseconds = value; }

    [JsonIgnore]
    public TimeSpan? DeclaredDuration => DeclaredDurationMilliseconds is { } value
        ? TimeSpan.FromMilliseconds(value)
        : null;

    [JsonIgnore]
    public TimeSpan? DecodedDuration => DecodedDurationMilliseconds is { } value
        ? TimeSpan.FromMilliseconds(value)
        : null;

    [JsonIgnore]
    public long DurationMismatchMilliseconds => DeclaredDurationMilliseconds is { } declared &&
        DecodedDurationMilliseconds is { } decoded ? decoded - declared : 0;

    [JsonIgnore]
    public bool HasDurationMismatch => Math.Abs(DurationMismatchMilliseconds) > 2;

    public string? SourceVersion { get; set; }

    public bool IsNativeWz { get; set; }

    public bool IsTruncated { get; set; }

    public bool IsLossy => OriginalEncoding == AudioEncoding.Mp3;

    public AudioClipMetadata Clone() => new()
    {
        OriginalFormat = OriginalFormat?.Clone(),
        DecodedFormat = DecodedFormat?.Clone(),
        PayloadSizeBytes = PayloadSizeBytes,
        DeclaredDurationMilliseconds = DeclaredDurationMilliseconds,
        DecodedDurationMilliseconds = DecodedDurationMilliseconds,
        SourceVersion = SourceVersion,
        IsNativeWz = IsNativeWz,
        IsTruncated = IsTruncated,
    };
}

/// <summary>Stable, serializable reference to native WZ/IMG or external project media.</summary>
public sealed class AudioSourceReference : IEquatable<AudioSourceReference>
{
    public AudioSourceKind SourceKind { get; set; }

    public string? SourceId { get; set; }

    public string? Category { get; set; }

    public string? ImagePath { get; set; }

    public string? PropertyPath { get; set; }

    /// <summary>Path is relative to the project file when possible.</summary>
    public string? ExternalPath { get; set; }

    public string? ContentHash { get; set; }

    public AudioClipMetadata? FormatMetadata { get; set; }

    public bool IsExternal => SourceKind is AudioSourceKind.ExternalFile or AudioSourceKind.ProjectMedia;

    public string? ResolveExternalPath(string? projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(ExternalPath))
            return null;
        if (Path.IsPathRooted(ExternalPath) || string.IsNullOrWhiteSpace(projectDirectory))
            return Path.GetFullPath(ExternalPath);
        return Path.GetFullPath(Path.Combine(projectDirectory, ExternalPath));
    }

    public AudioSourceReference Clone() => new()
    {
        SourceKind = SourceKind,
        SourceId = SourceId,
        Category = Category,
        ImagePath = ImagePath,
        PropertyPath = PropertyPath,
        ExternalPath = ExternalPath,
        ContentHash = ContentHash,
        FormatMetadata = FormatMetadata?.Clone(),
    };

    public bool Equals(AudioSourceReference? other)
        => other is not null && SourceKind == other.SourceKind &&
           string.Equals(SourceId, other.SourceId, StringComparison.Ordinal) &&
           string.Equals(ImagePath, other.ImagePath, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(PropertyPath, other.PropertyPath, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(ExternalPath, other.ExternalPath, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as AudioSourceReference);

    public override int GetHashCode()
        => HashCode.Combine(SourceKind, SourceId, ImagePath?.ToUpperInvariant(), PropertyPath?.ToUpperInvariant(),
            ExternalPath?.ToUpperInvariant());

    public override string ToString()
        => IsExternal ? ExternalPath ?? SourceId ?? "(external audio)" :
            string.Join("/", new[] { ImagePath, PropertyPath }.Where(value => !string.IsNullOrWhiteSpace(value)));
}

/// <summary>Decoded, non-interleaved 32-bit floating point samples.</summary>
public sealed class AudioBuffer
{
    public AudioBuffer(AudioFormatDescriptor format, float[][] channels, bool takeOwnership = false)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(channels);
        format.Validate();
        if (channels.Length != format.ChannelCount)
            throw new ArgumentException("Channel count does not match the format.", nameof(channels));
        if (channels.Any(channel => channel is null))
            throw new ArgumentException("A channel cannot be null.", nameof(channels));
        var sampleCount = channels.Length == 0 ? 0 : channels[0].Length;
        if (channels.Any(channel => channel.Length != sampleCount))
            throw new ArgumentException("Every channel must have the same sample count.", nameof(channels));

        Format = format.Clone();
        Samples = takeOwnership ? channels : channels.Select(channel => channel.ToArray()).ToArray();
        SampleCount = sampleCount;
    }

    public AudioFormatDescriptor Format { get; }

    /// <summary>Samples[channel][sample], never interleaved.</summary>
    public float[][] Samples { get; }

    public float[][] Channels => Samples;

    public long SampleCount { get; }

    public TimeSpan Duration => TimeSpan.FromSeconds(SampleCount / (double)Format.SampleRate);

    public AudioBuffer Clone() => new(Format, Samples);

    public AudioBuffer Slice(long startSample, long lengthSamples)
    {
        if (startSample < 0 || lengthSamples < 0 || startSample > SampleCount - lengthSamples)
            throw new ArgumentOutOfRangeException(nameof(startSample));
        if (lengthSamples > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(lengthSamples), "A single in-memory channel cannot exceed Int32.MaxValue samples.");

        var channels = new float[Format.ChannelCount][];
        for (var channel = 0; channel < channels.Length; channel++)
        {
            channels[channel] = new float[(int)lengthSamples];
            Array.Copy(Samples[channel], (int)startSample, channels[channel], 0, (int)lengthSamples);
        }
        return new AudioBuffer(Format, channels, true);
    }

    public float[] ToInterleaved()
    {
        if (SampleCount > int.MaxValue / Math.Max(1, Format.ChannelCount))
            throw new InvalidOperationException("The interleaved buffer is too large.");
        var result = new float[(int)SampleCount * Format.ChannelCount];
        var index = 0;
        for (var sample = 0; sample < SampleCount; sample++)
            for (var channel = 0; channel < Format.ChannelCount; channel++)
                result[index++] = Samples[channel][sample];
        return result;
    }

    public static AudioBuffer FromInterleaved(AudioFormatDescriptor format, ReadOnlySpan<float> interleaved)
    {
        format.Validate();
        if (interleaved.Length % format.ChannelCount != 0)
            throw new ArgumentException("Interleaved sample count must be divisible by the channel count.", nameof(interleaved));
        var sampleCount = interleaved.Length / format.ChannelCount;
        var channels = new float[format.ChannelCount][];
        for (var channel = 0; channel < channels.Length; channel++)
            channels[channel] = new float[sampleCount];
        var index = 0;
        for (var sample = 0; sample < sampleCount; sample++)
            for (var channel = 0; channel < channels.Length; channel++)
                channels[channel][sample] = interleaved[index++];
        return new AudioBuffer(format, channels, true);
    }

    public static AudioBuffer Silence(AudioFormatDescriptor format, long sampleCount)
    {
        if (sampleCount < 0 || sampleCount > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        var channels = new float[format.ChannelCount][];
        for (var channel = 0; channel < channels.Length; channel++)
            channels[channel] = new float[(int)sampleCount];
        return new AudioBuffer(format, channels, true);
    }
}

public sealed class AudioDiagnostic
{
    public AudioDiagnostic(AudioDiagnosticCode code, string message, bool isError = false)
    {
        Code = code;
        Message = message;
        IsError = isError;
    }

    public AudioDiagnosticCode Code { get; }
    public string Message { get; }
    public bool IsError { get; }

    public override string ToString() => $"{Code}: {Message}";
}

public enum AudioDiagnosticCode
{
    UnsupportedEncoding,
    MalformedHeader,
    TruncatedPayload,
    DurationMismatch,
    MissingSource,
    HashMismatch,
    Mp3EncoderUnavailable,
    DeviceUnavailable,
    RenderCancelled,
    CacheCorrupt,
}

public sealed class AudioDecodeResult
{
    public AudioDecodeResult(AudioBuffer buffer, AudioClipMetadata metadata,
        IReadOnlyList<AudioDiagnostic>? diagnostics = null)
    {
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Diagnostics = diagnostics ?? Array.Empty<AudioDiagnostic>();
    }

    public AudioBuffer Buffer { get; }
    public AudioClipMetadata Metadata { get; }
    public IReadOnlyList<AudioDiagnostic> Diagnostics { get; }
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.IsError);
}

public sealed class AudioEncodeSettings
{
    public AudioEncoding Encoding { get; set; } = AudioEncoding.Pcm;
    public int SampleRate { get; set; } = 44100;
    public int ChannelCount { get; set; } = 2;
    public int BitsPerSample { get; set; } = 16;
    public int Mp3BitrateKbps { get; set; } = 192;

    public AudioFormatDescriptor ToFormat() => new(SampleRate, ChannelCount, BitsPerSample, Encoding);
}

public sealed class AudioEncodeResult
{
    public AudioEncodeResult(byte[] data, AudioFormatDescriptor format,
        IReadOnlyList<AudioDiagnostic>? diagnostics = null)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Format = format ?? throw new ArgumentNullException(nameof(format));
        Diagnostics = diagnostics ?? Array.Empty<AudioDiagnostic>();
    }

    public byte[] Data { get; }
    public AudioFormatDescriptor Format { get; }
    public IReadOnlyList<AudioDiagnostic> Diagnostics { get; }
}
