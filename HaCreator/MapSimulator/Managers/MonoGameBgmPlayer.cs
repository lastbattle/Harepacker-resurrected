using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Audio;
using System;

namespace HaCreator.MapSimulator.Managers
{
    internal sealed class MonoGameBgmPlayer : IDisposable
    {
        private readonly SoundEffect _soundEffect;
        private readonly SoundEffectInstance _instance;
        private bool _disposed;

        public MonoGameBgmPlayer(WzBinaryProperty sound, bool looped, float volume = 0.5f)
        {
            _soundEffect = MonoGameAudioFactory.CreateSoundEffect(sound);
            _instance = _soundEffect.CreateInstance();
            _instance.IsLooped = looped;
            _instance.Volume = volume;
        }

        public SoundState State => _instance?.State ?? SoundState.Stopped;

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
