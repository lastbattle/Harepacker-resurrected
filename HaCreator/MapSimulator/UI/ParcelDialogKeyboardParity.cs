using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework.Input;

namespace HaCreator.MapSimulator.UI
{
    internal enum ParcelDialogKeyboardAction
    {
        None,
        CloseDialog,
        CancelArmedItemPicker,
        ClaimReceiveAttachment,
        DispatchSend
    }

    internal static class ParcelDialogKeyboardParity
    {
        internal static ParcelDialogKeyboardAction ResolveAction(
            global::HaCreator.MapSimulator.Interaction.ParcelDialogTab activeTab,
            bool hasFocusedComposeField,
            bool hasArmedItemPicker,
            Keys key)
        {
            if (hasFocusedComposeField)
            {
                return ParcelDialogKeyboardAction.None;
            }

            if (key == Keys.Escape)
            {
                if (activeTab == global::HaCreator.MapSimulator.Interaction.ParcelDialogTab.Send
                    && hasArmedItemPicker)
                {
                    return ParcelDialogKeyboardAction.CancelArmedItemPicker;
                }

                return ParcelDialogKeyboardAction.CloseDialog;
            }

            if (key != Keys.Enter)
            {
                return ParcelDialogKeyboardAction.None;
            }

            return activeTab switch
            {
                global::HaCreator.MapSimulator.Interaction.ParcelDialogTab.Receive => ParcelDialogKeyboardAction.ClaimReceiveAttachment,
                global::HaCreator.MapSimulator.Interaction.ParcelDialogTab.Send => ParcelDialogKeyboardAction.DispatchSend,
                _ => ParcelDialogKeyboardAction.None
            };
        }
    }
}
