using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketScriptDedicatedOwnerRowHeight = 30;
        private const float PacketScriptDedicatedOwnerPromptScale = 0.42f;
        private const float PacketScriptDedicatedOwnerDetailScale = 0.36f;
        private const float PacketScriptDedicatedOwnerChoiceScale = 0.42f;

        private PacketScriptOwnerLayer[] _packetScriptAvatarOwnerLayers;
        private PacketScriptButtonVisuals _packetScriptAvatarPrevButtonVisuals;
        private PacketScriptButtonVisuals _packetScriptAvatarNextButtonVisuals;
        private PacketScriptButtonVisuals _packetScriptAvatarOkButtonVisuals;
        private PacketScriptButtonVisuals _packetScriptAvatarCancelButtonVisuals;
        private PacketScriptOwnerLayer _packetScriptAvatarNameTagLayer;
        private PacketScriptOwnerLayer _packetScriptAvatarShadowLayer;
        private PacketScriptOwnerLayer[] _packetScriptPetOwnerLayers;
        private PacketScriptOwnerLayer[] _packetScriptMultiPetOwnerLayers;
        private PacketScriptButtonVisuals _packetScriptPetPrevButtonVisuals;
        private PacketScriptButtonVisuals _packetScriptPetNextButtonVisuals;
        private PacketScriptButtonVisuals _packetScriptPetOkButtonVisuals;
        private PacketScriptButtonVisuals _packetScriptPetCancelButtonVisuals;
        private PacketScriptAnimationStrip _packetScriptSlideMenuType0Background;
        private PacketScriptAnimationStrip _packetScriptSlideMenuType1Background;
        private PacketScriptButtonVisuals _packetScriptSlideMenuLeftButtonVisuals;
        private PacketScriptButtonVisuals _packetScriptSlideMenuRightButtonVisuals;
        private PacketScriptButtonVisuals _packetScriptSlideMenuMoveButtonVisuals;
        private PacketScriptButtonVisuals _packetScriptSlideMenuCancelButtonVisuals;
        private PacketScriptOwnerLayer _packetScriptSlideMenuType0Cover;
        private PacketScriptOwnerLayer _packetScriptSlideMenuType1Cover;
        private PacketScriptOwnerLayer _packetScriptSlideMenuType0Choice;
        private PacketScriptOwnerLayer _packetScriptSlideMenuType1Choice;
        private PacketScriptOwnerLayer _packetScriptSlideMenuType0Recommend;
        private PacketScriptDedicatedOwnerButtonKind _packetScriptDedicatedOwnerHoveredButton;
        private PacketScriptDedicatedOwnerButtonKind _packetScriptDedicatedOwnerPressedButton;

        private sealed record PacketScriptOwnerLayer(Texture2D Texture, Point Origin);
        private sealed record PacketScriptDedicatedOwnerLayout(
            Rectangle PrevBounds,
            Rectangle NextBounds,
            Rectangle ConfirmBounds,
            Rectangle CancelBounds,
            Rectangle SelectionBounds);

        private enum PacketScriptDedicatedOwnerButtonKind
        {
            None,
            Prev,
            Next,
            Confirm,
            Cancel
        }

        private void ClearPacketScriptDedicatedOwnerVisualState()
        {
            _packetScriptDedicatedOwnerHoveredButton = PacketScriptDedicatedOwnerButtonKind.None;
            _packetScriptDedicatedOwnerPressedButton = PacketScriptDedicatedOwnerButtonKind.None;
        }

        private bool HandlePacketScriptDedicatedOwnerMouse(MouseState mouseState, MouseState previousMouseState, int currentTickCount)
        {
            if (!_packetScriptDedicatedOwnerRuntime.TryBuildSnapshot(out PacketScriptDedicatedOwnerSnapshot snapshot))
            {
                ClearPacketScriptDedicatedOwnerVisualState();
                return false;
            }

            Rectangle ownerBounds = ResolvePacketScriptDedicatedOwnerBounds(snapshot, currentTickCount);
            PacketScriptDedicatedOwnerLayout layout = ResolvePacketScriptDedicatedOwnerLayout(
                snapshot,
                ownerBounds,
                currentTickCount);

            Point cursor = mouseState.Position;
            _packetScriptDedicatedOwnerHoveredButton = ResolvePacketScriptDedicatedOwnerHoveredButton(
                cursor,
                layout.PrevBounds,
                layout.NextBounds,
                layout.ConfirmBounds,
                layout.CancelBounds);
            if (!IsPacketScriptDedicatedOwnerButtonEnabled(snapshot, _packetScriptDedicatedOwnerHoveredButton))
            {
                _packetScriptDedicatedOwnerHoveredButton = PacketScriptDedicatedOwnerButtonKind.None;
            }

            if (layout.SelectionBounds.Contains(cursor) &&
                snapshot.Choices.Count > 0 &&
                mouseState.LeftButton == ButtonState.Released &&
                previousMouseState.LeftButton == ButtonState.Pressed)
            {
                SubmitPacketScriptDedicatedOwnerSelection(confirm: true, showFeedback: true);
                return true;
            }

            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool justPressed = leftPressed && previousMouseState.LeftButton == ButtonState.Released;
            bool justReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;
            if (justPressed)
            {
                _packetScriptDedicatedOwnerPressedButton = _packetScriptDedicatedOwnerHoveredButton;
            }
            else if (!leftPressed && !justReleased)
            {
                _packetScriptDedicatedOwnerPressedButton = PacketScriptDedicatedOwnerButtonKind.None;
            }

            if (!justReleased)
            {
                return ownerBounds.Contains(cursor);
            }

            PacketScriptDedicatedOwnerButtonKind confirmedButton =
                _packetScriptDedicatedOwnerPressedButton == _packetScriptDedicatedOwnerHoveredButton
                    ? _packetScriptDedicatedOwnerHoveredButton
                    : PacketScriptDedicatedOwnerButtonKind.None;
            _packetScriptDedicatedOwnerPressedButton = PacketScriptDedicatedOwnerButtonKind.None;

            switch (confirmedButton)
            {
                case PacketScriptDedicatedOwnerButtonKind.Prev:
                    if (IsPacketScriptDedicatedOwnerButtonEnabled(snapshot, confirmedButton))
                    {
                        _packetScriptDedicatedOwnerRuntime.MoveSelection(-1);
                    }

                    return true;
                case PacketScriptDedicatedOwnerButtonKind.Next:
                    if (IsPacketScriptDedicatedOwnerButtonEnabled(snapshot, confirmedButton))
                    {
                        _packetScriptDedicatedOwnerRuntime.MoveSelection(1);
                    }

                    return true;
                case PacketScriptDedicatedOwnerButtonKind.Confirm:
                    if (IsPacketScriptDedicatedOwnerButtonEnabled(snapshot, confirmedButton))
                    {
                        SubmitPacketScriptDedicatedOwnerSelection(confirm: true, showFeedback: true);
                    }

                    return true;
                case PacketScriptDedicatedOwnerButtonKind.Cancel:
                    SubmitPacketScriptDedicatedOwnerSelection(confirm: false, showFeedback: true);
                    return true;
                default:
                    return ownerBounds.Contains(cursor);
            }
        }

        private bool HandlePacketScriptDedicatedOwnerKeyboard(KeyboardState newKeyboardState, KeyboardState oldKeyboardState, int currentTickCount)
        {
            if (!_packetScriptDedicatedOwnerRuntime.TryBuildSnapshot(out PacketScriptDedicatedOwnerSnapshot snapshot))
            {
                return false;
            }

            if (newKeyboardState.IsKeyDown(Keys.Escape) && oldKeyboardState.IsKeyUp(Keys.Escape))
            {
                SubmitPacketScriptDedicatedOwnerSelection(confirm: false, showFeedback: true);
                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Enter) && oldKeyboardState.IsKeyUp(Keys.Enter))
            {
                if (IsPacketScriptDedicatedOwnerButtonEnabled(snapshot, PacketScriptDedicatedOwnerButtonKind.Confirm))
                {
                    SubmitPacketScriptDedicatedOwnerSelection(confirm: true, showFeedback: true);
                }

                return true;
            }

            if ((newKeyboardState.IsKeyDown(Keys.Left) && oldKeyboardState.IsKeyUp(Keys.Left)) ||
                (newKeyboardState.IsKeyDown(Keys.Up) && oldKeyboardState.IsKeyUp(Keys.Up)))
            {
                if (IsPacketScriptDedicatedOwnerButtonEnabled(snapshot, PacketScriptDedicatedOwnerButtonKind.Prev))
                {
                    _packetScriptDedicatedOwnerRuntime.MoveSelection(-1);
                }

                return true;
            }

            if ((newKeyboardState.IsKeyDown(Keys.Right) && oldKeyboardState.IsKeyUp(Keys.Right)) ||
                (newKeyboardState.IsKeyDown(Keys.Down) && oldKeyboardState.IsKeyUp(Keys.Down)))
            {
                if (IsPacketScriptDedicatedOwnerButtonEnabled(snapshot, PacketScriptDedicatedOwnerButtonKind.Next))
                {
                    _packetScriptDedicatedOwnerRuntime.MoveSelection(1);
                }

                return true;
            }

            return snapshot.Choices.Count > 0;
        }

        private void SubmitPacketScriptDedicatedOwnerSelection(bool confirm, bool showFeedback)
        {
            if (!_packetScriptDedicatedOwnerRuntime.TryBuildSnapshot(out PacketScriptDedicatedOwnerSnapshot snapshot))
            {
                return;
            }

            NpcInteractionChoice selectedChoice = null;
            if (confirm)
            {
                _packetScriptDedicatedOwnerRuntime.TryGetSelectedChoice(out selectedChoice);
            }

            NpcInteractionInputSubmission submission = new()
            {
                EntryId = 1,
                EntryTitle = snapshot.Title,
                NpcName = _activeNpcInteractionNpc?.NpcInstance?.NpcInfo?.StringName ?? "Script",
                PresentationStyle = NpcInteractionPresentationStyle.PacketScriptUtilDialog,
                Kind = confirm ? selectedChoice?.SubmissionKind ?? NpcInteractionInputKind.None : NpcInteractionInputKind.None,
                Value = confirm ? selectedChoice?.SubmissionValue ?? string.Empty : string.Empty,
                NumericValue = confirm ? selectedChoice?.SubmissionNumericValue : null
            };

            if (_packetScriptMessageRuntime.TryBuildResponsePacket(
                submission,
                out PacketScriptMessageRuntime.PacketScriptResponsePacket responsePacket,
                out string message))
            {
                bool dispatched = TryDispatchPacketScriptResponse(responsePacket, out string dispatchStatus);
                _packetScriptMessageRuntime.RecordResponseDispatch(responsePacket, dispatched, dispatchStatus);
                if (showFeedback)
                {
                    ShowUtilityFeedbackMessage($"{message} {dispatchStatus}".Trim());
                }
            }
            else if (showFeedback && !string.IsNullOrWhiteSpace(message))
            {
                ShowUtilityFeedbackMessage(message);
            }

            _packetScriptDedicatedOwnerRuntime.Clear();
            ClearPacketScriptDedicatedOwnerVisualState();
            _activeNpcInteractionNpc = null;
            _activeNpcInteractionNpcId = 0;
        }

        private void DrawCenteredPacketScriptDedicatedOwner(int currentTickCount)
        {
            if (_fontChat == null ||
                GraphicsDevice == null ||
                !_packetScriptDedicatedOwnerRuntime.TryBuildSnapshot(out PacketScriptDedicatedOwnerSnapshot snapshot))
            {
                return;
            }

            EnsurePacketScriptOwnerVisualsLoaded();
            EnsurePacketScriptDedicatedOwnerVisualsLoaded();

            Rectangle ownerBounds = ResolvePacketScriptDedicatedOwnerBounds(snapshot, currentTickCount);
            PacketScriptDedicatedOwnerLayout layout = ResolvePacketScriptDedicatedOwnerLayout(
                snapshot,
                ownerBounds,
                currentTickCount);
            Rectangle promptBounds = new(ownerBounds.X + 20, ownerBounds.Y + 18, ownerBounds.Width - 40, Math.Max(40, layout.SelectionBounds.Y - ownerBounds.Y - 24));
            Rectangle detailBounds = new(ownerBounds.X + 20, layout.SelectionBounds.Bottom + 12, ownerBounds.Width - 40, Math.Max(42, layout.CancelBounds.Y - layout.SelectionBounds.Bottom - 20));

            DrawPacketScriptOwnerFrame(
                new Rectangle(0, 0, _renderParams.RenderWidth, _renderParams.RenderHeight),
                new Color(0, 0, 0, 168),
                Color.Transparent);

            switch (snapshot.Kind)
            {
                case PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.AvatarSelection:
                case PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.MembershopAvatarSelection:
                    DrawPacketScriptAvatarSelectionOwner(ownerBounds, layout.SelectionBounds);
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptAvatarPrevButtonVisuals, layout.PrevBounds, PacketScriptDedicatedOwnerButtonKind.Prev, "Prev");
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptAvatarNextButtonVisuals, layout.NextBounds, PacketScriptDedicatedOwnerButtonKind.Next, "Next");
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptAvatarOkButtonVisuals, layout.ConfirmBounds, PacketScriptDedicatedOwnerButtonKind.Confirm, "OK");
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptAvatarCancelButtonVisuals, layout.CancelBounds, PacketScriptDedicatedOwnerButtonKind.Cancel, "Cancel");
                    break;

                case PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.SlideMenu:
                    DrawPacketScriptSlideMenuOwner(snapshot, ownerBounds, layout.SelectionBounds, currentTickCount);
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptSlideMenuLeftButtonVisuals, layout.PrevBounds, PacketScriptDedicatedOwnerButtonKind.Prev, "Prev");
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptSlideMenuRightButtonVisuals, layout.NextBounds, PacketScriptDedicatedOwnerButtonKind.Next, "Next");
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptSlideMenuMoveButtonVisuals, layout.ConfirmBounds, PacketScriptDedicatedOwnerButtonKind.Confirm, "Move");
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptSlideMenuCancelButtonVisuals, layout.CancelBounds, PacketScriptDedicatedOwnerButtonKind.Cancel, "Cancel");
                    break;

                case PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.MultiPetSelection:
                case PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.PetSelection:
                    DrawPacketScriptPetSelectionOwner(snapshot, ownerBounds, layout.SelectionBounds);
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptPetPrevButtonVisuals, layout.PrevBounds, PacketScriptDedicatedOwnerButtonKind.Prev, "Prev");
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptPetNextButtonVisuals, layout.NextBounds, PacketScriptDedicatedOwnerButtonKind.Next, "Next");
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptPetOkButtonVisuals, layout.ConfirmBounds, PacketScriptDedicatedOwnerButtonKind.Confirm, "OK");
                    DrawPacketScriptDedicatedOwnerButton(snapshot, ownerBounds, _packetScriptPetCancelButtonVisuals, layout.CancelBounds, PacketScriptDedicatedOwnerButtonKind.Cancel, "Cancel");
                    break;
            }

            DrawPacketScriptOwnerWrappedText(snapshot.PromptText, promptBounds, new Color(76, 46, 24), PacketScriptDedicatedOwnerPromptScale, maxLines: 3);
            DrawPacketScriptOwnerWrappedText(ResolvePacketScriptDedicatedOwnerSelectionText(snapshot), layout.SelectionBounds, Color.Black, PacketScriptDedicatedOwnerChoiceScale, maxLines: 1);
            if (!string.IsNullOrWhiteSpace(snapshot.DetailText))
            {
                DrawPacketScriptOwnerWrappedText(snapshot.DetailText, detailBounds, new Color(92, 64, 38), PacketScriptDedicatedOwnerDetailScale, maxLines: 5);
            }
        }

        private void DrawPacketScriptAvatarSelectionOwner(Rectangle ownerBounds, Rectangle selectionBounds)
        {
            DrawPacketScriptOwnerLayers(ownerBounds, _packetScriptAvatarOwnerLayers);
            if (_packetScriptAvatarShadowLayer?.Texture != null)
            {
                Vector2 position = new(
                    selectionBounds.Center.X - (_packetScriptAvatarShadowLayer.Texture.Width * 0.5f) - _packetScriptAvatarShadowLayer.Origin.X,
                    selectionBounds.Y - 42 - _packetScriptAvatarShadowLayer.Origin.Y);
                _spriteBatch.Draw(_packetScriptAvatarShadowLayer.Texture, position, Color.White);
            }

            if (_packetScriptAvatarNameTagLayer?.Texture != null)
            {
                Vector2 position = new(
                    selectionBounds.Center.X - (_packetScriptAvatarNameTagLayer.Texture.Width * 0.5f) - _packetScriptAvatarNameTagLayer.Origin.X,
                    selectionBounds.Y - _packetScriptAvatarNameTagLayer.Origin.Y);
                _spriteBatch.Draw(_packetScriptAvatarNameTagLayer.Texture, position, Color.White);
            }
            else
            {
                DrawPacketScriptOwnerFrame(selectionBounds, new Color(255, 255, 255, 214), new Color(126, 92, 54));
            }
        }

        private void DrawPacketScriptPetSelectionOwner(PacketScriptDedicatedOwnerSnapshot snapshot, Rectangle ownerBounds, Rectangle selectionBounds)
        {
            PacketScriptOwnerLayer[] layers =
                snapshot.Kind == PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.MultiPetSelection
                    ? _packetScriptMultiPetOwnerLayers
                    : _packetScriptPetOwnerLayers;
            DrawPacketScriptOwnerLayers(ownerBounds, layers);
            DrawPacketScriptOwnerFrame(selectionBounds, new Color(255, 255, 255, 214), new Color(126, 92, 54));
        }

        private void DrawPacketScriptSlideMenuOwner(PacketScriptDedicatedOwnerSnapshot snapshot, Rectangle ownerBounds, Rectangle selectionBounds, int currentTickCount)
        {
            PacketScriptAnimationStrip background = snapshot.Mode == 0
                ? _packetScriptSlideMenuType0Background
                : _packetScriptSlideMenuType1Background;
            Texture2D frame = ResolvePacketScriptAnimationFrame(background, currentTickCount);
            if (frame != null)
            {
                _spriteBatch.Draw(frame, ownerBounds, Color.White);
            }
            else
            {
                DrawPacketScriptOwnerFrame(ownerBounds, new Color(39, 28, 22, 224), new Color(193, 141, 88));
            }

            PacketScriptOwnerLayer cover = snapshot.Mode == 0 ? _packetScriptSlideMenuType0Cover : _packetScriptSlideMenuType1Cover;
            if (cover?.Texture != null)
            {
                Vector2 position = new(selectionBounds.Center.X - (cover.Texture.Width * 0.5f) - cover.Origin.X, selectionBounds.Center.Y - (cover.Texture.Height * 0.5f) - cover.Origin.Y);
                _spriteBatch.Draw(cover.Texture, position, Color.White);
            }
            else
            {
                DrawPacketScriptOwnerFrame(selectionBounds, new Color(255, 245, 225, 210), new Color(146, 104, 62));
            }

            PacketScriptOwnerLayer choiceFrame = snapshot.Mode == 0 ? _packetScriptSlideMenuType0Choice : _packetScriptSlideMenuType1Choice;
            if (choiceFrame?.Texture != null)
            {
                Vector2 position = new(selectionBounds.X + 6 - choiceFrame.Origin.X, selectionBounds.Center.Y - (choiceFrame.Texture.Height * 0.5f) - choiceFrame.Origin.Y);
                _spriteBatch.Draw(choiceFrame.Texture, position, Color.White);
            }

            if (snapshot.Mode == 0 && _packetScriptSlideMenuType0Recommend?.Texture != null)
            {
                Vector2 position = new(ownerBounds.Right - _packetScriptSlideMenuType0Recommend.Texture.Width - 16 - _packetScriptSlideMenuType0Recommend.Origin.X, ownerBounds.Y + 18 - _packetScriptSlideMenuType0Recommend.Origin.Y);
                _spriteBatch.Draw(_packetScriptSlideMenuType0Recommend.Texture, position, Color.White);
            }
        }

        private void DrawPacketScriptDedicatedOwnerButton(
            PacketScriptDedicatedOwnerSnapshot snapshot,
            Rectangle ownerBounds,
            PacketScriptButtonVisuals visuals,
            Rectangle bounds,
            PacketScriptDedicatedOwnerButtonKind buttonKind,
            string fallbackLabel)
        {
            if (bounds == Rectangle.Empty)
            {
                return;
            }

            bool enabled = IsPacketScriptDedicatedOwnerButtonEnabled(snapshot, buttonKind);
            bool hovered = enabled && _packetScriptDedicatedOwnerHoveredButton == buttonKind;
            bool pressed = enabled && hovered && _packetScriptDedicatedOwnerPressedButton == buttonKind;
            PacketScriptOwnerButtonVisualState state = PacketScriptOwnerVisualStateResolver.ResolveButtonState(
                enabled,
                hovered,
                pressed);
            PacketScriptButtonFrame frame = visuals?.ResolveFrame(state);
            if (frame?.Texture != null)
            {
                Rectangle drawBounds = ResolvePacketScriptOwnerAnchoredBounds(
                    ownerBounds,
                    frame.Origin,
                    new Point(frame.Texture.Width, frame.Texture.Height));
                _spriteBatch.Draw(frame.Texture, drawBounds, Color.White);
                return;
            }

            Color fill = state switch
            {
                PacketScriptOwnerButtonVisualState.Pressed => new Color(132, 82, 47, 220),
                PacketScriptOwnerButtonVisualState.Hover => new Color(176, 121, 68, 208),
                PacketScriptOwnerButtonVisualState.Disabled => new Color(84, 65, 49, 180),
                _ => new Color(148, 98, 56, 196)
            };
            DrawPacketScriptOwnerFrame(bounds, fill, new Color(224, 189, 124, 220));
            DrawPacketScriptOwnerWrappedText(fallbackLabel, bounds, new Color(255, 241, 205), 0.36f, maxLines: 1);
        }

        private static bool IsPacketScriptDedicatedOwnerButtonEnabled(
            PacketScriptDedicatedOwnerSnapshot snapshot,
            PacketScriptDedicatedOwnerButtonKind buttonKind)
        {
            int choiceCount = snapshot?.Choices?.Count ?? 0;
            return buttonKind switch
            {
                PacketScriptDedicatedOwnerButtonKind.Prev or PacketScriptDedicatedOwnerButtonKind.Next => choiceCount > 1,
                PacketScriptDedicatedOwnerButtonKind.Confirm => choiceCount > 0 && snapshot.SelectedChoiceIndex >= 0,
                PacketScriptDedicatedOwnerButtonKind.Cancel => snapshot != null,
                _ => false
            };
        }

        private Rectangle ResolvePacketScriptDedicatedOwnerBounds(PacketScriptDedicatedOwnerSnapshot snapshot, int currentTickCount)
        {
            Point size = snapshot.Kind switch
            {
                PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.SlideMenu => ResolvePacketScriptSlideMenuOwnerSize(snapshot.Mode, currentTickCount),
                PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.MultiPetSelection => ResolvePacketScriptLayeredOwnerSize(_packetScriptMultiPetOwnerLayers, new Point(415, 215)),
                PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.AvatarSelection or PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.MembershopAvatarSelection => ResolvePacketScriptLayeredOwnerSize(_packetScriptAvatarOwnerLayers, new Point(367, 259)),
                _ => ResolvePacketScriptLayeredOwnerSize(_packetScriptPetOwnerLayers, new Point(367, 259))
            };
            int x = Math.Max(0, (_renderParams.RenderWidth - size.X) / 2);
            int y = Math.Max(ResolvePacketScriptOwnerPreviewTop(), (_renderParams.RenderHeight - size.Y) / 2);
            return new Rectangle(x, y, size.X, size.Y);
        }

        private Point ResolvePacketScriptSlideMenuOwnerSize(int mode, int currentTickCount)
        {
            Texture2D frame = ResolvePacketScriptAnimationFrame(
                mode == 0 ? _packetScriptSlideMenuType0Background : _packetScriptSlideMenuType1Background,
                currentTickCount);
            return frame != null ? new Point(frame.Width, frame.Height) : new Point(284, 217);
        }

        internal static Rectangle ResolvePacketScriptOwnerAnchoredBounds(Rectangle ownerBounds, Point origin, Point size)
        {
            return new Rectangle(
                ownerBounds.X - origin.X,
                ownerBounds.Y - origin.Y,
                Math.Max(1, size.X),
                Math.Max(1, size.Y));
        }

        private static Point ResolvePacketScriptLayeredOwnerSize(PacketScriptOwnerLayer[] layers, Point fallback)
        {
            if (layers == null || layers.Length == 0 || layers.All(static layer => layer?.Texture == null))
            {
                return fallback;
            }

            int minX = 0;
            int minY = 0;
            int maxX = 0;
            int maxY = 0;
            bool found = false;
            foreach (PacketScriptOwnerLayer layer in layers)
            {
                if (layer?.Texture == null)
                {
                    continue;
                }

                found = true;
                minX = Math.Min(minX, layer.Origin.X);
                minY = Math.Min(minY, layer.Origin.Y);
                maxX = Math.Max(maxX, layer.Origin.X + layer.Texture.Width);
                maxY = Math.Max(maxY, layer.Origin.Y + layer.Texture.Height);
            }

            return found ? new Point(Math.Max(1, maxX - minX), Math.Max(1, maxY - minY)) : fallback;
        }

        private void DrawPacketScriptOwnerLayers(Rectangle ownerBounds, PacketScriptOwnerLayer[] layers)
        {
            if (layers == null || layers.Length == 0)
            {
                DrawPacketScriptOwnerFrame(ownerBounds, new Color(39, 28, 22, 224), new Color(193, 141, 88));
                return;
            }

            int minX = layers.Where(static layer => layer?.Texture != null).DefaultIfEmpty().Min(static layer => layer?.Origin.X ?? 0);
            int minY = layers.Where(static layer => layer?.Texture != null).DefaultIfEmpty().Min(static layer => layer?.Origin.Y ?? 0);
            bool drewAny = false;
            foreach (PacketScriptOwnerLayer layer in layers)
            {
                if (layer?.Texture == null)
                {
                    continue;
                }

                Vector2 position = new(ownerBounds.X + (layer.Origin.X - minX), ownerBounds.Y + (layer.Origin.Y - minY));
                _spriteBatch.Draw(layer.Texture, position, Color.White);
                drewAny = true;
            }

            if (!drewAny)
            {
                DrawPacketScriptOwnerFrame(ownerBounds, new Color(39, 28, 22, 224), new Color(193, 141, 88));
            }
        }

        private static PacketScriptDedicatedOwnerButtonKind ResolvePacketScriptDedicatedOwnerHoveredButton(
            Point cursor,
            Rectangle prevBounds,
            Rectangle nextBounds,
            Rectangle confirmBounds,
            Rectangle cancelBounds)
        {
            if (prevBounds.Contains(cursor))
            {
                return PacketScriptDedicatedOwnerButtonKind.Prev;
            }

            if (nextBounds.Contains(cursor))
            {
                return PacketScriptDedicatedOwnerButtonKind.Next;
            }

            if (confirmBounds.Contains(cursor))
            {
                return PacketScriptDedicatedOwnerButtonKind.Confirm;
            }

            return cancelBounds.Contains(cursor)
                ? PacketScriptDedicatedOwnerButtonKind.Cancel
                : PacketScriptDedicatedOwnerButtonKind.None;
        }

        private static string ResolvePacketScriptDedicatedOwnerSelectionText(PacketScriptDedicatedOwnerSnapshot snapshot)
        {
            if (snapshot.Choices == null || snapshot.Choices.Count == 0)
            {
                return "No selectable entries";
            }

            int index = snapshot.SelectedChoiceIndex >= 0 && snapshot.SelectedChoiceIndex < snapshot.Choices.Count
                ? snapshot.SelectedChoiceIndex
                : 0;
            string label = snapshot.Choices[index]?.Label ?? "(null)";
            return $"{index + 1}/{snapshot.Choices.Count}  {label}";
        }

        private PacketScriptDedicatedOwnerLayout ResolvePacketScriptDedicatedOwnerLayout(
            PacketScriptDedicatedOwnerSnapshot snapshot,
            Rectangle ownerBounds,
            int currentTickCount)
        {
            int selectionWidth = Math.Max(144, ownerBounds.Width - 140);
            int selectionX = ownerBounds.X + Math.Max(34, (ownerBounds.Width - selectionWidth) / 2);
            int selectionY = ownerBounds.Y + Math.Max(78, ownerBounds.Height / 2 - (PacketScriptDedicatedOwnerRowHeight / 2));
            Rectangle selectionBounds = new Rectangle(selectionX, selectionY, selectionWidth, PacketScriptDedicatedOwnerRowHeight);

            int navWidth = 46;
            int navHeight = 20;
            Rectangle prevBounds = new Rectangle(selectionBounds.X - navWidth - 10, selectionBounds.Center.Y - (navHeight / 2), navWidth, navHeight);
            Rectangle nextBounds = new Rectangle(selectionBounds.Right + 10, selectionBounds.Center.Y - (navHeight / 2), navWidth, navHeight);

            int confirmWidth = snapshot.Kind == PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.SlideMenu ? 60 : 40;
            int confirmHeight = 16;
            int cancelWidth = 40;
            int buttonY = ownerBounds.Bottom - 34;
            Rectangle confirmBounds = new Rectangle(ownerBounds.X + ownerBounds.Width / 2 - confirmWidth - 8, buttonY, confirmWidth, confirmHeight);
            Rectangle cancelBounds = new Rectangle(ownerBounds.X + ownerBounds.Width / 2 + 8, buttonY, cancelWidth, confirmHeight);

            if (snapshot.Kind is PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.PetSelection or PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.MultiPetSelection)
            {
                prevBounds = TryResolveAnchoredButtonBounds(ownerBounds, _packetScriptPetPrevButtonVisuals, prevBounds);
                nextBounds = TryResolveAnchoredButtonBounds(ownerBounds, _packetScriptPetNextButtonVisuals, nextBounds);
                confirmBounds = TryResolveAnchoredButtonBounds(ownerBounds, _packetScriptPetOkButtonVisuals, confirmBounds);
                cancelBounds = TryResolveAnchoredButtonBounds(ownerBounds, _packetScriptPetCancelButtonVisuals, cancelBounds);

                int petSelectionLeft = Math.Max(ownerBounds.X + 28, prevBounds.Right + 10);
                int petSelectionRight = Math.Min(ownerBounds.Right - 28, nextBounds.X - 10);
                int petSelectionWidth = Math.Max(120, petSelectionRight - petSelectionLeft);
                int petSelectionY = Math.Max(ownerBounds.Y + 132, prevBounds.Y - 2);
                selectionBounds = new Rectangle(petSelectionLeft, petSelectionY, petSelectionWidth, PacketScriptDedicatedOwnerRowHeight);
            }
            else if (snapshot.Kind is PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.AvatarSelection or PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.MembershopAvatarSelection)
            {
                prevBounds = TryResolveAnchoredButtonBounds(ownerBounds, _packetScriptAvatarPrevButtonVisuals, prevBounds);
                nextBounds = TryResolveAnchoredButtonBounds(ownerBounds, _packetScriptAvatarNextButtonVisuals, nextBounds);
                confirmBounds = TryResolveAnchoredButtonBounds(ownerBounds, _packetScriptAvatarOkButtonVisuals, confirmBounds);
                cancelBounds = TryResolveAnchoredButtonBounds(ownerBounds, _packetScriptAvatarCancelButtonVisuals, cancelBounds);

                int avatarSelectionLeft = Math.Max(ownerBounds.X + 64, prevBounds.Right + 10);
                int avatarSelectionRight = Math.Min(ownerBounds.Right - 64, nextBounds.X - 10);
                int avatarSelectionWidth = Math.Max(150, avatarSelectionRight - avatarSelectionLeft);
                int avatarSelectionY = Math.Max(ownerBounds.Y + 164, prevBounds.Y - 2);
                selectionBounds = new Rectangle(avatarSelectionLeft, avatarSelectionY, avatarSelectionWidth, PacketScriptDedicatedOwnerRowHeight);
            }
            else if (snapshot.Kind == PacketScriptMessageRuntime.PacketScriptDedicatedOwnerKind.SlideMenu)
            {
                PacketScriptButtonVisuals leftVisuals = _packetScriptSlideMenuLeftButtonVisuals;
                PacketScriptButtonVisuals rightVisuals = _packetScriptSlideMenuRightButtonVisuals;
                PacketScriptButtonVisuals moveVisuals = _packetScriptSlideMenuMoveButtonVisuals;
                PacketScriptButtonVisuals cancelVisuals = _packetScriptSlideMenuCancelButtonVisuals;
                bool allowZeroOriginAnchors = snapshot.Mode == 0;
                prevBounds = TryResolveAnchoredButtonBounds(ownerBounds, leftVisuals, prevBounds, allowZeroOriginAnchors);
                nextBounds = TryResolveAnchoredButtonBounds(ownerBounds, rightVisuals, nextBounds, allowZeroOriginAnchors);
                confirmBounds = TryResolveAnchoredButtonBounds(ownerBounds, moveVisuals, confirmBounds, allowZeroOriginAnchors);
                cancelBounds = TryResolveAnchoredButtonBounds(ownerBounds, cancelVisuals, cancelBounds, allowZeroOriginAnchors);

                PacketScriptOwnerLayer cover = snapshot.Mode == 0 ? _packetScriptSlideMenuType0Cover : _packetScriptSlideMenuType1Cover;
                if (cover?.Texture != null &&
                    TryResolvePacketScriptOwnerAnchoredBounds(
                        ownerBounds,
                        cover.Origin,
                        new Point(cover.Texture.Width, cover.Texture.Height),
                        allowZeroOriginAnchors,
                        out Rectangle coverBounds))
                {
                    selectionBounds = coverBounds;
                }
                else
                {
                    Texture2D background = ResolvePacketScriptAnimationFrame(
                        snapshot.Mode == 0 ? _packetScriptSlideMenuType0Background : _packetScriptSlideMenuType1Background,
                        currentTickCount);
                    int slideSelectionWidth = background != null ? Math.Max(120, background.Width - 120) : selectionBounds.Width;
                    selectionBounds = new Rectangle(
                        ownerBounds.X + Math.Max(28, (ownerBounds.Width - slideSelectionWidth) / 2),
                        ownerBounds.Y + (snapshot.Mode == 0 ? 54 : 42),
                        slideSelectionWidth,
                        PacketScriptDedicatedOwnerRowHeight);
                }
            }

            return new PacketScriptDedicatedOwnerLayout(prevBounds, nextBounds, confirmBounds, cancelBounds, selectionBounds);
        }

        internal static bool TryResolvePacketScriptOwnerAnchoredBounds(
            Rectangle ownerBounds,
            Point origin,
            Point size,
            bool allowZeroOrigin,
            out Rectangle bounds)
        {
            if (!CanUsePacketScriptOwnerAnchor(origin, size, allowZeroOrigin))
            {
                bounds = Rectangle.Empty;
                return false;
            }

            bounds = ResolvePacketScriptOwnerAnchoredBounds(ownerBounds, origin, size);
            return true;
        }

        internal static bool CanUsePacketScriptOwnerAnchor(Point origin, Point size, bool allowZeroOrigin)
        {
            return size.X > 0
                && size.Y > 0
                && (allowZeroOrigin || origin != Point.Zero);
        }

        private static Rectangle TryResolveAnchoredButtonBounds(
            Rectangle ownerBounds,
            PacketScriptButtonVisuals visuals,
            Rectangle fallback,
            bool allowZeroOrigin = true)
        {
            if (visuals != null &&
                visuals.TryGetAnchorMetrics(out Point origin, out Point size) &&
                TryResolvePacketScriptOwnerAnchoredBounds(ownerBounds, origin, size, allowZeroOrigin, out Rectangle anchoredBounds))
            {
                return anchoredBounds;
            }

            return fallback;
        }

        private void EnsurePacketScriptDedicatedOwnerVisualsLoaded()
        {
            if (_packetScriptPetOwnerLayers != null)
            {
                return;
            }

            WzImage uiWindowImage = Program.FindImage("UI", "UIWindow.img");
            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img") ?? uiWindowImage;

            WzSubProperty avatarPreferred = uiWindow2Image?["UtilDlgEx_Avatar"] as WzSubProperty;
            WzSubProperty avatarFallback = uiWindowImage?["UtilDlgEx_Avatar"] as WzSubProperty;
            _packetScriptAvatarOwnerLayers = new[]
            {
                LoadPacketScriptOwnerLayer((avatarPreferred?["backgrnd"] ?? avatarFallback?["backgrnd"]) as WzCanvasProperty),
                LoadPacketScriptOwnerLayer((avatarPreferred?["backgrnd2"] ?? avatarFallback?["backgrnd2"]) as WzCanvasProperty),
                LoadPacketScriptOwnerLayer((avatarPreferred?["backgrnd3"] ?? avatarFallback?["backgrnd3"]) as WzCanvasProperty)
            };
            _packetScriptAvatarPrevButtonVisuals = LoadPacketScriptButtonVisuals(avatarPreferred?["BtPrev"] as WzSubProperty, avatarFallback?["BtPrev"] as WzSubProperty);
            _packetScriptAvatarNextButtonVisuals = LoadPacketScriptButtonVisuals(avatarPreferred?["BtNext"] as WzSubProperty, avatarFallback?["BtNext"] as WzSubProperty);
            _packetScriptAvatarOkButtonVisuals = LoadPacketScriptButtonVisuals(
                avatarPreferred?["BtOK"] as WzSubProperty ?? avatarPreferred?["BtOn"] as WzSubProperty,
                avatarFallback?["BtOK"] as WzSubProperty ?? avatarFallback?["BtOn"] as WzSubProperty);
            _packetScriptAvatarCancelButtonVisuals = LoadPacketScriptButtonVisuals(
                avatarPreferred?["BtCancle"] as WzSubProperty ?? avatarPreferred?["BtOff"] as WzSubProperty ?? avatarPreferred?["BtExit"] as WzSubProperty,
                avatarFallback?["BtCancle"] as WzSubProperty ?? avatarFallback?["BtOff"] as WzSubProperty ?? avatarFallback?["BtExit"] as WzSubProperty);
            _packetScriptAvatarNameTagLayer = LoadPacketScriptOwnerLayer((avatarPreferred?["nameTag"] ?? avatarFallback?["nameTag"]) as WzCanvasProperty);
            _packetScriptAvatarShadowLayer = LoadPacketScriptOwnerLayer((avatarPreferred?["shadow"] ?? avatarFallback?["shadow"]) as WzCanvasProperty);

            WzSubProperty petPreferred = uiWindow2Image?["UtilDlgEx_Pet"] as WzSubProperty;
            WzSubProperty petFallback = uiWindowImage?["UtilDlgEx_Pet"] as WzSubProperty;
            _packetScriptPetOwnerLayers = new[]
            {
                LoadPacketScriptOwnerLayer((petPreferred?["backgrnd"] ?? petFallback?["backgrnd"]) as WzCanvasProperty),
                LoadPacketScriptOwnerLayer((petPreferred?["backgrnd2"] ?? petFallback?["backgrnd2"]) as WzCanvasProperty),
                LoadPacketScriptOwnerLayer((petPreferred?["backgrnd3"] ?? petFallback?["backgrnd3"]) as WzCanvasProperty)
            };
            _packetScriptPetPrevButtonVisuals = LoadPacketScriptButtonVisuals(petPreferred?["BtPrev"] as WzSubProperty, petFallback?["BtPrev"] as WzSubProperty);
            _packetScriptPetNextButtonVisuals = LoadPacketScriptButtonVisuals(petPreferred?["BtNext"] as WzSubProperty, petFallback?["BtNext"] as WzSubProperty);
            _packetScriptPetOkButtonVisuals = LoadPacketScriptButtonVisuals(petPreferred?["BtOK"] as WzSubProperty, petFallback?["BtOK"] as WzSubProperty);
            _packetScriptPetCancelButtonVisuals = LoadPacketScriptButtonVisuals(petPreferred?["BtCancle"] as WzSubProperty, petFallback?["BtCancle"] as WzSubProperty);

            WzSubProperty multiPetProperty = uiWindowImage?["UtilDlgEx_MultiPetEquip"] as WzSubProperty;
            _packetScriptMultiPetOwnerLayers = new[]
            {
                LoadPacketScriptOwnerLayer(multiPetProperty?["backgrnd"] as WzCanvasProperty)
            };

            WzSubProperty slidePreferred = uiWindow2Image?["SlideMenu"] as WzSubProperty;
            WzSubProperty slideFallback = uiWindowImage?["SlideMenu"] as WzSubProperty;
            WzSubProperty slideType0Preferred = slidePreferred?["0"] as WzSubProperty;
            WzSubProperty slideType0Fallback = slideFallback?["0"] as WzSubProperty;
            WzSubProperty slideType1Preferred = slidePreferred?["1"] as WzSubProperty;
            WzSubProperty slideType1Fallback = slideFallback?["1"] as WzSubProperty;

            _packetScriptSlideMenuType0Background = LoadPacketScriptAnimationStrip(slideType0Preferred?["backgrd"] as WzSubProperty, slideType0Fallback?["backgrd"] as WzSubProperty);
            _packetScriptSlideMenuType1Background = LoadPacketScriptAnimationStrip(slideType1Preferred?["backgrd"] as WzSubProperty, slideType1Fallback?["backgrd"] as WzSubProperty);
            _packetScriptSlideMenuLeftButtonVisuals = LoadPacketScriptButtonVisuals(
                slideType0Preferred?["BtArrow"]?["left"] as WzSubProperty ?? slideType1Preferred?["BtArrow"]?["left"] as WzSubProperty,
                slideType0Fallback?["BtArrow"]?["left"] as WzSubProperty ?? slideType1Fallback?["BtArrow"]?["left"] as WzSubProperty);
            _packetScriptSlideMenuRightButtonVisuals = LoadPacketScriptButtonVisuals(
                slideType0Preferred?["BtArrow"]?["right"] as WzSubProperty ?? slideType1Preferred?["BtArrow"]?["right"] as WzSubProperty,
                slideType0Fallback?["BtArrow"]?["right"] as WzSubProperty ?? slideType1Fallback?["BtArrow"]?["right"] as WzSubProperty);
            _packetScriptSlideMenuMoveButtonVisuals = LoadPacketScriptButtonVisuals(
                slideType0Preferred?["BtMove"] as WzSubProperty ?? slideType1Preferred?["BtMove"] as WzSubProperty,
                slideType0Fallback?["BtMove"] as WzSubProperty ?? slideType1Fallback?["BtMove"] as WzSubProperty);
            _packetScriptSlideMenuCancelButtonVisuals = LoadPacketScriptButtonVisuals(
                slideType0Preferred?["BtCancle"] as WzSubProperty ?? slideType1Preferred?["BtCancle"] as WzSubProperty,
                slideType0Fallback?["BtCancle"] as WzSubProperty ?? slideType1Fallback?["BtCancle"] as WzSubProperty);
            _packetScriptSlideMenuType0Cover = LoadPacketScriptOwnerLayer((slideType0Preferred?["Cover"] ?? slideType0Fallback?["Cover"]) as WzCanvasProperty);
            _packetScriptSlideMenuType1Cover = LoadPacketScriptOwnerLayer((slideType1Preferred?["Cover"] ?? slideType1Fallback?["Cover"]) as WzCanvasProperty);
            _packetScriptSlideMenuType0Choice = LoadPacketScriptOwnerLayer((slideType0Preferred?["Choice"] ?? slideType0Fallback?["Choice"]) as WzCanvasProperty);
            _packetScriptSlideMenuType1Choice = LoadPacketScriptOwnerLayer((slideType1Preferred?["Choice"] ?? slideType1Fallback?["Choice"]) as WzCanvasProperty);
            _packetScriptSlideMenuType0Recommend = LoadPacketScriptOwnerLayer((slideType0Preferred?["Recommend"] ?? slideType0Fallback?["Recommend"]) as WzCanvasProperty);
        }

        private PacketScriptOwnerLayer LoadPacketScriptOwnerLayer(WzCanvasProperty canvas)
        {
            Texture2D texture = LoadUiCanvasTexture(canvas);
            return texture == null ? null : new PacketScriptOwnerLayer(texture, ResolveCanvasOrigin(canvas));
        }
    }
}
