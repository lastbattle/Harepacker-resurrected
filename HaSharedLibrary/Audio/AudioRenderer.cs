#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio;

public sealed class AudioRenderRequest
{
    public AudioProject Project { get; set; } = AudioProject.Create();
    public Func<AudioSourceReference, CancellationToken, ValueTask<AudioDecodeResult>>? SourceResolver { get; set; }

    public Func<AudioSourceReference, ValueTask<AudioDecodeResult>>? SourceProvider { get; set; }
    public AudioFormatDescriptor? OutputFormat { get; set; }
    public long StartSample { get; set; }
    public long? LengthSamples { get; set; }
    public bool IncludeMutedTracks { get; set; }

    public AudioRenderRequest()
    {
    }

    public AudioRenderRequest(AudioProject project,
        Func<AudioSourceReference, CancellationToken, ValueTask<AudioDecodeResult>> sourceResolver)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        SourceResolver = sourceResolver ?? throw new ArgumentNullException(nameof(sourceResolver));
    }

    public AudioRenderRequest(AudioProject project,
        Func<AudioSourceReference, ValueTask<AudioDecodeResult>> sourceProvider)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        SourceProvider = sourceProvider ?? throw new ArgumentNullException(nameof(sourceProvider));
    }
}

public sealed class AudioRenderResult
{
    public AudioRenderResult(AudioBuffer buffer, IReadOnlyList<AudioDiagnostic>? diagnostics = null)
    {
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        Diagnostics = diagnostics ?? Array.Empty<AudioDiagnostic>();
    }

    public AudioBuffer Buffer { get; }
    public IReadOnlyList<AudioDiagnostic> Diagnostics { get; }
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.IsError);
}

