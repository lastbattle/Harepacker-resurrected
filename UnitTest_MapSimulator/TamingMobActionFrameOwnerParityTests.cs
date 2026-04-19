using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class TamingMobActionFrameOwnerParityTests
    {
        [Fact]
        public void MechanicFrameOwner_RemapFallback_MapsBraveSlash3ToLadder2()
        {
            CharacterPart mechanicMount = CreateMechanicMount("ladder2", "stand1");
            var owner = new TamingMobActionFrameOwner(1932016);

            CharacterAnimation animation = owner.GetAnimation(mechanicMount, "braveslash3");

            Assert.NotNull(animation);
            Assert.True(string.Equals("ladder2", animation.ActionName, StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("braveslash4")]
        [InlineData("chargeBlow")]
        public void MechanicFrameOwner_RemapFallback_MapsToStand1(string actionName)
        {
            CharacterPart mechanicMount = CreateMechanicMount("stand1");
            var owner = new TamingMobActionFrameOwner(1932016);

            CharacterAnimation animation = owner.GetAnimation(mechanicMount, actionName);

            Assert.NotNull(animation);
            Assert.True(string.Equals("stand1", animation.ActionName, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void MechanicFrameOwner_DoesNotApplyRemapFallback_ToBraveSlash1()
        {
            CharacterPart mechanicMount = CreateMechanicMount("ladder2", "stand1");
            var owner = new TamingMobActionFrameOwner(1932016);

            CharacterAnimation animation = owner.GetAnimation(mechanicMount, "braveslash1");

            Assert.Null(animation);
        }

        [Fact]
        public void MechanicFrameOwner_RemapFallback_MapsAlertToStand1_EvenWhenAlertExists()
        {
            CharacterPart mechanicMount = CreateMechanicMount("alert", "stand1");
            var owner = new TamingMobActionFrameOwner(1932016);

            CharacterAnimation animation = owner.GetAnimation(mechanicMount, "alert");

            Assert.NotNull(animation);
            Assert.True(string.Equals("stand1", animation.ActionName, StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("braveslash3")]
        [InlineData("braveslash4")]
        [InlineData("chargeBlow")]
        public void BattleshipFrameOwner_RemapFallback_MapsToStand1_WhenOnlyStand1Exists(string actionName)
        {
            CharacterPart battleshipMount = CreateBattleshipMount("stand1");
            var owner = new TamingMobActionFrameOwner(1932000);

            CharacterAnimation animation = owner.GetAnimation(battleshipMount, actionName);

            Assert.NotNull(animation);
            Assert.True(string.Equals("stand1", animation.ActionName, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void BattleshipFrameOwner_RemapFallback_MapsAlertToStand1()
        {
            CharacterPart battleshipMount = CreateBattleshipMount("stand1");
            var owner = new TamingMobActionFrameOwner(1932000);

            CharacterAnimation animation = owner.GetAnimation(battleshipMount, "alert");

            Assert.NotNull(animation);
            Assert.True(string.Equals("stand1", animation.ActionName, StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("iceStrike")]
        [InlineData("quadBlow")]
        public void KnownVehicleFrameOwner_Preserves_ExactAuthoredPreGateActionNames(string actionName)
        {
            CharacterPart mechanicMount = CreateMechanicMount(actionName);
            var owner = new TamingMobActionFrameOwner(1932016);

            CharacterAnimation animation = owner.GetAnimation(mechanicMount, actionName);

            Assert.NotNull(animation);
            Assert.True(string.Equals(actionName, animation.ActionName, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void MechanicFrameOwner_RejectsWzOnlyGatlingShot_ForOwnerOnlyPreservation()
        {
            CharacterPart mechanicMount = CreateMechanicMount("gatlingshot");
            var owner = new TamingMobActionFrameOwner(1932016);

            CharacterAnimation animation = owner.GetAnimation(mechanicMount, "gatlingshot");

            Assert.Null(animation);
        }

        private static CharacterPart CreateMechanicMount(params string[] publishedActionNames)
        {
            return CreateMount(1932016, "MechanicMount", publishedActionNames);
        }

        private static CharacterPart CreateBattleshipMount(params string[] publishedActionNames)
        {
            return CreateMount(1932000, "BattleshipMount", publishedActionNames);
        }

        private static CharacterPart CreateMount(int itemId, string name, params string[] publishedActionNames)
        {
            var availableAnimations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string actionName in publishedActionNames ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(actionName))
                {
                    availableAnimations.Add(actionName);
                }
            }

            return new CharacterPart
            {
                ItemId = itemId,
                Name = name,
                Type = CharacterPartType.TamingMob,
                Slot = EquipSlot.TamingMob,
                AvailableAnimations = availableAnimations,
                AnimationResolver = actionName =>
                {
                    if (string.IsNullOrWhiteSpace(actionName) || !availableAnimations.Contains(actionName))
                    {
                        return null;
                    }

                    return CreateSingleFrameAnimation(actionName);
                }
            };
        }

        private static CharacterAnimation CreateSingleFrameAnimation(string actionName)
        {
            return new CharacterAnimation
            {
                ActionName = actionName,
                Frames = new List<CharacterFrame>
                {
                    new()
                }
            };
        }
    }
}
