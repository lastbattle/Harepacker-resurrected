#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace HaSharedLibrary.Audio;

public enum AudioTransportState
{
    Stopped = 0,
    Playing = 1,
    Paused = 2,
    Faulted = 3,
    Disposed = 4,
}

public sealed class AudioTransportPositionChangedEventArgs : EventArgs
{
    public AudioTransportPositionChangedEventArgs(long positionSamples, long durationSamples, int sampleRate = 44100)
    {
        PositionSamples = positionSamples;
        DurationSamples = durationSamples;
        SampleRate = sampleRate <= 0 ? 44100 : sampleRate;
    }

    public long PositionSamples { get; }
    public long DurationSamples { get; }
    public int SampleRate { get; }
    public TimeSpan Position => TimeSpan.FromSeconds(PositionSamples / (double)SampleRate);
}

public sealed class AudioTransportFaultEventArgs : EventArgs
{
    public AudioTransportFaultEventArgs(Exception exception) => Exception = exception;
    public Exception Exception { get; }
}

public interface IAudioPlaybackTransport : IDisposable, IAsyncDisposable
{
    AudioTransportState State { get; }
    AudioBuffer? Buffer { get; }
    long PositionSamples { get; }
    long DurationSamples { get; }
    bool LoopEnabled { get; set; }
    long LoopStartSample { get; set; }
    long LoopEndSample { get; set; }
    float Volume { get; set; }
    event EventHandler<AudioTransportPositionChangedEventArgs>? PositionChanged;
    event EventHandler<AudioTransportFaultEventArgs>? Faulted;

    void Load(AudioBuffer buffer);
    void Play();
    void Pause();
    void Stop();
    void Seek(long sample);
    Task PlayAsync(CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SeekAsync(long sample, CancellationToken cancellationToken = default);
}

/// <summary>
/// WASAPI shared-mode transport. The transport owns the NAudio output object and can be
/// recreated after a device failure; decoded samples remain owned by the caller.
/// </summary>
public class WasapiAudioPlaybackTransport : IAudioPlaybackTransport
{
    private readonly object gate = new();
    private WasapiOut? output;
    private PlaybackWaveProvider? provider;
    private CancellationTokenRegistration cancellationRegistration;
    private bool disposed;
    private AudioTransportState state = AudioTransportState.Stopped;
    private float volume = 1;