public interface IAudioRenderer
{
    ValueTask<AudioRenderResult> RenderAsync(AudioRenderRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<AudioEncodeResult> RenderToAsync(AudioRenderRequest request, IAudioCodecProvider codecProvider,
        AudioEncodeSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>Simple deterministic render graph for clip gain/pan/fades and core gain effects.</summary>
public sealed class AudioRenderer : IAudioRenderer
{
    public async ValueTask<AudioRenderResult> RenderAsync(AudioRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Project);
        request.Project.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        if (request.StartSample < 0)
            throw new ArgumentOutOfRangeException(nameof(request.StartSample));

        var outputFormat = (request.OutputFormat ?? request.Project.MasterFormat).Clone();
        outputFormat.Encoding = AudioEncoding.Float32;
        outputFormat.BitsPerSample = 32;
        outputFormat.Validate();
        var projectSampleRate = request.Project.MasterFormat.SampleRate;
        var renderStart = (long)Math.Round(request.StartSample * outputFormat.SampleRate /
            (double)projectSampleRate, MidpointRounding.AwayFromZero);
        var diagnostics = new List<AudioDiagnostic>();
        var busesById = request.Project.Buses.ToDictionary(bus => bus.Id);
        var hasSolo = request.Project.Tracks.Any(track => track.Solo && !track.Mute);
        var hasSoloBus = request.Project.Buses.Any(bus => bus.Solo && !bus.Mute);
        var resolvedClips = new List<ResolvedClip>();
        foreach (var track in request.Project.Tracks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((!request.IncludeMutedTracks && track.Mute) || (hasSolo && !track.Solo))
                continue;
            var bus = track.BusRoute is { } busId && busesById.TryGetValue(busId, out var routedBus)
                ? routedBus
                : null;
            if (bus is not null && ((!request.IncludeMutedTracks && bus.Mute) || (hasSoloBus && !bus.Solo)))
                continue;
            foreach (var clip in track.Clips)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((!request.IncludeMutedTracks && clip.Muted) || clip.DurationSample < 0)
                    continue;
                if (request.SourceResolver is null && request.SourceProvider is null)
                {
                    diagnostics.Add(new AudioDiagnostic(AudioDiagnosticCode.MissingSource,
                        $"No source resolver was provided for clip {clip.Id}.", true));
                    continue;
                }
                try
                {
                    var source = request.SourceResolver is not null
                        ? await request.SourceResolver(clip.SourceReference, cancellationToken).ConfigureAwait(false)
                        : await request.SourceProvider!(clip.SourceReference).ConfigureAwait(false);
                    resolvedClips.Add(new ResolvedClip(track, clip, bus, source));
                    diagnostics.AddRange(source.Diagnostics);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    diagnostics.Add(new AudioDiagnostic(AudioDiagnosticCode.MissingSource,
                        $"Could not resolve clip {clip.Id}: {exception.Message}", true));
                }
            }
        }
        var projectEnd = GetProjectEnd(resolvedClips, projectSampleRate);
        var fullLength = Math.Max(0, (long)Math.Ceiling(projectEnd * outputFormat.SampleRate /
            (double)projectSampleRate) - renderStart);
        var outputLength = request.LengthSamples ?? fullLength;
        if (outputLength < 0 || outputLength > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request.LengthSamples));

        var output = AudioBuffer.Silence(outputFormat, outputLength);
        foreach (var resolved in resolvedClips)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var effects = resolved.Clip.Effects.Concat(resolved.Track.Effects)
                .Concat(resolved.Bus?.Effects ?? Enumerable.Empty<AudioEffectNode>()).ToArray();
            var trackVolume = resolved.Track.Volume * (resolved.Bus?.Volume ?? 1);
            var trackPan = resolved.Track.Pan + (resolved.Bus?.Pan ?? 0);
            MixClip(output, resolved.Track, resolved.Clip, resolved.Source.Buffer, projectSampleRate, renderStart,
                trackVolume, trackPan, effects, cancellationToken);
        }
        return new AudioRenderResult(output, diagnostics);
    }

    public async ValueTask<AudioEncodeResult> RenderToAsync(AudioRenderRequest request,
        IAudioCodecProvider codecProvider, AudioEncodeSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codecProvider);
        var rendered = await RenderAsync(request, cancellationToken).ConfigureAwait(false);
        return await codecProvider.EncodeAsync(rendered.Buffer, settings, cancellationToken).ConfigureAwait(false);
    }

    private static double GetProjectEnd(IEnumerable<ResolvedClip> clips, int projectSampleRate)
        => clips.Select(item =>
        {
            var clip = item.Clip;
            var sourceDuration = item.Source.Buffer.SampleCount * projectSampleRate /
                (double)item.Source.Buffer.Format.SampleRate;
            var clipLength = clip.DurationSample > 0 ? clip.DurationSample : sourceDuration;
            return clip.StartSample + clipLength / Math.Max(0.000001, clip.StretchRatio);
        }).DefaultIfEmpty(0).Max();

    private sealed record ResolvedClip(AudioTrack Track, AudioClip Clip, AudioBus? Bus, AudioDecodeResult Source);

    private static void MixClip(AudioBuffer output, AudioTrack track, AudioClip clip, AudioBuffer source,
        int projectSampleRate, long renderStartTarget, double trackVolume, double trackPan,
        IReadOnlyList<AudioEffectNode> effects, CancellationToken cancellationToken)
    {
        var sourceRate = source.Format.SampleRate;
        var targetRate = output.Format.SampleRate;
        var clipStart = clip.StartSample * targetRate / (double)projectSampleRate;
        var clipLength = clip.DurationSample <= 0
            ? source.SampleCount * projectSampleRate / (double)sourceRate
            : clip.DurationSample;
        var sourceOffset = clip.SourceOffsetSample;
        var sourceToTarget = targetRate / (double)projectSampleRate;
        var targetEnd = clipStart + clipLength * sourceToTarget / Math.Max(0.000001, clip.StretchRatio);
        var first = Math.Max(0, (long)Math.Floor(clipStart - renderStartTarget));
        var last = Math.Min(output.SampleCount, (long)Math.Ceiling(targetEnd - renderStartTarget));
        if (last <= first)
            return;
        var pan = Math.Clamp(clip.Pan + trackPan, -1, 1);
        var leftPan = Math.Sqrt((1 - pan) * .5);
        var rightPan = Math.Sqrt((1 + pan) * .5);
        var gain = clip.Gain * trackVolume;
        var effectRuntime = new EffectRuntime(effects, source.Format.SampleRate);
        var sample = new float[source.Format.ChannelCount];
        for (var targetSample = first; targetSample < last; targetSample++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localTarget = targetSample + renderStartTarget - clipStart;
            var sourcePosition = sourceOffset + localTarget / targetRate * sourceRate * clip.StretchRatio;
            if (sourcePosition < 0 || sourcePosition >= source.SampleCount)
                continue;
            var sourceIndex = (int)Math.Min(source.SampleCount - 1, Math.Floor(sourcePosition));
            var nextIndex = Math.Min(sourceIndex + 1, (int)source.SampleCount - 1);
            var fraction = sourcePosition - sourceIndex;
            var localSourceSample = localTarget / sourceToTarget * Math.Max(0.000001, clip.StretchRatio);
            var fade = 1d;
            if (clip.FadeInSample > 0 && localSourceSample < clip.FadeInSample)
                fade *= Math.Clamp(localSourceSample / clip.FadeInSample, 0, 1);
            var remainingSource = clipLength - localSourceSample;
            if (clip.FadeOutSample > 0 && remainingSource < clip.FadeOutSample)
                fade *= Math.Clamp(remainingSource / clip.FadeOutSample, 0, 1);
            for (var channel = 0; channel < sample.Length; channel++)
                sample[channel] = source.Samples[channel][sourceIndex] * (float)(1 - fraction) +
                    source.Samples[channel][nextIndex] * (float)fraction;
            effectRuntime.Process(sample, (long)Math.Max(0, Math.Round(localSourceSample,
                MidpointRounding.AwayFromZero)));

            var timelineSample = (long)Math.Max(0, Math.Round((targetSample + renderStartTarget) /
                sourceToTarget, MidpointRounding.AwayFromZero));
            var automationGain = EvaluateAutomation(track.Automation, "volume", timelineSample, 1);
            var automationPan = EvaluateAutomation(track.Automation, "pan", timelineSample, 0);
            var mixedGain = gain * automationGain;
            var mixedPan = Math.Clamp(pan + automationPan, -1, 1);
            var mixedLeftPan = Math.Sqrt((1 - mixedPan) * .5);
            var mixedRightPan = Math.Sqrt((1 + mixedPan) * .5);

            if (output.Format.ChannelCount == 1)
            {
                var mono = sample.Average() * (float)(mixedGain * fade);
                output.Samples[0][(int)targetSample] += mono;
            }
            else
            {
                var left = sample[0] * (float)(mixedGain * fade * mixedLeftPan);
                var right = (sample.Length > 1 ? sample[1] : sample[0]) * (float)(mixedGain * fade * mixedRightPan);
                output.Samples[0][(int)targetSample] += left;
                output.Samples[1][(int)targetSample] += right;
                for (var channel = 2; channel < output.Format.ChannelCount; channel++)
                    output.Samples[channel][(int)targetSample] += sample[Math.Min(channel, sample.Length - 1)] * (float)(mixedGain * fade);
            }
        }
    }

    private static double EvaluateAutomation(IEnumerable<AudioAutomationLane> lanes, string parameter,
        long sample, double fallback)
    {
        var lane = lanes.FirstOrDefault(candidate => string.Equals(candidate.Parameter, parameter,
            StringComparison.OrdinalIgnoreCase));
        if (lane?.Points is not { Count: > 0 })
            return fallback;
        var points = lane.Points.OrderBy(point => point.Sample).ToArray();
        if (sample <= points[0].Sample)
            return points[0].Value;
        if (sample >= points[^1].Sample)
            return points[^1].Value;
        for (var index = 1; index < points.Length; index++)
        {
            var next = points[index];
            if (sample > next.Sample)
                continue;
            var previous = points[index - 1];
            if (string.Equals(previous.Interpolation, "step", StringComparison.OrdinalIgnoreCase))
                return previous.Value;
            var fraction = (sample - previous.Sample) / (double)Math.Max(1, next.Sample - previous.Sample);
            return previous.Value + (next.Value - previous.Value) * fraction;
        }
        return fallback;
    }
}

