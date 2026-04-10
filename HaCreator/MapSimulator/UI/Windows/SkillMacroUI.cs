using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI.Controls;
using HaCreator.MapSimulator;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Skill Macro UI window for creating and managing skill macros
    /// Each macro can chain up to 3 skills together with a custom name
    ///
    /// WZ Data Structure:
    /// - Pre-BB: UI.wz/UIWindow.img/SkillMacro (207x289)
    /// - Post-BB: UI.wz/UIWindow2.img/Skill/macro (195x281)
    /// - Extended: UI.wz/UIWindow.img/SkillMacroEx (181x303)
    ///
    /// Binary Analysis:
    /// - CUISkillMacro class handles macro window rendering and input
    /// - Macros stored as triplets of skill IDs with associated name
    /// - Maximum 5 macro slots available
    /// </summary>
    public class SkillMacroUI : UIWindowBase, ISoftKeyboardHost
    {
        private const uint CandidateWindowStyleRect = 0x0001;
        private const uint CandidateWindowStylePoint = 0x0002;
        private const uint CandidateWindowStyleForcePosition = 0x0020;
        private const uint CandidateWindowStyleCandidatePosition = 0x0040;
        private const uint CandidateWindowStyleExclude = 0x0080;

        private enum MacroDragMode
        {
            None,
            Skill,
            MacroBinding
        }

        #region Constants
        // Window dimensions from WZ data (pre-Big Bang style)
        // UI.wz/UIWindow.img/SkillMacro/backgrnd: 207x289
        public const int WINDOW_WIDTH = 207;
        public const int WINDOW_HEIGHT = 289;

        // Post-Big Bang dimensions
        // UI.wz/UIWindow2.img/Skill/macro/backgrnd: 195x281
        public const int WINDOW_WIDTH_BB = 195;
        public const int WINDOW_HEIGHT_BB = 281;

        // Skill icon size (standard MapleStory icon size)
        private const int SKILL_ICON_SIZE = 32;

        // Maximum skills per macro
        public const int SKILLS_PER_MACRO = 3;

        // Maximum number of macro slots
        public const int MAX_MACRO_SLOTS = 5;

        // `CUIMacroSysEx::Draw` highlights rows at (11, 27 + 41*i) and
        // places the three skill icons at (14 + 34*j, 30 + 41*i).
        private const int MACRO_SLOT_X = 11;
        private const int MACRO_SLOT_Y = 27;
        private const int MACRO_SLOT_WIDTH = 158;
        private const int MACRO_SLOT_HEIGHT = 38;
        private const int MACRO_SLOT_SPACING = 41;
        private const int SKILL_SLOT_START_X = 14;
        private const int SKILL_SLOT_Y = 30;
        private const int SKILL_SLOT_SPACING = 34;
        private const int MACRO_ICON_X = 135;
        private const int SELECTED_MACRO_ICON_X = 14;
        private const int SELECTED_MACRO_ICON_Y = 241;

        // `CUIMacroSysEx::OnCreate` builds the owner edit control at (53, 260, 114, 14).
        private const int NAME_FIELD_WIDTH = 114;
        private const int NAME_FIELD_HEIGHT = 14;
        private const int NAME_FIELD_X = 53;
        private const int NAME_FIELD_Y = 260;
        private const int NAME_FIELD_TEXT_INSET_X = 4;
        private const int NAME_FIELD_TEXT_INSET_Y = 0;
        private const float NAME_FIELD_COUNTER_SCALE = 0.55f;
        private const int OWNER_TOOLTIP_MARGIN = 4;
        // `CCtrlEdit::CreateIMECandWnd` formats candidate ordinals through StringPool 0x1A15.
        private const int CandidateNumberFormatStringPoolId = 0x1A15;
        private static readonly Color NameFieldCounterColor = new(196, 196, 178);

        // WZ `Skill/macro/check` resolves to (161, 235) inside the owner.
        private const int CHECKBOX_X = 161;
        private const int CHECKBOX_Y = 235;
        private const int CHECKBOX_SIZE = 15;

        // `CUIMacroSysEx::OnCreate` loads the owner save-button tooltip from
        // StringPool 0x1108 and `OnButtonClicked` shows StringPool 0x0D01 after saving.
        // Keep the unresolved payload path in the dedicated owner-local resolver so the
        // exact client text can be filled in later without touching the dialog seam again.
        #endregion

        #region Fields
        private SkillManager _skillManager;
        private SkillLoader _skillLoader;
        private SpriteFont _font;
        private readonly GraphicsDevice _graphicsDevice;

        // Macro data
        private readonly SkillMacro[] _macros = new SkillMacro[MAX_MACRO_SLOTS];
        private int _selectedMacroIndex = -1;
        private int _editingMacroIndex = -1;

        // Editing state for currently selected macro
        private string _editingMacroName = "";
        private int[] _editingSkillIds = new int[SKILLS_PER_MACRO];

        // Drag and drop for skill assignment
        private MacroDragMode _dragMode = MacroDragMode.None;
        private int _dragSkillId = 0;
        private int _dragMacroIndex = -1;
        private int _dragSourceSlot = -1;
        private int _dragSourceMacro = -1;
        private Vector2 _dragPosition;

        // Hover state
        private int _hoveredMacroIndex = -1;
        private int _hoveredSkillSlot = -1;

        // Textures
        private Texture2D _emptySlotTexture;
        private Texture2D _slotHighlightTexture;
        private Texture2D _macroSlotTexture;
        private Texture2D _selectedSlotTexture;
        private Texture2D _textPixelTexture;
        private Texture2D[] _macroSlotIcons;
        private Texture2D[] _macroSlotDisabledIcons;
        private Texture2D[] _macroSlotMouseOverIcons;
        private Texture2D _foregroundTexture;
        private Point _foregroundOffset;
        private Texture2D _contentTexture;
        private Point _contentOffset;
        private Texture2D _checkboxTexture;
        private ClientTextRasterizer _candidateTextRasterizer;
        private ClientTextRasterizer _candidateSelectedTextRasterizer;

        // Checkbox state
        private bool _notifyPartyMembers = false;
        private string _validationMessage = string.Empty;
        private string _ownerNoticeMessage = string.Empty;
        private Color _ownerNoticeColor = Color.White;
        private bool _nameFieldFocused;
        private int _editingCursorPosition;
        private int _editingSelectionAnchor = -1;
        private bool _nameSelectionDragActive;
        private int _caretBlinkTick;
        private string _compositionText = string.Empty;
        private int _compositionInsertionIndex = -1;
        private IReadOnlyList<int> _compositionClauseOffsets = Array.Empty<int>();
        private int _compositionCursorPosition = -1;
        private ImeCandidateListState _candidateListState = ImeCandidateListState.Empty;
        private SkillMacroSoftKeyboardSkin _softKeyboardSkin;
        private bool _softKeyboardVisible;
        private bool _softKeyboardActive;
        private bool _softKeyboardMinimized;
        private bool _softKeyboardShift;
        private bool _softKeyboardCapsLock;
        private int _hoveredSoftKeyboardKeyIndex = -1;
        private SkillMacroSoftKeyboardFunctionKey _hoveredSoftKeyboardFunctionKey = SkillMacroSoftKeyboardFunctionKey.None;
        private SkillMacroSoftKeyboardWindowButton _hoveredSoftKeyboardWindowButton = SkillMacroSoftKeyboardWindowButton.None;
        private int _pressedSoftKeyboardKeyIndex = -1;
        private SkillMacroSoftKeyboardFunctionKey _pressedSoftKeyboardFunctionKey = SkillMacroSoftKeyboardFunctionKey.None;
        private SkillMacroSoftKeyboardWindowButton _pressedSoftKeyboardWindowButton = SkillMacroSoftKeyboardWindowButton.None;
        private int _softKeyboardPressedVisualUntil;
        private Point? _lastMousePosition;

        // Buttons
        private UIObject _btnOK;
        private UIObject _btnCancel;
        private UIObject _btnDelete;

        // Skill icon cache
        private readonly Dictionary<int, IDXObject> _skillIconCache = new();
        #endregion

        #region Properties
        public override string WindowName => "SkillMacro";
        public override bool CapturesKeyboardInput => IsVisible && _editingMacroIndex >= 0 && _nameFieldFocused;
        bool ISoftKeyboardHost.WantsSoftKeyboard => IsVisible && _editingMacroIndex >= 0 && _nameFieldFocused && _softKeyboardActive;
        SoftKeyboardKeyboardType ISoftKeyboardHost.SoftKeyboardKeyboardType => SoftKeyboardKeyboardType.AlphaNumeric;
        int ISoftKeyboardHost.SoftKeyboardTextLength => GetSoftKeyboardTextLengthBytes(_editingMacroName);
        int ISoftKeyboardHost.SoftKeyboardMaxLength => SkillMacroNameRules.MaxNameBytes;
        bool ISoftKeyboardHost.CanSubmitSoftKeyboard => CanSaveCurrentMacro();
        string ISoftKeyboardHost.GetSoftKeyboardText() => _editingMacroName ?? string.Empty;
        public bool IsDraggingSkillSlot => _dragMode == MacroDragMode.Skill;
        public bool IsDraggingMacroBinding => _dragMode == MacroDragMode.MacroBinding;
        public int DraggedMacroIndex => IsDraggingMacroBinding ? _dragMacroIndex : -1;

        /// <summary>
        /// Gets or sets the currently selected macro index (-1 for none)
        /// </summary>
        public int SelectedMacroIndex
        {
            get => _selectedMacroIndex;
            set
            {
                if (value >= -1 && value < MAX_MACRO_SLOTS)
                {
                    _selectedMacroIndex = value;
                    if (value >= 0)
                    {
                        LoadMacroForEditing(value);
                    }
                    else
                    {
                        ResetEditingState();
                    }

                    UpdateOwnerButtonState();
                }
            }
        }

        /// <summary>
        /// Gets all macros
        /// </summary>
        public IReadOnlyList<SkillMacro> Macros => _macros;

        public void LoadMacros(IReadOnlyList<SkillMacro> macros)
        {
            for (int i = 0; i < MAX_MACRO_SLOTS; i++)
            {
                SkillMacro sourceMacro = macros != null && i < macros.Count ? macros[i] : null;
                _macros[i] = CloneMacro(sourceMacro, i);
            }

            _selectedMacroIndex = -1;
            _editingMacroIndex = -1;
            _editingMacroName = string.Empty;
            _notifyPartyMembers = false;
            _validationMessage = string.Empty;
            _editingCursorPosition = 0;
            _caretBlinkTick = Environment.TickCount;
            ClearCompositionText();
            Array.Clear(_editingSkillIds, 0, _editingSkillIds.Length);
            CancelDrag();
            HideSoftKeyboard();
            UpdateOwnerButtonState();
        }

        /// <summary>
        /// Callback when a macro is saved
        /// </summary>
        public Action<int, SkillMacro> OnMacroSaved;

        /// <summary>
        /// Callback when a macro is deleted
        /// </summary>
        public Action<int> OnMacroDeleted;

        /// <summary>
        /// Callback when macro window is closed
        /// </summary>
        public Action OnMacroWindowClosed;
        internal Func<int, int, bool> OnImeCandidateSelected;
        internal Func<IntPtr> ResolveImeWindowHandle;
        #endregion

        #region Constructor
        public SkillMacroUI(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            _graphicsDevice = device;
            // Initialize macro slots
            for (int i = 0; i < MAX_MACRO_SLOTS; i++)
            {
                _macros[i] = CreateDefaultMacro(i);
            }

            CreateSlotTextures(device);
        }

        private void CreateSlotTextures(GraphicsDevice device)
        {
            // Empty skill slot texture (dark with border)
            _emptySlotTexture = new Texture2D(device, SKILL_ICON_SIZE, SKILL_ICON_SIZE);
            Color[] slotData = new Color[SKILL_ICON_SIZE * SKILL_ICON_SIZE];
            Color slotColor = new Color(35, 35, 55, 180);
            Color borderColor = new Color(80, 80, 100, 255);

            for (int y = 0; y < SKILL_ICON_SIZE; y++)
            {
                for (int x = 0; x < SKILL_ICON_SIZE; x++)
                {
                    if (x == 0 || x == SKILL_ICON_SIZE - 1 || y == 0 || y == SKILL_ICON_SIZE - 1)
                        slotData[y * SKILL_ICON_SIZE + x] = borderColor;
                    else
                        slotData[y * SKILL_ICON_SIZE + x] = slotColor;
                }
            }
            _emptySlotTexture.SetData(slotData);

            // Highlight texture (yellow/orange tint for hover)
            _slotHighlightTexture = new Texture2D(device, SKILL_ICON_SIZE, SKILL_ICON_SIZE);
            Color[] highlightData = new Color[SKILL_ICON_SIZE * SKILL_ICON_SIZE];
            Color highlightColor = new Color(255, 200, 100, 100);

            for (int i = 0; i < highlightData.Length; i++)
                highlightData[i] = highlightColor;
            _slotHighlightTexture.SetData(highlightData);

            // Macro slot background texture
            _macroSlotTexture = new Texture2D(device, MACRO_SLOT_WIDTH, MACRO_SLOT_HEIGHT);
            Color[] macroData = new Color[MACRO_SLOT_WIDTH * MACRO_SLOT_HEIGHT];
            Color macroColor = new Color(45, 45, 65, 150);
            Color macroBorder = new Color(100, 100, 120, 200);

            for (int y = 0; y < MACRO_SLOT_HEIGHT; y++)
            {
                for (int x = 0; x < MACRO_SLOT_WIDTH; x++)
                {
                    if (x == 0 || x == MACRO_SLOT_WIDTH - 1 || y == 0 || y == MACRO_SLOT_HEIGHT - 1)
                        macroData[y * MACRO_SLOT_WIDTH + x] = macroBorder;
                    else
                        macroData[y * MACRO_SLOT_WIDTH + x] = macroColor;
                }
            }
            _macroSlotTexture.SetData(macroData);

            // Selected macro slot texture
            _selectedSlotTexture = new Texture2D(device, MACRO_SLOT_WIDTH, MACRO_SLOT_HEIGHT);
            Color[] selectedData = new Color[MACRO_SLOT_WIDTH * MACRO_SLOT_HEIGHT];
            Color selectedColor = new Color(70, 90, 120, 180);
            Color selectedBorder = new Color(150, 180, 220, 255);

            for (int y = 0; y < MACRO_SLOT_HEIGHT; y++)
            {
                for (int x = 0; x < MACRO_SLOT_WIDTH; x++)
                {
                    if (x <= 1 || x >= MACRO_SLOT_WIDTH - 2 || y <= 1 || y >= MACRO_SLOT_HEIGHT - 2)
                        selectedData[y * MACRO_SLOT_WIDTH + x] = selectedBorder;
                    else
                        selectedData[y * MACRO_SLOT_WIDTH + x] = selectedColor;
                }
            }
            _selectedSlotTexture.SetData(selectedData);

            _textPixelTexture = new Texture2D(device, 1, 1);
            _textPixelTexture.SetData(new[] { Color.White });
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Set the skill manager reference
        /// </summary>
        public void SetSkillManager(SkillManager skillManager)
        {
            _skillManager = skillManager;
        }

        /// <summary>
        /// Set the skill loader for loading skill icons
        /// </summary>
        public void SetSkillLoader(SkillLoader skillLoader)
        {
            _skillLoader = skillLoader;
        }

        /// <summary>
        /// Set the font for text rendering
        /// </summary>
        public override void SetFont(SpriteFont font)
        {
            _font = font;
            _candidateTextRasterizer = null;
            _candidateSelectedTextRasterizer = null;
        }

        /// <summary>
        /// Initialize macro window buttons
        /// </summary>
        /// <param name="btnOK">OK/Save button</param>
        /// <param name="btnCancel">Cancel button</param>
        /// <param name="btnDelete">Delete button (optional)</param>
        public void InitializeButtons(UIObject btnOK, UIObject btnCancel, UIObject btnDelete = null)
        {
            _btnOK = btnOK;
            _btnCancel = btnCancel;
            _btnDelete = btnDelete;

            if (btnOK != null)
            {
                AddButton(btnOK);
                btnOK.ButtonClickReleased += OnOKClicked;
            }

            if (btnCancel != null)
            {
                AddButton(btnCancel);
                btnCancel.ButtonClickReleased += OnCancelClicked;
            }

            if (btnDelete != null)
            {
                AddButton(btnDelete);
                btnDelete.ButtonClickReleased += OnDeleteClicked;
            }
        }

        /// <summary>
        /// Set the selection highlight texture
        /// </summary>
        public void SetSelectionTexture(Texture2D texture)
        {
            _selectedSlotTexture = texture;
        }

        /// <summary>
        /// Set the macro slot icon textures (5 icons for the 5 macro slots)
        /// </summary>
        public void SetMacroSlotIcons(Texture2D[] icons, Texture2D[] disabledIcons = null, Texture2D[] mouseOverIcons = null)
        {
            if (icons != null)
            {
                _macroSlotIcons = icons;
            }

            _macroSlotDisabledIcons = disabledIcons;
            _macroSlotMouseOverIcons = mouseOverIcons;
        }

        internal void SetOwnerChrome(Texture2D foregroundTexture, Point foregroundOffset, Texture2D contentTexture, Point contentOffset, Texture2D checkboxTexture)
        {
            _foregroundTexture = foregroundTexture;
            _foregroundOffset = foregroundOffset;
            _contentTexture = contentTexture;
            _contentOffset = contentOffset;
            _checkboxTexture = checkboxTexture;
        }

        internal void SetSoftKeyboardSkin(SkillMacroSoftKeyboardSkin skin)
        {
            _softKeyboardSkin = skin;
        }

        private void UpdateOwnerButtonState()
        {
            _btnOK?.SetEnabled(_selectedMacroIndex >= 0);
        }

        private void ResetEditingState()
        {
            _editingMacroIndex = -1;
            _editingMacroName = string.Empty;
            _notifyPartyMembers = false;
            _validationMessage = string.Empty;
            _nameFieldFocused = false;
            ClearOwnerNotice();
            _editingCursorPosition = 0;
            ClearNameSelection();
            _nameSelectionDragActive = false;
            _caretBlinkTick = Environment.TickCount;
            ClearCompositionText();
            Array.Clear(_editingSkillIds, 0, _editingSkillIds.Length);
        }

        private void ResetOwnerSession()
        {
            _selectedMacroIndex = -1;
            _hoveredMacroIndex = -1;
            _hoveredSkillSlot = -1;
            ResetEditingState();
            CancelDrag();
            HideSoftKeyboard();
            UpdateOwnerButtonState();
        }
        #endregion

        #region Drawing
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            int windowX = Position.X;
            int windowY = Position.Y;

            if (_foregroundTexture != null)
            {
                sprite.Draw(_foregroundTexture, new Vector2(windowX + _foregroundOffset.X, windowY + _foregroundOffset.Y), Color.White);
            }

            if (_contentTexture != null)
            {
                sprite.Draw(_contentTexture, new Vector2(windowX + _contentOffset.X, windowY + _contentOffset.Y), Color.White);
            }

            // Draw macro slots
            for (int i = 0; i < MAX_MACRO_SLOTS; i++)
            {
                DrawMacroSlot(sprite, i, windowX, windowY);
            }

            // Draw name field if editing
            if (_editingMacroIndex >= 0)
            {
                DrawNameField(sprite, windowX, windowY);
            }

            // Draw checkbox for party notification
            DrawCheckbox(sprite, windowX, windowY);

            // Draw dragged skill icon
            if (_dragMode == MacroDragMode.Skill && _dragSkillId > 0)
            {
                DrawDraggedSkill(sprite);
            }
            else if (_dragMode == MacroDragMode.MacroBinding && _dragMacroIndex >= 0)
            {
                DrawDraggedMacro(sprite);
            }
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            base.DrawOverlay(sprite, skeletonMeshRenderer, gameTime, mapShiftX, mapShiftY, centerX, centerY, drawReflectionInfo, renderParameters, TickCount);

            DrawOwnerButtonTooltip(sprite);
            DrawSoftKeyboard(sprite);
            DrawImeCandidateWindow(sprite);
        }

        private void DrawMacroSlot(SpriteBatch sprite, int macroIndex, int windowX, int windowY)
        {
            SkillMacro macro = _macros[macroIndex];
            int slotX = windowX + MACRO_SLOT_X;
            int slotY = windowY + MACRO_SLOT_Y + (macroIndex * MACRO_SLOT_SPACING);
            bool isSelected = macroIndex == _selectedMacroIndex;
            bool isHovered = macroIndex == _hoveredMacroIndex;

            if (isSelected && _selectedSlotTexture != null)
            {
                sprite.Draw(_selectedSlotTexture, new Vector2(slotX, slotY), Color.White);
            }
            else if (isHovered)
            {
                sprite.Draw(_slotHighlightTexture, new Rectangle(slotX, slotY, MACRO_SLOT_WIDTH, MACRO_SLOT_HEIGHT), new Color(255, 255, 255, 72));
            }

            for (int skillSlot = 0; skillSlot < SKILLS_PER_MACRO; skillSlot++)
            {
                int skillX = windowX + SKILL_SLOT_START_X + (skillSlot * SKILL_SLOT_SPACING);
                int skillY = windowY + SKILL_SLOT_Y + (macroIndex * MACRO_SLOT_SPACING);
                int skillId = macro.SkillIds[skillSlot];
                if (skillId > 0)
                {
                    IDXObject icon = GetSkillIcon(skillId);
                    if (icon != null)
                    {
                        icon.DrawBackground(sprite, null, null, skillX, skillY, Color.White, false, null);
                    }
                    else
                    {
                        sprite.Draw(_emptySlotTexture, new Rectangle(skillX, skillY, SKILL_ICON_SIZE, SKILL_ICON_SIZE), new Color(40, 40, 48, 220));
                    }
                }
                else
                {
                    sprite.Draw(_emptySlotTexture, new Rectangle(skillX, skillY, SKILL_ICON_SIZE, SKILL_ICON_SIZE), new Color(40, 40, 48, 220));
                }

                if (isHovered && skillSlot == _hoveredSkillSlot)
                {
                    sprite.Draw(_slotHighlightTexture, new Rectangle(skillX, skillY, SKILL_ICON_SIZE, SKILL_ICON_SIZE), new Color(255, 255, 255, 96));
                }
            }

            DrawMacroIcon(sprite, macroIndex, new Point(windowX + MACRO_ICON_X, slotY), isHovered, macro.IsEnabled, isSelected ? Color.White : new Color(255, 255, 255, 220));
        }

        private void DrawMacroIcon(SpriteBatch sprite, int macroIndex, Point position, bool hovered, bool enabled, Color color)
        {
            Texture2D iconTexture = ResolveMacroIconTexture(macroIndex, hovered, enabled);
            if (iconTexture != null)
            {
                sprite.Draw(iconTexture, new Vector2(position.X, position.Y), color);
                return;
            }

            if (_font != null)
            {
                sprite.DrawString(_font, (macroIndex + 1).ToString(CultureInfo.InvariantCulture), new Vector2(position.X + 10, position.Y + 8), color);
            }
        }

        private void DrawNameField(SpriteBatch sprite, int windowX, int windowY)
        {
            if (_font == null)
                return;

            int fieldX = windowX + NAME_FIELD_X;
            int fieldY = windowY + NAME_FIELD_Y;
            Rectangle fieldRect = new(fieldX, fieldY, NAME_FIELD_WIDTH, NAME_FIELD_HEIGHT);
            sprite.Draw(_textPixelTexture, fieldRect, new Color(18, 18, 24, 28));

            string committedText = _editingMacroName ?? string.Empty;
            int safeCursorPosition = Math.Clamp(_editingCursorPosition, 0, committedText.Length);
            int selectionStart = GetNameSelectionStart();
            int selectionLength = GetNameSelectionLength();
            int selectionEnd = selectionStart >= 0
                ? Math.Clamp(selectionStart + selectionLength, 0, committedText.Length)
                : -1;
            string committedPrefix = safeCursorPosition > 0
                ? committedText[..safeCursorPosition]
                : string.Empty;
            string compositionText = _compositionText ?? string.Empty;
            Vector2 textPosition = new(fieldX + NAME_FIELD_TEXT_INSET_X, fieldY + NAME_FIELD_TEXT_INSET_Y);
            DrawCommittedNameText(sprite, committedText, selectionStart, selectionEnd, textPosition);

            float prefixWidth = committedPrefix.Length > 0 ? _font.MeasureString(committedPrefix).X : 0f;
            Vector2 compositionPosition = new(textPosition.X + prefixWidth, textPosition.Y);
            if (compositionText.Length > 0)
            {
                sprite.DrawString(_font, compositionText, compositionPosition, new Color(255, 235, 160));

                int compositionWidth = Math.Max(1, (int)Math.Ceiling(_font.MeasureString(compositionText).X));
                int underlineY = fieldY + NAME_FIELD_HEIGHT - 3;
                sprite.Draw(_textPixelTexture,
                    new Rectangle((int)compositionPosition.X, underlineY, compositionWidth, 1),
                    new Color(255, 235, 160, 220));
            }

            if (_nameFieldFocused && ((Environment.TickCount - _caretBlinkTick) / 500) % 2 == 0)
            {
                float caretOffset = prefixWidth;
                if (compositionText.Length > 0)
                {
                    string compositionCaretPrefix = ResolveCompositionCaretPrefix();
                    caretOffset += compositionCaretPrefix.Length > 0
                        ? _font.MeasureString(compositionCaretPrefix).X
                        : 0f;
                }

                int caretX = fieldX + NAME_FIELD_TEXT_INSET_X + (int)Math.Round(caretOffset);
                int caretY = fieldY + 1;
                sprite.Draw(_textPixelTexture,
                    new Rectangle(caretX, caretY, 1, Math.Max(NAME_FIELD_HEIGHT - 2, _font.LineSpacing - 2)),
                    Color.White);
            }

            bool selectedMacroEnabled = _editingMacroIndex >= 0
                && _editingMacroIndex < MAX_MACRO_SLOTS
                && _macros[_editingMacroIndex].IsEnabled;
            DrawMacroIcon(sprite, _editingMacroIndex, new Point(windowX + SELECTED_MACRO_ICON_X, windowY + SELECTED_MACRO_ICON_Y), false, selectedMacroEnabled, Color.White);

            if (!string.IsNullOrWhiteSpace(_validationMessage))
            {
                sprite.DrawString(_font, _validationMessage,
                    new Vector2(fieldX, fieldY - _font.LineSpacing - 2),
                    Color.IndianRed);
            }
            else if (!string.IsNullOrWhiteSpace(_ownerNoticeMessage))
            {
                sprite.DrawString(_font, _ownerNoticeMessage,
                    new Vector2(fieldX, fieldY - _font.LineSpacing - 2),
                    _ownerNoticeColor);
            }

            DrawNameByteCounter(sprite, fieldRect);
        }

        private void DrawOwnerButtonTooltip(SpriteBatch sprite)
        {
            if (_font == null || !ShouldShowChangeNameTooltip())
            {
                return;
            }

            string tooltipText = SkillMacroOwnerStringPoolText.GetSaveButtonTooltip();
            if (string.IsNullOrWhiteSpace(tooltipText))
            {
                return;
            }

            Rectangle buttonBounds = GetButtonBounds(_btnOK);
            Vector2 textSize = _font.MeasureString(tooltipText);
            int tooltipX = buttonBounds.Right - (int)Math.Ceiling(textSize.X);
            int tooltipY = buttonBounds.Y - _font.LineSpacing - OWNER_TOOLTIP_MARGIN;

            if (tooltipX < Position.X)
            {
                tooltipX = Position.X;
            }

            if (tooltipY < Position.Y)
            {
                tooltipY = buttonBounds.Bottom + OWNER_TOOLTIP_MARGIN;
            }

            sprite.DrawString(
                _font,
                tooltipText,
                new Vector2(tooltipX, tooltipY),
                new Color(216, 226, 183));
        }

        private void DrawNameByteCounter(SpriteBatch sprite, Rectangle fieldRect)
        {
            if (_font == null)
            {
                return;
            }

            string counterText = BuildNameByteCounterText();
            if (string.IsNullOrEmpty(counterText))
            {
                return;
            }

            Vector2 size = _font.MeasureString(counterText) * NAME_FIELD_COUNTER_SCALE;
            Vector2 position = new(
                fieldRect.Right - size.X,
                fieldRect.Bottom + 2);
            sprite.DrawString(
                _font,
                counterText,
                position,
                NameFieldCounterColor,
                0f,
                Vector2.Zero,
                NAME_FIELD_COUNTER_SCALE,
                SpriteEffects.None,
                0f);
        }

        private string BuildNameByteCounterText()
        {
            string displayedText = BuildDisplayedNameText();
            int byteCount = SkillMacroNameRules.GetByteCount(displayedText);
            return $"{byteCount}/{SkillMacroNameRules.MaxNameBytes}";
        }

        private Texture2D ResolveMacroIconTexture(int macroIndex, bool hovered, bool enabled)
        {
            if (macroIndex < 0)
            {
                return null;
            }

            if (!enabled
                && _macroSlotDisabledIcons != null
                && macroIndex < _macroSlotDisabledIcons.Length
                && _macroSlotDisabledIcons[macroIndex] != null)
            {
                return _macroSlotDisabledIcons[macroIndex];
            }

            if (hovered
                && _macroSlotMouseOverIcons != null
                && macroIndex < _macroSlotMouseOverIcons.Length
                && _macroSlotMouseOverIcons[macroIndex] != null)
            {
                return _macroSlotMouseOverIcons[macroIndex];
            }

            if (_macroSlotIcons != null
                && macroIndex < _macroSlotIcons.Length
                && _macroSlotIcons[macroIndex] != null)
            {
                return _macroSlotIcons[macroIndex];
            }

            return null;
        }

        public Texture2D GetMacroIconTexture(int macroIndex, bool enabled = true)
        {
            return ResolveMacroIconTexture(macroIndex, hovered: false, enabled);
        }

        private void DrawSoftKeyboard(SpriteBatch sprite)
        {
            if (!_softKeyboardVisible)
            {
                return;
            }

            Point origin = GetSoftKeyboardPosition();
            Texture2D backgroundTexture = _softKeyboardMinimized
                ? _softKeyboardSkin?.MinimizedBackground
                : _softKeyboardSkin?.ExpandedBackground;
            Texture2D titleTexture = _softKeyboardMinimized
                ? _softKeyboardSkin?.MinimizedTitle
                : _softKeyboardSkin?.ExpandedTitle;

            if (backgroundTexture != null)
            {
                sprite.Draw(backgroundTexture, new Vector2(origin.X, origin.Y), Color.White);
            }
            else
            {
                Rectangle fallbackBounds = SkillMacroSoftKeyboardLayout.GetBounds(origin, _softKeyboardMinimized);
                sprite.Draw(_emptySlotTexture, fallbackBounds, new Color(32, 32, 40, 230));
            }

            if (titleTexture != null)
            {
                sprite.Draw(titleTexture, new Vector2(origin.X + 14, origin.Y + 8), Color.White);
            }

            DrawSoftKeyboardWindowButton(sprite, origin, SkillMacroSoftKeyboardWindowButton.Maximize);
            DrawSoftKeyboardWindowButton(sprite, origin, SkillMacroSoftKeyboardWindowButton.Minimize);
            DrawSoftKeyboardWindowButton(sprite, origin, SkillMacroSoftKeyboardWindowButton.Close);

            if (_softKeyboardMinimized)
            {
                return;
            }

            if (_softKeyboardSkin?.KeyboardBackground != null)
            {
                sprite.Draw(_softKeyboardSkin.KeyboardBackground, new Vector2(origin.X + 6, origin.Y + 20), Color.White);
            }

            foreach (int keyIndex in SkillMacroSoftKeyboardLayout.EnumerateVisibleKeyIndices(false))
            {
                DrawSoftKeyboardKey(sprite, origin, keyIndex);
            }

            DrawSoftKeyboardFunctionKey(sprite, origin, SkillMacroSoftKeyboardFunctionKey.CapsLock);
            DrawSoftKeyboardFunctionKey(sprite, origin, SkillMacroSoftKeyboardFunctionKey.LeftShift);
            DrawSoftKeyboardFunctionKey(sprite, origin, SkillMacroSoftKeyboardFunctionKey.RightShift);
            DrawSoftKeyboardFunctionKey(sprite, origin, SkillMacroSoftKeyboardFunctionKey.Enter);
            DrawSoftKeyboardFunctionKey(sprite, origin, SkillMacroSoftKeyboardFunctionKey.Backspace);
        }

        private void DrawSoftKeyboardKey(SpriteBatch sprite, Point origin, int keyIndex)
        {
            Rectangle bounds = SkillMacroSoftKeyboardLayout.GetKeyBounds(keyIndex);
            bounds.Offset(origin);

            bool enabled = IsSoftKeyboardKeyEnabled(keyIndex);
            SkillMacroSoftKeyboardVisualState visualState = ResolveSoftKeyboardKeyVisualState(
                enabled,
                keyIndex == _hoveredSoftKeyboardKeyIndex,
                keyIndex == _pressedSoftKeyboardKeyIndex);

            DrawSoftKeyboardTexture(sprite, _softKeyboardSkin?.KeyTextures.TryGetValue(keyIndex, out SkillMacroSoftKeyboardKeyTextures textures) == true ? textures : null, bounds, visualState);
            DrawSoftKeyboardLabel(sprite, bounds, SkillMacroSoftKeyboardLayout.GetKeyText(keyIndex, IsSoftKeyboardUppercase()), enabled ? Color.White : Color.Gray);
        }

        private void DrawSoftKeyboardFunctionKey(SpriteBatch sprite, Point origin, SkillMacroSoftKeyboardFunctionKey key)
        {
            Rectangle bounds = SkillMacroSoftKeyboardLayout.GetFunctionKeyBounds(key);
            if (bounds.IsEmpty)
            {
                return;
            }

            bounds.Offset(origin);
            bool enabled = IsSoftKeyboardFunctionKeyEnabled(key);
            bool active = key switch
            {
                SkillMacroSoftKeyboardFunctionKey.CapsLock => _softKeyboardCapsLock,
                SkillMacroSoftKeyboardFunctionKey.LeftShift or SkillMacroSoftKeyboardFunctionKey.RightShift => _softKeyboardShift,
                _ => false
            };

            bool hovered = key == _hoveredSoftKeyboardFunctionKey;
            bool pressed = key == _pressedSoftKeyboardFunctionKey || active;
            SkillMacroSoftKeyboardVisualState visualState = ResolveSoftKeyboardKeyVisualState(enabled, hovered, pressed);
            DrawSoftKeyboardTexture(sprite, _softKeyboardSkin?.FunctionKeyTextures.TryGetValue(key, out SkillMacroSoftKeyboardKeyTextures textures) == true ? textures : null, bounds, visualState);

            string label = key switch
            {
                SkillMacroSoftKeyboardFunctionKey.CapsLock => "CAPS",
                SkillMacroSoftKeyboardFunctionKey.LeftShift => "SHIFT",
                SkillMacroSoftKeyboardFunctionKey.RightShift => "SHIFT",
                SkillMacroSoftKeyboardFunctionKey.Enter => "ENTER",
                SkillMacroSoftKeyboardFunctionKey.Backspace => "BS",
                _ => string.Empty
            };
            DrawSoftKeyboardLabel(sprite, bounds, label, enabled ? Color.White : Color.Gray);
        }

        private void DrawSoftKeyboardWindowButton(SpriteBatch sprite, Point origin, SkillMacroSoftKeyboardWindowButton button)
        {
            Rectangle bounds = SkillMacroSoftKeyboardLayout.GetWindowButtonBounds(button);
            if (bounds.IsEmpty)
            {
                return;
            }

            bounds.Offset(origin);
            bool pressed = button == _pressedSoftKeyboardWindowButton;
            bool hovered = button == _hoveredSoftKeyboardWindowButton;
            SkillMacroSoftKeyboardVisualState visualState = ResolveSoftKeyboardKeyVisualState(true, hovered, pressed);
            DrawSoftKeyboardTexture(sprite, _softKeyboardSkin?.WindowButtonTextures.TryGetValue(button, out SkillMacroSoftKeyboardKeyTextures textures) == true ? textures : null, bounds, visualState);
        }

        private void DrawSoftKeyboardTexture(SpriteBatch sprite, SkillMacroSoftKeyboardKeyTextures textures, Rectangle bounds, SkillMacroSoftKeyboardVisualState visualState)
        {
            Texture2D texture = textures?.Resolve(visualState);
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
                return;
            }

            Color fallbackColor = visualState switch
            {
                SkillMacroSoftKeyboardVisualState.Disabled => new Color(55, 55, 60, 220),
                SkillMacroSoftKeyboardVisualState.Hovered => new Color(90, 90, 110, 230),
                SkillMacroSoftKeyboardVisualState.Pressed => new Color(120, 120, 145, 230),
                _ => new Color(70, 70, 84, 220)
            };
            sprite.Draw(_emptySlotTexture, bounds, fallbackColor);
        }

        private void DrawSoftKeyboardLabel(SpriteBatch sprite, Rectangle bounds, string label, Color color)
        {
            if (_font == null || string.IsNullOrEmpty(label))
            {
                return;
            }

            Vector2 size = _font.MeasureString(label);
            Vector2 position = new(
                bounds.X + Math.Max(0f, (bounds.Width - size.X) / 2f),
                bounds.Y + Math.Max(0f, (bounds.Height - size.Y) / 2f) - 1f);
            sprite.DrawString(_font, label, position, color);
        }

        private static SkillMacroSoftKeyboardVisualState ResolveSoftKeyboardKeyVisualState(bool enabled, bool hovered, bool pressed)
        {
            if (!enabled)
            {
                return SkillMacroSoftKeyboardVisualState.Disabled;
            }

            if (pressed)
            {
                return SkillMacroSoftKeyboardVisualState.Pressed;
            }

            return hovered
                ? SkillMacroSoftKeyboardVisualState.Hovered
                : SkillMacroSoftKeyboardVisualState.Normal;
        }

        private void DrawCheckbox(SpriteBatch sprite, int windowX, int windowY)
        {
            if (!_notifyPartyMembers)
            {
                return;
            }

            int checkX = windowX + CHECKBOX_X;
            int checkY = windowY + CHECKBOX_Y;

            if (_checkboxTexture != null)
            {
                sprite.Draw(_checkboxTexture, new Vector2(checkX, checkY), Color.White);
                return;
            }

            Rectangle checkRect = new(checkX, checkY, CHECKBOX_SIZE, CHECKBOX_SIZE);
            sprite.Draw(_textPixelTexture, checkRect, new Color(147, 221, 122, 220));
        }

        private void DrawDraggedSkill(SpriteBatch sprite)
        {
            var icon = GetSkillIcon(_dragSkillId);
            if (icon != null)
            {
                int dragX = (int)_dragPosition.X - SKILL_ICON_SIZE / 2;
                int dragY = (int)_dragPosition.Y - SKILL_ICON_SIZE / 2;
                icon.DrawBackground(sprite, null, null, dragX, dragY, Color.White * 0.7f, false, null);
            }
        }

        private void DrawDraggedMacro(SpriteBatch sprite)
        {
            SkillMacro macro = GetMacro(_dragMacroIndex);
            int skillId = macro?.SkillIds?.FirstOrDefault(id => id > 0) ?? 0;
            int drawX = (int)_dragPosition.X - SKILL_ICON_SIZE / 2;
            int drawY = (int)_dragPosition.Y - SKILL_ICON_SIZE / 2;

            if (skillId > 0)
            {
                IDXObject icon = GetSkillIcon(skillId);
                icon?.DrawBackground(sprite, null, null, drawX, drawY, Color.White * 0.8f, false, null);
            }
            else
            {
                sprite.Draw(_emptySlotTexture, new Rectangle(drawX, drawY, SKILL_ICON_SIZE, SKILL_ICON_SIZE), Color.White);
            }

            if (_font != null)
            {
                sprite.DrawString(_font, $"M{_dragMacroIndex + 1}",
                    new Vector2(drawX - 2, drawY - 14),
                    Color.Yellow);
            }
        }

        private IDXObject GetSkillIcon(int skillId)
        {
            if (_skillIconCache.TryGetValue(skillId, out var cached))
                return cached;

            var skill = _skillLoader?.LoadSkill(skillId);
            if (skill?.Icon != null)
            {
                _skillIconCache[skillId] = skill.Icon;
                return skill.Icon;
            }

            return null;
        }
        #endregion

        #region Macro Management
        /// <summary>
        /// Load a macro into the editing state
        /// </summary>
        private void LoadMacroForEditing(int index)
        {
            if (index < 0 || index >= MAX_MACRO_SLOTS)
                return;

            _editingMacroIndex = index;
            _editingMacroName = _macros[index].Name ?? "";
            _nameFieldFocused = false;
            _editingCursorPosition = _editingMacroName.Length;
            ClearNameSelection();
            _nameSelectionDragActive = false;
            _notifyPartyMembers = _macros[index].NotifyParty;
            _validationMessage = string.Empty;
            ClearOwnerNotice();
            _caretBlinkTick = Environment.TickCount;
            ClearCompositionText();

            for (int i = 0; i < SKILLS_PER_MACRO; i++)
            {
                _editingSkillIds[i] = _macros[index].SkillIds[i];
            }
            UpdateOwnerButtonState();
        }

        /// <summary>
        /// Save the currently editing macro
        /// </summary>
        public bool SaveCurrentMacro()
        {
            if (_editingMacroIndex < 0 || _editingMacroIndex >= MAX_MACRO_SLOTS)
                return false;

            string validatedName = ValidateMacroName(_editingMacroName);
            if (validatedName == null)
                return false;

            if (!_editingSkillIds.Any(skillId => skillId > 0))
            {
                _validationMessage = "Assign at least one skill.";
                return false;
            }

            _macros[_editingMacroIndex].Name = validatedName;
            _macros[_editingMacroIndex].NotifyParty = _notifyPartyMembers;
            for (int i = 0; i < SKILLS_PER_MACRO; i++)
            {
                _macros[_editingMacroIndex].SkillIds[i] = _editingSkillIds[i];
            }

            OnMacroSaved?.Invoke(_editingMacroIndex, _macros[_editingMacroIndex]);
            _validationMessage = string.Empty;
            SetOwnerNotice(
                SkillMacroOwnerStringPoolText.GetSaveNotice(),
                new Color(216, 226, 183));
            UpdateOwnerButtonState();
            return true;
        }

        private bool CanSaveCurrentMacro()
        {
            return _editingMacroIndex >= 0
                && _editingMacroIndex < MAX_MACRO_SLOTS
                && _editingSkillIds.Any(skillId => skillId > 0)
                && SkillMacroNameRules.TryNormalize(_editingMacroName, out _, out _);
        }

        private void ShowSoftKeyboard(bool resetDismissedState = false)
        {
            SetNameFieldFocus(true);
            _softKeyboardActive = true;
            if (resetDismissedState || !_softKeyboardVisible)
            {
                _softKeyboardVisible = true;
                _softKeyboardMinimized = false;
            }

            ResetSoftKeyboardTransientState();
        }

        private void HideSoftKeyboard()
        {
            _softKeyboardActive = false;
            _softKeyboardVisible = false;
            _softKeyboardMinimized = false;
            ResetSoftKeyboardTransientState();
        }

        private void ResetSoftKeyboardTransientState()
        {
            _hoveredSoftKeyboardKeyIndex = -1;
            _hoveredSoftKeyboardFunctionKey = SkillMacroSoftKeyboardFunctionKey.None;
            _hoveredSoftKeyboardWindowButton = SkillMacroSoftKeyboardWindowButton.None;
            _pressedSoftKeyboardKeyIndex = -1;
            _pressedSoftKeyboardFunctionKey = SkillMacroSoftKeyboardFunctionKey.None;
            _pressedSoftKeyboardWindowButton = SkillMacroSoftKeyboardWindowButton.None;
            _softKeyboardPressedVisualUntil = 0;
            _softKeyboardShift = false;
        }

        private bool IsSoftKeyboardVisible => _softKeyboardVisible && _editingMacroIndex >= 0 && _nameFieldFocused;

        private void SetNameFieldFocus(bool focused)
        {
            if (_nameFieldFocused == focused)
            {
                if (focused)
                {
                    _caretBlinkTick = Environment.TickCount;
                }

                return;
            }

            _nameFieldFocused = focused;
            _caretBlinkTick = Environment.TickCount;
            if (!focused)
            {
                _softKeyboardActive = false;
                _nameSelectionDragActive = false;
                ClearNameSelection();
                ClearCompositionText();
            }
        }

        private Point GetSoftKeyboardPosition()
        {
            int windowWidth = CurrentFrame?.Width ?? WINDOW_WIDTH_BB;
            int windowHeight = CurrentFrame?.Height ?? WINDOW_HEIGHT_BB;
            return new Point(
                Position.X + Math.Max(0, windowWidth - SkillMacroSoftKeyboardLayout.ExpandedWidth),
                Position.Y + windowHeight + 6);
        }

        private bool IsPointInSoftKeyboard(int mouseX, int mouseY)
        {
            return IsSoftKeyboardVisible && SkillMacroSoftKeyboardLayout.GetBounds(GetSoftKeyboardPosition(), _softKeyboardMinimized).Contains(mouseX, mouseY);
        }

        private bool IsSoftKeyboardUppercase()
        {
            return _softKeyboardCapsLock ^ _softKeyboardShift;
        }

        private SkillMacroSoftKeyboardConstraintType GetSoftKeyboardConstraintType()
        {
            return SkillMacroSoftKeyboardConstraintType.AlphaNumeric;
        }

        internal static int GetSoftKeyboardTextLengthBytes(string text, Encoding encoding = null)
        {
            return SkillMacroNameRules.GetByteCount(text, encoding);
        }

        private int GetSoftKeyboardConstraintLength()
        {
            return GetSoftKeyboardTextLengthBytes(_editingMacroName);
        }

        private int GetSoftKeyboardConstraintMaxLength()
        {
            return SkillMacroNameRules.MaxNameBytes;
        }

        private SkillMacroSoftKeyboardConstraintMode GetSoftKeyboardConstraintMode()
        {
            return SkillMacroSoftKeyboardLayout.ResolveConstraintMode(
                GetSoftKeyboardConstraintType(),
                GetSoftKeyboardConstraintLength(),
                GetSoftKeyboardConstraintMaxLength());
        }

        private bool IsSoftKeyboardKeyEnabled(int keyIndex)
        {
            SkillMacroSoftKeyboardConstraintMode mode = GetSoftKeyboardConstraintMode();
            if (!IsSoftKeyboardKeyFamilyEnabled(keyIndex, mode))
            {
                return false;
            }

            string insertedText = SkillMacroSoftKeyboardLayout.GetKeyText(keyIndex, IsSoftKeyboardUppercase());
            return CanInsertSoftKeyboardText(insertedText);
        }

        private bool IsSoftKeyboardFunctionKeyEnabled(SkillMacroSoftKeyboardFunctionKey key)
        {
            SkillMacroSoftKeyboardConstraintMode mode = GetSoftKeyboardConstraintMode();
            return key switch
            {
                SkillMacroSoftKeyboardFunctionKey.CapsLock => SkillMacroSoftKeyboardLayout.IsAlphabeticFamilyEnabled(mode),
                SkillMacroSoftKeyboardFunctionKey.LeftShift or SkillMacroSoftKeyboardFunctionKey.RightShift => SkillMacroSoftKeyboardLayout.IsAlphabeticFamilyEnabled(mode),
                SkillMacroSoftKeyboardFunctionKey.Enter => CanSaveCurrentMacro(),
                SkillMacroSoftKeyboardFunctionKey.Backspace => !string.IsNullOrEmpty(_editingMacroName) || HasNameSelection,
                _ => false
            };
        }

        private static bool IsSoftKeyboardKeyFamilyEnabled(int keyIndex, SkillMacroSoftKeyboardConstraintMode mode)
        {
            if (mode == SkillMacroSoftKeyboardConstraintMode.Disabled)
            {
                return false;
            }

            if (SkillMacroSoftKeyboardLayout.IsNumericKey(keyIndex))
            {
                return SkillMacroSoftKeyboardLayout.IsNumericFamilyEnabled(mode);
            }

            if (SkillMacroSoftKeyboardLayout.IsAlphabeticKey(keyIndex))
            {
                return SkillMacroSoftKeyboardLayout.IsAlphabeticFamilyEnabled(mode);
            }

            return false;
        }

        private bool CanInsertSoftKeyboardText(string text)
        {
            return SkillMacroNameRules.TryInsertBestEffort(
                _editingMacroName,
                _editingCursorPosition,
                text,
                out _,
                out int insertedLength,
                out _)
                && insertedLength > 0;
        }

        private bool TryInsertSoftKeyboardText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            ClearCompositionText();
            DeleteNameSelectionIfAny();
            if (SkillMacroNameRules.TryInsertBestEffort(_editingMacroName, _editingCursorPosition, text, out string updatedText, out int insertedLength, out string error)
                && insertedLength > 0)
            {
                _editingMacroName = updatedText;
                _editingCursorPosition = Math.Clamp(_editingCursorPosition + insertedLength, 0, _editingMacroName.Length);
                _validationMessage = string.Empty;
                _caretBlinkTick = Environment.TickCount;
                return true;
            }

            if (!string.IsNullOrEmpty(error))
            {
                _validationMessage = error;
            }

            return false;
        }

        private void RemoveCharacterBeforeCursor()
        {
            if (!SkillMacroNameRules.TryRemoveTextElementBeforeCaret(_editingMacroName, _editingCursorPosition, out string updatedText, out int updatedCaretIndex))
            {
                return;
            }

            ClearCompositionText();
            ClearOwnerNotice();
            _editingMacroName = updatedText;
            _editingCursorPosition = updatedCaretIndex;
            _validationMessage = string.Empty;
            _caretBlinkTick = Environment.TickCount;
        }

        private void RemoveCharacterAtCursor()
        {
            if (!SkillMacroNameRules.TryRemoveTextElementAtCaret(_editingMacroName, _editingCursorPosition, out string updatedText, out int updatedCaretIndex))
            {
                return;
            }

            ClearCompositionText();
            ClearOwnerNotice();
            _editingMacroName = updatedText;
            _editingCursorPosition = updatedCaretIndex;
            _validationMessage = string.Empty;
            _caretBlinkTick = Environment.TickCount;
        }

        /// <summary>
        /// Delete the selected macro
        /// </summary>
        public void DeleteSelectedMacro()
        {
            if (_selectedMacroIndex < 0 || _selectedMacroIndex >= MAX_MACRO_SLOTS)
                return;

            _macros[_selectedMacroIndex] = CreateDefaultMacro(_selectedMacroIndex);
            OnMacroDeleted?.Invoke(_selectedMacroIndex);
            LoadMacroForEditing(_selectedMacroIndex);
            ClearOwnerNotice();
        }

        /// <summary>
        /// Set a skill in a specific macro slot
        /// </summary>
        public void SetMacroSkill(int macroIndex, int skillSlot, int skillId)
        {
            if (macroIndex < 0 || macroIndex >= MAX_MACRO_SLOTS)
                return;
            if (skillSlot < 0 || skillSlot >= SKILLS_PER_MACRO)
                return;

            _macros[macroIndex].SkillIds[skillSlot] = skillId;

            // Update editing state if this is the currently editing macro
            if (macroIndex == _editingMacroIndex)
            {
                _editingSkillIds[skillSlot] = skillId;
                ClearOwnerNotice();
            }
        }

        /// <summary>
        /// Clear a skill from a specific macro slot
        /// </summary>
        public void ClearMacroSkill(int macroIndex, int skillSlot)
        {
            SetMacroSkill(macroIndex, skillSlot, 0);
        }

        /// <summary>
        /// Get the macro at the specified index
        /// </summary>
        public SkillMacro GetMacro(int index)
        {
            if (index < 0 || index >= MAX_MACRO_SLOTS)
                return null;
            return _macros[index];
        }

        /// <summary>
        /// Execute the macro (trigger all skills in sequence)
        /// </summary>
        public void ExecuteMacro(int macroIndex)
        {
            if (macroIndex < 0 || macroIndex >= MAX_MACRO_SLOTS)
                return;

            _skillManager?.TryExecuteMacro(macroIndex, Environment.TickCount);
        }

        private string ValidateMacroName(string name)
        {
            if (SkillMacroNameRules.TryNormalize(name, out string normalized, out string error))
            {
                _validationMessage = string.Empty;
                return normalized;
            }

            _validationMessage = error;
            return null;
        }

        private static SkillMacro CreateDefaultMacro(int slotIndex)
        {
            return new SkillMacro
            {
                Name = $"Macro {slotIndex + 1}",
                SkillIds = new int[SKILLS_PER_MACRO]
            };
        }

        private static SkillMacro CloneMacro(SkillMacro source, int slotIndex)
        {
            SkillMacro clone = CreateDefaultMacro(slotIndex);
            if (source == null)
            {
                return clone;
            }

            clone.Name = string.IsNullOrWhiteSpace(source.Name) ? clone.Name : source.Name;
            clone.NotifyParty = source.NotifyParty;
            if (source.SkillIds != null)
            {
                for (int i = 0; i < Math.Min(SKILLS_PER_MACRO, source.SkillIds.Length); i++)
                {
                    clone.SkillIds[i] = Math.Max(0, source.SkillIds[i]);
                }
            }

            return clone;
        }
        #endregion

        #region Mouse Input
        /// <summary>
        /// Get the macro index at a mouse position
        /// </summary>
        public int GetMacroIndexAtPosition(int mouseX, int mouseY)
        {
            int relX = mouseX - Position.X - MACRO_SLOT_X;
            int relY = mouseY - Position.Y - MACRO_SLOT_Y;

            if (relX < 0 || relX > MACRO_SLOT_WIDTH)
                return -1;
            if (relY < 0)
                return -1;

            int index = relY / MACRO_SLOT_SPACING;

            int slotY = index * MACRO_SLOT_SPACING;
            if (relY - slotY > MACRO_SLOT_HEIGHT)
                return -1;

            if (index >= 0 && index < MAX_MACRO_SLOTS)
                return index;

            return -1;
        }

        /// <summary>
        /// Get the skill slot index within a macro at a mouse position
        /// </summary>
        public int GetSkillSlotAtPosition(int mouseX, int mouseY, int macroIndex)
        {
            if (macroIndex < 0 || macroIndex >= MAX_MACRO_SLOTS)
                return -1;

            int skillRowX = Position.X + SKILL_SLOT_START_X;
            int skillRowY = Position.Y + SKILL_SLOT_Y + (macroIndex * MACRO_SLOT_SPACING);
            int relX = mouseX - skillRowX;
            int relY = mouseY - skillRowY;

            if (relY < 0 || relY > SKILL_ICON_SIZE)
                return -1;

            // Check each skill slot
            for (int i = 0; i < SKILLS_PER_MACRO; i++)
            {
                int skillStartX = i * SKILL_SLOT_SPACING;
                if (relX >= skillStartX && relX < skillStartX + SKILL_ICON_SIZE)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Handle mouse move for hover effects
        /// </summary>
        public void OnMouseMove(int mouseX, int mouseY)
        {
            _lastMousePosition = new Point(mouseX, mouseY);

            if (_dragMode != MacroDragMode.None)
            {
                _dragPosition = new Vector2(mouseX, mouseY);
            }

            if (_softKeyboardPressedVisualUntil > 0 && Environment.TickCount >= _softKeyboardPressedVisualUntil)
            {
                _pressedSoftKeyboardKeyIndex = -1;
                _pressedSoftKeyboardFunctionKey = SkillMacroSoftKeyboardFunctionKey.None;
                _pressedSoftKeyboardWindowButton = SkillMacroSoftKeyboardWindowButton.None;
                _softKeyboardPressedVisualUntil = 0;
            }

            if (_nameSelectionDragActive)
            {
                MouseState mouseState = Mouse.GetState();
                if (mouseState.LeftButton == ButtonState.Pressed && _editingMacroIndex >= 0)
                {
                    _editingCursorPosition = ResolveNameCursorFromMouse(mouseX);
                    _caretBlinkTick = Environment.TickCount;
                }
                else
                {
                    _nameSelectionDragActive = false;
                }
            }

            if (IsSoftKeyboardVisible)
            {
                Point softKeyboardPosition = GetSoftKeyboardPosition();
                int localX = mouseX - softKeyboardPosition.X;
                int localY = mouseY - softKeyboardPosition.Y;
                _hoveredSoftKeyboardWindowButton = SkillMacroSoftKeyboardLayout.GetWindowButtonFromPoint(localX, localY);
                _hoveredSoftKeyboardFunctionKey = _hoveredSoftKeyboardWindowButton == SkillMacroSoftKeyboardWindowButton.None
                    ? SkillMacroSoftKeyboardLayout.GetFunctionKeyFromPoint(localX, localY, _softKeyboardMinimized)
                    : SkillMacroSoftKeyboardFunctionKey.None;
                _hoveredSoftKeyboardKeyIndex = (!_softKeyboardMinimized
                                                && _hoveredSoftKeyboardWindowButton == SkillMacroSoftKeyboardWindowButton.None
                                                && _hoveredSoftKeyboardFunctionKey == SkillMacroSoftKeyboardFunctionKey.None)
                    ? SkillMacroSoftKeyboardLayout.GetKeyIndexFromPoint(localX, localY)
                    : -1;
            }
            else
            {
                _hoveredSoftKeyboardKeyIndex = -1;
                _hoveredSoftKeyboardFunctionKey = SkillMacroSoftKeyboardFunctionKey.None;
                _hoveredSoftKeyboardWindowButton = SkillMacroSoftKeyboardWindowButton.None;
            }

            _hoveredMacroIndex = GetMacroIndexAtPosition(mouseX, mouseY);
            if (_hoveredMacroIndex >= 0)
            {
                _hoveredSkillSlot = GetSkillSlotAtPosition(mouseX, mouseY, _hoveredMacroIndex);
            }
            else
            {
                _hoveredSkillSlot = -1;
            }
        }

        /// <summary>
        /// Handle mouse down
        /// </summary>
        public void OnMouseDown(int mouseX, int mouseY, bool leftButton, bool rightButton)
        {
            if (leftButton && IsPointInSoftKeyboard(mouseX, mouseY))
            {
                HandleSoftKeyboardMouseDown(mouseX, mouseY);
                return;
            }

            if (IsPointInImeCandidateWindow(mouseX, mouseY))
            {
                HandleImeCandidateMouseDown(mouseX, mouseY, leftButton);
                return;
            }

            int macroIndex = GetMacroIndexAtPosition(mouseX, mouseY);

            if (macroIndex < 0)
            {
                if (leftButton && IsPointInNameField(mouseX, mouseY))
                {
                    ShowSoftKeyboard(resetDismissedState: true);
                    ClearOwnerNotice();
                    int updatedCursorPosition = ResolveNameCursorFromMouse(mouseX);
                    bool extendSelection = IsShiftHeld();
                    MoveNameCaret(updatedCursorPosition, extendSelection);
                    if (!extendSelection)
                    {
                        _editingSelectionAnchor = updatedCursorPosition;
                    }

                    _nameSelectionDragActive = true;
                    if (_compositionText.Length > 0)
                    {
                        _compositionInsertionIndex = updatedCursorPosition;
                    }

                    _validationMessage = string.Empty;
                    _caretBlinkTick = Environment.TickCount;
                    UpdateImePresentationPlacement();
                }
                else if (_editingMacroIndex >= 0 && IsPointInCheckbox(mouseX, mouseY) && leftButton)
                {
                    SetNameFieldFocus(false);
                    _notifyPartyMembers = !_notifyPartyMembers;
                    ClearOwnerNotice();
                }
                else if (leftButton)
                {
                    SetNameFieldFocus(false);
                }
                return;
            }

            int skillSlot = GetSkillSlotAtPosition(mouseX, mouseY, macroIndex);

            if (rightButton && skillSlot >= 0)
            {
                SetNameFieldFocus(false);
                // Right-click to clear skill slot
                ClearMacroSkill(macroIndex, skillSlot);
            }
            else if (leftButton)
            {
                if (skillSlot >= 0)
                {
                    SetNameFieldFocus(false);
                    int skillId = _macros[macroIndex].SkillIds[skillSlot];
                    if (skillId > 0)
                    {
                        _dragMode = MacroDragMode.Skill;
                        _dragSkillId = skillId;
                        _dragSourceMacro = macroIndex;
                        _dragSourceSlot = skillSlot;
                        _dragPosition = new Vector2(mouseX, mouseY);
                    }
                }
                else if (_selectedMacroIndex == macroIndex && _macros[macroIndex].IsEnabled)
                {
                    SetNameFieldFocus(false);
                    _dragMode = MacroDragMode.MacroBinding;
                    _dragMacroIndex = macroIndex;
                    _dragPosition = new Vector2(mouseX, mouseY);
                }
                else
                {
                    SetNameFieldFocus(false);
                    SelectedMacroIndex = macroIndex;
                }
            }
        }

        /// <summary>
        /// Handle mouse up
        /// </summary>
        public void OnMouseUp(int mouseX, int mouseY)
        {
            _nameSelectionDragActive = false;

            if (_dragMode == MacroDragMode.Skill)
            {
                int targetMacro = GetMacroIndexAtPosition(mouseX, mouseY);
                int targetSlot = targetMacro >= 0 ? GetSkillSlotAtPosition(mouseX, mouseY, targetMacro) : -1;

                if (targetMacro >= 0 && targetSlot >= 0)
                {
                    // Drop skill onto target slot
                    int targetSkillId = _macros[targetMacro].SkillIds[targetSlot];

                    // Swap skills
                    SetMacroSkill(targetMacro, targetSlot, _dragSkillId);
                    if (_dragSourceMacro >= 0 && _dragSourceSlot >= 0)
                    {
                        SetMacroSkill(_dragSourceMacro, _dragSourceSlot, targetSkillId);
                    }
                }
            }

            CancelDrag();
        }

        /// <summary>
        /// Accept a skill drop from SkillUI
        /// </summary>
        public bool AcceptSkillDrop(int skillId, int mouseX, int mouseY)
        {
            int macroIndex = GetMacroIndexAtPosition(mouseX, mouseY);
            if (macroIndex < 0)
                return false;

            int skillSlot = GetSkillSlotAtPosition(mouseX, mouseY, macroIndex);
            if (skillSlot < 0)
            {
                // Find first empty slot in the macro
                for (int i = 0; i < SKILLS_PER_MACRO; i++)
                {
                    if (_macros[macroIndex].SkillIds[i] == 0)
                    {
                        skillSlot = i;
                        break;
                    }
                }
            }

            if (skillSlot >= 0)
            {
                SetMacroSkill(macroIndex, skillSlot, skillId);
                return true;
            }

            return false;
        }

        private bool IsPointInCheckbox(int mouseX, int mouseY)
        {
            if (_editingMacroIndex < 0)
            {
                return false;
            }

            int checkX = Position.X + CHECKBOX_X;
            int checkY = Position.Y + CHECKBOX_Y;

            return mouseX >= checkX && mouseX <= checkX + CHECKBOX_SIZE &&
                   mouseY >= checkY && mouseY <= checkY + CHECKBOX_SIZE;
        }

        private Rectangle GetNameFieldBounds()
        {
            return new Rectangle(Position.X + NAME_FIELD_X, Position.Y + NAME_FIELD_Y, NAME_FIELD_WIDTH, NAME_FIELD_HEIGHT);
        }

        private bool IsPointInNameField(int mouseX, int mouseY)
        {
            return _editingMacroIndex >= 0 && GetNameFieldBounds().Contains(mouseX, mouseY);
        }

        private bool HasNameSelection => GetNameSelectionLength() > 0;

        private int GetNameSelectionStart()
        {
            return ClientEditSelectionHelper.GetSelectionStart(
                _editingMacroName?.Length ?? 0,
                _editingSelectionAnchor,
                _editingCursorPosition);
        }

        private int GetNameSelectionLength()
        {
            return ClientEditSelectionHelper.GetSelectionLength(
                _editingMacroName?.Length ?? 0,
                _editingSelectionAnchor,
                _editingCursorPosition);
        }

        private void ClearNameSelection()
        {
            _editingSelectionAnchor = -1;
        }

        private void MoveNameCaret(int targetPosition, bool extendSelection)
        {
            int textLength = _editingMacroName?.Length ?? 0;
            int clampedPosition = Math.Clamp(targetPosition, 0, textLength);
            if (extendSelection)
            {
                _editingSelectionAnchor = _editingSelectionAnchor >= 0
                    ? Math.Clamp(_editingSelectionAnchor, 0, textLength)
                    : Math.Clamp(_editingCursorPosition, 0, textLength);
            }
            else
            {
                ClearNameSelection();
            }

            _editingCursorPosition = clampedPosition;
        }

        private bool DeleteNameSelectionIfAny()
        {
            if (!ClientEditSelectionHelper.TryDeleteSelection(
                _editingMacroName,
                _editingSelectionAnchor,
                _editingCursorPosition,
                out string updatedText,
                out int updatedCaretIndex))
            {
                return false;
            }

            _editingMacroName = updatedText;
            _editingCursorPosition = updatedCaretIndex;
            ClearNameSelection();
            return true;
        }

        private void SelectAllNameText()
        {
            if (string.IsNullOrEmpty(_editingMacroName))
            {
                ClearNameSelection();
                _editingCursorPosition = 0;
                _caretBlinkTick = Environment.TickCount;
                return;
            }

            _editingSelectionAnchor = 0;
            _editingCursorPosition = _editingMacroName.Length;
            _caretBlinkTick = Environment.TickCount;
        }

        private void CopySelectedNameText(bool cutSelection)
        {
            int selectionStart = GetNameSelectionStart();
            int selectionLength = GetNameSelectionLength();
            if (selectionStart < 0 || selectionLength <= 0)
            {
                return;
            }

            try
            {
                System.Windows.Forms.Clipboard.SetText(_editingMacroName.Substring(selectionStart, selectionLength));
            }
            catch
            {
                return;
            }

            if (!cutSelection)
            {
                return;
            }

            DeleteNameSelectionIfAny();
            _validationMessage = string.Empty;
            _caretBlinkTick = Environment.TickCount;
        }

        private static bool IsShiftHeld()
        {
            KeyboardState keyboardState = Keyboard.GetState();
            return keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
        }

        private int ResolveNameCursorFromMouse(int mouseX)
        {
            if (_font == null || string.IsNullOrEmpty(_editingMacroName))
            {
                return 0;
            }

            Rectangle bounds = GetNameFieldBounds();
            float targetX = mouseX - bounds.X - NAME_FIELD_TEXT_INSET_X;
            if (targetX <= 0f)
            {
                return 0;
            }

            int bestCursor = _editingMacroName.Length;
            float bestDistance = float.MaxValue;
            foreach (int caretStop in EnumerateCaretStops(_editingMacroName))
            {
                string prefix = caretStop <= 0 ? string.Empty : _editingMacroName[..caretStop];
                float prefixWidth = prefix.Length > 0 ? _font.MeasureString(prefix).X : 0f;
                float distance = Math.Abs(prefixWidth - targetX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCursor = caretStop;
                }
            }

            return bestCursor;
        }

        private void DrawCommittedNameText(SpriteBatch sprite, string committedText, int selectionStart, int selectionEnd, Vector2 textPosition)
        {
            if (string.IsNullOrEmpty(committedText))
            {
                return;
            }

            if (selectionStart < 0 || selectionEnd <= selectionStart)
            {
                sprite.DrawString(_font, committedText, textPosition, Color.White);
                return;
            }

            string prefix = selectionStart > 0 ? committedText[..selectionStart] : string.Empty;
            string selectedText = committedText.Substring(selectionStart, selectionEnd - selectionStart);
            string suffix = selectionEnd < committedText.Length ? committedText[selectionEnd..] : string.Empty;

            if (prefix.Length > 0)
            {
                sprite.DrawString(_font, prefix, textPosition, Color.White);
            }

            float prefixWidth = prefix.Length > 0 ? _font.MeasureString(prefix).X : 0f;
            Vector2 selectionPosition = new(textPosition.X + prefixWidth, textPosition.Y);
            if (selectedText.Length > 0)
            {
                Vector2 selectionSize = _font.MeasureString(selectedText);
                sprite.Draw(
                    _textPixelTexture,
                    new Rectangle(
                        (int)Math.Floor(selectionPosition.X),
                        (int)Math.Floor(selectionPosition.Y),
                        Math.Max(1, (int)Math.Ceiling(selectionSize.X)),
                        Math.Max(1, Math.Min(NAME_FIELD_HEIGHT, _font.LineSpacing))),
                    new Color(89, 108, 147, 220));
                sprite.DrawString(_font, selectedText, selectionPosition, Color.White);
            }

            if (suffix.Length > 0)
            {
                float selectionWidth = selectedText.Length > 0 ? _font.MeasureString(selectedText).X : 0f;
                sprite.DrawString(_font, suffix, new Vector2(selectionPosition.X + selectionWidth, selectionPosition.Y), Color.White);
            }
        }

        private void HandleSoftKeyboardMouseDown(int mouseX, int mouseY)
        {
            Point origin = GetSoftKeyboardPosition();
            int localX = mouseX - origin.X;
            int localY = mouseY - origin.Y;

            SkillMacroSoftKeyboardWindowButton windowButton = SkillMacroSoftKeyboardLayout.GetWindowButtonFromPoint(localX, localY);
            if (windowButton != SkillMacroSoftKeyboardWindowButton.None)
            {
                _pressedSoftKeyboardWindowButton = windowButton;
                _softKeyboardPressedVisualUntil = Environment.TickCount + 120;
                switch (windowButton)
                {
                    case SkillMacroSoftKeyboardWindowButton.Close:
                        HideSoftKeyboard();
                        break;
                    case SkillMacroSoftKeyboardWindowButton.Minimize:
                        _softKeyboardMinimized = true;
                        ResetSoftKeyboardTransientState();
                        break;
                    case SkillMacroSoftKeyboardWindowButton.Maximize:
                        _softKeyboardVisible = true;
                        _softKeyboardMinimized = false;
                        ResetSoftKeyboardTransientState();
                        break;
                }

                return;
            }

            SkillMacroSoftKeyboardFunctionKey functionKey = SkillMacroSoftKeyboardLayout.GetFunctionKeyFromPoint(localX, localY, _softKeyboardMinimized);
            if (functionKey != SkillMacroSoftKeyboardFunctionKey.None)
            {
                if (!IsSoftKeyboardFunctionKeyEnabled(functionKey))
                {
                    return;
                }

                _pressedSoftKeyboardFunctionKey = functionKey;
                _softKeyboardPressedVisualUntil = Environment.TickCount + 120;

                switch (functionKey)
                {
                    case SkillMacroSoftKeyboardFunctionKey.CapsLock:
                        _softKeyboardCapsLock = !_softKeyboardCapsLock;
                        break;
                    case SkillMacroSoftKeyboardFunctionKey.LeftShift:
                    case SkillMacroSoftKeyboardFunctionKey.RightShift:
                        _softKeyboardShift = !_softKeyboardShift;
                        break;
                    case SkillMacroSoftKeyboardFunctionKey.Enter:
                        SaveCurrentMacro();
                        break;
                    case SkillMacroSoftKeyboardFunctionKey.Backspace:
                        if (!DeleteNameSelectionIfAny())
                        {
                            RemoveCharacterBeforeCursor();
                        }
                        break;
                }

                return;
            }

            if (_softKeyboardMinimized)
            {
                return;
            }

            int keyIndex = SkillMacroSoftKeyboardLayout.GetKeyIndexFromPoint(localX, localY);
            if (keyIndex < 0 || !IsSoftKeyboardKeyEnabled(keyIndex))
            {
                return;
            }

            _pressedSoftKeyboardKeyIndex = keyIndex;
            _softKeyboardPressedVisualUntil = Environment.TickCount + 120;
            if (TryInsertSoftKeyboardText(SkillMacroSoftKeyboardLayout.GetKeyText(keyIndex, IsSoftKeyboardUppercase())))
            {
                _softKeyboardShift = false;
            }
        }

        private void HandleImeCandidateMouseDown(int mouseX, int mouseY, bool leftButton)
        {
            SetNameFieldFocus(true);
            ClearOwnerNotice();
            _caretBlinkTick = Environment.TickCount;
            _nameSelectionDragActive = false;

            if (!leftButton)
            {
                return;
            }

            int candidateIndex = ResolveImeCandidateIndexFromPoint(mouseX, mouseY);
            if (candidateIndex < 0)
            {
                return;
            }

            OnImeCandidateSelected?.Invoke(_candidateListState.ListIndex, candidateIndex);
        }

        public bool HandlesMacroInteractionPoint(int mouseX, int mouseY)
        {
            return GetMacroIndexAtPosition(mouseX, mouseY) >= 0
                || IsPointInCheckbox(mouseX, mouseY)
                || IsPointInNameField(mouseX, mouseY)
                || IsPointInImeCandidateWindow(mouseX, mouseY)
                || IsPointInSoftKeyboard(mouseX, mouseY);
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            foreach (Rectangle bounds in base.GetAdditionalInteractiveBounds())
            {
                yield return bounds;
            }

            if (IsSoftKeyboardVisible)
            {
                yield return SkillMacroSoftKeyboardLayout.GetBounds(GetSoftKeyboardPosition(), _softKeyboardMinimized);
            }

            Rectangle candidateBounds = GetImeCandidateWindowBounds(GetActiveViewport());
            if (!candidateBounds.IsEmpty)
            {
                yield return candidateBounds;
            }
        }
        #endregion

        #region Button Handlers
        private void OnOKClicked(UIObject sender)
        {
            SetNameFieldFocus(false);
            SaveCurrentMacro();
        }

        private void OnCancelClicked(UIObject sender)
        {
            ResetEditingState();
            HideSoftKeyboard();
            Hide();
            OnMacroWindowClosed?.Invoke();
        }

        private void OnDeleteClicked(UIObject sender)
        {
            DeleteSelectedMacro();
        }

        public override void Show()
        {
            ResetOwnerSession();
            base.Show();
        }

        public override void Hide()
        {
            HideSoftKeyboard();
            CancelDrag();
            ClearCompositionText();
            base.Hide();
        }
        #endregion

        #region Update
        private KeyboardState _previousKeyboardState;

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboardState = Keyboard.GetState();

            if (_softKeyboardPressedVisualUntil > 0 && Environment.TickCount >= _softKeyboardPressedVisualUntil)
            {
                _pressedSoftKeyboardKeyIndex = -1;
                _pressedSoftKeyboardFunctionKey = SkillMacroSoftKeyboardFunctionKey.None;
                _pressedSoftKeyboardWindowButton = SkillMacroSoftKeyboardWindowButton.None;
                _softKeyboardPressedVisualUntil = 0;
            }

            if (IsVisible && SkillMacroOwnerKeyHandler.ShouldCloseWindow(keyboardState, _previousKeyboardState))
            {
                OnCancelClicked(null);
                _previousKeyboardState = keyboardState;
                return;
            }

            if (!CapturesKeyboardInput)
            {
                _previousKeyboardState = keyboardState;
                return;
            }

            HandleKeyboardInput(keyboardState);
            _previousKeyboardState = keyboardState;
            UpdateImePresentationPlacement();
        }
        #endregion

        #region Cleanup
        /// <summary>
        /// Clear cached icons
        /// </summary>
        public void ClearIconCache()
        {
            _skillIconCache.Clear();
        }

        public void CancelDrag()
        {
            _dragMode = MacroDragMode.None;
            _dragSkillId = 0;
            _dragMacroIndex = -1;
            _dragSourceMacro = -1;
            _dragSourceSlot = -1;
        }

        public override void HandleCommittedText(string text)
        {
            if (!CapturesKeyboardInput || string.IsNullOrEmpty(text))
            {
                return;
            }

            ClearCompositionText();
            ClearOwnerNotice();
            DeleteNameSelectionIfAny();
            if (SkillMacroNameRules.TryInsertBestEffort(_editingMacroName, _editingCursorPosition, text, out string updatedText, out int insertedLength, out string error))
            {
                _editingMacroName = updatedText;
                _editingCursorPosition = Math.Clamp(_editingCursorPosition + insertedLength, 0, _editingMacroName.Length);
                _validationMessage = string.Empty;
                _caretBlinkTick = Environment.TickCount;
            }
            else if (!string.IsNullOrEmpty(error))
            {
                _validationMessage = error;
            }
        }

        public override void HandleCompositionState(ImeCompositionState state)
        {
            if (!CapturesKeyboardInput)
            {
                ClearCompositionText();
                return;
            }

            ImeCompositionState effectiveState = state ?? ImeCompositionState.Empty;
            string sanitized = SanitizeCompositionText(effectiveState.Text);
            if (sanitized.Length == 0)
            {
                ClearCompositionText();
                return;
            }

            if (_compositionText.Length == 0)
            {
                DeleteNameSelectionIfAny();
                _compositionInsertionIndex = _editingCursorPosition;
            }

            int insertionIndex = Math.Clamp(_compositionInsertionIndex >= 0 ? _compositionInsertionIndex : _editingCursorPosition, 0, _editingMacroName.Length);
            string preview = SkillMacroNameRules.BuildCompositionPreview(_editingMacroName, insertionIndex, sanitized, out string error);
            if (preview.Length == 0)
            {
                ClearCompositionText();
                if (!string.IsNullOrEmpty(error))
                {
                    _validationMessage = error;
                }

                return;
            }

            _compositionInsertionIndex = insertionIndex;
            _compositionText = preview;
            _compositionClauseOffsets = ClampClauseOffsets(effectiveState.ClauseOffsets, preview.Length);
            _compositionCursorPosition = Math.Clamp(effectiveState.CursorPosition, -1, preview.Length);
            _validationMessage = string.Empty;
            ClearOwnerNotice();
            _caretBlinkTick = Environment.TickCount;
            UpdateImePresentationPlacement();
        }

        public override void HandleCompositionText(string text)
        {
            HandleCompositionState(new ImeCompositionState(text ?? string.Empty, Array.Empty<int>(), -1));
        }

        public override void ClearCompositionText()
        {
            _compositionText = string.Empty;
            _compositionInsertionIndex = -1;
            _compositionClauseOffsets = Array.Empty<int>();
            _compositionCursorPosition = -1;
            ClearImeCandidateList();
        }

        public override void HandleImeCandidateList(ImeCandidateListState state)
        {
            _candidateListState = CapturesKeyboardInput && state != null && state.HasCandidates
                ? state
                : ImeCandidateListState.Empty;
            UpdateImePresentationPlacement();
        }

        public override void ClearImeCandidateList()
        {
            _candidateListState = ImeCandidateListState.Empty;
        }

        Rectangle ISoftKeyboardHost.GetSoftKeyboardAnchorBounds() => GetNameFieldBounds();

        bool ISoftKeyboardHost.TryInsertSoftKeyboardCharacter(char character, out string errorMessage)
        {
            ClearOwnerNotice();
            if (!TryInsertSoftKeyboardText(character.ToString()))
            {
                errorMessage = string.IsNullOrWhiteSpace(_validationMessage)
                    ? "The macro name cannot accept that character."
                    : _validationMessage;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        bool ISoftKeyboardHost.TryReplaceLastSoftKeyboardCharacter(char character, out string errorMessage)
        {
            if (string.IsNullOrEmpty(_editingMacroName) && !HasNameSelection)
            {
                return ((ISoftKeyboardHost)this).TryInsertSoftKeyboardCharacter(character, out errorMessage);
            }

            ClearCompositionText();
            ClearOwnerNotice();
            if (!DeleteNameSelectionIfAny())
            {
                RemoveCharacterBeforeCursor();
            }
            if (!TryInsertSoftKeyboardText(character.ToString()))
            {
                errorMessage = string.IsNullOrWhiteSpace(_validationMessage)
                    ? "The macro name cannot accept that character."
                    : _validationMessage;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        bool ISoftKeyboardHost.TryBackspaceSoftKeyboard(out string errorMessage)
        {
            if (string.IsNullOrEmpty(_editingMacroName) && !HasNameSelection)
            {
                errorMessage = "The macro name is already empty.";
                return false;
            }

            ClearCompositionText();
            ClearOwnerNotice();
            if (!DeleteNameSelectionIfAny())
            {
                RemoveCharacterBeforeCursor();
            }
            errorMessage = string.Empty;
            return true;
        }

        bool ISoftKeyboardHost.TrySubmitSoftKeyboard(out string errorMessage)
        {
            if (!CanSaveCurrentMacro())
            {
                errorMessage = "The selected macro is not ready to save.";
                return false;
            }

            SaveCurrentMacro();
            errorMessage = string.Empty;
            return true;
        }

        void ISoftKeyboardHost.OnSoftKeyboardClosed()
        {
            _softKeyboardActive = false;
            ResetSoftKeyboardTransientState();
            UpdateImePresentationPlacement();
        }

        void ISoftKeyboardHost.SetSoftKeyboardCompositionText(string text)
        {
            HandleCompositionText(text);
        }

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            bool ctrl = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);

            if (!ctrl && shift && keyboardState.IsKeyDown(Keys.Insert) && _previousKeyboardState.IsKeyUp(Keys.Insert))
            {
                HandleClipboardPaste();
                return;
            }

            if (!ctrl && shift && keyboardState.IsKeyDown(Keys.Delete) && _previousKeyboardState.IsKeyUp(Keys.Delete))
            {
                CopySelectedNameText(cutSelection: true);
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Back) && _previousKeyboardState.IsKeyUp(Keys.Back))
            {
                if (_compositionText.Length > 0)
                {
                    ClearCompositionText();
                    _validationMessage = string.Empty;
                    _caretBlinkTick = Environment.TickCount;
                }
                else if (DeleteNameSelectionIfAny())
                {
                    _validationMessage = string.Empty;
                    _caretBlinkTick = Environment.TickCount;
                }
                else if (_editingCursorPosition > 0)
                {
                    RemoveCharacterBeforeCursor();
                }
            }

            if (keyboardState.IsKeyDown(Keys.Delete) && _previousKeyboardState.IsKeyUp(Keys.Delete))
            {
                if (_compositionText.Length > 0)
                {
                    ClearCompositionText();
                    _validationMessage = string.Empty;
                    _caretBlinkTick = Environment.TickCount;
                }
                else if (DeleteNameSelectionIfAny())
                {
                    _validationMessage = string.Empty;
                    _caretBlinkTick = Environment.TickCount;
                }
                else if (_editingCursorPosition < _editingMacroName.Length)
                {
                    RemoveCharacterAtCursor();
                }
            }

            if (keyboardState.IsKeyDown(Keys.Left) && _previousKeyboardState.IsKeyUp(Keys.Left))
            {
                ClearCompositionText();
                ClearOwnerNotice();
                int baseCaret = shift
                    ? Math.Clamp(_editingCursorPosition, 0, _editingMacroName.Length)
                    : ClientEditSelectionHelper.ResolveNavigationCaret(
                    _editingMacroName.Length,
                    _editingSelectionAnchor,
                    _editingCursorPosition,
                    moveRight: false);
                int targetCaret = !shift && HasNameSelection
                    ? baseCaret
                    : SkillMacroNameRules.GetPreviousCaretStop(_editingMacroName, baseCaret);
                MoveNameCaret(targetCaret, shift);
                _caretBlinkTick = Environment.TickCount;
            }

            if (keyboardState.IsKeyDown(Keys.Right) && _previousKeyboardState.IsKeyUp(Keys.Right))
            {
                ClearCompositionText();
                ClearOwnerNotice();
                int baseCaret = shift
                    ? Math.Clamp(_editingCursorPosition, 0, _editingMacroName.Length)
                    : ClientEditSelectionHelper.ResolveNavigationCaret(
                    _editingMacroName.Length,
                    _editingSelectionAnchor,
                    _editingCursorPosition,
                    moveRight: true);
                int targetCaret = !shift && HasNameSelection
                    ? baseCaret
                    : SkillMacroNameRules.GetNextCaretStop(_editingMacroName, baseCaret);
                MoveNameCaret(targetCaret, shift);
                _caretBlinkTick = Environment.TickCount;
            }

            if (keyboardState.IsKeyDown(Keys.Home) && _previousKeyboardState.IsKeyUp(Keys.Home))
            {
                ClearCompositionText();
                ClearOwnerNotice();
                MoveNameCaret(0, shift);
                _caretBlinkTick = Environment.TickCount;
            }

            if (keyboardState.IsKeyDown(Keys.End) && _previousKeyboardState.IsKeyUp(Keys.End))
            {
                ClearCompositionText();
                ClearOwnerNotice();
                MoveNameCaret(_editingMacroName.Length, shift);
                _validationMessage = string.Empty;
                _caretBlinkTick = Environment.TickCount;
            }

            if (_compositionInsertionIndex >= 0 && _compositionInsertionIndex != _editingCursorPosition)
            {
                _compositionInsertionIndex = _editingCursorPosition;
            }

            if (_compositionText.Length > 0)
            {
                _validationMessage = string.Empty;
            }

            if (ctrl && keyboardState.IsKeyDown(Keys.V) && _previousKeyboardState.IsKeyUp(Keys.V))
            {
                HandleClipboardPaste();
                return;
            }

            if (ctrl && keyboardState.IsKeyDown(Keys.A) && _previousKeyboardState.IsKeyUp(Keys.A))
            {
                SelectAllNameText();
                return;
            }

            if (ctrl && keyboardState.IsKeyDown(Keys.C) && _previousKeyboardState.IsKeyUp(Keys.C))
            {
                CopySelectedNameText(cutSelection: false);
                return;
            }

            if (ctrl && keyboardState.IsKeyDown(Keys.X) && _previousKeyboardState.IsKeyUp(Keys.X))
            {
                CopySelectedNameText(cutSelection: true);
                return;
            }
        }

        private void HandleClipboardPaste()
        {
            try
            {
                if (!System.Windows.Forms.Clipboard.ContainsText())
                {
                    return;
                }

                string clipboardText = System.Windows.Forms.Clipboard.GetText();
                if (string.IsNullOrEmpty(clipboardText))
                {
                    return;
                }

                string normalizedClipboardText = SanitizeCompositionText(
                    clipboardText.Replace("\r", string.Empty).Replace("\n", string.Empty));
                if (string.IsNullOrEmpty(normalizedClipboardText))
                {
                    return;
                }

                ClearCompositionText();
                ClearOwnerNotice();
                DeleteNameSelectionIfAny();
                if (SkillMacroNameRules.TryInsertBestEffort(_editingMacroName, _editingCursorPosition, normalizedClipboardText, out string updatedText, out int insertedLength, out string error))
                {
                    _editingMacroName = updatedText;
                    _editingCursorPosition = Math.Clamp(_editingCursorPosition + insertedLength, 0, _editingMacroName.Length);
                    _validationMessage = string.Empty;
                    _caretBlinkTick = Environment.TickCount;
                }
                else
                {
                    _validationMessage = error;
                }
            }
            catch
            {
                _validationMessage = "Clipboard paste is not available right now.";
            }
        }

        private void SetOwnerNotice(string message, Color color)
        {
            _ownerNoticeMessage = message ?? string.Empty;
            _ownerNoticeColor = color;
        }

        private void ClearOwnerNotice()
        {
            _ownerNoticeMessage = string.Empty;
            _ownerNoticeColor = Color.White;
        }

        private bool ShouldShowChangeNameTooltip()
        {
            if (_editingMacroIndex < 0
                || _selectedMacroIndex < 0
                || _btnOK == null
                || !_btnOK.ButtonVisible
                || !_btnOK.IsEnabled
                || _dragMode != MacroDragMode.None
                || !_lastMousePosition.HasValue)
            {
                return false;
            }

            return GetButtonBounds(_btnOK).Contains(_lastMousePosition.Value);
        }

        private Rectangle GetButtonBounds(UIObject button)
        {
            return new Rectangle(
                Position.X + button.X,
                Position.Y + button.Y,
                Math.Max(1, button.CanvasSnapshotWidth),
                Math.Max(1, button.CanvasSnapshotHeight));
        }

        private static string SanitizeCompositionText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new(text.Length);
            foreach (char ch in text)
            {
                if (!char.IsControl(ch))
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }

        private string BuildDisplayedNameText()
        {
            string committedText = _editingMacroName ?? string.Empty;
            if (string.IsNullOrEmpty(_compositionText))
            {
                return committedText;
            }

            int insertionIndex = Math.Clamp(_compositionInsertionIndex >= 0 ? _compositionInsertionIndex : _editingCursorPosition, 0, committedText.Length);
            return committedText.Insert(insertionIndex, _compositionText);
        }

        private void DrawImeCandidateWindow(SpriteBatch sprite)
        {
            if (_font == null || !_candidateListState.HasCandidates)
            {
                return;
            }

            Rectangle candidateBounds = GetImeCandidateWindowBounds(sprite.GraphicsDevice.Viewport);
            if (candidateBounds.Width <= 0 || candidateBounds.Height <= 0)
            {
                return;
            }

            sprite.Draw(_textPixelTexture, candidateBounds, new Color(33, 33, 41, 235));

            Color borderColor = new(214, 214, 214, 220);
            sprite.Draw(_textPixelTexture, new Rectangle(candidateBounds.X, candidateBounds.Y, candidateBounds.Width, 1), borderColor);
            sprite.Draw(_textPixelTexture, new Rectangle(candidateBounds.X, candidateBounds.Bottom - 1, candidateBounds.Width, 1), borderColor);
            sprite.Draw(_textPixelTexture, new Rectangle(candidateBounds.X, candidateBounds.Y, 1, candidateBounds.Height), borderColor);
            sprite.Draw(_textPixelTexture, new Rectangle(candidateBounds.Right - 1, candidateBounds.Y, 1, candidateBounds.Height), borderColor);

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(GetVisibleCandidateCount(), _candidateListState.Candidates.Count - start);
            if (count <= 0)
            {
                return;
            }

            if (_candidateListState.Vertical)
            {
                int rowHeight = GetClientCandidateRowHeight();
                int numberWidth = GetCandidateNumberWidth();
                for (int i = 0; i < count; i++)
                {
                    int candidateIndex = start + i;
                    string numberText = FormatCandidateNumber(i + 1);
                    Rectangle rowBounds = new(candidateBounds.X + 2, candidateBounds.Y + 2 + (i * rowHeight), candidateBounds.Width - 4, rowHeight);
                    bool selected = candidateIndex == _candidateListState.Selection;
                    if (selected)
                    {
                        sprite.Draw(_textPixelTexture, rowBounds, new Color(89, 108, 147, 220));
                    }

                    DrawCandidateWindowText(sprite, numberText, new Vector2(rowBounds.X + 4, rowBounds.Y), selected ? Color.White : new Color(222, 222, 222), selected);
                    DrawCandidateWindowText(
                        sprite,
                        _candidateListState.Candidates[candidateIndex] ?? string.Empty,
                        new Vector2(rowBounds.X + 8 + numberWidth, rowBounds.Y),
                        selected ? Color.White : new Color(240, 235, 200),
                        selected);
                }
            }
            else
            {
                int cellWidth = GetHorizontalCandidateCellWidth();
                int textY = candidateBounds.Y + 3;
                for (int i = 0; i < count; i++)
                {
                    int candidateIndex = start + i;
                    int cellX = candidateBounds.X + 3 + (i * cellWidth);
                    string numberText = FormatCandidateNumber(i + 1);
                    int numberWidth = (int)Math.Ceiling(MeasureCandidateWindowText(numberText).X);
                    Rectangle cellBounds = new(cellX - 1, candidateBounds.Y + 1, cellWidth, Math.Max(1, candidateBounds.Height - 2));
                    bool selected = candidateIndex == _candidateListState.Selection;
                    if (selected)
                    {
                        sprite.Draw(_textPixelTexture, cellBounds, new Color(89, 108, 147, 220));
                    }

                    DrawCandidateWindowText(sprite, numberText, new Vector2(cellX, textY), selected ? Color.White : new Color(222, 222, 222), selected);
                    DrawCandidateWindowText(
                        sprite,
                        _candidateListState.Candidates[candidateIndex] ?? string.Empty,
                        new Vector2(cellX + numberWidth + 3, textY),
                        selected ? Color.White : new Color(240, 235, 200),
                        selected);
                }
            }
        }

        private Rectangle GetImeCandidateWindowBounds(Viewport viewport)
        {
            if (ImeCandidateWindowRendering.ShouldPreferNativeWindow(_candidateListState, clientOwnedCandidateWindow: true))
            {
                return Rectangle.Empty;
            }

            int visibleCount = GetVisibleCandidateCount();
            if (visibleCount <= 0)
            {
                return Rectangle.Empty;
            }

            Rectangle ownerBounds = GetNameFieldBounds();
            if (ownerBounds.IsEmpty)
            {
                return Rectangle.Empty;
            }

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(visibleCount, _candidateListState.Candidates.Count - start);
            int viewportWidth = Math.Max(1, Math.Min(viewport.Width, SkillMacroImeCandidateWindowLayout.ClientViewportWidth));
            int viewportHeight = Math.Max(1, Math.Min(viewport.Height, SkillMacroImeCandidateWindowLayout.ClientViewportHeight));
            int width;
            int height;
            if (_candidateListState.Vertical)
            {
                int widestEntryWidth = 0;
                for (int i = 0; i < count; i++)
                {
                    int candidateIndex = start + i;
                    string numberText = FormatCandidateNumber(i + 1);
                    string candidateText = _candidateListState.Candidates[candidateIndex] ?? string.Empty;
                    int entryWidth = (int)Math.Ceiling(
                        MeasureCandidateWindowText(numberText).X
                        + MeasureCandidateWindowText(candidateText).X)
                        + 2;
                    widestEntryWidth = Math.Max(widestEntryWidth, entryWidth);
                }

                SkillMacroImeCandidateWindowMetrics metrics = SkillMacroImeCandidateWindowLayout.MeasureVerticalClientOwnerExact(
                    _font.LineSpacing,
                    count,
                    widestEntryWidth);
                width = metrics.Width;
                height = metrics.Height;
            }
            else
            {
                SkillMacroImeCandidateWindowMetrics metrics = SkillMacroImeCandidateWindowLayout.MeasureHorizontal(
                    _font.LineSpacing,
                    count);
                width = metrics.Width;
                height = metrics.Height;
            }

            width = Math.Max(1, width);
            height = Math.Max(1, height);
            Point origin = ResolveCandidateWindowOrigin(viewport, width, height);

            return SkillMacroImeCandidateWindowLayout.ResolveClientOwnerBounds(
                viewportWidth,
                viewportHeight,
                width,
                height,
                origin,
                ownerBounds.Y - height - 1);
        }

        private Viewport GetActiveViewport()
        {
            if (_graphicsDevice != null)
            {
                return _graphicsDevice.Viewport;
            }

            return new Viewport(0, 0, SkillMacroImeCandidateWindowLayout.ClientViewportWidth, SkillMacroImeCandidateWindowLayout.ClientViewportHeight);
        }

        private bool IsPointInImeCandidateWindow(int mouseX, int mouseY)
        {
            Rectangle candidateBounds = GetImeCandidateWindowBounds(GetActiveViewport());
            return !candidateBounds.IsEmpty && candidateBounds.Contains(mouseX, mouseY);
        }

        private int ResolveImeCandidateIndexFromPoint(int mouseX, int mouseY)
        {
            if (!_candidateListState.HasCandidates)
            {
                return -1;
            }

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(GetVisibleCandidateCount(), _candidateListState.Candidates.Count - start);
            if (count <= 0)
            {
                return -1;
            }

            Rectangle candidateBounds = GetImeCandidateWindowBounds(GetActiveViewport());
            int localIndex = SkillMacroImeCandidateWindowLayout.HitTestCandidate(
                candidateBounds,
                new Point(mouseX, mouseY),
                _candidateListState.Vertical,
                count,
                GetClientCandidateRowHeight(),
                GetHorizontalCandidateCellWidth());
            return localIndex >= 0
                ? start + localIndex
                : -1;
        }

        private Point ResolveCandidateWindowOrigin(Viewport viewport, int width, int height)
        {
            if (TryResolveCandidateWindowOriginFromWindowForm(viewport, width, height, out Point windowFormOrigin))
            {
                return windowFormOrigin;
            }

            Rectangle bounds = GetNameFieldBounds();
            bool useClauseAnchor = ShouldUseCompositionClauseAnchor();
            string prefix = useClauseAnchor
                ? ResolveCompositionAnchorPrefix()
                : ResolveCandidateCaretPrefix();

            float prefixWidth = prefix.Length > 0 ? _font.MeasureString(prefix).X : 0f;
            int x = bounds.X + NAME_FIELD_TEXT_INSET_X + (int)Math.Round(prefixWidth);
            if (useClauseAnchor)
            {
                x -= _font.LineSpacing + 4;
            }

            return new Point(x, bounds.Y + _font.LineSpacing + 1);
        }

        private bool TryResolveCandidateWindowOriginFromWindowForm(Viewport viewport, int width, int height, out Point origin)
        {
            ImeCandidateWindowForm windowForm = _candidateListState?.WindowForm;
            if (windowForm == null || !windowForm.HasPlacementData)
            {
                origin = Point.Zero;
                return false;
            }

            int viewportWidth = Math.Max(1, Math.Min(viewport.Width, SkillMacroImeCandidateWindowLayout.ClientViewportWidth));
            int viewportHeight = Math.Max(1, Math.Min(viewport.Height, SkillMacroImeCandidateWindowLayout.ClientViewportHeight));
            uint style = windowForm.Style;

            int x = windowForm.CurrentX;
            int y = windowForm.CurrentY;

            if ((style & CandidateWindowStyleExclude) != 0 && windowForm.AreaWidth > 0 && windowForm.AreaHeight > 0)
            {
                x = windowForm.CurrentX;
                y = windowForm.AreaY + windowForm.AreaHeight + 1;
                if (y + height > viewportHeight)
                {
                    y = windowForm.AreaY - height - 1;
                }
            }
            else if ((style & (CandidateWindowStyleForcePosition | CandidateWindowStyleCandidatePosition | CandidateWindowStylePoint)) != 0)
            {
                y = windowForm.CurrentY + 1;
            }
            else if ((style & CandidateWindowStyleRect) != 0 && windowForm.AreaWidth > 0 && windowForm.AreaHeight > 0)
            {
                x = windowForm.AreaX;
                y = windowForm.AreaY + windowForm.AreaHeight + 1;
                if (y + height > viewportHeight)
                {
                    y = windowForm.AreaY - height - 1;
                }
            }
            else
            {
                origin = Point.Zero;
                return false;
            }

            origin = new Point(x, y);
            return true;
        }

        private string ResolveCompositionAnchorPrefix()
        {
            if (string.IsNullOrEmpty(_compositionText))
            {
                return ResolveCommittedInsertionPrefix();
            }

            int anchorIndex = ResolveCompositionAnchorIndex();
            string committedPrefix = ResolveCommittedInsertionPrefix();
            if (anchorIndex <= 0)
            {
                return committedPrefix;
            }

            return committedPrefix + _compositionText[..Math.Min(anchorIndex, _compositionText.Length)];
        }

        private int ResolveCompositionAnchorIndex()
        {
            if (string.IsNullOrEmpty(_compositionText))
            {
                return 0;
            }

            if (_compositionClauseOffsets.Count >= 2)
            {
                int cursor = Math.Clamp(_compositionCursorPosition, 0, _compositionText.Length);
                for (int i = 0; i < _compositionClauseOffsets.Count - 1; i++)
                {
                    int start = Math.Clamp(_compositionClauseOffsets[i], 0, _compositionText.Length);
                    int end = Math.Clamp(_compositionClauseOffsets[i + 1], start, _compositionText.Length);
                    if (cursor >= start && cursor <= end)
                    {
                        return start;
                    }
                }
            }

            return _compositionCursorPosition >= 0
                ? Math.Clamp(_compositionCursorPosition, 0, _compositionText.Length)
                : _compositionText.Length;
        }

        private int GetVisibleCandidateCount()
        {
            if (!_candidateListState.HasCandidates)
            {
                return 0;
            }

            int pageStart = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int pageSize = _candidateListState.PageSize > 0 ? _candidateListState.PageSize : _candidateListState.Candidates.Count;
            return Math.Max(0, Math.Min(pageSize, _candidateListState.Candidates.Count - pageStart));
        }

        private int GetCandidatePageSize()
        {
            if (!_candidateListState.HasCandidates)
            {
                return 0;
            }

            return Math.Max(1, _candidateListState.PageSize > 0 ? _candidateListState.PageSize : GetVisibleCandidateCount());
        }

        private int GetClientCandidateRowHeight()
        {
            return SkillMacroImeCandidateWindowLayout.MeasureVertical(_font.LineSpacing, 1, 0).RowHeight;
        }

        private int GetHorizontalCandidateCellWidth()
        {
            if (_font == null)
            {
                return SkillMacroImeCandidateWindowLayout.MeasureHorizontal(10, 1).CellWidth;
            }

            return SkillMacroImeCandidateWindowLayout.MeasureHorizontal(_font.LineSpacing, 1).CellWidth;
        }

        private int GetHorizontalCandidateWindowWidth()
        {
            int pageSize = GetCandidatePageSize();
            if (pageSize <= 0)
            {
                return 64;
            }

            return SkillMacroImeCandidateWindowLayout.MeasureHorizontal(_font.LineSpacing, pageSize).Width;
        }

        private int GetCandidateNumberWidth()
        {
            int widestIndex = Math.Max(1, GetVisibleCandidateCount());
            return (int)Math.Ceiling(MeasureCandidateWindowText(FormatCandidateNumber(widestIndex)).X);
        }

        private static string FormatCandidateNumber(int candidateNumber)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                CandidateNumberFormatStringPoolId,
                "{0}",
                maxPlaceholderCount: 1,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, candidateNumber);
        }

        private Vector2 MeasureCandidateWindowText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            ClientTextRasterizer rasterizer = EnsureCandidateWindowTextRasterizer(selected: false);
            return rasterizer?.MeasureString(text) ?? _font?.MeasureString(text) ?? Vector2.Zero;
        }

        private void DrawCandidateWindowText(SpriteBatch sprite, string text, Vector2 position, Color color, bool selected)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            ClientTextRasterizer rasterizer = EnsureCandidateWindowTextRasterizer(selected);
            if (rasterizer != null)
            {
                rasterizer.DrawString(sprite, text, position, color);
                return;
            }

            if (_font != null)
            {
                sprite.DrawString(_font, text, position, color);
            }
        }

        private ClientTextRasterizer EnsureCandidateWindowTextRasterizer(bool selected)
        {
            if (_graphicsDevice == null || _font == null)
            {
                return null;
            }

            float basePointSize = Math.Max(1f, _font.LineSpacing);
            if (selected)
            {
                _candidateSelectedTextRasterizer ??= new ClientTextRasterizer(
                    _graphicsDevice,
                    basePointSize: basePointSize,
                    fontStyle: System.Drawing.FontStyle.Bold);
                return _candidateSelectedTextRasterizer;
            }

            _candidateTextRasterizer ??= new ClientTextRasterizer(
                _graphicsDevice,
                basePointSize: basePointSize,
                fontStyle: System.Drawing.FontStyle.Regular);
            return _candidateTextRasterizer;
        }

        private bool ShouldUseCompositionClauseAnchor()
        {
            return !string.IsNullOrEmpty(_compositionText)
                && _compositionClauseOffsets.Count >= 2
                && _compositionCursorPosition >= 0;
        }

        private string ResolveCandidateCaretPrefix()
        {
            string committedPrefix = ResolveCommittedInsertionPrefix();
            if (string.IsNullOrEmpty(_compositionText))
            {
                return committedPrefix;
            }

            string compositionCaretPrefix = ResolveCompositionCaretPrefix();
            return compositionCaretPrefix.Length == 0
                ? committedPrefix
                : committedPrefix + compositionCaretPrefix;
        }

        private string ResolveCommittedInsertionPrefix()
        {
            int insertionIndex = Math.Clamp(_compositionInsertionIndex >= 0 ? _compositionInsertionIndex : _editingCursorPosition, 0, _editingMacroName.Length);
            return insertionIndex <= 0
                ? string.Empty
                : _editingMacroName[..insertionIndex];
        }

        private string ResolveCompositionCaretPrefix()
        {
            if (string.IsNullOrEmpty(_compositionText))
            {
                return string.Empty;
            }

            int caretIndex = _compositionCursorPosition >= 0
                ? Math.Clamp(_compositionCursorPosition, 0, _compositionText.Length)
                : _compositionText.Length;
            return caretIndex <= 0
                ? string.Empty
                : _compositionText[..caretIndex];
        }

        private static IReadOnlyList<int> ClampClauseOffsets(IReadOnlyList<int> offsets, int maxLength)
        {
            if (offsets == null || offsets.Count == 0)
            {
                return Array.Empty<int>();
            }

            List<int> clamped = new(offsets.Count);
            foreach (int offset in offsets)
            {
                int safeOffset = Math.Clamp(offset, 0, maxLength);
                if (clamped.Count == 0 || safeOffset >= clamped[^1])
                {
                    clamped.Add(safeOffset);
                }
            }

            if (clamped.Count == 0)
            {
                return Array.Empty<int>();
            }

            if (clamped[^1] != maxLength)
            {
                clamped.Add(maxLength);
            }

            return clamped;
        }

        private void UpdateImePresentationPlacement()
        {
            if (!CapturesKeyboardInput
                || _font == null
                || ResolveImeWindowHandle == null)
            {
                return;
            }

            IntPtr windowHandle = ResolveImeWindowHandle();
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            Rectangle nameFieldBounds = GetNameFieldBounds();
            int compositionCaretWidth = MeasureImePlacementWidth(ResolveCandidateCaretPrefix());
            bool useClauseAnchor = ShouldUseCompositionClauseAnchor();
            int clauseAnchorWidth = useClauseAnchor
                ? MeasureImePlacementWidth(ResolveCompositionAnchorPrefix())
                : compositionCaretWidth;
            int clauseWidth = useClauseAnchor
                ? MeasureImePlacementWidth(ResolveActiveCompositionClauseText())
                : 1;

            SkillMacroImeWindowPlacement placement = SkillMacroImeWindowPlacementLayout.Resolve(
                nameFieldBounds,
                NAME_FIELD_TEXT_INSET_X,
                _font.LineSpacing,
                compositionCaretWidth,
                useClauseAnchor,
                clauseAnchorWidth,
                clauseWidth);
            placement = SkillMacroImeWindowPlacementLayout.PreserveNativeCandidateWindowPlacement(placement, _candidateListState);
            if (WindowsImePresentationBridge.TryUpdatePlacement(windowHandle, placement, _candidateListState, out ImeCandidateListState refreshedCandidateState))
            {
                _candidateListState = refreshedCandidateState;
            }
        }

        public override void RefreshImePresentationPlacement()
        {
            UpdateImePresentationPlacement();
        }

        protected override void ResetImePresentationPlacement()
        {
            if (ResolveImeWindowHandle == null)
            {
                return;
            }

            IntPtr windowHandle = ResolveImeWindowHandle();
            if (windowHandle != IntPtr.Zero)
            {
                WindowsImePresentationBridge.TryResetPlacement(windowHandle);
            }
        }

        private int MeasureImePlacementWidth(string text)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return (int)Math.Round(_font.MeasureString(text).X);
        }

        private string ResolveActiveCompositionClauseText()
        {
            if (string.IsNullOrEmpty(_compositionText))
            {
                return string.Empty;
            }

            if (_compositionClauseOffsets.Count >= 2)
            {
                int cursor = Math.Clamp(_compositionCursorPosition, 0, _compositionText.Length);
                for (int i = 0; i < _compositionClauseOffsets.Count - 1; i++)
                {
                    int start = Math.Clamp(_compositionClauseOffsets[i], 0, _compositionText.Length);
                    int end = Math.Clamp(_compositionClauseOffsets[i + 1], start, _compositionText.Length);
                    if (cursor >= start && cursor <= end)
                    {
                        return _compositionText[start..end];
                    }
                }
            }

            return _compositionText;
        }

        private static IEnumerable<int> EnumerateCaretStops(string text)
        {
            string value = text ?? string.Empty;
            yield return 0;
            if (value.Length == 0)
            {
                yield break;
            }

            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext())
            {
                yield return enumerator.ElementIndex + enumerator.GetTextElement().Length;
            }
        }
        #endregion
    }

    /// <summary>
    /// Represents a single skill macro containing up to 3 skills
    /// </summary>
    public class SkillMacro
    {
        /// <summary>
        /// User-defined name for this macro
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Array of skill IDs (up to 3 skills)
        /// </summary>
        public int[] SkillIds { get; set; }

        /// <summary>
        /// Whether to notify party members when using this macro
        /// </summary>
        public bool NotifyParty { get; set; }

        /// <summary>
        /// Whether this macro is enabled
        /// </summary>
        public bool IsEnabled => SkillCount > 0;

        /// <summary>
        /// Get the skill ID at a specific position (0-2)
        /// </summary>
        public int GetSkillAt(int index)
        {
            if (SkillIds == null || index < 0 || index >= SkillIds.Length)
                return 0;
            return SkillIds[index];
        }

        /// <summary>
        /// Count of non-zero skills in this macro
        /// </summary>
        public int SkillCount
        {
            get
            {
                if (SkillIds == null)
                    return 0;
                int count = 0;
                foreach (int id in SkillIds)
                {
                    if (id != 0)
                        count++;
                }
                return count;
            }
        }
    }
}
