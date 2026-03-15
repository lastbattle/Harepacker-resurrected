using System.Reflection;
using HaCreator.MapSimulator.Character;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class PlayerSkillAvatarTransformTests
    {
        [Fact]
        public void MechanicTransform_PrefersMovementSpecificAvatarActionsWhenAvailable()
        {
            var player = new PlayerCharacter(CreateBuild(
                "tank_stand",
                "tank_walk",
                "tank_jump",
                "tank_ladder",
                "tank_fly",
                "tank_hit",
                "tank",
                "tank_prone"));

            Assert.True(player.ApplySkillAvatarTransform(35121005, actionName: null));

            Assert.Equal("tank_jump", ResolveTransformAction(player, PlayerState.Jumping));
            Assert.Equal("tank_ladder", ResolveTransformAction(player, PlayerState.Ladder));
            Assert.Equal("tank_fly", ResolveTransformAction(player, PlayerState.Flying));
            Assert.Equal("tank_hit", ResolveTransformAction(player, PlayerState.Hit));
        }

        [Fact]
        public void MechanicTransform_FallsBackToStandActionWhenMovementVariantIsMissing()
        {
            var player = new PlayerCharacter(CreateBuild("siege_stand", "siege"));

            Assert.True(player.ApplySkillAvatarTransform(35111004, actionName: null));

            Assert.Equal("siege_stand", ResolveTransformAction(player, PlayerState.Jumping));
            Assert.Equal("siege_stand", ResolveTransformAction(player, PlayerState.Ladder));
            Assert.Equal("siege_stand", ResolveTransformAction(player, PlayerState.Flying));
            Assert.Equal("siege_stand", ResolveTransformAction(player, PlayerState.Hit));
        }

        private static CharacterBuild CreateBuild(params string[] actionNames)
        {
            var body = new BodyPart();
            foreach (string actionName in actionNames)
            {
                body.Animations[actionName] = new CharacterAnimation
                {
                    ActionName = actionName
                };
            }

            return new CharacterBuild
            {
                Body = body
            };
        }

        private static string ResolveTransformAction(PlayerCharacter player, PlayerState state)
        {
            MethodInfo method = typeof(PlayerCharacter).GetMethod(
                "GetSkillTransformActionName",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);
            return (string)method.Invoke(player, new object[] { state });
        }
    }
}
