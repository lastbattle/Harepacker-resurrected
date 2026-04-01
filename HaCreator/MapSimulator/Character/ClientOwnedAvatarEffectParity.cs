using System;

namespace HaCreator.MapSimulator.Character
{
    public static class ClientOwnedAvatarEffectParity
    {
        public static bool ShouldHideDuringPlayerAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("doublejump", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("backspin", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("spin", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(actionName, "screw", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "somersault", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "finalCut", StringComparison.OrdinalIgnoreCase);
        }
    }
}
