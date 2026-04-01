using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class WeddingWishListWindow : UIWindowBase
    {
        public WeddingWishListWindow()
            : base(null)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.WeddingWishList;

        internal void SetSnapshotProvider(Func<WeddingWishListSnapshot> snapshotProvider)
        {
        }

        internal void SetActionHandlers(
            Func<WeddingWishListSelectionPane, string> focusPaneHandler,
            Func<int, string> setTabHandler,
            Func<WeddingWishListSelectionPane, int, string> selectEntryHandler,
            Func<string> getSelectedHandler,
            Func<string> putSelectedHandler,
            Func<string> enterSelectedHandler,
            Func<string> deleteSelectedHandler,
            Func<string> confirmHandler,
            Func<string> closeHandler,
            Action<string> feedbackHandler)
        {
        }

        public override void SetFont(SpriteFont font)
        {
        }
    }
}
