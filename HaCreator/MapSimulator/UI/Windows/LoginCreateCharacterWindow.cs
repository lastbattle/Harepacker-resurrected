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
    public abstract class LoginCreateCharacterWindowBase : UIWindowBase, ISoftKeyboardHost
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
        private static readonly Rectangle AvatarListBounds = new(66, 103, 147, 162);
        private static readonly Rectangle AvatarDiceBounds = new(172, 286, 37, 56);
        private static readonly Rectangle NameDisplayBounds = new(44, 286, 132, 24);
        private static readonly Rectangle NameInputBounds = new(36, 108, 120, 15);
        private static readonly Rectangle PreviewBounds = new(260, 82, 160, 190);
        private static readonly Rectangle StatusBounds = new(232, 282, 252, 68);
        private static readonly Rectangle AvatarActionPanelBounds = new(24, 324, 177, 53);
        private static readonly Rectangle NameActionPanelBounds = new(15, 171, 177, 53);
        private static readonly Point RaceConfirmDialogOffset = new(303, 204);
        private static readonly Rectangle RaceConfirmDialogBounds = new(0, 0, 214, 112);
        private static readonly Rectangle RaceConfirmOkBounds = new(42, 77, 50, 23);
        private static readonly Rectangle RaceConfirmCancelBounds = new(114, 77, 50, 23);
        private const int ActionPanelSlotWidth = 81;
        private const int ActionPanelSlotGap = 15;
        private static readonly Rectangle[] AvatarArrowOffsets =
        {
            new(0, 0, 15, 16),
            new(132, 0, 15, 16)
        };
        private static readonly Color SelectionFillColor = new(255, 236, 176, 96);
        private static readonly Color SelectionOutlineColor = new(255, 236, 176);
        private static readonly Color MutedTextColor = new(188, 188, 188);
        private static readonly Color StatusTextColor = new(238, 226, 193);
        private const int NameMaxLength = 13;

        private readonly Dictionary<LoginCreateCharacterRaceKind, Dictionary<LoginCreateCharacterStage, Texture2D>> _framesByRaceAndStage;
        private readonly IReadOnlyList<Texture2D> _jobTextures;
        private readonly Dictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> _avatarEnabledTexturesByRace;
        private readonly Dictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> _avatarDisabledTexturesByRace;
        private readonly Dictionary<LoginCreateCharacterRaceKind, Texture2D> _racePreviewTexturesByRace;
        private readonly IReadOnlyList<CharacterSelectWindow.AnimationFrame> _diceFrames;
        private readonly Texture2D _leftArrowTexture;
        private readonly Texture2D _rightArrowTexture;
        private readonly Texture2D _raceConfirmBackgroundTexture;
        private readonly Texture2D _raceConfirmOkTexture;
        private readonly Texture2D _raceConfirmCancelTexture;
        private readonly Dictionary<LoginCreateCharacterRaceKind, Texture2D> _raceConfirmLabelTexturesByRace;
        private readonly UIObject _confirmButton;
        private readonly UIObject _cancelButton;
        private readonly UIObject _checkButton;
        private readonly string _windowName;
        private readonly LoginCreateCharacterStage _fixedStage;
        private readonly Texture2D _pixelTexture;
        private SpriteFont _font;
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
        private bool _isRaceConfirmationOpen;
        private bool _isRaceConfirmAcceptFocused = true;
        private KeyboardState _previousKeyboardState;
        private bool _softKeyboardActive;
        private string _compositionText = string.Empty;
        private ImeCandidateListState _candidateListState = ImeCandidateListState.Empty;

        protected LoginCreateCharacterWindowBase(
            IDXObject frame,
            string windowName,
            LoginCreateCharacterStage fixedStage,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyDictionary<LoginCreateCharacterStage, Texture2D>> framesByRaceAndStage,
            IReadOnlyList<Texture2D> jobTextures,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarEnabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarDisabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, Texture2D> racePreviewTexturesByRace,
            IReadOnlyList<CharacterSelectWindow.AnimationFrame> diceFrames,
            Texture2D leftArrowTexture,
            Texture2D rightArrowTexture,
            Texture2D raceConfirmBackgroundTexture,
            Texture2D raceConfirmOkTexture,
            Texture2D raceConfirmCancelTexture,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, Texture2D> raceConfirmLabelTexturesByRace,
            UIObject confirmButton,
            UIObject cancelButton,
            UIObject checkButton)
            : base(frame)
        {
            _windowName = windowName ?? MapSimulatorWindowNames.LoginCreateCharacter;
            _fixedStage = fixedStage;
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
            _raceConfirmBackgroundTexture = raceConfirmBackgroundTexture;
            _raceConfirmOkTexture = raceConfirmOkTexture;
            _raceConfirmCancelTexture = raceConfirmCancelTexture;
            _raceConfirmLabelTexturesByRace = raceConfirmLabelTexturesByRace?.ToDictionary(pair => pair.Key, pair => pair.Value)
                ?? new Dictionary<LoginCreateCharacterRaceKind, Texture2D>();
            if (frame?.Texture?.GraphicsDevice != null)
            {
                _pixelTexture = new Texture2D(frame.Texture.GraphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }

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
                _checkButton.ButtonClickReleased += _ =>
                {
                    _softKeyboardActive = false;
                    DuplicateCheckRequested?.Invoke();
                };
                AddButton(_checkButton);
            }
        }

        public override string WindowName => _windowName;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => true;
        bool ISoftKeyboardHost.WantsSoftKeyboard => IsVisible && _fixedStage == LoginCreateCharacterStage.NameSelect && _softKeyboardActive;
        SoftKeyboardKeyboardType ISoftKeyboardHost.SoftKeyboardKeyboardType => SoftKeyboardKeyboardType.AlphaNumeric;
        int ISoftKeyboardHost.SoftKeyboardTextLength => _displayName?.Length ?? 0;
        int ISoftKeyboardHost.SoftKeyboardMaxLength => NameMaxLength;
        bool ISoftKeyboardHost.CanSubmitSoftKeyboard => IsNameInputEditable && !string.IsNullOrWhiteSpace(_displayName);

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
            _ = state;
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
            _isRaceConfirmationOpen = state?.IsRaceConfirmationOpen == true;
            _isRaceConfirmAcceptFocused = true;
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
            if (!IsNameInputEditable)
            {
                _softKeyboardActive = false;
                ClearCompositionText();
            }
            else
            {
                _softKeyboardActive = true;
            }

            ConfigureButtons();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboardState = Keyboard.GetState();
            if (!IsVisible)
            {
                _softKeyboardActive = false;
                ClearCompositionText();
                _previousKeyboardState = keyboardState;
                return;
            }

            if (_fixedStage != LoginCreateCharacterStage.NameSelect)
            {
                _softKeyboardActive = false;
                ClearCompositionText();
            }

            switch (_fixedStage)
            {
                case LoginCreateCharacterStage.RaceSelect:
                    HandleRaceSelectKeyboardInput(keyboardState);
                    break;
                case LoginCreateCharacterStage.AvatarSelect:
                    HandleAvatarSelectKeyboardInput(keyboardState);
                    break;
                case LoginCreateCharacterStage.NameSelect:
                    HandleNameSelectKeyboardInput(keyboardState);
                    break;
            }

            _previousKeyboardState = keyboardState;
        }

        private void HandleRaceSelectKeyboardInput(KeyboardState keyboardState)
        {
            if (_isRaceConfirmationOpen)
            {
                if (Pressed(keyboardState, Keys.Tab) ||
                    Pressed(keyboardState, Keys.Left) ||
                    Pressed(keyboardState, Keys.Right) ||
                    Pressed(keyboardState, Keys.Up) ||
                    Pressed(keyboardState, Keys.Down))
                {
                    _isRaceConfirmAcceptFocused = !_isRaceConfirmAcceptFocused;
                }

                if (Pressed(keyboardState, Keys.Enter))
                {
                    if (_isRaceConfirmAcceptFocused)
                    {
                        ConfirmRequested?.Invoke();
                    }
                    else
                    {
                        CancelRequested?.Invoke();
                    }
                }
                else if (Pressed(keyboardState, Keys.Space))
                {
                    if (_isRaceConfirmAcceptFocused)
                    {
                        ConfirmRequested?.Invoke();
                    }
                    else
                    {
                        CancelRequested?.Invoke();
                    }
                }
                else if (Pressed(keyboardState, Keys.Escape))
                {
                    CancelRequested?.Invoke();
                }

                return;
            }

            if (Pressed(keyboardState, Keys.Escape))
            {
                CancelRequested?.Invoke();
            }

            if (Pressed(keyboardState, Keys.Enter))
            {
                ConfirmRequested?.Invoke();
            }

            TryMoveRaceSelection(keyboardState, Keys.Left, new Point(-1, 0));
            TryMoveRaceSelection(keyboardState, Keys.Right, new Point(1, 0));
            TryMoveRaceSelection(keyboardState, Keys.Up, new Point(0, -1));
            TryMoveRaceSelection(keyboardState, Keys.Down, new Point(0, 1));
        }

        private void HandleAvatarSelectKeyboardInput(KeyboardState keyboardState)
        {
            if (Pressed(keyboardState, Keys.Enter))
            {
                ConfirmRequested?.Invoke();
            }

            if (Pressed(keyboardState, Keys.Escape))
            {
                CancelRequested?.Invoke();
            }
        }

        private void HandleNameSelectKeyboardInput(KeyboardState keyboardState)
        {
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
                ClearCompositionText();
                RemoveLastNameCharacter();
            }
        }

        private void TryMoveRaceSelection(KeyboardState keyboardState, Keys key, Point direction)
        {
            if (!Pressed(keyboardState, key))
            {
                return;
            }

            if (TryGetRaceSelectionInDirection(direction, out int raceIndex))
            {
                RaceSelected?.Invoke(raceIndex);
            }
        }

        private bool TryGetRaceSelectionInDirection(Point direction, out int raceIndex)
        {
            raceIndex = -1;
            if (!RaceCardBoundsByRace.TryGetValue(_selectedRace, out Rectangle selectedBounds))
            {
                return false;
            }

            Point selectedCenter = selectedBounds.Center;
            int bestPrimaryDistance = int.MaxValue;
            int bestSecondaryDistance = int.MaxValue;

            for (int i = 0; i < LoginCreateCharacterFlowState.SupportedRaces.Length; i++)
            {
                LoginCreateCharacterRaceKind candidateRace = LoginCreateCharacterFlowState.SupportedRaces[i];
                if (candidateRace == _selectedRace || !RaceCardBoundsByRace.TryGetValue(candidateRace, out Rectangle candidateBounds))
                {
                    continue;
                }

                Point candidateCenter = candidateBounds.Center;
                int deltaX = candidateCenter.X - selectedCenter.X;
                int deltaY = candidateCenter.Y - selectedCenter.Y;
                int primaryDelta = direction.X != 0 ? deltaX * direction.X : deltaY * direction.Y;
                if (primaryDelta <= 0)
                {
                    continue;
                }

                int secondaryDelta = direction.X != 0
                    ? Math.Abs(deltaY)
                    : Math.Abs(deltaX);
                if (primaryDelta < bestPrimaryDistance ||
                    (primaryDelta == bestPrimaryDistance && secondaryDelta < bestSecondaryDistance))
                {
                    bestPrimaryDistance = primaryDelta;
                    bestSecondaryDistance = secondaryDelta;
                    raceIndex = i;
                }
            }

            return raceIndex >= 0;
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
            if (_fixedStage == LoginCreateCharacterStage.JobSelect)
            {
                DrawJobOptions(sprite);
            }
            else if (_fixedStage == LoginCreateCharacterStage.AvatarSelect)
            {
                DrawPreview(sprite, skeletonMeshRenderer, tickCount);
                DrawAvatarOptions(sprite, tickCount);
            }
            else if (_fixedStage == LoginCreateCharacterStage.NameSelect)
            {
                DrawNameEntry(sprite);
            }
            else if (_fixedStage == LoginCreateCharacterStage.RaceSelect)
            {
                DrawRaceCards(sprite);
            }
            else
            {
                DrawRaceOptions(sprite);
            }

            if (_fixedStage == LoginCreateCharacterStage.RaceSelect && _isRaceConfirmationOpen)
            {
                DrawRaceConfirmDialog(sprite);
            }
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            bool handled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            if (!IsVisible)
            {
                return handled;
            }

            if (_fixedStage == LoginCreateCharacterStage.NameSelect && handled && mouseState.LeftButton == ButtonState.Pressed)
            {
                Point handledLocalPoint = new(mouseState.X - Position.X, mouseState.Y - Position.Y);
                if (!GetLocalNameInputBounds().Contains(handledLocalPoint))
                {
                    _softKeyboardActive = false;
                }

                return true;
            }

            if (handled)
            {
                return true;
            }

            if (!IsVisible || mouseState.LeftButton != ButtonState.Pressed)
            {
                return false;
            }

            Point localPoint = new(mouseState.X - Position.X, mouseState.Y - Position.Y);
            if (_fixedStage == LoginCreateCharacterStage.RaceSelect && _isRaceConfirmationOpen)
            {
                return TryHandleRaceConfirmClick(localPoint, mouseCursor);
            }

            if (_fixedStage == LoginCreateCharacterStage.RaceSelect)
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
            else if (_fixedStage == LoginCreateCharacterStage.JobSelect)
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
            else if (_fixedStage == LoginCreateCharacterStage.NameSelect)
            {
                Rectangle nameInputBounds = GetLocalNameInputBounds();
                if (nameInputBounds.Contains(localPoint))
                {
                    _softKeyboardActive = true;
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }

                _softKeyboardActive = false;
            }
            else if (_fixedStage == LoginCreateCharacterStage.AvatarSelect)
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
            Rectangle stageStatusBounds = _fixedStage == LoginCreateCharacterStage.RaceSelect
                ? new Rectangle(Position.X + RaceStatusPosition.X, Position.Y + RaceStatusPosition.Y, RaceStatusBounds.Width, RaceStatusBounds.Height)
                : new Rectangle(Position.X + stageOrigin.X + StatusBounds.X, Position.Y + stageOrigin.Y + StatusBounds.Y, StatusBounds.Width, StatusBounds.Height);
            DrawParagraph(sprite, _statusMessage, stageStatusBounds, StatusTextColor);
        }

        private void DrawRaceConfirmDialog(SpriteBatch sprite)
        {
            Rectangle dialogBounds = GetRaceConfirmDialogBounds();
            if (_raceConfirmBackgroundTexture != null)
            {
                sprite.Draw(_raceConfirmBackgroundTexture, dialogBounds.Location.ToVector2(), Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, dialogBounds, new Color(34, 24, 10, 220));
            }

            if (_raceConfirmLabelTexturesByRace.TryGetValue(_selectedRace, out Texture2D raceLabel) && raceLabel != null)
            {
                Vector2 labelPosition = new(
                    dialogBounds.X + (dialogBounds.Width - raceLabel.Width) / 2f,
                    dialogBounds.Y + 24f);
                sprite.Draw(raceLabel, labelPosition, Color.White);
            }
            else
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    LoginCreateCharacterFlowState.GetRaceLabel(_selectedRace),
                    new Vector2(dialogBounds.X + 24, dialogBounds.Y + 28),
                    Color.White);
            }

            DrawRaceConfirmButton(sprite, _raceConfirmOkTexture, OffsetRectangle(dialogBounds.Location, RaceConfirmOkBounds));
            DrawRaceConfirmButton(sprite, _raceConfirmCancelTexture, OffsetRectangle(dialogBounds.Location, RaceConfirmCancelBounds));
            if (_pixelTexture != null)
            {
                Rectangle focusedBounds = OffsetRectangle(
                    dialogBounds.Location,
                    _isRaceConfirmAcceptFocused ? RaceConfirmOkBounds : RaceConfirmCancelBounds);
                DrawSelection(sprite, focusedBounds);
            }
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
            Point stageOrigin = GetStageOrigin();

            for (int i = 0; i < _avatarIndices.Length; i++)
            {
                Rectangle rowBounds = new(Position.X + stageOrigin.X + AvatarListBounds.X, Position.Y + stageOrigin.Y + AvatarListBounds.Y + (i * 18), AvatarListBounds.Width, 17);
                Texture2D labelTexture = i < enabledTextures.Count
                    ? enabledTextures[i]
                    : null;
                if (labelTexture != null)
                {
                    sprite.Draw(labelTexture, new Vector2(rowBounds.X, rowBounds.Y), Color.White);
                }
                DrawArrowTexture(sprite, _leftArrowTexture, new Vector2(Position.X + stageOrigin.X + AvatarListBounds.X, rowBounds.Y));
                DrawArrowTexture(sprite, _rightArrowTexture, new Vector2(Position.X + stageOrigin.X + AvatarListBounds.X + 210, rowBounds.Y));
                DrawAvatarValueLabel(sprite, i, rowBounds);
            }

            Rectangle genderBounds = new(Position.X + stageOrigin.X + AvatarListBounds.X, Position.Y + stageOrigin.Y + AvatarListBounds.Y + (_avatarIndices.Length * 18), AvatarListBounds.Width, 17);
            Texture2D genderTexture = _avatarIndices.Length < enabledTextures.Count
                ? enabledTextures[_avatarIndices.Length]
                : null;
            if (genderTexture != null)
            {
                sprite.Draw(genderTexture, new Vector2(genderBounds.X, genderBounds.Y), Color.White);
            }
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
            Rectangle inputBounds = GetNameInputBounds();
            DrawSelection(sprite, inputBounds);
            string text = BuildVisibleNameText();
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                text,
                new Vector2(inputBounds.X + 6, inputBounds.Y + 4),
                string.IsNullOrWhiteSpace(_displayName) && string.IsNullOrEmpty(_compositionText) ? MutedTextColor : Color.White);

            if (!string.IsNullOrWhiteSpace(_checkedName))
            {
                Point stageOrigin = GetStageOrigin();
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Last checked: {_checkedName}",
                    new Vector2(Position.X + stageOrigin.X + 34, Position.Y + stageOrigin.Y + 130),
                    new Color(145, 232, 145));
            }

            DrawImeCandidateWindow(sprite);
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
                bool showConfirmButton =
                    _fixedStage != LoginCreateCharacterStage.NameSelect &&
                    !(_fixedStage == LoginCreateCharacterStage.RaceSelect && _isRaceConfirmationOpen);
                _confirmButton.SetVisible(showConfirmButton);
                _confirmButton.SetEnabled(
                    _fixedStage switch
                    {
                        LoginCreateCharacterStage.AvatarSelect => !string.IsNullOrWhiteSpace(_checkedName),
                        _ => true
                    });
                Point stageOrigin = GetStageOrigin();
                (_confirmButton.X, _confirmButton.Y) = _fixedStage switch
                {
                    LoginCreateCharacterStage.RaceSelect => (stageOrigin.X + 26, stageOrigin.Y + 144),
                    LoginCreateCharacterStage.AvatarSelect => GetActionPanelButtonPosition(stageOrigin, AvatarActionPanelBounds, _confirmButton, secondarySlot: false),
                    LoginCreateCharacterStage.NameSelect => (stageOrigin.X + 27, stageOrigin.Y + 178),
                    _ => (stageOrigin.X + 18, stageOrigin.Y + 238)
                };
            }

            if (_cancelButton != null)
            {
                _cancelButton.SetVisible(!(_fixedStage == LoginCreateCharacterStage.RaceSelect && _isRaceConfirmationOpen));
                _cancelButton.SetEnabled(true);
                Point stageOrigin = GetStageOrigin();
                (_cancelButton.X, _cancelButton.Y) = _fixedStage switch
                {
                    LoginCreateCharacterStage.RaceSelect => (stageOrigin.X + 110, stageOrigin.Y + 144),
                    LoginCreateCharacterStage.AvatarSelect => GetActionPanelButtonPosition(stageOrigin, AvatarActionPanelBounds, _cancelButton, secondarySlot: true),
                    LoginCreateCharacterStage.NameSelect => GetActionPanelButtonPosition(stageOrigin, NameActionPanelBounds, _cancelButton, secondarySlot: true),
                    _ => (stageOrigin.X + 102, stageOrigin.Y + 238)
                };
            }

            if (_checkButton != null)
            {
                bool showCheckButton = _fixedStage == LoginCreateCharacterStage.NameSelect;
                _checkButton.SetVisible(showCheckButton);
                _checkButton.SetEnabled(showCheckButton && !string.IsNullOrWhiteSpace(_displayName));
                if (showCheckButton)
                {
                    Point stageOrigin = GetStageOrigin();
                    (_checkButton.X, _checkButton.Y) = GetActionPanelButtonPosition(
                        stageOrigin,
                        NameActionPanelBounds,
                        _checkButton,
                        secondarySlot: false,
                        centerWithinSlot: true);
                }
            }
        }

        private static (int X, int Y) GetActionPanelButtonPosition(
            Point stageOrigin,
            Rectangle actionPanelBounds,
            UIObject button,
            bool secondarySlot,
            bool centerWithinSlot = false)
        {
            int buttonWidth = Math.Max(1, button?.CanvasSnapshotWidth ?? 1);
            int buttonHeight = Math.Max(1, button?.CanvasSnapshotHeight ?? 1);
            int slotX = actionPanelBounds.X + (secondarySlot ? ActionPanelSlotWidth + ActionPanelSlotGap : 0);
            int x = centerWithinSlot
                ? slotX + Math.Max(0, (ActionPanelSlotWidth - buttonWidth) / 2)
                : slotX;
            int y = actionPanelBounds.Y + Math.Max(0, (actionPanelBounds.Height - buttonHeight) / 2);
            return (stageOrigin.X + x, stageOrigin.Y + y);
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

        public override void HandleCommittedText(string text)
        {
            if (!IsNameInputEditable || string.IsNullOrEmpty(text))
            {
                return;
            }

            ClearCompositionText();
            foreach (char character in text)
            {
                TryInsertNameCharacter(character);
            }
        }

        public override void HandleCompositionText(string text)
        {
            HandleCompositionState(new ImeCompositionState(text ?? string.Empty, Array.Empty<int>(), -1));
        }

        public override void HandleCompositionState(ImeCompositionState state)
        {
            if (!IsNameInputEditable)
            {
                ClearCompositionText();
                return;
            }

            string sanitized = SanitizeNameCompositionText(state?.Text);
            if (sanitized.Length == 0)
            {
                ClearCompositionText();
                return;
            }

            int availableLength = Math.Max(0, NameMaxLength - (_displayName?.Length ?? 0));
            if (availableLength <= 0)
            {
                ClearCompositionText();
                return;
            }

            _compositionText = sanitized.Length > availableLength
                ? sanitized[..availableLength]
                : sanitized;
        }

        public override void ClearCompositionText()
        {
            _compositionText = string.Empty;
            ClearImeCandidateList();
        }

        public override void HandleImeCandidateList(ImeCandidateListState state)
        {
            _candidateListState = IsNameInputEditable && state != null && state.HasCandidates
                ? state
                : ImeCandidateListState.Empty;
        }

        public override void ClearImeCandidateList()
        {
            _candidateListState = ImeCandidateListState.Empty;
        }

        Rectangle ISoftKeyboardHost.GetSoftKeyboardAnchorBounds() => GetNameInputBounds();

        bool ISoftKeyboardHost.TryInsertSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!IsNameInputEditable)
            {
                errorMessage = "This owner is not editable right now.";
                return false;
            }

            if (!TryInsertNameCharacter(character))
            {
                errorMessage = "That key is disabled for this field.";
                return false;
            }

            return true;
        }

        bool ISoftKeyboardHost.TryBackspaceSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!SoftKeyboardUI.CanBackspace(_displayName?.Length ?? 0))
            {
                errorMessage = "Nothing to remove.";
                return false;
            }

            RemoveLastNameCharacter();
            return true;
        }

        bool ISoftKeyboardHost.TrySubmitSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!IsNameInputEditable || string.IsNullOrWhiteSpace(_displayName))
            {
                errorMessage = "Enter a character name first.";
                return false;
            }

            _softKeyboardActive = false;
            DuplicateCheckRequested?.Invoke();
            return true;
        }

        void ISoftKeyboardHost.OnSoftKeyboardClosed()
        {
            _softKeyboardActive = false;
        }

        void ISoftKeyboardHost.SetSoftKeyboardCompositionText(string text)
        {
            HandleCompositionText(text);
        }

        private void AppendNameCharacter(char character)
        {
            if (!IsNameInputEditable)
            {
                return;
            }

            TryInsertNameCharacter(character);
        }

        private bool TryInsertNameCharacter(char character)
        {
            if (!CanAcceptNameCharacter(character))
            {
                return false;
            }

            NameChanged?.Invoke(_displayName + character);
            return true;
        }

        bool ISoftKeyboardHost.TryReplaceLastSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!IsNameInputEditable)
            {
                errorMessage = "Name entry is not editable right now.";
                return false;
            }

            if (string.IsNullOrEmpty(_displayName))
            {
                errorMessage = "Nothing to replace.";
                return false;
            }

            if (!SoftKeyboardUI.CanAcceptCharacter(SoftKeyboardKeyboardType.AlphaNumeric, _displayName.Length - 1, NameMaxLength, character))
            {
                errorMessage = "That key is disabled for this field.";
                return false;
            }

            NameChanged?.Invoke(_displayName[..^1] + character);
            return true;
        }

        private void RemoveLastNameCharacter()
        {
            if (!SoftKeyboardUI.CanBackspace(_displayName?.Length ?? 0))
            {
                return;
            }

            NameChanged?.Invoke(_displayName[..^1]);
        }

        private bool CanAcceptNameCharacter(char character)
        {
            return SoftKeyboardUI.CanAcceptCharacter(
                SoftKeyboardKeyboardType.AlphaNumeric,
                _displayName?.Length ?? 0,
                NameMaxLength,
                character);
        }

        private string SanitizeNameCompositionText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            List<char> accepted = new(text.Length);
            foreach (char character in text)
            {
                if (!SoftKeyboardUI.CanAcceptCharacter(
                        SoftKeyboardKeyboardType.AlphaNumeric,
                        (_displayName?.Length ?? 0) + accepted.Count,
                        NameMaxLength,
                        character))
                {
                    continue;
                }

                accepted.Add(character);
                if ((_displayName?.Length ?? 0) + accepted.Count >= NameMaxLength)
                {
                    break;
                }
            }

            return accepted.Count == 0
                ? string.Empty
                : new string(accepted.ToArray());
        }

        private string BuildVisibleNameText()
        {
            if (string.IsNullOrWhiteSpace(_displayName) && string.IsNullOrEmpty(_compositionText))
            {
                return "Type a name";
            }

            string text = _displayName ?? string.Empty;
            if (!string.IsNullOrEmpty(_compositionText))
            {
                text += _compositionText;
            }

            if (((Environment.TickCount / 350) % 2) == 0)
            {
                text += "|";
            }

            return text;
        }

        private Rectangle GetLocalNameInputBounds()
        {
            Point stageOrigin = GetStageOrigin();
            return new Rectangle(
                stageOrigin.X + NameInputBounds.X,
                stageOrigin.Y + NameInputBounds.Y,
                NameInputBounds.Width,
                NameInputBounds.Height);
        }

        private Rectangle GetNameInputBounds()
        {
            Rectangle bounds = GetLocalNameInputBounds();
            bounds.Offset(Position);
            return bounds;
        }

        private void DrawImeCandidateWindow(SpriteBatch sprite)
        {
            if (_font == null || _pixelTexture == null || !_candidateListState.HasCandidates || !IsNameInputEditable)
            {
                return;
            }

            Rectangle bounds = GetImeCandidateWindowBounds(sprite.GraphicsDevice.Viewport);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            sprite.Draw(_pixelTexture, bounds, new Color(33, 33, 41, 235));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(214, 214, 214, 220));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(214, 214, 214, 220));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(214, 214, 214, 220));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(214, 214, 214, 220));

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

        private Rectangle GetImeCandidateWindowBounds(Viewport viewport)
        {
            int visibleCount = GetVisibleCandidateCount();
            if (visibleCount <= 0 || _font == null)
            {
                return Rectangle.Empty;
            }

            Rectangle ownerBounds = GetNameInputBounds();
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
            int x = Math.Clamp(ownerBounds.X, 0, Math.Max(0, viewport.Width - width));
            int y = ownerBounds.Bottom + 2;
            if (y + height > viewport.Height)
            {
                y = Math.Max(0, ownerBounds.Y - height - 2);
            }

            return new Rectangle(x, y, width, height);
        }

        private int GetVisibleCandidateCount()
        {
            if (!_candidateListState.HasCandidates)
            {
                return 0;
            }

            return Math.Clamp(_candidateListState.PageSize > 0 ? _candidateListState.PageSize : _candidateListState.Candidates.Count, 1, _candidateListState.Candidates.Count);
        }

        private bool IsNameInputEditable => IsVisible && _fixedStage == LoginCreateCharacterStage.NameSelect;

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
                raceStages.TryGetValue(_fixedStage, out texture) &&
                texture != null)
            {
                return true;
            }

            return _framesByRaceAndStage.TryGetValue(LoginCreateCharacterRaceKind.Explorer, out Dictionary<LoginCreateCharacterStage, Texture2D> explorerStages) &&
                   explorerStages.TryGetValue(_fixedStage, out texture) &&
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

        private bool TryHandleRaceConfirmClick(Point localPoint, MouseCursorItem mouseCursor)
        {
            Rectangle dialogBounds = GetRaceConfirmDialogBounds();
            Rectangle okBounds = OffsetRectangle(dialogBounds.Location, RaceConfirmOkBounds);
            Rectangle cancelBounds = OffsetRectangle(dialogBounds.Location, RaceConfirmCancelBounds);
            if (okBounds.Contains(localPoint))
            {
                _isRaceConfirmAcceptFocused = true;
                ConfirmRequested?.Invoke();
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (cancelBounds.Contains(localPoint))
            {
                _isRaceConfirmAcceptFocused = false;
                CancelRequested?.Invoke();
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            return dialogBounds.Contains(localPoint);
        }

        private Rectangle GetRaceConfirmDialogBounds()
        {
            return new Rectangle(
                RaceConfirmDialogOffset.X,
                RaceConfirmDialogOffset.Y,
                _raceConfirmBackgroundTexture?.Width ?? RaceConfirmDialogBounds.Width,
                _raceConfirmBackgroundTexture?.Height ?? RaceConfirmDialogBounds.Height);
        }

        private void DrawRaceConfirmButton(SpriteBatch sprite, Texture2D texture, Rectangle bounds)
        {
            if (texture != null)
            {
                sprite.Draw(texture, bounds.Location.ToVector2(), Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, bounds, new Color(255, 236, 176, 180));
            }
        }

        private static Rectangle OffsetRectangle(Point origin, Rectangle rectangle)
        {
            return new Rectangle(origin.X + rectangle.X, origin.Y + rectangle.Y, rectangle.Width, rectangle.Height);
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

    public sealed class LoginCreateCharacterRaceSelectWindow : LoginCreateCharacterWindowBase
    {
        public LoginCreateCharacterRaceSelectWindow(
            IDXObject frame,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyDictionary<LoginCreateCharacterStage, Texture2D>> framesByRaceAndStage,
            IReadOnlyList<Texture2D> jobTextures,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarEnabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarDisabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, Texture2D> racePreviewTexturesByRace,
            IReadOnlyList<CharacterSelectWindow.AnimationFrame> diceFrames,
            Texture2D leftArrowTexture,
            Texture2D rightArrowTexture,
            Texture2D raceConfirmBackgroundTexture,
            Texture2D raceConfirmOkTexture,
            Texture2D raceConfirmCancelTexture,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, Texture2D> raceConfirmLabelTexturesByRace,
            UIObject confirmButton,
            UIObject cancelButton,
            UIObject checkButton)
            : base(
                frame,
                MapSimulatorWindowNames.LoginCreateCharacterRaceSelect,
                LoginCreateCharacterStage.RaceSelect,
                framesByRaceAndStage,
                jobTextures,
                avatarEnabledTexturesByRace,
                avatarDisabledTexturesByRace,
                racePreviewTexturesByRace,
                diceFrames,
                leftArrowTexture,
                rightArrowTexture,
                raceConfirmBackgroundTexture,
                raceConfirmOkTexture,
                raceConfirmCancelTexture,
                raceConfirmLabelTexturesByRace,
                confirmButton,
                cancelButton,
                checkButton)
        {
        }
    }

    public sealed class LoginCreateCharacterJobSelectWindow : LoginCreateCharacterWindowBase
    {
        public LoginCreateCharacterJobSelectWindow(
            IDXObject frame,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyDictionary<LoginCreateCharacterStage, Texture2D>> framesByRaceAndStage,
            IReadOnlyList<Texture2D> jobTextures,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarEnabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarDisabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, Texture2D> racePreviewTexturesByRace,
            IReadOnlyList<CharacterSelectWindow.AnimationFrame> diceFrames,
            Texture2D leftArrowTexture,
            Texture2D rightArrowTexture,
            Texture2D raceConfirmBackgroundTexture,
            Texture2D raceConfirmOkTexture,
            Texture2D raceConfirmCancelTexture,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, Texture2D> raceConfirmLabelTexturesByRace,
            UIObject confirmButton,
            UIObject cancelButton,
            UIObject checkButton)
            : base(
                frame,
                MapSimulatorWindowNames.LoginCreateCharacterJobSelect,
                LoginCreateCharacterStage.JobSelect,
                framesByRaceAndStage,
                jobTextures,
                avatarEnabledTexturesByRace,
                avatarDisabledTexturesByRace,
                racePreviewTexturesByRace,
                diceFrames,
                leftArrowTexture,
                rightArrowTexture,
                raceConfirmBackgroundTexture,
                raceConfirmOkTexture,
                raceConfirmCancelTexture,
                raceConfirmLabelTexturesByRace,
                confirmButton,
                cancelButton,
                checkButton)
        {
        }
    }

    public sealed class LoginCreateCharacterAvatarSelectWindow : LoginCreateCharacterWindowBase
    {
        public LoginCreateCharacterAvatarSelectWindow(
            IDXObject frame,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyDictionary<LoginCreateCharacterStage, Texture2D>> framesByRaceAndStage,
            IReadOnlyList<Texture2D> jobTextures,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarEnabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarDisabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, Texture2D> racePreviewTexturesByRace,
            IReadOnlyList<CharacterSelectWindow.AnimationFrame> diceFrames,
            Texture2D leftArrowTexture,
            Texture2D rightArrowTexture,
            Texture2D raceConfirmBackgroundTexture,
            Texture2D raceConfirmOkTexture,
            Texture2D raceConfirmCancelTexture,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, Texture2D> raceConfirmLabelTexturesByRace,
            UIObject confirmButton,
            UIObject cancelButton,
            UIObject checkButton)
            : base(
                frame,
                MapSimulatorWindowNames.LoginCreateCharacterAvatarSelect,
                LoginCreateCharacterStage.AvatarSelect,
                framesByRaceAndStage,
                jobTextures,
                avatarEnabledTexturesByRace,
                avatarDisabledTexturesByRace,
                racePreviewTexturesByRace,
                diceFrames,
                leftArrowTexture,
                rightArrowTexture,
                raceConfirmBackgroundTexture,
                raceConfirmOkTexture,
                raceConfirmCancelTexture,
                raceConfirmLabelTexturesByRace,
                confirmButton,
                cancelButton,
                checkButton)
        {
        }
    }

    public sealed class LoginCreateCharacterNameSelectWindow : LoginCreateCharacterWindowBase
    {
        public LoginCreateCharacterNameSelectWindow(
            IDXObject frame,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyDictionary<LoginCreateCharacterStage, Texture2D>> framesByRaceAndStage,
            IReadOnlyList<Texture2D> jobTextures,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarEnabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>> avatarDisabledTexturesByRace,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, Texture2D> racePreviewTexturesByRace,
            IReadOnlyList<CharacterSelectWindow.AnimationFrame> diceFrames,
            Texture2D leftArrowTexture,
            Texture2D rightArrowTexture,
            Texture2D raceConfirmBackgroundTexture,
            Texture2D raceConfirmOkTexture,
            Texture2D raceConfirmCancelTexture,
            IReadOnlyDictionary<LoginCreateCharacterRaceKind, Texture2D> raceConfirmLabelTexturesByRace,
            UIObject confirmButton,
            UIObject cancelButton,
            UIObject checkButton)
            : base(
                frame,
                MapSimulatorWindowNames.LoginCreateCharacterNameSelect,
                LoginCreateCharacterStage.NameSelect,
                framesByRaceAndStage,
                jobTextures,
                avatarEnabledTexturesByRace,
                avatarDisabledTexturesByRace,
                racePreviewTexturesByRace,
                diceFrames,
                leftArrowTexture,
                rightArrowTexture,
                raceConfirmBackgroundTexture,
                raceConfirmOkTexture,
                raceConfirmCancelTexture,
                raceConfirmLabelTexturesByRace,
                confirmButton,
                cancelButton,
                checkButton)
        {
        }
    }
}
