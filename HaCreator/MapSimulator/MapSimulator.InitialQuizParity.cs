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
        private static readonly Point InitialQuizOwnerEditOrigin = new(109, 157);

        private bool _initialQuizOwnerVisualsLoaded;
        private ClientTextRasterizer _initialQuizOwnerInputTextRasterizer;
        private AntiMacroEditControl _initialQuizOwnerEditControl;
        private NativeAntiMacroEditHost _initialQuizOwnerNativeEditHost;
        private Texture2D _initialQuizOwnerBackgroundTexture;
        private Texture2D _initialQuizOwnerBackgroundTexture2;
        private Texture2D _initialQuizOwnerBackgroundTexture3;
        private int _initialQuizOwnerBackgroundZ;
        private int _initialQuizOwnerBackground2Z;
        private int _initialQuizOwnerBackground3Z;
        private InitialQuizButtonFrame _initialQuizOwnerOkButtonNormalFrame;
        private InitialQuizButtonFrame _initialQuizOwnerOkButtonHoverFrame;
        private InitialQuizButtonFrame _initialQuizOwnerOkButtonPressedFrame;
        private InitialQuizButtonFrame _initialQuizOwnerOkButtonDisabledFrame;
        private InitialQuizButtonFrame _initialQuizOwnerOkButtonKeyFocusedFrame;
        private Texture2D[] _initialQuizOwnerDigits;
        private Texture2D _initialQuizOwnerCommaTexture;
        private InitialQuizAnimationFrame[] _initialQuizOwnerAnimationFrames = Array.Empty<InitialQuizAnimationFrame>();
        private Point _initialQuizOwnerBackgroundOrigin = Point.Zero;
        private Point _initialQuizOwnerBackground2Origin = Point.Zero;
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
        private int _initialQuizOwnerDisplayedRemainingSeconds;
        private bool _initialQuizOwnerHasDisplayedRemainingSeconds;
        private int _initialQuizOwnerEditTextElementLimit = InitialQuizOwnerInputMaxLength;
        private InitialQuizOwnerFocusTarget _initialQuizOwnerFocusTarget = InitialQuizOwnerFocusTarget.Input;
        private InitialQuizOwnerCaptureState _initialQuizOwnerCaptureState = InitialQuizOwnerCaptureState.None;
        private InitialQuizOwnerChildControlState _initialQuizOwnerChildControlState = InitialQuizOwnerChildControlState.Inactive;
        private bool UsingInitialQuizOwnerNativeEditHost => _initialQuizOwnerNativeEditHost?.IsAttached == true;

        private sealed record InitialQuizAnimationFrame(Texture2D Texture, int DelayMs);
        private sealed record InitialQuizButtonFrame(Texture2D Texture, Point Origin);
        internal readonly record struct InitialQuizOwnerLayerOrder(InitialQuizOwnerLayerKind Layer, int Z);
        internal enum InitialQuizOwnerFocusTarget
        {
            Owner,
            Input,
            OkButton
        }

        internal enum InitialQuizOwnerLayerKind
        {
            Backgrnd,
            Backgrnd2,
            Backgrnd3
        }

        internal enum InitialQuizOwnerButtonVisualState
        {
            Normal,
            Hover,
            Pressed,
            Disabled,
            KeyFocused
        }

        internal enum InitialQuizOwnerCaptureState
        {
            None,
            OwnerOnly,
            OwnerWithEditFocus
        }

        internal readonly record struct InitialQuizOwnerChildControlState(bool EditVisible, bool EditEnabled, bool OkButtonEnabled)
        {
            internal static InitialQuizOwnerChildControlState Active { get; } = new(true, true, true);
            internal static InitialQuizOwnerChildControlState Inactive { get; } = new(false, false, false);
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
            _initialQuizOwnerEditTextElementLimit = ResolveInitialQuizOwnerEditTextElementLimit(
                _initialQuizTimerRuntime.TryBuildOwnerSnapshot(currentTickCount, out InitialQuizOwnerSnapshot currentSnapshot)
                    ? currentSnapshot.MaxInputByteLength
                    : 0);
            DestroyInitialQuizOwnerControlStack();
            EnsureInitialQuizOwnerControlStackCreated();
            ResetInitialQuizOwnerHeldEditKey();
            if (_initialQuizTimerRuntime.TryBuildOwnerSnapshot(currentTickCount, out InitialQuizOwnerSnapshot snapshot))
            {
                _initialQuizOwnerDisplayedRemainingSeconds = Math.Max(0, snapshot.RemainingSeconds);
                _initialQuizOwnerHasDisplayedRemainingSeconds = true;
            }
            else
            {
                _initialQuizOwnerDisplayedRemainingSeconds = 0;
                _initialQuizOwnerHasDisplayedRemainingSeconds = false;
            }

            _initialQuizOwnerChildControlState = InitialQuizOwnerChildControlState.Active;
            _initialQuizOwnerCaptureState = ResolveInitialQuizOwnerCaptureState(
                ownerActive: true,
                _initialQuizOwnerFocusTarget);
            SyncInitialQuizOwnerEditControlState(ownerActive: true, _initialQuizOwnerChildControlState);
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
            _initialQuizOwnerEditTextElementLimit = InitialQuizOwnerInputMaxLength;
            DestroyInitialQuizOwnerControlStack();
            ClearInitialQuizOwnerCompositionText();
            ClearInitialQuizOwnerImeCandidateList();
            ResetInitialQuizOwnerHeldEditKey();
            _initialQuizOwnerDisplayedRemainingSeconds = 0;
            _initialQuizOwnerHasDisplayedRemainingSeconds = false;
            _initialQuizOwnerChildControlState = InitialQuizOwnerChildControlState.Inactive;
            _initialQuizOwnerCaptureState = InitialQuizOwnerCaptureState.None;
        }

        private void UpdateInitialQuizOwner(int currentTickCount)
        {
            if (!_initialQuizTimerRuntime.TryBuildOwnerSnapshot(currentTickCount, out InitialQuizOwnerSnapshot snapshot))
            {
                ClearInitialQuizOwnerInputState();
                return;
            }

            _initialQuizOwnerCaptureState = ResolveInitialQuizOwnerCaptureState(
                ownerActive: true,
                _initialQuizOwnerFocusTarget);
            _initialQuizOwnerChildControlState = ResolveInitialQuizOwnerChildControlState(snapshot.RemainingSeconds);
            SyncInitialQuizOwnerEditControlState(ownerActive: true, _initialQuizOwnerChildControlState);
            _initialQuizOwnerDisplayedRemainingSeconds = ResolveInitialQuizOwnerDisplayedRemainingSeconds(
                _initialQuizOwnerHasDisplayedRemainingSeconds
                    ? _initialQuizOwnerDisplayedRemainingSeconds
                    : null,
                snapshot.RemainingSeconds);
            _initialQuizOwnerHasDisplayedRemainingSeconds = true;

            InitialQuizOwnerTimeoutBehavior timeoutBehavior = ResolveInitialQuizOwnerTimeoutBehavior(
                snapshot.RemainingSeconds,
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
                SetInitialQuizOwnerFocusTarget(InitialQuizOwnerFocusTarget.Owner);
                EnsureInitialQuizOwnerEditControl()?.EndMouseSelection();
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

            EnsureInitialQuizOwnerVisualsLoaded();
            Rectangle ownerBounds = ResolveInitialQuizOwnerBounds();
            Rectangle okButtonBounds = ResolveInitialQuizOwnerOkButtonBounds(ownerBounds);
            Rectangle inputBounds = ResolveInitialQuizOwnerInputBounds(ownerBounds);
            Point cursor = new(mouseState.X, mouseState.Y);
            InitialQuizOwnerChildControlState controlState = ResolveInitialQuizOwnerChildControlState(snapshot.RemainingSeconds);
            bool showInput = controlState.EditVisible && controlState.EditEnabled;
            bool cursorInInput = inputBounds.Contains(cursor);
            bool okButtonEnabled = controlState.OkButtonEnabled;
            _initialQuizOwnerHoveringOkButton = showInput && okButtonEnabled && okButtonBounds.Contains(cursor);
            NativeAntiMacroEditHost nativeEditHost = EnsureInitialQuizOwnerNativeEditHost();
            AntiMacroEditControl editControl = UsingInitialQuizOwnerNativeEditHost
                ? null
                : EnsureInitialQuizOwnerEditControl();

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

                InitialQuizOwnerFocusTarget nextFocusTarget = ResolveInitialQuizOwnerMousePressFocusTarget(
                    showInput,
                    _initialQuizOwnerHoveringOkButton,
                    cursorInInput);
                SetInitialQuizOwnerFocusTarget(nextFocusTarget);
                _initialQuizOwnerPressedOkButton = showInput
                    && okButtonEnabled
                    && nextFocusTarget == InitialQuizOwnerFocusTarget.OkButton;
                if (nextFocusTarget == InitialQuizOwnerFocusTarget.Input)
                {
                    if (UsingInitialQuizOwnerNativeEditHost && nativeEditHost != null)
                    {
                        nativeEditHost.BeginSelectionAtPoint(cursor);
                        SyncInitialQuizOwnerLegacyInputStateFromEditControl();
                    }
                    else if (editControl != null)
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

                if (showInput)
                {
                    _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
                }
            }
            if (justReleased)
            {
                nativeEditHost?.EndMouseSelection();
                editControl?.EndMouseSelection();
                bool confirm = ShouldSubmitInitialQuizOwnerOkButtonRelease(
                    _initialQuizOwnerPressedOkButton,
                    _initialQuizOwnerHoveringOkButton,
                    showInput && okButtonEnabled);
                _initialQuizOwnerPressedOkButton = false;
                if (confirm)
                {
                    SubmitInitialQuizOwnerResult(GetInitialQuizOwnerSubmittedText(), currentTickCount, showFeedback: true);
                }
            }
            else if (leftPressed && showInput && _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.Input)
            {
                if (UsingInitialQuizOwnerNativeEditHost && nativeEditHost?.IsSelectingWithMouse == true)
                {
                    nativeEditHost.UpdateSelectionAtPoint(cursor);
                    SyncInitialQuizOwnerLegacyInputStateFromEditControl();
                }
                else if (editControl?.IsSelectingWithMouse == true)
                {
                    editControl.UpdateSelectionAtMouseX(cursor.X, ownerBounds);
                    SyncInitialQuizOwnerLegacyInputStateFromEditControl();
                }
            }
            else if (!leftPressed)
            {
                nativeEditHost?.EndMouseSelection();
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

            if (ShouldSwallowInitialQuizOwnerCancelKey(newKeyboardState, oldKeyboardState))
            {
                // `CUIInitialQuiz::SetRet` returns immediately for ret=2, so cancel is swallowed.
                return true;
            }

            if (snapshot.RemainingSeconds <= 0)
            {
                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Tab) && oldKeyboardState.IsKeyUp(Keys.Tab))
            {
                SetInitialQuizOwnerFocusTarget(ResolveNextInitialQuizOwnerFocusTarget(_initialQuizOwnerFocusTarget));
                _initialQuizOwnerPressedOkButton = false;
                _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
                return true;
            }

            bool inputFocused = _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.Input;
            bool buttonFocused = _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.OkButton;
            InitialQuizOwnerChildControlState controlState = ResolveInitialQuizOwnerChildControlState(snapshot.RemainingSeconds);

            if (newKeyboardState.IsKeyDown(Keys.Enter) && oldKeyboardState.IsKeyUp(Keys.Enter))
            {
                if (!controlState.OkButtonEnabled)
                {
                    return true;
                }

                SubmitInitialQuizOwnerResult(GetInitialQuizOwnerSubmittedText(), currentTickCount, showFeedback: true);
                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Space) && oldKeyboardState.IsKeyUp(Keys.Space) && buttonFocused)
            {
                if (!controlState.OkButtonEnabled)
                {
                    return true;
                }

                SubmitInitialQuizOwnerResult(GetInitialQuizOwnerSubmittedText(), currentTickCount, showFeedback: true);
                return true;
            }

            if (!inputFocused)
            {
                _initialQuizOwnerNativeEditHost?.Blur();
                EnsureInitialQuizOwnerEditControl()?.SetFocus(false);
                ResetInitialQuizOwnerHeldEditKey();
                return true;
            }

            if (UsingInitialQuizOwnerNativeEditHost)
            {
                _initialQuizOwnerNativeEditHost?.SynchronizeState();
                ResetInitialQuizOwnerHeldEditKey();
                return true;
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
                    SetInitialQuizOwnerFocusTarget(InitialQuizOwnerFocusTarget.Input);
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

            foreach (InitialQuizOwnerLayerOrder layer in ResolveInitialQuizOwnerLayerDrawOrder(
                         hasBackgrnd: _initialQuizOwnerBackgroundTexture != null,
                         backgrndZ: _initialQuizOwnerBackgroundZ,
                         hasBackgrnd2: _initialQuizOwnerBackgroundTexture2 != null,
                         backgrnd2Z: _initialQuizOwnerBackground2Z,
                         hasBackgrnd3: _initialQuizOwnerBackgroundTexture3 != null,
                         backgrnd3Z: _initialQuizOwnerBackground3Z))
            {
                DrawInitialQuizOwnerLayer(layer.Layer, ownerBounds, overlayBounds);
            }

            int displayedRemainingSeconds = _initialQuizOwnerHasDisplayedRemainingSeconds
                ? _initialQuizOwnerDisplayedRemainingSeconds
                : snapshot.RemainingSeconds;
            DrawInitialQuizOwnerTimerDigits(ownerBounds, displayedRemainingSeconds);
            DrawInitialQuizOwnerAnimationFrame(ownerBounds, currentTickCount);

            DrawInitialQuizOwnerSingleLineText(
                snapshot.Title,
                new Rectangle(ownerBounds.X + 30, ownerBounds.Y + 84, 190, 18),
                Color.White,
                InitialQuizOwnerTextScale);
            if (ShouldShowInitialQuizOwnerQuestion(snapshot.ProblemText))
            {
                DrawInitialQuizOwnerQuestionLabel(ownerBounds);
                DrawInitialQuizOwnerSingleLineText(
                    snapshot.ProblemText,
                    new Rectangle(ownerBounds.X + 92, ownerBounds.Y + 110, 146, 18),
                    Color.White,
                    InitialQuizOwnerTextScale);
            }

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

            _initialQuizOwnerChildControlState = ResolveInitialQuizOwnerChildControlState(snapshot.RemainingSeconds);
            bool showInput = _initialQuizOwnerChildControlState.EditVisible;
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

            InitialQuizButtonFrame okButtonFrame = ResolveInitialQuizOwnerOkButtonFrame(_initialQuizOwnerChildControlState.OkButtonEnabled);
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
            bool inputEnabled = _initialQuizOwnerChildControlState.EditVisible && _initialQuizOwnerChildControlState.EditEnabled;
            bool inputFocused = inputEnabled && _initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.Input;
            if (UsingInitialQuizOwnerNativeEditHost)
            {
                _initialQuizOwnerNativeEditHost?.UpdateBounds(inputBounds);
                _initialQuizOwnerNativeEditHost?.SetVisible(_initialQuizOwnerChildControlState.EditVisible);
                if (inputFocused)
                {
                    _initialQuizOwnerNativeEditHost?.Focus();
                }
                else
                {
                    _initialQuizOwnerNativeEditHost?.Blur();
                }

                _initialQuizOwnerNativeEditHost?.SynchronizeState();
                return;
            }

            AntiMacroEditControl editControl = EnsureInitialQuizOwnerEditControl();
            if (editControl != null && ownerBounds != Rectangle.Empty && _initialQuizOwnerChildControlState.EditVisible)
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

        private ClientTextRasterizer CreateInitialQuizOwnerTransientLabelRasterizer()
        {
            if (GraphicsDevice == null)
            {
                return null;
            }

            try
            {
                string requestedFontFamily = MapleStoryStringPool.GetOrFallback(AntiMacroEditControl.ClientFontStringPoolId, "Arial");
                string resolvedFontFamily = ClientTextRasterizer.ResolvePreferredFontFamily(
                    requestedFontFamily,
                    preferredPrivateFontFamilyCandidates: InitialQuizOwnerInputFontFamilyCandidates,
                    preferEmbeddedPrivateFontSources: true);
                return new ClientTextRasterizer(
                    GraphicsDevice,
                    resolvedFontFamily,
                    basePointSize: InitialQuizOwnerInputFontHeightPixels,
                    preferEmbeddedPrivateFontSources: true);
            }
            catch
            {
                return null;
            }
        }

        private void EnsureInitialQuizOwnerControlStackCreated()
        {
            EnsureInitialQuizOwnerPixelTexture();
            EnsureInitialQuizOwnerInputTextRasterizer();
            EnsureInitialQuizOwnerNativeEditHost();
            AntiMacroEditControl editControl = EnsureInitialQuizOwnerEditControl();
            editControl?.Reset();
            _initialQuizOwnerNativeEditHost?.Reset();
            SyncInitialQuizOwnerEditControlState(ownerActive: true, _initialQuizOwnerChildControlState);
        }

        private void DestroyInitialQuizOwnerControlStack()
        {
            DestroyInitialQuizOwnerEditControl();
            DestroyInitialQuizOwnerNativeEditHost();
            if (_initialQuizOwnerInputTextRasterizer != null)
            {
                _initialQuizOwnerInputTextRasterizer.Dispose();
                _initialQuizOwnerInputTextRasterizer = null;
            }
        }

        private void DisposeInitialQuizOwnerParityResources()
        {
            DestroyInitialQuizOwnerControlStack();
        }

        private AntiMacroEditControl EnsureInitialQuizOwnerEditControl()
        {
            if (_initialQuizOwnerEditControl != null)
            {
                return _initialQuizOwnerEditControl;
            }

            EnsureInitialQuizOwnerPixelTexture();
            if (_packetScriptOwnerPixelTexture == null)
            {
                return null;
            }

            _initialQuizOwnerEditControl = new AntiMacroEditControl(
                _packetScriptOwnerPixelTexture,
                InitialQuizOwnerEditOrigin,
                150,
                13,
                _initialQuizOwnerEditTextElementLimit,
                InitialQuizTimerRuntime.GetClientMapleStringEncoding());
            _initialQuizOwnerEditControl.SetFont(_fontChat);
            _initialQuizOwnerEditControl.UseClientAntiMacroVisualStyle();
            _initialQuizOwnerEditControl.SetFocus(_initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.Input);
            return _initialQuizOwnerEditControl;
        }

        private void DestroyInitialQuizOwnerEditControl()
        {
            if (_initialQuizOwnerEditControl == null)
            {
                return;
            }

            _initialQuizOwnerEditControl.Clear();
            _initialQuizOwnerEditControl.SetFocus(false);
            _initialQuizOwnerEditControl = null;
        }

        private NativeAntiMacroEditHost EnsureInitialQuizOwnerNativeEditHost()
        {
            if (_initialQuizOwnerNativeEditHost == null)
            {
                _initialQuizOwnerNativeEditHost = new NativeAntiMacroEditHost(
                    _initialQuizOwnerEditTextElementLimit,
                    InitialQuizTimerRuntime.GetClientMapleStringEncoding());
                _initialQuizOwnerNativeEditHost.TextChanged += OnInitialQuizOwnerNativeEditHostTextChanged;
                _initialQuizOwnerNativeEditHost.SubmitRequested += OnInitialQuizOwnerNativeEditHostSubmitRequested;
                _initialQuizOwnerNativeEditHost.FocusChanged += OnInitialQuizOwnerNativeEditHostFocusChanged;
            }

            if (!UsingInitialQuizOwnerNativeEditHost)
            {
                _initialQuizOwnerNativeEditHost.TryAttach(
                    Window?.Handle ?? IntPtr.Zero,
                    ResolveInitialQuizOwnerInputBounds(ResolveInitialQuizOwnerBounds()));
            }

            return _initialQuizOwnerNativeEditHost;
        }

        private void DestroyInitialQuizOwnerNativeEditHost()
        {
            if (_initialQuizOwnerNativeEditHost == null)
            {
                return;
            }

            _initialQuizOwnerNativeEditHost.TextChanged -= OnInitialQuizOwnerNativeEditHostTextChanged;
            _initialQuizOwnerNativeEditHost.SubmitRequested -= OnInitialQuizOwnerNativeEditHostSubmitRequested;
            _initialQuizOwnerNativeEditHost.FocusChanged -= OnInitialQuizOwnerNativeEditHostFocusChanged;
            _initialQuizOwnerNativeEditHost.Dispose();
            _initialQuizOwnerNativeEditHost = null;
        }

        private void OnInitialQuizOwnerNativeEditHostTextChanged(string text)
        {
            SyncInitialQuizOwnerLegacyInputStateFromEditControl();
        }

        private void OnInitialQuizOwnerNativeEditHostSubmitRequested()
        {
            if (!_initialQuizTimerRuntime.TryBuildOwnerSnapshot(currTickCount, out InitialQuizOwnerSnapshot snapshot))
            {
                return;
            }

            if (!ResolveInitialQuizOwnerChildControlState(snapshot.RemainingSeconds).OkButtonEnabled)
            {
                return;
            }

            SubmitInitialQuizOwnerResult(GetInitialQuizOwnerSubmittedText(), currTickCount, showFeedback: true);
        }

        private void OnInitialQuizOwnerNativeEditHostFocusChanged(bool focused)
        {
            if (focused)
            {
                if (_initialQuizOwnerFocusTarget != InitialQuizOwnerFocusTarget.Input)
                {
                    SetInitialQuizOwnerFocusTarget(InitialQuizOwnerFocusTarget.Input);
                }
            }
            else if (_initialQuizOwnerFocusTarget == InitialQuizOwnerFocusTarget.Input)
            {
                SetInitialQuizOwnerFocusTarget(InitialQuizOwnerFocusTarget.Owner);
            }
        }

        private void SyncInitialQuizOwnerLegacyInputStateFromEditControl()
        {
            if (UsingInitialQuizOwnerNativeEditHost)
            {
                string nativeText = _initialQuizOwnerNativeEditHost?.Text ?? string.Empty;
                _initialQuizOwnerInput.Clear();
                _initialQuizOwnerInput.Append(nativeText);
                _initialQuizOwnerCursorIndex = nativeText.Length;
                return;
            }

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
            if (UsingInitialQuizOwnerNativeEditHost)
            {
                SyncInitialQuizOwnerLegacyInputStateFromEditControl();
                return _initialQuizOwnerNativeEditHost?.Text ?? string.Empty;
            }

            if (_initialQuizOwnerEditControl != null)
            {
                SyncInitialQuizOwnerLegacyInputStateFromEditControl();
                return _initialQuizOwnerEditControl.Text ?? string.Empty;
            }

            return _initialQuizOwnerInput.ToString();
        }

        internal static bool ShouldCaptureInitialQuizOwnerTextInput(bool ownerActive, int remainingSeconds, InitialQuizOwnerFocusTarget focusTarget)
        {
            return ShouldCaptureInitialQuizOwnerTextInput(
                ownerActive,
                ResolveInitialQuizOwnerChildControlState(remainingSeconds),
                focusTarget);
        }

        private bool ShouldCaptureInitialQuizOwnerTextInput()
        {
            return _initialQuizTimerRuntime.TryBuildOwnerSnapshot(currTickCount, out InitialQuizOwnerSnapshot snapshot)
                && ShouldCaptureInitialQuizOwnerTextInput(
                    ownerActive: true,
                    ResolveInitialQuizOwnerChildControlState(snapshot.RemainingSeconds),
                    _initialQuizOwnerFocusTarget);
        }

        internal static bool ShouldCaptureInitialQuizOwnerTextInput(
            bool ownerActive,
            InitialQuizOwnerChildControlState controlState,
            InitialQuizOwnerFocusTarget focusTarget)
        {
            return ownerActive
                && controlState.EditVisible
                && controlState.EditEnabled
                && focusTarget == InitialQuizOwnerFocusTarget.Input;
        }

        private bool DoesInitialQuizOwnerCaptureWindowInput()
        {
            return _initialQuizTimerRuntime.TryBuildOwnerSnapshot(currTickCount, out _)
                && _initialQuizOwnerCaptureState != InitialQuizOwnerCaptureState.None;
        }

        internal static bool ShouldForwardInitialQuizOwnerInputToActiveWindow(bool ownerCapturesWindowInput)
        {
            return !ownerCapturesWindowInput;
        }

        internal static bool ShouldForwardInitialQuizOwnerImeToNpcOverlay(
            bool ownerCapturesWindowInput,
            bool npcOverlayCapturesKeyboardInput)
        {
            return !ownerCapturesWindowInput && npcOverlayCapturesKeyboardInput;
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

            if (UsingInitialQuizOwnerNativeEditHost)
            {
                _initialQuizOwnerNativeEditHost?.SynchronizeState();
                return;
            }

            AntiMacroEditControl editControl = EnsureInitialQuizOwnerEditControl();
            editControl?.HandleCommittedText(text, capturesKeyboardInput: true);
            SyncInitialQuizOwnerLegacyInputStateFromEditControl();
        }

        private void HandleInitialQuizOwnerCompositionText(string compositionText)
        {
            if (UsingInitialQuizOwnerNativeEditHost)
            {
                return;
            }

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
            if (UsingInitialQuizOwnerNativeEditHost)
            {
                return;
            }

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
            if (UsingInitialQuizOwnerNativeEditHost)
            {
                return;
            }

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

        private void DrawInitialQuizOwnerLayer(Texture2D texture, Rectangle bounds, Point origin)
        {
            if (texture == null)
            {
                return;
            }

            Rectangle drawBounds = ResolveInitialQuizOwnerLayerBounds(bounds, texture.Width, texture.Height, origin);
            if (drawBounds == Rectangle.Empty)
            {
                return;
            }

            _spriteBatch.Draw(texture, drawBounds, Color.White);
        }

        private void DrawInitialQuizOwnerLayer(InitialQuizOwnerLayerKind layer, Rectangle ownerBounds, Rectangle overlayBounds)
        {
            switch (layer)
            {
                case InitialQuizOwnerLayerKind.Backgrnd:
                    DrawInitialQuizOwnerLayer(_initialQuizOwnerBackgroundTexture, ownerBounds, _initialQuizOwnerBackgroundOrigin);
                    break;
                case InitialQuizOwnerLayerKind.Backgrnd2:
                    DrawInitialQuizOwnerLayer(_initialQuizOwnerBackgroundTexture2, ownerBounds, _initialQuizOwnerBackground2Origin);
                    break;
                case InitialQuizOwnerLayerKind.Backgrnd3:
                    DrawInitialQuizOwnerLayer(_initialQuizOwnerBackgroundTexture3, overlayBounds, Point.Zero);
                    break;
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

            int frameIndex = ResolveInitialQuizOwnerAnimationFrameIndex(
                currentTickCount,
                _initialQuizOwnerAnimationFrames.Select(static frame => frame.DelayMs).ToArray());
            return frameIndex >= 0 && frameIndex < _initialQuizOwnerAnimationFrames.Length
                ? _initialQuizOwnerAnimationFrames[frameIndex]
                : _initialQuizOwnerAnimationFrames[0];
        }

        private Rectangle ResolveInitialQuizOwnerBounds()
        {
            int ownerWidth = _initialQuizOwnerBackgroundTexture?.Width > 0
                ? _initialQuizOwnerBackgroundTexture.Width
                : InitialQuizOwnerWidth;
            int ownerHeight = _initialQuizOwnerBackgroundTexture?.Height > 0
                ? _initialQuizOwnerBackgroundTexture.Height
                : InitialQuizOwnerHeight;
            return ResolveInitialQuizOwnerBounds(
                _renderParams.RenderWidth,
                _renderParams.RenderHeight,
                ownerWidth,
                ownerHeight);
        }

        internal static Rectangle ResolveInitialQuizOwnerBounds(int renderWidth, int renderHeight, int ownerWidth, int ownerHeight)
        {
            int resolvedWidth = ownerWidth > 0 ? ownerWidth : InitialQuizOwnerWidth;
            int resolvedHeight = ownerHeight > 0 ? ownerHeight : InitialQuizOwnerHeight;
            return new Rectangle(
                (renderWidth - resolvedWidth) / 2,
                (renderHeight - resolvedHeight) / 2,
                resolvedWidth,
                resolvedHeight);
        }

        private Rectangle ResolveInitialQuizOwnerOkButtonBounds(Rectangle ownerBounds)
        {
            Point buttonSize = ResolveInitialQuizOwnerOkButtonHitSize(
                _initialQuizOwnerOkButtonNormalFrame?.Texture?.Width ?? 0,
                _initialQuizOwnerOkButtonNormalFrame?.Texture?.Height ?? 0,
                _initialQuizOwnerOkButtonHoverFrame?.Texture?.Width ?? 0,
                _initialQuizOwnerOkButtonHoverFrame?.Texture?.Height ?? 0,
                _initialQuizOwnerOkButtonPressedFrame?.Texture?.Width ?? 0,
                _initialQuizOwnerOkButtonPressedFrame?.Texture?.Height ?? 0,
                _initialQuizOwnerOkButtonDisabledFrame?.Texture?.Width ?? 0,
                _initialQuizOwnerOkButtonDisabledFrame?.Texture?.Height ?? 0,
                _initialQuizOwnerOkButtonKeyFocusedFrame?.Texture?.Width ?? 0,
                _initialQuizOwnerOkButtonKeyFocusedFrame?.Texture?.Height ?? 0);
            Point buttonOrigin = ResolveInitialQuizOwnerOkButtonHitOrigin(
                _initialQuizOwnerOkButtonNormalFrame?.Origin ?? Point.Zero,
                _initialQuizOwnerOkButtonHoverFrame?.Origin ?? Point.Zero,
                _initialQuizOwnerOkButtonPressedFrame?.Origin ?? Point.Zero,
                _initialQuizOwnerOkButtonDisabledFrame?.Origin ?? Point.Zero,
                _initialQuizOwnerOkButtonKeyFocusedFrame?.Origin ?? Point.Zero);
            return ResolveInitialQuizOwnerOkButtonBounds(ownerBounds, buttonSize.X, buttonSize.Y, buttonOrigin);
        }

        internal static Rectangle ResolveInitialQuizOwnerOkButtonBounds(Rectangle ownerBounds, int buttonWidth, int buttonHeight)
        {
            return ResolveInitialQuizOwnerOkButtonBounds(ownerBounds, buttonWidth, buttonHeight, Point.Zero);
        }

        internal static Rectangle ResolveInitialQuizOwnerOkButtonBounds(Rectangle ownerBounds, int buttonWidth, int buttonHeight, Point origin)
        {
            return new Rectangle(
                ownerBounds.X + InitialQuizOwnerOkButtonLeft - origin.X,
                ownerBounds.Y + InitialQuizOwnerOkButtonTop - origin.Y,
                Math.Max(1, buttonWidth),
                Math.Max(1, buttonHeight));
        }

        internal static Point ResolveInitialQuizOwnerOkButtonHitOrigin(
            Point normalOrigin,
            Point hoverOrigin,
            Point pressedOrigin,
            Point disabledOrigin,
            Point keyFocusedOrigin)
        {
            if (normalOrigin != Point.Zero)
            {
                return normalOrigin;
            }

            if (hoverOrigin != Point.Zero)
            {
                return hoverOrigin;
            }

            if (pressedOrigin != Point.Zero)
            {
                return pressedOrigin;
            }

            if (disabledOrigin != Point.Zero)
            {
                return disabledOrigin;
            }

            return keyFocusedOrigin;
        }

        internal static Point ResolveInitialQuizOwnerOkButtonHitSize(
            int normalWidth,
            int normalHeight,
            int hoverWidth,
            int hoverHeight,
            int pressedWidth,
            int pressedHeight,
            int disabledWidth,
            int disabledHeight,
            int keyFocusedWidth,
            int keyFocusedHeight)
        {
            if (normalWidth > 0 && normalHeight > 0)
            {
                return new Point(normalWidth, normalHeight);
            }

            if (hoverWidth > 0 && hoverHeight > 0)
            {
                return new Point(hoverWidth, hoverHeight);
            }

            if (pressedWidth > 0 && pressedHeight > 0)
            {
                return new Point(pressedWidth, pressedHeight);
            }

            if (disabledWidth > 0 && disabledHeight > 0)
            {
                return new Point(disabledWidth, disabledHeight);
            }

            if (keyFocusedWidth > 0 && keyFocusedHeight > 0)
            {
                return new Point(keyFocusedWidth, keyFocusedHeight);
            }

            return new Point(InitialQuizOwnerOkButtonWidth, InitialQuizOwnerOkButtonHeight);
        }

        private static Rectangle ResolveInitialQuizOwnerInputBounds(Rectangle ownerBounds)
        {
            return new Rectangle(ownerBounds.X + 109, ownerBounds.Y + 157, 150, 13);
        }

        private void EnsureInitialQuizOwnerPixelTexture()
        {
            if (_packetScriptOwnerPixelTexture != null || GraphicsDevice == null)
            {
                return;
            }

            _packetScriptOwnerPixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _packetScriptOwnerPixelTexture.SetData(new[] { Color.White });
        }

        private void EnsureInitialQuizOwnerVisualsLoaded()
        {
            if (_initialQuizOwnerVisualsLoaded || GraphicsDevice == null)
            {
                return;
            }

            _initialQuizOwnerVisualsLoaded = true;
            EnsureInitialQuizOwnerPixelTexture();

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
            _initialQuizOwnerBackgroundOrigin = ResolveCanvasOrigin((preferred?["backgrnd"] ?? fallback?["backgrnd"]) as WzCanvasProperty);
            _initialQuizOwnerBackground2Origin = ResolveCanvasOrigin((preferred?["backgrnd2"] ?? fallback?["backgrnd2"]) as WzCanvasProperty);
            _initialQuizOwnerBackgroundZ = ResolveCanvasZ((preferred?["backgrnd"] ?? fallback?["backgrnd"]) as WzCanvasProperty, -5);
            _initialQuizOwnerBackground2Z = ResolveCanvasZ((preferred?["backgrnd2"] ?? fallback?["backgrnd2"]) as WzCanvasProperty, -4);
            _initialQuizOwnerBackground3Z = ResolveCanvasZ(preferredBackground3 ?? fallbackBackground3, -3);
            _initialQuizOwnerOkButtonNormalFrame = LoadInitialQuizOwnerButtonFrame(okButtonProperty, "normal");
            _initialQuizOwnerOkButtonHoverFrame = LoadInitialQuizOwnerButtonFrame(okButtonProperty, "mouseOver");
            _initialQuizOwnerOkButtonPressedFrame = LoadInitialQuizOwnerButtonFrame(okButtonProperty, "pressed");
            _initialQuizOwnerOkButtonDisabledFrame = LoadInitialQuizOwnerButtonFrame(okButtonProperty, "disabled");
            _initialQuizOwnerOkButtonKeyFocusedFrame = LoadInitialQuizOwnerButtonFrame(okButtonProperty, "keyFocused");
            _initialQuizOwnerDigits = LoadInitialQuizOwnerDigits(preferred?["num1"] as WzSubProperty, fallback?["num1"] as WzSubProperty, out _initialQuizOwnerCommaTexture);
            _initialQuizOwnerAnimationFrames = LoadInitialQuizOwnerAnimationFrames(preferred?["ani"] as WzSubProperty, fallback?["ani"] as WzSubProperty);
            _initialQuizOwnerBackground3Origin = ResolveCanvasOrigin(preferredBackground3 ?? fallbackBackground3);
        }

        private void SyncInitialQuizOwnerEditControlState(bool ownerActive, InitialQuizOwnerChildControlState controlState)
        {
            NativeAntiMacroEditHost nativeEditHost = EnsureInitialQuizOwnerNativeEditHost();
            if (nativeEditHost != null && nativeEditHost.IsAttached)
            {
                Rectangle inputBounds = ResolveInitialQuizOwnerInputBounds(ResolveInitialQuizOwnerBounds());
                nativeEditHost.UpdateBounds(inputBounds);
                bool showInput = ownerActive && controlState.EditVisible;
                nativeEditHost.SetVisible(showInput);
                bool nativeInputFocused = ShouldCaptureInitialQuizOwnerTextInput(ownerActive, controlState, _initialQuizOwnerFocusTarget);
                if (showInput && nativeInputFocused)
                {
                    nativeEditHost.Focus();
                }
                else
                {
                    nativeEditHost.Blur();
                }

                nativeEditHost.SynchronizeState();
            }

            AntiMacroEditControl editControl = EnsureInitialQuizOwnerEditControl();
            if (editControl == null)
            {
                return;
            }

            bool inputFocused = ShouldCaptureInitialQuizOwnerTextInput(ownerActive, controlState, _initialQuizOwnerFocusTarget);
            editControl.SetFocus(inputFocused);
            if (!inputFocused)
            {
                editControl.EndMouseSelection();
            }
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
            Dictionary<string, WzCanvasProperty> canvasByName = canvases.ToDictionary(static canvas => canvas.Name, StringComparer.Ordinal);
            foreach (string frameName in ResolveInitialQuizOwnerAnimationFrameNames(canvasByName.Keys))
            {
                WzCanvasProperty canvas = canvasByName[frameName];
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

        internal static string[] ResolveInitialQuizOwnerAnimationFrameNames(IEnumerable<string> frameNames)
        {
            return (frameNames ?? Enumerable.Empty<string>())
                .OrderBy(static frameName => int.TryParse(frameName, out int frameIndex) ? frameIndex : int.MaxValue)
                .ThenBy(static frameName => frameName, StringComparer.Ordinal)
                .ToArray();
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
            return ResolveCanvasOrigin(canvas);
        }

        internal static bool ShouldShowInitialQuizOwnerHint(string hintText)
        {
            return !string.IsNullOrEmpty(hintText);
        }

        internal static bool ShouldShowInitialQuizOwnerQuestion(string problemText)
        {
            return !string.IsNullOrEmpty(problemText);
        }

        internal static bool ShouldShowInitialQuizOwnerInput(int remainingSeconds)
        {
            return ResolveInitialQuizOwnerChildControlState(remainingSeconds).EditVisible;
        }

        internal static InitialQuizOwnerChildControlState ResolveInitialQuizOwnerChildControlState(int remainingSeconds)
        {
            return remainingSeconds > 0
                ? InitialQuizOwnerChildControlState.Active
                : InitialQuizOwnerChildControlState.Inactive;
        }

        internal static int ResolveInitialQuizOwnerEditTextElementLimit(int maxInputByteLength)
        {
            int clientByteLimit = Math.Max(0, maxInputByteLength);
            return clientByteLimit > 0
                ? clientByteLimit
                : InitialQuizOwnerInputMaxLength;
        }

        internal static InitialQuizOwnerFocusTarget ResolveInitialQuizOwnerMousePressFocusTarget(
            bool showInput,
            bool hoveringOkButton,
            bool cursorInInput)
        {
            if (!showInput)
            {
                return InitialQuizOwnerFocusTarget.Owner;
            }

            if (hoveringOkButton)
            {
                return InitialQuizOwnerFocusTarget.OkButton;
            }

            return cursorInInput
                ? InitialQuizOwnerFocusTarget.Input
                : InitialQuizOwnerFocusTarget.Owner;
        }

        internal static InitialQuizOwnerFocusTarget ResolveNextInitialQuizOwnerFocusTarget(InitialQuizOwnerFocusTarget currentFocus)
        {
            return currentFocus switch
            {
                InitialQuizOwnerFocusTarget.Input => InitialQuizOwnerFocusTarget.OkButton,
                InitialQuizOwnerFocusTarget.OkButton => InitialQuizOwnerFocusTarget.Input,
                _ => InitialQuizOwnerFocusTarget.Input
            };
        }

        internal static bool ShouldClearInitialQuizOwnerImeOnFocusChange(
            InitialQuizOwnerFocusTarget previousFocus,
            InitialQuizOwnerFocusTarget nextFocus)
        {
            return previousFocus == InitialQuizOwnerFocusTarget.Input
                && nextFocus != InitialQuizOwnerFocusTarget.Input;
        }

        private void SetInitialQuizOwnerFocusTarget(InitialQuizOwnerFocusTarget focusTarget)
        {
            InitialQuizOwnerFocusTarget previousFocus = _initialQuizOwnerFocusTarget;
            _initialQuizOwnerFocusTarget = focusTarget;
            _initialQuizOwnerCaptureState = ResolveInitialQuizOwnerCaptureState(
                _initialQuizOwnerCaptureState != InitialQuizOwnerCaptureState.None,
                focusTarget);
            SyncInitialQuizOwnerEditControlState(
                _initialQuizOwnerCaptureState != InitialQuizOwnerCaptureState.None,
                _initialQuizOwnerChildControlState);
            if (!ShouldClearInitialQuizOwnerImeOnFocusChange(previousFocus, focusTarget))
            {
                return;
            }

            EnsureInitialQuizOwnerEditControl()?.SetFocus(false);
            ClearInitialQuizOwnerCompositionText();
            ClearInitialQuizOwnerImeCandidateList();
        }

        internal static InitialQuizOwnerTimeoutBehavior ResolveInitialQuizOwnerTimeoutBehavior(
            int remainingSeconds,
            bool resultSent,
            bool timeoutCloseArmed)
        {
            if (resultSent || remainingSeconds > 0)
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

        internal static IReadOnlyList<InitialQuizOwnerLayerOrder> ResolveInitialQuizOwnerLayerDrawOrder(
            bool hasBackgrnd,
            int backgrndZ,
            bool hasBackgrnd2,
            int backgrnd2Z,
            bool hasBackgrnd3,
            int backgrnd3Z)
        {
            List<(InitialQuizOwnerLayerOrder Order, int SourceIndex)> layers = new(3);
            if (hasBackgrnd)
            {
                layers.Add((new InitialQuizOwnerLayerOrder(InitialQuizOwnerLayerKind.Backgrnd, backgrndZ), 0));
            }

            if (hasBackgrnd2)
            {
                layers.Add((new InitialQuizOwnerLayerOrder(InitialQuizOwnerLayerKind.Backgrnd2, backgrnd2Z), 1));
            }

            if (hasBackgrnd3)
            {
                layers.Add((new InitialQuizOwnerLayerOrder(InitialQuizOwnerLayerKind.Backgrnd3, backgrnd3Z), 2));
            }

            return layers
                .OrderBy(static layer => layer.Order.Z)
                .ThenBy(static layer => layer.SourceIndex)
                .Select(static layer => layer.Order)
                .ToArray();
        }

        internal static bool ShouldSubmitInitialQuizOwnerOkButtonRelease(bool pressedOkButton, bool hoveringOkButton, bool enabled)
        {
            return enabled && pressedOkButton && hoveringOkButton;
        }

        internal static bool ShouldSwallowInitialQuizOwnerCancelKey(KeyboardState newKeyboardState, KeyboardState oldKeyboardState)
        {
            return newKeyboardState.IsKeyDown(Keys.Escape)
                && oldKeyboardState.IsKeyUp(Keys.Escape);
        }

        internal static InitialQuizOwnerCaptureState ResolveInitialQuizOwnerCaptureState(
            bool ownerActive,
            InitialQuizOwnerFocusTarget focusTarget)
        {
            if (!ownerActive)
            {
                return InitialQuizOwnerCaptureState.None;
            }

            return focusTarget == InitialQuizOwnerFocusTarget.Input
                ? InitialQuizOwnerCaptureState.OwnerWithEditFocus
                : InitialQuizOwnerCaptureState.OwnerOnly;
        }

        internal static bool ShouldDrawInitialQuizOwnerCursor(int currentTickCount, int cursorBlinkStartedAt)
        {
            return ((currentTickCount - cursorBlinkStartedAt) / 500) % 2 == 0;
        }

        internal static int ResolveInitialQuizOwnerDisplayedRemainingSeconds(
            int? previousDisplayedRemainingSeconds,
            int currentRemainingSeconds)
        {
            int current = Math.Max(0, currentRemainingSeconds);
            if (!previousDisplayedRemainingSeconds.HasValue)
            {
                return current;
            }

            int previous = Math.Max(0, previousDisplayedRemainingSeconds.Value);
            if (current == previous)
            {
                return previous;
            }

            // `CUIInitialQuiz::Update` invalidates countdown changes only once the clock reaches <= 120s.
            return current <= 120
                ? current
                : previous;
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

        internal static int ResolveInitialQuizOwnerAnimationFrameIndex(int currentTickCount, IReadOnlyList<int> frameDelaysMs)
        {
            if (frameDelaysMs == null || frameDelaysMs.Count == 0)
            {
                return -1;
            }

            int totalDuration = 0;
            for (int i = 0; i < frameDelaysMs.Count; i++)
            {
                totalDuration += Math.Max(1, frameDelaysMs[i]);
            }

            if (totalDuration <= 0)
            {
                return 0;
            }

            uint frameTime = unchecked((uint)currentTickCount) % (uint)totalDuration;
            int accumulated = 0;
            for (int i = 0; i < frameDelaysMs.Count; i++)
            {
                accumulated += Math.Max(1, frameDelaysMs[i]);
                if (frameTime < accumulated)
                {
                    return i;
                }
            }

            return frameDelaysMs.Count - 1;
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
                // `CUIInitialQuiz::SetRet` early-outs for trimmed-empty answers without
                // forcing focus back to the edit control.
                return new InitialQuizOwnerSubmissionValidation(false, null, false);
            }

            int inputByteLength = InitialQuizTimerRuntime.GetClientMapleStringByteCount(answerText);
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

            using ClientTextRasterizer transientRasterizer = CreateInitialQuizOwnerTransientLabelRasterizer();
            if (transientRasterizer != null)
            {
                // `CUIInitialQuiz::Draw` acquires/releases draw resources per text call.
                transientRasterizer.DrawString(
                    _spriteBatch,
                    displayText,
                    new Vector2(bounds.X, bounds.Y),
                    color,
                    scale,
                    bounds.Width);
                return;
            }

            displayText = FitInitialQuizOwnerTextToBounds(displayText, bounds.Width, scale, null);
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

        private string FitInitialQuizOwnerTextToBounds(string text, int maxWidth, float scale, ClientTextRasterizer rasterizer)
        {
            if ((rasterizer == null && _fontChat == null) || string.IsNullOrEmpty(text) || maxWidth <= 0)
            {
                return string.Empty;
            }

            if (MeasureInitialQuizOwnerTextWidth(text, scale, rasterizer, _fontChat) <= maxWidth)
            {
                return text;
            }

            int length = text.Length;
            while (length > 0)
            {
                string candidate = text[..length];
                if (MeasureInitialQuizOwnerTextWidth(candidate, scale, rasterizer, _fontChat) <= maxWidth)
                {
                    return candidate.TrimEnd();
                }

                length--;
            }

            return string.Empty;
        }

        private static float MeasureInitialQuizOwnerTextWidth(
            string text,
            float scale,
            ClientTextRasterizer rasterizer,
            SpriteFont fallbackFont)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            return rasterizer != null
                ? rasterizer.MeasureString(text, scale).X
                : (fallbackFont?.MeasureString(text).X ?? 0f) * scale;
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

            return ResolveInitialQuizOwnerLayerBounds(
                ownerBounds,
                _initialQuizOwnerBackgroundTexture3.Width,
                _initialQuizOwnerBackgroundTexture3.Height,
                _initialQuizOwnerBackground3Origin);
        }

        internal static Rectangle ResolveInitialQuizOwnerLayerBounds(Rectangle ownerBounds, int textureWidth, int textureHeight, Point origin)
        {
            if (ownerBounds == Rectangle.Empty || textureWidth <= 0 || textureHeight <= 0)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(
                ownerBounds.X - origin.X,
                ownerBounds.Y - origin.Y,
                textureWidth,
                textureHeight);
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

        private static int ResolveCanvasZ(WzCanvasProperty canvas, int fallback)
        {
            return (canvas?["z"] as WzIntProperty)?.Value ?? fallback;
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
