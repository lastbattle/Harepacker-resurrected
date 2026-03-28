using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using static HaCreator.MapSimulator.UI.CharacterSelectWindow;
using AnimationFrame = HaCreator.MapSimulator.UI.CharacterSelectWindow.AnimationFrame;

namespace HaCreator.MapSimulator.UI
{
    public sealed class LoginCreateCharacterWindow : UIWindowBase
    {
        private static readonly Rectangle RaceListBounds = new(35, 48, 160, 202);
        private static readonly Rectangle JobListBounds = new(34, 58, 160, 112);
        private static readonly Rectangle AvatarListBounds = new(250, 84, 220, 148);
        private static readonly Rectangle NameDisplayBounds = new(242, 42, 208, 28);
        private static readonly Rectangle PreviewBounds = new(56, 84, 160, 190);
        private static readonly Rectangle StatusBounds = new(18, 265, 210, 66);
        private static readonly Color SelectionFillColor = new(255, 236, 176, 96);
        private static readonly Color SelectionOutlineColor = new(255, 236, 176);
        private static readonly Color MutedTextColor = new(188, 188, 188);
        private static readonly Color StatusTextColor = new(238, 226, 193);
        private static readonly string[] AvatarPartLabels =
        {
            "Face",
            "Hair",
            "Skin",
            "Top",
            "Bottom",
            "Shoes",
            "Weapon"
        };

        private readonly Dictionary<LoginCreateCharacterStage, Texture2D> _framesByStage;
        private readonly IReadOnlyList<Texture2D> _jobTextures;
        private readonly IReadOnlyList<Texture2D> _avatarEnabledTextures;
        private readonly IReadOnlyList<Texture2D> _avatarDisabledTextures;
        private readonly IReadOnlyList<CharacterSelectWindow.AnimationFrame> _diceFrames;
        private readonly UIObject _confirmButton;
        private readonly UIObject _cancelButton;
        private readonly UIObject _checkButton;
        private SpriteFont _font;
        private LoginCreateCharacterStage _stage;
        private IReadOnlyList<string> _raceLabels = Array.Empty<string>();
        private int _selectedRaceIndex;
        private IReadOnlyList<LoginCreateCharacterJobOption> _jobOptions = Array.Empty<LoginCreateCharacterJobOption>();
        private int _selectedJobIndex;
        private string _displayName = string.Empty;
        private string _statusMessage = string.Empty;
        private string _checkedName = string.Empty;
        private CharacterBuild _previewBuild;
        private CharacterAssembler _previewAssembler;
        private int[] _avatarIndices = new int[AvatarPartLabels.Length];
        private CharacterGender _gender = CharacterGender.Male;
        private KeyboardState _previousKeyboardState;

