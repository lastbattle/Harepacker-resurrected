using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Globalization;
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

        internal static string CreateSoundKey()
        {
            return $"Skill:{SkillId}:{SoundName}";
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

        internal static string CreateSoundKey(string resolvedDescriptor)
        {
            return $"DropPetPickup:{resolvedDescriptor}";
        }
    }

    public partial class MapSimulator
    {
        private readonly Dictionary<int, List<IDXObject>> _packetOwnedDropExplodeFramesByVariant = new();
        private int _packetOwnedDropExplodeVariantCount = -1;
        private bool _packetOwnedDropExplodeSoundRegistrationAttempted;
        private string _packetOwnedDropExplodeSoundKey;
        private bool _packetOwnedLocalPetPickupSoundRegistrationAttempted;
        private string _packetOwnedLocalPetPickupSoundKey;

        private bool TryShowPacketOwnedDropExplodeAnimation(DropItem drop, int currentTime)
        {
            if (drop == null || GraphicsDevice == null)
            {
                return false;
            }

            if (!TryResolvePacketOwnedDropExplodeFrames(drop, out List<IDXObject> frames))
            {
                return false;
            }

            _animationEffects.AddOneTime(frames, drop.X, drop.Y, flip: false, currentTime, zOrder: 1);
            return true;
        }

        private void PlayPacketOwnedDropExplodeSound(DropItem drop)
        {
            if (_soundManager == null)
            {
                return;
            }

            float volumeScale = ResolvePacketOwnedDropExplodeVolumeScale(drop);

            string soundKey = EnsurePacketOwnedDropExplodeSoundKey();
            if (!string.IsNullOrWhiteSpace(soundKey))
            {
                _soundManager.PlaySound(soundKey, volumeScale);
                return;
            }

            PlayDropItemSE(volumeScale);
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
                _soundManager.PlaySound(soundKey);
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

            if (!TryResolvePacketOwnedWzSound(descriptor, "Game.img", out WzBinaryProperty soundProperty, out string resolvedDescriptor)
                || soundProperty == null)
            {
                return PacketOwnedDropPetPickupPresentation.SoundName;
            }

            _packetOwnedLocalPetPickupSoundKey = PacketOwnedDropPetPickupPresentation.CreateSoundKey(resolvedDescriptor);
            _soundManager.RegisterSound(_packetOwnedLocalPetPickupSoundKey, soundProperty);
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

            _packetOwnedDropExplodeSoundKey = PacketOwnedDropExplosionPresentation.CreateSoundKey();
            _soundManager.RegisterSound(_packetOwnedDropExplodeSoundKey, soundBinary);
            return _packetOwnedDropExplodeSoundKey;
        }

        private bool TryResolvePacketOwnedDropExplodeFrames(DropItem drop, out List<IDXObject> frames)
        {
            frames = null;

            int variantCount = GetPacketOwnedDropExplodeVariantCount();
            if (variantCount <= 0)
            {
                return false;
            }

            int preferredVariantIndex = PacketOwnedDropExplosionPresentation.ResolveHitVariantIndex(drop, variantCount);
            if (TryGetPacketOwnedDropExplodeFrames(preferredVariantIndex, out frames))
            {
                return true;
            }

            return preferredVariantIndex != 0
                && TryGetPacketOwnedDropExplodeFrames(0, out frames);
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
    }
}
