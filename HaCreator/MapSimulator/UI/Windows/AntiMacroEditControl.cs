using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AntiMacroEditControl
    {
        private static readonly Color InputCaretColor = new(32, 32, 32);
        private static readonly Color InputCompositionColor = new(74, 74, 74);
        private static readonly Color InputCompositionUnderlineColor = new(74, 74, 74);
        private static readonly Color InputBackgroundColor = Color.White;
        private static readonly Color InputBorderColor = new(114, 114, 114);

        private sealed class InputVisualState
        {
            public InputVisualState(
                string visibleText,
                int visibleStart,
                int visibleCaretIndex,
                int visibleCompositionStart,
                int visibleCompositionLength)
            {
                VisibleText = visibleText ?? string.Empty;
                VisibleStart = Math.Max(0, visibleStart);
                VisibleCaretIndex = Math.Clamp(visibleCaretIndex, 0, VisibleText.Length);
                VisibleCompositionStart = visibleCompositionStart < 0
                    ? -1
                    : Math.Clamp(visibleCompositionStart, 0, VisibleText.Length);
                VisibleCompositionLength = VisibleCompositionStart < 0
                    ? 0
                    : Math.Clamp(visibleCompositionLength, 0, VisibleText.Length - VisibleCompositionStart);
            }

            public string VisibleText { get; }
            public int VisibleStart { get; }
            public int VisibleCaretIndex { get; }
            public int VisibleCompositionStart { get; }
            public int VisibleCompositionLength { get; }

            public string VisibleCommittedPrefix =>
                VisibleCompositionStart < 0
                    ? VisibleText
                    : VisibleText[..VisibleCompositionStart];

            public string VisibleComposition =>
                VisibleCompositionStart < 0 || VisibleCompositionLength <= 0
                    ? string.Empty
                    : VisibleText.Substring(VisibleCompositionStart, VisibleCompositionLength);

            public string VisibleCommittedSuffix =>
                VisibleCompositionStart < 0 || VisibleCompositionLength <= 0
                    ? string.Empty
                    : VisibleText[(VisibleCompositionStart + VisibleCompositionLength)..];
        }

        private readonly Texture2D _pixelTexture;
        private readonly int _width;
        private readonly int _height;
        private readonly int _maxLength;

        private SpriteFont _font;
        private Point _inputOrigin;
        private string _inputText = string.Empty;
        private string _compositionText = string.Empty;
        private ImeCandidateListState _candidateListState = ImeCandidateListState.Empty;
        private int _caretIndex;
        private int _compositionCaretIndex = -1;
        private int _compositionInsertionIndex = -1;
        private int _caretBlinkTick;

        public AntiMacroEditControl(Texture2D pixelTexture, Point inputOrigin, int width, int height, int maxLength)
        {
            _pixelTexture = pixelTexture ?? throw new ArgumentNullException(nameof(pixelTexture));
            _inputOrigin = inputOrigin;
            _width = width;
            _height = height;
            _maxLength = maxLength;
            HasFocus = true;
            _caretBlinkTick = Environment.TickCount;
        }

        public bool HasFocus { get; private set; }
        public string Text => _inputText;

        public void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void UpdateLayout(Point inputOrigin)
        {
            _inputOrigin = inputOrigin;
        }

        public Rectangle GetBounds(Rectangle ownerBounds)
        {
            return new Rectangle(
                ownerBounds.X + _inputOrigin.X,
                ownerBounds.Y + _inputOrigin.Y,
                _width,
                _height);
        }

        public void Reset()
        {
            _inputText = string.Empty;
            _caretIndex = 0;
            _caretBlinkTick = Environment.TickCount;
            HasFocus = true;
            ClearCompositionText();
        }

        public void Clear()
        {
            _inputText = string.Empty;
            _caretIndex = 0;
            _caretBlinkTick = Environment.TickCount;
            HasFocus = false;
            ClearCompositionText();
        }

        public void SetFocus(bool focused)
        {
            HasFocus = focused;
            if (!focused)
            {
                ClearCompositionText();
            }
        }

        public void FocusAtMouseX(int mouseX, Rectangle ownerBounds)
        {
            HasFocus = true;
            ClearCompositionText();
            _caretIndex = ResolveCaretIndexFromMouseX(mouseX, ownerBounds);
            _caretBlinkTick = Environment.TickCount;
        }

        public void HandleCommittedText(string text, bool capturesKeyboardInput)
        {
            if (!capturesKeyboardInput || string.IsNullOrEmpty(text))
            {
                return;
            }

            ClearCompositionText();
            foreach (char character in text)
            {
                if (GetTextElementCount(_inputText) >= _maxLength)
                {
                    break;
                }

                if (!char.IsControl(character))
                {
                    InsertCharacter(character);
                }
            }
        }

        public void HandleCompositionText(string text, bool capturesKeyboardInput)
        {
            HandleCompositionState(new ImeCompositionState(text ?? string.Empty, Array.Empty<int>(), -1), capturesKeyboardInput);
        }

        public void HandleCompositionState(ImeCompositionState state, bool capturesKeyboardInput)
        {
            if (!capturesKeyboardInput)
            {
                ClearCompositionText();
                return;
            }

            string sanitized = state?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(sanitized))
            {
                ClearCompositionText();
                return;
            }

            int availableLength = Math.Max(0, _maxLength - GetTextElementCount(_inputText));
            if (availableLength <= 0)
            {
                ClearCompositionText();
                return;
            }

            _compositionInsertionIndex = Math.Clamp(_caretIndex, 0, _inputText.Length);
            int compositionTextElementCount = GetTextElementCount(sanitized);
            _compositionText = compositionTextElementCount > availableLength
                ? GetLeadingTextElements(sanitized, availableLength)
                : sanitized;
            _compositionCaretIndex = Math.Clamp(state.CursorPosition, -1, _compositionText.Length);
            _caretBlinkTick = Environment.TickCount;
        }

        public void HandleImeCandidateList(ImeCandidateListState state, bool capturesKeyboardInput)
        {
            _candidateListState = capturesKeyboardInput && state != null && state.HasCandidates
                ? state
                : ImeCandidateListState.Empty;
        }

        public void ClearCompositionText()
        {
            _compositionText = string.Empty;
            _compositionCaretIndex = -1;
            _compositionInsertionIndex = -1;
            ClearImeCandidateList();
        }

        public void ClearImeCandidateList()
        {
            _candidateListState = ImeCandidateListState.Empty;
        }

        public void HandleKeyboardInput(KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            bool ctrlHeld = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);

            if (ctrlHeld && Pressed(keyboardState, previousKeyboardState, Keys.V))
            {
                PasteClipboardText();
            }

            if (Pressed(keyboardState, previousKeyboardState, Keys.Back))
            {
                if (_compositionText.Length > 0)
                {
                    ClearCompositionText();
                }
                else if (_caretIndex > 0)
                {
                    RemoveCharacterBeforeCaret();
                }

                _caretBlinkTick = Environment.TickCount;
            }

            if (Pressed(keyboardState, previousKeyboardState, Keys.Delete))
            {
                if (_compositionText.Length > 0)
                {
                    ClearCompositionText();
                }
                else if (_caretIndex < _inputText.Length)
                {
                    RemoveCharacterAtCaret();
                }

                _caretBlinkTick = Environment.TickCount;
            }

            if (Pressed(keyboardState, previousKeyboardState, Keys.Left))
            {
                ClearCompositionText();
                _caretIndex = GetPreviousCaretStop(_inputText, _caretIndex);
                _caretBlinkTick = Environment.TickCount;
            }

            if (Pressed(keyboardState, previousKeyboardState, Keys.Right))
            {
                ClearCompositionText();
                _caretIndex = GetNextCaretStop(_inputText, _caretIndex);
                _caretBlinkTick = Environment.TickCount;
            }

            if (Pressed(keyboardState, previousKeyboardState, Keys.Home))
            {
                ClearCompositionText();
                _caretIndex = 0;
                _caretBlinkTick = Environment.TickCount;
            }

            if (Pressed(keyboardState, previousKeyboardState, Keys.End))
            {
                ClearCompositionText();
                _caretIndex = _inputText.Length;
                _caretBlinkTick = Environment.TickCount;
            }
        }

        public bool CanSubmitAnswer(int remainingSeconds)
        {
            return !string.IsNullOrWhiteSpace(_inputText) && remainingSeconds > 0;
        }

        public void Draw(SpriteBatch sprite, Rectangle ownerBounds)
        {
            if (_font == null)
            {
                return;
            }

            Rectangle inputBounds = GetBounds(ownerBounds);
            DrawBox(sprite, inputBounds, InputBackgroundColor, InputBorderColor);

            Vector2 textPosition = new(inputBounds.X + 2, inputBounds.Y - 2);
            InputVisualState visualState = BuildInputVisualState(Math.Max(1, inputBounds.Width - 4));
            if (!string.IsNullOrEmpty(visualState.VisibleText))
            {
                if (visualState.VisibleCommittedPrefix.Length > 0)
                {
                    sprite.DrawString(_font, visualState.VisibleCommittedPrefix, textPosition, Color.Black);
                }

                float committedPrefixWidth = MeasureTextWidth(visualState.VisibleCommittedPrefix);
                Vector2 compositionPosition = textPosition + new Vector2(committedPrefixWidth, 0f);
                if (visualState.VisibleComposition.Length > 0)
                {
                    sprite.DrawString(_font, visualState.VisibleComposition, compositionPosition, InputCompositionColor);
                    DrawCompositionUnderline(sprite, compositionPosition, visualState.VisibleComposition, inputBounds);
                }

                if (visualState.VisibleCommittedSuffix.Length > 0)
                {
                    Vector2 suffixPosition = compositionPosition + new Vector2(MeasureTextWidth(visualState.VisibleComposition), 0f);
                    sprite.DrawString(_font, visualState.VisibleCommittedSuffix, suffixPosition, Color.Black);
                }
            }

            if (ShouldDrawCaret())
            {
                float caretX = textPosition.X + MeasureTextWidth(visualState.VisibleText[..visualState.VisibleCaretIndex]);
                Rectangle caretBounds = new((int)Math.Round(caretX), inputBounds.Y + 2, 1, Math.Max(1, inputBounds.Height - 4));
                sprite.Draw(_pixelTexture, caretBounds, InputCaretColor);
            }
        }

        public void DrawImeCandidateWindow(SpriteBatch sprite, Rectangle ownerBounds)
        {
            if (_font == null || !_candidateListState.HasCandidates)
            {
                return;
            }

            Rectangle inputBounds = GetBounds(ownerBounds);
            Rectangle bounds = GetImeCandidateWindowBounds(sprite.GraphicsDevice.Viewport, inputBounds);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            DrawBox(sprite, bounds, new Color(33, 33, 41, 235), new Color(214, 214, 214, 220));
            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(GetVisibleCandidateCount(), _candidateListState.Candidates.Count - start);
            int rowHeight = Math.Max(_font.LineSpacing + 1, 16);
            int numberWidth = (int)Math.Ceiling(_font.MeasureString($"{Math.Max(1, count)}.").X);
            for (int i = 0; i < count; i++)
            {
                int candidateIndex = start + i;
                string numberText = $"{i + 1}.";
                Rectangle rowBounds = new(bounds.X + 2, bounds.Y + 2 + (i * rowHeight), bounds.Width - 4, rowHeight);
                bool selected = candidateIndex == _candidateListState.Selection;
                if (selected)
                {
                    sprite.Draw(_pixelTexture, rowBounds, new Color(89, 108, 147, 220));
                }

                sprite.DrawString(_font, numberText, new Vector2(rowBounds.X + 4, rowBounds.Y), selected ? Color.White : new Color(222, 222, 222));
                sprite.DrawString(
                    _font,
                    _candidateListState.Candidates[candidateIndex] ?? string.Empty,
                    new Vector2(rowBounds.X + 8 + numberWidth, rowBounds.Y),
                    selected ? Color.White : new Color(240, 235, 200));
            }
        }

        private static bool Pressed(KeyboardState keyboardState, KeyboardState previousKeyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key);
        }

        private string BuildVisibleInputText()
        {
            if (_compositionText.Length == 0)
            {
                return _inputText;
            }

            int insertionIndex = Math.Clamp(_compositionInsertionIndex >= 0 ? _compositionInsertionIndex : _caretIndex, 0, _inputText.Length);
            return _inputText.Insert(insertionIndex, _compositionText);
        }

        private float MeasureTextWidth(string text)
        {
            return string.IsNullOrEmpty(text) || _font == null
                ? 0f
                : _font.MeasureString(text).X;
        }

        private bool ShouldDrawCaret()
        {
            return HasFocus && ((Environment.TickCount - _caretBlinkTick) % 1000) < 500;
        }

        private void InsertCharacter(char character)
        {
            int insertionIndex = Math.Clamp(_caretIndex, 0, _inputText.Length);
            _inputText = _inputText.Insert(insertionIndex, character.ToString());
            _caretIndex = insertionIndex + 1;
            _caretBlinkTick = Environment.TickCount;
        }

        private void RemoveCharacterBeforeCaret()
        {
            int currentCaret = Math.Clamp(_caretIndex, 0, _inputText.Length);
            int previousCaret = GetPreviousCaretStop(_inputText, currentCaret);
            if (previousCaret >= currentCaret)
            {
                return;
            }

            _inputText = _inputText.Remove(previousCaret, currentCaret - previousCaret);
            _caretIndex = previousCaret;
        }

        private void RemoveCharacterAtCaret()
        {
            int currentCaret = Math.Clamp(_caretIndex, 0, _inputText.Length);
            int nextCaret = GetNextCaretStop(_inputText, currentCaret);
            if (nextCaret <= currentCaret)
            {
                return;
            }

            _inputText = _inputText.Remove(currentCaret, nextCaret - currentCaret);
        }

        private void PasteClipboardText()
        {
            try
            {
                if (!System.Windows.Forms.Clipboard.ContainsText())
                {
                    return;
                }

                HandleCommittedText(System.Windows.Forms.Clipboard.GetText(), capturesKeyboardInput: true);
            }
            catch
            {
            }
        }

        private void DrawCompositionUnderline(SpriteBatch sprite, Vector2 compositionPosition, string compositionText, Rectangle inputBounds)
        {
            float underlineWidth = MeasureTextWidth(compositionText);
            if (underlineWidth <= 0f)
            {
                return;
            }

            int underlineX = (int)Math.Floor(compositionPosition.X);
            int underlineY = Math.Min(inputBounds.Bottom - 2, inputBounds.Y + _font.LineSpacing + 1);
            Rectangle underlineBounds = new(underlineX, underlineY, Math.Max(1, (int)Math.Ceiling(underlineWidth)), 1);
            sprite.Draw(_pixelTexture, underlineBounds, InputCompositionUnderlineColor);
        }

        private void DrawBox(SpriteBatch sprite, Rectangle bounds, Color fillColor, Color borderColor)
        {
            sprite.Draw(_pixelTexture, bounds, fillColor);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), borderColor);
        }

        private Rectangle GetImeCandidateWindowBounds(Viewport viewport, Rectangle inputBounds)
        {
            int visibleCount = GetVisibleCandidateCount();
            if (visibleCount <= 0 || _font == null)
            {
                return Rectangle.Empty;
            }

            int widestEntryWidth = 0;
            for (int i = 0; i < visibleCount; i++)
            {
                int candidateIndex = Math.Clamp(_candidateListState.PageStart + i, 0, _candidateListState.Candidates.Count - 1);
                string candidateText = _candidateListState.Candidates[candidateIndex] ?? string.Empty;
                int entryWidth = (int)Math.Ceiling(_font.MeasureString($"{i + 1}.").X + _font.MeasureString(candidateText).X) + 16;
                widestEntryWidth = Math.Max(widestEntryWidth, entryWidth);
            }

            int width = Math.Max(96, widestEntryWidth + 14);
            int height = (visibleCount * Math.Max(_font.LineSpacing + 1, 16)) + 4;
            int x = Math.Clamp(inputBounds.X, 0, Math.Max(0, viewport.Width - width));
            int y = inputBounds.Bottom + 2;
            if (y + height > viewport.Height)
            {
                y = Math.Max(0, inputBounds.Y - height - 2);
            }

            return new Rectangle(x, y, width, height);
        }

        private int GetVisibleCandidateCount()
        {
            if (!_candidateListState.HasCandidates)
            {
                return 0;
            }

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int pageSize = _candidateListState.PageSize > 0 ? _candidateListState.PageSize : _candidateListState.Candidates.Count;
            return Math.Max(0, Math.Min(pageSize, _candidateListState.Candidates.Count - start));
        }

        private InputVisualState BuildInputVisualState(int maxWidth)
        {
            string displayText = BuildVisibleInputText();
            if (string.IsNullOrEmpty(displayText))
            {
                return new InputVisualState(string.Empty, 0, 0, -1, 0);
            }

            int caretIndex = ResolveDisplayCaretIndex();
            int clampedCaretIndex = Math.Clamp(caretIndex, 0, displayText.Length);
            int compositionStart = -1;
            int compositionLength = 0;
            if (_compositionText.Length > 0)
            {
                compositionStart = Math.Clamp(_compositionInsertionIndex >= 0 ? _compositionInsertionIndex : _caretIndex, 0, displayText.Length);
                compositionLength = Math.Min(_compositionText.Length, Math.Max(0, displayText.Length - compositionStart));
            }

            int visibleStart = 0;
            while (visibleStart < clampedCaretIndex)
            {
                if (MeasureTextWidth(displayText[visibleStart..clampedCaretIndex]) <= maxWidth)
                {
                    break;
                }

                visibleStart = GetNextCaretStop(displayText, visibleStart);
            }

            int visibleEnd = displayText.Length;
            while (visibleEnd > clampedCaretIndex)
            {
                if (MeasureTextWidth(displayText[visibleStart..visibleEnd]) <= maxWidth)
                {
                    break;
                }

                visibleEnd = GetPreviousCaretStop(displayText, visibleEnd);
            }

            if (visibleEnd < clampedCaretIndex)
            {
                visibleEnd = clampedCaretIndex;
            }

            string visibleText = displayText[visibleStart..visibleEnd];
            int visibleCaretIndex = clampedCaretIndex - visibleStart;
            int visibleCompositionStart = -1;
            int visibleCompositionLength = 0;
            if (compositionStart >= 0)
            {
                int compositionEnd = compositionStart + compositionLength;
                int clampedVisibleCompositionStart = Math.Clamp(compositionStart - visibleStart, 0, visibleText.Length);
                int clampedVisibleCompositionEnd = Math.Clamp(compositionEnd - visibleStart, 0, visibleText.Length);
                if (clampedVisibleCompositionEnd > clampedVisibleCompositionStart)
                {
                    visibleCompositionStart = clampedVisibleCompositionStart;
                    visibleCompositionLength = clampedVisibleCompositionEnd - clampedVisibleCompositionStart;
                }
            }

            return new InputVisualState(
                visibleText,
                visibleStart,
                visibleCaretIndex,
                visibleCompositionStart,
                visibleCompositionLength);
        }

        private int ResolveCaretIndexFromMouseX(int mouseX, Rectangle ownerBounds)
        {
            if (_font == null || string.IsNullOrEmpty(_inputText))
            {
                Rectangle emptyInputBounds = GetBounds(ownerBounds);
                return Math.Clamp(mouseX < emptyInputBounds.Center.X ? 0 : _inputText.Length, 0, _inputText.Length);
            }

            Rectangle inputBounds = GetBounds(ownerBounds);
            InputVisualState visualState = BuildInputVisualState(Math.Max(1, inputBounds.Width - 4));
            float relativeX = mouseX - (inputBounds.X + 2);
            if (relativeX <= 0f)
            {
                return visualState.VisibleStart;
            }

            foreach (int caretStop in EnumerateCaretStops(visualState.VisibleText))
            {
                if (caretStop <= 0)
                {
                    continue;
                }

                float width = MeasureTextWidth(visualState.VisibleText[..caretStop]);
                if (relativeX < width)
                {
                    int previousCaretStop = GetPreviousCaretStop(visualState.VisibleText, caretStop);
                    float previousWidth = previousCaretStop <= 0 ? 0f : MeasureTextWidth(visualState.VisibleText[..previousCaretStop]);
                    int resolvedVisibleCaret = relativeX - previousWidth <= width - relativeX ? previousCaretStop : caretStop;
                    return visualState.VisibleStart + resolvedVisibleCaret;
                }
            }

            return _inputText.Length;
        }

        private int ResolveDisplayCaretIndex()
        {
            if (_compositionText.Length == 0)
            {
                return Math.Clamp(_caretIndex, 0, _inputText.Length);
            }

            int insertionIndex = Math.Clamp(_compositionInsertionIndex >= 0 ? _compositionInsertionIndex : _caretIndex, 0, _inputText.Length);
            int compositionCaret = _compositionCaretIndex >= 0
                ? Math.Clamp(_compositionCaretIndex, 0, _compositionText.Length)
                : _compositionText.Length;
            return insertionIndex + compositionCaret;
        }

        private static int GetTextElementCount(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int count = 0;
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext())
            {
                count++;
            }

            return count;
        }

        private static string GetLeadingTextElements(string value, int count)
        {
            if (string.IsNullOrEmpty(value) || count <= 0)
            {
                return string.Empty;
            }

            int currentCount = 0;
            int lastIndex = 0;
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext())
            {
                currentCount++;
                lastIndex = enumerator.ElementIndex + enumerator.GetTextElement().Length;
                if (currentCount >= count)
                {
                    return value[..lastIndex];
                }
            }

            return value;
        }

        private static IEnumerable<int> EnumerateCaretStops(string value)
        {
            yield return 0;
            if (string.IsNullOrEmpty(value))
            {
                yield break;
            }

            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext())
            {
                yield return enumerator.ElementIndex + enumerator.GetTextElement().Length;
            }
        }

        private static int GetPreviousCaretStop(string value, int caretIndex)
        {
            int clampedCaretIndex = Math.Clamp(caretIndex, 0, value?.Length ?? 0);
            int previous = 0;
            foreach (int stop in EnumerateCaretStops(value))
            {
                if (stop >= clampedCaretIndex)
                {
                    break;
                }

                previous = stop;
            }

            return previous;
        }

        private static int GetNextCaretStop(string value, int caretIndex)
        {
            int length = value?.Length ?? 0;
            int clampedCaretIndex = Math.Clamp(caretIndex, 0, length);
            foreach (int stop in EnumerateCaretStops(value))
            {
                if (stop > clampedCaretIndex)
                {
                    return stop;
                }
            }

            return length;
        }
    }
}
