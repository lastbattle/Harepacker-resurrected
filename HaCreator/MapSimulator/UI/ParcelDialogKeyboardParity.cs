using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework.Input;

namespace HaCreator.MapSimulator.UI
{
    internal enum ParcelDialogKeyboardAction
    {
        None,
        CloseDialog,
        DispatchSend
    }

    internal static class ParcelDialogKeyboardParity
    {
        internal static ParcelDialogKeyboardAction ResolveAction(ParcelDialogTab activeTab, bool hasFocusedComposeField, Keys key)
        {
            if (key == Keys.Escape)
            {
                return ParcelDialogKeyboardAction.CloseDialog;
            }

            if (key != Keys.Enter || hasFocusedComposeField)
            {
                return ParcelDialogKeyboardAction.None;
            }

            return activeTab == ParcelDialogTab.Send
                ? ParcelDialogKeyboardAction.DispatchSend
                : ParcelDialogKeyboardAction.None;
        }
    }
}