        public LoginCreateCharacterWindow(
            IDXObject frame,
            IReadOnlyDictionary<LoginCreateCharacterStage, Texture2D> framesByStage,
            IReadOnlyList<Texture2D> jobTextures,
            IReadOnlyList<Texture2D> avatarEnabledTextures,
            IReadOnlyList<Texture2D> avatarDisabledTextures,
            IReadOnlyList<CharacterSelectWindow.AnimationFrame> diceFrames,
            UIObject confirmButton,
            UIObject cancelButton,
            UIObject checkButton)
            : base(frame)
        {
            _framesByStage = framesByStage?.ToDictionary(pair => pair.Key, pair => pair.Value)
                ?? new Dictionary<LoginCreateCharacterStage, Texture2D>();
            _jobTextures = jobTextures ?? Array.Empty<Texture2D>();
            _avatarEnabledTextures = avatarEnabledTextures ?? Array.Empty<Texture2D>();
            _avatarDisabledTextures = avatarDisabledTextures ?? Array.Empty<Texture2D>();
            _diceFrames = diceFrames ?? Array.Empty<AnimationFrame>();

            _confirmButton = confirmButton;
            if (_confirmButton != null)
            {
                _confirmButton.ButtonClickReleased += _ => ConfirmRequested?.Invoke();
                AddButton(_confirmButton);
            }

            _cancelButton = cancelButton;
            if (_cancelButton != null)
            {
                _cancelButton.ButtonClickReleased += _ => CancelRequested?.Invoke();
                AddButton(_cancelButton);
            }

            _checkButton = checkButton;
            if (_checkButton != null)
            {
                _checkButton.ButtonClickReleased += _ => DuplicateCheckRequested?.Invoke();
                AddButton(_checkButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.LoginCreateCharacter;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => true;

        public event Action<int> RaceSelected;
        public event Action<int> JobSelected;
        public event Action<LoginCreateCharacterAvatarPart, int> AvatarShiftRequested;
        public event Action GenderToggleRequested;
        public event Action DiceRequested;
        public event Action NameEditRequested;
        public event Action ConfirmRequested;
        public event Action CancelRequested;
        public event Action DuplicateCheckRequested;
        public event Action<string> NameChanged;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Configure(
            LoginCreateCharacterFlowState state,
            CharacterBuild previewBuild)
        {
            _stage = state?.Stage ?? LoginCreateCharacterStage.RaceSelect;
            _raceLabels = LoginCreateCharacterFlowState.SupportedRaces
                .Select(LoginCreateCharacterFlowState.GetRaceLabel)
                .ToArray();
            _selectedRaceIndex = state?.SelectedRaceIndex ?? 0;
            _jobOptions = state?.CurrentJobs ?? Array.Empty<LoginCreateCharacterJobOption>();
            _selectedJobIndex = state?.SelectedJobIndex ?? 0;
            _displayName = state?.EnteredName ?? string.Empty;
            _checkedName = state?.CheckedName ?? string.Empty;
            _statusMessage = state?.StatusMessage ?? string.Empty;
            _gender = state?.SelectedGender ?? CharacterGender.Male;
            _avatarIndices = new[]
            {
                state?.SelectedFaceIndex ?? 0,
                state?.SelectedHairIndex ?? 0,
                state?.SelectedSkinIndex ?? 0,
                state?.SelectedCoatIndex ?? 0,
                state?.SelectedPantsIndex ?? 0,
                state?.SelectedShoesIndex ?? 0,
                state?.SelectedWeaponIndex ?? 0
            };

            _previewBuild = previewBuild;
            _previewAssembler = previewBuild != null ? new CharacterAssembler(previewBuild) : null;
            ConfigureButtons();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboardState = Keyboard.GetState();
            if (_stage != LoginCreateCharacterStage.NameSelect || !IsVisible)
            {
                _previousKeyboardState = keyboardState;
                return;
            }

            if (Pressed(keyboardState, Keys.Enter))
            {
                DuplicateCheckRequested?.Invoke();
            }

            if (Pressed(keyboardState, Keys.Escape))
            {
                CancelRequested?.Invoke();
            }

            if (Pressed(keyboardState, Keys.Back) && _displayName.Length > 0)
            {
                NameChanged?.Invoke(_displayName[..^1]);
            }

            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in Enumerable.Range((int)Keys.A, 26).Select(value => (Keys)value))
            {
                if (Pressed(keyboardState, key))
                {
                    char character = (char)('a' + (key - Keys.A));
                    AppendNameCharacter(shift ? char.ToUpperInvariant(character) : character);
                }
            }

            foreach (Keys key in Enumerable.Range((int)Keys.D0, 10).Select(value => (Keys)value))
            {
                if (Pressed(keyboardState, key))
                {
                    AppendNameCharacter((char)('0' + (key - Keys.D0)));
                }
            }

            _previousKeyboardState = keyboardState;
        }

        protected override void DrawContents(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (_framesByStage.TryGetValue(_stage, out Texture2D stageTexture) && stageTexture != null)
            {
                sprite.Draw(stageTexture, Position.ToVector2(), Color.White);
            }

            if (_font == null)
            {
                return;
            }

            DrawStageText(sprite);
            if (_stage == LoginCreateCharacterStage.JobSelect)
            {
                DrawJobOptions(sprite);
            }
            else if (_stage == LoginCreateCharacterStage.AvatarSelect)
            {
                DrawPreview(sprite, skeletonMeshRenderer, tickCount);
                DrawAvatarOptions(sprite, tickCount);
            }
            else if (_stage == LoginCreateCharacterStage.NameSelect)
            {
                DrawNameEntry(sprite);
            }
            else
            {
                DrawRaceOptions(sprite);
            }
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight))
            {
                return true;
            }

            if (!IsVisible || mouseState.LeftButton != ButtonState.Pressed)
            {
                return false;
            }

            Point localPoint = new(mouseState.X - Position.X, mouseState.Y - Position.Y);
            if (_stage == LoginCreateCharacterStage.RaceSelect)
            {
                int raceIndex = HitTestListItem(localPoint, RaceListBounds, _raceLabels.Count, 32);
                if (raceIndex >= 0)
                {
                    RaceSelected?.Invoke(raceIndex);
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }
            else if (_stage == LoginCreateCharacterStage.JobSelect)
            {
                int jobIndex = HitTestListItem(localPoint, JobListBounds, _jobOptions.Count, 21);
                if (jobIndex >= 0)
                {
                    JobSelected?.Invoke(jobIndex);
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }
            else if (_stage == LoginCreateCharacterStage.AvatarSelect)
            {
                if (NameDisplayBounds.Contains(localPoint))
                {
                    NameEditRequested?.Invoke();
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }

                if (TryHandleAvatarShift(localPoint, mouseCursor))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryHandleAvatarShift(Point localPoint, MouseCursorItem mouseCursor)
        {
            const int rowHeight = 20;
            const int leftX = 222;
            const int rightX = 476;
            const int arrowWidth = 18;
            const int genderRowIndex = 7;

            for (int row = 0; row < AvatarPartLabels.Length + 1; row++)
            {
                int rowY = AvatarListBounds.Y + (row * rowHeight);
                Rectangle leftArrow = new(leftX, rowY, arrowWidth, rowHeight);
                Rectangle rightArrow = new(rightX, rowY, arrowWidth, rowHeight);
                if (!leftArrow.Contains(localPoint) && !rightArrow.Contains(localPoint))
                {
                    continue;
                }

                if (row == genderRowIndex)
                {
                    GenderToggleRequested?.Invoke();
                }
                else if (row < AvatarPartLabels.Length)
                {
                    AvatarShiftRequested?.Invoke((LoginCreateCharacterAvatarPart)row, leftArrow.Contains(localPoint) ? -1 : 1);
                }
                else
                {
                    DiceRequested?.Invoke();
                }

                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            return false;
        }

        private void DrawStageText(SpriteBatch sprite)
        {
            DrawParagraph(sprite, _statusMessage, new Rectangle(Position.X + StatusBounds.X, Position.Y + StatusBounds.Y, StatusBounds.Width, StatusBounds.Height), StatusTextColor);
        }

        private void DrawRaceOptions(SpriteBatch sprite)
        {
            for (int i = 0; i < _raceLabels.Count; i++)
            {
                Rectangle itemBounds = new(Position.X + RaceListBounds.X, Position.Y + RaceListBounds.Y + (i * 32), RaceListBounds.Width, 28);
                DrawSelectableTextRow(sprite, itemBounds, _raceLabels[i], i == _selectedRaceIndex);
            }
        }

        private void DrawJobOptions(SpriteBatch sprite)
        {
            for (int i = 0; i < _jobOptions.Count; i++)
            {
                Rectangle itemBounds = new(Position.X + JobListBounds.X, Position.Y + JobListBounds.Y + (i * 21), JobListBounds.Width, 18);
                bool selected = i == _selectedJobIndex;
                if (selected)
                {
                    DrawSelection(sprite, itemBounds);
                }

                Texture2D labelTexture = i < _jobTextures.Count ? _jobTextures[i] : null;
                if (labelTexture != null)
                {
                    sprite.Draw(labelTexture, new Vector2(itemBounds.X, itemBounds.Y), selected ? Color.White : new Color(200, 200, 200));
                }
                else
                {
                    SelectorWindowDrawing.DrawShadowedText(sprite, _font, _jobOptions[i].Label, new Vector2(itemBounds.X + 6, itemBounds.Y), selected ? Color.White : MutedTextColor);
                }
            }
        }

        private void DrawAvatarOptions(SpriteBatch sprite, int tickCount)
        {
            DrawNameDisplay(sprite);

            for (int i = 0; i < AvatarPartLabels.Length; i++)
            {
                Rectangle rowBounds = new(Position.X + AvatarListBounds.X, Position.Y + AvatarListBounds.Y + (i * 20), AvatarListBounds.Width, 17);
                Texture2D labelTexture = i < _avatarEnabledTextures.Count
                    ? _avatarEnabledTextures[i]
                    : null;
                Texture2D disabledTexture = i < _avatarDisabledTextures.Count
                    ? _avatarDisabledTextures[i]
                    : null;
                sprite.Draw(disabledTexture ?? labelTexture, new Vector2(rowBounds.X, rowBounds.Y), Color.White);
                DrawArrowLabel(sprite, new Vector2(Position.X + 224, rowBounds.Y), "<");
                DrawArrowLabel(sprite, new Vector2(Position.X + 480, rowBounds.Y), ">");
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"{AvatarPartLabels[i]} {_avatarIndices[i] + 1}",
                    new Vector2(rowBounds.X + 14, rowBounds.Y),
                    Color.White);
            }

            Rectangle genderBounds = new(Position.X + AvatarListBounds.X, Position.Y + AvatarListBounds.Y + (AvatarPartLabels.Length * 20), AvatarListBounds.Width, 17);
            DrawArrowLabel(sprite, new Vector2(Position.X + 224, genderBounds.Y), "<");
            DrawArrowLabel(sprite, new Vector2(Position.X + 480, genderBounds.Y), ">");
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                $"Gender {_gender}",
                new Vector2(genderBounds.X + 14, genderBounds.Y),
                Color.White);

            Rectangle diceBounds = new(Position.X + AvatarListBounds.X, Position.Y + AvatarListBounds.Y + ((AvatarPartLabels.Length + 1) * 20), AvatarListBounds.Width, 22);
            DrawArrowLabel(sprite, new Vector2(Position.X + 224, diceBounds.Y), "<");
            DrawArrowLabel(sprite, new Vector2(Position.X + 480, diceBounds.Y), ">");
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, "Dice randomize", new Vector2(diceBounds.X + 14, diceBounds.Y), new Color(255, 238, 165));

            AnimationFrame diceFrame = ResolveAnimationFrame(_diceFrames, tickCount);
            if (diceFrame.Texture != null)
            {
                sprite.Draw(diceFrame.Texture, new Vector2(Position.X + 504 + diceFrame.Offset.X, Position.Y + 170 + diceFrame.Offset.Y), Color.White);
            }
        }

        private void DrawNameDisplay(SpriteBatch sprite)
        {
            Rectangle displayBounds = new(Position.X + NameDisplayBounds.X, Position.Y + NameDisplayBounds.Y, NameDisplayBounds.Width, NameDisplayBounds.Height);
            DrawSelection(sprite, displayBounds);
            string nameLabel = string.IsNullOrWhiteSpace(_displayName)
                ? "Click here to enter a name"
                : _displayName;
            Color textColor = string.IsNullOrWhiteSpace(_displayName) ? MutedTextColor : Color.White;
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, nameLabel, new Vector2(displayBounds.X + 6, displayBounds.Y + 6), textColor);

            if (!string.IsNullOrWhiteSpace(_checkedName))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    "Checked",
                    new Vector2(displayBounds.Right - 62, displayBounds.Y + 6),
                    new Color(145, 232, 145));
            }
        }

