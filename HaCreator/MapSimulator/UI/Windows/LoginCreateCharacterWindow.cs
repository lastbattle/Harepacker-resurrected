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
        private static readonly Point StageBasePosition = new(220, 180);
        private static readonly Point RaceStatusPosition = new(224, 420);
        private static readonly Rectangle RaceStatusBounds = new(0, 0, 350, 78);
        private static readonly IReadOnlyDictionary<LoginCreateCharacterRaceKind, Rectangle> RaceCardBoundsByRace =
            new Dictionary<LoginCreateCharacterRaceKind, Rectangle>
            {
                [LoginCreateCharacterRaceKind.Explorer] = new(45, 43, 187, 120),
                [LoginCreateCharacterRaceKind.Cygnus] = new(284, 43, 187, 120),
                [LoginCreateCharacterRaceKind.Aran] = new(524, 43, 187, 120),
                [LoginCreateCharacterRaceKind.Evan] = new(45, 295, 187, 120),
                [LoginCreateCharacterRaceKind.Resistance] = new(284, 295, 187, 120)
            };
        private static readonly Rectangle RaceListBounds = new(22, 28, 164, 174);
        private static readonly Rectangle JobListBounds = new(64, 102, 72, 110);
        private static readonly Rectangle AvatarListBounds = new(12, 102, 200, 162);
        private static readonly Rectangle AvatarDiceBounds = new(172, 286, 37, 56);
        private static readonly Rectangle NameDisplayBounds = new(44, 286, 132, 24);
        private static readonly Rectangle NameInputBounds = new(31, 79, 138, 24);
        private static readonly Rectangle PreviewBounds = new(260, 82, 160, 190);
        private static readonly Rectangle StatusBounds = new(232, 282, 252, 68);
        private static readonly Rectangle[] AvatarArrowOffsets =
        {
            new(0, 0, 15, 16),
            new(210, 0, 15, 16)
        };
        private static readonly Color SelectionFillColor = new(255, 236, 176, 96);
        private static readonly Color SelectionOutlineColor = new(255, 236, 176);
        private static readonly Color MutedTextColor = new(188, 188, 188);
        private static readonly Color StatusTextColor = new(238, 226, 193);

        private readonly Dictionary<LoginCreateCharacterRaceKind, Dictionary<LoginCreateCharacterStage, Texture2D>> _framesByRaceAndStage;
        private readonly IReadOnlyList<Texture2D> _jobTextures;
        private readonly Dictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> _avatarEnabledTexturesByRace;
        private readonly Dictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> _avatarDisabledTexturesByRace;
        private readonly Dictionary<LoginCreateCharacterRaceKind, Texture2D> _racePreviewTexturesByRace;
        private readonly IReadOnlyList<CharacterSelectWindow.AnimationFrame> _diceFrames;
        private readonly Texture2D _leftArrowTexture;
        private readonly Texture2D _rightArrowTexture;
        private readonly UIObject _confirmButton;
        private readonly UIObject _cancelButton;
        private readonly UIObject _checkButton;
        private SpriteFont _font;
        private LoginCreateCharacterStage _stage;
        private IReadOnlyList<string> _raceLabels = Array.Empty<string>();
        private int _selectedRaceIndex;
        private LoginCreateCharacterRaceKind _selectedRace = LoginCreateCharacterRaceKind.Explorer;
        private IReadOnlyList<LoginCreateCharacterJobOption> _jobOptions = Array.Empty<LoginCreateCharacterJobOption>();
        private int _selectedJobIndex;
        private string _displayName = string.Empty;
        private string _statusMessage = string.Empty;
        private string _checkedName = string.Empty;
        private CharacterBuild _previewBuild;
        private CharacterAssembler _previewAssembler;
        private int[] _avatarIndices = new int[8];
        private CharacterGender _gender = CharacterGender.Male;
        private KeyboardState _previousKeyboardState;

        public LoginCreateCharacterWindow(
            IDXObject frame,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyDictionary<LoginCreateCharacterStage, Texture2D>> framesByRaceAndStage,
            IReadOnlyList<Texture2D> jobTextures,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarEnabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarDisabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, Texture2D> racePreviewTexturesByRace,
            IReadOnlyList<CharacterSelectWindow.AnimationFrame> diceFrames,
            Texture2D leftArrowTexture,
            Texture2D rightArrowTexture,
            UIObject confirmButton,
            UIObject cancelButton,
            UIObject checkButton)
            : base(frame)
        {
            _framesByRaceAndStage = framesByRaceAndStage?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.ToDictionary(stagePair => stagePair.Key, stagePair => stagePair.Value) ?? new Dictionary<LoginCreateCharacterStage, Texture2D>())
                ?? new Dictionary<LoginCreateCharacterRaceKind, Dictionary<LoginCreateCharacterStage, Texture2D>>();
            _jobTextures = jobTextures ?? Array.Empty<Texture2D>();
            _avatarEnabledTexturesByRace = avatarEnabledTexturesByRace?.ToDictionary(pair => pair.Key, pair => pair.Value ?? Array.Empty<Texture2D>())
                ?? new Dictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>>();
            _avatarDisabledTexturesByRace = avatarDisabledTexturesByRace?.ToDictionary(pair => pair.Key, pair => pair.Value ?? Array.Empty<Texture2D>())
                ?? new Dictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>>();
            _racePreviewTexturesByRace = racePreviewTexturesByRace?.ToDictionary(pair => pair.Key, pair => pair.Value)
                ?? new Dictionary<LoginCreateCharacterRaceKind, Texture2D>();
            _diceFrames = diceFrames ?? Array.Empty<AnimationFrame>();
            _leftArrowTexture = leftArrowTexture;
            _rightArrowTexture = rightArrowTexture;

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
            _selectedRace = state?.SelectedRace ?? LoginCreateCharacterRaceKind.Explorer;
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
                state?.SelectedHairColorIndex ?? 0,
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
            if (TryGetStageTexture(out Texture2D stageTexture) && stageTexture != null)
            {
                Point stageOrigin = GetStageOrigin();
                sprite.Draw(stageTexture, new Vector2(Position.X + stageOrigin.X, Position.Y + stageOrigin.Y), Color.White);
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
            else if (_stage == LoginCreateCharacterStage.RaceSelect)
            {
                DrawRaceCards(sprite);
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
                foreach (KeyValuePair<LoginCreateCharacterRaceKind, Rectangle> raceCard in RaceCardBoundsByRace)
                {
                    if (raceCard.Value.Contains(localPoint))
                    {
                        int raceIndex = Array.IndexOf(LoginCreateCharacterFlowState.SupportedRaces, raceCard.Key);
                        if (raceIndex >= 0)
                        {
                            RaceSelected?.Invoke(raceIndex);
                            mouseCursor?.SetMouseCursorMovedToClickableItem();
                            return true;
                        }
                    }
                }

                Point stageOrigin = GetStageOrigin();
                int selectedRaceIndex = HitTestListItem(
                    localPoint,
                    new Rectangle(stageOrigin.X + RaceListBounds.X, stageOrigin.Y + RaceListBounds.Y, RaceListBounds.Width, RaceListBounds.Height),
                    _raceLabels.Count,
                    32);
                if (selectedRaceIndex >= 0)
                {
                    RaceSelected?.Invoke(selectedRaceIndex);
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }
            else if (_stage == LoginCreateCharacterStage.JobSelect)
            {
                Point stageOrigin = GetStageOrigin();
                int jobIndex = HitTestListItem(
                    localPoint,
                    new Rectangle(stageOrigin.X + JobListBounds.X, stageOrigin.Y + JobListBounds.Y, JobListBounds.Width, JobListBounds.Height),
                    _jobOptions.Count,
                    21);
                if (jobIndex >= 0)
                {
                    JobSelected?.Invoke(jobIndex);
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }
            else if (_stage == LoginCreateCharacterStage.AvatarSelect)
            {
                Point stageOrigin = GetStageOrigin();
                Rectangle nameDisplayBounds = new(stageOrigin.X + NameDisplayBounds.X, stageOrigin.Y + NameDisplayBounds.Y, NameDisplayBounds.Width, NameDisplayBounds.Height);
                if (nameDisplayBounds.Contains(localPoint))
                {
                    NameEditRequested?.Invoke();
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }

                Rectangle avatarDiceBounds = new(stageOrigin.X + AvatarDiceBounds.X, stageOrigin.Y + AvatarDiceBounds.Y, AvatarDiceBounds.Width, AvatarDiceBounds.Height);
                if (avatarDiceBounds.Contains(localPoint))
                {
                    DiceRequested?.Invoke();
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
            const int rowHeight = 18;
            Point stageOrigin = GetStageOrigin();

            for (int row = 0; row < _avatarIndices.Length + 1; row++)
            {
                int rowY = stageOrigin.Y + AvatarListBounds.Y + (row * rowHeight);
                Rectangle leftArrow = new(stageOrigin.X + AvatarListBounds.X + AvatarArrowOffsets[0].X, rowY, AvatarArrowOffsets[0].Width, AvatarArrowOffsets[0].Height);
                Rectangle rightArrow = new(stageOrigin.X + AvatarListBounds.X + AvatarArrowOffsets[1].X, rowY, AvatarArrowOffsets[1].Width, AvatarArrowOffsets[1].Height);
                if (!leftArrow.Contains(localPoint) && !rightArrow.Contains(localPoint))
                {
                    continue;
                }

                if (row == _avatarIndices.Length)
                {
                    GenderToggleRequested?.Invoke();
                }
                else
                {
                    AvatarShiftRequested?.Invoke((LoginCreateCharacterAvatarPart)row, leftArrow.Contains(localPoint) ? -1 : 1);
                }

                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            return false;
        }

        private void DrawStageText(SpriteBatch sprite)
        {
            Point stageOrigin = GetStageOrigin();
            Rectangle stageStatusBounds = _stage == LoginCreateCharacterStage.RaceSelect
                ? new Rectangle(Position.X + RaceStatusPosition.X, Position.Y + RaceStatusPosition.Y, RaceStatusBounds.Width, RaceStatusBounds.Height)
                : new Rectangle(Position.X + stageOrigin.X + StatusBounds.X, Position.Y + stageOrigin.Y + StatusBounds.Y, StatusBounds.Width, StatusBounds.Height);
            DrawParagraph(sprite, _statusMessage, stageStatusBounds, StatusTextColor);
        }

        private void DrawRaceOptions(SpriteBatch sprite)
        {
            Point stageOrigin = GetStageOrigin();
            for (int i = 0; i < _raceLabels.Count; i++)
            {
                Rectangle itemBounds = new(Position.X + stageOrigin.X + RaceListBounds.X, Position.Y + stageOrigin.Y + RaceListBounds.Y + (i * 32), RaceListBounds.Width, 28);
                DrawSelectableTextRow(sprite, itemBounds, _raceLabels[i], i == _selectedRaceIndex);
            }
        }

        private void DrawJobOptions(SpriteBatch sprite)
        {
            Point stageOrigin = GetStageOrigin();
            for (int i = 0; i < _jobOptions.Count; i++)
            {
                Rectangle itemBounds = new(Position.X + stageOrigin.X + JobListBounds.X, Position.Y + stageOrigin.Y + JobListBounds.Y + (i * 21), JobListBounds.Width, 18);
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
            IReadOnlyList<Texture2D> enabledTextures = ResolveAvatarTextures(_avatarEnabledTexturesByRace);
            IReadOnlyList<Texture2D> disabledTextures = ResolveAvatarTextures(_avatarDisabledTexturesByRace);
            Point stageOrigin = GetStageOrigin();

            for (int i = 0; i < _avatarIndices.Length; i++)
            {
                Rectangle rowBounds = new(Position.X + stageOrigin.X + AvatarListBounds.X, Position.Y + stageOrigin.Y + AvatarListBounds.Y + (i * 18), AvatarListBounds.Width, 17);
                Texture2D labelTexture = i < enabledTextures.Count
                    ? enabledTextures[i]
                    : null;
                Texture2D disabledTexture = i < disabledTextures.Count
                    ? disabledTextures[i]
                    : null;
                sprite.Draw(disabledTexture ?? labelTexture, new Vector2(rowBounds.X, rowBounds.Y), Color.White);
                DrawArrowTexture(sprite, _leftArrowTexture, new Vector2(Position.X + stageOrigin.X + AvatarListBounds.X, rowBounds.Y));
                DrawArrowTexture(sprite, _rightArrowTexture, new Vector2(Position.X + stageOrigin.X + AvatarListBounds.X + 210, rowBounds.Y));
                DrawAvatarValueLabel(sprite, i, rowBounds);
            }

            Rectangle genderBounds = new(Position.X + stageOrigin.X + AvatarListBounds.X, Position.Y + stageOrigin.Y + AvatarListBounds.Y + (_avatarIndices.Length * 18), AvatarListBounds.Width, 17);
            DrawArrowTexture(sprite, _leftArrowTexture, new Vector2(Position.X + stageOrigin.X + AvatarListBounds.X, genderBounds.Y));
            DrawArrowTexture(sprite, _rightArrowTexture, new Vector2(Position.X + stageOrigin.X + AvatarListBounds.X + 210, genderBounds.Y));
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                _gender == CharacterGender.Male ? "Male" : "Female",
                new Vector2(genderBounds.Right - 52, genderBounds.Y),
                Color.White);

            AnimationFrame diceFrame = ResolveAnimationFrame(_diceFrames, tickCount);
            if (diceFrame.Texture != null)
            {
                sprite.Draw(
                    diceFrame.Texture,
                    new Vector2(Position.X + stageOrigin.X + AvatarDiceBounds.X + diceFrame.Offset.X, Position.Y + stageOrigin.Y + AvatarDiceBounds.Y + diceFrame.Offset.Y),
                    Color.White);
            }
        }

        private void DrawNameDisplay(SpriteBatch sprite)
        {
            Point stageOrigin = GetStageOrigin();
            Rectangle displayBounds = new(Position.X + stageOrigin.X + NameDisplayBounds.X, Position.Y + stageOrigin.Y + NameDisplayBounds.Y, NameDisplayBounds.Width, NameDisplayBounds.Height);
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
            Point stageOrigin = GetStageOrigin();
            Rectangle inputBounds = new(Position.X + stageOrigin.X + NameInputBounds.X, Position.Y + stageOrigin.Y + NameInputBounds.Y, NameInputBounds.Width, NameInputBounds.Height);
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
                    new Vector2(Position.X + stageOrigin.X + 34, Position.Y + stageOrigin.Y + 130),
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
            Point stageOrigin = GetStageOrigin();
            anchorX += stageOrigin.X;
            int anchorY = Position.Y + stageOrigin.Y + PreviewBounds.Bottom - 6;
            frame.Draw(sprite, skeletonMeshRenderer, anchorX, anchorY, false, Color.White);
        }

        private void DrawRaceCards(SpriteBatch sprite)
        {
            foreach (LoginCreateCharacterRaceKind race in LoginCreateCharacterFlowState.SupportedRaces)
            {
                if (!RaceCardBoundsByRace.TryGetValue(race, out Rectangle bounds))
                {
                    continue;
                }

                Rectangle drawBounds = new(Position.X + bounds.X, Position.Y + bounds.Y, bounds.Width, bounds.Height);
                if (_racePreviewTexturesByRace.TryGetValue(race, out Texture2D texture) && texture != null)
                {
                    sprite.Draw(texture, drawBounds.Location.ToVector2(), Color.White);
                }
                else
                {
                    DrawSelectableTextRow(sprite, drawBounds, LoginCreateCharacterFlowState.GetRaceLabel(race), race == _selectedRace);
                }

                if (race == _selectedRace)
                {
                    DrawSelection(sprite, drawBounds);
                }
            }
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
                Point stageOrigin = GetStageOrigin();
                _confirmButton.X = stageOrigin.X + (_stage == LoginCreateCharacterStage.RaceSelect ? 26 : 18);
                _confirmButton.Y = stageOrigin.Y + (_stage == LoginCreateCharacterStage.RaceSelect ? 144 : 238);
            }

            if (_cancelButton != null)
            {
                _cancelButton.SetVisible(true);
                _cancelButton.SetEnabled(true);
                Point stageOrigin = GetStageOrigin();
                _cancelButton.X = stageOrigin.X + (_stage == LoginCreateCharacterStage.RaceSelect ? 110 : 102);
                _cancelButton.Y = stageOrigin.Y + (_stage == LoginCreateCharacterStage.RaceSelect ? 144 : 238);
            }

            if (_checkButton != null)
            {
                _checkButton.SetVisible(_stage == LoginCreateCharacterStage.NameSelect);
                _checkButton.SetEnabled(_stage == LoginCreateCharacterStage.NameSelect && !string.IsNullOrWhiteSpace(_displayName));
                Point stageOrigin = GetStageOrigin();
                _checkButton.X = stageOrigin.X + 20;
                _checkButton.Y = stageOrigin.Y + 188;
            }
        }

        private void DrawAvatarValueLabel(SpriteBatch sprite, int index, Rectangle rowBounds)
        {
            string valueText = $"{_avatarIndices[index] + 1}";
            if (index == (int)LoginCreateCharacterAvatarPart.HairColor)
            {
                valueText = $"{_avatarIndices[index]}";
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                valueText,
                new Vector2(rowBounds.Right - 24, rowBounds.Y),
                Color.White);
        }

        private void DrawArrowTexture(SpriteBatch sprite, Texture2D texture, Vector2 position)
        {
            if (texture != null)
            {
                sprite.Draw(texture, position, Color.White);
                return;
            }

            DrawArrowLabel(sprite, position, ">");
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

        private Point GetStageOrigin()
        {
            return StageBasePosition;
        }

        private bool TryGetStageTexture(out Texture2D texture)
        {
            texture = null;
            if (_framesByRaceAndStage.TryGetValue(_selectedRace, out Dictionary<LoginCreateCharacterStage, Texture2D> raceStages) &&
                raceStages.TryGetValue(_stage, out texture) &&
                texture != null)
            {
                return true;
            }

            return _framesByRaceAndStage.TryGetValue(LoginCreateCharacterRaceKind.Explorer, out Dictionary<LoginCreateCharacterStage, Texture2D> explorerStages) &&
                   explorerStages.TryGetValue(_stage, out texture) &&
                   texture != null;
        }

        private IReadOnlyList<Texture2D> ResolveAvatarTextures(Dictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> texturesByRace)
        {
            if (texturesByRace.TryGetValue(_selectedRace, out IReadOnlyList<Texture2D> textures) &&
                textures != null &&
                textures.Count > 0)
            {
                return textures;
            }

            return texturesByRace.TryGetValue(LoginCreateCharacterRaceKind.Explorer, out textures)
                ? textures
                : Array.Empty<Texture2D>();
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
