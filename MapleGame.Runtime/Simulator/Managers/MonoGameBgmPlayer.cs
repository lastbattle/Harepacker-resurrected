using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Diagnostics;

namespace HaCreator.MapSimulator.Managers
{
    internal sealed class MonoGameBgmPlayer : IDisposable
    {
        private readonly SoundEffect _soundEffect;
        private readonly SoundEffectInstance _instance;
        private bool _disposed;

        public MonoGameBgmPlayer(WzBinaryProperty sound, bool looped, float volume = 0.5f)
            : this(sound, looped, 0, volume)
        {
        }

        public MonoGameBgmPlayer(WzBinaryProperty sound, bool looped, int startOffsetMs, float volume = 0.5f)
        {
            SoundEffect soundEffect = MonoGameAudioFactory.CreateSoundEffect(
                sound,
                startOffsetMs,
                out TimeSpan availableDuration,
                out TimeSpan totalDuration,
                out TimeSpan actualOffset);
            SoundEffectInstance instance = null;
            try
            {
                instance = soundEffect.CreateInstance();
                instance.IsLooped = looped;
                instance.Volume = volume;
                _soundEffect = soundEffect;
                _instance = instance;
                Duration = availableDuration;
                TotalDuration = totalDuration;
                StartOffset = actualOffset;
            }
            catch
            {
                instance?.Dispose();
                soundEffect.Dispose();
                throw;
            }
        }

        public static bool TryCreate(WzBinaryProperty sound, bool looped, int startOffsetMs, float volume, out MonoGameBgmPlayer player)
        {
            try
            {
                player = new MonoGameBgmPlayer(sound, looped, startOffsetMs, volume);
                return true;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
            {
                Debug.WriteLine($"Unable to create BGM playback: {exception.Message}");
                player = null;
                return false;
            }
        }

        public SoundState State => _instance?.State ?? SoundState.Stopped;
        public TimeSpan Duration { get; }
        public TimeSpan TotalDuration { get; }
        public TimeSpan StartOffset { get; }

        public float Volume
        {
            get => _instance?.Volume ?? 0f;
            set
            {
                if (_disposed || _instance == null)
                {
                    return;
                }

                _instance.Volume = Math.Clamp(value, 0f, 1f);
            }
        }

        public void Play()
        {
            if (_disposed || _instance == null)
            {
                return;
            }

            switch (_instance.State)
            {
                case SoundState.Playing:
                    return;
                case SoundState.Paused:
                    _instance.Resume();
                    return;
                default:
                    _instance.Play();
                    return;
            }
        }

        public void Pause()
        {
            if (_disposed || _instance == null || _instance.State != SoundState.Playing)
            {
                return;
            }

            _instance.Pause();
        }

        public void Resume()
        {
            if (_disposed || _instance == null || _instance.State != SoundState.Paused)
            {
                return;
            }

            _instance.Resume();
        }

        public void Stop()
        {
            if (_disposed || _instance == null || _instance.State == SoundState.Stopped)
            {
                return;
            }

            _instance.Stop();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _instance?.Dispose();
            _soundEffect?.Dispose();
        }
    }
}
