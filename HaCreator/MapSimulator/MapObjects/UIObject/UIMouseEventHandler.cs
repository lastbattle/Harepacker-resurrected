using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Runtime.CompilerServices;


namespace HaCreator.MapSimulator.MapObjects.UIObject {

    public class UIMouseEventHandler {

        /// <summary>
        /// Checks for mouse event to the UI buttons
        /// </summary>
        /// <param name="shiftCenteredX"></param>
        /// <param name="shiftCenteredY"></param>
        /// <param name="thisPositionX"></param>
        /// <param name="thisPositionY"></param>
        /// <param name="mouseState"></param>
        /// <param name="uiButtons"></param>
        /// <param name="bIsUIMovableByMouse">If the entire UI is movable by mouse holding</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckMouseEvent(
            int shiftCenteredX, int shiftCenteredY, 
            int thisPositionX, int thisPositionY, MouseState mouseState, 
            List<MapObjects.UIObject.UIObject> uiButtons, bool bIsUIMovableByMouse) {

            foreach (UIObject uiBtn in uiButtons) {
                bool bHandled = uiBtn.CheckMouseEvent(shiftCenteredX, shiftCenteredY, thisPositionX, thisPositionY, mouseState);
                if (bHandled) {
                    return true;
                }
            }
            return false;
        }
    }
}
