using Microsoft.Xna.Framework.Input;
using HaCreator.MapSimulator.Character.Skills;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillMacroOwnerKeyHandler
    {
        internal const int ClientForwardedPrimarySlotKeyCount = 8;
        internal const int ClientForwardedFunctionKeyCount = 12;
        internal const int ClientForwardedCtrlSlotKeyCount = 8;

        internal static bool ShouldCloseWindow(KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            return keyboardState.IsKeyDown(Keys.Escape)
                && previousKeyboardState.IsKeyUp(Keys.Escape);
        }

        internal static bool TryGetClientForwardedFunctionKeyIndex(Keys key, out int functionKeyIndex)
        {
            if (key >= Keys.F1 && key <= Keys.F12)
            {
                functionKeyIndex = key - Keys.F1;
                return true;
            }

            functionKeyIndex = -1;
            return false;
        }

        internal static bool TryGetClientForwardedPrimarySlotIndex(Keys key, out int primarySlotIndex)
        {
            if (key >= Keys.D1 && key <= Keys.D8)
            {
                primarySlotIndex = key - Keys.D1;
                return true;
            }

            primarySlotIndex = -1;
            return false;
        }

        internal static bool TryGetClientForwardedCtrlSlotIndex(Keys key, out int ctrlSlotIndex)
        {
            if (TryGetClientForwardedPrimarySlotIndex(key, out ctrlSlotIndex))
            {
                return true;
            }

            ctrlSlotIndex = -1;
            return false;
        }

        internal static bool TryResolveClientForwardedNonFunctionHotkeySlot(Keys key, bool controlHeld, out int hotkeySlot)
        {
            if (controlHeld
                && TryGetClientForwardedCtrlSlotIndex(key, out int ctrlSlotIndex))
            {
                hotkeySlot = SkillManager.CTRL_SLOT_OFFSET + ctrlSlotIndex;
                return true;
            }

            if (TryGetClientForwardedPrimarySlotIndex(key, out int primarySlotIndex))
            {
                hotkeySlot = primarySlotIndex;
                return true;
            }

            hotkeySlot = -1;
            return false;
        }

        internal static bool IsClientForwardedNonFunctionHotkeyPhysicalKey(Keys key)
        {
            return TryGetClientForwardedPrimarySlotIndex(key, out _);
        }

        internal static bool IsClientForwardedModifierPhysicalKey(Keys key)
        {
            return key == Keys.LeftControl
                || key == Keys.RightControl
                || key == Keys.LeftShift
                || key == Keys.RightShift
                || key == Keys.LeftAlt
                || key == Keys.RightAlt;
        }

        internal static bool ShouldForwardClientOwnedNonFunctionKeyDownToParent(
            Keys key,
            bool controlHeld,
            bool shiftHeld,
            bool imeCompositionActive,
            bool imeCandidateWindowActive)
        {
            if (TryGetClientForwardedFunctionKeyIndex(key, out _))
            {
                return false;
            }

            if (IsClientForwardedModifierPhysicalKey(key))
            {
                return false;
            }

            return key switch
            {
                Keys.Back => false,
                Keys.Delete => false,
                Keys.C or Keys.V or Keys.X => !controlHeld,
                Keys.Home or Keys.End => controlHeld,
                Keys.Down => true,
                Keys.Insert => !shiftHeld,
                Keys.Enter or Keys.Left or Keys.Right or Keys.Up => true,
                _ => true
            };
        }

        internal static bool ShouldForwardClientOwnedNonFunctionKeyUpToParent(Keys key)
        {
            return !TryGetClientForwardedFunctionKeyIndex(key, out _)
                && !IsClientForwardedModifierPhysicalKey(key);
        }

        internal static bool ShouldApplyCaretBoundaryNavigation(bool controlHeld)
        {
            // `CCtrlEdit::OnKey` forwards Ctrl+Home/Ctrl+End to the parent owner path
            // instead of moving the local edit caret.
            return !controlHeld;
        }
    }
}
