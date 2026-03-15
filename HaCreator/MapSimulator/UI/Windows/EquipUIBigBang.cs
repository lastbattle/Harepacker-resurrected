using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using CharacterBuild = HaCreator.MapSimulator.Character.CharacterBuild;
using CharacterEquipSlot = HaCreator.MapSimulator.Character.EquipSlot;
using CharacterPart = HaCreator.MapSimulator.Character.CharacterPart;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Equipment UI window for post-Big Bang MapleStory (v100+)
    /// Structure: UI.wz/UIWindow2.img/Equip/character
    /// Window dimensions: 184x290 pixels
    /// </summary>
    public class EquipUIBigBang : UIWindowBase
    {
        #region Constants
        private const int SLOT_SIZE = 32;

        // Window dimensions (from UIWindow2.img/Equip/character/backgrnd)
        private const int WINDOW_WIDTH = 184;
        private const int WINDOW_HEIGHT = 290;
        private const int TOOLTIP_FALLBACK_WIDTH = 300;
        private const int TOOLTIP_PADDING = 10;
        private const int TOOLTIP_ICON_GAP = 8;
        private const int TOOLTIP_TITLE_GAP = 8;
        private const int TOOLTIP_SECTION_GAP = 4;
        private const int TOOLTIP_OFFSET_X = 12;
        private const int TOOLTIP_OFFSET_Y = -4;

        // Tab indices
        private const int TAB_CHARACTER = 0;
        private const int TAB_PET = 1;
        private const int TAB_DRAGON = 2;
        private const int TAB_MECHANIC = 3;
        private const int TAB_ANDROID = 4;
        #endregion

        #region Equipment Slot Types
        public enum EquipSlot
        {
            Ring1 = 0,
            Ring2 = 1,
            Ring3 = 2,
            Ring4 = 3,
            Pocket = 4,
            Pendant1 = 5,
            Pendant2 = 6,
            Weapon = 7,
            Belt = 8,
            Cap = 9,
            FaceAccessory = 10,
            EyeAccessory = 11,
            Top = 12,
            Bottom = 13,
            Shoes = 14,
            Earring = 15,
            Shoulder = 16,
            Glove = 17,
            Shield = 18,
            Cape = 19,
            Heart = 20,
            Badge = 21,
            Medal = 22,
            Android = 23,
            AndroidHeart = 24,
            Totem1 = 25,
            Totem2 = 26,
            Totem3 = 27
        }
        #endregion

        #region Fields
        private int _currentTab = TAB_CHARACTER;

        // Foreground texture (backgrnd2 - grid overlay)
        private IDXObject _foreground;
        private Point _foregroundOffset;

        // Slot labels and character silhouette (backgrnd3)
        private IDXObject _slotLabels;
        private Point _slotLabelsOffset;

        // Tab buttons
        private UIObject _btnPet;
        private UIObject _btnDragon;
        private UIObject _btnMechanic;
        private UIObject _btnAndroid;
        private UIObject _btnSlot;

        // Equipment slot positions (relative to window)
        private readonly Dictionary<EquipSlot, Point> slotPositions;

        // Equipped items data
        private readonly Dictionary<EquipSlot, EquipSlotData> equippedItems;

        // Graphics device
        private GraphicsDevice _device;
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private Texture2D _debugPlaceholder;
        private SpriteFont _font;
        private CharacterBuild _characterBuild;
        private EquipSlot? _hoveredSlot;
        private Point _lastMousePosition;
        #endregion

        #region Properties
        public override string WindowName => "Equipment";
        public override CharacterBuild CharacterBuild
        {
            get => _characterBuild;
            set => _characterBuild = value;
        }

        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                if (value >= TAB_CHARACTER && value <= TAB_ANDROID)
                {
                    _currentTab = value;
                }
            }
        }
        #endregion

        #region Constructor
        public EquipUIBigBang(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            _device = device;

            // Initialize slot positions for Big Bang layout
            // Grid formula from IDA: X = 33*col + 10, Y = 33*row + 27 (for nType=0)
            // Col 0=10, Col 1=43, Col 2=76, Col 3=109, Col 4=142
            // Row 0=27, Row 1=60, Row 2=93, Row 3=126, Row 4=159, Row 5=192, Row 6=225
            slotPositions = new Dictionary<EquipSlot, Point>
            {
                // Row 0
                { EquipSlot.Badge, new Point(10, 27) },      // Col 0, Row 0
                { EquipSlot.Cap, new Point(43, 27) },        // Col 1, Row 0
                { EquipSlot.Android, new Point(109, 27) },   // Col 3, Row 0
                { EquipSlot.Heart, new Point(142, 27) },     // Col 4, Row 0

                // Row 1
                { EquipSlot.Medal, new Point(10, 60) },      // Col 0, Row 1
                { EquipSlot.FaceAccessory, new Point(43, 60) }, // Col 1, Row 1 (Forehead)
                { EquipSlot.Ring1, new Point(109, 60) },     // Col 3, Row 1
                { EquipSlot.Ring2, new Point(142, 60) },     // Col 4, Row 1

                // Row 2
                { EquipSlot.EyeAccessory, new Point(43, 93) }, // Col 1, Row 2
                { EquipSlot.Shoulder, new Point(142, 93) },  // Col 4, Row 2

                // Row 3
                { EquipSlot.Cape, new Point(10, 126) },      // Col 0, Row 3 (Mantle/Cape)
                { EquipSlot.Top, new Point(43, 126) },       // Col 1, Row 3 (Clothes)
                { EquipSlot.Pendant1, new Point(76, 126) },  // Col 2, Row 3
                { EquipSlot.Weapon, new Point(142, 126) },   // Col 4, Row 3

                // Row 4
                { EquipSlot.Glove, new Point(10, 159) },     // Col 0, Row 4
                { EquipSlot.Belt, new Point(76, 159) },      // Col 2, Row 4
                { EquipSlot.Ring3, new Point(142, 159) },    // Col 4, Row 4

                // Row 5
                { EquipSlot.Bottom, new Point(43, 192) },    // Col 1, Row 5 (Pants)
                { EquipSlot.Shoes, new Point(76, 192) },     // Col 2, Row 5
                { EquipSlot.Pocket, new Point(142, 192) },   // Col 4, Row 5 (Pet slot)

                // Row 6
                { EquipSlot.Totem1, new Point(10, 225) },    // Col 0, Row 6 (Taming Mob)
                { EquipSlot.Totem2, new Point(43, 225) },    // Col 1, Row 6 (Saddle)
                { EquipSlot.Totem3, new Point(76, 225) },    // Col 2, Row 6 (Mob Equip)

                // Additional slots (off main grid or secondary positions)
                { EquipSlot.Pendant2, new Point(109, 126) }, // Col 3, Row 3 (secondary pendant)
                { EquipSlot.Ring4, new Point(109, 159) },    // Col 3, Row 4
                { EquipSlot.Shield, new Point(109, 93) },    // Col 3, Row 2 (Sub-weapon)
                { EquipSlot.Earring, new Point(10, 93) },    // Col 0, Row 2
            };

            equippedItems = new Dictionary<EquipSlot, EquipSlotData>();
            _debugPlaceholder = new Texture2D(device, 1, 1);
            _debugPlaceholder.SetData(new[] { Color.White });
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Set the foreground texture (backgrnd2 - grid overlay)
        /// </summary>
        public void SetForeground(IDXObject foreground, int offsetX, int offsetY)
        {
            _foreground = foreground;
            _foregroundOffset = new Point(offsetX, offsetY);
        }

        /// <summary>
        /// Set the slot labels and character silhouette (backgrnd3)
        /// </summary>
        public void SetSlotLabels(IDXObject slotLabels, int offsetX, int offsetY)
        {
            _slotLabels = slotLabels;
            _slotLabelsOffset = new Point(offsetX, offsetY);
        }

        /// <summary>
        /// Initialize equipment tab buttons
        /// </summary>
        public void InitializeTabButtons(UIObject btnPet, UIObject btnDragon, UIObject btnMechanic, UIObject btnAndroid, UIObject btnSlot)
        {
            _btnPet = btnPet;
            _btnDragon = btnDragon;
            _btnMechanic = btnMechanic;
            _btnAndroid = btnAndroid;
            _btnSlot = btnSlot;

            if (btnPet != null)
            {
                AddButton(btnPet);
                btnPet.ButtonClickReleased += (sender) => CurrentTab = TAB_PET;
            }
            if (btnDragon != null)
            {
                AddButton(btnDragon);
                btnDragon.ButtonClickReleased += (sender) => CurrentTab = TAB_DRAGON;
            }
            if (btnMechanic != null)
            {
                AddButton(btnMechanic);
                btnMechanic.ButtonClickReleased += (sender) => CurrentTab = TAB_MECHANIC;
            }
            if (btnAndroid != null)
            {
                AddButton(btnAndroid);
                btnAndroid.ButtonClickReleased += (sender) => CurrentTab = TAB_ANDROID;
            }
            if (btnSlot != null)
            {
                AddButton(btnSlot);
            }
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetTooltipTextures(Texture2D[] tooltipFrames)
        {
            if (tooltipFrames == null)
                return;

            for (int i = 0; i < Math.Min(_tooltipFrames.Length, tooltipFrames.Length); i++)
            {
                _tooltipFrames[i] = tooltipFrames[i];
            }
        }
        #endregion

        #region Drawing
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            int windowX = this.Position.X;
            int windowY = this.Position.Y;

            // Draw foreground (backgrnd2 - grid overlay) z=0
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _foregroundOffset.X, windowY + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            // Draw slot labels and character silhouette (backgrnd3) z=1
            if (_slotLabels != null)
            {
                _slotLabels.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _slotLabelsOffset.X, windowY + _slotLabelsOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            foreach ((EquipSlot uiSlot, Point slotPosition) in slotPositions)
            {
                if (!TryGetEquippedPart(uiSlot, out CharacterPart part) && !equippedItems.TryGetValue(uiSlot, out _))
                    continue;

                int slotX = windowX + slotPosition.X;
                int slotY = windowY + slotPosition.Y;
                DrawEquippedItemIcon(sprite, part, equippedItems.TryGetValue(uiSlot, out EquipSlotData slotData) ? slotData : null, slotX, slotY);
            }
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            DrawHoveredEquipmentTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
        }
        #endregion

        #region Equipment Management
        /// <summary>
        /// Equip an item to a slot
        /// </summary>
        public void EquipItem(EquipSlot slot, int itemId, Texture2D texture, string itemName = "")
        {
            equippedItems[slot] = new EquipSlotData
            {
                ItemId = itemId,
                ItemTexture = texture,
                ItemName = itemName
            };
        }

        /// <summary>
        /// Unequip an item from a slot
        /// </summary>
        public void UnequipItem(EquipSlot slot)
        {
            if (equippedItems.ContainsKey(slot))
            {
                equippedItems.Remove(slot);
            }
        }

        /// <summary>
        /// Get equipped item data
        /// </summary>
        public EquipSlotData GetEquippedItem(EquipSlot slot)
        {
            return equippedItems.TryGetValue(slot, out var data) ? data : null;
        }

        /// <summary>
        /// Clear all equipped items
        /// </summary>
        public void ClearAllEquipment()
        {
            equippedItems.Clear();
        }
        #endregion

        #region Update
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            MouseState mouseState = Mouse.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            _hoveredSlot = GetSlotAtPosition(mouseState.X, mouseState.Y);
        }
        #endregion

        private void DrawEquippedItemIcon(SpriteBatch sprite, CharacterPart part, EquipSlotData slotData, int slotX, int slotY)
        {
            IDXObject icon = part?.IconRaw ?? part?.Icon ?? slotData?.ItemIcon;
            if (icon != null)
            {
                icon.DrawBackground(sprite, null, null, slotX, slotY, Color.White, false, null);
                return;
            }

            if (slotData?.ItemTexture != null)
            {
                sprite.Draw(slotData.ItemTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), Color.White);
            }
        }

        private void DrawHoveredEquipmentTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (_font == null || _hoveredSlot == null || _currentTab != TAB_CHARACTER)
                return;

            EquipSlot hoveredSlot = _hoveredSlot.Value;
            if (!TryGetEquippedPart(hoveredSlot, out CharacterPart part))
            {
                if (!equippedItems.TryGetValue(hoveredSlot, out EquipSlotData fallbackData))
                    return;

                DrawItemTooltip(sprite,
                    fallbackData.ItemName,
                    $"Item ID: {fallbackData.ItemId}",
                    $"Slot: {hoveredSlot}",
                    fallbackData.ItemTexture,
                    fallbackData.ItemIcon,
                    renderWidth,
                    renderHeight);
                return;
            }

            string itemName = string.IsNullOrWhiteSpace(part.Name) ? $"Equip {part.ItemId}" : part.Name;
            string line1 = $"Item ID: {part.ItemId}";
            string line2 = $"Slot: {ResolveSlotLabel(hoveredSlot)}";
            if (part.IsCash)
            {
                line2 += "  Cash";
            }

            DrawItemTooltip(sprite,
                itemName,
                line1,
                line2,
                null,
                part.IconRaw ?? part.Icon,
                renderWidth,
                renderHeight);
        }

        private void DrawItemTooltip(
            SpriteBatch sprite,
            string title,
            string line1,
            string line2,
            Texture2D itemTexture,
            IDXObject itemIcon,
            int renderWidth,
            int renderHeight)
        {
            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + SLOT_SIZE + TOOLTIP_ICON_GAP;
            float titleWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            float sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;
            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            string[] wrappedLine1 = WrapTooltipText(line1, sectionWidth);
            string[] wrappedLine2 = WrapTooltipText(line2, sectionWidth);
            float titleHeight = MeasureLinesHeight(wrappedTitle);
            float line1Height = MeasureLinesHeight(wrappedLine1);
            float line2Height = MeasureLinesHeight(wrappedLine2);
            float contentHeight = line1Height;
            if (line2Height > 0f)
                contentHeight += (contentHeight > 0f ? TOOLTIP_SECTION_GAP : 0f) + line2Height;

            float iconBlockHeight = Math.Max(SLOT_SIZE, contentHeight);
            int tooltipHeight = (int)Math.Ceiling((TOOLTIP_PADDING * 2) + titleHeight + TOOLTIP_TITLE_GAP + iconBlockHeight);
            int tooltipX = _lastMousePosition.X + TOOLTIP_OFFSET_X;
            int tooltipY = _lastMousePosition.Y + 20;
            int tooltipFrameIndex = 1;

            if (tooltipX + tooltipWidth > renderWidth - TOOLTIP_PADDING)
            {
                tooltipX = _lastMousePosition.X - tooltipWidth - TOOLTIP_OFFSET_X;
                tooltipFrameIndex = 0;
            }

            if (tooltipX < TOOLTIP_PADDING)
                tooltipX = TOOLTIP_PADDING;

            if (tooltipY + tooltipHeight > renderHeight - TOOLTIP_PADDING)
            {
                tooltipY = Math.Max(TOOLTIP_PADDING, _lastMousePosition.Y - tooltipHeight + TOOLTIP_OFFSET_Y);
                tooltipFrameIndex = 2;
            }

            Rectangle backgroundRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
            DrawTooltipBackground(sprite, backgroundRect, tooltipFrameIndex);

            int titleX = tooltipX + TOOLTIP_PADDING;
            int titleY = tooltipY + TOOLTIP_PADDING;
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

            int contentY = tooltipY + TOOLTIP_PADDING + (int)Math.Ceiling(titleHeight) + TOOLTIP_TITLE_GAP;
            int iconX = tooltipX + TOOLTIP_PADDING;
            if (itemIcon != null)
            {
                itemIcon.DrawBackground(sprite, null, null, iconX, contentY, Color.White, false, null);
            }
            else if (itemTexture != null)
            {
                sprite.Draw(itemTexture, new Rectangle(iconX, contentY, SLOT_SIZE, SLOT_SIZE), Color.White);
            }

            int textX = tooltipX + textLeftOffset;
            float sectionY = contentY;
            DrawTooltipLines(sprite, wrappedLine1, textX, sectionY, new Color(181, 224, 255));
            if (line1Height > 0f)
                sectionY += line1Height + (line2Height > 0f ? TOOLTIP_SECTION_GAP : 0f);
            DrawTooltipLines(sprite, wrappedLine2, textX, sectionY, Color.White);
        }

        private EquipSlot? GetSlotAtPosition(int mouseX, int mouseY)
        {
            if (_currentTab != TAB_CHARACTER)
                return null;

            foreach ((EquipSlot slot, Point slotPosition) in slotPositions)
            {
                Rectangle slotRect = new Rectangle(Position.X + slotPosition.X, Position.Y + slotPosition.Y, SLOT_SIZE, SLOT_SIZE);
                if (slotRect.Contains(mouseX, mouseY))
                    return slot;
            }

            return null;
        }

        private bool TryGetEquippedPart(EquipSlot uiSlot, out CharacterPart part)
        {
            part = null;
            if (_characterBuild?.Equipment == null)
                return false;

            CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(uiSlot);
            if (characterSlot == null)
                return false;

            return _characterBuild.Equipment.TryGetValue(characterSlot.Value, out part) && part != null;
        }

        private static CharacterEquipSlot? MapToCharacterEquipSlot(EquipSlot uiSlot)
        {
            return uiSlot switch
            {
                EquipSlot.Ring1 => CharacterEquipSlot.Ring1,
                EquipSlot.Ring2 => CharacterEquipSlot.Ring2,
                EquipSlot.Ring3 => CharacterEquipSlot.Ring3,
                EquipSlot.Ring4 => CharacterEquipSlot.Ring4,
                EquipSlot.Pendant1 => CharacterEquipSlot.Pendant,
                EquipSlot.Pendant2 => CharacterEquipSlot.Pendant,
                EquipSlot.Weapon => CharacterEquipSlot.Weapon,
                EquipSlot.Belt => CharacterEquipSlot.Belt,
                EquipSlot.Cap => CharacterEquipSlot.Cap,
                EquipSlot.FaceAccessory => CharacterEquipSlot.FaceAccessory,
                EquipSlot.EyeAccessory => CharacterEquipSlot.EyeAccessory,
                EquipSlot.Top => CharacterEquipSlot.Coat,
                EquipSlot.Bottom => CharacterEquipSlot.Pants,
                EquipSlot.Shoes => CharacterEquipSlot.Shoes,
                EquipSlot.Earring => CharacterEquipSlot.Earrings,
                EquipSlot.Glove => CharacterEquipSlot.Glove,
                EquipSlot.Shield => CharacterEquipSlot.Shield,
                EquipSlot.Cape => CharacterEquipSlot.Cape,
                EquipSlot.Medal => CharacterEquipSlot.Medal,
                EquipSlot.Totem1 => CharacterEquipSlot.TamingMob,
                EquipSlot.Totem2 => CharacterEquipSlot.Saddle,
                _ => null
            };
        }

        private static string ResolveSlotLabel(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.Earring => "Earring",
                EquipSlot.FaceAccessory => "Face Accessory",
                EquipSlot.EyeAccessory => "Eye Accessory",
                EquipSlot.Pendant1 => "Pendant",
                EquipSlot.Pendant2 => "Pendant",
                _ => slot.ToString()
            };
        }

        private int ResolveTooltipWidth()
        {
            int textureWidth = _tooltipFrames[1]?.Width ?? 0;
            return textureWidth > 0 ? textureWidth : TOOLTIP_FALLBACK_WIDTH;
        }

        private void DrawTooltipBackground(SpriteBatch sprite, Rectangle rect, int tooltipFrameIndex)
        {
            Texture2D tooltipFrame = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrames.Length
                ? _tooltipFrames[tooltipFrameIndex]
                : null;

            if (tooltipFrame != null)
            {
                sprite.Draw(tooltipFrame, rect, Color.White);
                return;
            }

            sprite.Draw(_debugPlaceholder, rect, new Color(18, 18, 26, 235));
            DrawTooltipBorder(sprite, rect);
        }

        private void DrawTooltipBorder(SpriteBatch sprite, Rectangle rect)
        {
            Color borderColor = new Color(214, 174, 82);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 1), borderColor);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Bottom, rect.Width + 2, 1), borderColor);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Y, 1, rect.Height), borderColor);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.Right, rect.Y, 1, rect.Height), borderColor);
        }

        private void DrawTooltipLines(SpriteBatch sprite, string[] lines, int x, float y, Color color)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                DrawTooltipText(sprite, lines[i], new Vector2(x, y + (i * _font.LineSpacing)), color);
            }
        }

        private void DrawTooltipText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            sprite.DrawString(_font, text, position + Vector2.One, Color.Black);
            sprite.DrawString(_font, text, position, color);
        }

        private float MeasureLinesHeight(string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return 0f;

            int nonEmptyLines = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    nonEmptyLines++;
            }

            return nonEmptyLines > 0 ? nonEmptyLines * _font.LineSpacing : 0f;
        }

        private string[] WrapTooltipText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            var lines = new List<string>();
            string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');
            foreach (string paragraph in paragraphs)
            {
                string trimmed = paragraph.Trim();
                if (trimmed.Length == 0)
                    continue;

                string currentLine = string.Empty;
                string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string word in words)
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                    {
                        lines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                    lines.Add(currentLine);
            }

            return lines.ToArray();
        }
    }
}
