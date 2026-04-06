using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.Util;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Wz;

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
    }

    public partial class MapSimulator
    {
        private readonly Dictionary<int, List<IDXObject>> _packetOwnedDropExplodeFramesByVariant = new();
        private int _packetOwnedDropExplodeVariantCount = -1;
        private bool _packetOwnedDropExplodeSoundRegistrationAttempted;
        private string _packetOwnedDropExplodeSoundKey;

        private bool TryShowPacketOwnedDropExplodeAnimation(DropItem drop, int currentTime)
        {
            if (drop == null || GraphicsDevice == null)
            {
                return false;
            }

            if (!TryResolvePacketOwnedDropExplodeFrames(drop.PoolId, out List<IDXObject> frames))
            {
                return false;
            }

            _animationEffects.AddOneTime(frames, drop.X, drop.Y, flip: false, currentTime, zOrder: 1);
            return true;
        }

        private void PlayPacketOwnedDropExplodeSound()
        {
            if (_soundManager == null)
            {
                return;
            }

            string soundKey = EnsurePacketOwnedDropExplodeSoundKey();
            if (!string.IsNullOrWhiteSpace(soundKey))
            {
                _soundManager.PlaySound(soundKey);
                return;
            }

            PlayDropItemSE();
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

        private bool TryResolvePacketOwnedDropExplodeFrames(int dropId, out List<IDXObject> frames)
        {
            frames = null;

            int variantCount = GetPacketOwnedDropExplodeVariantCount();
            if (variantCount <= 0)
            {
                return false;
            }

            int preferredVariantIndex = PacketOwnedDropExplosionPresentation.ResolveVariantIndex(dropId, variantCount);
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
    }
}