        private void DrawNameEntry(SpriteBatch sprite)
        {
            Rectangle inputBounds = new(Position.X + 34, Position.Y + 94, 130, 24);
            DrawSelection(sprite, inputBounds);
            string text = string.IsNullOrWhiteSpace(_displayName) ? "Type a name" : _displayName;
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                text,
                new Vector2(inputBounds.X + 6, inputBounds.Y + 4),
                string.IsNullOrWhiteSpace(_displayName) ? MutedTextColor : Color.White);

            if (!string.IsNullOrWhiteSpace(_checkedName))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Last checked: {_checkedName}",
                    new Vector2(Position.X + 34, Position.Y + 130),
                    new Color(145, 232, 145));
            }
        }

        private void DrawPreview(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, int tickCount)
        {
            if (_previewAssembler == null)
            {
                return;
            }

            AssembledFrame frame = _previewAssembler.GetFrameAtTime("stand1", tickCount);
            if (frame == null)
            {
                return;
            }

            int anchorX = Position.X + PreviewBounds.X + (PreviewBounds.Width / 2);
            int anchorY = Position.Y + PreviewBounds.Bottom - 6;
            frame.Draw(sprite, skeletonMeshRenderer, anchorX, anchorY, false, Color.White);
        }

        private void DrawParagraph(SpriteBatch sprite, string text, Rectangle bounds, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 cursor = new(bounds.X, bounds.Y);
            foreach (string line in WrapText(text, bounds.Width))
            {
                SelectorWindowDrawing.DrawShadowedText(sprite, _font, line, cursor, color);
                cursor.Y += _font.LineSpacing;
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string currentLine = string.Empty;
            foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                if (_font.MeasureString(candidate).X <= maxWidth)
                {
                    currentLine = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    yield return currentLine;
                }

                currentLine = word;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }

        private void DrawSelectableTextRow(SpriteBatch sprite, Rectangle bounds, string label, bool selected)
        {
            if (selected)
            {
                DrawSelection(sprite, bounds);
            }

            SelectorWindowDrawing.DrawShadowedText(sprite, _font, label, new Vector2(bounds.X + 6, bounds.Y + 6), selected ? Color.White : MutedTextColor);
        }

        private void DrawSelection(SpriteBatch sprite, Rectangle bounds)
        {
            sprite.Draw(Frame.Texture, bounds, SelectionFillColor);
            sprite.Draw(Frame.Texture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), SelectionOutlineColor);
            sprite.Draw(Frame.Texture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), SelectionOutlineColor);
            sprite.Draw(Frame.Texture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), SelectionOutlineColor);
            sprite.Draw(Frame.Texture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), SelectionOutlineColor);
        }

        private void DrawArrowLabel(SpriteBatch sprite, Vector2 position, string text)
        {
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, text, position, new Color(255, 238, 165));
        }

        private void ConfigureButtons()
        {
            if (_confirmButton != null)
            {
                _confirmButton.SetVisible(_stage != LoginCreateCharacterStage.NameSelect);
                _confirmButton.SetEnabled(_stage != LoginCreateCharacterStage.AvatarSelect || !string.IsNullOrWhiteSpace(_checkedName));
                _confirmButton.X = _stage == LoginCreateCharacterStage.RaceSelect ? 92 : 82;
                _confirmButton.Y = _stage == LoginCreateCharacterStage.RaceSelect ? 248 : 248;
            }

            if (_cancelButton != null)
            {
                _cancelButton.SetVisible(true);
                _cancelButton.SetEnabled(true);
                _cancelButton.X = _stage == LoginCreateCharacterStage.NameSelect ? 116 : 178;
                _cancelButton.Y = _stage == LoginCreateCharacterStage.NameSelect ? 162 : 248;
            }

            if (_checkButton != null)
            {
                _checkButton.SetVisible(_stage == LoginCreateCharacterStage.NameSelect);
                _checkButton.SetEnabled(_stage == LoginCreateCharacterStage.NameSelect && !string.IsNullOrWhiteSpace(_displayName));
                _checkButton.X = 52;
                _checkButton.Y = 162;
            }
        }

        private void AppendNameCharacter(char character)
        {
            if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
            {
                return;
            }

            if (_displayName.Length >= 13)
            {
                return;
            }

            NameChanged?.Invoke(_displayName + character);
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private static int HitTestListItem(Point point, Rectangle bounds, int itemCount, int rowHeight)
        {
            if (!bounds.Contains(point))
            {
                return -1;
            }

            int index = (point.Y - bounds.Y) / rowHeight;
            return index >= 0 && index < itemCount ? index : -1;
        }

        private static CharacterSelectWindow.AnimationFrame ResolveAnimationFrame(IReadOnlyList<CharacterSelectWindow.AnimationFrame> frames, int tickCount)
        {
            if (frames == null || frames.Count == 0)
            {
                return default;
            }

            int totalDelay = frames.Sum(frame => Math.Max(1, frame.Delay));
            if (totalDelay <= 0)
            {
                return frames[0];
            }

            int time = Math.Abs(tickCount % totalDelay);
            int accumulated = 0;
            foreach (AnimationFrame frame in frames)
            {
                accumulated += Math.Max(1, frame.Delay);
                if (time < accumulated)
                {
                    return frame;
                }
            }

            return frames[^1];
        }
    }
}
