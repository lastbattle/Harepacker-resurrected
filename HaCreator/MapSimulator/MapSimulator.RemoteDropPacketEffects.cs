using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Globalization;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.Util;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Wz;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    internal static class PacketOwnedDropExplosionPresentation
    {
        internal const int SkillId = 4211006;
        internal const string SkillImageName = "421.img";
        internal const string SkillHitPath = "skill/4211006/hit";
        internal const string SoundImageName = "Skill.img";
        internal const string SoundPropertyPath = "4211006/Hit";
        internal const string SoundName = "Hit";
        internal const string EffectBaseUol = "Skill/421.img/skill/4211006/hit";

        internal static int ResolveVariantIndex(int dropId, int variantCount)
        {
            if (variantCount <= 1 || dropId <= 0)
            {
                return 0;
            }

            long normalizedDropId = dropId - 1L;
            if (normalizedDropId < 0)
            {
                normalizedDropId = -normalizedDropId;
            }

            return (int)(normalizedDropId % variantCount);
        }

        internal static string CreateSoundKey(WzBinaryProperty soundProperty = null)
        {
            return SoundManager.BuildPacketOwnedClientSoundKey(
                $"{SoundImageName}/{SoundPropertyPath}",
                soundProperty);
        }

        internal static int ResolveHitVariantIndex(DropItem drop, int variantCount)
        {
            if (variantCount <= 1)
            {
                return 0;
            }

            int fallbackIndex = ResolveVariantIndex(drop?.PoolId ?? 0, variantCount);
            if (drop?.Type != DropType.Meso)
            {
                return fallbackIndex;
            }

            int bandedVariantCount = Math.Min(3, variantCount);
            int bandOffset = DropPool.GetMoneyIconTypeForAmount(Math.Max(0, drop.MesoAmount)) switch
            {
                <= 1 => 0,
                2 => 3,
                _ => 6
            };

            int candidateIndex = bandOffset + ResolveVariantIndex(drop.PoolId, bandedVariantCount);
            return candidateIndex < variantCount
                ? candidateIndex
                : fallbackIndex;
        }

        internal static string BuildEffectUol(int variantIndex)
        {
            return variantIndex < 0
                ? null
                : $"{EffectBaseUol}/{variantIndex.ToString(CultureInfo.InvariantCulture)}";
        }

        internal static string BuildOwnerSlotKey(int dropId)
        {
            return dropId <= 0
                ? string.Empty
                : $"aux.packetOwnedDropExplosion.oneTime:{dropId.ToString(CultureInfo.InvariantCulture)}";
        }

        internal static int ResolveRestoreElapsed(
            int previousDropId,
            int previousVariantIndex,
            string previousEffectUol,
            Vector2 previousPosition,
            int previousAnimationStartTime,
            int currentDropId,
            int currentVariantIndex,
            string currentEffectUol,
            Vector2 currentPosition,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || previousDropId != currentDropId
                || previousVariantIndex != currentVariantIndex
                || previousPosition != currentPosition
                || !string.Equals(previousEffectUol, currentEffectUol, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        internal static float ResolveSoundVolumeScale(Vector2 listenerPosition, DropItem drop)
        {
            if (drop == null)
            {
                return 1f;
            }

            const float minDistance = 64f;
            const float maxDistance = 900f;
            const float minVolume = 0.2f;

            float distance = Vector2.Distance(listenerPosition, new Vector2(drop.X, drop.Y));
            if (distance <= minDistance)
            {
                return 1f;
            }

            if (distance >= maxDistance)
            {
                return minVolume;
            }

            float normalized = 1f - ((distance - minDistance) / (maxDistance - minDistance));
            return MathHelper.Lerp(minVolume, 1f, MathHelper.Clamp(normalized, 0f, 1f));
        }
    }

    internal static class PacketOwnedDropPetPickupPresentation
    {
        internal const int SoundStringPoolId = 0x0506;
        internal const string SoundDescriptorFallback = "Sound/Game.img/PickUpItem";
        internal const string SoundName = "PickUpItem";

        internal static string CreateSoundKey(string resolvedDescriptor, WzBinaryProperty soundProperty = null)
        {
            return SoundManager.BuildPacketOwnedClientSoundKey(resolvedDescriptor, soundProperty);
        }
    }

    public partial class MapSimulator
    {
        private readonly Dictionary<int, List<IDXObject>> _packetOwnedDropExplodeFramesByVariant = new();
        private readonly Dictionary<string, AnimationDisplayerPacketOwnedDropExplosionOwnerState> _animationDisplayerPacketOwnedDropExplosionOwnerStates = new(StringComparer.OrdinalIgnoreCase);
        private int _packetOwnedDropExplodeVariantCount = -1;
        private bool _packetOwnedDropExplodeSoundRegistrationAttempted;
        private string _packetOwnedDropExplodeSoundKey;
        private WzBinaryProperty _packetOwnedDropExplodeSoundBinary;
        private bool _packetOwnedLocalPetPickupSoundRegistrationAttempted;
        private string _packetOwnedLocalPetPickupSoundKey;
        private WzBinaryProperty _packetOwnedLocalPetPickupSoundBinary;

        private sealed class AnimationDisplayerPacketOwnedDropExplosionOwnerState
        {
            public int DropId { get; init; }
            public int VariantIndex { get; init; }
            public string EffectUol { get; init; }
            public Vector2 Position { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private bool TryShowPacketOwnedDropExplodeAnimation(DropItem drop, int currentTime)
        {
            if (drop == null || GraphicsDevice == null)
            {
                return false;
            }

            if (!TryResolvePacketOwnedDropExplodeFrames(drop, out List<IDXObject> frames, out int variantIndex, out string effectUol))
            {
                return false;
            }

            Vector2 position = new(drop.X, drop.Y);
            int initialElapsedMs = ResolveAnimationDisplayerPacketOwnedDropExplosionInitialElapsed(
                drop.PoolId,
                variantIndex,
                effectUol,
                position,
                currentTime,
                ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
            _animationEffects.AddPacketOwnedDropExplosion(
                frames,
                effectUol,
                drop.X,
                drop.Y,
                currentTime,
                zOrder: 1,
                initialElapsedMs);
            return true;
        }

        private void PlayPacketOwnedDropExplodeSound(DropItem drop)
        {
            if (_soundManager == null)
            {
                return;
            }

            string soundKey = EnsurePacketOwnedDropExplodeSoundKey();
            if (!string.IsNullOrWhiteSpace(soundKey))
            {
                if (_packetOwnedDropExplodeSoundBinary != null)
                {
                    Vector2? listenerPosition = _playerManager?.Player?.Position;
                    if (drop != null)
                    {
                        _soundManager.TryPlayClientSoundEffectAt(
                            soundKey,
                            _packetOwnedDropExplodeSoundBinary,
                            startVolumeScale: 1f,
                            loop: false,
                            suppressWhileActive: false,
                            listenerX: listenerPosition?.X,
                            listenerY: listenerPosition?.Y,
                            sourceX: drop.X,
                            sourceY: drop.Y,
                            out _,
                            out _);
                    }
                    else
                    {
                        _soundManager.TryPlayClientSoundEffect(
                            soundKey,
                            _packetOwnedDropExplodeSoundBinary,
                            startVolumeScale: 1f,
                            loop: false,
                            suppressWhileActive: false,
                            out _,
                            out _);
                    }
                }
                else
                {
                    _soundManager.PlaySound(soundKey);
                }
                return;
            }

            PlayDropItemSE(drop);
        }

        private void PlayPacketOwnedLocalPetPickupSound()
        {
            if (_soundManager == null)
            {
                return;
            }

            string soundKey = EnsurePacketOwnedLocalPetPickupSoundKey();
            if (!string.IsNullOrWhiteSpace(soundKey))
            {
                if (_packetOwnedLocalPetPickupSoundBinary != null)
                {
                    _soundManager.TryPlayClientSoundEffect(
                        soundKey,
                        _packetOwnedLocalPetPickupSoundBinary,
                        startVolumeScale: 1f,
                        loop: false,
                        suppressWhileActive: false,
                        out _,
                        out _);
                }
                else
                {
                    _soundManager.PlaySound(soundKey);
                }
                return;
            }

            PlayPickUpItemSE();
        }

        private string EnsurePacketOwnedLocalPetPickupSoundKey()
        {
            if (_packetOwnedLocalPetPickupSoundRegistrationAttempted)
            {
                return _packetOwnedLocalPetPickupSoundKey;
            }

            _packetOwnedLocalPetPickupSoundRegistrationAttempted = true;

            string descriptor = MapleStoryStringPool.GetOrFallback(
                PacketOwnedDropPetPickupPresentation.SoundStringPoolId,
                PacketOwnedDropPetPickupPresentation.SoundDescriptorFallback);

            if (!TryResolvePacketOwnedWzSound(descriptor, "Game.img", out WzBinaryProperty soundProperty, out string resolvedDescriptor, false)
                || soundProperty == null)
            {
                return PacketOwnedDropPetPickupPresentation.SoundName;
            }

            _packetOwnedLocalPetPickupSoundKey = PacketOwnedDropPetPickupPresentation.CreateSoundKey(resolvedDescriptor, soundProperty);
            _packetOwnedLocalPetPickupSoundBinary = soundProperty;
            return _packetOwnedLocalPetPickupSoundKey;
        }

        private string EnsurePacketOwnedDropExplodeSoundKey()
        {
            if (_packetOwnedDropExplodeSoundRegistrationAttempted)
            {
                return _packetOwnedDropExplodeSoundKey;
            }

            _packetOwnedDropExplodeSoundRegistrationAttempted = true;

            WzImageProperty resolved = ResolvePacketOwnedSoundProperty(
                PacketOwnedDropExplosionPresentation.SoundImageName,
                PacketOwnedDropExplosionPresentation.SoundPropertyPath);
            WzBinaryProperty soundBinary = WzInfoTools.GetRealProperty(resolved) as WzBinaryProperty
                ?? (resolved as WzUOLProperty)?.LinkValue as WzBinaryProperty;
            if (soundBinary == null)
            {
                return null;
            }

            _packetOwnedDropExplodeSoundKey = PacketOwnedDropExplosionPresentation.CreateSoundKey(soundBinary);
            _packetOwnedDropExplodeSoundBinary = soundBinary;
            return _packetOwnedDropExplodeSoundKey;
        }

        private bool TryResolvePacketOwnedDropExplodeFrames(
            DropItem drop,
            out List<IDXObject> frames,
            out int variantIndex,
            out string effectUol)
        {
            frames = null;
            variantIndex = -1;
            effectUol = null;

            int variantCount = GetPacketOwnedDropExplodeVariantCount();
            if (variantCount <= 0)
            {
                return false;
            }

            int preferredVariantIndex = PacketOwnedDropExplosionPresentation.ResolveHitVariantIndex(drop, variantCount);
            if (TryGetPacketOwnedDropExplodeFrames(preferredVariantIndex, out frames))
            {
                variantIndex = preferredVariantIndex;
                effectUol = PacketOwnedDropExplosionPresentation.BuildEffectUol(preferredVariantIndex);
                return true;
            }

            if (preferredVariantIndex != 0 && TryGetPacketOwnedDropExplodeFrames(0, out frames))
            {
                variantIndex = 0;
                effectUol = PacketOwnedDropExplosionPresentation.BuildEffectUol(0);
                return true;
            }

            return false;
        }

        private bool TryGetPacketOwnedDropExplodeFrames(int variantIndex, out List<IDXObject> frames)
        {
            if (_packetOwnedDropExplodeFramesByVariant.TryGetValue(variantIndex, out frames))
            {
                return frames?.Count > 0;
            }

            WzImageProperty variantProperty = ResolvePacketOwnedDropExplodeVariantProperty(variantIndex);
            if (variantProperty == null)
            {
                _packetOwnedDropExplodeFramesByVariant[variantIndex] = null;
                frames = null;
                return false;
            }

            frames = MapSimulatorLoader.LoadFrames(
                _texturePool,
                variantProperty,
                0,
                0,
                GraphicsDevice,
                new ConcurrentBag<WzObject>());
            if (frames != null && frames.Count == 0)
            {
                frames = null;
            }

            _packetOwnedDropExplodeFramesByVariant[variantIndex] = frames;
            return frames?.Count > 0;
        }

        private int GetPacketOwnedDropExplodeVariantCount()
        {
            if (_packetOwnedDropExplodeVariantCount >= 0)
            {
                return _packetOwnedDropExplodeVariantCount;
            }

            _packetOwnedDropExplodeVariantCount = 0;
            WzImageProperty hitRoot = ResolvePacketOwnedDropExplodeHitRoot();
            if (hitRoot == null)
            {
                return _packetOwnedDropExplodeVariantCount;
            }

            while (WzInfoTools.GetRealProperty(hitRoot[_packetOwnedDropExplodeVariantCount.ToString(CultureInfo.InvariantCulture)]) != null)
            {
                _packetOwnedDropExplodeVariantCount++;
            }

            return _packetOwnedDropExplodeVariantCount;
        }

        private WzImageProperty ResolvePacketOwnedDropExplodeVariantProperty(int variantIndex)
        {
            if (variantIndex < 0)
            {
                return null;
            }

            WzImageProperty hitRoot = ResolvePacketOwnedDropExplodeHitRoot();
            return WzInfoTools.GetRealProperty(hitRoot?[variantIndex.ToString(CultureInfo.InvariantCulture)]) as WzImageProperty;
        }

        private static WzImageProperty ResolvePacketOwnedDropExplodeHitRoot()
        {
            WzImage skillImage = Program.FindImage("Skill", PacketOwnedDropExplosionPresentation.SkillImageName);
            skillImage?.ParseImage();
            return skillImage?["skill"]?["4211006"]?["hit"] as WzImageProperty;
        }

        private float ResolvePacketOwnedDropExplodeVolumeScale(DropItem drop)
        {
            if (_playerManager?.Player == null)
            {
                return 1f;
            }

            return PacketOwnedDropExplosionPresentation.ResolveSoundVolumeScale(_playerManager.Player.Position, drop);
        }

        private int ResolveAnimationDisplayerPacketOwnedDropExplosionInitialElapsed(
            int dropId,
            int variantIndex,
            string effectUol,
            Vector2 position,
            int currentTime,
            int durationMs)
        {
            string ownerSlotKey = PacketOwnedDropExplosionPresentation.BuildOwnerSlotKey(dropId);
            if (string.IsNullOrWhiteSpace(ownerSlotKey)
                || string.IsNullOrWhiteSpace(effectUol)
                || variantIndex < 0
                || durationMs <= 0)
            {
                return 0;
            }

            int initialElapsedMs = 0;
            if (_animationDisplayerPacketOwnedDropExplosionOwnerStates.TryGetValue(
                    ownerSlotKey,
                    out AnimationDisplayerPacketOwnedDropExplosionOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = PacketOwnedDropExplosionPresentation.ResolveRestoreElapsed(
                    existingState.DropId,
                    existingState.VariantIndex,
                    existingState.EffectUol,
                    existingState.Position,
                    existingState.AnimationStartTime,
                    dropId,
                    variantIndex,
                    effectUol,
                    position,
                    currentTime,
                    durationMs);
            }

            _animationDisplayerPacketOwnedDropExplosionOwnerStates[ownerSlotKey] =
                new AnimationDisplayerPacketOwnedDropExplosionOwnerState
                {
                    DropId = dropId,
                    VariantIndex = variantIndex,
                    EffectUol = effectUol,
                    Position = position,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };
            return initialElapsedMs;
        }

        internal static string BuildAnimationDisplayerPacketOwnedDropExplosionOwnerSlotKeyForTesting(int dropId)
        {
            return PacketOwnedDropExplosionPresentation.BuildOwnerSlotKey(dropId);
        }

        internal static string BuildAnimationDisplayerPacketOwnedDropExplosionEffectUolForTesting(int variantIndex)
        {
            return PacketOwnedDropExplosionPresentation.BuildEffectUol(variantIndex);
        }

        internal static int ResolveAnimationDisplayerPacketOwnedDropExplosionRestoreElapsedForTesting(
            int previousDropId,
            int previousVariantIndex,
            string previousEffectUol,
            Vector2 previousPosition,
            int previousAnimationStartTime,
            int currentDropId,
            int currentVariantIndex,
            string currentEffectUol,
            Vector2 currentPosition,
            int currentTime,
            int durationMs)
        {
            return PacketOwnedDropExplosionPresentation.ResolveRestoreElapsed(
                previousDropId,
                previousVariantIndex,
                previousEffectUol,
                previousPosition,
                previousAnimationStartTime,
                currentDropId,
                currentVariantIndex,
                currentEffectUol,
                currentPosition,
                currentTime,
                durationMs);
        }

        internal static string BuildPacketOwnedDropExplosionSoundKeyForTesting(WzBinaryProperty soundProperty = null)
        {
            return PacketOwnedDropExplosionPresentation.CreateSoundKey(soundProperty);
        }

        internal static string BuildPacketOwnedDropPetPickupSoundKeyForTesting(
            string resolvedDescriptor,
            WzBinaryProperty soundProperty = null)
        {
            return PacketOwnedDropPetPickupPresentation.CreateSoundKey(resolvedDescriptor, soundProperty);
        }
    }
}