public static class AudioDsp
{
    public static AudioBuffer Trim(AudioBuffer source, long startSample, long lengthSamples)
        => source.Slice(startSample, lengthSamples);

    public static (AudioBuffer Left, AudioBuffer Right) Split(AudioBuffer source, long splitSample)
    {
        if (splitSample < 0 || splitSample > source.SampleCount)
            throw new ArgumentOutOfRangeException(nameof(splitSample));
        return (source.Slice(0, splitSample), source.Slice(splitSample, source.SampleCount - splitSample));
    }

    public static AudioBuffer FadeIn(AudioBuffer source, long samples)
    {
        var result = source.Clone();
        var count = Math.Min(result.SampleCount, Math.Max(0, samples));
        for (var sample = 0; sample < count; sample++)
        {
            var gain = count <= 1 ? 1 : sample / (double)(count - 1);
            for (var channel = 0; channel < result.Format.ChannelCount; channel++)
                result.Samples[channel][(int)sample] *= (float)gain;
        }
        return result;
    }

    public static AudioBuffer FadeOut(AudioBuffer source, long samples)
    {
        var result = source.Clone();
        var count = Math.Min(result.SampleCount, Math.Max(0, samples));
        var start = result.SampleCount - count;
        for (var sample = 0; sample < count; sample++)
        {
            var gain = count <= 1 ? 1 : 1 - sample / (double)(count - 1);
            for (var channel = 0; channel < result.Format.ChannelCount; channel++)
                result.Samples[channel][(int)(start + sample)] *= (float)gain;
        }
        return result;
    }

