using System.Collections.Generic;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator
{
    public sealed class FollowCharacterEligibilityResolverTests
    {
        private const int MechanicTamingMobItemId = 1932016;
        private const int BattleshipTamingMobItemId = 1932000;

        [Theory]
        [InlineData("flamethrower")]
        [InlineData("rbooster_pre")]
        [InlineData("msummon")]
        [InlineData("msummon2")]
        [InlineData("earthslug")]
        [InlineData("herbalism_mechanic")]
        public void ResolveMountedVehicleId_OwnerlessBorrowedMechanicActionsDoNotInferMount(string actionName)
        {
            int mountedVehicleId = FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
                null,
                actionName,
                mechanicMode: null);

            Assert.Equal(0, mountedVehicleId);
        }

        [Theory]
        [InlineData("tank")]
        [InlineData("siege")]
        [InlineData("tank_stand")]
        [InlineData("tank_siegeattack")]
        public void ResolveMountedVehicleId_OwnerlessDistinctMechanicActionsStillInferMount(string actionName)
        {
            int mountedVehicleId = FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
                null,
                actionName,
                mechanicMode: null);

            Assert.Equal(MechanicTamingMobItemId, mountedVehicleId);
        }

        [Fact]
        public void ResolveMountedVehicleId_EquippedMechanicMountPreservesBorrowedMechanicAction()
        {
            CharacterPart mountPart = CreateTamingMobPart(MechanicTamingMobItemId, "flamethrower");

            int mountedVehicleId = FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
                mountPart,
                "flamethrower",
                mechanicMode: null);

            Assert.Equal(MechanicTamingMobItemId, mountedVehicleId);
        }

        [Fact]
        public void ResolveMountedVehicleId_ActiveMechanicMountPreservesBorrowedMechanicAction()
        {
            CharacterPart mountPart = CreateTamingMobPart(MechanicTamingMobItemId, "flamethrower");

            int mountedVehicleId = FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
                tamingMobPart: null,
                actionName: "flamethrower",
                mechanicMode: null,
                activeMountedRenderOwner: mountPart);

            Assert.Equal(MechanicTamingMobItemId, mountedVehicleId);
        }

        [Fact]
        public void ResolveMountedVehicleId_OwnerlessBattleshipActionStillInfersMount()
        {
            int mountedVehicleId = FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
                null,
                "cannon",
                mechanicMode: null);

            Assert.Equal(BattleshipTamingMobItemId, mountedVehicleId);
        }

        [Theory]
        [InlineData("flamethrower")]
        [InlineData("rbooster_pre")]
        [InlineData("msummon")]
        [InlineData("earthslug")]
        public void ResolveClientOwnedVehicleCurrentActionMountItemId_OwnerlessBorrowedMechanicActionsDoNotInferMount(
            string actionName)
        {
            int mountItemId = SkillManager.ResolveClientOwnedVehicleCurrentActionMountItemId(
                actionName,
                activeSkillMountItemId: 0,
                equippedMountItemId: 0);

            Assert.Equal(0, mountItemId);
        }

        [Theory]
        [InlineData("tank")]
        [InlineData("siege")]
        [InlineData("tank_stand")]
        [InlineData("tank_siegeattack")]
        public void ResolveClientOwnedVehicleCurrentActionMountItemId_OwnerlessDistinctMechanicActionsStillInferMount(
            string actionName)
        {
            int mountItemId = SkillManager.ResolveClientOwnedVehicleCurrentActionMountItemId(
                actionName,
                activeSkillMountItemId: 0,
                equippedMountItemId: 0);

            Assert.Equal(MechanicTamingMobItemId, mountItemId);
        }

        [Fact]
        public void ResolveClientOwnedVehicleCurrentActionMountItemId_ActiveMechanicMountPreservesBorrowedAction()
        {
            int mountItemId = SkillManager.ResolveClientOwnedVehicleCurrentActionMountItemId(
                "flamethrower",
                activeSkillMountItemId: MechanicTamingMobItemId,
                equippedMountItemId: 0);

            Assert.Equal(MechanicTamingMobItemId, mountItemId);
        }

        private static CharacterPart CreateTamingMobPart(int itemId, params string[] actionNames)
        {
            var part = new CharacterPart
            {
                ItemId = itemId,
                Slot = EquipSlot.TamingMob,
                Type = CharacterPartType.TamingMob,
                Animations = new Dictionary<string, CharacterAnimation>(System.StringComparer.OrdinalIgnoreCase)
            };

            foreach (string actionName in actionNames)
            {
                part.Animations[actionName] = new CharacterAnimation
                {
                    ActionName = actionName
                };
            }

            return part;
        }
    }
}
