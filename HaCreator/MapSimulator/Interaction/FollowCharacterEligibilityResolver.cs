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
                && SupportsMountedOwnershipAction(activeMountedRenderOwner, actionName))
            {
                return ResolveMountedActionOwnerItemId(
                    activeMountedRenderOwner.ItemId,
                    explicitMountedVehicleId,
                    actionName);
            }

            if (tamingMobPart?.Slot == EquipSlot.TamingMob
                && SupportsMountedOwnershipAction(tamingMobPart, actionName))
            {
                return ResolveMountedActionOwnerItemId(
                    tamingMobPart.ItemId,
                    explicitMountedVehicleId,
                    actionName);
            }

            if (explicitMountedVehicleId > 0
                && ClientOwnedVehicleSkillClassifier.IsKnownClientOwnedVehicleCurrentActionName(
                    explicitMountedVehicleId,
                    actionName))
            {
                return explicitMountedVehicleId;
            }

            if (ClientOwnedVehicleSkillClassifier.IsBattleshipMountedActionName(actionName))
            {
                return BattleshipTamingMobItemId;
            }

            if ((explicitMountedVehicleId == MechanicTamingMobItemId
                 || ClientOwnedVehicleSkillClassifier.IsExplicitMechanicVehiclePresentationSkillId(mechanicMode))
                && ClientOwnedVehicleSkillClassifier.SupportsExplicitMechanicVehiclePresentationCurrentAction(actionName))
            {
                return MechanicTamingMobItemId;
            }

            if (ClientOwnedVehicleSkillClassifier.IsOwnerlessMechanicVehicleInferenceActionName(
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
            return ClientOwnedVehicleSkillClassifier.IsClientOwnedVehicleMountOwnerItemId(mountItemId)
                ? mountItemId
                : 0;
        }

        private static int ResolveMountedActionOwnerItemId(
            int candidateMountedVehicleId,
            int explicitMountedVehicleId,
            string actionName)
        {
            if (explicitMountedVehicleId > 0
                && candidateMountedVehicleId != explicitMountedVehicleId
                && ClientOwnedVehicleSkillClassifier.IsKnownClientOwnedVehicleCurrentActionName(
                    explicitMountedVehicleId,
                    actionName))
            {
                return explicitMountedVehicleId;
            }

            return candidateMountedVehicleId;
        }

        private static bool SupportsMountedOwnershipAction(CharacterPart tamingMobPart, string actionName)
        {
            if (tamingMobPart?.Slot != EquipSlot.TamingMob || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (!ShouldRequireExactPublishedActionForOwnership(tamingMobPart.ItemId, actionName))
            {
                return CharacterAssembler.SupportsTamingMobAction(tamingMobPart, actionName);
            }

            return HasPublishedExactTamingMobAction(tamingMobPart, actionName);
        }

        private static bool ShouldRequireExactPublishedActionForOwnership(int mountItemId, string actionName)
        {
            // Keep `sit` out client-owned mounted owner inference when the mount family does not
            // publish a dedicated `sit` root in WZ. This still allows explicit `sit` ownership
            // if an exact `sit` animation exists on the mounted asset.
            return ClientOwnedVehicleSkillClassifier.IsClientOwnedVehicleMountOwnerItemId(mountItemId)
                   && string.Equals(actionName, "sit", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasPublishedExactTamingMobAction(CharacterPart tamingMobPart, string actionName)
        {
            if (tamingMobPart?.Type != CharacterPartType.TamingMob || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (tamingMobPart.AvailableAnimations != null
                && tamingMobPart.AvailableAnimations.Count > 0)
            {
                return tamingMobPart.AvailableAnimations.Contains(actionName);
            }

            if (!tamingMobPart.Animations.TryGetValue(actionName, out CharacterAnimation animation)
                || animation?.Frames?.Count <= 0)
            {
                return false;
            }

            // Exact-root ownership checks must not treat cached alias frames as authored roots.
            // CharacterPart.TryGetAnimation can cache alias results under the requested key
            // (`sit` -> `stand1`) when WZ metadata is unavailable, so only trust the cache when
            // the resolved frame owner name is absent or still matches the requested root.
            return string.IsNullOrWhiteSpace(animation.ActionName)
                   || string.Equals(animation.ActionName, actionName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