    public static AudioBuffer Reverse(AudioBuffer source)
    {
        var result = source.Clone();
        foreach (var channel in result.Samples)
            Array.Reverse(channel);
        return result;
    }

    public static AudioBuffer Normalize(AudioBuffer source, float peak = 1f)
    {
        if (peak <= 0 || float.IsNaN(peak) || float.IsInfinity(peak))
            throw new ArgumentOutOfRangeException(nameof(peak));
        var result = source.Clone();
        var maximum = result.Samples.SelectMany(samples => samples).Select(Math.Abs).DefaultIfEmpty(0).Max();
        if (maximum <= 0)
            return result;
        var gain = peak / maximum;
        for (var channel = 0; channel < result.Format.ChannelCount; channel++)
            for (var sample = 0; sample < result.Samples[channel].Length; sample++)
                result.Samples[channel][sample] = Math.Clamp(result.Samples[channel][sample] * gain, -1, 1);
        return result;
    }

    public static AudioBuffer InsertSilence(AudioBuffer source, long atSample, long sampleCount)
    {
        if (atSample < 0 || atSample > source.SampleCount || sampleCount < 0 || sampleCount > int.MaxValue)
            throw new ArgumentOutOfRangeException();
        var result = new float[source.Format.ChannelCount][];
        for (var channel = 0; channel < result.Length; channel++)
        {
            result[channel] = new float[checked((int)(source.SampleCount + sampleCount))];
            Array.Copy(source.Samples[channel], 0, result[channel], 0, (int)atSample);
            Array.Copy(source.Samples[channel], (int)atSample, result[channel], (int)(atSample + sampleCount),
                (int)(source.SampleCount - atSample));
        }
        return new AudioBuffer(source.Format, result, true);
    }

