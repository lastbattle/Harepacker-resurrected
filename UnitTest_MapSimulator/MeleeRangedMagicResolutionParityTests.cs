using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class MeleeRangedMagicResolutionParityTests
    {
        [Fact]
        public void ResolveClientTargetHitAnimation_MultipleLayerUsesHit0ForFirstTargetOnly()
        {
            SkillAnimation hitRoot = new() { Name = "hit" };
            SkillAnimation hit0 = new() { Name = "hit0" };
            SkillData skill = new()
            {
                SkillId = 15111004,
                HitEffect = hitRoot,
                TargetHitEffects = new List<SkillAnimation> { hitRoot },
                MultipleLayerTargetHitEffects = new List<SkillAnimation> { hit0, new SkillAnimation { Name = "hit1" } }
            };

            SkillAnimation firstTarget = SkillManager.ResolveClientTargetHitAnimation(
                skill,
                hitRoot,
                targetOrder: 0,
                damageIndex: 0,
                attackCount: 6);
            SkillAnimation secondTarget = SkillManager.ResolveClientTargetHitAnimation(
                skill,
                hitRoot,
                targetOrder: 1,
                damageIndex: 0,
                attackCount: 6);

            Assert.Same(hit0, firstTarget);
            Assert.Null(secondTarget);
        }

        [Fact]
        public void ResolveClientTargetHitAnimation_MultipleLayerLaterTargetsDoNotFallbackToRootHit()
        {
            SkillAnimation fallback = new() { Name = "fallback" };
            SkillData skill = new()
            {
                SkillId = 5121007,
                TargetHitEffects = new List<SkillAnimation> { fallback },
                MultipleLayerTargetHitEffects = new List<SkillAnimation> { new SkillAnimation { Name = "hit0" } }
            };

            SkillAnimation resolved = SkillManager.ResolveClientTargetHitAnimation(
                skill,
                fallback,
                targetOrder: 3,
                damageIndex: 2,
                attackCount: 6);

            Assert.Null(resolved);
        }
    }
}
