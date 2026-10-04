namespace HaCreator.MapSimulator.Managers
{
    public static class ItemMakerHiddenRecipeRevealPolicy
    {
        public static bool ShouldRevealLocally(bool isHidden, bool passesPersistentGate, bool passesTransientGate, bool hasExplicitUnlockGate)
        {
            if (!isHidden)
            {
                return false;
            }

            if (!passesPersistentGate)
            {
                return false;
            }

            return !hasExplicitUnlockGate || passesTransientGate;
        }
    }
}
