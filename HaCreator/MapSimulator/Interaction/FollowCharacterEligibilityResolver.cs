using System;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class FollowCharacterEligibilityResolver
    {
        public static bool IsMountedState(CharacterPart tamingMobPart, string actionName)
        {
            if (ClientOwnedVehicleSkillClassifier.IsBattleshipVehicleOwnedCurrentActionName(actionName, includeSupportActions: true)
                || ClientOwnedVehicleSkillClassifier.IsMechanicVehicleOwnedCurrentActionName(actionName, includeTransformStates: true))
            {
                return true;
            }

            if (tamingMobPart?.Slot != EquipSlot.TamingMob || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return string.Equals(actionName, "ride2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "getoff2", StringComparison.OrdinalIgnoreCase)
                   || CharacterAssembler.SupportsTamingMobAction(tamingMobPart, actionName);
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
