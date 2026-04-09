using Microsoft.Xna.Framework.Input;
using System;


namespace HaCreator.MapSimulator.UI
{
    internal static class KeyboardTextInputHelper
    {
        internal const int ClientRepeatInitialDelayMs = 360;
        internal const int ClientRepeatDelayMs = 42;
        private const int FallbackInitialDelayMs = 380;
        private const int FallbackRepeatDelayMs = 45;

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

            int initialDelay = GetInitialRepeatDelayMilliseconds();
            int repeatDelay = GetRepeatDelayMilliseconds();
            if (tickCount - holdStartTime < initialDelay)
            {
                return false;
            }

            return tickCount - lastRepeatTime >= repeatDelay;
        }

        internal static bool ShouldRepeatKeyUsingFixedCadence(
            Keys key,
            KeyboardState currentState,
            int holdStartTime,
            int lastRepeatTime,
            int tickCount,
            int initialDelayMs = ClientRepeatInitialDelayMs,
            int repeatDelayMs = ClientRepeatDelayMs)
        {
            if (key == Keys.None || !currentState.IsKeyDown(key))
            {
                return false;
            }

            if (tickCount - holdStartTime < Math.Max(0, initialDelayMs))
            {
                return false;
            }

            return tickCount - lastRepeatTime >= Math.Max(1, repeatDelayMs);
        }

        private static int GetInitialRepeatDelayMilliseconds()
        {
            try
            {
                return (Math.Clamp(System.Windows.Forms.SystemInformation.KeyboardDelay, 0, 3) + 1) * 250;
            }
            catch
            {
                return FallbackInitialDelayMs;
            }
        }

        private static int GetRepeatDelayMilliseconds()
        {
            try
            {
                int keyboardSpeed = Math.Clamp(System.Windows.Forms.SystemInformation.KeyboardSpeed, 0, 31);
                double charactersPerSecond = 2.5 + ((keyboardSpeed / 31d) * 27.5);
                if (charactersPerSecond <= 0d)
                {
                    return FallbackRepeatDelayMs;
                }

                return Math.Max(1, (int)Math.Round(1000d / charactersPerSecond));
            }
            catch
            {
                return FallbackRepeatDelayMs;
            }
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
