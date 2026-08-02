using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int SpeedQuizOwnerWidth = 265;
        private const int SpeedQuizOwnerHeight = 422;
        private const int SpeedQuizOwnerFrameFadeAlpha = 176;
        private const int SpeedQuizOwnerInputMaxLength = 50;

        private readonly StringBuilder _speedQuizOwnerInput = new(SpeedQuizOwnerInputMaxLength);
        private int _speedQuizOwnerCursorIndex;
        private int _speedQuizOwnerCursorBlinkStartedAt;
        private bool _speedQuizOwnerResultSent;
        private SpeedQuizOwnerButtonKind _speedQuizOwnerHoveredButton;
        private SpeedQuizOwnerButtonKind _speedQuizOwnerPressedButton;

        internal enum SpeedQuizOwnerButtonKind
        {
            None,
            GiveUp,
            Next,
            Ok
        }

        private void ResetSpeedQuizOwnerInputState(int currentTickCount)
        {
            _speedQuizOwnerInput.Clear();
            _speedQuizOwnerCursorIndex = 0;
            _speedQuizOwnerCursorBlinkStartedAt = currentTickCount;
            _speedQuizOwnerResultSent = false;
            _speedQuizOwnerHoveredButton = SpeedQuizOwnerButtonKind.None;
            _speedQuizOwnerPressedButton = SpeedQuizOwnerButtonKind.None;
        }

        private void ClearSpeedQuizOwnerInputState()
        {
            _speedQuizOwnerInput.Clear();
            _speedQuizOwnerCursorIndex = 0;
            _speedQuizOwnerResultSent = false;
            _speedQuizOwnerHoveredButton = SpeedQuizOwnerButtonKind.None;
            _speedQuizOwnerPressedButton = SpeedQuizOwnerButtonKind.None;
        }

        private void UpdateSpeedQuizOwner(int currentTickCount)
        {
            if (!_speedQuizOwnerRuntime.TryBuildOwnerSnapshot(currentTickCount, out SpeedQuizOwnerSnapshot snapshot))
            {
                ClearSpeedQuizOwnerInputState();
                return;
            }

            if (snapshot.RemainingMs > 0 || _speedQuizOwnerResultSent)
            {
                return;
            }

            SubmitSpeedQuizOwnerResult(string.Empty, currentTickCount, showFeedback: false);
        }

        private bool HandleSpeedQuizOwnerMouse(MouseState mouseState, MouseState previousMouseState, int currentTickCount)
        {
            if (!_speedQuizOwnerRuntime.TryBuildOwnerSnapshot(currentTickCount, out SpeedQuizOwnerSnapshot snapshot))
            {
                return false;
            }

            Rectangle ownerBounds = ResolveSpeedQuizOwnerBounds();
            Rectangle okButtonBounds = ResolveSpeedQuizOwnerOkButtonBounds(ownerBounds);
            Rectangle nextButtonBounds = ResolveSpeedQuizOwnerNextButtonBounds(ownerBounds);
            Rectangle giveUpButtonBounds = ResolveSpeedQuizOwnerGiveUpButtonBounds(ownerBounds);
            Point cursor = new(mouseState.X, mouseState.Y);

            _speedQuizOwnerHoveredButton = ResolveSpeedQuizOwnerHoveredButton(cursor, okButtonBounds, nextButtonBounds, giveUpButtonBounds);

            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool justPressed = leftPressed && previousMouseState.LeftButton == ButtonState.Released;
            bool justReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

            if (justPressed)
            {
                _speedQuizOwnerPressedButton = _speedQuizOwnerHoveredButton;
                _speedQuizOwnerCursorBlinkStartedAt = currentTickCount;
            }
            else if (!leftPressed && !justReleased)
            {
                _speedQuizOwnerPressedButton = SpeedQuizOwnerButtonKind.None;
            }

            if (justReleased)
            {
                SpeedQuizOwnerButtonKind confirmedButton = ResolveSpeedQuizOwnerReleaseConfirmation(
                    _speedQuizOwnerPressedButton,
                    _speedQuizOwnerHoveredButton);
                _speedQuizOwnerPressedButton = SpeedQuizOwnerButtonKind.None;

                switch (confirmedButton)
                {
                    case SpeedQuizOwnerButtonKind.Ok:
                    case SpeedQuizOwnerButtonKind.Next:
                        SubmitSpeedQuizOwnerResult(_speedQuizOwnerInput.ToString(), currentTickCount, showFeedback: true);
                        break;
                    case SpeedQuizOwnerButtonKind.GiveUp:
                        SubmitSpeedQuizOwnerResult(string.Empty, currentTickCount, showFeedback: true);
                        break;
                }
            }

            return snapshot.RemainingMs > 0 || _speedQuizOwnerResultSent;
        }

        internal static SpeedQuizOwnerButtonKind ResolveSpeedQuizOwnerReleaseConfirmation(
            SpeedQuizOwnerButtonKind pressedButton,
            SpeedQuizOwnerButtonKind hoveredButton)
        {
            return pressedButton != SpeedQuizOwnerButtonKind.None && pressedButton == hoveredButton
                ? pressedButton
                : SpeedQuizOwnerButtonKind.None;
        }

        private bool HandleSpeedQuizOwnerKeyboard(KeyboardState newKeyboardState, KeyboardState oldKeyboardState, int currentTickCount)
        {
            if (!_speedQuizOwnerRuntime.TryBuildOwnerSnapshot(currentTickCount, out SpeedQuizOwnerSnapshot snapshot))
            {
                return false;
            }

            if (snapshot.RemainingMs <= 0)
            {
                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Enter) && oldKeyboardState.IsKeyUp(Keys.Enter))
            {
                SubmitSpeedQuizOwnerResult(_speedQuizOwnerInput.ToString(), currentTickCount, showFeedback: true);
                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Back) && oldKeyboardState.IsKeyUp(Keys.Back))
            {
                if (_speedQuizOwnerCursorIndex > 0)
                {
                    _speedQuizOwnerInput.Remove(_speedQuizOwnerCursorIndex - 1, 1);
                    _speedQuizOwnerCursorIndex--;
                    _speedQuizOwnerCursorBlinkStartedAt = currentTickCount;
                }

                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Delete) && oldKeyboardState.IsKeyUp(Keys.Delete))
            {
                if (_speedQuizOwnerCursorIndex < _speedQuizOwnerInput.Length)
                {
                    _speedQuizOwnerInput.Remove(_speedQuizOwnerCursorIndex, 1);
                    _speedQuizOwnerCursorBlinkStartedAt = currentTickCount;
                }

                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Left) && oldKeyboardState.IsKeyUp(Keys.Left))
            {
                if (_speedQuizOwnerCursorIndex > 0)
                {
                    _speedQuizOwnerCursorIndex--;
                    _speedQuizOwnerCursorBlinkStartedAt = currentTickCount;
                }

                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Right) && oldKeyboardState.IsKeyUp(Keys.Right))
            {
                if (_speedQuizOwnerCursorIndex < _speedQuizOwnerInput.Length)
                {
                    _speedQuizOwnerCursorIndex++;
                    _speedQuizOwnerCursorBlinkStartedAt = currentTickCount;
                }

                return true;
            }

            bool shiftPressed = newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in newKeyboardState.GetPressedKeys())
            {
                if (oldKeyboardState.IsKeyDown(key))
                {
                    continue;
                }

                char? typed = TryMapInitialQuizOwnerChar(key, shiftPressed);
                if (!typed.HasValue)
                {
                    continue;
                }

                if (_speedQuizOwnerInput.Length >= SpeedQuizOwnerInputMaxLength)
                {
                    break;
                }

                _speedQuizOwnerInput.Insert(_speedQuizOwnerCursorIndex, typed.Value);
                _speedQuizOwnerCursorIndex++;
                _speedQuizOwnerCursorBlinkStartedAt = currentTickCount;
            }

            return true;
        }

        private void SubmitSpeedQuizOwnerResult(string answerText, int currentTickCount, bool showFeedback)
        {
            if (_speedQuizOwnerResultSent)
            {
                return;
            }

            _speedQuizOwnerResultSent = true;
            string submittedValue = answerText ?? string.Empty;
            NpcInteractionInputSubmission submission = new()
            {
                EntryId = 1,
                EntryTitle = "Speed Quiz",
                NpcName = "Speed Quiz",
                PresentationStyle = NpcInteractionPresentationStyle.PacketScriptUtilDialog,
                Kind = NpcInteractionInputKind.Text,
                Value = submittedValue
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

            _speedQuizOwnerRuntime.Clear();
            ClearSpeedQuizOwnerInputState();
        }

        private void DrawCenteredPacketOwnedSpeedQuizOwner(int currentTickCount, SpeedQuizOwnerSnapshot snapshot)
        {
            if (_fontChat == null || GraphicsDevice == null || snapshot == null)
            {
                return;
            }

            EnsurePacketScriptOwnerVisualsLoaded();

            Rectangle ownerBounds = ResolveSpeedQuizOwnerBounds();
            Rectangle inputBounds = ResolveSpeedQuizOwnerInputBounds(ownerBounds);
            Rectangle giveUpButtonBounds = ResolveSpeedQuizOwnerGiveUpButtonBounds(ownerBounds);
            Rectangle nextButtonBounds = ResolveSpeedQuizOwnerNextButtonBounds(ownerBounds);
            Rectangle okButtonBounds = ResolveSpeedQuizOwnerOkButtonBounds(ownerBounds);
            int panelWidth = ownerBounds.Width;
            int panelHeight = ownerBounds.Height;

            DrawPacketScriptOwnerFrame(
                new Rectangle(0, 0, _renderParams.RenderWidth, _renderParams.RenderHeight),
                new Color(0, 0, 0, SpeedQuizOwnerFrameFadeAlpha),
                Color.Transparent);

            DrawSpeedQuizOwnerLayer(_packetScriptSpeedQuizBackTexture, ownerBounds.X, ownerBounds.Y);
            DrawSpeedQuizOwnerLayer(_packetScriptSpeedQuizBackTexture2, ownerBounds.X, ownerBounds.Y);
            DrawSpeedQuizOwnerLayer(_packetScriptSpeedQuizBackTexture3, ownerBounds.X, ownerBounds.Y);

            Rectangle headerBounds = PacketScriptQuizOwnerLayout.AnchorRect(ownerBounds, 18, 20, panelWidth - 36, 18, panelWidth, panelHeight);
            DrawPacketScriptOwnerWrappedText("Speed Quiz", headerBounds, new Color(78, 39, 18), 0.54f, maxLines: 1);

            DrawPacketScriptOwnerMetric(ownerBounds, 58, "Question", $"{snapshot.CurrentQuestion}/{Math.Max(snapshot.TotalQuestions, 1)}");
            DrawPacketScriptOwnerMetric(ownerBounds, 106, "Correct", snapshot.CorrectAnswers.ToString());
            DrawPacketScriptOwnerMetric(ownerBounds, 154, "Remain", snapshot.RemainingQuestions.ToString());

            Rectangle timerLabelBounds = PacketScriptQuizOwnerLayout.AnchorRect(ownerBounds, 22, 202, 86, 18, panelWidth, panelHeight);
            Rectangle timerDigitsBounds = PacketScriptQuizOwnerLayout.AnchorRect(ownerBounds, 126, 196, 100, 28, panelWidth, panelHeight);
            DrawPacketScriptOwnerWrappedText("Timer", timerLabelBounds, new Color(96, 50, 20), 0.43f, maxLines: 1);
            DrawPacketScriptTime(timerDigitsBounds, Math.Max(0, snapshot.RemainingSeconds), _packetScriptSpeedQuizDigits, Color.White);

            Rectangle summaryBounds = PacketScriptQuizOwnerLayout.AnchorRect(ownerBounds, 24, 246, panelWidth - 48, 54, panelWidth, panelHeight);
            DrawPacketScriptOwnerWrappedText(
                $"Question {snapshot.CurrentQuestion} of {Math.Max(snapshot.TotalQuestions, 1)}\n" +
                $"Correct answers: {snapshot.CorrectAnswers}\n" +
                $"Questions remaining: {snapshot.RemainingQuestions}",
                summaryBounds,
                new Color(88, 52, 24),
                0.42f,
                maxLines: 4);

            DrawSpeedQuizOwnerInputField(inputBounds, snapshot.RemainingMs > 0, currentTickCount);
            DrawSpeedQuizOwnerButton(
                _packetScriptSpeedQuizGiveUpButtonVisuals,
                giveUpButtonBounds,
                SpeedQuizOwnerButtonKind.GiveUp,
                "Give Up",
                snapshot.RemainingMs > 0);
            DrawSpeedQuizOwnerButton(
                _packetScriptSpeedQuizNextButtonVisuals,
                nextButtonBounds,
                SpeedQuizOwnerButtonKind.Next,
                "Next",
                snapshot.RemainingMs > 0);
            DrawSpeedQuizOwnerButton(
                _packetScriptSpeedQuizOkButtonVisuals,
                okButtonBounds,
                SpeedQuizOwnerButtonKind.Ok,
                "OK",
                snapshot.RemainingMs > 0);
        }

        private void DrawPacketScriptOwnerMetric(Rectangle ownerBounds, int sourceY, string label, string value)
        {
            Rectangle labelBounds = PacketScriptQuizOwnerLayout.AnchorRect(ownerBounds, 22, sourceY, 84, 18, ownerBounds.Width, ownerBounds.Height);
            Rectangle valueBounds = PacketScriptQuizOwnerLayout.AnchorRect(ownerBounds, 118, sourceY - 2, 112, 22, ownerBounds.Width, ownerBounds.Height);
            DrawPacketScriptOwnerWrappedText(label, labelBounds, new Color(96, 50, 20), 0.43f, maxLines: 1);
            DrawPacketScriptNumber(valueBounds, value, _packetScriptSpeedQuizDigits, Color.White, centerHorizontally: false);
        }

        private void DrawSpeedQuizOwnerInputField(Rectangle inputBounds, bool enabled, int currentTickCount)
        {
            Color fillColor = enabled ? new Color(255, 255, 255, 212) : new Color(170, 170, 170, 180);
            Color borderColor = enabled ? new Color(113, 78, 48) : new Color(84, 84, 84);
            DrawPacketScriptOwnerFrame(inputBounds, fillColor, borderColor);

            Rectangle labelBounds = new(inputBounds.X - 70, inputBounds.Y - 1, 62, inputBounds.Height + 2);
            DrawPacketScriptOwnerWrappedText("Answer", labelBounds, new Color(95, 61, 35), 0.37f, maxLines: 1);

            string inputText = _speedQuizOwnerInput.ToString();
            bool showPlaceholder = string.IsNullOrEmpty(inputText);
            const float inputTextScale = 0.38f;
            string displayText = showPlaceholder
                ? "Type answer..."
                : ResolveSpeedQuizOwnerInputTextView(
                    inputText,
                    _speedQuizOwnerCursorIndex,
                    Math.Max(1f, inputBounds.Width - 8f),
                    text => _fontChat.MeasureString(text ?? string.Empty).X * inputTextScale)
                    .VisibleText;
            Color textColor = showPlaceholder ? new Color(123, 123, 123) : Color.Black;
            Vector2 drawPosition = new(inputBounds.X + 4, inputBounds.Y - 1);
            _spriteBatch.DrawString(_fontChat, displayText, drawPosition, textColor, 0f, Vector2.Zero, inputTextScale, SpriteEffects.None, 0f);

            if (!enabled || showPlaceholder)
            {
                return;
            }

            bool cursorVisible = ((currentTickCount - _speedQuizOwnerCursorBlinkStartedAt) / 500) % 2 == 0;
            if (!cursorVisible)
            {
                return;
            }

            SpeedQuizOwnerInputTextView textView = ResolveSpeedQuizOwnerInputTextView(
                inputText,
                _speedQuizOwnerCursorIndex,
                Math.Max(1f, inputBounds.Width - 8f),
                text => _fontChat.MeasureString(text ?? string.Empty).X * inputTextScale);
            string prefix = textView.VisibleText[..Math.Clamp(textView.VisibleCursorIndex, 0, textView.VisibleText.Length)];
            int cursorX = inputBounds.X + 4 + (int)Math.Round(_fontChat.MeasureString(prefix).X * inputTextScale);
            Rectangle cursorBounds = new(cursorX, inputBounds.Y + 2, 1, Math.Max(9, inputBounds.Height - 4));
            _spriteBatch.Draw(_packetScriptOwnerPixelTexture, cursorBounds, Color.Black);
        }

        internal static SpeedQuizOwnerInputTextView ResolveSpeedQuizOwnerInputTextView(
            string inputText,
            int cursorIndex,
            float maxTextWidth,
            Func<string, float> measureText)
        {
            inputText ??= string.Empty;
            measureText ??= static text => text?.Length ?? 0;

            if (inputText.Length == 0)
            {
                return new SpeedQuizOwnerInputTextView(string.Empty, 0, 0);
            }

            int cursor = Math.Clamp(cursorIndex, 0, inputText.Length);
            float boundedWidth = Math.Max(1f, maxTextWidth);
            int start = 0;
            while (start < cursor &&
                   measureText(inputText[start..cursor]) > boundedWidth)
            {
                start++;
            }

            int end = inputText.Length;
            while (end > cursor &&
                   measureText(inputText[start..end]) > boundedWidth)
            {
                end--;
            }

            if (end <= start)
            {
                end = Math.Min(inputText.Length, start + 1);
            }

            return new SpeedQuizOwnerInputTextView(
                inputText[start..end],
                Math.Clamp(cursor - start, 0, end - start),
                start);
        }

        internal readonly record struct SpeedQuizOwnerInputTextView(string VisibleText, int VisibleCursorIndex, int SourceStartIndex);

        private void DrawSpeedQuizOwnerButton(
            PacketScriptButtonVisuals visuals,
            Rectangle bounds,
            SpeedQuizOwnerButtonKind buttonKind,
            string fallbackLabel,
            bool enabled)
        {
            PacketScriptOwnerButtonVisualState state = !enabled
                ? PacketScriptOwnerButtonVisualState.Disabled
                : _speedQuizOwnerPressedButton == buttonKind
                    ? PacketScriptOwnerButtonVisualState.Pressed
                    : _speedQuizOwnerHoveredButton == buttonKind
                        ? PacketScriptOwnerButtonVisualState.Hover
                        : PacketScriptOwnerButtonVisualState.Normal;
            Texture2D texture = visuals?.ResolveTexture(state);
            if (texture != null)
            {
                _spriteBatch.Draw(texture, bounds, Color.White);
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

        private void DrawSpeedQuizOwnerLayer(Texture2D texture, int x, int y)
        {
            if (texture == null)
            {
                return;
            }

            _spriteBatch.Draw(texture, new Rectangle(x, y, texture.Width, texture.Height), Color.White);
        }

        private static SpeedQuizOwnerButtonKind ResolveSpeedQuizOwnerHoveredButton(
            Point cursor,
            Rectangle okButtonBounds,
            Rectangle nextButtonBounds,
            Rectangle giveUpButtonBounds)
        {
            if (okButtonBounds.Contains(cursor))
            {
                return SpeedQuizOwnerButtonKind.Ok;
            }

            if (nextButtonBounds.Contains(cursor))
            {
                return SpeedQuizOwnerButtonKind.Next;
            }

            if (giveUpButtonBounds.Contains(cursor))
            {
                return SpeedQuizOwnerButtonKind.GiveUp;
            }

            return SpeedQuizOwnerButtonKind.None;
        }

        private Rectangle ResolveSpeedQuizOwnerBounds()
        {
            Texture2D primaryBackground = _packetScriptSpeedQuizBackTexture ?? _packetScriptSpeedQuizBackTexture2 ?? _packetScriptSpeedQuizBackTexture3;
            int width = primaryBackground?.Width ?? SpeedQuizOwnerWidth;
            int height = primaryBackground?.Height ?? SpeedQuizOwnerHeight;
            int left = Math.Max(0, (_renderParams.RenderWidth - width) / 2);
            int top = Math.Max(24, (_renderParams.RenderHeight - height) / 2);
            return new Rectangle(left, top, width, height);
        }

        private static Rectangle ResolveSpeedQuizOwnerInputBounds(Rectangle ownerBounds)
        {
            return new Rectangle(ownerBounds.X + 100, ownerBounds.Y + 339, 165, 13);
        }

        private Rectangle ResolveSpeedQuizOwnerGiveUpButtonBounds(Rectangle ownerBounds)
        {
            int width = _packetScriptSpeedQuizGiveUpButtonVisuals?.Normal?.Width
                ?? _packetScriptSpeedQuizGiveUpButtonVisuals?.Hover?.Width
                ?? 60;
            int height = _packetScriptSpeedQuizGiveUpButtonVisuals?.Normal?.Height
                ?? _packetScriptSpeedQuizGiveUpButtonVisuals?.Hover?.Height
                ?? 16;
            Rectangle fallbackBounds = new(ownerBounds.X + 126, ownerBounds.Y + 380, width, height);
            Point origin = Point.Zero;
            Point size = Point.Zero;
            bool hasAnchor = _packetScriptSpeedQuizGiveUpButtonVisuals?.TryGetAnchorMetrics(out origin, out size) == true;
            return ResolveSpeedQuizOwnerButtonBounds(ownerBounds, hasAnchor, origin, size, fallbackBounds);
        }

        private Rectangle ResolveSpeedQuizOwnerNextButtonBounds(Rectangle ownerBounds)
        {
            int width = _packetScriptSpeedQuizNextButtonVisuals?.Normal?.Width
                ?? _packetScriptSpeedQuizNextButtonVisuals?.Hover?.Width
                ?? 40;
            int height = _packetScriptSpeedQuizNextButtonVisuals?.Normal?.Height
                ?? _packetScriptSpeedQuizNextButtonVisuals?.Hover?.Height
                ?? 16;
            Rectangle fallbackBounds = new(ownerBounds.X + 190, ownerBounds.Y + 380, width, height);
            Point origin = Point.Zero;
            Point size = Point.Zero;
            bool hasAnchor = _packetScriptSpeedQuizNextButtonVisuals?.TryGetAnchorMetrics(out origin, out size) == true;
            return ResolveSpeedQuizOwnerButtonBounds(ownerBounds, hasAnchor, origin, size, fallbackBounds);
        }

        private Rectangle ResolveSpeedQuizOwnerOkButtonBounds(Rectangle ownerBounds)
        {
            int width = _packetScriptSpeedQuizOkButtonVisuals?.Normal?.Width
                ?? _packetScriptSpeedQuizOkButtonVisuals?.Hover?.Width
                ?? 40;
            int height = _packetScriptSpeedQuizOkButtonVisuals?.Normal?.Height
                ?? _packetScriptSpeedQuizOkButtonVisuals?.Hover?.Height
                ?? 16;
            Rectangle fallbackBounds = new(ownerBounds.X + 233, ownerBounds.Y + 380, width, height);
            Point origin = Point.Zero;
            Point size = Point.Zero;
            bool hasAnchor = _packetScriptSpeedQuizOkButtonVisuals?.TryGetAnchorMetrics(out origin, out size) == true;
            return ResolveSpeedQuizOwnerButtonBounds(ownerBounds, hasAnchor, origin, size, fallbackBounds);
        }

        internal static Rectangle ResolveSpeedQuizOwnerButtonBounds(
            Rectangle ownerBounds,
            bool hasAnchorMetrics,
            Point anchorOrigin,
            Point anchorSize,
            Rectangle fallbackBounds)
        {
            if (!hasAnchorMetrics || anchorSize.X <= 0 || anchorSize.Y <= 0)
            {
                return fallbackBounds;
            }

            return ResolvePacketScriptOwnerAnchoredBounds(ownerBounds, anchorOrigin, anchorSize);
        }
    }
}
