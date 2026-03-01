/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MapleLib.WzLib.WzProperties;
using NAudio.Wave;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Manages sound effect playback with support for multiple simultaneous instances.
    /// Uses a pool of WaveOutEvent instances to allow overlapping sound effects.
    /// </summary>
    public class SoundManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, WzBinaryProperty> _soundSources;
        private readonly List<OneShotSound> _activeSounds;
        private readonly object _lock = new object();
        private float _volume = 0.5f;
        private bool _disposed;

        // Maximum concurrent sounds per effect type to prevent resource exhaustion
        private const int MaxConcurrentSoundsPerType = 8;
        private readonly ConcurrentDictionary<string, int> _activeSoundCounts;

        public SoundManager()
        {
            _soundSources = new ConcurrentDictionary<string, WzBinaryProperty>();
            _activeSounds = new List<OneShotSound>();
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
            _soundSources[name] = sound;
            _activeSoundCounts[name] = 0;
        }

        /// <summary>
        /// Plays a registered sound effect. Supports multiple simultaneous playback.
        /// </summary>
        /// <param name="name">The registered sound name</param>
        public void PlaySound(string name)
        {
            if (_disposed) return;

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
                var oneShot = new OneShotSound(soundSource, name, _volume, OnSoundCompleted);

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

            _soundSources.Clear();
            _activeSoundCounts.Clear();
        }

        /// <summary>
        /// Represents a single one-shot sound playback instance.
        /// Uses WaveOutEvent for more reliable playback compared to WaveOut with FunctionCallback.
        /// </summary>
        private class OneShotSound : IDisposable
        {
            private readonly Stream _byteStream;
            private readonly Mp3FileReader _mpegStream;
            private readonly WaveOutEvent _wavePlayer;
            private readonly Action<OneShotSound> _onCompleted;
            private int _disposed; // Use int for Interlocked
            private int _completed;

            public string SoundName { get; }
            public bool IsCompleted => Interlocked.CompareExchange(ref _completed, 0, 0) == 1 ||
                                       Interlocked.CompareExchange(ref _disposed, 0, 0) == 1;

            public OneShotSound(WzBinaryProperty sound, string soundName, float volume, Action<OneShotSound> onCompleted)
            {
                SoundName = soundName;
                _onCompleted = onCompleted;

                // Use WaveOutEvent instead of WaveOut(FunctionCallback) for more reliable playback
                _wavePlayer = new WaveOutEvent();

                if (sound.WavFormat.Encoding == WaveFormatEncoding.MpegLayer3)
                {
                    _byteStream = new MemoryStream(sound.GetBytes(false));
                    _mpegStream = new Mp3FileReader(_byteStream);
                    _wavePlayer.Init(_mpegStream);
                }
                else if (sound.WavFormat.Encoding == WaveFormatEncoding.Pcm)
                {
                    throw new NotSupportedException("PCM format not currently supported");
                }
                else
                {
                    throw new NotSupportedException($"Unsupported audio format: {sound.WavFormat.Encoding}");
                }

                _wavePlayer.Volume = volume;
                _wavePlayer.PlaybackStopped += WavePlayer_PlaybackStopped;
            }

            private void WavePlayer_PlaybackStopped(object sender, StoppedEventArgs e)
            {
                Interlocked.Exchange(ref _completed, 1);
                _onCompleted?.Invoke(this);
            }

            public void Play()
            {
                if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1) return;
                _wavePlayer.Play();
            }

            public void Stop()
            {
                if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1) return;
                try
                {
                    _wavePlayer.Stop();
                }
                catch { }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
                Interlocked.Exchange(ref _completed, 1);

                try
                {
                    _wavePlayer.PlaybackStopped -= WavePlayer_PlaybackStopped;
                    _wavePlayer.Stop();
                    _wavePlayer.Dispose();
                }
                catch { }

                try
                {
                    _mpegStream?.Dispose();
                    _byteStream?.Dispose();
                }
                catch { }
            }
        }
    }
}
