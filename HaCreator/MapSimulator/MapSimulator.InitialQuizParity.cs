using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
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
        private const int InitialQuizOwnerInputFontHeightPixels = 12;
        private const int InitialQuizOwnerFrameFadeAlpha = 176;
        private const int InitialQuizBackgroundUolStringPoolId = 0x0F72;
        private const int InitialQuizTimerDigitUolStringPoolId = 0x0F73;
        private const int InitialQuizTimerCommaUolStringPoolId = 0x0F74;
        private const int InitialQuizOkButtonUolStringPoolId = 0x0512;
        private const int InitialQuizQuestionLabelStringPoolId = 0x0F75;
        private const int InitialQuizHintLabelStringPoolId = 3958;
        private const int InitialQuizAnswerLabelStringPoolId = 3959;
        private const int InitialQuizAnswerNoticeStringPoolId = 3960;
        private const int InitialQuizMinInputNoticeStringPoolId = 0x0F79;
        private const int InitialQuizMaxInputNoticeStringPoolId = 0x0F7A;
        private const int InitialQuizTimeoutNoticeStringPoolId = 3964;
        private const int InitialQuizOwnerOkButtonLeft = 241;
        private const int InitialQuizOwnerOkButtonTop = 199;
        private const int InitialQuizOwnerOkButtonWidth = 47;
        private const int InitialQuizOwnerOkButtonHeight = 18;
        private const float InitialQuizOwnerTextScale = 0.44f;
        private const float InitialQuizOwnerSecondaryTextScale = 0.42f;
        private const float InitialQuizOwnerLabelTextScale = 0.39f;
        private const float InitialQuizOwnerInputTextScale = 0.38f;

        private bool _initialQuizOwnerVisualsLoaded;
        private ClientTextRasterizer _initialQuizOwnerInputTextRasterizer;
        private AntiMacroEditControl _initialQuizOwnerEditControl;
        private Texture2D _initialQuizOwnerBackgroundTexture;
        private Texture2D _initialQuizOwnerBackgroundTexture2;
        private Texture2D _initialQuizOwnerBackgroundTexture3;
        private InitialQuizButtonFrame _initialQuizOwnerOkButtonNormalFrame;
        private InitialQuizButtonFrame _initialQuizOwnerOkButtonHoverFrame;
        private InitialQuizButtonFrame _initialQuizOwnerOkButtonPressedFrame;
        private InitialQuizButtonFrame _initialQuizOwnerOkButtonDisabledFrame;
        private InitialQuizButtonFrame _initialQuizOwnerOkButtonKeyFocusedFrame;
        private Texture2D[] _initialQuizOwnerDigits;
        private Texture2D _initialQuizOwnerCommaTexture;
        private InitialQuizAnimationFrame[] _initialQuizOwnerAnimationFrames = Array.Empty<InitialQuizAnimationFrame>();
        private Point _initialQuizOwnerBackground3Origin = Point.Zero;
        private static readonly string[] InitialQuizOwnerInputFontFamilyCandidates =
        {
            "Arial",
            "DotumChe",
            "Dotum",
            "GulimChe",
            "Gulim",
            "Tahoma",
        };
        private static readonly Keys[] InitialQuizOwnerEditKeyPriority =
        {
            Keys.Back,
            Keys.Delete,
            Keys.Left,
            Keys.Right,
            Keys.Home,
            Keys.End,
        };

        private readonly StringBuilder _initialQuizOwnerInput = new(InitialQuizOwnerInputMaxLength);
        private int _initialQuizOwnerCursorIndex;
        private int _initialQuizOwnerCursorBlinkStartedAt;
        private Keys _initialQuizOwnerHeldEditKey = Keys.None;
        private int _initialQuizOwnerKeyHoldStartedAt;
        private int _initialQuizOwnerLastKeyRepeatAt;
        private bool _initialQuizOwnerHoveringOkButton;
        private bool _initialQuizOwnerPressedOkButton;
        private bool _initialQuizOwnerResultSent;
        private bool _initialQuizOwnerTimeoutCloseArmed;
        private InitialQuizOwnerFocusTarget _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.Input;

        private sealed record InitialQuizAnimationFrame(Texture2D Texture, int DelayMs);
        private sealed record InitialQuizButtonFrame(Texture2D Texture, Point Origin);
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
                out InitialQuizOwnerApplyDisposition disposition,
                out message);
            if (applied)
            {
                if (disposition == InitialQuizOwnerApplyDisposition.Started)
                {
                    ResetInitialQuizOwnerInputState(currTickCount);
                }
                else if (disposition == InitialQuizOwnerApplyDisposition.Cleared)
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
            _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
            _initialQuizOwnerHoveringOkButton = false;
            _initialQuizOwnerPressedOkButton = false;
            _initialQuizOwnerResultSent = false;
            _initialQuizOwnerTimeoutCloseArmed = false;
            _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.Input;
            _initialQuizOwnerInput.Clear();
            _initialQuizOwnerCursorIndex = 0;
            if (_initialQuizOwnerEditControl != null)
            {
                _initialQuizOwnerEditControl.Reset();
            }
            ResetInitialQuizOwnerHeldEditKey();
        }

        private void ClearInitialQuizOwnerInputState()
        {
            _initialQuizOwnerHoveringOkButton = false;
            _initialQuizOwnerPressedOkButton = false;
            _initialQuizOwnerResultSent = false;
            _initialQuizOwnerTimeoutCloseArmed = false;
            _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.Input;
            _initialQuizOwnerInput.Clear();
            _initialQuizOwnerCursorIndex = 0;
            if (_initialQuizOwnerEditControl != null)
            {
                _initialQuizOwnerEditControl.Clear();
            }
            ResetInitialQuizOwnerHeldEditKey();
        }

        private void UpdateInitialQuizOwner(int currentTickCount)
        {
            if (!_initialQuizTimerRuntime.TryBuildOwnerSnapshot(currentTickCount, out InitialQuizOwnerSnapshot snapshot))
            {
                ClearInitialQuizOwnerInputState();
                return;
            }

            InitialQuizOwnerTimeoutBehavior timeoutBehavior = ResolveInitialQuizOwnerTimeoutBehavior(
                snapshot.RemainingMs,
                _initialQuizOwnerResultSent,
                _initialQuizOwnerTimeoutCloseArmed);
            if (timeoutBehavior == InitialQuizOwnerTimeoutBehavior.Wait)
            {
                _initialQuizOwnerTimeoutCloseArmed = false;
                return;
            }

            if (timeoutBehavior == InitialQuizOwnerTimeoutBehavior.ArmClose)
            {
                _initialQuizOwnerTimeoutCloseArmed = true;
                _initialQuizOwnerPressedOkButton = false;
                _initialQuizOwnerHoveringOkButton = false;
                return;
            }

            SubmitInitialQuizOwnerResult(string.Empty, currentTickCount, showFeedback: false, validateAnswer: false);
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
            AntiMacroEditControl editControl = EnsureInitialQuizOwnerEditControl();

            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool justPressed = leftPressed && previousMouseState.LeftButton == ButtonState.Released;
            bool justReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

            if (justPressed)
            {
                if (showInput
                    && _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.Input
                    && TrySelectInitialQuizOwnerImeCandidateFromMouse(editControl, ownerBounds, cursor))
                {
                    _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
                    return true;
                }

                if (_initialQuizOwnerHoveringOkButton)
                {
                    _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.OkButton;
                    _initialQuizOwnerPressedOkButton = showInput;
                }
                else if (showInput && inputBounds.Contains(cursor))
                {
                    _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.Input;
                    _initialQuizOwnerPressedOkButton = false;
                    if (editControl != null)
                    {
                        editControl.BeginSelectionAtMouseX(cursor.X, ownerBounds);
                        SyncInitialQuizOwnerLegacyInputStateFromEditControl();
                    }
                    else
                    {
                        _initialQuizOwnerCursorIndex = ResolveInitialQuizOwnerCursorIndexFromClick(
                            _initialQuizOwnerInput.ToString(),
                            inputBounds,
                            cursor.X);
                    }
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
            if (justReleased)
            {
                editControl?.EndMouseSelection();
                bool confirm = ShouldSubmitInitialQuizOwnerOkButtonRelease(
                    _initialQuizOwnerPressedOkButton,
                    _initialQuizOwnerHoveringOkButton,
                    showInput);
                _initialQuizOwnerPressedOkButton = false;
                if (confirm)
                {
                    SubmitInitialQuizOwnerResult(GetInitialQuizOwnerSubmittedText(), currentTickCount, showFeedback: true);
                }
            }
            else if (leftPressed && showInput && _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.Input)
            {
                if (editControl?.IsSelectingWithMouse == true)
                {
                    editControl.UpdateSelectionAtMouseX(cursor.X, ownerBounds);
                    SyncInitialQuizOwnerLegacyInputStateFromEditControl();
                }
            }
            else if (!leftPressed)
            {
                editControl?.EndMouseSelection();
                _initialQuizOwnerPressedOkButton = false;
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
                SubmitInitialQuizOwnerResult(GetInitialQuizOwnerSubmittedText(), currentTickCount, showFeedback: true);
                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Space) && oldKeyboardState.IsKeyUp(Keys.Space) && buttonFocused)
            {
                SubmitInitialQuizOwnerResult(GetInitialQuizOwnerSubmittedText(), currentTickCount, showFeedback: true);
                return true;
            }

            if (!inputFocused)
            {
                EnsureInitialQuizOwnerEditControl()?.SetFocus(false);
                ResetInitialQuizOwnerHeldEditKey();
                return buttonFocused;
            }

            AntiMacroEditControl editControl = EnsureInitialQuizOwnerEditControl();
            if (TryDispatchInitialQuizOwnerImeCandidateSelection(
                    editControl?.CandidateListState ?? ImeCandidateListState.Empty,
                    newKeyboardState,
                    oldKeyboardState))
            {
                return true;
            }

            editControl?.SetFocus(true);
            editControl?.HandleKeyboardInput(newKeyboardState, oldKeyboardState);
            SyncInitialQuizOwnerLegacyInputStateFromEditControl();
            ResetInitialQuizOwnerHeldEditKey();
            return true;
        }

        private static bool TryResolveInitialQuizOwnerNewEditKey(KeyboardState newKeyboardState, KeyboardState oldKeyboardState, out Keys editKey)
        {
            Keys[] pressedKeys = newKeyboardState.GetPressedKeys();
            foreach (Keys candidate in InitialQuizOwnerEditKeyPriority)
            {
                if (newKeyboardState.IsKeyDown(candidate) && oldKeyboardState.IsKeyUp(candidate))
                {
                    editKey = candidate;
                    return true;
                }
            }

            bool shiftPressed = newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys candidate in pressedKeys)
            {
                if (oldKeyboardState.IsKeyDown(candidate) || !TryMapInitialQuizOwnerChar(candidate, shiftPressed).HasValue)
                {
                    continue;
                }

                editKey = candidate;
                return true;
            }

            editKey = Keys.None;
            return false;
        }

        private void TrackInitialQuizOwnerHeldEditKey(Keys editKey, int currentTickCount)
        {
            _initialQuizOwnerHeldEditKey = editKey;
            _initialQuizOwnerKeyHoldStartedAt = currentTickCount;
            _initialQuizOwnerLastKeyRepeatAt = currentTickCount;
        }

        private void ResetInitialQuizOwnerHeldEditKey()
        {
            _initialQuizOwnerHeldEditKey = Keys.None;
            _initialQuizOwnerKeyHoldStartedAt = 0;
            _initialQuizOwnerLastKeyRepeatAt = 0;
        }

        private void ApplyInitialQuizOwnerEditKey(Keys editKey, bool shiftPressed, int currentTickCount)
        {
            switch (editKey)
            {
                case Keys.Back:
                    if (_initialQuizOwnerCursorIndex > 0)
                    {
                        _initialQuizOwnerInput.Remove(_initialQuizOwnerCursorIndex - 1, 1);
                        _initialQuizOwnerCursorIndex--;
                    }
                    break;
                case Keys.Delete:
                    if (_initialQuizOwnerCursorIndex < _initialQuizOwnerInput.Length)
                    {
                        _initialQuizOwnerInput.Remove(_initialQuizOwnerCursorIndex, 1);
                    }
                    break;
                case Keys.Left:
                    if (_initialQuizOwnerCursorIndex > 0)
                    {
                        _initialQuizOwnerCursorIndex--;
                    }
                    break;
                case Keys.Right:
                    if (_initialQuizOwnerCursorIndex < _initialQuizOwnerInput.Length)
                    {
                        _initialQuizOwnerCursorIndex++;
                    }
                    break;
                case Keys.Home:
                    _initialQuizOwnerCursorIndex = 0;
                    break;
                case Keys.End:
                    _initialQuizOwnerCursorIndex = _initialQuizOwnerInput.Length;
                    break;
                default:
                    char? typed = TryMapInitialQuizOwnerChar(editKey, shiftPressed);
                    if (typed.HasValue && _initialQuizOwnerInput.Length < InitialQuizOwnerInputMaxLength)
                    {
                        _initialQuizOwnerInput.Insert(_initialQuizOwnerCursorIndex, typed.Value);
                        _initialQuizOwnerCursorIndex++;
                    }
                    break;
            }

            _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
        }

        private void SubmitInitialQuizOwnerResult(string answerText, int currentTickCount, bool showFeedback, bool validateAnswer = true)
        {
            if (_initialQuizOwnerResultSent)
            {
                return;
            }

            string submittedValue = answerText ?? string.Empty;
            InitialQuizOwnerSubmissionValidation validation = ValidateInitialQuizOwnerSubmission(
                submittedValue,
                _initialQuizTimerRuntime.TryBuildOwnerSnapshot(currentTickCount, out InitialQuizOwnerSnapshot snapshot)
                    ? snapshot.MinInputByteLength
                    : 0,
                snapshot?.MaxInputByteLength ?? 0,
                validateAnswer);
            if (!validation.CanSubmit)
            {
                if (showFeedback && !string.IsNullOrWhiteSpace(validation.NoticeMessage))
                {
                    ShowUtilityFeedbackMessage(validation.NoticeMessage);
                }

                if (validation.RefocusInput)
                {
                    _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.Input;
                    _initialQuizOwnerPressedOkButton = false;
                    _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
                }

                return;
            }

            _initialQuizOwnerResultSent = true;
            PacketScriptMessageRuntime.PacketScriptResponsePacket responsePacket =
                _packetScriptMessageRuntime.BuildInitialQuizOwnerResponsePacket(submittedValue);
            bool dispatched = TryDispatchPacketScriptResponse(responsePacket, out string dispatchStatus);
            _packetScriptMessageRuntime.RecordResponseDispatch(responsePacket, dispatched, dispatchStatus);
            if (showFeedback)
            {
                ShowUtilityFeedbackMessage($"{responsePacket.Summary} {dispatchStatus}".Trim());
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
            DrawInitialQuizOwnerAnimationFrame(ownerBounds, currentTickCount);

            DrawInitialQuizOwnerSingleLineText(
                snapshot.Title,
                new Rectangle(ownerBounds.X + 30, ownerBounds.Y + 84, 190, 18),
                Color.White,
                InitialQuizOwnerTextScale);
            DrawInitialQuizOwnerQuestionLabel(ownerBounds);
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
            DrawInitialQuizOwnerAnswerLabel(inputBounds);
            if (showInput)
            {
                DrawInitialQuizOwnerInputField(ownerBounds, inputBounds, currentTickCount);
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

            InitialQuizButtonFrame okButtonFrame = ResolveInitialQuizOwnerOkButtonFrame(showInput);
            if (okButtonFrame?.Texture != null)
            {
                Rectangle drawBounds = new(
                    okButtonBounds.X - okButtonFrame.Origin.X,
                    okButtonBounds.Y - okButtonFrame.Origin.Y,
                    okButtonFrame.Texture.Width,
                    okButtonFrame.Texture.Height);
                _spriteBatch.Draw(okButtonFrame.Texture, drawBounds, Color.White);
            }
            else
            {
                DrawPacketScriptOwnerFrame(okButtonBounds, new Color(82, 63, 39, 220), new Color(222, 197, 140));
                DrawPacketScriptOwnerWrappedText("OK", okButtonBounds, Color.White, 0.42f, maxLines: 1);
            }
        }

        private void DrawInitialQuizOwnerAnswerLabel(Rectangle inputBounds)
        {
            string answerLabel = MapleStoryStringPool.GetOrFallback(InitialQuizAnswerLabelStringPoolId, "Answer:");
            DrawInitialQuizOwnerSingleLineText(
                answerLabel,
                new Rectangle(inputBounds.X - 64, inputBounds.Y, 60, inputBounds.Height),
                Color.White,
                0.37f);
        }

        private void DrawInitialQuizOwnerQuestionLabel(Rectangle ownerBounds)
        {
            string questionLabel = MapleStoryStringPool.GetOrFallback(InitialQuizQuestionLabelStringPoolId, "Question:");
            DrawInitialQuizOwnerSingleLineText(
                questionLabel,
                new Rectangle(ownerBounds.X + 52, ownerBounds.Y + 110, 38, 18),
                Color.White,
                InitialQuizOwnerSecondaryTextScale);
        }

        private void DrawInitialQuizOwnerInputField(Rectangle inputBounds, int currentTickCount)
        {
            DrawInitialQuizOwnerInputField(Rectangle.Empty, inputBounds, currentTickCount);
        }

        private void DrawInitialQuizOwnerInputField(Rectangle ownerBounds, Rectangle inputBounds, int currentTickCount)
        {
            bool inputFocused = _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.Input;
            Color fillColor = inputFocused
                ? new Color(255, 255, 255, 212)
                : new Color(232, 228, 221, 212);
            Color borderColor = inputFocused
                ? new Color(113, 78, 48)
                : new Color(93, 80, 60);
            DrawPacketScriptOwnerFrame(inputBounds, fillColor, borderColor);

            AntiMacroEditControl editControl = EnsureInitialQuizOwnerEditControl();
            if (editControl != null && ownerBounds != Rectangle.Empty)
            {
                editControl.SetFocus(inputFocused);
                editControl.Draw(_spriteBatch, ownerBounds, drawChrome: false);
                editControl.DrawImeCandidateWindow(_spriteBatch, ownerBounds);
                SyncInitialQuizOwnerLegacyInputStateFromEditControl();
                return;
            }

            ClientTextRasterizer inputTextRasterizer = EnsureInitialQuizOwnerInputTextRasterizer();
            string inputText = _initialQuizOwnerInput.ToString();
            int textAreaWidth = Math.Max(0, inputBounds.Width - 8);
            int[] glyphWidths = MeasureInitialQuizOwnerInputGlyphWidths(inputText);
            int visibleStart = ResolveInitialQuizOwnerVisibleStart(glyphWidths, _initialQuizOwnerCursorIndex, textAreaWidth);
            int visibleLength = ResolveInitialQuizOwnerVisibleLength(glyphWidths, visibleStart, textAreaWidth);
            string visibleInputText = visibleLength > 0
                ? inputText.Substring(visibleStart, visibleLength)
                : string.Empty;
            Vector2 drawPosition = new(inputBounds.X + 4, inputBounds.Y - 1);
            if (!string.IsNullOrEmpty(visibleInputText))
            {
                if (inputTextRasterizer != null)
                {
                    inputTextRasterizer.DrawString(_spriteBatch, visibleInputText, drawPosition, Color.Black, 1f, textAreaWidth);
                }
                else
                {
                    ClientTextDrawing.Draw(
                        _spriteBatch,
                        visibleInputText,
                        drawPosition,
                        Color.Black,
                        InitialQuizOwnerInputTextScale,
                        _fontChat,
                        textAreaWidth);
                }
            }

            bool cursorVisible = inputFocused && ShouldDrawInitialQuizOwnerCursor(currentTickCount, _initialQuizOwnerCursorBlinkStartedAt);
            if (!cursorVisible)
            {
                return;
            }

            int cursorX = inputBounds.X + 4 + SumInitialQuizOwnerGlyphWidths(
                glyphWidths,
                visibleStart,
                Math.Clamp(_initialQuizOwnerCursorIndex, visibleStart, inputText.Length) - visibleStart);
            Rectangle cursorBounds = new(cursorX, inputBounds.Y + 2, 1, Math.Max(9, inputBounds.Height - 4));
            _spriteBatch.Draw(_packetScriptOwnerPixelTexture, cursorBounds, Color.Black);
        }

        private int ResolveInitialQuizOwnerCursorIndexFromClick(string inputText, Rectangle inputBounds, int mouseX)
        {
            int textAreaWidth = Math.Max(0, inputBounds.Width - 8);
            int[] glyphWidths = MeasureInitialQuizOwnerInputGlyphWidths(inputText);
            int visibleStart = ResolveInitialQuizOwnerVisibleStart(glyphWidths, _initialQuizOwnerCursorIndex, textAreaWidth);
            int relativeX = Math.Max(0, mouseX - (inputBounds.X + 4));
            return ResolveInitialQuizOwnerCursorIndexFromRelativeX(glyphWidths, visibleStart, textAreaWidth, relativeX);
        }

        private int[] MeasureInitialQuizOwnerInputGlyphWidths(string inputText)
        {
            if (string.IsNullOrEmpty(inputText))
            {
                return Array.Empty<int>();
            }

            ClientTextRasterizer inputTextRasterizer = EnsureInitialQuizOwnerInputTextRasterizer();
            int[] glyphWidths = new int[inputText.Length];
            for (int i = 0; i < inputText.Length; i++)
            {
                Vector2 glyphSize = inputTextRasterizer != null
                    ? inputTextRasterizer.MeasureString(inputText[i].ToString())
                    : ClientTextDrawing.Measure(
                        GraphicsDevice,
                        inputText[i].ToString(),
                        InitialQuizOwnerInputTextScale,
                        _fontChat);
                glyphWidths[i] = Math.Max(1, (int)Math.Ceiling(glyphSize.X));
            }

            return glyphWidths;
        }

        private ClientTextRasterizer EnsureInitialQuizOwnerInputTextRasterizer()
        {
            if (_initialQuizOwnerInputTextRasterizer != null || GraphicsDevice == null)
            {
                return _initialQuizOwnerInputTextRasterizer;
            }

            try
            {
                string requestedFontFamily = MapleStoryStringPool.GetOrFallback(AntiMacroEditControl.ClientFontStringPoolId, "Arial");
                string resolvedFontFamily = ClientTextRasterizer.ResolvePreferredFontFamily(
                    requestedFontFamily,
                    preferredPrivateFontFamilyCandidates: InitialQuizOwnerInputFontFamilyCandidates,
                    preferEmbeddedPrivateFontSources: true);
                _initialQuizOwnerInputTextRasterizer = new ClientTextRasterizer(
                    GraphicsDevice,
                    resolvedFontFamily,
                    basePointSize: InitialQuizOwnerInputFontHeightPixels,
                    preferEmbeddedPrivateFontSources: true);
            }
            catch
            {
                _initialQuizOwnerInputTextRasterizer = null;
            }

            return _initialQuizOwnerInputTextRasterizer;
        }

        private AntiMacroEditControl EnsureInitialQuizOwnerEditControl()
        {
            if (_initialQuizOwnerEditControl != null || _packetScriptOwnerPixelTexture == null)
            {
                return _initialQuizOwnerEditControl;
            }

            _initialQuizOwnerEditControl = new AntiMacroEditControl(
                _packetScriptOwnerPixelTexture,
                new Point(109, 157),
                150,
                13,
                InitialQuizOwnerInputMaxLength);
            _initialQuizOwnerEditControl.SetFont(_fontChat);
            _initialQuizOwnerEditControl.UseClientAntiMacroVisualStyle();
            _initialQuizOwnerEditControl.SetFocus(_initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.Input);
            return _initialQuizOwnerEditControl;
        }

        private void SyncInitialQuizOwnerLegacyInputStateFromEditControl()
        {
            if (_initialQuizOwnerEditControl == null)
            {
                return;
            }

            string text = _initialQuizOwnerEditControl.Text ?? string.Empty;
            _initialQuizOwnerInput.Clear();
            _initialQuizOwnerInput.Append(text);
            _initialQuizOwnerCursorIndex = text.Length;
        }

        private string GetInitialQuizOwnerSubmittedText()
        {
            if (_initialQuizOwnerEditControl != null)
            {
                SyncInitialQuizOwnerLegacyInputStateFromEditControl();
                return _initialQuizOwnerEditControl.Text ?? string.Empty;
            }

            return _initialQuizOwnerInput.ToString();
        }

        internal static bool ShouldCaptureInitialQuizOwnerTextInput(bool ownerActive, int remainingMs, InitialQuizOwnerFocusTarget focusTarget)
        {
            return ownerActive
                && remainingMs > 0
                && focusTarget == InitialQuizOwnerFocusTarget.Input;
        }

        private bool ShouldCaptureInitialQuizOwnerTextInput()
        {
            return _initialQuizTimerRuntime.TryBuildOwnerSnapshot(currTickCount, out InitialQuizOwnerSnapshot snapshot)
                && ShouldCaptureInitialQuizOwnerTextInput(true, snapshot.RemainingMs, _initialQuizOwnerFocusTarget);
        }

        private bool DoesInitialQuizOwnerCaptureWindowInput()
        {
            return _initialQuizTimerRuntime.TryBuildOwnerSnapshot(currTickCount, out _);
        }

        internal static bool ShouldForwardInitialQuizOwnerInputToActiveWindow(bool ownerCapturesWindowInput)
        {
            return !ownerCapturesWindowInput;
        }

        internal static bool ShouldInitialQuizOwnerOverrideNpcOverlayInput(bool ownerCapturesWindowInput)
        {
            return ownerCapturesWindowInput;
        }

        internal static bool ShouldBlockInitialQuizOwnerInputForNpcOverlayModal(bool ownerCapturesWindowInput, bool npcOverlayBlocksUnderlyingInput)
        {
            return !ShouldInitialQuizOwnerOverrideNpcOverlayInput(ownerCapturesWindowInput)
                && npcOverlayBlocksUnderlyingInput;
        }

        private void HandleInitialQuizOwnerCommittedText(string text)
        {
            if (!ShouldCaptureInitialQuizOwnerTextInput())
            {
                return;
            }

            AntiMacroEditControl editControl = EnsureInitialQuizOwnerEditControl();
            editControl?.HandleCommittedText(text, capturesKeyboardInput: true);
            SyncInitialQuizOwnerLegacyInputStateFromEditControl();
        }

        private void HandleInitialQuizOwnerCompositionText(string compositionText)
        {
            AntiMacroEditControl editControl = EnsureInitialQuizOwnerEditControl();
            if (editControl == null)
            {
                return;
            }

            editControl.HandleCompositionText(compositionText, ShouldCaptureInitialQuizOwnerTextInput());
            SyncInitialQuizOwnerLegacyInputStateFromEditControl();
        }

        private void HandleInitialQuizOwnerCompositionState(ImeCompositionState compositionState)
        {
            AntiMacroEditControl editControl = EnsureInitialQuizOwnerEditControl();
            if (editControl == null)
            {
                return;
            }

            editControl.HandleCompositionState(compositionState ?? ImeCompositionState.Empty, ShouldCaptureInitialQuizOwnerTextInput());
            SyncInitialQuizOwnerLegacyInputStateFromEditControl();
        }

        private void HandleInitialQuizOwnerImeCandidateList(ImeCandidateListState candidateState)
        {
            AntiMacroEditControl editControl = EnsureInitialQuizOwnerEditControl();
            if (editControl == null)
            {
                return;
            }

            editControl.HandleImeCandidateList(candidateState, ShouldCaptureInitialQuizOwnerTextInput());
        }

        private void ClearInitialQuizOwnerCompositionText()
        {
            _initialQuizOwnerEditControl?.ClearCompositionText();
        }

        private void ClearInitialQuizOwnerImeCandidateList()
        {
            _initialQuizOwnerEditControl?.ClearImeCandidateList();
        }

        private bool TrySelectInitialQuizOwnerImeCandidateFromMouse(AntiMacroEditControl editControl, Rectangle ownerBounds, Point cursor)
        {
            if (editControl == null)
            {
                return false;
            }

            int candidateIndex = editControl.ResolveImeCandidateIndexFromMouse(ownerBounds, cursor.X, cursor.Y);
            if (candidateIndex < 0)
            {
                return false;
            }

            return TrySelectInitialQuizImeCandidate(editControl.CandidateListState?.ListIndex ?? -1, candidateIndex);
        }

        private bool TryDispatchInitialQuizOwnerImeCandidateSelection(
            ImeCandidateListState candidateState,
            KeyboardState newKeyboardState,
            KeyboardState oldKeyboardState)
        {
            return TryResolveInitialQuizOwnerImeCandidateSelection(
                       candidateState,
                       newKeyboardState,
                       oldKeyboardState,
                       out int listIndex,
                       out int candidateIndex)
                   && TrySelectInitialQuizImeCandidate(listIndex, candidateIndex);
        }

        internal static bool TryResolveInitialQuizOwnerImeCandidateSelection(
            ImeCandidateListState candidateState,
            KeyboardState newKeyboardState,
            KeyboardState oldKeyboardState,
            out int listIndex,
            out int candidateIndex)
        {
            listIndex = candidateState?.ListIndex ?? -1;
            candidateIndex = -1;
            if (candidateState == null || !candidateState.HasCandidates || listIndex < 0)
            {
                return false;
            }

            bool WasPressed(KeyboardState currentState, Keys key)
            {
                return currentState.IsKeyDown(key) && oldKeyboardState.IsKeyUp(key);
            }

            candidateIndex = SkillMacroImeCandidateWindowLayout.ResolveVisibleCandidateIndexFromKeyboard(
                candidateState,
                newKeyboardState,
                WasPressed);
            if (candidateIndex < 0)
            {
                candidateIndex = SkillMacroImeCandidateWindowLayout.ResolveAdjacentCandidateIndexFromKeyboard(
                    candidateState,
                    newKeyboardState,
                    WasPressed);
            }

            return candidateIndex >= 0;
        }

        private bool TrySelectInitialQuizImeCandidate(int listIndex, int candidateIndex)
        {
            return Window != null
                && WindowsImeCandidateSelectionBridge.TrySelectCandidate(Window.Handle, listIndex, candidateIndex);
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

        private InitialQuizButtonFrame ResolveInitialQuizOwnerOkButtonFrame(bool enabled)
        {
            InitialQuizOwnerButtonVisualState state = ResolveInitialQuizOwnerButtonVisualState(
                enabled,
                _initialQuizOwnerPressedOkButton,
                _initialQuizOwnerHoveringOkButton,
                _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.OkButton);

            return state switch
            {
                InitialQuizOwnerButtonVisualState.Disabled => _initialQuizOwnerOkButtonDisabledFrame ?? _initialQuizOwnerOkButtonNormalFrame,
                InitialQuizOwnerButtonVisualState.Pressed => _initialQuizOwnerOkButtonPressedFrame ?? _initialQuizOwnerOkButtonHoverFrame ?? _initialQuizOwnerOkButtonKeyFocusedFrame ?? _initialQuizOwnerOkButtonNormalFrame,
                InitialQuizOwnerButtonVisualState.Hover => _initialQuizOwnerOkButtonHoverFrame ?? _initialQuizOwnerOkButtonKeyFocusedFrame ?? _initialQuizOwnerOkButtonNormalFrame,
                InitialQuizOwnerButtonVisualState.KeyFocused => _initialQuizOwnerOkButtonKeyFocusedFrame ?? _initialQuizOwnerOkButtonHoverFrame ?? _initialQuizOwnerOkButtonNormalFrame,
                _ => _initialQuizOwnerOkButtonNormalFrame
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
            return new Rectangle(
                ownerBounds.X + InitialQuizOwnerOkButtonLeft,
                ownerBounds.Y + InitialQuizOwnerOkButtonTop,
                InitialQuizOwnerOkButtonWidth,
                InitialQuizOwnerOkButtonHeight);
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
            WzSubProperty okButtonProperty = ResolveInitialQuizOwnerOkButtonProperty(preferred, fallback);

            _initialQuizOwnerBackgroundTexture = LoadUiCanvasTexture(
                ResolveInitialQuizOwnerCanvasFromStringPool(InitialQuizBackgroundUolStringPoolId)
                ?? (preferred?["backgrnd"] ?? fallback?["backgrnd"]) as WzCanvasProperty);
            _initialQuizOwnerBackgroundTexture2 = LoadUiCanvasTexture((preferred?["backgrnd2"] ?? fallback?["backgrnd2"]) as WzCanvasProperty);
            _initialQuizOwnerBackgroundTexture3 = LoadUiCanvasTexture(preferredBackground3 ?? fallbackBackground3);
            _initialQuizOwnerOkButtonNormalFrame = LoadInitialQuizOwnerButtonFrame(okButtonProperty, "normal");
            _initialQuizOwnerOkButtonHoverFrame = LoadInitialQuizOwnerButtonFrame(okButtonProperty, "mouseOver");
            _initialQuizOwnerOkButtonPressedFrame = LoadInitialQuizOwnerButtonFrame(okButtonProperty, "pressed");
            _initialQuizOwnerOkButtonDisabledFrame = LoadInitialQuizOwnerButtonFrame(okButtonProperty, "disabled");
            _initialQuizOwnerOkButtonKeyFocusedFrame = LoadInitialQuizOwnerButtonFrame(okButtonProperty, "keyFocused");
            _initialQuizOwnerDigits = LoadInitialQuizOwnerDigits(preferred?["num1"] as WzSubProperty, fallback?["num1"] as WzSubProperty, out _initialQuizOwnerCommaTexture);
            _initialQuizOwnerAnimationFrames = LoadInitialQuizOwnerAnimationFrames(preferred?["ani"] as WzSubProperty, fallback?["ani"] as WzSubProperty);
            _initialQuizOwnerBackground3Origin = ResolveCanvasOrigin(preferredBackground3 ?? fallbackBackground3);
        }

        private Texture2D[] LoadInitialQuizOwnerDigits(WzSubProperty preferred, WzSubProperty fallback, out Texture2D commaTexture)
        {
            Texture2D[] digits = new Texture2D[10];
            for (int i = 0; i < digits.Length; i++)
            {
                digits[i] = LoadUiCanvasTexture(
                    ResolveInitialQuizOwnerTimerDigitCanvas(i)
                    ?? (preferred?[i.ToString()] ?? fallback?[i.ToString()]) as WzCanvasProperty);
            }

            commaTexture = LoadUiCanvasTexture(
                ResolveInitialQuizOwnerCanvasFromStringPool(InitialQuizTimerCommaUolStringPoolId)
                ?? (preferred?["comma"] ?? fallback?["comma"]) as WzCanvasProperty);
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

        private InitialQuizButtonFrame LoadInitialQuizOwnerButtonFrame(WzSubProperty buttonProperty, string stateName)
        {
            WzCanvasProperty canvas = ResolveInitialQuizOwnerButtonCanvas(buttonProperty, stateName);
            Texture2D texture = LoadUiCanvasTexture(canvas);
            return texture == null
                ? null
                : new InitialQuizButtonFrame(texture, ResolveInitialQuizOwnerButtonOrigin(canvas));
        }

        private static WzSubProperty ResolveInitialQuizOwnerOkButtonProperty(WzSubProperty preferred, WzSubProperty fallback)
        {
            if (MapleStoryStringPool.TryGet(InitialQuizOkButtonUolStringPoolId, out string okButtonUol)
                && TryResolveInitialQuizOwnerUiSubPropertyPath(okButtonUol, out WzSubProperty stringPoolButton))
            {
                return stringPoolButton;
            }

            return preferred?["BtOK"] as WzSubProperty
                ?? fallback?["BtOK"] as WzSubProperty;
        }

        internal static bool TryDecodeInitialQuizOwnerUiResourcePath(
            string resourcePath,
            out string imageName,
            out string propertyPath)
        {
            imageName = null;
            propertyPath = null;
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return false;
            }

            string normalized = resourcePath.Trim().Replace('\\', '/');
            const string categoryPrefix = "UI/";
            if (normalized.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[categoryPrefix.Length..];
            }

            int separatorIndex = normalized.IndexOf('/');
            if (separatorIndex <= 0 || separatorIndex >= normalized.Length - 1)
            {
                return false;
            }

            imageName = normalized[..separatorIndex];
            propertyPath = normalized[(separatorIndex + 1)..];
            return !string.IsNullOrWhiteSpace(imageName) && !string.IsNullOrWhiteSpace(propertyPath);
        }

        private static bool TryResolveInitialQuizOwnerUiSubPropertyPath(string resourcePath, out WzSubProperty property)
        {
            property = null;
            if (!TryDecodeInitialQuizOwnerUiResourcePath(resourcePath, out string imageName, out string propertyPath))
            {
                return false;
            }

            WzImage image = Program.FindImage("UI", imageName.Trim());
            property = image?[propertyPath.Trim()] as WzSubProperty;
            return property != null;
        }

        private static WzCanvasProperty ResolveInitialQuizOwnerTimerDigitCanvas(int digit)
        {
            return ResolveInitialQuizOwnerCanvasFromStringPool(InitialQuizTimerDigitUolStringPoolId, digit);
        }

        private static WzCanvasProperty ResolveInitialQuizOwnerCanvasFromStringPool(int stringPoolId, int? formatArgument = null)
        {
            return TryResolveInitialQuizOwnerStringPoolResourcePath(stringPoolId, formatArgument, out string resourcePath)
                && TryResolveInitialQuizOwnerUiCanvasPath(resourcePath, out WzCanvasProperty canvas)
                    ? canvas
                    : null;
        }

        internal static bool TryResolveInitialQuizOwnerStringPoolResourcePath(int stringPoolId, int? formatArgument, out string resourcePath)
        {
            resourcePath = null;
            if (!MapleStoryStringPool.TryGet(stringPoolId, out string template) || string.IsNullOrWhiteSpace(template))
            {
                return false;
            }

            resourcePath = formatArgument.HasValue
                ? template.Replace("%d", formatArgument.Value.ToString(), StringComparison.Ordinal)
                : template;
            return !string.IsNullOrWhiteSpace(resourcePath);
        }

        private static bool TryResolveInitialQuizOwnerUiCanvasPath(string resourcePath, out WzCanvasProperty canvas)
        {
            canvas = null;
            if (!TryDecodeInitialQuizOwnerUiResourcePath(resourcePath, out string imageName, out string propertyPath))
            {
                return false;
            }

            WzImage image = Program.FindImage("UI", imageName.Trim());
            canvas = image?[propertyPath.Trim()] as WzCanvasProperty;
            return canvas != null;
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

        private static Point ResolveInitialQuizOwnerButtonOrigin(WzCanvasProperty canvas)
        {
            Point origin = ResolveCanvasOrigin(canvas);
            return origin.X < 0 || origin.Y < 0
                ? Point.Zero
                : origin;
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

        internal static InitialQuizOwnerTimeoutBehavior ResolveInitialQuizOwnerTimeoutBehavior(
            int remainingMs,
            bool resultSent,
            bool timeoutCloseArmed)
        {
            if (resultSent || remainingMs > 0)
            {
                return InitialQuizOwnerTimeoutBehavior.Wait;
            }

            return timeoutCloseArmed
                ? InitialQuizOwnerTimeoutBehavior.SubmitAndClose
                : InitialQuizOwnerTimeoutBehavior.ArmClose;
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

        internal static bool ShouldSubmitInitialQuizOwnerOkButtonRelease(bool pressedOkButton, bool hoveringOkButton, bool enabled)
        {
            return enabled && pressedOkButton && hoveringOkButton;
        }

        internal static bool ShouldDrawInitialQuizOwnerCursor(int currentTickCount, int cursorBlinkStartedAt)
        {
            return ((currentTickCount - cursorBlinkStartedAt) / 500) % 2 == 0;
        }

        internal static int ResolveInitialQuizOwnerVisibleStart(IReadOnlyList<int> glyphWidths, int cursorIndex, int maxWidth)
        {
            if (glyphWidths == null || glyphWidths.Count == 0 || maxWidth <= 0)
            {
                return 0;
            }

            int clampedCursorIndex = Math.Clamp(cursorIndex, 0, glyphWidths.Count);
            int start = clampedCursorIndex;
            int width = 0;
            while (start > 0)
            {
                int nextWidth = width + Math.Max(1, glyphWidths[start - 1]);
                if (nextWidth > maxWidth)
                {
                    break;
                }

                start--;
                width = nextWidth;
            }

            return start;
        }

        internal static int ResolveInitialQuizOwnerVisibleLength(IReadOnlyList<int> glyphWidths, int visibleStart, int maxWidth)
        {
            if (glyphWidths == null || glyphWidths.Count == 0 || maxWidth <= 0)
            {
                return 0;
            }

            int start = Math.Clamp(visibleStart, 0, glyphWidths.Count);
            int width = 0;
            int end = start;
            while (end < glyphWidths.Count)
            {
                int nextWidth = width + Math.Max(1, glyphWidths[end]);
                if (nextWidth > maxWidth)
                {
                    break;
                }

                width = nextWidth;
                end++;
            }

            return end - start;
        }

        internal static int ResolveInitialQuizOwnerCursorIndexFromRelativeX(
            IReadOnlyList<int> glyphWidths,
            int visibleStart,
            int maxWidth,
            int relativeX)
        {
            if (glyphWidths == null || glyphWidths.Count == 0)
            {
                return 0;
            }

            int start = Math.Clamp(visibleStart, 0, glyphWidths.Count);
            int visibleLength = ResolveInitialQuizOwnerVisibleLength(glyphWidths, start, maxWidth);
            int clampedX = Math.Max(0, relativeX);
            int offset = 0;
            for (int i = 0; i < visibleLength; i++)
            {
                int glyphWidth = Math.Max(1, glyphWidths[start + i]);
                if (clampedX < offset + (glyphWidth / 2))
                {
                    return start + i;
                }

                offset += glyphWidth;
                if (clampedX < offset)
                {
                    return start + i + 1;
                }
            }

            return start + visibleLength;
        }

        internal static int SumInitialQuizOwnerGlyphWidths(IReadOnlyList<int> glyphWidths, int start, int count)
        {
            if (glyphWidths == null || glyphWidths.Count == 0 || count <= 0)
            {
                return 0;
            }

            int begin = Math.Clamp(start, 0, glyphWidths.Count);
            int end = Math.Clamp(begin + count, begin, glyphWidths.Count);
            int total = 0;
            for (int i = begin; i < end; i++)
            {
                total += Math.Max(1, glyphWidths[i]);
            }

            return total;
        }

        internal static InitialQuizOwnerSubmissionValidation ValidateInitialQuizOwnerSubmission(
            string answerText,
            int minInputByteLength,
            int maxInputByteLength,
            bool validateAnswer)
        {
            if (!validateAnswer)
            {
                return InitialQuizOwnerSubmissionValidation.Accepted;
            }

            if (string.IsNullOrWhiteSpace(answerText))
            {
                return new InitialQuizOwnerSubmissionValidation(false, null, true);
            }

            int inputByteLength = Encoding.Default.GetByteCount(answerText ?? string.Empty);
            int minimum = Math.Max(0, minInputByteLength);
            if (inputByteLength < minimum)
            {
                return new InitialQuizOwnerSubmissionValidation(
                    false,
                    FormatInitialQuizOwnerInputLengthNotice(
                        InitialQuizMinInputNoticeStringPoolId,
                        "You must enter atleast {0} letters. (Korean)",
                        minimum),
                    true);
            }

            int maximum = Math.Max(0, maxInputByteLength);
            if (inputByteLength > maximum)
            {
                return new InitialQuizOwnerSubmissionValidation(
                    false,
                    FormatInitialQuizOwnerInputLengthNotice(
                        InitialQuizMaxInputNoticeStringPoolId,
                        "You must enter less than {0} letters. (Korean)",
                        maximum),
                    true);
            }

            return InitialQuizOwnerSubmissionValidation.Accepted;
        }

        private static string FormatInitialQuizOwnerInputLengthNotice(int stringPoolId, string fallbackFormat, int byteLength)
        {
            string format = MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackFormat);
            return string.Format(format.Replace("%d", "{0}", StringComparison.Ordinal), byteLength);
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
            if (_spriteBatch == null || bounds == Rectangle.Empty)
            {
                return;
            }

            string displayText = NormalizeInitialQuizOwnerSingleLineText(text);
            if (string.IsNullOrEmpty(displayText))
            {
                return;
            }

            ClientTextDrawing.Draw(
                _spriteBatch,
                displayText,
                new Vector2(bounds.X, bounds.Y),
                color,
                scale,
                _fontChat,
                bounds.Width);
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

        internal sealed record InitialQuizOwnerSubmissionValidation(bool CanSubmit, string NoticeMessage, bool RefocusInput)
        {
            internal static InitialQuizOwnerSubmissionValidation Accepted { get; } = new(true, null, false);
        }

        internal enum InitialQuizOwnerTimeoutBehavior
        {
            Wait,
            ArmClose,
            SubmitAndClose
        }
    }
}