    public AudioTransportState State { get { lock (gate) return state; } }
    public AudioBuffer? Buffer { get; private set; }
    public long PositionSamples { get { lock (gate) return provider?.PositionSamples ?? 0; } }
    public long DurationSamples => Buffer?.SampleCount ?? 0;
    public bool LoopEnabled { get { lock (gate) return provider?.LoopEnabled ?? false; } set { lock (gate) { EnsureNotDisposed(); if (provider is not null) provider.LoopEnabled = value; } } }
    public long LoopStartSample
    {
        get { lock (gate) return provider?.LoopStartSample ?? 0; }
        set
        {
            lock (gate)
            {
                EnsureNotDisposed();
                if (provider is not null)
                    provider.LoopStartSample = Math.Clamp(value, 0, Buffer?.SampleCount ?? 0);
            }
        }
    }
    public long LoopEndSample
    {
        get { lock (gate) return provider?.LoopEndSample ?? DurationSamples; }
        set
        {
            lock (gate)
            {
                EnsureNotDisposed();
                if (provider is not null)
                    provider.LoopEndSample = Math.Clamp(value, 0, Buffer?.SampleCount ?? 0);
            }
        }
    }
    public float Volume
    {
        get => volume;
        set
        {
            if (value is < 0 or > 1 || float.IsNaN(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            volume = value;
            lock (gate)
            {
                if (output is not null)
                    output.Volume = value;
            }
        }
    }

    public event EventHandler<AudioTransportPositionChangedEventArgs>? PositionChanged;
    public event EventHandler<AudioTransportFaultEventArgs>? Faulted;

    public void Load(AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        lock (gate)
        {
            EnsureNotDisposed();
            cancellationRegistration.Dispose();
            cancellationRegistration = default;
            DisposeOutputLocked();
            Buffer = buffer;
            provider = new PlaybackWaveProvider(buffer);
            state = AudioTransportState.Stopped;
        }
        RaisePositionChanged();
    }

    public void Play()
    {
        Exception? fault = null;
        lock (gate)
        {
            EnsureNotDisposed();
            if (provider is null)
                throw new InvalidOperationException("Load an audio buffer before playing.");
            try
            {
                if (output is null)
                {
                    output = new WasapiOut(AudioClientShareMode.Shared, 100);
                    output.Init(provider);
                    output.Volume = volume;
                    output.PlaybackStopped += OutputOnPlaybackStopped;
                }
                output.Play();
                state = AudioTransportState.Playing;
            }
            catch (Exception exception)
            {
                state = AudioTransportState.Faulted;
                fault = exception;
            }
        }
        if (fault is not null)
        {
            RaiseFault(fault);
            throw new AudioCodecException(AudioDiagnosticCode.DeviceUnavailable,
                "The WASAPI playback device could not be opened.", fault);
        }
    }

    public void Pause()
    {
        lock (gate)
        {
            EnsureNotDisposed();
            output?.Pause();
            if (state == AudioTransportState.Playing)
                state = AudioTransportState.Paused;
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            EnsureNotDisposed();
            cancellationRegistration.Dispose();
            cancellationRegistration = default;
            output?.Stop();
            provider?.Seek(0);
            state = AudioTransportState.Stopped;
        }
        RaisePositionChanged();
    }

    public void Seek(long sample)
    {
        lock (gate)
        {
            EnsureNotDisposed();
            if (provider is null)
                throw new InvalidOperationException("Load an audio buffer before seeking.");
            provider.Seek(sample);
        }
        RaisePositionChanged();
    }

    public Task PlayAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Play();
        cancellationRegistration.Dispose();
        cancellationRegistration = cancellationToken.Register(static state =>
        {
            var transport = (WasapiAudioPlaybackTransport)state!;
            try { transport.Stop(); }
            catch (ObjectDisposedException) { /* cancellation racing disposal is benign */ }
        }, this);
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Pause();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stop();
        return Task.CompletedTask;
    }

    public Task SeekAsync(long sample, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Seek(sample);
        return Task.CompletedTask;
    }

    private void OutputOnPlaybackStopped(object? sender, StoppedEventArgs eventArgs)
    {
        Exception? fault = null;
        lock (gate)
        {
            if (disposed)
                return;
            if (eventArgs.Exception is not null)
            {
                state = AudioTransportState.Faulted;
                fault = eventArgs.Exception;
            }
            else if (state == AudioTransportState.Playing)
            {
                state = AudioTransportState.Stopped;
            }
        }
        if (fault is not null)
            RaiseFault(fault);
        RaisePositionChanged();
    }

    private void DisposeOutputLocked()
    {
        if (output is null)
            return;
        output.PlaybackStopped -= OutputOnPlaybackStopped;
        output.Dispose();
        output = null;
    }

    private void RaisePositionChanged()
        => PositionChanged?.Invoke(this, new AudioTransportPositionChangedEventArgs(PositionSamples, DurationSamples,
            Buffer?.Format.SampleRate ?? 44100));

    private void RaiseFault(Exception exception) => Faulted?.Invoke(this, new AudioTransportFaultEventArgs(exception));

    private void EnsureNotDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(WasapiAudioPlaybackTransport));
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            cancellationRegistration.Dispose();
            DisposeOutputLocked();
            provider = null;
            Buffer = null;
            state = AudioTransportState.Disposed;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class PlaybackWaveProvider : IWaveProvider
    {
        private readonly AudioBuffer buffer;
        private readonly float[] interleaved;
        private readonly object gate = new();
        private int position;
        private long loopStartSample;
        private long loopEndSample;

        public PlaybackWaveProvider(AudioBuffer buffer)
        {
            this.buffer = buffer;
            interleaved = buffer.ToInterleaved();
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(buffer.Format.SampleRate, buffer.Format.ChannelCount);
            loopEndSample = buffer.SampleCount;
        }

        public WaveFormat WaveFormat { get; }
        public bool LoopEnabled { get; set; }
        public long LoopStartSample
        {
            get { lock (gate) return loopStartSample; }
            set { lock (gate) loopStartSample = Math.Clamp(value, 0, buffer.SampleCount); }
        }
        public long LoopEndSample
        {
            get { lock (gate) return loopEndSample; }
            set { lock (gate) loopEndSample = Math.Clamp(value, 0, buffer.SampleCount); }
        }
        public long PositionSamples { get { lock (gate) return position / buffer.Format.ChannelCount; } }

        public void Seek(long sample)
        {
            lock (gate)
            {
                var clamped = Math.Clamp(sample, 0, buffer.SampleCount);
                position = (int)Math.Min(int.MaxValue, clamped * buffer.Format.ChannelCount);
            }
        }

        public int Read(byte[] destination, int offset, int count)
        {
            if (count <= 0)
                return 0;
            lock (gate)
            {
                var bytesPerSample = sizeof(float);
                var samplesRequested = count / bytesPerSample;
                var samplesWritten = 0;
                while (samplesWritten < samplesRequested)
                {
                    var loopStart = Math.Clamp(LoopStartSample, 0, buffer.SampleCount);
                    var loopEnd = Math.Clamp(LoopEndSample, loopStart, buffer.SampleCount);
                    var end = (int)Math.Min(interleaved.Length, loopEnd * buffer.Format.ChannelCount);
                    if (position >= end)
                    {
                        if (!LoopEnabled || loopEnd <= loopStart)
                            break;
                        position = (int)Math.Min(int.MaxValue, loopStart * buffer.Format.ChannelCount);
                    }
                    var available = Math.Min(samplesRequested - samplesWritten, end - position);
                    System.Buffer.BlockCopy(interleaved, position * bytesPerSample, destination,
                        offset + samplesWritten * bytesPerSample, available * bytesPerSample);
                    position += available;
                    samplesWritten += available;
                }
                return samplesWritten * bytesPerSample;
            }
        }
    }
}

/// <summary>Name used by editor hosts that do not need to mention the WASAPI backend.</summary>
public sealed class AudioPlaybackTransport : WasapiAudioPlaybackTransport
{
}

/// <summary>Deterministic transport for previews/tests where no audio device should be opened.</summary>
public sealed class NullAudioPlaybackTransport : IAudioPlaybackTransport
{
    private bool disposed;
    private AudioTransportState state;
    private AudioBuffer? buffer;
    private long position;
    private float volume = 1;
    private long loopStartSample;
    private long loopEndSample;

