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
        private const int InitialQuizQuestionLabelStringPoolId = 3958;
        private const int InitialQuizHintLabelStringPoolId = 3959;
        private const int InitialQuizAnswerLabelStringPoolId = 3960;
        private const int InitialQuizTimeoutNoticeStringPoolId = 3964;

        private bool _initialQuizOwnerVisualsLoaded;
        private Texture2D _initialQuizOwnerBackgroundTexture;
        private Texture2D _initialQuizOwnerBackgroundTexture2;
        private Texture2D _initialQuizOwnerBackgroundTexture3;
        private Texture2D _initialQuizOwnerOkButtonNormalTexture;
        private Texture2D _initialQuizOwnerOkButtonHoverTexture;
        private Texture2D _initialQuizOwnerOkButtonPressedTexture;
        private Texture2D _initialQuizOwnerOkButtonDisabledTexture;
        private Texture2D[] _initialQuizOwnerDigits;
        private Texture2D[] _initialQuizOwnerHeaderDigits;
        private Texture2D _initialQuizOwnerCommaTexture;
        private InitialQuizAnimationFrame[] _initialQuizOwnerAnimationFrames = Array.Empty<InitialQuizAnimationFrame>();

        private readonly StringBuilder _initialQuizOwnerInput = new(InitialQuizOwnerInputMaxLength);
        private int _initialQuizOwnerCursorIndex;
        private int _initialQuizOwnerCursorBlinkStartedAt;
        private bool _initialQuizOwnerHoveringOkButton;
        private bool _initialQuizOwnerPressedOkButton;
        private bool _initialQuizOwnerResultSent;

        private sealed record InitialQuizAnimationFrame(Texture2D Texture, int DelayMs);

        private bool TryApplyPacketOwnedInitialQuizPayload(byte[] payload, out string message)
        {
            bool applied = _initialQuizTimerRuntime.TryApplyPayload(payload, currTickCount, out message);
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

        private void ResetInitialQuizOwnerInputState(int currentTickCount)
        {
            _initialQuizOwnerInput.Clear();
            _initialQuizOwnerCursorIndex = 0;
            _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
            _initialQuizOwnerHoveringOkButton = false;
            _initialQuizOwnerPressedOkButton = false;
            _initialQuizOwnerResultSent = false;
        }

        private void ClearInitialQuizOwnerInputState()
        {
            _initialQuizOwnerInput.Clear();
            _initialQuizOwnerCursorIndex = 0;
            _initialQuizOwnerHoveringOkButton = false;
            _initialQuizOwnerPressedOkButton = false;
            _initialQuizOwnerResultSent = false;
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
            if (!_initialQuizTimerRuntime.TryBuildOwnerSnapshot(currentTickCount, out _))
            {
                return false;
            }

            Rectangle ownerBounds = ResolveInitialQuizOwnerBounds();
            Rectangle okButtonBounds = ResolveInitialQuizOwnerOkButtonBounds(ownerBounds);
            Point cursor = new(mouseState.X, mouseState.Y);
            _initialQuizOwnerHoveringOkButton = okButtonBounds.Contains(cursor);

            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool justPressed = leftPressed && previousMouseState.LeftButton == ButtonState.Released;
            bool justReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

            if (justPressed)
            {
                _initialQuizOwnerPressedOkButton = _initialQuizOwnerHoveringOkButton;
                _initialQuizOwnerCursorBlinkStartedAt = currentTickCount;
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

            if (newKeyboardState.IsKeyDown(Keys.Enter) && oldKeyboardState.IsKeyUp(Keys.Enter))
            {
                SubmitInitialQuizOwnerResult(_initialQuizOwnerInput.ToString(), currentTickCount, showFeedback: true);
                return true;
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

            Rectangle timerBounds = new(ownerBounds.X + 111, ownerBounds.Y + 33, 43, 24);
            Rectangle questionBounds = new(ownerBounds.X + 219, ownerBounds.Y + 72, 30, 24);
            DrawInitialQuizOwnerTimerDigits(timerBounds, snapshot.RemainingSeconds);
            DrawPacketScriptNumber(questionBounds, Math.Max(0, snapshot.QuestionNumber), _packetScriptInitialQuizHeaderDigits ?? _packetScriptInitialQuizDigits, Color.White);

            DrawInitialQuizOwnerAnimationFrame(ownerBounds, currentTickCount);

            DrawPacketScriptOwnerWrappedText(
                snapshot.Title,
                new Rectangle(ownerBounds.X + 30, ownerBounds.Y + 84, 190, 18),
                Color.White,
                0.44f,
                maxLines: 1);
            DrawPacketScriptOwnerWrappedText(
                snapshot.ProblemText,
                new Rectangle(ownerBounds.X + 92, ownerBounds.Y + 110, 146, 18),
                Color.White,
                0.44f,
                maxLines: 1);

            string hintLabel = MapleStoryStringPool.GetOrFallback(InitialQuizHintLabelStringPoolId, "Clue:");
            DrawPacketScriptOwnerWrappedText(
                hintLabel.Trim(),
                new Rectangle(ownerBounds.X + 52, ownerBounds.Y + 130, 38, 18),
                Color.White,
                0.42f,
                maxLines: 1);
            DrawPacketScriptOwnerWrappedText(
                snapshot.HintText,
                new Rectangle(ownerBounds.X + 92, ownerBounds.Y + 130, 146, 18),
                Color.White,
                0.42f,
                maxLines: 1);

            DrawPacketScriptOwnerWrappedText(
                MapleStoryStringPool.GetOrFallback(InitialQuizQuestionLabelStringPoolId, "Question:"),
                new Rectangle(ownerBounds.X + 18, ownerBounds.Y + 68, 90, 18),
                new Color(117, 69, 27),
                0.40f,
                maxLines: 1);

            DrawInitialQuizOwnerInputField(inputBounds, snapshot.RemainingMs > 0, currentTickCount);

            string footerText = snapshot.RemainingMs > 0
                ? MapleStoryStringPool.GetOrFallback(InitialQuizTimeoutNoticeStringPoolId, "Enter an answer within the time limit.")
                : "Time is over.";
            DrawPacketScriptOwnerWrappedText(
                footerText,
                new Rectangle(ownerBounds.X + 38, ownerBounds.Y + 202, 190, 18),
                new Color(255, 80, 80),
                0.39f,
                maxLines: 1);

            Texture2D okButtonTexture = ResolveInitialQuizOwnerOkButtonTexture(snapshot.RemainingMs > 0);
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

        private void DrawInitialQuizOwnerInputField(Rectangle inputBounds, bool enabled, int currentTickCount)
        {
            Color fillColor = enabled ? new Color(255, 255, 255, 212) : new Color(170, 170, 170, 180);
            Color borderColor = enabled ? new Color(113, 78, 48) : new Color(84, 84, 84);
            DrawPacketScriptOwnerFrame(inputBounds, fillColor, borderColor);

            string answerLabel = MapleStoryStringPool.GetOrFallback(InitialQuizAnswerLabelStringPoolId, "[Enter Answer]");
            DrawPacketScriptOwnerWrappedText(
                answerLabel,
                new Rectangle(inputBounds.X - 64, inputBounds.Y, 60, inputBounds.Height),
                new Color(95, 61, 35),
                0.37f,
                maxLines: 1);

            string inputText = _initialQuizOwnerInput.ToString();
            bool showPlaceholder = string.IsNullOrEmpty(inputText);
            string displayText = showPlaceholder ? "Type answer..." : inputText;
            Color textColor = showPlaceholder ? new Color(123, 123, 123) : Color.Black;
            Vector2 drawPosition = new(inputBounds.X + 4, inputBounds.Y - 1);
            _spriteBatch.DrawString(_fontChat, displayText, drawPosition, textColor, 0f, Vector2.Zero, 0.38f, SpriteEffects.None, 0f);

            if (!enabled || showPlaceholder)
            {
                return;
            }

            bool cursorVisible = ((currentTickCount - _initialQuizOwnerCursorBlinkStartedAt) / 500) % 2 == 0;
            if (!cursorVisible)
            {
                return;
            }

            string prefix = inputText[..Math.Clamp(_initialQuizOwnerCursorIndex, 0, inputText.Length)];
            int cursorX = inputBounds.X + 4 + (int)Math.Round(_fontChat.MeasureString(prefix).X * 0.38f);
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
                _spriteBatch.Draw(texture, bounds, Color.White);
            }
        }

        private void DrawInitialQuizOwnerTimerDigits(Rectangle bounds, int remainingSeconds)
        {
            if (_initialQuizOwnerDigits == null || _initialQuizOwnerDigits.Length == 0)
            {
                DrawPacketScriptTime(bounds, remainingSeconds, _packetScriptInitialQuizDigits, Color.White);
                return;
            }

            int minutes = Math.Max(0, remainingSeconds) / 60;
            int seconds = Math.Max(0, remainingSeconds) % 60;
            string minuteText = $"{minutes:D2}";
            string secondText = $"{seconds:D2}";
            DrawPacketScriptNumber(new Rectangle(bounds.X, bounds.Y, 21, bounds.Height), minuteText[0].ToString(), _packetScriptInitialQuizDigits, Color.White, centerHorizontally: false);
            DrawPacketScriptNumber(new Rectangle(bounds.X + 21, bounds.Y, 21, bounds.Height), minuteText[1].ToString(), _packetScriptInitialQuizDigits, Color.White, centerHorizontally: false);
            DrawPacketScriptNumber(new Rectangle(bounds.X + 43, bounds.Y, 21, bounds.Height), secondText[0].ToString(), _packetScriptInitialQuizDigits, Color.White, centerHorizontally: false);
            DrawPacketScriptNumber(new Rectangle(bounds.X + 64, bounds.Y, 21, bounds.Height), secondText[1].ToString(), _packetScriptInitialQuizDigits, Color.White, centerHorizontally: false);
        }

        private Texture2D ResolveInitialQuizOwnerOkButtonTexture(bool enabled)
        {
            if (!enabled)
            {
                return _initialQuizOwnerOkButtonDisabledTexture ?? _initialQuizOwnerOkButtonNormalTexture;
            }

            if (_initialQuizOwnerPressedOkButton)
            {
                return _initialQuizOwnerOkButtonPressedTexture ?? _initialQuizOwnerOkButtonHoverTexture ?? _initialQuizOwnerOkButtonNormalTexture;
            }

            if (_initialQuizOwnerHoveringOkButton)
            {
                return _initialQuizOwnerOkButtonHoverTexture ?? _initialQuizOwnerOkButtonNormalTexture;
            }

            return _initialQuizOwnerOkButtonNormalTexture;
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
            int left = Math.Max(0, (_renderParams.RenderWidth - InitialQuizOwnerWidth) / 2);
            int top = Math.Max(24, (_renderParams.RenderHeight - InitialQuizOwnerHeight) / 2);
            return new Rectangle(left, top, InitialQuizOwnerWidth, InitialQuizOwnerHeight);
        }

        private static Rectangle ResolveInitialQuizOwnerOverlayBounds(Rectangle ownerBounds)
        {
            return new Rectangle(ownerBounds.X + 22, ownerBounds.Y + 67, 234, 118);
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

            _initialQuizOwnerBackgroundTexture = LoadUiCanvasTexture((preferred?["backgrnd"] ?? fallback?["backgrnd"]) as WzCanvasProperty);
            _initialQuizOwnerBackgroundTexture2 = LoadUiCanvasTexture((preferred?["backgrnd2"] ?? fallback?["backgrnd2"]) as WzCanvasProperty);
            _initialQuizOwnerBackgroundTexture3 = LoadUiCanvasTexture((preferred?["backgrnd3"] ?? fallback?["backgrnd3"]) as WzCanvasProperty);
            _initialQuizOwnerOkButtonNormalTexture = LoadUiCanvasTexture(ResolveInitialQuizOwnerButtonCanvas(preferred?["BtOK"] as WzSubProperty, "normal") ?? ResolveInitialQuizOwnerButtonCanvas(fallback?["BtOK"] as WzSubProperty, "normal"));
            _initialQuizOwnerOkButtonHoverTexture = LoadUiCanvasTexture(ResolveInitialQuizOwnerButtonCanvas(preferred?["BtOK"] as WzSubProperty, "mouseOver") ?? ResolveInitialQuizOwnerButtonCanvas(fallback?["BtOK"] as WzSubProperty, "mouseOver"));
            _initialQuizOwnerOkButtonPressedTexture = LoadUiCanvasTexture(ResolveInitialQuizOwnerButtonCanvas(preferred?["BtOK"] as WzSubProperty, "pressed") ?? ResolveInitialQuizOwnerButtonCanvas(fallback?["BtOK"] as WzSubProperty, "pressed"));
            _initialQuizOwnerOkButtonDisabledTexture = LoadUiCanvasTexture(ResolveInitialQuizOwnerButtonCanvas(preferred?["BtOK"] as WzSubProperty, "disabled") ?? ResolveInitialQuizOwnerButtonCanvas(fallback?["BtOK"] as WzSubProperty, "disabled"));
            _initialQuizOwnerDigits = LoadInitialQuizOwnerDigits(preferred?["num1"] as WzSubProperty, fallback?["num1"] as WzSubProperty, out _initialQuizOwnerCommaTexture);
            _initialQuizOwnerHeaderDigits = LoadInitialQuizOwnerDigits(preferred?["number"] as WzSubProperty, fallback?["number"] as WzSubProperty, out _);
            _initialQuizOwnerAnimationFrames = LoadInitialQuizOwnerAnimationFrames(preferred?["ani"] as WzSubProperty, fallback?["ani"] as WzSubProperty);
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
