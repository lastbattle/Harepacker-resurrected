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
            int explicitMountedVehicleId = ResolveExplicitMountedVehicleId(
                tamingMobPart,
                mechanicMode,
                activeMountedRenderOwner);
            if (explicitMountedVehicleId > 0
                && string.IsNullOrWhiteSpace(actionName))
            {
                return explicitMountedVehicleId;
            }

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

            if (explicitMountedVehicleId == MechanicTamingMobItemId
                || ClientOwnedVehicleSkillClassifier.IsExplicitMechanicVehiclePresentationSkillId(mechanicMode)
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

        private static int ResolveExplicitMountedVehicleId(
            CharacterPart tamingMobPart,
            int? mechanicMode,
            CharacterPart activeMountedRenderOwner)
        {
            int activeMountedOwnerItemId = NormalizeMountedVehicleOwnerItemId(activeMountedRenderOwner?.ItemId ?? 0);
            if (activeMountedOwnerItemId > 0)
            {
                return activeMountedOwnerItemId;
            }

            int equippedMountedOwnerItemId = NormalizeMountedVehicleOwnerItemId(tamingMobPart?.ItemId ?? 0);
            if (equippedMountedOwnerItemId > 0)
            {
                return equippedMountedOwnerItemId;
            }

            return ClientOwnedVehicleSkillClassifier.IsExplicitMechanicVehiclePresentationSkillId(mechanicMode)
                ? MechanicTamingMobItemId
                : 0;
        }

        private static int NormalizeMountedVehicleOwnerItemId(int mountItemId)
        {
            return mountItemId == BattleshipTamingMobItemId || mountItemId == MechanicTamingMobItemId
                ? mountItemId
                : 0;
        }
    }
}
