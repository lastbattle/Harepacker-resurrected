using System;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class FollowCharacterEligibilityResolver
    {
        private const int BattleshipTamingMobItemId = 1932000;
        private const int MechanicTamingMobItemId = 1932016;

        public static bool IsMountedState(CharacterPart tamingMobPart, string actionName, int? mechanicMode = null)
        {
            return ResolveMountedVehicleId(tamingMobPart, actionName, mechanicMode) > 0;
        }

        public static int ResolveMountedVehicleId(CharacterPart tamingMobPart, string actionName, int? mechanicMode = null)
        {
            return ResolveMountedVehicleId(tamingMobPart, actionName, mechanicMode, activeMountedRenderOwner: null);
        }

        internal static int ResolveMountedVehicleId(
            CharacterPart tamingMobPart,
            string actionName,
            int? mechanicMode,
            CharacterPart activeMountedRenderOwner)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return 0;
            }

            if (activeMountedRenderOwner?.Slot == EquipSlot.TamingMob
                && CharacterAssembler.SupportsTamingMobAction(activeMountedRenderOwner, actionName))
            {
                return activeMountedRenderOwner.ItemId;
            }

            if (tamingMobPart?.Slot == EquipSlot.TamingMob
                && CharacterAssembler.SupportsTamingMobAction(tamingMobPart, actionName))
            {
                return tamingMobPart.ItemId;
            }

            if (ClientOwnedVehicleSkillClassifier.IsBattleshipMountedActionName(actionName))
            {
                return BattleshipTamingMobItemId;
            }

            if (mechanicMode.GetValueOrDefault() > 0
                || ClientOwnedVehicleSkillClassifier.IsOwnerlessMechanicVehicleInferenceActionName(
                    actionName,
                    includeTransformStates: true))
            {
                return MechanicTamingMobItemId;
            }

            return 0;
        }

        public static bool IsGhostAction(string actionName)
        {
            return string.Equals(
                actionName,
                CharacterPart.GetActionString(CharacterAction.Ghost),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
