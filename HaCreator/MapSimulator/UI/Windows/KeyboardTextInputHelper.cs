using Microsoft.Xna.Framework.Input;

namespace HaCreator.MapSimulator.UI
{
    internal static class KeyboardTextInputHelper
    {
        internal static bool IsControlKey(Keys key)
        {
            return key is Keys.LeftShift or Keys.RightShift
                or Keys.LeftControl or Keys.RightControl
                or Keys.LeftAlt or Keys.RightAlt
                or Keys.CapsLock or Keys.Tab
                or Keys.Enter or Keys.Escape;
        }

        internal static bool ShouldRepeatKey(Keys key, KeyboardState currentState, int holdStartTime, int lastRepeatTime, int tickCount)
        {
            if (key == Keys.None || !currentState.IsKeyDown(key))
            {
                return false;
            }

            const int initialDelay = 380;
            const int repeatDelay = 45;
            if (tickCount - holdStartTime < initialDelay)
            {
                return false;
            }

            return tickCount - lastRepeatTime >= repeatDelay;
        }

        internal static char? KeyToChar(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                char offset = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpperInvariant(offset) : offset;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                string shifted = ")!@#$%^&*(";
                int index = key - Keys.D0;
                return shift ? shifted[index] : (char)('0' + index);
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }

            return key switch
            {
                Keys.Space => ' ',
                Keys.OemPeriod => shift ? '>' : '.',
                Keys.OemComma => shift ? '<' : ',',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPlus => shift ? '+' : '=',
                Keys.OemQuestion => shift ? '?' : '/',
                Keys.OemSemicolon => shift ? ':' : ';',
                Keys.OemQuotes => shift ? '"' : '\'',
                Keys.OemOpenBrackets => shift ? '{' : '[',
                Keys.OemCloseBrackets => shift ? '}' : ']',
                Keys.OemPipe => shift ? '|' : '\\',
                Keys.OemTilde => shift ? '~' : '`',
                _ => null
            };
        }
    }
}
