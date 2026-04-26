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
        private const int MechanicSiegeModeSkillId = 35111005;
        private static readonly HashSet<int> MysticDoorSkillIds = new HashSet<int>
        {
            2311002,
            8001,
            10008001,
            20008001,
            20018001,
            30008001
        };

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
            5121004,
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
            15101003,
            32101001,
            32111011,
            35001003,
            1121001,
            1321001,
            3120010
        };

        private static readonly HashSet<int> ClientDojoOrBalrogOnlySkillIds = new HashSet<int>
        {
            1009,
            1010,
            1011,
            10001009,
            10001010,
            10001011,
            20001009,
            20001010,
            20001011,
            20011009,
            20011010,
            20011011,
            30001009,
            30001010,
            30001011
        };

        private static readonly HashSet<int> ClientMassacreOnlySkillIds = new HashSet<int>
        {
            1020,
            10001020,
            20001020,
            20011020,
            30001020
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

            if (HasMobMassacreDisableSkillFlag(mapInfo))
            {
                return "Skills cannot be used while the Mu Lung Dojo massacre field disables skill usage.";
            }

            string fieldTypeRestrictionMessage = GetClientFieldTypeSkillRestrictionMessage(mapInfo, skill);
            if (!string.IsNullOrWhiteSpace(fieldTypeRestrictionMessage))
            {
                return fieldTypeRestrictionMessage;
            }

            string noSkillRestrictionMessage = GetNoSkillRestrictionMessage(mapInfo, skill, currentJobId);
            if (!string.IsNullOrWhiteSpace(noSkillRestrictionMessage))
            {
                return noSkillRestrictionMessage;
            }

            // Client evidence: CUserLocal::DoActiveSkill rejects Evan current jobs
            // in FIELDTYPE_NODRAGON before dispatching the requested skill family.
            if (mapInfo.fieldType == FieldType.FIELDTYPE_NODRAGON
                && IsEvanJobId(currentJobId > 0 ? currentJobId : skill.Job))
            {
                return "Evan characters cannot use active skills in no-dragon fields.";
            }

            if (mapInfo.fieldType == FieldType.FIELDTYPE_COCONUT
                && runtimeState?.CoconutBasicActionOwned == true)
            {
                return "Skills cannot be used while the Coconut minigame owns basic attacks.";
            }

            if (mapInfo.fieldType == FieldType.FIELDTYPE_SNOWBALL
                && runtimeState?.SnowBallBasicActionOwned == true)
            {
                return "Skills cannot be used while the Snowball minigame owns basic attacks.";
            }

            if (mapInfo.fieldType == FieldType.FIELDTYPE_GUILDBOSS
                && runtimeState?.GuildBossBasicActionOwned == true)
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

            if (MatchesAnyListedSkillClass(noSkillProperty, currentJobId, skill))
            {
                return "This field forbids skills for your job branch.";
            }

            if (MatchesAnyListedSkill(noSkillProperty, skill.SkillId))
            {
                return "This skill is forbidden in this field.";
            }

            return null;
        }

        private static string GetClientFieldTypeSkillRestrictionMessage(MapInfo mapInfo, SkillData skill)
        {
            if (mapInfo == null || skill == null)
            {
                return null;
            }

            if (mapInfo.fieldType == FieldType.FIELDTYPE_MONSTERCARNIVAL_NOT_USE
                && skill.SkillId == MechanicSiegeModeSkillId)
            {
                return "This Mechanic skill cannot be used in Monster Carnival restricted fields.";
            }

            if (ClientDojoOrBalrogOnlySkillIds.Contains(skill.SkillId)
                && mapInfo.fieldType != FieldType.FIELDTYPE_DOJANG
                && mapInfo.fieldType != FieldType.FIELDTYPE_BALROG)
            {
                return "This event skill can only be used in Dojo or Balrog fields.";
            }

            if (ClientMassacreOnlySkillIds.Contains(skill.SkillId)
                && mapInfo.fieldType != FieldType.FIELDTYPE_MASSACRE)
            {
                return "This event skill can only be used in massacre fields.";
            }

            return null;
        }

        private static bool MatchesAnyListedSkill(WzImageProperty noSkillProperty, int skillId)
        {
            foreach (WzImageProperty property in EnumerateNamedChildren(noSkillProperty, "skill"))
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
            foreach (WzImageProperty property in EnumerateNamedChildren(noSkillProperty, "class"))
            {
                if (MatchesListedSkillClass(property, skill))
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

        private static bool HasMobMassacreDisableSkillFlag(MapInfo mapInfo)
        {
            WzImageProperty mobMassacreProperty = FindAdditionalFieldProperty(mapInfo, "mobMassacre");
            WzImageProperty disableSkillProperty = mobMassacreProperty?["disableSkill"] as WzImageProperty;
            return TryReadInt(disableSkillProperty, out int value) && value != 0;
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

            if (mapInfo?.unsupportedInfoProperties != null)
            {
                for (int i = 0; i < mapInfo.unsupportedInfoProperties.Count; i++)
                {
                    WzImageProperty property = mapInfo.unsupportedInfoProperties[i];
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

        private static bool MatchesListedSkillClass(WzImageProperty property, SkillData skill)
        {
            if (property == null)
            {
                return false;
            }

            foreach (int listedClass in EnumerateIntValues(property))
            {
                if (MatchesClientSkillClass(listedClass, skill))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<int> EnumerateIntValues(WzImageProperty property)
        {
            if (property == null)
            {
                yield break;
            }

            Stack<WzImageProperty> pending = new Stack<WzImageProperty>();
            pending.Push(property);
            while (pending.Count > 0)
            {
                WzImageProperty current = pending.Pop();
                if (TryReadInt(current, out int value))
                {
                    yield return value;
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

        private static IEnumerable<WzImageProperty> EnumerateNamedChildren(WzImageProperty root, string propertyName)
        {
            if (root == null || string.IsNullOrWhiteSpace(propertyName))
            {
                yield break;
            }

            WzPropertyCollection children = root.WzProperties;
            if (children == null)
            {
                yield break;
            }

            for (int i = 0; i < children.Count; i++)
            {
                WzImageProperty child = children[i];
                if (string.Equals(child?.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return child;
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

        private static bool MatchesClientSkillClass(int listedClass, SkillData skill)
        {
            if (listedClass <= 0)
            {
                return false;
            }

            int skillId = Math.Abs(skill?.SkillId ?? 0);
            if (skillId <= 0)
            {
                return false;
            }

            int skillRoot = skillId / 10000;

            if (IsEvanSkillClassRoot(skillRoot))
            {
                int evanClass = GetClientJobClassGrade(skillRoot);
                return listedClass switch
                {
                    1 => evanClass >= 1 && evanClass <= 2,
                    2 => evanClass >= 3 && evanClass <= 6,
                    3 => evanClass >= 7 && evanClass <= 8,
                    4 => evanClass >= 9 && evanClass <= 10,
                    _ => false
                };
            }

            if (IsDualBladeSkillClassRoot(skillRoot))
            {
                int dualBladeStep = skillRoot - 430;
                return listedClass switch
                {
                    1 => dualBladeStep == 0,
                    2 => dualBladeStep >= 1 && dualBladeStep <= 2,
                    3 => dualBladeStep == 3,
                    4 => dualBladeStep == 4,
                    _ => false
                };
            }

            int skillClass = GetClientJobClassGrade(skillRoot);
            if (skillClass > 0)
            {
                return listedClass == skillClass;
            }

            return false;
        }

        private static bool IsEvanSkillClassRoot(int jobId)
        {
            return jobId / 100 == 22 || jobId == 2001;
        }

        private static bool IsDualBladeSkillClassRoot(int jobId)
        {
            return jobId / 10 == 43;
        }

        private static int GetClientJobClassGrade(int jobId)
        {
            // Client evidence:
            // get_skill_class -> get_job_level(nSkillID / 10000).
            // Mirror get_job_level so noSkill/class matches Field::SkillInfo::IsSkill.
            jobId = Math.Abs(jobId);
            if (jobId % 100 == 0 || jobId == 2001)
            {
                return 1;
            }

            int advancementSeed;
            if (jobId / 10 == 43)
            {
                advancementSeed = (jobId - 430) / 2;
            }
            else
            {
                advancementSeed = jobId % 10;
            }

            int classGrade = advancementSeed + 2;
            if (classGrade < 2)
            {
                return 0;
            }

            if (classGrade <= 4)
            {
                return classGrade;
            }

            return classGrade <= 10 && IsEvanJobId(jobId) ? classGrade : 0;
        }

        private static bool IsMysticDoorSkill(SkillData skill)
        {
            return skill != null
                   && (MysticDoorSkillIds.Contains(skill.SkillId)
                       || string.Equals(skill.Name, "Mystic Door", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(skill.Name, "Decent Mystic Door", StringComparison.OrdinalIgnoreCase));
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

        private static bool IsEvanJobId(int jobId)
        {
            jobId = Math.Abs(jobId);
            return jobId == 2001 || jobId / 100 == 22;
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
