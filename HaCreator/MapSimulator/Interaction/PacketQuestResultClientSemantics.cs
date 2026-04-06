namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketQuestResultClientSemantics
    {
        internal const int FirstHandledSubtype = 6;
        internal const int LastHandledSubtype = 18;

        internal static bool IsHandledSubtype(int resultType)
        {
            return resultType >= FirstHandledSubtype && resultType <= LastHandledSubtype;
        }
    }
}
