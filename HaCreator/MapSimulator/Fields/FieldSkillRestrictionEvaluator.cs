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
        private const int WildHunterSwallowAbsorbSkillId = 33101005;

        public sealed class RuntimeState
        {
            public bool CoconutBasicActionOwned { get; init; }
            public bool SnowBallBasicActionOwned { get; init; }
            public bool GuildBossBasicActionOwned { get; init; }
            public bool? HasLocalDragonActor { get; init; }
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

                if (IsSwallowAbsorbSkill(skill))
                {
                    return "Swallow absorb cannot be used in this field.";
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

            if (IsEvanDragonMagicAttack(skill)
                && (runtimeState?.HasLocalDragonActor == false
                    || (runtimeState?.HasLocalDragonActor != true && IsNoDragonField(mapInfo))))
            {
                return "Evan dragon magic skills cannot be used while the local dragon is unavailable.";
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

            if (MatchesAnyListedSkill(noSkillProperty, skill.SkillId))
            {
                return "This skill is forbidden in this field.";
            }

            if (MatchesAnyListedSkillClass(noSkillProperty, currentJobId, skill))
            {
                return "This field forbids skills for your job branch.";
            }

            return null;
        }

        private static bool MatchesAnyListedSkill(WzImageProperty noSkillProperty, int skillId)
        {
            foreach (WzImageProperty property in EnumerateNamedDescendants(noSkillProperty, "skill"))
            {
                if (MatchesListedSkill(property, skillId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesAnyListedSkillClass(WzImageProperty noSkillProperty, int currentJobId, SkillData skill)
        {
            foreach (WzImageProperty property in EnumerateNamedDescendants(noSkillProperty, "class"))
            {
                if (MatchesListedSkillClass(property, currentJobId, skill))
                {
                    return true;
                }
            }

            return false;
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

            foreach (int candidate in EnumerateSkillRestrictionClassCandidates(Math.Abs(currentJobId), skill))
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

        private static IEnumerable<WzImageProperty> EnumerateNamedDescendants(WzImageProperty root, string propertyName)
        {
            if (root == null || string.IsNullOrWhiteSpace(propertyName))
            {
                yield break;
            }

            Stack<WzImageProperty> pending = new Stack<WzImageProperty>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                WzImageProperty current = pending.Pop();
                if (string.Equals(current?.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return current;
                }

                if (current?.WzProperties == null)
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

        private static IEnumerable<int> EnumerateSkillRestrictionClassCandidates(int currentJobId, SkillData skill)
        {
            HashSet<int> yielded = new HashSet<int>();

            if (skill == null)
            {
                foreach (int candidate in EnumerateClassCandidatesForJobId(currentJobId))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                yield break;
            }

            if (skill.IsFourthJob)
            {
                if (yielded.Add(4))
                {
                    yield return 4;
                }
            }

            foreach (int candidate in EnumerateClassCandidatesForJobId(Math.Abs(skill.Job)))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (int candidate in EnumerateClassCandidatesForJobId(Math.Abs(skill.SkillId / 10000)))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            if (yielded.Count > 0)
            {
                yield break;
            }

            foreach (int candidate in EnumerateClassCandidatesForJobId(currentJobId))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<int> EnumerateClassCandidatesForJobId(int jobId)
        {
            int classGrade = GetClientJobClassGrade(jobId);
            if (classGrade > 0)
            {
                yield return classGrade;
            }
        }

        private static int GetClientJobClassGrade(int jobId)
        {
            // WZ noSkill/class entries are authored as advancement tiers, not full job ids.
            jobId = Math.Abs(jobId);
            if (jobId < 100)
            {
                return 0;
            }

            if (jobId >= 800 && jobId < 1000)
            {
                return 1;
            }

            if (jobId == 2001)
            {
                return 1;
            }

            if (jobId >= 430 && jobId <= 434)
            {
                // Dual Blade roots advance through 430, 431, 432, 433, 434.
                // The client-facing noSkill/class surface still uses the small
                // advancement tiers 1-4 rather than those full irregular job ids.
                return Math.Min(4, (jobId - 430) + 1);
            }

            if (jobId >= 2200 && jobId <= 2218)
            {
                if (jobId == 2200)
                {
                    return 1;
                }

                if (jobId == 2210)
                {
                    return 2;
                }

                return Math.Min(4, (jobId - 2210) + 2);
            }

            int branchDigit = jobId % 10;
            if (branchDigit == 1)
            {
                return 3;
            }

            if (branchDigit == 2)
            {
                return 4;
            }

            return jobId % 100 == 0 ? 1 : 2;
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

        private static bool IsSwallowAbsorbSkill(SkillData skill)
        {
            // Client evidence: CUserLocal::TryDoingSwallowAbsorb checks
            // CField::IsUnableToUseSkill directly before sending the absorb packet.
            return skill?.SkillId == WildHunterSwallowAbsorbSkillId;
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
