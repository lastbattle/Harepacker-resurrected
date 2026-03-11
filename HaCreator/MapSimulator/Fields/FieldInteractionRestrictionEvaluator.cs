using MapleLib.WzLib.WzStructure.Data;

namespace HaCreator.MapSimulator.Fields
{
    public static class FieldInteractionRestrictionEvaluator
    {
        public static bool CanTransferField(long fieldLimit)
        {
            return GetTransferRestrictionMessage(fieldLimit) == null;
        }

        public static string GetTransferRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Migrate.Check(fieldLimit)
                ? "This field forbids map transfer."
                : null;
        }
    }
}