    public AudioTransportState State => disposed ? AudioTransportState.Disposed : state;
    public AudioBuffer? Buffer => buffer;
    public long PositionSamples => position;
    public long DurationSamples => buffer?.SampleCount ?? 0;
    public bool LoopEnabled { get; set; }
    public long LoopStartSample { get => loopStartSample; set => loopStartSample = Math.Clamp(value, 0, DurationSamples); }
    public long LoopEndSample { get => loopEndSample; set => loopEndSample = Math.Clamp(value, 0, DurationSamples); }
    public float Volume { get => volume; set => volume = Math.Clamp(value, 0, 1); }
    public event EventHandler<AudioTransportPositionChangedEventArgs>? PositionChanged;
    public event EventHandler<AudioTransportFaultEventArgs>? Faulted;

    public void Load(AudioBuffer value)
    {
        Ensure();
        buffer = value ?? throw new ArgumentNullException(nameof(value));
        position = 0;
        LoopStartSample = 0;
        LoopEndSample = DurationSamples;
        state = AudioTransportState.Stopped;
        RaisePositionChanged();
    }
    public void Play() { Ensure(); if (buffer is null) throw new InvalidOperationException("Load an audio buffer before playing."); state = AudioTransportState.Playing; }
    public void Pause() { Ensure(); if (state == AudioTransportState.Playing) state = AudioTransportState.Paused; }
    public void Stop() { Ensure(); state = AudioTransportState.Stopped; position = 0; RaisePositionChanged(); }
    public void Seek(long sample) { Ensure(); position = Math.Clamp(sample, 0, DurationSamples); RaisePositionChanged(); }
    public Task PlayAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Play(); return Task.CompletedTask; }
    public Task PauseAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Pause(); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Stop(); return Task.CompletedTask; }
    public Task SeekAsync(long sample, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Seek(sample); return Task.CompletedTask; }
    public void Dispose() { disposed = true; buffer = null; state = AudioTransportState.Disposed; }
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    private void Ensure() { if (disposed) throw new ObjectDisposedException(nameof(NullAudioPlaybackTransport)); }
    private void RaisePositionChanged()
        => PositionChanged?.Invoke(this, new AudioTransportPositionChangedEventArgs(position, DurationSamples,
            buffer?.Format.SampleRate ?? 44100));
}
