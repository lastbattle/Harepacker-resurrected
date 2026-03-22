using MapleLib.WzLib.WzStructure.Data;

namespace HaCreator.MapSimulator.Fields
{
    public static class FieldInteractionRestrictionEvaluator
    {
        public static bool CanTransferField(long fieldLimit)
        {
            return GetTransferRestrictionMessage(fieldLimit) == null;
        }

        public static bool CanJump(long fieldLimit)
        {
            return GetJumpRestrictionMessage(fieldLimit) == null;
        }

        public static string GetTeleportItemRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Use_Teleport_Item.Check(fieldLimit)
                ? "Teleport items cannot be used in this map."
                : null;
        }

        public static string GetTransferRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Migrate.Check(fieldLimit)
                ? "This field forbids map transfer."
                : null;
        }

        public static string GetMapTransferRestrictionMessage(long fieldLimit)
        {
            return GetTeleportItemRestrictionMessage(fieldLimit) ?? GetTransferRestrictionMessage(fieldLimit);
        }

        public static bool CanRegisterMapTransferDestination(int mapId)
        {
            return GetMapTransferRegistrationRestrictionMessage(mapId) == null;
        }

        public static string GetMapTransferRegistrationRestrictionMessage(int mapId)
        {
            if (mapId <= 0 || mapId == MapConstants.MaxMap)
            {
                return "This destination cannot be saved in a teleport slot.";
            }

            if (mapId < 100_000_000)
            {
                return "Only regular field maps can be saved in a teleport slot.";
            }

            int millionGroup = (mapId / 1_000_000) % 100;
            return millionGroup == 9
                ? "This destination cannot be saved in a teleport slot."
                : null;
        }

        public static string GetJumpRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Jump.Check(fieldLimit)
                ? "Jumping is disabled in this map."
                : null;
        }
    }
}
