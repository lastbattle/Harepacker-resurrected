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
        public enum BgmRequestAction
        {
            None = 0,
            Stop,
            Start
        }

        public enum ClientSoundPlaybackAction
        {
            None = 0,
            PlayOneShot,
            PlayLooping
        }

        public enum ClientSoundStateMutationKind
        {
            CreateState = 0,
            SetStartVolume,
            SetLoopMode,
            RegisterOneShotCompletion,
            RegisterLoopHandle,
            ReleaseLocalStateReference,
            ReleaseOneShotCompletion,
            ReleaseLoopHandle,
            DisposeManager
        }

        public readonly struct ClientSoundPlaybackPlan
        {
            public ClientSoundPlaybackPlan(ClientSoundPlaybackAction action, float startVolumeScale, string reason)
            {
                Action = action;
                StartVolumeScale = startVolumeScale;
                Reason = reason;
            }

            public ClientSoundPlaybackAction Action { get; }
            public float StartVolumeScale { get; }
            public string Reason { get; }
        }

        public readonly struct ClientSoundStateSnapshot
        {
            public ClientSoundStateSnapshot(
                uint stateId,
                string key,
                bool loop,
                float startVolumeScale,
                float resolvedVolumeScale,
                uint loopHandle,
                bool localReferenceReleased,
                bool active)
            {
                StateId = stateId;
                Key = key;
                Loop = loop;
                StartVolumeScale = startVolumeScale;
                ResolvedVolumeScale = resolvedVolumeScale;
                LoopHandle = loopHandle;
                LocalReferenceReleased = localReferenceReleased;
                Active = active;
            }

            public uint StateId { get; }
            public string Key { get; }
            public bool Loop { get; }
            public float StartVolumeScale { get; }
            public float ResolvedVolumeScale { get; }
            public uint LoopHandle { get; }
            public bool LocalReferenceReleased { get; }
            public bool Active { get; }
        }

        public readonly struct ClientSoundStateMutation
        {
            public ClientSoundStateMutation(
                ClientSoundStateMutationKind kind,
                uint stateId,
                string key,
                uint loopHandle,
                float startVolumeScale,
                float resolvedVolumeScale,
                bool loop)
            {
                Kind = kind;
                StateId = stateId;
                Key = key;
                LoopHandle = loopHandle;
                StartVolumeScale = startVolumeScale;
                ResolvedVolumeScale = resolvedVolumeScale;
                Loop = loop;
            }

            public ClientSoundStateMutationKind Kind { get; }
            public uint StateId { get; }
            public string Key { get; }
            public uint LoopHandle { get; }
            public float StartVolumeScale { get; }
            public float ResolvedVolumeScale { get; }
            public bool Loop { get; }
        }

        private readonly ConcurrentDictionary<string, SoundEffect> _soundSources;
        private readonly ConcurrentDictionary<string, WzBinaryProperty> _registeredSoundProperties;
        private readonly ConcurrentDictionary<string, ulong> _clientSoundSourceAccessSerials;
        private readonly HashSet<string> _clientSoundSourceKeys;
        private readonly List<OneShotSound> _activeSounds;
        private readonly ConcurrentDictionary<string, LoopingSound> _activeLoopingSounds;
        private readonly ConcurrentDictionary<uint, string> _loopingSoundHandles;
        private readonly ConcurrentDictionary<uint, uint> _clientLoopingSoundStateIds;
        private readonly ConcurrentDictionary<uint, ClientSoundStateSnapshot> _clientSoundStates;
        private readonly List<ClientSoundStateMutation> _clientSoundStateMutations;
        private readonly object _lock = new object();
        private float _volume = 0.5f;
        private bool _disposed;
        private bool _focusActive = true;
        private uint _nextLoopingSerial;
        private uint _nextClientSoundStateSerial;
        private ulong _nextClientSoundSourceAccessSerial;

        // Maximum concurrent sounds per effect type to prevent resource exhaustion
        private const int MaxConcurrentSoundsPerType = 8;
        private const int MaxClientSoundEffectCacheEntries = 128;
        private readonly ConcurrentDictionary<string, int> _activeSoundCounts;

        public SoundManager()
        {
            _soundSources = new ConcurrentDictionary<string, SoundEffect>();
            _registeredSoundProperties = new ConcurrentDictionary<string, WzBinaryProperty>();
            _clientSoundSourceAccessSerials = new ConcurrentDictionary<string, ulong>();
            _clientSoundSourceKeys = new HashSet<string>(StringComparer.Ordinal);
            _activeSounds = new List<OneShotSound>();
            _activeLoopingSounds = new ConcurrentDictionary<string, LoopingSound>();
            _loopingSoundHandles = new ConcurrentDictionary<uint, string>();
            _clientLoopingSoundStateIds = new ConcurrentDictionary<uint, uint>();
            _clientSoundStates = new ConcurrentDictionary<uint, ClientSoundStateSnapshot>();
            _clientSoundStateMutations = new List<ClientSoundStateMutation>();
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
            if (string.IsNullOrWhiteSpace(name) || sound == null) return;
            _soundSources.AddOrUpdate(
                name,
                _ => MonoGameAudioFactory.CreateSoundEffect(sound),
                (_, existing) => existing);
            _registeredSoundProperties.AddOrUpdate(name, sound, (_, _) => sound);
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
            if (TryPlayRegisteredClientSoundEffect(name, volumeScale, suppressWhileActive: false, out _))
            {
                return;
            }

            TryPlaySound(name, volumeScale, suppressWhileActive: false, out _);
        }

        public void PlaySoundAt(
            string name,
            float startVolumeScale,
            float? listenerX,
            float? listenerY,
            float sourceX,
            float sourceY)
        {
            float positionedStartVolumeScale = ResolveClientPositionedStartVolumeScale(
                startVolumeScale,
                listenerX,
                listenerY,
                sourceX,
                sourceY);

            if (TryPlayRegisteredClientSoundEffect(name, positionedStartVolumeScale, suppressWhileActive: false, out _))
            {
                return;
            }

            TryPlaySound(name, positionedStartVolumeScale, suppressWhileActive: false, out _);
        }

        internal bool TryGetRegisteredSoundSource(string name, out WzBinaryProperty sound)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                sound = null;
                return false;
            }

            return _registeredSoundProperties.TryGetValue(name, out sound) && sound != null;
        }

        internal bool TryPlayRegisteredClientSoundEffect(
            string name,
            float startVolumeScale,
            bool suppressWhileActive,
            out string reason)
        {
            reason = null;
            if (!TryGetRegisteredSoundSource(name, out WzBinaryProperty sound))
            {
                return false;
            }

            TryPlayClientSoundEffect(
                BuildRegisteredClientSoundKey(name, sound),
                sound,
                startVolumeScale,
                loop: false,
                suppressWhileActive: suppressWhileActive,
                out _,
                out reason);
            return true;
        }

        internal bool TryPlayRegisteredClientSoundEffectAt(
            string name,
            float startVolumeScale,
            bool suppressWhileActive,
            float? listenerX,
            float? listenerY,
            float sourceX,
            float sourceY,
            out string reason)
        {
            float positionedStartVolumeScale = ResolveClientPositionedStartVolumeScale(
                startVolumeScale,
                listenerX,
                listenerY,
                sourceX,
                sourceY);
            return TryPlayRegisteredClientSoundEffect(
                name,
                positionedStartVolumeScale,
                suppressWhileActive,
                out reason);
        }

        internal bool TryPlayClientSoundEffect(
            string key,
            WzBinaryProperty sound,
            float startVolumeScale,
            bool loop,
            bool suppressWhileActive,
            out uint handle,
            out string reason)
        {
            handle = 0;
            ClientSoundPlaybackPlan plan = ResolveClientSoundPlaybackPlan(
                key,
                hasSoundProperty: sound != null,
                loop,
                startVolumeScale,
                _activeSoundCounts.GetOrAdd(key ?? string.Empty, 0),
                suppressWhileActive,
                _disposed,
                _focusActive);

            reason = plan.Reason;
            if (plan.Action == ClientSoundPlaybackAction.None)
            {
                return false;
            }

            RegisterClientSoundSource(key, sound);

            if (plan.Action == ClientSoundPlaybackAction.PlayLooping)
            {
                handle = PlayLoopingSoundHandle(key, plan.StartVolumeScale);
                reason = handle == 0 ? "loop-start-failed" : "played";
                if (handle != 0 && !TryGetClientLoopingSoundState(handle, out _))
                {
                    AdmitClientSoundState(key, plan.StartVolumeScale, loop: true, handle);
                }

                return handle != 0;
            }

            if (!TryPlaySound(key, plan.StartVolumeScale, suppressWhileActive, out reason, out OneShotSound oneShot))
            {
                return false;
            }

            uint stateId = AdmitClientSoundState(key, plan.StartVolumeScale, loop: false, loopHandle: 0);
            oneShot.ClientSoundStateId = stateId;
            return true;
        }

        internal bool TryPlayClientSoundEffectAt(
            string key,
            WzBinaryProperty sound,
            float startVolumeScale,
            bool loop,
            bool suppressWhileActive,
            float? listenerX,
            float? listenerY,
            float sourceX,
            float sourceY,
            out uint handle,
            out string reason)
        {
            float positionedStartVolumeScale = ResolveClientPositionedStartVolumeScale(
                startVolumeScale,
                listenerX,
                listenerY,
                sourceX,
                sourceY);
            return TryPlayClientSoundEffect(
                key,
                sound,
                positionedStartVolumeScale,
                loop,
                suppressWhileActive,
                out handle,
                out reason);
        }

        private void RegisterClientSoundSource(string key, WzBinaryProperty sound)
        {
            RegisterSound(key, sound);

            ulong accessSerial = ++_nextClientSoundSourceAccessSerial;
            if (accessSerial == 0)
            {
                accessSerial = ++_nextClientSoundSourceAccessSerial;
            }

            lock (_lock)
            {
                _clientSoundSourceKeys.Add(key);
            }

            _clientSoundSourceAccessSerials[key] = accessSerial;
            FlushClientSoundEffectCache(MaxClientSoundEffectCacheEntries);
        }

        internal bool TryPlaySound(string name, float volumeScale, bool suppressWhileActive, out string reason)
        {
            return TryPlaySound(name, volumeScale, suppressWhileActive, out reason, out _);
        }

        private bool TryPlaySound(
            string name,
            float volumeScale,
            bool suppressWhileActive,
            out string reason,
            out OneShotSound oneShot)
        {
            reason = null;
            oneShot = null;
            if (_disposed)
            {
                reason = "disposed";
                return false;
            }

            if (!_focusActive)
            {
                reason = "focus-paused";
                return false;
            }

            if (!_soundSources.TryGetValue(name, out var soundSource))
            {
                reason = "not-registered";
                Debug.WriteLine($"[SoundManager] Sound '{name}' not registered");
                return false;
            }

            // Check if we're at the limit for this sound type
            int currentCount = _activeSoundCounts.GetOrAdd(name, 0);
            if (ShouldSuppressDuplicatePlayback(currentCount, suppressWhileActive))
            {
                reason = "duplicate-active";
                return false;
            }

            if (currentCount >= MaxConcurrentSoundsPerType)
            {
                reason = "concurrent-limit";
                Debug.WriteLine($"[SoundManager] Max concurrent sounds reached for '{name}' ({currentCount})");
                return false;
            }

            try
            {
                float resolvedVolume = ResolveClientVolumeScale(volumeScale, _volume);
                oneShot = new OneShotSound(soundSource, name, resolvedVolume, OnSoundCompleted);

                lock (_lock)
                {
                    _activeSounds.Add(oneShot);
                }

                _activeSoundCounts.AddOrUpdate(name, 1, (_, c) => c + 1);
                oneShot.Play();
                reason = "played";
                return true;
            }
            catch (Exception ex)
            {
                reason = "play-failed";
                Debug.WriteLine($"[SoundManager] Failed to play sound '{name}': {ex.Message}");
                return false;
            }
        }

        public void PlayLoopingSound(string name)
        {
            PlayLoopingSound(name, 1f);
        }

        public void PlayLoopingSound(string name, float volumeScale)
        {
            PlayLoopingSoundHandle(name, volumeScale);
        }

        internal uint PlayLoopingSoundHandle(string name, float volumeScale = 1f)
        {
            if (_disposed)
            {
                return 0;
            }

            if (!_soundSources.TryGetValue(name, out var soundSource))
            {
                Debug.WriteLine($"[SoundManager] Looping sound '{name}' not registered");
                return 0;
            }

            try
            {
                LoopingSound loopingSound = _activeLoopingSounds.GetOrAdd(
                    name,
                    _ =>
                    {
                        return new LoopingSound(soundSource, ResolveClientVolumeScale(volumeScale, _volume));
                    });
                loopingSound.Volume = ResolveClientVolumeScale(volumeScale, _volume);
                if (_focusActive)
                {
                    loopingSound.Play();
                }

                uint existingHandle = ResolveLoopingSoundHandle(name);
                if (existingHandle != 0)
                {
                    return existingHandle;
                }

                uint handle = AllocateLoopingSoundHandle();
                _loopingSoundHandles[handle] = name;
                return handle;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundManager] Failed to play looping sound '{name}': {ex.Message}");
                return 0;
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

            foreach (KeyValuePair<uint, string> pair in _loopingSoundHandles)
            {
                if (string.Equals(pair.Value, name, StringComparison.Ordinal))
                {
                    if (_loopingSoundHandles.TryRemove(pair.Key, out _))
                    {
                        ReleaseClientLoopingSoundState(pair.Key);
                    }
                }
            }
        }

        internal void StopLoopingSound(uint handle)
        {
            if (handle == 0)
            {
                return;
            }

            if (_loopingSoundHandles.TryRemove(handle, out string name))
            {
                ReleaseClientLoopingSoundState(handle);
                StopLoopingSound(name);
            }
        }

        internal bool TryStopClientSoundHandle(uint handle)
        {
            if (handle == 0 || !_loopingSoundHandles.ContainsKey(handle))
            {
                return false;
            }

            StopLoopingSound(handle);
            return true;
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

            if (sound?.ClientSoundStateId > 0)
            {
                ReleaseClientOneShotSoundState(sound.ClientSoundStateId);
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
                    if (sound.ClientSoundStateId > 0)
                    {
                        ReleaseClientOneShotSoundState(sound.ClientSoundStateId);
                    }

                    if (sound.SoundName != null)
                    {
                        _activeSoundCounts.AddOrUpdate(sound.SoundName, 0, (_, _) => 0);
                    }

                    sound.Dispose();
                }
                _activeSounds.Clear();

                foreach (LoopingSound sound in _activeLoopingSounds.Values)
                {
                    sound.Stop();
                    sound.Dispose();
                }

                _activeLoopingSounds.Clear();
                foreach (uint handle in _loopingSoundHandles.Keys)
                {
                    ReleaseClientLoopingSoundState(handle);
                }

                _loopingSoundHandles.Clear();
            }
        }

        private void FlushClientSoundEffectCache(int maxEntries)
        {
            int normalizedMaxEntries = Math.Max(0, maxEntries);
            while (GetClientSoundSourceKeyCount() > normalizedMaxEntries)
            {
                HashSet<string> activeLoopingSoundKeys = new HashSet<string>(_activeLoopingSounds.Keys, StringComparer.Ordinal);
                string evictKey = SelectClientSoundCacheEvictionCandidate(
                    _clientSoundSourceAccessSerials,
                    _activeSoundCounts,
                    activeLoopingSoundKeys);
                if (string.IsNullOrEmpty(evictKey))
                {
                    return;
                }

                lock (_lock)
                {
                    _clientSoundSourceKeys.Remove(evictKey);
                }

                _clientSoundSourceAccessSerials.TryRemove(evictKey, out _);
                _activeSoundCounts.TryRemove(evictKey, out _);
                _registeredSoundProperties.TryRemove(evictKey, out _);
                if (_soundSources.TryRemove(evictKey, out SoundEffect soundEffect))
                {
                    soundEffect.Dispose();
                }
            }
        }

        private int GetClientSoundSourceKeyCount()
        {
            lock (_lock)
            {
                return _clientSoundSourceKeys.Count;
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
                    if (sound.ClientSoundStateId > 0)
                    {
                        ReleaseClientOneShotSoundState(
                            sound.ClientSoundStateId,
                            ClientSoundStateMutationKind.DisposeManager);
                    }

                    sound.Dispose();
                }
                _activeSounds.Clear();
            }

            foreach (var pair in _activeLoopingSounds)
            {
                pair.Value.Dispose();
            }

            _activeLoopingSounds.Clear();
            foreach (uint handle in _loopingSoundHandles.Keys)
            {
                ReleaseClientLoopingSoundState(handle, ClientSoundStateMutationKind.DisposeManager);
            }

            _loopingSoundHandles.Clear();
            _clientLoopingSoundStateIds.Clear();
            _clientSoundStates.Clear();

            _soundSources.Clear();
            _registeredSoundProperties.Clear();
            _clientSoundSourceAccessSerials.Clear();
            lock (_lock)
            {
                _clientSoundSourceKeys.Clear();
            }
            _activeSoundCounts.Clear();
        }

        internal int ActiveOneShotCount
        {
            get
            {
                lock (_lock)
                {
                    return _activeSounds.Count;
                }
            }
        }

        internal int ActiveLoopingCount => _activeLoopingSounds.Count;

        internal IReadOnlyList<ClientSoundStateMutation> ClientSoundStateMutations
        {
            get
            {
                lock (_lock)
                {
                    return _clientSoundStateMutations.ToArray();
                }
            }
        }

        internal bool TryGetClientSoundState(uint stateId, out ClientSoundStateSnapshot state)
        {
            return _clientSoundStates.TryGetValue(stateId, out state);
        }

        internal bool TryGetClientLoopingSoundState(uint handle, out ClientSoundStateSnapshot state)
        {
            state = default;
            return _clientLoopingSoundStateIds.TryGetValue(handle, out uint stateId)
                && _clientSoundStates.TryGetValue(stateId, out state);
        }

        internal static bool ShouldSuppressDuplicatePlayback(int activeCount, bool suppressWhileActive)
        {
            return suppressWhileActive && activeCount > 0;
        }

        internal static float ResolveClientVolumeScale(float perCallVolumeScale, float masterVolume)
        {
            return Math.Clamp(Math.Max(0f, perCallVolumeScale) * Math.Clamp(masterVolume, 0f, 1f), 0f, 1f);
        }

        internal static float ResolveClientStartVolumeScale(float startVolumeScale)
        {
            return Math.Clamp(startVolumeScale, 0f, 1f);
        }

        internal static float ResolveClientVolumePercentScale(uint startVolumePercent, uint sharedVolumePercent)
        {
            double nativeVolume = startVolumePercent * (double)sharedVolumePercent / 100.0d;
            return (float)Math.Clamp(nativeVolume / 100.0d, 0.0d, 1.0d);
        }

        internal static float ResolveClientPositionVolumeScale(
            float? listenerX,
            float? listenerY,
            float sourceX,
            float sourceY)
        {
            return ResolveClientPositionVolumePercent(listenerX, listenerY, sourceX, sourceY) / 100f;
        }

        internal static int ResolveClientPositionVolumePercent(
            float? listenerX,
            float? listenerY,
            float sourceX,
            float sourceY)
        {
            if (!listenerX.HasValue || !listenerY.HasValue)
            {
                return 40;
            }

            double dx = listenerX.Value - sourceX;
            double dy = listenerY.Value - sourceY;
            double distance = Math.Sqrt(dx * dx + dy * dy + 0.001d);
            if (distance < 250.0d)
            {
                return 100;
            }

            if (distance <= 1000.0d)
            {
                return (int)Math.Clamp(120.0d - distance * 0.08d, 40.0d, 100.0d);
            }

            return 40;
        }

        internal static float ResolveClientPositionedStartVolumeScale(
            float startVolumeScale,
            float? listenerX,
            float? listenerY,
            float sourceX,
            float sourceY)
        {
            return ResolveClientStartVolumeScale(startVolumeScale)
                * ResolveClientPositionVolumeScale(listenerX, listenerY, sourceX, sourceY);
        }

        internal static BgmRequestAction ResolveBgmRequestAction(
            string currentPath,
            string requestedPath,
            bool forceRestart,
            bool hasActiveBgm)
        {
            if (string.IsNullOrWhiteSpace(requestedPath))
            {
                return hasActiveBgm ? BgmRequestAction.Stop : BgmRequestAction.None;
            }

            return !forceRestart && string.Equals(currentPath, requestedPath, StringComparison.Ordinal)
                ? BgmRequestAction.None
                : BgmRequestAction.Start;
        }

        internal static string SelectClientSoundCacheEvictionCandidate(
            IReadOnlyDictionary<string, ulong> accessSerials,
            IReadOnlyDictionary<string, int> activeSoundCounts,
            IReadOnlyCollection<string> activeLoopingSoundKeys)
        {
            string selectedKey = null;
            ulong selectedSerial = ulong.MaxValue;
            foreach (KeyValuePair<string, ulong> pair in accessSerials)
            {
                string key = pair.Key;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (activeSoundCounts != null
                    && activeSoundCounts.TryGetValue(key, out int activeCount)
                    && activeCount > 0)
                {
                    continue;
                }

                if (activeLoopingSoundKeys != null
                    && ContainsSoundKey(activeLoopingSoundKeys, key))
                {
                    continue;
                }

                if (pair.Value < selectedSerial)
                {
                    selectedKey = key;
                    selectedSerial = pair.Value;
                }
            }

            return selectedKey;
        }

        private static bool ContainsSoundKey(IEnumerable<string> keys, string key)
        {
            foreach (string candidate in keys)
            {
                if (candidate == key)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ShouldMuteBgmForRadio(bool radioPlaying, bool radioMuted)
        {
            return radioPlaying && !radioMuted;
        }

        internal static ClientSoundStateMutation[] BuildClientSoundStateAdmissionMutations(
            uint stateId,
            string key,
            bool loop,
            uint loopHandle,
            float startVolumeScale,
            float masterVolume)
        {
            float clampedStartVolumeScale = ResolveClientStartVolumeScale(startVolumeScale);
            float resolvedVolumeScale = ResolveClientVolumeScale(clampedStartVolumeScale, masterVolume);
            if (loop)
            {
                return new[]
                {
                    new ClientSoundStateMutation(
                        ClientSoundStateMutationKind.CreateState,
                        stateId,
                        key,
                        loopHandle,
                        clampedStartVolumeScale,
                        resolvedVolumeScale,
                        loop),
                    new ClientSoundStateMutation(
                        ClientSoundStateMutationKind.SetStartVolume,
                        stateId,
                        key,
                        loopHandle,
                        clampedStartVolumeScale,
                        resolvedVolumeScale,
                        loop),
                    new ClientSoundStateMutation(
                        ClientSoundStateMutationKind.SetLoopMode,
                        stateId,
                        key,
                        loopHandle,
                        clampedStartVolumeScale,
                        resolvedVolumeScale,
                        loop),
                    new ClientSoundStateMutation(
                        ClientSoundStateMutationKind.RegisterLoopHandle,
                        stateId,
                        key,
                        loopHandle,
                        clampedStartVolumeScale,
                        resolvedVolumeScale,
                        loop),
                    new ClientSoundStateMutation(
                        ClientSoundStateMutationKind.ReleaseLocalStateReference,
                        stateId,
                        key,
                        loopHandle,
                        clampedStartVolumeScale,
                        resolvedVolumeScale,
                        loop)
                };
            }

            return new[]
            {
                new ClientSoundStateMutation(
                    ClientSoundStateMutationKind.CreateState,
                    stateId,
                    key,
                    loopHandle,
                    clampedStartVolumeScale,
                    resolvedVolumeScale,
                    loop),
                new ClientSoundStateMutation(
                    ClientSoundStateMutationKind.SetStartVolume,
                    stateId,
                    key,
                    loopHandle,
                    clampedStartVolumeScale,
                    resolvedVolumeScale,
                    loop),
                new ClientSoundStateMutation(
                    ClientSoundStateMutationKind.SetLoopMode,
                    stateId,
                    key,
                    loopHandle,
                    clampedStartVolumeScale,
                    resolvedVolumeScale,
                    loop),
                new ClientSoundStateMutation(
                    ClientSoundStateMutationKind.RegisterOneShotCompletion,
                    stateId,
                    key,
                    loopHandle,
                    clampedStartVolumeScale,
                    resolvedVolumeScale,
                    loop),
                new ClientSoundStateMutation(
                    ClientSoundStateMutationKind.ReleaseLocalStateReference,
                    stateId,
                    key,
                    loopHandle,
                    clampedStartVolumeScale,
                    resolvedVolumeScale,
                    loop)
            };
        }

        internal static ClientSoundStateSnapshot BuildClientSoundStateAdmissionSnapshot(
            uint stateId,
            string key,
            bool loop,
            uint loopHandle,
            float startVolumeScale,
            float masterVolume)
        {
            float clampedStartVolumeScale = ResolveClientStartVolumeScale(startVolumeScale);
            return new ClientSoundStateSnapshot(
                stateId,
                key,
                loop,
                clampedStartVolumeScale,
                ResolveClientVolumeScale(clampedStartVolumeScale, masterVolume),
                loopHandle,
                localReferenceReleased: true,
                active: true);
        }

        internal static ClientSoundStateMutation BuildClientSoundStateReleaseMutation(
            ClientSoundStateMutationKind kind,
            ClientSoundStateSnapshot state)
        {
            return new ClientSoundStateMutation(
                kind,
                state.StateId,
                state.Key,
                state.LoopHandle,
                state.StartVolumeScale,
                state.ResolvedVolumeScale,
                state.Loop);
        }

        internal static string BuildRegisteredClientSoundKey(string registeredName, WzBinaryProperty sound)
        {
            return BuildClientSoundKey("RegisteredSound", registeredName, sound);
        }

        internal static string BuildPacketOwnedClientSoundKey(string resolvedDescriptor, WzBinaryProperty sound)
        {
            return BuildClientSoundKey("PacketOwnedSound", resolvedDescriptor, sound);
        }

        internal static string BuildClientSoundKey(string ownerPrefix, string descriptor, WzBinaryProperty sound)
        {
            string normalizedOwnerPrefix = string.IsNullOrWhiteSpace(ownerPrefix)
                ? "Sound"
                : ownerPrefix.Trim().TrimEnd(':');
            return $"{normalizedOwnerPrefix}:{ResolveClientSoundPath(descriptor, sound)}";
        }

        internal static string ResolveClientSoundPath(string descriptor, WzBinaryProperty sound)
        {
            string path = sound?.FullPath?.Replace('\\', '/');
            string normalizedDescriptor = descriptor?.Trim().Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(path)
                || (path.IndexOf('/') < 0 && !string.IsNullOrWhiteSpace(normalizedDescriptor))
                || (path.IndexOf(".img/", StringComparison.OrdinalIgnoreCase) < 0
                    && !string.IsNullOrWhiteSpace(normalizedDescriptor)
                    && normalizedDescriptor.IndexOf(".img/", StringComparison.OrdinalIgnoreCase) >= 0)
                || (path.IndexOf(".img/", StringComparison.OrdinalIgnoreCase) >= 0
                    && !string.IsNullOrWhiteSpace(normalizedDescriptor)
                    && normalizedDescriptor.StartsWith("Sound/", StringComparison.OrdinalIgnoreCase)
                    && normalizedDescriptor.IndexOf(".img/", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                path = normalizedDescriptor;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            while (path.StartsWith("/", StringComparison.Ordinal))
            {
                path = path[1..];
            }

            if (path.StartsWith("Sound.wz/", StringComparison.OrdinalIgnoreCase))
            {
                path = path["Sound.wz/".Length..];
            }

            if (path.StartsWith("sound/", StringComparison.Ordinal))
            {
                path = "Sound/" + path["sound/".Length..];
            }

            if (path.StartsWith("Sound/", StringComparison.OrdinalIgnoreCase))
            {
                return "Sound/" + path["Sound/".Length..].Trim('/');
            }

            if (path.IndexOf(".img/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Sound/" + path.Trim('/');
            }

            string[] segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2
                && segments[0].IndexOf(".img", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return $"Sound/{segments[0]}.img/{string.Join("/", segments, 1, segments.Length - 1)}";
            }

            return path.Trim('/');
        }

        internal static ClientSoundPlaybackPlan ResolveClientSoundPlaybackPlan(
            string key,
            bool hasSoundProperty,
            bool loop,
            float startVolumeScale,
            int activeCount,
            bool suppressWhileActive,
            bool disposed,
            bool focusActive)
        {
            if (disposed)
            {
                return new ClientSoundPlaybackPlan(ClientSoundPlaybackAction.None, 0f, "disposed");
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                return new ClientSoundPlaybackPlan(ClientSoundPlaybackAction.None, 0f, "missing-key");
            }

            if (!hasSoundProperty)
            {
                return new ClientSoundPlaybackPlan(ClientSoundPlaybackAction.None, 0f, "missing-sound");
            }

            if (!focusActive && !loop)
            {
                return new ClientSoundPlaybackPlan(ClientSoundPlaybackAction.None, 0f, "focus-paused");
            }

            if (!loop && ShouldSuppressDuplicatePlayback(activeCount, suppressWhileActive))
            {
                return new ClientSoundPlaybackPlan(ClientSoundPlaybackAction.None, 0f, "duplicate-active");
            }

            if (!loop && activeCount >= MaxConcurrentSoundsPerType)
            {
                return new ClientSoundPlaybackPlan(ClientSoundPlaybackAction.None, 0f, "concurrent-limit");
            }

            return new ClientSoundPlaybackPlan(
                loop ? ClientSoundPlaybackAction.PlayLooping : ClientSoundPlaybackAction.PlayOneShot,
                ResolveClientStartVolumeScale(startVolumeScale),
                "ready");
        }

        private uint ResolveLoopingSoundHandle(string name)
        {
            foreach (KeyValuePair<uint, string> pair in _loopingSoundHandles)
            {
                if (string.Equals(pair.Value, name, StringComparison.Ordinal))
                {
                    return pair.Key;
                }
            }

            return 0;
        }

        private uint AllocateLoopingSoundHandle()
        {
            uint handle = ++_nextLoopingSerial;
            return handle == 0
                ? ++_nextLoopingSerial
                : handle;
        }

        private uint AllocateClientSoundStateId()
        {
            uint stateId = ++_nextClientSoundStateSerial;
            return stateId == 0
                ? ++_nextClientSoundStateSerial
                : stateId;
        }

        private uint AdmitClientSoundState(string key, float startVolumeScale, bool loop, uint loopHandle)
        {
            uint stateId = AllocateClientSoundStateId();
            ClientSoundStateSnapshot snapshot = BuildClientSoundStateAdmissionSnapshot(
                stateId,
                key,
                loop,
                loopHandle,
                startVolumeScale,
                _volume);

            _clientSoundStates[stateId] = snapshot;
            if (loop && loopHandle != 0)
            {
                _clientLoopingSoundStateIds[loopHandle] = stateId;
            }

            ClientSoundStateMutation[] mutations = BuildClientSoundStateAdmissionMutations(
                stateId,
                key,
                loop,
                loopHandle,
                snapshot.StartVolumeScale,
                _volume);
            lock (_lock)
            {
                _clientSoundStateMutations.AddRange(mutations);
            }

            return stateId;
        }

        private void ReleaseClientLoopingSoundState(
            uint handle,
            ClientSoundStateMutationKind mutationKind = ClientSoundStateMutationKind.ReleaseLoopHandle)
        {
            if (!_clientLoopingSoundStateIds.TryRemove(handle, out uint stateId))
            {
                return;
            }

            if (!_clientSoundStates.TryGetValue(stateId, out ClientSoundStateSnapshot state))
            {
                return;
            }

            ClientSoundStateSnapshot releasedState = new ClientSoundStateSnapshot(
                state.StateId,
                state.Key,
                state.Loop,
                state.StartVolumeScale,
                state.ResolvedVolumeScale,
                state.LoopHandle,
                state.LocalReferenceReleased,
                active: false);
            _clientSoundStates[stateId] = releasedState;

            lock (_lock)
            {
                _clientSoundStateMutations.Add(BuildClientSoundStateReleaseMutation(mutationKind, state));
            }
        }

        private void ReleaseClientOneShotSoundState(
            uint stateId,
            ClientSoundStateMutationKind mutationKind = ClientSoundStateMutationKind.ReleaseOneShotCompletion)
        {
            if (!_clientSoundStates.TryGetValue(stateId, out ClientSoundStateSnapshot state) || state.Loop)
            {
                return;
            }

            if (!state.Active)
            {
                return;
            }

            ClientSoundStateSnapshot releasedState = new ClientSoundStateSnapshot(
                state.StateId,
                state.Key,
                state.Loop,
                state.StartVolumeScale,
                state.ResolvedVolumeScale,
                state.LoopHandle,
                state.LocalReferenceReleased,
                active: false);
            _clientSoundStates[stateId] = releasedState;

            lock (_lock)
            {
                _clientSoundStateMutations.Add(BuildClientSoundStateReleaseMutation(mutationKind, state));
            }
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
            public uint ClientSoundStateId { get; set; }

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
