using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class WeddingWishListController
    {
        private readonly WeddingWishListRuntime _runtime = new();

        internal Action<string, int> SocialChatObserved
        {
            get => _runtime.SocialChatObserved;
            set => _runtime.SocialChatObserved = value;
        }

        internal Func<int, IReadOnlyList<byte>, string, string> ClientPacketDispatcher
        {
            get => _runtime.ClientPacketDispatcher;
            set => _runtime.ClientPacketDispatcher = value;
        }

        internal void UpdateLocalContext(CharacterBuild build)
        {
            _runtime.UpdateLocalContext(build);
        }

        internal string DescribeStatus()
        {
            return _runtime.DescribeStatus();
        }

        internal void WireWindow(
            UIWindowManager windowManager,
            CharacterBuild build,
            IInventoryRuntime inventory,
            SpriteFont font,
            Action<string> feedbackHandler)
        {
            if (windowManager?.GetWindow(MapSimulatorWindowNames.WeddingWishList) is not WeddingWishListWindow window)
            {
                return;
            }

            _runtime.UpdateLocalContext(build);
            _runtime.BindInventory(inventory);
            window.SetSnapshotProvider(() => _runtime.BuildSnapshot());
            window.SetActionHandlers(
                pane => FocusPane(pane),
                tabIndex => SetTab(tabIndex),
                (pane, index) => SelectEntry(pane, index),
                (pane, delta) => ScrollPane(pane, delta),
                value => AppendCandidateQuery(value),
                () => BackspaceCandidateQuery(),
                () => GetSelected(windowManager),
                () => PutSelected(windowManager),
                () => EnterSelected(windowManager),
                () => DeleteSelected(windowManager),
                () => Confirm(windowManager),
                () => Close(windowManager),
                value => AppendPutQuantityDigit(windowManager, value),
                () => BackspacePutQuantityDigit(windowManager),
                () => CancelTransientPrompt(windowManager),
                feedbackHandler);
            window.SetFont(font);
        }

        internal string Open(
            WeddingWishListDialogMode mode,
            WeddingWishListRole? role,
            UIWindowManager windowManager,
            CharacterBuild build,
            IInventoryRuntime inventory,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow)
        {
            _runtime.UpdateLocalContext(build);
            _runtime.BindInventory(inventory);
            string message = _runtime.Open(mode, role);
            ShowWindow(windowManager, build, inventory, font, feedbackHandler, showWindow);
            return message;
        }

        internal string SetMode(
            WeddingWishListDialogMode mode,
            UIWindowManager windowManager,
            CharacterBuild build,
            IInventoryRuntime inventory,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow)
        {
            _runtime.UpdateLocalContext(build);
            _runtime.BindInventory(inventory);
            string message = _runtime.SetMode(mode);
            ShowWindow(windowManager, build, inventory, font, feedbackHandler, showWindow);
            return message;
        }

        internal string SetTab(int tabIndex) => _runtime.SetTab(tabIndex);

        internal string FocusPane(WeddingWishListSelectionPane pane) => _runtime.SetActivePane(pane);

        internal string SelectEntry(WeddingWishListSelectionPane pane, int index) => _runtime.SelectEntry(pane, index);

        internal string MoveSelection(int delta) => _runtime.MoveSelection(delta);

        internal string ScrollPane(WeddingWishListSelectionPane pane, int delta) => _runtime.ScrollPane(pane, delta);

        internal string AppendCandidateQuery(char value) => _runtime.AppendCandidateQuery(value);

        internal string BackspaceCandidateQuery() => _runtime.BackspaceCandidateQuery();

        internal string AppendPutQuantityDigit(UIWindowManager windowManager, char value)
        {
            string message = _runtime.AppendPutQuantityDigit(value);
            KeepWindowVisible(windowManager);
            return message;
        }

        internal string BackspacePutQuantityDigit(UIWindowManager windowManager)
        {
            string message = _runtime.BackspacePutQuantityDigit();
            KeepWindowVisible(windowManager);
            return message;
        }

        internal string CancelTransientPrompt(UIWindowManager windowManager)
        {
            string message = _runtime.CancelTransientPrompt();
            KeepWindowVisible(windowManager);
            return message;
        }

        internal string PutSelected(UIWindowManager windowManager)
        {
            string message = _runtime.TryPutSelectedItem();
            KeepWindowVisible(windowManager);
            return message;
        }

        internal string GetSelected(UIWindowManager windowManager)
        {
            string message = _runtime.TryGetSelectedItem();
            KeepWindowVisible(windowManager);
            return message;
        }

        internal string EnterSelected(UIWindowManager windowManager)
        {
            string message = _runtime.TryAddCandidateWish();
            KeepWindowVisible(windowManager);
            return message;
        }

        internal string DeleteSelected(UIWindowManager windowManager)
        {
            string message = _runtime.TryDeleteWish();
            KeepWindowVisible(windowManager);
            return message;
        }

        internal string Confirm(UIWindowManager windowManager)
        {
            string message = _runtime.ConfirmInput();
            if (_runtime.BuildSnapshot().IsOpen)
            {
                KeepWindowVisible(windowManager);
            }
            else
            {
                windowManager?.HideWindow(MapSimulatorWindowNames.WeddingWishList);
            }

            return message;
        }

        internal string Close(UIWindowManager windowManager)
        {
            string message = _runtime.Close();
            windowManager?.HideWindow(MapSimulatorWindowNames.WeddingWishList);
            return message;
        }

        internal string Clear(UIWindowManager windowManager)
        {
            string message = _runtime.Clear();
            windowManager?.HideWindow(MapSimulatorWindowNames.WeddingWishList);
            return message;
        }

        private void ShowWindow(
            UIWindowManager windowManager,
            CharacterBuild build,
            IInventoryRuntime inventory,
            SpriteFont font,
            Action<string> feedbackHandler,
            Action showWindow)
        {
            WireWindow(windowManager, build, inventory, font, feedbackHandler);
            showWindow?.Invoke();
        }

        private static void KeepWindowVisible(UIWindowManager windowManager)
        {
            if (windowManager?.GetWindow(MapSimulatorWindowNames.WeddingWishList) is WeddingWishListWindow window)
            {
                windowManager.BringToFront(window);
            }
        }
    }
}
