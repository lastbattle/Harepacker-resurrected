using System;
using System.Linq;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int RemoteAffectedAreaFallbackTickMs = 1000;

        private ChatCommandHandler.CommandResult ApplyRemoteAffectedAreaPacketCommand(int packetType, byte[] payload)
        {
            if (_affectedAreaPool == null || _mapBoard?.MapInfo == null)
            {
                return ChatCommandHandler.CommandResult.Error("Remote affected-area pool is unavailable until a field is loaded.");
            }

            if (!_remoteAffectedAreaPacketRuntime.TryApplyPacket(
                    packetType,
                    payload,
                    ApplyRemoteAffectedAreaCreatePacket,
                    RemoveRemoteAffectedArea,
                    out string result))
            {
                return ChatCommandHandler.CommandResult.Error(result ?? $"Failed to apply remote affected-area packet {packetType}.");
            }

            return ChatCommandHandler.CommandResult.Ok($"{result} {DescribeRemoteAffectedAreaStatus()}");
        }

        private bool ApplyRemoteAffectedAreaCreatePacket(RemoteAffectedAreaCreatedPacket packet)
        {
            if (packet.SkillId <= 0)
            {
                return false;
            }

            if (RemoteAffectedAreaSupportResolver.IsAreaBuffItemType(packet.Type))
            {
                return UpsertRemoteAreaBuffItemAffectedArea(
                    packet.ObjectId,
                    packet.Type,
                    unchecked((int)packet.OwnerCharacterId),
                    packet.SkillId,
                    packet.Bounds,
                    packet.StartDelayUnits,
                    packet.ElementAttribute,
                    packet.Phase);
            }

            return packet.SkillId >= 10000
                ? UpsertRemotePlayerAffectedArea(
                    packet.ObjectId,
                    packet.Type,
                    unchecked((int)packet.OwnerCharacterId),
                    packet.SkillId,
                    packet.SkillLevel,
                    packet.Bounds,
                    packet.StartDelayUnits,
                    packet.ElementAttribute,
                    packet.Phase)
                : UpsertRemoteMobAffectedArea(
                    packet.ObjectId,
                    packet.Type,
                    unchecked((int)packet.OwnerCharacterId),
                    packet.SkillId,
                    packet.SkillLevel,
                    packet.Bounds,
                    packet.StartDelayUnits,
                    packet.ElementAttribute,
                    packet.Phase);
        }

        private string DescribeRemoteAffectedAreaStatus()
        {
            return _affectedAreaPool == null
                ? "Remote affected-area pool unavailable."
                : $"Remote affected-area pool count={_affectedAreaPool.Count}.";
        }

        private void BindRemoteAffectedAreaPacketField()
        {
            _remoteAffectedAreaPacketRuntime.BindField(_mapBoard?.MapInfo?.id ?? -1, ClearRemoteAffectedAreas);
        }

        private void UpdateRemoteAffectedAreaGameplay(int currentTime)
        {
            if (_affectedAreaPool == null)
            {
                return;
            }

            foreach (ActiveAffectedArea area in _affectedAreaPool.ActiveAreas.ToArray())
            {
                if (area?.IsActive(currentTime) != true)
                {
                    continue;
                }

                switch (area.SourceKind)
                {
                    case AffectedAreaSourceKind.MobSkill:
                        UpdateRemoteMobAffectedAreaGameplay(area, currentTime);
                        break;

                    case AffectedAreaSourceKind.PlayerSkill:
                        UpdateRemotePlayerAffectedAreaGameplay(area, currentTime);
                        break;

                    case AffectedAreaSourceKind.AreaBuffItem:
                        break;
                }
            }
        }

        private void UpdateRemoteMobAffectedAreaGameplay(ActiveAffectedArea area, int currentTime)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null || !player.IsAlive || !area.Contains(player.X, player.Y))
            {
                return;
            }

            MobSkillRuntimeData runtimeData = ResolveMobSkillRuntimeData(area.SkillId, Math.Max(1, area.SkillLevel));
            int intervalMs = Math.Max(250, runtimeData?.IntervalMs > 0 ? runtimeData.IntervalMs : RemoteAffectedAreaFallbackTickMs);
            if (!_affectedAreaPool.TryBeginGameplayTick(area, currentTime, intervalMs))
            {
                return;
            }

            if (runtimeData != null)
            {
                _playerManager?.TryApplyMobSkillStatus(area.SkillId, runtimeData, currentTime, area.WorldBounds.Center.X);
            }

            _fieldEffects?.TriggerDamageMist(0.35f, Math.Max(250, intervalMs), currentTime);
        }

        private void UpdateRemotePlayerAffectedAreaGameplay(ActiveAffectedArea area, int currentTime)
        {
            SkillData skill = _playerManager?.SkillLoader?.LoadSkill(area.SkillId);
            SkillLevelData levelData = ResolveRemoteAffectedAreaSkillLevel(skill, area.SkillLevel);
            if (TryApplyRemotePlayerSupportAffectedAreaGameplay(area, skill, levelData, currentTime))
            {
                return;
            }

            if (_mobPool?.ActiveMobs == null || _mobPool.ActiveMobs.Count == 0)
            {
                return;
            }

            if (!TryResolveRemotePlayerAffectedAreaStatus(skill, levelData, out MobStatusEffect effect, out int durationMs, out int value))
            {
                return;
            }

            if (!_affectedAreaPool.TryBeginGameplayTick(area, currentTime, RemoteAffectedAreaFallbackTickMs))
            {
                return;
            }

            foreach (MobItem mob in _mobPool.ActiveMobs)
            {
                if (mob?.AI == null || mob.AI.IsDead || !area.Contains(mob.CurrentX, mob.CurrentY))
                {
                    continue;
                }

                mob.AI.ApplyStatusEffect(effect, durationMs, currentTime, value, RemoteAffectedAreaFallbackTickMs, sourceSkillId: skill.SkillId);
            }
        }

        private bool TryApplyRemotePlayerSupportAffectedAreaGameplay(
            ActiveAffectedArea area,
            SkillData skill,
            SkillLevelData levelData,
            int currentTime)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null
                || !player.IsAlive
                || !RemoteAffectedAreaSupportResolver.IsSupportZone(skill)
                || !area.Contains(player.X, player.Y))
            {
                return false;
            }

            int localPlayerId = player.Build?.Id ?? 0;
            if (!RemoteAffectedAreaSupportResolver.CanAffectLocalPlayer(
                    skill,
                    localPlayerId,
                    area.OwnerId,
                    IsAffectedAreaOwnerPartyMember(area.OwnerId)))
            {
                return false;
            }

            if (RemoteAffectedAreaSupportResolver.IsInvincibleZone(skill))
            {
                return true;
            }

            if (!RemoteAffectedAreaSupportResolver.IsRecoveryZone(skill) || levelData == null)
            {
                return false;
            }

            if (!_affectedAreaPool.TryBeginGameplayTick(area, currentTime, RemoteAffectedAreaFallbackTickMs))
            {
                return true;
            }

            int hpHeal = levelData.HP;
            int mpHeal = levelData.MP;
            if (levelData.X > 0)
            {
                hpHeal = player.MaxHP * levelData.X / 100;
            }

            if (hpHeal <= 0 && mpHeal <= 0)
            {
                return true;
            }

            _playerManager?.Heal(hpHeal, mpHeal);
            return true;
        }

        private static SkillLevelData ResolveRemoteAffectedAreaSkillLevel(SkillData skill, int level)
        {
            if (skill == null)
            {
                return null;
            }

            SkillLevelData levelData = skill.GetLevel(Math.Max(1, level));
            if (levelData != null)
            {
                return levelData;
            }

            for (int i = 1; i <= Math.Max(1, skill.MaxLevel); i++)
            {
                levelData = skill.GetLevel(i);
                if (levelData != null)
                {
                    return levelData;
                }
            }

            return null;
        }

        private static bool TryResolveRemotePlayerAffectedAreaStatus(
            SkillData skill,
            SkillLevelData levelData,
            out MobStatusEffect effect,
            out int durationMs,
            out int value)
        {
            effect = MobStatusEffect.None;
            durationMs = 0;
            value = 0;

            if (skill == null || levelData == null)
            {
                return false;
            }

            string searchText = BuildRemoteAffectedAreaSkillSearchText(skill);
            durationMs = Math.Max(1000, levelData.Time > 0 ? levelData.Time * 1000 : 10000);

            if (searchText.Contains("venom", StringComparison.OrdinalIgnoreCase))
            {
                effect = MobStatusEffect.Venom;
                value = ResolveRemoteAffectedAreaDotValue(skill, levelData, 3);
                return true;
            }

            if (searchText.Contains("poison", StringComparison.OrdinalIgnoreCase)
                || skill.Element == SkillElement.Poison
                || string.Equals(skill.DotType, "poison", StringComparison.OrdinalIgnoreCase))
            {
                effect = MobStatusEffect.Poison;
                value = ResolveRemoteAffectedAreaDotValue(skill, levelData, 4);
                return true;
            }

            if (searchText.Contains("burn", StringComparison.OrdinalIgnoreCase)
                || searchText.Contains("flame", StringComparison.OrdinalIgnoreCase)
                || string.Equals(skill.DotType, "burn", StringComparison.OrdinalIgnoreCase))
            {
                effect = MobStatusEffect.Burned;
                value = ResolveRemoteAffectedAreaDotValue(skill, levelData, 5);
                return true;
            }

            return false;
        }

        private static int ResolveRemoteAffectedAreaDotValue(SkillData skill, SkillLevelData levelData, int fallbackDivisor)
        {
            int explicitValue = Math.Max(levelData.X, levelData.Y);
            if (explicitValue > 0)
            {
                return explicitValue;
            }

            int damage = Math.Max(1, levelData.Damage);
            int attackCount = Math.Max(1, levelData.AttackCount);
            int scaledDamage = Math.Max(1, damage * attackCount / Math.Max(1, fallbackDivisor));
            return skill?.IsMagicDamageSkill == true
                ? scaledDamage
                : Math.Max(1, scaledDamage / 2);
        }

        private static string BuildRemoteAffectedAreaSkillSearchText(SkillData skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }

            return string.Join(
                " ",
                new[]
                {
                    skill.Name,
                    skill.Description,
                    skill.DebuffMessageToken,
                    skill.DotType,
                    skill.ZoneType
                }.Where(static value => !string.IsNullOrWhiteSpace(value)));
        }

        private bool IsRemoteAffectedAreaProtectionActive(int currentTime)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null || _affectedAreaPool == null)
            {
                return false;
            }

            int localPlayerId = player.Build?.Id ?? 0;
            foreach (ActiveAffectedArea area in _affectedAreaPool.ActiveAreas)
            {
                if (area?.SourceKind != AffectedAreaSourceKind.PlayerSkill
                    || !area.IsActive(currentTime)
                    || !area.Contains(player.X, player.Y))
                {
                    continue;
                }

                SkillData skill = _playerManager?.SkillLoader?.LoadSkill(area.SkillId);
                if (!RemoteAffectedAreaSupportResolver.IsInvincibleZone(skill)
                    || !RemoteAffectedAreaSupportResolver.CanAffectLocalPlayer(
                        skill,
                        localPlayerId,
                        area.OwnerId,
                        IsAffectedAreaOwnerPartyMember(area.OwnerId)))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool IsAffectedAreaOwnerPartyMember(int ownerId)
        {
            if (ownerId <= 0 || !_remoteUserPool.TryGetActor(ownerId, out RemoteUserActor actor) || string.IsNullOrWhiteSpace(actor?.Name))
            {
                return false;
            }

            return _socialListRuntime
                .BuildTrackedEntriesSnapshot()
                .Any(entry => entry?.Tab == Interaction.SocialListTab.Party
                              && !entry.IsLocalPlayer
                              && string.Equals(entry.Name, actor.Name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
