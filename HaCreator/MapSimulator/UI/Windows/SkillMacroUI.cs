using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.UI.Controls;
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
    public class SkillMacroUI : UIWindowBase
    {
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

        // Macro slot layout constants (from SkillMacro/macroslot2: 168x42)
        private const int MACRO_SLOT_WIDTH = 168;
        private const int MACRO_SLOT_HEIGHT = 42;
        private const int MACRO_SLOT_PADDING = 4;

        // Content area offsets (from window edges)
        private const int CONTENT_OFFSET_X = 20;
        private const int CONTENT_OFFSET_Y = 50;

        // Skill slot positions within each macro row
        // Three 32x32 icons with small padding between them
        private const int SKILL_SLOT_START_X = 4;
        private const int SKILL_SLOT_Y_OFFSET = 5;
        private const int SKILL_SLOT_SPACING = 36;

        // Macro name text field position (from macroname: 164x57)
        private const int NAME_FIELD_WIDTH = 164;
        private const int NAME_FIELD_HEIGHT = 20;
        private const int NAME_FIELD_X = CONTENT_OFFSET_X;
        private const int NAME_FIELD_Y = CONTENT_OFFSET_Y - 30;
        private const int NAME_FIELD_TEXT_INSET_X = 4;
        private const int NAME_FIELD_TEXT_INSET_Y = 3;
        // Button positions (relative to window)
        // BtOK button origin from UIWindow2: (-145, -255) means 145 from left, 255 from top
        private const int BUTTON_OK_X = 50;
        private const int BUTTON_OK_Y = 255;
        private const int BUTTON_CANCEL_X = 110;
        private const int BUTTON_CANCEL_Y = 255;
        private const int BUTTON_DELETE_X = 160;
        private const int BUTTON_DELETE_Y = 255;

        // Checkbox position for "Notify party members" option
        private const int CHECKBOX_X = 15;
        private const int CHECKBOX_Y = 235;
        #endregion

        #region Fields
        private SkillManager _skillManager;
        private SkillLoader _skillLoader;
        private SpriteFont _font;

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

        // Checkbox state
        private bool _notifyPartyMembers = false;
        private string _validationMessage = string.Empty;
        private int _editingCursorPosition;
        private int _caretBlinkTick;
        private string _compositionText = string.Empty;
        private int _compositionInsertionIndex = -1;
        private SkillMacroSoftKeyboardSkin _softKeyboardSkin;
        private bool _softKeyboardVisible;
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

        // Buttons
        private UIObject _btnOK;
        private UIObject _btnCancel;
        private UIObject _btnDelete;

        // Skill icon cache
        private readonly Dictionary<int, IDXObject> _skillIconCache = new();
        #endregion

        #region Properties
        public override string WindowName => "SkillMacro";
        public override bool CapturesKeyboardInput => IsVisible && _editingMacroIndex >= 0;
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
        #endregion

        #region Constructor
        public SkillMacroUI(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
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
        public void SetMacroSlotIcons(Texture2D[] icons)
        {
            if (icons != null)
            {
                _macroSlotIcons = icons;
            }
        }

        internal void SetSoftKeyboardSkin(SkillMacroSoftKeyboardSkin skin)
        {
            _softKeyboardSkin = skin;
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

            DrawSoftKeyboard(sprite);
        }

        private void DrawMacroSlot(SpriteBatch sprite, int macroIndex, int windowX, int windowY)
        {
            var macro = _macros[macroIndex];

            // Calculate slot position
            int slotX = windowX + CONTENT_OFFSET_X;
            int slotY = windowY + CONTENT_OFFSET_Y + macroIndex * (MACRO_SLOT_HEIGHT + MACRO_SLOT_PADDING);

            // Draw slot background
            bool isSelected = (macroIndex == _selectedMacroIndex);
            bool isHovered = (macroIndex == _hoveredMacroIndex);

            Texture2D bgTexture = isSelected ? _selectedSlotTexture : _macroSlotTexture;
            sprite.Draw(bgTexture, new Rectangle(slotX, slotY, MACRO_SLOT_WIDTH, MACRO_SLOT_HEIGHT), Color.White);

            // Draw hover highlight
            if (isHovered && !isSelected)
            {
                sprite.Draw(_slotHighlightTexture,
                    new Rectangle(slotX, slotY, MACRO_SLOT_WIDTH, MACRO_SLOT_HEIGHT),
                    Color.White * 0.3f);
            }

            // Draw skill slots (3 skills per macro)
            for (int skillSlot = 0; skillSlot < SKILLS_PER_MACRO; skillSlot++)
            {
                int skillX = slotX + SKILL_SLOT_START_X + skillSlot * SKILL_SLOT_SPACING;
                int skillY = slotY + SKILL_SLOT_Y_OFFSET;

                // Draw empty slot background
                sprite.Draw(_emptySlotTexture,
                    new Rectangle(skillX, skillY, SKILL_ICON_SIZE, SKILL_ICON_SIZE),
                    Color.White);

                // Draw skill icon if assigned
                int skillId = macro.SkillIds[skillSlot];
                if (skillId > 0)
                {
                    var icon = GetSkillIcon(skillId);
                    if (icon != null)
                    {
                        icon.DrawBackground(sprite, null, null, skillX, skillY, Color.White, false, null);
                    }
                }

                // Draw highlight on hovered skill slot
                if (macroIndex == _hoveredMacroIndex && skillSlot == _hoveredSkillSlot)
                {
                    sprite.Draw(_slotHighlightTexture,
                        new Rectangle(skillX, skillY, SKILL_ICON_SIZE, SKILL_ICON_SIZE),
                        Color.White);
                }
            }

            // Draw macro name
            if (_font != null && !string.IsNullOrEmpty(macro.Name))
            {
                int nameX = slotX + SKILL_SLOT_START_X + SKILLS_PER_MACRO * SKILL_SLOT_SPACING + 8;
                int nameY = slotY + (MACRO_SLOT_HEIGHT - 12) / 2;

                // Truncate name if too long
                string displayName = macro.Name;
                if (displayName.Length > 10)
                    displayName = displayName.Substring(0, 10) + "...";

                sprite.DrawString(_font, displayName, new Vector2(nameX, nameY), Color.White);
            }

            // Draw macro number indicator
            if (_font != null)
            {
                string numberText = $"M{macroIndex + 1}";
                sprite.DrawString(_font, numberText,
                    new Vector2(slotX - 15, slotY + (MACRO_SLOT_HEIGHT - 12) / 2),
                    isSelected ? Color.Yellow : Color.LightGray);
            }
        }

        private void DrawNameField(SpriteBatch sprite, int windowX, int windowY)
        {
            if (_font == null)
                return;

            int fieldX = windowX + NAME_FIELD_X;
            int fieldY = windowY + NAME_FIELD_Y;

            // Draw label
            sprite.DrawString(_font, "Macro Name:", new Vector2(fieldX, fieldY - 16), Color.LightGray);

            // Draw text field background (simple rectangle)
            var fieldRect = new Rectangle(fieldX, fieldY, NAME_FIELD_WIDTH, NAME_FIELD_HEIGHT);
            sprite.Draw(_emptySlotTexture, fieldRect, Color.White);

            int safeCursorPosition = Math.Clamp(_editingCursorPosition, 0, _editingMacroName?.Length ?? 0);
            string committedPrefix = safeCursorPosition > 0
                ? _editingMacroName[..safeCursorPosition]
                : string.Empty;
            string committedSuffix = safeCursorPosition < (_editingMacroName?.Length ?? 0)
                ? _editingMacroName[safeCursorPosition..]
                : string.Empty;
            string compositionText = _compositionText ?? string.Empty;
            Vector2 textPosition = new(fieldX + NAME_FIELD_TEXT_INSET_X, fieldY + NAME_FIELD_TEXT_INSET_Y);

            if (committedPrefix.Length > 0)
            {
                sprite.DrawString(_font, committedPrefix, textPosition, Color.White);
            }

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

            if (committedSuffix.Length > 0)
            {
                float compositionWidth = compositionText.Length > 0 ? _font.MeasureString(compositionText).X : 0f;
                sprite.DrawString(_font, committedSuffix,
                    new Vector2(compositionPosition.X + compositionWidth, compositionPosition.Y),
                    Color.White);
            }

            if (((Environment.TickCount - _caretBlinkTick) / 500) % 2 == 0)
            {
                float caretOffset = prefixWidth;
                if (compositionText.Length > 0)
                {
                    caretOffset += _font.MeasureString(compositionText).X;
                }

                int caretX = fieldX + NAME_FIELD_TEXT_INSET_X + (int)Math.Round(caretOffset);
                int caretY = fieldY + 2;
                sprite.Draw(_textPixelTexture,
                    new Rectangle(caretX, caretY, 1, Math.Max(12, _font.LineSpacing - 2)),
                    Color.White);
            }

            string displayText = BuildDisplayedNameText();
            string byteCountText = $"{SkillMacroNameRules.GetByteCount(displayText)}/{SkillMacroNameRules.MaxNameBytes} bytes";
            sprite.DrawString(_font, byteCountText,
                new Vector2(fieldX + 92, fieldY - 16),
                Color.LightGray);

            if (!string.IsNullOrWhiteSpace(_validationMessage))
            {
                sprite.DrawString(_font, _validationMessage,
                    new Vector2(fieldX, fieldY + NAME_FIELD_HEIGHT + 2),
                    Color.IndianRed);
            }
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
            bool enabled = key != SkillMacroSoftKeyboardFunctionKey.Enter || CanSaveCurrentMacro();
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
            if (_font == null)
                return;

            int checkX = windowX + CHECKBOX_X;
            int checkY = windowY + CHECKBOX_Y;

            // Draw checkbox (simple 12x12 box)
            Rectangle checkRect = new Rectangle(checkX, checkY, 12, 12);
            sprite.Draw(_emptySlotTexture, checkRect, Color.White);

            // Draw check mark if enabled
            if (_notifyPartyMembers)
            {
                // Simple filled inner square to indicate checked
                Rectangle innerRect = new Rectangle(checkX + 2, checkY + 2, 8, 8);
                sprite.Draw(_slotHighlightTexture, innerRect, Color.LimeGreen);
            }

            // Draw label
            sprite.DrawString(_font, "Notify party members",
                new Vector2(checkX + 18, checkY - 1),
                Color.LightGray);
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
            _editingCursorPosition = _editingMacroName.Length;
            _notifyPartyMembers = _macros[index].NotifyParty;
            _validationMessage = string.Empty;
            _caretBlinkTick = Environment.TickCount;
            ClearCompositionText();

            for (int i = 0; i < SKILLS_PER_MACRO; i++)
            {
                _editingSkillIds[i] = _macros[index].SkillIds[i];
            }

            ShowSoftKeyboard();
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
            if (_softKeyboardSkin == null && resetDismissedState)
            {
                return;
            }

            _softKeyboardVisible = true;
            if (resetDismissedState)
            {
                _softKeyboardMinimized = false;
            }
        }

        private void HideSoftKeyboard()
        {
            _softKeyboardVisible = false;
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

        private bool IsSoftKeyboardVisible => _softKeyboardVisible && _editingMacroIndex >= 0;

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

        private bool IsSoftKeyboardKeyEnabled(int keyIndex)
        {
            string insertedText = SkillMacroSoftKeyboardLayout.GetKeyText(keyIndex, IsSoftKeyboardUppercase());
            return CanInsertSoftKeyboardText(insertedText);
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

            // Clear the macro
            _macros[_selectedMacroIndex] = CreateDefaultMacro(_selectedMacroIndex);

            OnMacroDeleted?.Invoke(_selectedMacroIndex);

            // Reset editing state
            _editingMacroIndex = -1;
            _editingMacroName = "";
            _notifyPartyMembers = false;
            _validationMessage = string.Empty;
            _editingCursorPosition = 0;
            _caretBlinkTick = Environment.TickCount;
            ClearCompositionText();
            Array.Clear(_editingSkillIds, 0, SKILLS_PER_MACRO);
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
            int relX = mouseX - Position.X - CONTENT_OFFSET_X;
            int relY = mouseY - Position.Y - CONTENT_OFFSET_Y;

            if (relX < 0 || relX > MACRO_SLOT_WIDTH)
                return -1;
            if (relY < 0)
                return -1;

            int slotHeight = MACRO_SLOT_HEIGHT + MACRO_SLOT_PADDING;
            int index = relY / slotHeight;

            // Check if actually within the slot (not in padding)
            int slotY = index * slotHeight;
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

            int slotX = Position.X + CONTENT_OFFSET_X;
            int slotY = Position.Y + CONTENT_OFFSET_Y + macroIndex * (MACRO_SLOT_HEIGHT + MACRO_SLOT_PADDING);

            int relX = mouseX - slotX - SKILL_SLOT_START_X;
            int relY = mouseY - slotY - SKILL_SLOT_Y_OFFSET;

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

            int macroIndex = GetMacroIndexAtPosition(mouseX, mouseY);

            if (macroIndex < 0)
            {
                if (leftButton && IsPointInNameField(mouseX, mouseY))
                {
                    ClearCompositionText();
                    _editingCursorPosition = ResolveNameCursorFromMouse(mouseX);
                    _caretBlinkTick = Environment.TickCount;
                    ShowSoftKeyboard(resetDismissedState: true);
                }

                // Check checkbox
                if (IsPointInCheckbox(mouseX, mouseY) && leftButton)
                {
                    _notifyPartyMembers = !_notifyPartyMembers;
                }
                return;
            }

            int skillSlot = GetSkillSlotAtPosition(mouseX, mouseY, macroIndex);

            if (rightButton && skillSlot >= 0)
            {
                // Right-click to clear skill slot
                ClearMacroSkill(macroIndex, skillSlot);
            }
            else if (leftButton)
            {
                if (skillSlot >= 0)
                {
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
                    _dragMode = MacroDragMode.MacroBinding;
                    _dragMacroIndex = macroIndex;
                    _dragPosition = new Vector2(mouseX, mouseY);
                }
                else
                {
                    SelectedMacroIndex = macroIndex;
                }
            }
        }

        /// <summary>
        /// Handle mouse up
        /// </summary>
        public void OnMouseUp(int mouseX, int mouseY)
        {
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
            int checkX = Position.X + CHECKBOX_X;
            int checkY = Position.Y + CHECKBOX_Y;

            return mouseX >= checkX && mouseX <= checkX + 12 &&
                   mouseY >= checkY && mouseY <= checkY + 12;
        }

        private Rectangle GetNameFieldBounds()
        {
            return new Rectangle(Position.X + NAME_FIELD_X, Position.Y + NAME_FIELD_Y, NAME_FIELD_WIDTH, NAME_FIELD_HEIGHT);
        }

        private bool IsPointInNameField(int mouseX, int mouseY)
        {
            return _editingMacroIndex >= 0 && GetNameFieldBounds().Contains(mouseX, mouseY);
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
                        if (SaveCurrentMacro())
                        {
                            HideSoftKeyboard();
                            Hide();
                            OnMacroWindowClosed?.Invoke();
                        }
                        break;
                    case SkillMacroSoftKeyboardFunctionKey.Backspace:
                        RemoveCharacterBeforeCursor();
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

        public bool HandlesMacroInteractionPoint(int mouseX, int mouseY)
        {
            return GetMacroIndexAtPosition(mouseX, mouseY) >= 0
                || IsPointInCheckbox(mouseX, mouseY)
                || IsPointInNameField(mouseX, mouseY)
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
        }
        #endregion

        #region Button Handlers
        private void OnOKClicked(UIObject sender)
        {
            if (SaveCurrentMacro())
            {
                Hide();
                OnMacroWindowClosed?.Invoke();
            }
        }

        private void OnCancelClicked(UIObject sender)
        {
            // Discard changes
            _editingMacroIndex = -1;
            _editingMacroName = "";
            _notifyPartyMembers = false;
            _validationMessage = string.Empty;
            _editingCursorPosition = 0;
            _caretBlinkTick = Environment.TickCount;
            ClearCompositionText();
            Array.Clear(_editingSkillIds, 0, SKILLS_PER_MACRO);
            HideSoftKeyboard();

            Hide();
            OnMacroWindowClosed?.Invoke();
        }

        private void OnDeleteClicked(UIObject sender)
        {
            DeleteSelectedMacro();
        }
        #endregion

        #region Update
        private KeyboardState _previousKeyboardState;

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_softKeyboardPressedVisualUntil > 0 && Environment.TickCount >= _softKeyboardPressedVisualUntil)
            {
                _pressedSoftKeyboardKeyIndex = -1;
                _pressedSoftKeyboardFunctionKey = SkillMacroSoftKeyboardFunctionKey.None;
                _pressedSoftKeyboardWindowButton = SkillMacroSoftKeyboardWindowButton.None;
                _softKeyboardPressedVisualUntil = 0;
            }

            if (!CapturesKeyboardInput)
            {
                _previousKeyboardState = Keyboard.GetState();
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            HandleKeyboardInput(keyboardState);
            _previousKeyboardState = keyboardState;
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

        public override void HandleCompositionText(string text)
        {
            if (!CapturesKeyboardInput)
            {
                ClearCompositionText();
                return;
            }

            string sanitized = SanitizeCompositionText(text);
            if (sanitized.Length == 0)
            {
                ClearCompositionText();
                return;
            }

            if (_compositionText.Length == 0)
            {
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
            _validationMessage = string.Empty;
            _caretBlinkTick = Environment.TickCount;
        }

        public override void ClearCompositionText()
        {
            _compositionText = string.Empty;
            _compositionInsertionIndex = -1;
        }

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            bool ctrl = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);

            if (keyboardState.IsKeyDown(Keys.Back) && _previousKeyboardState.IsKeyUp(Keys.Back) && _editingCursorPosition > 0)
            {
                RemoveCharacterBeforeCursor();
            }

            if (keyboardState.IsKeyDown(Keys.Delete) && _previousKeyboardState.IsKeyUp(Keys.Delete) && _editingCursorPosition < _editingMacroName.Length)
            {
                RemoveCharacterAtCursor();
            }

            if (keyboardState.IsKeyDown(Keys.Left) && _previousKeyboardState.IsKeyUp(Keys.Left))
            {
                ClearCompositionText();
                _editingCursorPosition = SkillMacroNameRules.GetPreviousCaretStop(_editingMacroName, _editingCursorPosition);
                _caretBlinkTick = Environment.TickCount;
            }

            if (keyboardState.IsKeyDown(Keys.Right) && _previousKeyboardState.IsKeyUp(Keys.Right))
            {
                ClearCompositionText();
                _editingCursorPosition = SkillMacroNameRules.GetNextCaretStop(_editingMacroName, _editingCursorPosition);
                _caretBlinkTick = Environment.TickCount;
            }

            if (keyboardState.IsKeyDown(Keys.Home) && _previousKeyboardState.IsKeyUp(Keys.Home))
            {
                ClearCompositionText();
                _editingCursorPosition = 0;
                _caretBlinkTick = Environment.TickCount;
            }

            if (keyboardState.IsKeyDown(Keys.End) && _previousKeyboardState.IsKeyUp(Keys.End))
            {
                ClearCompositionText();
                _editingCursorPosition = _editingMacroName.Length;
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

                string normalizedClipboardText = clipboardText.Replace("\r", string.Empty).Replace("\n", string.Empty);

                ClearCompositionText();
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
