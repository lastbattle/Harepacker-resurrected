using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Applies fieldLimit-based restrictions to local skill usage.
    /// </summary>
    public static class FieldSkillRestrictionEvaluator
    {
        private const int RocketBoosterSkillId = 35101004;

        public sealed class RuntimeState
        {
            public bool CoconutBasicActionOwned { get; init; }
            public bool SnowBallBasicActionOwned { get; init; }
            public bool GuildBossBasicActionOwned { get; init; }
        }

        private static readonly HashSet<int> ClientUnableToUseSkillForbiddenSet = new HashSet<int>
        {
            4211002,
            4221001,
            1121006,
            1221007,
            1321003,
            4321001,
            4121008,
            3121003,
            3221003,
            5101002,
            5101004,
            5201006,
            15101003,
            5121005,
            21100002,
            21110003,
            21110006,
            4311003,
            4331000,
            4331004,
            4331005,
            4341002,
            33111002,
            32101001,
            32111011,
            35001003,
            1121001,
            1321001,
            3120010
        };

        public static bool CanUseSkill(MapInfo mapInfo, SkillData skill)
        {
            return CanUseSkill(mapInfo, skill, 0);
        }

        public static bool CanUseSkill(MapInfo mapInfo, SkillData skill, int currentJobId)
        {
            return CanUseSkill(mapInfo, skill, currentJobId, mobMassacreDisableSkill: false);
        }

        public static bool CanUseSkill(MapInfo mapInfo, SkillData skill, int currentJobId, bool mobMassacreDisableSkill)
        {
            string externalRestrictionMessage = mobMassacreDisableSkill
                ? "Skills cannot be used while the Mu Lung Dojo massacre field disables skill usage."
                : null;
            return CanUseSkill(mapInfo, skill, currentJobId, externalRestrictionMessage);
        }

        public static bool CanUseSkill(MapInfo mapInfo, SkillData skill, int currentJobId, string externalRestrictionMessage)
        {
            return GetRestrictionMessage(mapInfo, skill, currentJobId, externalRestrictionMessage, runtimeState: null) == null;
        }

        public static bool CanUseSkill(
            MapInfo mapInfo,
            SkillData skill,
            int currentJobId,
            string externalRestrictionMessage,
            RuntimeState runtimeState)
        {
            return GetRestrictionMessage(mapInfo, skill, currentJobId, externalRestrictionMessage, runtimeState) == null;
        }

        public static string GetRestrictionMessage(MapInfo mapInfo, SkillData skill)
        {
            return GetRestrictionMessage(mapInfo, skill, 0);
        }

        public static string GetRestrictionMessage(MapInfo mapInfo, SkillData skill, int currentJobId)
        {
            return GetRestrictionMessage(mapInfo, skill, currentJobId, mobMassacreDisableSkill: false);
        }

        public static string GetRestrictionMessage(MapInfo mapInfo, SkillData skill, int currentJobId, bool mobMassacreDisableSkill)
        {
            string externalRestrictionMessage = mobMassacreDisableSkill
                ? "Skills cannot be used while the Mu Lung Dojo massacre field disables skill usage."
                : null;
            return GetRestrictionMessage(mapInfo, skill, currentJobId, externalRestrictionMessage);
        }

        public static string GetRestrictionMessage(MapInfo mapInfo, SkillData skill, int currentJobId, string externalRestrictionMessage)
        {
            return GetRestrictionMessage(mapInfo, skill, currentJobId, externalRestrictionMessage, runtimeState: null);
        }

        public static string GetRestrictionMessage(
            MapInfo mapInfo,
            SkillData skill,
            int currentJobId,
            string externalRestrictionMessage,
            RuntimeState runtimeState)
        {
            if (skill == null)
                return "Skill data is unavailable.";

            string fieldLimitRestrictionMessage = GetRestrictionMessage(mapInfo?.fieldLimit ?? 0, skill);
            if (!string.IsNullOrWhiteSpace(fieldLimitRestrictionMessage))
            {
                return fieldLimitRestrictionMessage;
            }

            if (!string.IsNullOrWhiteSpace(externalRestrictionMessage))
            {
                return externalRestrictionMessage;
            }

            return GetClientOwnedFieldRestrictionMessage(mapInfo, skill, currentJobId, runtimeState);
        }

        public static string GetSkillCancelRestrictionMessage(MapInfo mapInfo, SkillData skill)
        {
            if (mapInfo == null)
            {
                return null;
            }

            return HasNoCancelSkillFlag(mapInfo)
                ? "Active skill cancellation is disabled in this field."
                : null;
        }

        public static bool CanUseSkill(long fieldLimit, SkillData skill)
        {
            return GetRestrictionMessage(fieldLimit, skill) == null;
        }

        public static string GetRestrictionMessage(long fieldLimit, SkillData skill)
        {
            if (skill == null)
                return "Skill data is unavailable.";

            if (FieldLimitType.Unable_To_Use_Mystic_Door.Check(fieldLimit) && IsMysticDoorSkill(skill))
                return "Mystic Door cannot be used in this field.";

            if (FieldLimitType.Unable_To_Use_Rocket_Boost.Check(fieldLimit) && IsRocketBoosterSkill(skill))
                return "Rocket Booster cannot be used in this field.";

            if (FieldLimitType.Unable_To_Use_Taming_Mob.Check(fieldLimit) && UsesTamingMobRestrictedSkill(skill))
                return "Mount and mechanic vehicle skills cannot be used in this field.";

            if (FieldLimitType.Unable_To_Use_Skill.Check(fieldLimit))
            {
                if (IsClientForbiddenInUnableToUseSkillField(skill))
                {
                    return "This skill is forbidden in this field.";
                }
            }

            if (FieldLimitType.Move_Skill_Only.Check(fieldLimit) && !skill.IsMovement)
                return "Only movement skills can be used in this field.";

            return null;
        }

        public static bool HasFieldEntryNotice(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Use_Skill.Check(fieldLimit)
                   || FieldLimitType.Move_Skill_Only.Check(fieldLimit)
                   || FieldLimitType.Unable_To_Use_Mystic_Door.Check(fieldLimit)
                   || FieldLimitType.Unable_To_Use_Rocket_Boost.Check(fieldLimit)
                   || FieldLimitType.Unable_To_Use_Taming_Mob.Check(fieldLimit);
        }

        public static string GetFieldEntryNotice(long fieldLimit)
        {
            if (FieldLimitType.Unable_To_Use_Skill.Check(fieldLimit))
                return "Some client-forbidden skills are disabled in this map.";

            if (FieldLimitType.Move_Skill_Only.Check(fieldLimit))
                return "Only movement skills can be used in this map.";

            if (FieldLimitType.Unable_To_Use_Mystic_Door.Check(fieldLimit))
                return "Mystic Door is disabled in this map.";

            if (FieldLimitType.Unable_To_Use_Rocket_Boost.Check(fieldLimit))
                return "Rocket Booster is disabled in this map.";

            if (FieldLimitType.Unable_To_Use_Taming_Mob.Check(fieldLimit))
                return "Mount and mechanic vehicle skills are disabled in this map.";

            return null;
        }

        private static string GetClientOwnedFieldRestrictionMessage(MapInfo mapInfo, SkillData skill, int currentJobId, RuntimeState runtimeState)
        {
            if (mapInfo == null || skill == null)
            {
                return null;
            }

            string noSkillRestrictionMessage = GetNoSkillRestrictionMessage(mapInfo, skill, currentJobId);
            if (!string.IsNullOrWhiteSpace(noSkillRestrictionMessage))
            {
                return noSkillRestrictionMessage;
            }

            if (runtimeState?.CoconutBasicActionOwned == true)
            {
                return "Skills cannot be used while the Coconut minigame owns basic attacks.";
            }

            if (runtimeState?.SnowBallBasicActionOwned == true)
            {
                return "Skills cannot be used while the Snowball minigame owns basic attacks.";
            }

            if (runtimeState?.GuildBossBasicActionOwned == true)
            {
                return "Skills cannot be used while the Guild Boss field owns basic attacks.";
            }

            if (IsNoDragonField(mapInfo) && IsEvanDragonMagicAttack(skill))
            {
                return "Evan dragon magic skills cannot be used in no-dragon fields.";
            }

            return mapInfo.fieldType switch
            {
                _ => null
            };
        }

        private static string GetNoSkillRestrictionMessage(MapInfo mapInfo, SkillData skill, int currentJobId)
        {
            WzImageProperty noSkillProperty = FindAdditionalFieldProperty(mapInfo, "noSkill");
            if (noSkillProperty == null)
            {
                return null;
            }

            if (MatchesListedSkill(noSkillProperty["skill"], skill.SkillId))
            {
                return "This skill is forbidden in this field.";
            }

            if (MatchesListedSkillClass(noSkillProperty["class"], currentJobId, skill))
            {
                return "This field forbids skills for your job branch.";
            }

            return null;
        }

        private static bool HasNoCancelSkillFlag(MapInfo mapInfo)
        {
            WzImageProperty property = FindInfoFieldProperty(mapInfo, "noCancelSkill");
            return TryReadInt(property, out int value) && value != 0;
        }

        private static WzImageProperty FindAdditionalFieldProperty(MapInfo mapInfo, string propertyName)
        {
            if (mapInfo?.additionalNonInfoProps != null)
            {
                for (int i = 0; i < mapInfo.additionalNonInfoProps.Count; i++)
                {
                    WzImageProperty property = mapInfo.additionalNonInfoProps[i];
                    if (string.Equals(property?.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return property;
                    }
                }
            }

            return mapInfo?.Image?[propertyName] as WzImageProperty;
        }

        private static WzImageProperty FindInfoFieldProperty(MapInfo mapInfo, string propertyName)
        {
            if (mapInfo?.additionalProps != null)
            {
                for (int i = 0; i < mapInfo.additionalProps.Count; i++)
                {
                    WzImageProperty property = mapInfo.additionalProps[i];
                    if (string.Equals(property?.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return property;
                    }
                }
            }

            return mapInfo?.Image?["info"]?[propertyName] as WzImageProperty;
        }

        private static bool MatchesListedSkill(WzImageProperty property, int skillId)
        {
            return skillId > 0 && ContainsIntValue(property, skillId);
        }

        private static bool MatchesListedSkillClass(WzImageProperty property, int currentJobId, SkillData skill)
        {
            if (property == null)
            {
                return false;
            }

            foreach (int candidate in EnumerateSkillRestrictionJobCandidates(currentJobId, skill))
            {
                if (ContainsIntValue(property, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsIntValue(WzImageProperty property, int expectedValue)
        {
            if (property == null)
            {
                return false;
            }

            Stack<WzImageProperty> pending = new Stack<WzImageProperty>();
            pending.Push(property);
            while (pending.Count > 0)
            {
                WzImageProperty current = pending.Pop();
                if (TryReadInt(current, out int value) && value == expectedValue)
                {
                    return true;
                }

                if (current.WzProperties == null)
                {
                    continue;
                }

                for (int i = 0; i < current.WzProperties.Count; i++)
                {
                    if (current.WzProperties[i] != null)
                    {
                        pending.Push(current.WzProperties[i]);
                    }
                }
            }

            return false;
        }

        private static bool TryReadInt(WzImageProperty property, out int value)
        {
            value = 0;
            if (property == null)
            {
                return false;
            }

            try
            {
                value = property.GetInt();
                return true;
            }
            catch
            {
                if (property is WzStringProperty stringProperty
                    && int.TryParse(stringProperty.Value, out value))
                {
                    return true;
                }

                return false;
            }
        }

        private static IEnumerable<int> EnumerateSkillRestrictionJobCandidates(int currentJobId, SkillData skill)
        {
            HashSet<int> yielded = new HashSet<int>();

            foreach (int candidate in EnumerateJobRestrictionBranchCandidates(Math.Abs(currentJobId)))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (int candidate in EnumerateSkillOwnedJobRestrictionCandidates(skill))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<int> EnumerateSkillOwnedJobRestrictionCandidates(SkillData skill)
        {
            if (skill == null)
            {
                yield break;
            }

            int skillJobId = Math.Abs(skill.Job);
            if (skillJobId == 0)
            {
                yield return 0;
            }

            foreach (int candidate in EnumerateJobRestrictionBranchCandidates(skillJobId))
            {
                yield return candidate;
            }

            int skillBookId = Math.Abs(skill.SkillId / 10000);
            if (skillBookId == 0)
            {
                yield return 0;
            }

            foreach (int candidate in EnumerateJobRestrictionBranchCandidates(skillBookId))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<int> EnumerateJobRestrictionBranchCandidates(int jobId)
        {
            if (jobId == 0)
            {
                yield return 0;
                yield break;
            }

            int candidate = jobId;
            while (candidate > 0)
            {
                yield return candidate;
                candidate /= 10;
            }
        }

        private static bool IsMysticDoorSkill(SkillData skill)
        {
            return skill?.SkillId == 2311002
                   || string.Equals(skill?.Name, "Mystic Door", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRocketBoosterSkill(SkillData skill)
        {
            return skill?.SkillId == RocketBoosterSkillId
                   || string.Equals(skill?.Name, "Rocket Booster", StringComparison.OrdinalIgnoreCase);
        }

        private static bool UsesTamingMobRestrictedSkill(SkillData skill)
        {
            return ClientOwnedVehicleSkillClassifier.UsesVehicleOwnershipOrMountSkill(skill);
        }

        private static bool IsClientForbiddenInUnableToUseSkillField(SkillData skill)
        {
            // Client evidence: the explicit nSkillID comparisons in
            // CUserLocal::{TryDoingMeleeAttack,TryDoingShootAttack,TryDoingMagicAttack}
            // reject this exact skill set when CField::IsUnableToUseSkill is active.
            return skill != null && ClientUnableToUseSkillForbiddenSet.Contains(skill.SkillId);
        }

        private static bool IsNoDragonField(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_NODRAGON
                   || mapInfo?.vanishDragon == true;
        }

        private static bool IsEvanDragonMagicAttack(SkillData skill)
        {
            // Client evidence: CUserLocal::TryDoingMagicAttack rejects dragon-root
            // skills when the local CDragon actor is absent. No-dragon fields are the
            // WZ-authored field seam that already suppresses that actor in MapSimulator.
            return skill?.AttackType == SkillAttackType.Magic
                   && Math.Abs(GetSkillRoot(skill.SkillId)) / 100 == 22;
        }

        private static int GetSkillRoot(int skillId)
        {
            int absoluteSkillId = Math.Abs(skillId);
            return absoluteSkillId >= 10000 ? absoluteSkillId / 10000 : absoluteSkillId;
        }

        private static bool UsesVehicleOwnershipOrMountSkill(SkillData skill)
        {
            return ClientOwnedVehicleSkillClassifier.UsesVehicleOwnershipOrMountSkill(skill);
        }

        private static bool IsMechanicSkill(int skillId)
        {
            int skillBookId = skillId / 10000;
            return skillBookId >= 3500 && skillBookId <= 3512;
        }

        private static bool IsMechanicVehicleActionName(string actionName)
        {
            return ClientOwnedVehicleSkillClassifier.IsMechanicVehicleActionName(actionName, includeTransformStates: true)
                   || string.Equals(actionName, "ladder2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rope2", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsAny(string value, params string[] fragments)
        {
            if (string.IsNullOrWhiteSpace(value) || fragments == null)
            {
                return false;
            }

            for (int i = 0; i < fragments.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(fragments[i])
                    && value.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
