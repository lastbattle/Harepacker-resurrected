using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.MapStructure;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Contracts
{
    internal static class RuntimeMapInfoCloner
    {
        public static MapInfo Clone(MapInfo source)
        {
            if (source == null)
                return new MapInfo();

            var owner = new RuntimeMapInfoImage(source.Image?.Name ?? "runtime-map-info.img");
            try
            {
            MapInfo clone = new()
            {
                mapType = source.mapType,
                version = source.version,
                bgm = source.bgm,
                audio = CloneAudio(source.audio, owner),
                mapMark = source.mapMark,
                fieldLimit = source.fieldLimit,
                returnMap = source.returnMap,
                forcedReturn = source.forcedReturn,
                cloud = source.cloud,
                swim = source.swim,
                hideMinimap = source.hideMinimap,
                town = source.town,
                mobRate = source.mobRate,
                VRTop = source.VRTop,
                VRBottom = source.VRBottom,
                VRLeft = source.VRLeft,
                VRRight = source.VRRight,
                LBSide = source.LBSide,
                LBTop = source.LBTop,
                LBBottom = source.LBBottom,
                timeLimit = source.timeLimit,
                lvLimit = source.lvLimit,
                fieldType = source.fieldType,
                onFirstUserEnter = source.onFirstUserEnter,
                onUserEnter = source.onUserEnter,
                directionInfo = CloneDirection(source.directionInfo, owner),
                fly = source.fly,
                noMapCmd = source.noMapCmd,
                partyOnly = source.partyOnly,
                reactorShuffle = source.reactorShuffle,
                reactorShuffleName = source.reactorShuffleName,
                personalShop = source.personalShop,
                entrustedShop = source.entrustedShop,
                effect = source.effect,
                lvForceMove = source.lvForceMove,
                timeMob = source.timeMob,
                help = source.help,
                snow = source.snow,
                rain = source.rain,
                dropExpire = source.dropExpire,
                decHP = source.decHP,
                decInterval = source.decInterval,
                autoLieDetector = source.autoLieDetector == null
                    ? null
                    : new AutoLieDetector(
                        source.autoLieDetector.startHour,
                        source.autoLieDetector.endHour,
                        source.autoLieDetector.interval,
                        source.autoLieDetector.prop),
                expeditionOnly = source.expeditionOnly,
                fs = source.fs,
                protectItem = source.protectItem == null ? null : new(source.protectItem),
                createMobInterval = source.createMobInterval,
                fixedMobCapacity = source.fixedMobCapacity,
                mirror_Bottom = source.mirror_Bottom,
                moveLimit = source.moveLimit,
                mapDesc = source.mapDesc,
                mapName = source.mapName,
                streetName = source.streetName,
                miniMapOnOff = source.miniMapOnOff,
                noRegenMap = source.noRegenMap,
                allowedItem = source.allowedItem == null ? null : new(source.allowedItem),
                recovery = source.recovery,
                blockPBossChange = source.blockPBossChange,
                everlast = source.everlast,
                damageCheckFree = source.damageCheckFree,
                dropRate = source.dropRate,
                scrollDisable = source.scrollDisable,
                needSkillForFly = source.needSkillForFly,
                zakum2Hack = source.zakum2Hack,
                allMoveCheck = source.allMoveCheck,
                VRLimit = source.VRLimit,
                consumeItemCoolTime = source.consumeItemCoolTime,
                nofollowCharacter = source.nofollowCharacter,
                vanishDragon = source.vanishDragon,
                zeroSideOnly = source.zeroSideOnly,
                strMapName = source.strMapName,
                strStreetName = source.strStreetName,
                strCategoryName = source.strCategoryName,
                id = source.id,
                Image = owner
            };

            if (source.Image != null)
                foreach (WzImageProperty property in source.Image.WzProperties)
                    // Source WZ/IMG files may contain duplicate property names.
                    // Clone through the collection so those entries are
                    // preserved for runtime reads and round-tripping; the
                    // strict WzImage.AddProperty API is intended for edits to
                    // newly-authored images and rejects such source data.
                    owner.WzProperties.Add(property.DeepClone());

            foreach (var property in source.unsupportedInfoProperties)
                clone.unsupportedInfoProperties.Add(owner.CloneDetached(property));
            foreach (var property in source.additionalProps)
                clone.additionalProps.Add(owner.CloneDetached(property));
            foreach (var property in source.additionalNonInfoProps)
                clone.additionalNonInfoProps.Add(owner.CloneDetached(property));
            return clone;
            }
            catch (Exception cloneError)
            {
                try { owner.Dispose(); }
                catch (Exception cleanupError)
                {
                    throw new AggregateException("Map metadata cloning and cleanup failed.", cloneError, cleanupError);
                }
                throw;
            }
        }

        private static MapAudioInfo CloneAudio(MapAudioInfo source, RuntimeMapInfoImage owner)
        {
            if (source == null) return new MapAudioInfo();
            var clone = new MapAudioInfo
            {
                PrimaryBgm = source.PrimaryBgm, AmbientBgm = source.AmbientBgm,
                AmbientVolume = source.AmbientVolume,
                PrimaryBgmPropertyName = source.PrimaryBgmPropertyName,
                AmbientBgmPropertyName = source.AmbientBgmPropertyName,
                AmbientVolumePropertyName = source.AmbientVolumePropertyName,
                BgmSubPropertyName = source.BgmSubPropertyName,
                BgmSub = owner.CloneDetached(source.BgmSub)
            };
            foreach (WzImageProperty property in source.UnknownAudioProperties)
                clone.UnknownAudioProperties.Add(owner.CloneDetached(property));
            return clone;
        }

        private static MapDirectionInfo CloneDirection(MapDirectionInfo source, RuntimeMapInfoImage owner)
        {
            if (source == null) return null;
            using WzSubProperty serialized = source.ToProperty();
            MapDirectionInfo clone = MapDirectionInfo.FromProperty(serialized);
            foreach (WzImageProperty property in clone.UnknownProperties) owner.OwnDetached(property);
            foreach (MapDirectionEvent item in clone.Events)
            {
                foreach (WzImageProperty property in item.UnknownProperties) owner.OwnDetached(property);
                foreach (WzImageProperty property in item.UnknownEventQueueProperties) owner.OwnDetached(property);
            }
            return clone;
        }

        // MapInfo's parser clones these typed extension trees independently from its
        // borrowed Image. Only use this for a temporary parser result, not a runtime clone.
        internal static void DisposeParsedTypedMetadata(MapInfo info)
        {
            var properties = new HashSet<WzImageProperty>(ReferenceEqualityComparer.Instance);
            if (info.audio?.BgmSub != null) properties.Add(info.audio.BgmSub);
            if (info.audio != null)
                foreach (WzImageProperty property in info.audio.UnknownAudioProperties) properties.Add(property);
            if (info.directionInfo != null)
            {
                foreach (WzImageProperty property in info.directionInfo.UnknownProperties) properties.Add(property);
                foreach (MapDirectionEvent item in info.directionInfo.Events)
                {
                    foreach (WzImageProperty property in item.UnknownProperties) properties.Add(property);
                    foreach (WzImageProperty property in item.UnknownEventQueueProperties) properties.Add(property);
                }
            }
            foreach (WzImageProperty property in properties) property?.Dispose();
        }

        // Keeps existing MapInfo.Image.Dispose callers sufficient without inserting
        // synthetic nodes into the image or attaching/reparenting caller-owned data.
        private sealed class RuntimeMapInfoImage : WzImage
        {
            private readonly List<WzImageProperty> _detached = new();
            private bool _disposed;
            internal RuntimeMapInfoImage(string name) : base(name) { Parsed = true; Changed = true; }
            internal WzImageProperty CloneDetached(WzImageProperty source)
            {
                WzImageProperty clone = source?.DeepClone();
                OwnDetached(clone);
                return clone;
            }
            internal void OwnDetached(WzImageProperty property)
            {
                if (property != null) _detached.Add(property);
            }
            public override void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                List<Exception> failures = null;
                foreach (WzImageProperty property in _detached)
                {
                    try { property.Dispose(); }
                    catch (Exception error) { (failures ??= new()).Add(error); }
                }
                _detached.Clear();
                try { base.Dispose(); }
                catch (Exception error) { (failures ??= new()).Add(error); }
                if (failures != null) throw new AggregateException("Map metadata cleanup failed.", failures);
            }
        }
    }
}
