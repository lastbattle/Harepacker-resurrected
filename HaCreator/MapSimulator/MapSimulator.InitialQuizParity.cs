using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int InitialQuizOwnerWidth = 266;
        private const int InitialQuizOwnerHeight = 224;
        private const int InitialQuizOwnerInputMaxLength = 50;
        private const int InitialQuizOwnerFrameFadeAlpha = 176;
        private const int InitialQuizTimerStringPoolIdTensMinutes = 0x0F73;
        private const int InitialQuizTimerStringPoolIdOnesMinutes = 3955;
        private const int InitialQuizHintLabelStringPoolId = 3958;
        private const int InitialQuizAnswerLabelStringPoolId = 3959;
        private const int InitialQuizAnswerNoticeStringPoolId = 3960;
        private const int InitialQuizTimeoutNoticeStringPoolId = 3964;
        private const float InitialQuizOwnerTextScale = 0.44f;
        private const float InitialQuizOwnerSecondaryTextScale = 0.42f;
        private const float InitialQuizOwnerLabelTextScale = 0.39f;
        private const float InitialQuizOwnerInputTextScale = 0.38f;

        private bool _initialQuizOwnerVisualsLoaded;
        private Texture2D _initialQuizOwnerBackgroundTexture;
        private Texture2D _initialQuizOwnerBackgroundTexture2;
        private Texture2D _initialQuizOwnerBackgroundTexture3;
        private Texture2D _initialQuizOwnerOkButtonNormalTexture;
        private Texture2D _initialQuizOwnerOkButtonHoverTexture;
        private Texture2D _initialQuizOwnerOkButtonPressedTexture;
        private Texture2D _initialQuizOwnerOkButtonDisabledTexture;
        private Texture2D _initialQuizOwnerOkButtonKeyFocusedTexture;
        private Texture2D[] _initialQuizOwnerDigits;
        private Texture2D[] _initialQuizOwnerHeaderDigits;
        private Texture2D _initialQuizOwnerCommaTexture;
        private InitialQuizAnimationFrame[] _initialQuizOwnerAnimationFrames = Array.Empty<InitialQuizAnimationFrame>();
        private Point _initialQuizOwnerBackground3Origin = Point.Zero;

        private readonly StringBuilder _initialQuizOwnerInput = new(InitialQuizOwnerInputMaxLength);
        private int _initialQuizOwnerCursorIndex;
        private int _initialQuizOwnerCursorBlinkStartedAt;
        private bool _initialQuizOwnerHoveringOkButton;
        private bool _initialQuizOwnerPressedOkButton;
        private bool _initialQuizOwnerResultSent;
        private InitialQuizOwnerFocusTarget _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.Input;

        private sealed record InitialQuizAnimationFrame(Texture2D Texture, int DelayMs);
        internal enum InitialQuizOwnerFocusTarget
        {
            Input,
            OkButton
        }

        internal enum InitialQuizOwnerButtonVisualState
        {
            Normal,
            Hover,
            Pressed,
            Disabled,
            KeyFocused
        }

        private bool TryApplyPacketOwnedInitialQuizPayload(byte[] payload, out string message)
        {
            bool applied = _initialQuizTimerRuntime.TryApplyPayload(
                payload,
                currTickCount,
                ResolveInitialQuizOwnerRuntimeCharacterId(),
                out message);
            if (applied)
            {
                if (_initialQuizTimerRuntime.IsActive(currTickCount))
                {
                    ResetInitialQuizOwnerInputState(currTickCount);
                }
                else
                {
                    ClearInitialQuizOwnerInputState();
                }

                SyncUtilityChannelSelectorAvailability();
            }

            return applied;
        }

        private int ResolveInitialQuizOwnerRuntimeCharacterId()
        {
            return Math.Max(0, _playerManager?.Player?.Build?.Id ?? 0);
        }

        private void SyncInitialQuizOwnerContextLifecycle()
        {
            int runtimeCharacterId = ResolveInitialQuizOwnerRuntimeCharacterId();
            _initialQuizTimerRuntime.ObserveRuntimeCharacterId(runtimeCharacterId);
            if (_initialQuizTimerRuntime.RequiresCharacterReset(runtimeCharacterId))
            {
                _initialQuizTimerRuntime.ResetForRuntimeCharacterChange(runtimeCharacterId);
                ClearInitialQuizOwnerInputState();
                SyncUtilityChannelSelectorAvailability();
            }
        }

        private void ResetInitialQuizOwnerInputState(int currentTickCount)
        {
            _initialQuizOwnerInput.Clear();
            _initialQuizOwnerCursorIndex = 0;
            _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
            _initialQuizOwnerHoveringOkButton = false;
            _initialQuizOwnerPressedOkButton = false;
            _initialQuizOwnerResultSent = false;
            _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.Input;
        }

        private void ClearInitialQuizOwnerInputState()
        {
            _initialQuizOwnerInput.Clear();
            _initialQuizOwnerCursorIndex = 0;
            _initialQuizOwnerHoveringOkButton = false;
            _initialQuizOwnerPressedOkButton = false;
            _initialQuizOwnerResultSent = false;
            _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.Input;
        }

        private void UpdateInitialQuizOwner(int currentTickCount)
        {
            if (!_initialQuizTimerRuntime.TryBuildOwnerSnapshot(currentTickCount, out InitialQuizOwnerSnapshot snapshot))
            {
                ClearInitialQuizOwnerInputState();
                return;
            }

            if (snapshot.RemainingMs > 0 || _initialQuizOwnerResultSent)
            {
                return;
            }

            SubmitInitialQuizOwnerResult(string.Empty, currentTickCount, showFeedback: false);
        }

        private bool HandleInitialQuizOwnerMouse(MouseState mouseState, MouseState previousMouseState, int currentTickCount)
        {
            if (!_initialQuizTimerRuntime.TryBuildOwnerSnapshot(currentTickCount, out InitialQuizOwnerSnapshot snapshot))
            {
                return false;
            }

            Rectangle ownerBounds = ResolveInitialQuizOwnerBounds();
            Rectangle okButtonBounds = ResolveInitialQuizOwnerOkButtonBounds(ownerBounds);
            Rectangle inputBounds = ResolveInitialQuizOwnerInputBounds(ownerBounds);
            Point cursor = new(mouseState.X, mouseState.Y);
            _initialQuizOwnerHoveringOkButton = okButtonBounds.Contains(cursor);
            bool showInput = ShouldShowInitialQuizOwnerInput(snapshot.RemainingMs);

            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool justPressed = leftPressed && previousMouseState.LeftButton == ButtonState.Released;
            bool justReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

            if (justPressed)
            {
                if (_initialQuizOwnerHoveringOkButton)
                {
                    _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.OkButton;
                    _initialQuizOwnerPressedOkButton = showInput;
                }
                else if (showInput && inputBounds.Contains(cursor))
                {
                    _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.Input;
                    _initialQuizOwnerPressedOkButton = false;
                }
                else
                {
                    _initialQuizOwnerPressedOkButton = false;
                }

                if (showInput)
                {
                    _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
                }
            }
            else if (!leftPressed)
            {
                _initialQuizOwnerPressedOkButton = false;
            }

            if (justReleased)
            {
                bool confirm = _initialQuizOwnerPressedOkButton && _initialQuizOwnerHoveringOkButton;
                _initialQuizOwnerPressedOkButton = false;
                if (confirm)
                {
                    SubmitInitialQuizOwnerResult(_initialQuizOwnerInput.ToString(), currentTickCount, showFeedback: true);
                }
            }

            return true;
        }

        private bool HandleInitialQuizOwnerKeyboard(KeyboardState newKeyboardState, KeyboardState oldKeyboardState, int currentTickCount)
        {
            if (!_initialQuizTimerRuntime.TryBuildOwnerSnapshot(currentTickCount, out InitialQuizOwnerSnapshot snapshot))
            {
                return false;
            }

            if (snapshot.RemainingMs <= 0)
            {
                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Tab) && oldKeyboardState.IsKeyUp(Keys.Tab))
            {
                _initialQuizOwnerFocusTarget = ResolveNextInitialQuizOwnerFocusTarget(_initialQuizOwnerFocusTarget);
                _initialQuizOwnerPressedOkButton = false;
                _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
                return true;
            }

            bool inputFocused = _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.Input;
            bool buttonFocused = _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.OkButton;

            if (newKeyboardState.IsKeyDown(Keys.Enter) && oldKeyboardState.IsKeyUp(Keys.Enter))
            {
                SubmitInitialQuizOwnerResult(_initialQuizOwnerInput.ToString(), currentTickCount, showFeedback: true);
                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Space) && oldKeyboardState.IsKeyUp(Keys.Space) && buttonFocused)
            {
                SubmitInitialQuizOwnerResult(_initialQuizOwnerInput.ToString(), currentTickCount, showFeedback: true);
                return true;
            }

            if (!inputFocused)
            {
                return buttonFocused;
            }

            if (newKeyboardState.IsKeyDown(Keys.Back) && oldKeyboardState.IsKeyUp(Keys.Back))
            {
                if (_initialQuizOwnerCursorIndex > 0)
                {
                    _initialQuizOwnerInput.Remove(_initialQuizOwnerCursorIndex - 1, 1);
                    _initialQuizOwnerCursorIndex--;
                    _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
                }

                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Delete) && oldKeyboardState.IsKeyUp(Keys.Delete))
            {
                if (_initialQuizOwnerCursorIndex < _initialQuizOwnerInput.Length)
                {
                    _initialQuizOwnerInput.Remove(_initialQuizOwnerCursorIndex, 1);
                    _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
                }

                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Left) && oldKeyboardState.IsKeyUp(Keys.Left))
            {
                if (_initialQuizOwnerCursorIndex > 0)
                {
                    _initialQuizOwnerCursorIndex--;
                    _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
                }

                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Right) && oldKeyboardState.IsKeyUp(Keys.Right))
            {
                if (_initialQuizOwnerCursorIndex < _initialQuizOwnerInput.Length)
                {
                    _initialQuizOwnerCursorIndex++;
                    _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
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

                if (_initialQuizOwnerInput.Length >= InitialQuizOwnerInputMaxLength)
                {
                    break;
                }

                _initialQuizOwnerInput.Insert(_initialQuizOwnerCursorIndex, typed.Value);
                _initialQuizOwnerCursorIndex++;
                _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
            }

            return true;
        }

        private void SubmitInitialQuizOwnerResult(string answerText, int currentTickCount, bool showFeedback)
        {
            if (_initialQuizOwnerResultSent)
            {
                return;
            }

            _initialQuizOwnerResultSent = true;
            string submittedValue = answerText ?? string.Empty;
            NpcInteractionInputSubmission submission = new()
            {
                EntryId = 1,
                EntryTitle = "Initial Quiz",
                NpcName = "Initial Quiz",
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

            _initialQuizTimerRuntime.Clear();
            ClearInitialQuizOwnerInputState();
            SyncUtilityChannelSelectorAvailability();
        }

        private void DrawCenteredPacketOwnedInitialQuizOwner(int currentTickCount, InitialQuizOwnerSnapshot snapshot)
        {
            if (_fontChat == null || GraphicsDevice == null || snapshot == null)
            {
                return;
            }

            EnsureInitialQuizOwnerVisualsLoaded();
            EnsurePacketScriptOwnerVisualsLoaded();

            Rectangle ownerBounds = ResolveInitialQuizOwnerBounds();
            Rectangle overlayBounds = ResolveInitialQuizOwnerOverlayBounds(ownerBounds);
            Rectangle okButtonBounds = ResolveInitialQuizOwnerOkButtonBounds(ownerBounds);
            Rectangle inputBounds = ResolveInitialQuizOwnerInputBounds(ownerBounds);

            DrawPacketScriptOwnerFrame(
                new Rectangle(0, 0, _renderParams.RenderWidth, _renderParams.RenderHeight),
                new Color(0, 0, 0, InitialQuizOwnerFrameFadeAlpha),
                Color.Transparent);

            DrawInitialQuizOwnerLayer(_initialQuizOwnerBackgroundTexture, ownerBounds);
            DrawInitialQuizOwnerLayer(_initialQuizOwnerBackgroundTexture2, ownerBounds);
            DrawInitialQuizOwnerLayer(_initialQuizOwnerBackgroundTexture3, overlayBounds);

            DrawInitialQuizOwnerTimerDigits(ownerBounds, snapshot.RemainingSeconds);
            DrawInitialQuizOwnerQuestionNumber(ownerBounds, Math.Max(0, snapshot.QuestionNumber));

            DrawInitialQuizOwnerAnimationFrame(ownerBounds, currentTickCount);

            DrawInitialQuizOwnerSingleLineText(
                snapshot.Title,
                new Rectangle(ownerBounds.X + 30, ownerBounds.Y + 84, 190, 18),
                Color.White,
                InitialQuizOwnerTextScale);
            DrawInitialQuizOwnerSingleLineText(
                snapshot.ProblemText,
                new Rectangle(ownerBounds.X + 92, ownerBounds.Y + 110, 146, 18),
                Color.White,
                InitialQuizOwnerTextScale);

            if (ShouldShowInitialQuizOwnerHint(snapshot.HintText))
            {
                string hintLabel = MapleStoryStringPool.GetOrFallback(InitialQuizHintLabelStringPoolId, "Clue:");
                DrawInitialQuizOwnerSingleLineText(
                    hintLabel.Trim(),
                    new Rectangle(ownerBounds.X + 52, ownerBounds.Y + 130, 38, 18),
                    Color.White,
                    InitialQuizOwnerSecondaryTextScale);
                DrawInitialQuizOwnerSingleLineText(
                    snapshot.HintText,
                    new Rectangle(ownerBounds.X + 92, ownerBounds.Y + 130, 146, 18),
                    Color.White,
                    InitialQuizOwnerSecondaryTextScale);
            }

            bool showInput = ShouldShowInitialQuizOwnerInput(snapshot.RemainingMs);
            if (showInput)
            {
                DrawInitialQuizOwnerInputField(inputBounds, currentTickCount);
            }

            DrawInitialQuizOwnerSingleLineText(
                MapleStoryStringPool.GetOrFallback(InitialQuizAnswerNoticeStringPoolId, "Enter your answer."),
                new Rectangle(ownerBounds.X + 38, ownerBounds.Y + 202, 190, 18),
                new Color(255, 80, 80),
                InitialQuizOwnerLabelTextScale);

            if (!showInput)
            {
                DrawInitialQuizOwnerSingleLineText(
                    MapleStoryStringPool.GetOrFallback(InitialQuizTimeoutNoticeStringPoolId, "Time is over."),
                    new Rectangle(ownerBounds.X + 119, ownerBounds.Y + 158, 120, 18),
                    new Color(255, 80, 80),
                    InitialQuizOwnerLabelTextScale);
            }

            Texture2D okButtonTexture = ResolveInitialQuizOwnerOkButtonTexture(showInput);
            if (okButtonTexture != null)
            {
                _spriteBatch.Draw(okButtonTexture, okButtonBounds, Color.White);
            }
            else
            {
                DrawPacketScriptOwnerFrame(okButtonBounds, new Color(82, 63, 39, 220), new Color(222, 197, 140));
                DrawPacketScriptOwnerWrappedText("OK", okButtonBounds, Color.White, 0.42f, maxLines: 1);
            }
        }

        private void DrawInitialQuizOwnerInputField(Rectangle inputBounds, int currentTickCount)
        {
            bool inputFocused = _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.Input;
            Color fillColor = inputFocused
                ? new Color(255, 255, 255, 212)
                : new Color(232, 228, 221, 212);
            Color borderColor = inputFocused
                ? new Color(113, 78, 48)
                : new Color(93, 80, 60);
            DrawPacketScriptOwnerFrame(inputBounds, fillColor, borderColor);

            string answerLabel = MapleStoryStringPool.GetOrFallback(InitialQuizAnswerLabelStringPoolId, "Answer:");
            DrawInitialQuizOwnerSingleLineText(
                answerLabel,
                new Rectangle(inputBounds.X - 64, inputBounds.Y, 60, inputBounds.Height),
                Color.White,
                0.37f);

            string inputText = _initialQuizOwnerInput.ToString();
            Vector2 drawPosition = new(inputBounds.X + 4, inputBounds.Y - 1);
            if (!string.IsNullOrEmpty(inputText))
            {
                _spriteBatch.DrawString(_fontChat, inputText, drawPosition, Color.Black, 0f, Vector2.Zero, InitialQuizOwnerInputTextScale, SpriteEffects.None, 0f);
            }

            bool cursorVisible = inputFocused && ShouldDrawInitialQuizOwnerCursor(currentTickCount, _initialQuizOwnerCursorBlinkStartedAt);
            if (!cursorVisible)
            {
                return;
            }

            string prefix = inputText[..Math.Clamp(_initialQuizOwnerCursorIndex, 0, inputText.Length)];
            int cursorX = inputBounds.X + 4 + (int)Math.Round(_fontChat.MeasureString(prefix).X * InitialQuizOwnerInputTextScale);
            Rectangle cursorBounds = new(cursorX, inputBounds.Y + 2, 1, Math.Max(9, inputBounds.Height - 4));
            _spriteBatch.Draw(_packetScriptOwnerPixelTexture, cursorBounds, Color.Black);
        }

        private void DrawInitialQuizOwnerAnimationFrame(Rectangle ownerBounds, int currentTickCount)
        {
            InitialQuizAnimationFrame frame = ResolveInitialQuizOwnerAnimationFrame(currentTickCount);
            if (frame?.Texture == null)
            {
                return;
            }

            Rectangle drawBounds = new(ownerBounds.X + 222, ownerBounds.Y + 65, frame.Texture.Width, frame.Texture.Height);
            _spriteBatch.Draw(frame.Texture, drawBounds, Color.White);
        }

        private void DrawInitialQuizOwnerLayer(Texture2D texture, Rectangle bounds)
        {
            if (texture != null && bounds != Rectangle.Empty)
            {
                _spriteBatch.Draw(texture, new Rectangle(bounds.X, bounds.Y, texture.Width, texture.Height), Color.White);
            }
        }

        private void DrawInitialQuizOwnerTimerDigits(Rectangle ownerBounds, int remainingSeconds)
        {
            string timerText = ComposeInitialQuizOwnerTimerText(remainingSeconds);
            if (_initialQuizOwnerDigits == null || _initialQuizOwnerDigits.Length == 0)
            {
                DrawInitialQuizOwnerSingleLineText(
                    timerText.Replace(',', ':'),
                    new Rectangle(ownerBounds.X + 111, ownerBounds.Y + 33, 78, 24),
                    Color.White,
                    InitialQuizOwnerSecondaryTextScale);
                return;
            }

            DrawInitialQuizOwnerDigitGlyph(ownerBounds, timerText[0], 111, 33);
            DrawInitialQuizOwnerDigitGlyph(ownerBounds, timerText[1], 132, 33);
            DrawInitialQuizOwnerDigitGlyph(ownerBounds, timerText[2], 148, 36);
            DrawInitialQuizOwnerDigitGlyph(ownerBounds, timerText[3], 153, 33);
            DrawInitialQuizOwnerDigitGlyph(ownerBounds, timerText[4], 174, 33);
        }

        private void DrawInitialQuizOwnerQuestionNumber(Rectangle ownerBounds, int questionNumber)
        {
            string questionText = Math.Max(0, questionNumber).ToString();
            if (_initialQuizOwnerHeaderDigits == null || _initialQuizOwnerHeaderDigits.Length == 0)
            {
                DrawInitialQuizOwnerSingleLineText(
                    questionText,
                    new Rectangle(ownerBounds.X + 219, ownerBounds.Y + 72, 30, 24),
                    Color.White,
                    InitialQuizOwnerTextScale);
                return;
            }

            int drawX = ownerBounds.X + 219;
            for (int i = 0; i < questionText.Length; i++)
            {
                Texture2D digitTexture = ResolveInitialQuizOwnerDigitTexture(questionText[i], _initialQuizOwnerHeaderDigits, null);
                if (digitTexture == null)
                {
                    continue;
                }

                _spriteBatch.Draw(digitTexture, new Rectangle(drawX, ownerBounds.Y + 72, digitTexture.Width, digitTexture.Height), Color.White);
                drawX += digitTexture.Width;
            }
        }

        private Texture2D ResolveInitialQuizOwnerOkButtonTexture(bool enabled)
        {
            InitialQuizOwnerButtonVisualState state = ResolveInitialQuizOwnerButtonVisualState(
                enabled,
                _initialQuizOwnerPressedOkButton,
                _initialQuizOwnerHoveringOkButton,
                _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.OkButton);

            return state switch
            {
                InitialQuizOwnerButtonVisualState.Disabled => _initialQuizOwnerOkButtonDisabledTexture ?? _initialQuizOwnerOkButtonNormalTexture,
                InitialQuizOwnerButtonVisualState.Pressed => _initialQuizOwnerOkButtonPressedTexture ?? _initialQuizOwnerOkButtonHoverTexture ?? _initialQuizOwnerOkButtonKeyFocusedTexture ?? _initialQuizOwnerOkButtonNormalTexture,
                InitialQuizOwnerButtonVisualState.Hover => _initialQuizOwnerOkButtonHoverTexture ?? _initialQuizOwnerOkButtonKeyFocusedTexture ?? _initialQuizOwnerOkButtonNormalTexture,
                InitialQuizOwnerButtonVisualState.KeyFocused => _initialQuizOwnerOkButtonKeyFocusedTexture ?? _initialQuizOwnerOkButtonHoverTexture ?? _initialQuizOwnerOkButtonNormalTexture,
                _ => _initialQuizOwnerOkButtonNormalTexture
            };
        }

        private InitialQuizAnimationFrame ResolveInitialQuizOwnerAnimationFrame(int currentTickCount)
        {
            if (_initialQuizOwnerAnimationFrames.Length == 0)
            {
                return null;
            }

            int totalDuration = _initialQuizOwnerAnimationFrames.Sum(static frame => Math.Max(1, frame.DelayMs));
            if (totalDuration <= 0)
            {
                return _initialQuizOwnerAnimationFrames[0];
            }

            int frameTime = Math.Abs(currentTickCount) % totalDuration;
            int accumulated = 0;
            for (int i = 0; i < _initialQuizOwnerAnimationFrames.Length; i++)
            {
                accumulated += Math.Max(1, _initialQuizOwnerAnimationFrames[i].DelayMs);
                if (frameTime < accumulated)
                {
                    return _initialQuizOwnerAnimationFrames[i];
                }
            }

            return _initialQuizOwnerAnimationFrames[^1];
        }

        private Rectangle ResolveInitialQuizOwnerBounds()
        {
            int ownerWidth = _initialQuizOwnerBackgroundTexture?.Width > 0
                ? _initialQuizOwnerBackgroundTexture.Width
                : InitialQuizOwnerWidth;
            int ownerHeight = _initialQuizOwnerBackgroundTexture?.Height > 0
                ? _initialQuizOwnerBackgroundTexture.Height
                : InitialQuizOwnerHeight;
            int left = Math.Max(0, (_renderParams.RenderWidth - ownerWidth) / 2);
            int top = Math.Max(24, (_renderParams.RenderHeight - ownerHeight) / 2);
            return new Rectangle(left, top, ownerWidth, ownerHeight);
        }

        private static Rectangle ResolveInitialQuizOwnerOkButtonBounds(Rectangle ownerBounds)
        {
            return new Rectangle(ownerBounds.X + 241, ownerBounds.Y + 199, 40, 16);
        }

        private static Rectangle ResolveInitialQuizOwnerInputBounds(Rectangle ownerBounds)
        {
            return new Rectangle(ownerBounds.X + 109, ownerBounds.Y + 157, 150, 13);
        }

        private void EnsureInitialQuizOwnerVisualsLoaded()
        {
            if (_initialQuizOwnerVisualsLoaded || GraphicsDevice == null)
            {
                return;
            }

            _initialQuizOwnerVisualsLoaded = true;
            _packetScriptOwnerPixelTexture ??= new Texture2D(GraphicsDevice, 1, 1);
            _packetScriptOwnerPixelTexture.SetData(new[] { Color.White });

            WzImage uiWindowImage = Program.FindImage("UI", "UIWindow.img");
            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img") ?? uiWindowImage;
            WzSubProperty preferred = uiWindow2Image?["InitialQuiz"] as WzSubProperty;
            WzSubProperty fallback = uiWindowImage?["InitialQuiz"] as WzSubProperty;
            WzCanvasProperty preferredBackground3 = preferred?["backgrnd3"] as WzCanvasProperty;
            WzCanvasProperty fallbackBackground3 = fallback?["backgrnd3"] as WzCanvasProperty;

            _initialQuizOwnerBackgroundTexture = LoadUiCanvasTexture((preferred?["backgrnd"] ?? fallback?["backgrnd"]) as WzCanvasProperty);
            _initialQuizOwnerBackgroundTexture2 = LoadUiCanvasTexture((preferred?["backgrnd2"] ?? fallback?["backgrnd2"]) as WzCanvasProperty);
            _initialQuizOwnerBackgroundTexture3 = LoadUiCanvasTexture(preferredBackground3 ?? fallbackBackground3);
            _initialQuizOwnerOkButtonNormalTexture = LoadUiCanvasTexture(ResolveInitialQuizOwnerButtonCanvas(preferred?["BtOK"] as WzSubProperty, "normal") ?? ResolveInitialQuizOwnerButtonCanvas(fallback?["BtOK"] as WzSubProperty, "normal"));
            _initialQuizOwnerOkButtonHoverTexture = LoadUiCanvasTexture(ResolveInitialQuizOwnerButtonCanvas(preferred?["BtOK"] as WzSubProperty, "mouseOver") ?? ResolveInitialQuizOwnerButtonCanvas(fallback?["BtOK"] as WzSubProperty, "mouseOver"));
            _initialQuizOwnerOkButtonPressedTexture = LoadUiCanvasTexture(ResolveInitialQuizOwnerButtonCanvas(preferred?["BtOK"] as WzSubProperty, "pressed") ?? ResolveInitialQuizOwnerButtonCanvas(fallback?["BtOK"] as WzSubProperty, "pressed"));
            _initialQuizOwnerOkButtonDisabledTexture = LoadUiCanvasTexture(ResolveInitialQuizOwnerButtonCanvas(preferred?["BtOK"] as WzSubProperty, "disabled") ?? ResolveInitialQuizOwnerButtonCanvas(fallback?["BtOK"] as WzSubProperty, "disabled"));
            _initialQuizOwnerOkButtonKeyFocusedTexture = LoadUiCanvasTexture(ResolveInitialQuizOwnerButtonCanvas(preferred?["BtOK"] as WzSubProperty, "keyFocused") ?? ResolveInitialQuizOwnerButtonCanvas(fallback?["BtOK"] as WzSubProperty, "keyFocused"));
            _initialQuizOwnerDigits = LoadInitialQuizOwnerDigits(preferred?["num1"] as WzSubProperty, fallback?["num1"] as WzSubProperty, out _initialQuizOwnerCommaTexture);
            _initialQuizOwnerHeaderDigits = LoadInitialQuizOwnerDigits(
                ResolveInitialQuizOwnerHeaderDigits(preferred),
                ResolveInitialQuizOwnerHeaderDigits(fallback),
                out _);
            _initialQuizOwnerAnimationFrames = LoadInitialQuizOwnerAnimationFrames(preferred?["ani"] as WzSubProperty, fallback?["ani"] as WzSubProperty);
            _initialQuizOwnerBackground3Origin = ResolveCanvasOrigin(preferredBackground3 ?? fallbackBackground3);
        }

        private Texture2D[] LoadInitialQuizOwnerDigits(WzSubProperty preferred, WzSubProperty fallback, out Texture2D commaTexture)
        {
            Texture2D[] digits = new Texture2D[10];
            for (int i = 0; i < digits.Length; i++)
            {
                digits[i] = LoadUiCanvasTexture((preferred?[i.ToString()] ?? fallback?[i.ToString()]) as WzCanvasProperty);
            }

            commaTexture = LoadUiCanvasTexture((preferred?["comma"] ?? fallback?["comma"]) as WzCanvasProperty);
            return digits;
        }

        private InitialQuizAnimationFrame[] LoadInitialQuizOwnerAnimationFrames(WzSubProperty preferred, WzSubProperty fallback)
        {
            List<InitialQuizAnimationFrame> frames = new();
            IEnumerable<WzCanvasProperty> canvases = preferred?.WzProperties.OfType<WzCanvasProperty>()
                ?? fallback?.WzProperties.OfType<WzCanvasProperty>()
                ?? Enumerable.Empty<WzCanvasProperty>();
            foreach (WzCanvasProperty canvas in canvases.OrderBy(static canvas => canvas.Name, StringComparer.Ordinal))
            {
                Texture2D texture = LoadUiCanvasTexture(canvas);
                if (texture == null)
                {
                    continue;
                }

                int delay = (canvas["delay"] as WzIntProperty)?.Value ?? 60;
                frames.Add(new InitialQuizAnimationFrame(texture, Math.Max(1, delay)));
            }

            return frames.ToArray();
        }

        private static WzCanvasProperty ResolveInitialQuizOwnerButtonCanvas(WzSubProperty buttonProperty, string stateName)
        {
            if (buttonProperty == null)
            {
                return null;
            }

                return buttonProperty[stateName]?["0"] as WzCanvasProperty
                ?? buttonProperty[stateName]?.WzProperties.OfType<WzCanvasProperty>().FirstOrDefault()
                ?? buttonProperty[stateName == "mouseOver" ? "keyFocused" : stateName]?["0"] as WzCanvasProperty
                ?? buttonProperty.WzProperties.OfType<WzSubProperty>()
                    .FirstOrDefault(property => string.Equals(property.Name, stateName, StringComparison.OrdinalIgnoreCase))
                    ?.WzProperties.OfType<WzCanvasProperty>()
                    .FirstOrDefault();
        }

        private static WzSubProperty ResolveInitialQuizOwnerHeaderDigits(WzSubProperty ownerProperty)
        {
            return ownerProperty?["number"] as WzSubProperty
                ?? ownerProperty?["num2"] as WzSubProperty;
        }

        internal static bool ShouldShowInitialQuizOwnerHint(string hintText)
        {
            return !string.IsNullOrWhiteSpace(hintText);
        }

        internal static bool ShouldShowInitialQuizOwnerInput(int remainingMs)
        {
            return remainingMs > 0;
        }

        internal static InitialQuizOwnerFocusTarget ResolveNextInitialQuizOwnerFocusTarget(InitialQuizOwnerFocusTarget currentFocus)
        {
            return currentFocus == InitialQuizOwnerFocusTarget.Input
                ? InitialQuizOwnerFocusTarget.OkButton
                : InitialQuizOwnerFocusTarget.Input;
        }

        internal static InitialQuizOwnerButtonVisualState ResolveInitialQuizOwnerButtonVisualState(bool enabled, bool pressed, bool hover, bool keyFocused)
        {
            if (!enabled)
            {
                return InitialQuizOwnerButtonVisualState.Disabled;
            }

            if (pressed)
            {
                return InitialQuizOwnerButtonVisualState.Pressed;
            }

            if (hover)
            {
                return InitialQuizOwnerButtonVisualState.Hover;
            }

            return keyFocused
                ? InitialQuizOwnerButtonVisualState.KeyFocused
                : InitialQuizOwnerButtonVisualState.Normal;
        }

        internal static bool ShouldDrawInitialQuizOwnerCursor(int currentTickCount, int cursorBlinkStartedAt)
        {
            return ((currentTickCount - cursorBlinkStartedAt) / 500) % 2 == 0;
        }

        internal static string ComposeInitialQuizOwnerTimerText(int remainingSeconds)
        {
            int minutes = Math.Max(0, remainingSeconds) / 60;
            int seconds = Math.Max(0, remainingSeconds) % 60;
            return $"{minutes:D2},{seconds:D2}";
        }

        internal static string NormalizeInitialQuizOwnerSingleLineText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return string.Join(
                " ",
                text.Replace("\r", " ", StringComparison.Ordinal)
                    .Replace("\n", " ", StringComparison.Ordinal)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private void DrawInitialQuizOwnerSingleLineText(string text, Rectangle bounds, Color color, float scale)
        {
            if (_fontChat == null || bounds == Rectangle.Empty)
            {
                return;
            }

            string displayText = FitInitialQuizOwnerTextToBounds(NormalizeInitialQuizOwnerSingleLineText(text), bounds.Width, scale);
            if (string.IsNullOrEmpty(displayText))
            {
                return;
            }

            _spriteBatch.DrawString(_fontChat, displayText, new Vector2(bounds.X, bounds.Y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private string FitInitialQuizOwnerTextToBounds(string text, int maxWidth, float scale)
        {
            if (_fontChat == null || string.IsNullOrEmpty(text) || maxWidth <= 0)
            {
                return string.Empty;
            }

            if (_fontChat.MeasureString(text).X * scale <= maxWidth)
            {
                return text;
            }

            int length = text.Length;
            while (length > 0)
            {
                string candidate = text[..length];
                if (_fontChat.MeasureString(candidate).X * scale <= maxWidth)
                {
                    return candidate.TrimEnd();
                }

                length--;
            }

            return string.Empty;
        }

        private void DrawInitialQuizOwnerDigitGlyph(Rectangle ownerBounds, char ch, int relativeX, int relativeY)
        {
            Texture2D texture = ResolveInitialQuizOwnerDigitTexture(ch, _initialQuizOwnerDigits, _initialQuizOwnerCommaTexture);
            if (texture == null)
            {
                return;
            }

            Rectangle drawBounds = new(ownerBounds.X + relativeX, ownerBounds.Y + relativeY, texture.Width, texture.Height);
            _spriteBatch.Draw(texture, drawBounds, Color.White);
        }

        private static Texture2D ResolveInitialQuizOwnerDigitTexture(char ch, Texture2D[] digits, Texture2D commaTexture)
        {
            if (char.IsDigit(ch))
            {
                int index = ch - '0';
                return index >= 0 && digits != null && index < digits.Length
                    ? digits[index]
                    : null;
            }

            return ch is ',' or ':'
                ? commaTexture
                : null;
        }

        private Rectangle ResolveInitialQuizOwnerOverlayBounds(Rectangle ownerBounds)
        {
            if (_initialQuizOwnerBackgroundTexture3 == null)
            {
                return new Rectangle(ownerBounds.X + 22, ownerBounds.Y + 67, 234, 118);
            }

            return new Rectangle(
                ownerBounds.X - _initialQuizOwnerBackground3Origin.X,
                ownerBounds.Y - _initialQuizOwnerBackground3Origin.Y,
                _initialQuizOwnerBackgroundTexture3.Width,
                _initialQuizOwnerBackgroundTexture3.Height);
        }

        private static Point ResolveCanvasOrigin(WzCanvasProperty canvas)
        {
            if (canvas == null)
            {
                return Point.Zero;
            }

            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            return new Point((int)Math.Round(origin.X), (int)Math.Round(origin.Y));
        }

        private static char? TryMapInitialQuizOwnerChar(Keys key, bool shiftPressed)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                char value = (char)('a' + (key - Keys.A));
                return shiftPressed ? char.ToUpperInvariant(value) : value;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return shiftPressed
                    ? new[] { ')', '!', '@', '#', '$', '%', '^', '&', '*', '(' }[key - Keys.D0]
                    : (char)('0' + (key - Keys.D0));
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }

            return key switch
            {
                Keys.Space => ' ',
                Keys.OemPeriod => shiftPressed ? '>' : '.',
                Keys.OemComma => shiftPressed ? '<' : ',',
                Keys.OemMinus => shiftPressed ? '_' : '-',
                Keys.OemPlus => shiftPressed ? '+' : '=',
                Keys.OemQuestion => shiftPressed ? '?' : '/',
                Keys.OemSemicolon => shiftPressed ? ':' : ';',
                Keys.OemQuotes => shiftPressed ? '"' : '\'',
                Keys.OemOpenBrackets => shiftPressed ? '{' : '[',
                Keys.OemCloseBrackets => shiftPressed ? '}' : ']',
                Keys.OemPipe => shiftPressed ? '|' : '\\',
                Keys.OemTilde => shiftPressed ? '~' : '`',
                _ => null
            };
        }
    }
}
