using Microsoft.Xna.Framework.Input;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillMacroOwnerKeyHandler
    {
        internal static bool ShouldCloseWindow(KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            return keyboardState.IsKeyDown(Keys.Escape)
                && previousKeyboardState.IsKeyUp(Keys.Escape);
        }
    }
}
