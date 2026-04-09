using System.Linq;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class MovingShootParityTests
    {
        [Fact]
        public void TryResolveMovingShootAttackActionTypeFromCurrentAction_ClassifiesClientShootMagicAndMeleeFamilies()
        {
            Assert.True(SkillManager.TryResolveMovingShootAttackActionTypeFromCurrentAction(null, 22, out int shootType));
            Assert.True(SkillManager.TryResolveMovingShootAttackActionTypeFromCurrentAction(null, 45, out int alternateShootType));
            Assert.True(SkillManager.TryResolveMovingShootAttackActionTypeFromCurrentAction(null, 48, out int magicType));
            Assert.True(SkillManager.TryResolveMovingShootAttackActionTypeFromCurrentAction("proneStab", null, out int meleeType));

            Assert.Equal(shootType, alternateShootType);
            Assert.NotEqual(shootType, magicType);
            Assert.NotEqual(shootType, meleeType);
            Assert.NotEqual(magicType, meleeType);
        }

        [Fact]
        public void ResolveQueuedMovingShootEntryAction_UsesTwoHandedStabFallbackForSpear()
        {
            var skill = new SkillData
            {
                SkillId = 33121005,
                ClientInfoType = 3
            };

            (string actionName, int? rawActionCode) = SkillManager.ResolveQueuedMovingShootEntryAction(
                skill,
                currentActionName: null,
                currentRawActionCode: null,
                currentWeaponType: "spear",
                nextCandidateIndex: _ => 0);

            Assert.Equal("stabT1", actionName);
            Assert.True(rawActionCode.HasValue);
        }

        [Fact]
        public void ResolveQueuedMovingShootEntryAction_KeepsPolearmFallbackForPolearm()
        {
            var skill = new SkillData
            {
                SkillId = 33121005,
                ClientInfoType = 3
            };

            (string actionName, int? rawActionCode) = SkillManager.ResolveQueuedMovingShootEntryAction(
                skill,
                currentActionName: null,
                currentRawActionCode: null,
                currentWeaponType: "polearm",
                nextCandidateIndex: _ => 0);

            Assert.Equal("swingP1", actionName);
            Assert.True(rawActionCode.HasValue);
        }

        [Fact]
        public void ResolveQueuedMovingShootEntryAction_Type3UsesShootFamilyWhenQueuedAttackTypeIsShoot()
        {
            var skill = new SkillData
            {
                SkillId = 33121009,
                ClientInfoType = 3
            };

            (string actionName, int? rawActionCode) = SkillManager.ResolveQueuedMovingShootEntryAction(
                skill,
                currentActionName: "swingO1",
                currentRawActionCode: null,
                queuedAttackActionType: 1,
                currentWeaponType: "gun",
                nextCandidateIndex: _ => 0);

            Assert.Equal("shoot1", actionName);
            Assert.True(rawActionCode.HasValue);
        }

        [Fact]
        public void ResolveQueuedMovingShootEntryAction_Type3RejectsIncompatibleCurrentActionOwnerWithoutMatchingFallback()
        {
            var skill = new SkillData
            {
                SkillId = 33121009,
                ClientInfoType = 3
            };

            (string actionName, int? rawActionCode) = SkillManager.ResolveQueuedMovingShootEntryAction(
                skill,
                currentActionName: "swingO1",
                currentRawActionCode: null,
                queuedAttackActionType: 1,
                currentWeaponType: null,
                nextCandidateIndex: _ => 0);

            Assert.Null(actionName);
            Assert.False(rawActionCode.HasValue);
        }

        [Fact]
        public void ResolveQueuedMovingShootEntryAction_Type3CancelsWhenOnlyMagicAttackTypeFallbackExists()
        {
            var skill = new SkillData
            {
                SkillId = 22161005,
                ClientInfoType = 3
            };

            (string actionName, int? rawActionCode) = SkillManager.ResolveQueuedMovingShootEntryAction(
                skill,
                currentActionName: null,
                currentRawActionCode: null,
                queuedAttackActionType: 2,
                currentWeaponType: "staff",
                nextCandidateIndex: _ => 0);

            Assert.Null(actionName);
            Assert.False(rawActionCode.HasValue);
        }

        [Fact]
        public void EnumerateQueuedMovingShootEntryClientRandomActionCandidates_PrefersLiveCurrentActionFamilyOverWeaponFallback()
        {
            var candidates = SkillManager.EnumerateQueuedMovingShootEntryClientRandomActionCandidates(
                    queuedAttackActionType: null,
                    currentActionName: "swingP2",
                    currentRawActionCode: null,
                    currentWeaponType: "spear")
                .Select(candidate => candidate.ActionName)
                .ToArray();

            Assert.Equal(new[] { "swingP1", "swingP2", "swingPF", "swingP1PoleArm", "swingP2PoleArm" }, candidates);
        }

        [Fact]
        public void ResolveMovingShootAntiRepeatCountLimit_PrefersBypassThenRapidAttackAllowance()
        {
            var bypassSkill = new SkillData
            {
                SkillId = 33121009,
                IsRapidAttack = true
            };
            var rapidAttackSkill = new SkillData
            {
                SkillId = 35001001,
                IsRapidAttack = true
            };

            Assert.Equal(int.MaxValue, SkillManager.ResolveMovingShootAntiRepeatCountLimit(bypassSkill));
            Assert.Equal(1, SkillManager.ResolveMovingShootAntiRepeatCountLimit(rapidAttackSkill));
        }
    }
}
