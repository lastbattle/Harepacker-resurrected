/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Audio;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Manages MonoGame sound effect playback with support for multiple simultaneous instances.
    /// </summary>
    public class SoundManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, SoundEffect> _soundSources;
        private readonly List<OneShotSound> _activeSounds;
        private readonly ConcurrentDictionary<string, LoopingSound> _activeLoopingSounds;
        private readonly object _lock = new object();
        private float _volume = 0.5f;
        private bool _disposed;
        private bool _focusActive = true;

        // Maximum concurrent sounds per effect type to prevent resource exhaustion
        private const int MaxConcurrentSoundsPerType = 8;
        private readonly ConcurrentDictionary<string, int> _activeSoundCounts;

        public SoundManager()
        {
            _soundSources = new ConcurrentDictionary<string, SoundEffect>();
            _activeSounds = new List<OneShotSound>();
            _activeLoopingSounds = new ConcurrentDictionary<string, LoopingSound>();
            _activeSoundCounts = new ConcurrentDictionary<string, int>();
        }

        /// <summary>
        /// Gets or sets the master volume for all sound effects (0.0 to 1.0).
        /// </summary>
        public float Volume
        {
            get => _volume;
            set => _volume = Math.Max(0f, Math.Min(1f, value));
        }

        /// <summary>
        /// Registers a sound effect source for later playback.
        /// </summary>
        /// <param name="name">Unique identifier for the sound</param>
        /// <param name="sound">The WzBinaryProperty containing the sound data</param>
        public void RegisterSound(string name, WzBinaryProperty sound)
        {
            if (sound == null) return;
            _soundSources.AddOrUpdate(
                name,
                _ => MonoGameAudioFactory.CreateSoundEffect(sound),
                (_, existing) => existing);
            _activeSoundCounts[name] = 0;
        }

        /// <summary>
        /// Plays a registered sound effect. Supports multiple simultaneous playback.
        /// </summary>
        /// <param name="name">The registered sound name</param>
        public void PlaySound(string name)
        {
            PlaySound(name, 1f);
        }

        /// <summary>
        /// Plays a registered sound effect with an additional per-call volume scale.
        /// </summary>
        /// <param name="name">The registered sound name</param>
        /// <param name="volumeScale">Per-call volume multiplier clamped to 0.0-1.0</param>
        public void PlaySound(string name, float volumeScale)
        {
            if (_disposed) return;
            if (!_focusActive) return;

            if (!_soundSources.TryGetValue(name, out var soundSource))
            {
                Debug.WriteLine($"[SoundManager] Sound '{name}' not registered");
                return;
            }

            // Check if we're at the limit for this sound type
            int currentCount = _activeSoundCounts.GetOrAdd(name, 0);
            if (currentCount >= MaxConcurrentSoundsPerType)
            {
                Debug.WriteLine($"[SoundManager] Max concurrent sounds reached for '{name}' ({currentCount})");
                return;
            }

            try
            {
                float resolvedVolume = Math.Max(0f, Math.Min(1f, _volume * Math.Max(0f, volumeScale)));
                var oneShot = new OneShotSound(soundSource, name, resolvedVolume, OnSoundCompleted);

                lock (_lock)
                {
                    _activeSounds.Add(oneShot);
                }

                _activeSoundCounts.AddOrUpdate(name, 1, (_, c) => c + 1);
                oneShot.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundManager] Failed to play sound '{name}': {ex.Message}");
            }
        }

        public void PlayLoopingSound(string name)
        {
            PlayLoopingSound(name, 1f);
        }

        public void PlayLoopingSound(string name, float volumeScale)
        {
            if (_disposed) return;

            if (!_soundSources.TryGetValue(name, out var soundSource))
            {
                Debug.WriteLine($"[SoundManager] Looping sound '{name}' not registered");
                return;
            }

            try
            {
                LoopingSound loopingSound = _activeLoopingSounds.GetOrAdd(
                    name,
                    _ => new LoopingSound(soundSource, Math.Max(0f, Math.Min(1f, _volume * Math.Max(0f, volumeScale)))));
                loopingSound.Volume = Math.Max(0f, Math.Min(1f, _volume * Math.Max(0f, volumeScale)));
                if (_focusActive)
                {
                    loopingSound.Play();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundManager] Failed to play looping sound '{name}': {ex.Message}");
            }
        }

        public void StopLoopingSound(string name)
        {
            if (_disposed || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (_activeLoopingSounds.TryRemove(name, out LoopingSound loopingSound))
            {
                loopingSound.Dispose();
            }
        }

        public void SetFocusActive(bool isActive)
        {
            if (_disposed || _focusActive == isActive)
            {
                return;
            }

            _focusActive = isActive;

            lock (_lock)
            {
                foreach (var sound in _activeSounds)
                {
                    if (isActive)
                    {
                        sound.Resume();
                    }
                    else
                    {
                        sound.Pause();
                    }
                }

                foreach (LoopingSound sound in _activeLoopingSounds.Values)
                {
                    if (isActive)
                    {
                        sound.Resume();
                    }
                    else
                    {
                        sound.Pause();
                    }
                }
            }
        }

        private void OnSoundCompleted(OneShotSound sound)
        {
            if (sound?.SoundName != null)
            {
                _activeSoundCounts.AddOrUpdate(sound.SoundName, 0, (_, c) => Math.Max(0, c - 1));
            }
        }

        /// <summary>
        /// Cleans up finished sound instances. Call periodically (e.g., in Update loop).
        /// </summary>
        public void Update()
        {
            if (_disposed) return;

            lock (_lock)
            {
                // Poll instance state so completed sounds can release their slot.
                for (int i = 0; i < _activeSounds.Count; i++)
                {
                    _activeSounds[i].Update();
                }

                // Remove and dispose completed sounds
                for (int i = _activeSounds.Count - 1; i >= 0; i--)
                {
                    var sound = _activeSounds[i];
                    if (sound.IsCompleted)
                    {
                        _activeSounds.RemoveAt(i);
                        sound.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Stops all currently playing sounds.
        /// </summary>
        public void StopAll()
        {
            lock (_lock)
            {
                foreach (var sound in _activeSounds)
                {
                    sound.Stop();
                }

                foreach (LoopingSound sound in _activeLoopingSounds.Values)
                {
                    sound.Stop();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                foreach (var sound in _activeSounds)
                {
                    sound.Stop();
                    sound.Dispose();
                }
                _activeSounds.Clear();
            }

            foreach (var pair in _activeLoopingSounds)
            {
                pair.Value.Dispose();
            }

            _activeLoopingSounds.Clear();

            _soundSources.Clear();
            _activeSoundCounts.Clear();
        }

        /// <summary>
        /// Represents a single one-shot sound playback instance backed by MonoGame.
        /// </summary>
        private class OneShotSound : IDisposable
        {
            private readonly SoundEffectInstance _instance;
            private readonly Action<OneShotSound> _onCompleted;
            private bool _disposed;
            private bool _completed;

            public string SoundName { get; }
            public bool IsCompleted => _completed || _disposed;

            public OneShotSound(SoundEffect sound, string soundName, float volume, Action<OneShotSound> onCompleted)
            {
                SoundName = soundName;
                _onCompleted = onCompleted;
                _instance = sound.CreateInstance();
                _instance.Volume = volume;
            }

            public void Play()
            {
                if (_disposed) return;
                _instance.Play();
            }

            public void Pause()
            {
                if (_disposed || _instance.State != SoundState.Playing) return;
                _instance.Pause();
            }

            public void Resume()
            {
                if (_disposed || _instance.State != SoundState.Paused) return;
                _instance.Resume();
            }

            public void Stop()
            {
                if (_disposed || _instance.State == SoundState.Stopped) return;
                try
                {
                    _instance.Stop();
                }
                catch { }
            }

            public void Update()
            {
                if (_disposed || _completed)
                {
                    return;
                }

                if (_instance.State == SoundState.Stopped)
                {
                    _completed = true;
                    _onCompleted?.Invoke(this);
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _completed = true;

                try
                {
                    _instance.Stop();
                    _instance.Dispose();
                }
                catch { }
            }
        }

        private sealed class LoopingSound : IDisposable
        {
            private readonly SoundEffectInstance _instance;
            private bool _disposed;

            public LoopingSound(SoundEffect sound, float volume)
            {
                _instance = sound.CreateInstance();
                _instance.IsLooped = true;
                _instance.Volume = volume;
            }

            public float Volume
            {
                set
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _instance.Volume = value;
                }
            }

            public void Play()
            {
                if (_disposed || _instance.State == SoundState.Playing)
                {
                    return;
                }

                _instance.Play();
            }

            public void Pause()
            {
                if (_disposed || _instance.State != SoundState.Playing)
                {
                    return;
                }

                _instance.Pause();
            }

            public void Resume()
            {
                if (_disposed || _instance.State != SoundState.Paused)
                {
                    return;
                }

                _instance.Resume();
            }

            public void Stop()
            {
                if (_disposed || _instance.State == SoundState.Stopped)
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
                try
                {
                    _instance.Stop();
                    _instance.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}
