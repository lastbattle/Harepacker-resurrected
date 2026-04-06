using System.Collections;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public sealed class PreparedSkillAvatarTransformParityTests
    {
        private static readonly Type PlayerCharacterType = typeof(PlayerCharacter);
        private static readonly Type SkillManagerType = typeof(SkillManager);

        [Theory]
        [InlineData(14111006, "dash", "darkTornado_pre", "darkTornado_after")]
        [InlineData(32121003, "cyclone_pre", "cyclone", "cyclone_after")]
        [InlineData(5311002, "noiseWave_pre", "noiseWave_ing", "noiseWave")]
        [InlineData(23121000, "dualVulcanPrep", "dualVulcanLoop", "dualVulcanEnd")]
        [InlineData(31001000, "bluntSmashEnd", "bluntSmash", null)]
        [InlineData(31101000, "soulEater_end", "bluntSmash", null)]
        public void TryCreateBuiltInSkillAvatarTransform_RemapsWzOwnedPreparedFamiliesToRenderableAvatarActions(
            int skillId,
            string requestedActionName,
            string expectedStandActionName,
            string? expectedExitActionName)
        {
            object transform = InvokeBuiltInTransform(skillId, requestedActionName);

            Assert.Equal(expectedStandActionName, GetActionNames(transform, "StandActionNames").First());
            Assert.Equal(expectedExitActionName, GetStringProperty(transform, "ExitActionName"));
        }

        [Theory]
        [InlineData(33101005, "swallow_pre", "swallow_pre", null)]
        [InlineData(33101005, "swallow_loop", "swallow_loop", "swallow")]
        [InlineData(14111006, "darkTornado", "darkTornado", "darkTornado_after")]
        [InlineData(35001001, "flamethrower_pre", "flamethrower_pre", null)]
        [InlineData(35001001, "flamethrower", "flamethrower", "flamethrower_after")]
        [InlineData(35101009, "flamethrower_pre2", "flamethrower_pre2", null)]
        [InlineData(35101009, "flamethrower2", "flamethrower2", "flamethrower_after2")]
        public void TryCreateBuiltInSkillAvatarTransform_PreservesPrepareAndHoldStagesForPreparedFamilies(
            int skillId,
            string requestedActionName,
            string expectedStandActionName,
            string? expectedExitActionName)
        {
            object transform = InvokeBuiltInTransform(skillId, requestedActionName);

            Assert.Equal(expectedStandActionName, GetActionNames(transform, "StandActionNames").First());
            Assert.Equal(expectedExitActionName, GetStringProperty(transform, "ExitActionName"));
        }

        [Theory]
        [InlineData("rbooster_pre", "rbooster_pre", "rbooster_after")]
        [InlineData("rbooster", "rbooster", "rbooster_after")]
        [InlineData("tank_rbooster_pre", "tank_rbooster_pre", "tank_rbooster_after")]
        public void TryCreateBuiltInSkillAvatarTransform_KeepsRocketBoosterStartupAndExitFamilies(
            string requestedActionName,
            string expectedStandActionName,
            string expectedExitActionName)
        {
            object transform = InvokeBuiltInTransform(35101004, requestedActionName);

            Assert.Equal(expectedStandActionName, GetActionNames(transform, "StandActionNames").First());
            Assert.Equal("rbooster", GetActionNames(transform, "JumpActionNames").First());
            Assert.Equal(expectedExitActionName, GetStringProperty(transform, "ExitActionName"));
        }

        [Theory]
        [InlineData("tank_pre", "tank_stand", "tank_walk", "tank", "tank_after", false)]
        [InlineData("siege_pre", "siege_stand", "siege_stand", "siege", "siege_after", true)]
        [InlineData("tank_siegepre", "tank_siegestand", "tank_siegestand", "tank_siegeattack", "tank_siegeafter", true)]
        public void TryCreateBuiltInSkillAvatarTransform_KeepsMechanicStateFamiliesOnDedicatedAvatarBranches(
            string requestedActionName,
            string expectedStandActionName,
            string expectedWalkActionName,
            string expectedAttackActionName,
            string expectedExitActionName,
            bool expectedLocksMovement)
        {
            object transform = InvokeBuiltInTransform(0, requestedActionName);

            Assert.Equal(expectedStandActionName, GetActionNames(transform, "StandActionNames").First());
            Assert.Equal(expectedWalkActionName, GetActionNames(transform, "WalkActionNames").First());
            Assert.Equal(expectedAttackActionName, GetActionNames(transform, "AttackActionNames").First());
            Assert.Equal(expectedExitActionName, GetStringProperty(transform, "ExitActionName"));
            Assert.Equal(expectedLocksMovement, GetBoolProperty(transform, "LocksMovement"));
        }

        [Theory]
        [InlineData(14111006, "dash", "darkTornado", new[] { "darkTornado_pre", "dash", "darkTornado" })]
        [InlineData(33101005, "swallow_loop", "swallow", new[] { "swallow_pre", "swallow_loop", "swallow" })]
        [InlineData(32121003, "cyclone_pre", "cyclone_pre", new[] { "cyclone", "cyclone_pre" })]
        [InlineData(5311002, "noiseWave_pre", "noiseWave", new[] { "noiseWave_ing", "noiseWave_pre", "noiseWave" })]
        [InlineData(23121000, "dualVulcanPrep", "dualVulcanLoop", new[] { "dualVulcanLoop", "dualVulcanPrep" })]
        public void EnumeratePreparedAvatarActionCandidates_PrioritizesConfirmedBodyBackedPrepareRemaps(
            int skillId,
            string prepareActionName,
            string actionName,
            string[] expectedPrefix)
        {
            var skill = new SkillData
            {
                SkillId = skillId,
                PrepareActionName = prepareActionName,
                ActionName = actionName
            };

            string[] candidates = InvokeStringSequence(
                SkillManagerType,
                "EnumeratePreparedAvatarActionCandidates",
                skill,
                prepareActionName);

            Assert.Equal(expectedPrefix, candidates.Take(expectedPrefix.Length).ToArray());
        }

        [Theory]
        [InlineData(31001000, "bluntSmashEnd", "bluntSmash", new[] { "bluntSmashEnd", "bluntSmash" })]
        [InlineData(31101000, "soulEater_end", "bluntSmash", new[] { "soulEater_end", "soulEater", "bluntSmash" })]
        public void EnumerateKeydownEndAvatarActionCandidates_FallsBackToRenderableReleaseFamilies(
            int skillId,
            string keydownEndActionName,
            string actionName,
            string[] expectedPrefix)
        {
            var skill = new SkillData
            {
                SkillId = skillId,
                ActionName = actionName,
                KeydownEndActionName = keydownEndActionName
            };

            string[] candidates = InvokeStringSequence(
                SkillManagerType,
                "EnumerateKeydownEndAvatarActionCandidates",
                skill,
                keydownEndActionName);

            Assert.Equal(expectedPrefix, candidates.Take(expectedPrefix.Length).ToArray());
        }

        private static object InvokeBuiltInTransform(int skillId, string actionName)
        {
            MethodInfo method = PlayerCharacterType.GetMethod(
                "TryCreateBuiltInSkillAvatarTransform",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            object?[] args = { skillId, actionName, null };

            bool created = (bool)method.Invoke(null, args)!;

            Assert.True(created);
            Assert.NotNull(args[2]);
            return args[2]!;
        }

        private static string[] InvokeStringSequence(Type type, string methodName, params object?[] args)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!;
            var sequence = (IEnumerable)method.Invoke(null, args)!;

            return sequence.Cast<object?>()
                .Select(value => value?.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToArray();
        }

        private static IReadOnlyList<string> GetActionNames(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            var value = (IEnumerable<string>)property.GetValue(instance)!;
            return value.ToArray();
        }

        private static string? GetStringProperty(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            return property.GetValue(instance) as string;
        }

        private static bool GetBoolProperty(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            return (bool)property.GetValue(instance)!;
        }
    }
}