    public static AudioBuffer RemoveSilence(AudioBuffer source, long startSample, long sampleCount)
    {
        if (startSample < 0 || sampleCount < 0 || startSample > source.SampleCount - sampleCount)
            throw new ArgumentOutOfRangeException(nameof(startSample));
        var result = new float[source.Format.ChannelCount][];
        for (var channel = 0; channel < result.Length; channel++)
        {
            result[channel] = new float[checked((int)(source.SampleCount - sampleCount))];
            Array.Copy(source.Samples[channel], 0, result[channel], 0, (int)startSample);
            Array.Copy(source.Samples[channel], (int)(startSample + sampleCount), result[channel], (int)startSample,
                (int)(source.SampleCount - startSample - sampleCount));
        }
        return new AudioBuffer(source.Format, result, true);
    }

    public static AudioBuffer SwapChannels(AudioBuffer source)
    {
        if (source.Format.ChannelCount < 2)
            return source.Clone();
        var result = source.Clone();
        (result.Samples[0], result.Samples[1]) = (result.Samples[1], result.Samples[0]);
        return result;
    }

    public static AudioBuffer ToMono(AudioBuffer source)
    {
        if (source.Format.ChannelCount == 1)
            return source.Clone();
        var format = source.Format.Clone();
        format.ChannelCount = 1;
        format.ChannelLayout = "mono";
        var mono = new float[source.SampleCount];
        for (var sample = 0; sample < source.SampleCount; sample++)
        {
            var value = 0d;
            for (var channel = 0; channel < source.Format.ChannelCount; channel++)
                value += source.Samples[channel][sample];
            mono[sample] = (float)(value / source.Format.ChannelCount);
        }
        return new AudioBuffer(format, new[] { mono }, true);
    }

    public static AudioBuffer Resample(AudioBuffer source, int sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (sampleRate == source.Format.SampleRate)
            return source.Clone();
        var format = source.Format.Clone();
        format.SampleRate = sampleRate;
        var count = (long)Math.Round(source.SampleCount * sampleRate / (double)source.Format.SampleRate,
            MidpointRounding.AwayFromZero);
        if (count > int.MaxValue)
            throw new InvalidOperationException("The resampled buffer is too large.");
        var samples = new float[format.ChannelCount][];
        for (var channel = 0; channel < samples.Length; channel++)
        {
            samples[channel] = new float[(int)count];
            for (var index = 0; index < count; index++)
            {
                var sourcePosition = index * source.Format.SampleRate / (double)sampleRate;
                var sourceIndex = Math.Min((int)source.SampleCount - 1, (int)Math.Floor(sourcePosition));
                var next = Math.Min(sourceIndex + 1, (int)source.SampleCount - 1);
                var fraction = sourcePosition - sourceIndex;
                samples[channel][index] = source.Samples[channel][sourceIndex] * (float)(1 - fraction) +
                    source.Samples[channel][next] * (float)fraction;
            }
        }
        return new AudioBuffer(format, samples, true);
    }
}

public static class AudioBufferEditor
{
    public static AudioBuffer Trim(AudioBuffer source, long startSample, long lengthSamples) => AudioDsp.Trim(source, startSample, lengthSamples);
    public static (AudioBuffer Left, AudioBuffer Right) Split(AudioBuffer source, long splitSample) => AudioDsp.Split(source, splitSample);
    public static AudioBuffer FadeIn(AudioBuffer source, long samples) => AudioDsp.FadeIn(source, samples);
    public static AudioBuffer FadeOut(AudioBuffer source, long samples) => AudioDsp.FadeOut(source, samples);
    public static AudioBuffer Reverse(AudioBuffer source) => AudioDsp.Reverse(source);
    public static AudioBuffer Normalize(AudioBuffer source, float peak = 1f) => AudioDsp.Normalize(source, peak);
    public static AudioBuffer Resample(AudioBuffer source, int sampleRate) => AudioDsp.Resample(source, sampleRate);
}
