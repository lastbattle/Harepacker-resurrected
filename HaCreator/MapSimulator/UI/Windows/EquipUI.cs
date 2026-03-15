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
using EquipSlotStateResolver = HaCreator.MapSimulator.Character.EquipSlotStateResolver;
using EquipSlotVisualState = HaCreator.MapSimulator.Character.EquipSlotVisualState;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Equipment UI window displaying equipped items.
    /// Structure: UI.wz/UIWindow.img/Equip/
    /// </summary>
    public class EquipUI : UIWindowBase
    {
        private const int SLOT_SIZE = 32;
        private const int TOOLTIP_PADDING = 8;
        private const int TOOLTIP_OFFSET_X = 14;
        private const int TOOLTIP_OFFSET_Y = 8;

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

        private readonly Dictionary<EquipSlot, Point> slotPositions;
        private readonly Dictionary<EquipSlot, EquipSlotData> equippedItems;
        private readonly Texture2D _overlayPixel;
        private SpriteFont _font;
        private CharacterBuild _characterBuild;
        private EquipSlot? _hoveredSlot;
        private Point _lastMousePosition;

        private UIObject _tabNormal;
        private UIObject _tabCash;
        private UIObject _tabPet;
        private int _currentTab;

        public override string WindowName => "Equipment";
        public override CharacterBuild CharacterBuild
        {
            get => _characterBuild;
            set => _characterBuild = value;
        }

        public EquipUI(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            slotPositions = new Dictionary<EquipSlot, Point>
            {
                { EquipSlot.Ring1, new Point(12, 55) },
                { EquipSlot.Ring2, new Point(12, 92) },
                { EquipSlot.Ring3, new Point(12, 129) },
                { EquipSlot.Ring4, new Point(12, 166) },
                { EquipSlot.Pocket, new Point(12, 203) },
                { EquipSlot.Pendant1, new Point(49, 55) },
                { EquipSlot.Pendant2, new Point(49, 92) },
                { EquipSlot.Weapon, new Point(49, 129) },
                { EquipSlot.Belt, new Point(49, 166) },
                { EquipSlot.Cap, new Point(135, 55) },
                { EquipSlot.FaceAccessory, new Point(135, 92) },
                { EquipSlot.EyeAccessory, new Point(135, 129) },
                { EquipSlot.Top, new Point(135, 166) },
                { EquipSlot.Bottom, new Point(135, 203) },
                { EquipSlot.Shoes, new Point(135, 240) },
                { EquipSlot.Earring, new Point(172, 55) },
                { EquipSlot.Shoulder, new Point(172, 92) },
                { EquipSlot.Glove, new Point(172, 129) },
                { EquipSlot.Shield, new Point(172, 166) },
                { EquipSlot.Cape, new Point(172, 203) }
            };

            equippedItems = new Dictionary<EquipSlot, EquipSlotData>();
            _overlayPixel = new Texture2D(device, 1, 1);
            _overlayPixel.SetData(new[] { Color.White });
        }

        public void InitializeTabs(UIObject normalTab, UIObject cashTab, UIObject petTab)
        {
            _tabNormal = normalTab;
            _tabCash = cashTab;
            _tabPet = petTab;

            if (normalTab != null)
            {
                AddButton(normalTab);
                normalTab.ButtonClickReleased += _ => { _currentTab = 0; UpdateTabStates(); };
            }

            if (cashTab != null)
            {
                AddButton(cashTab);
                cashTab.ButtonClickReleased += _ => { _currentTab = 1; UpdateTabStates(); };
            }

            if (petTab != null)
            {
                AddButton(petTab);
                petTab.ButtonClickReleased += _ => { _currentTab = 2; UpdateTabStates(); };
            }

            UpdateTabStates();
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            foreach ((EquipSlot uiSlot, Point slotPosition) in slotPositions)
            {
                int slotX = Position.X + slotPosition.X;
                int slotY = Position.Y + slotPosition.Y;

                CharacterPart part = ResolveEquippedPart(uiSlot);
                EquipSlotData slotData = equippedItems.TryGetValue(uiSlot, out EquipSlotData data) ? data : null;
                if (part != null || slotData != null)
                {
                    DrawEquippedItemIcon(sprite, part, slotData, slotX, slotY);
                }

                CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(uiSlot);
                if (!characterSlot.HasValue)
                {
                    continue;
                }

                EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, characterSlot.Value);
                if (visualState.IsDisabled)
                {
                    DrawDisabledOverlay(sprite, slotX, slotY, visualState);
                }
            }
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            DrawHoverTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
        }

        public void EquipItem(EquipSlot slot, int itemId, Texture2D texture, string itemName = "")
        {
            equippedItems[slot] = new EquipSlotData
            {
                ItemId = itemId,
                ItemTexture = texture,
                ItemName = itemName
            };
        }

        public void UnequipItem(EquipSlot slot)
        {
            if (equippedItems.ContainsKey(slot))
            {
                equippedItems.Remove(slot);
            }
        }

        public EquipSlotData GetEquippedItem(EquipSlot slot)
        {
            return equippedItems.TryGetValue(slot, out EquipSlotData data) ? data : null;
        }

        public void ClearAllEquipment()
        {
            equippedItems.Clear();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            MouseState mouseState = Mouse.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            _hoveredSlot = GetSlotAtPosition(mouseState.X, mouseState.Y);
        }

        private void UpdateTabStates()
        {
            _tabNormal?.SetButtonState(_currentTab == 0 ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabCash?.SetButtonState(_currentTab == 1 ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabPet?.SetButtonState(_currentTab == 2 ? UIObjectState.Pressed : UIObjectState.Normal);
        }

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

        private void DrawDisabledOverlay(SpriteBatch sprite, int slotX, int slotY, EquipSlotVisualState visualState)
        {
            Color overlay = visualState.IsExpired
                ? new Color(110, 30, 30, 180)
                : visualState.IsBroken
                    ? new Color(90, 60, 20, 180)
                    : new Color(20, 20, 20, 145);
            sprite.Draw(_overlayPixel, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), overlay);
            sprite.Draw(_overlayPixel, new Rectangle(slotX, slotY, SLOT_SIZE, 2), Color.Black);
            sprite.Draw(_overlayPixel, new Rectangle(slotX, slotY + SLOT_SIZE - 2, SLOT_SIZE, 2), Color.Black);
            sprite.Draw(_overlayPixel, new Rectangle(slotX, slotY, 2, SLOT_SIZE), Color.Black);
            sprite.Draw(_overlayPixel, new Rectangle(slotX + SLOT_SIZE - 2, slotY, 2, SLOT_SIZE), Color.Black);
        }

        private void DrawHoverTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (_font == null || !_hoveredSlot.HasValue)
            {
                return;
            }

            string title = null;
            string line = null;

            CharacterPart part = ResolveEquippedPart(_hoveredSlot.Value);
            if (part != null)
            {
                title = string.IsNullOrWhiteSpace(part.Name) ? $"Equip {part.ItemId}" : part.Name;
                line = $"Item ID: {part.ItemId}";

                CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(_hoveredSlot.Value);
                if (characterSlot.HasValue)
                {
                    EquipSlotVisualState state = EquipSlotStateResolver.ResolveVisualState(_characterBuild, characterSlot.Value);
                    if (!string.IsNullOrWhiteSpace(state.Message))
                    {
                        line = $"{line}  {state.Message}";
                    }
                }
            }
            else if (equippedItems.TryGetValue(_hoveredSlot.Value, out EquipSlotData slotData))
            {
                title = slotData.ItemName;
                line = $"Item ID: {slotData.ItemId}";
            }
            else
            {
                CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(_hoveredSlot.Value);
                if (!characterSlot.HasValue)
                {
                    return;
                }

                EquipSlotVisualState state = EquipSlotStateResolver.ResolveVisualState(_characterBuild, characterSlot.Value);
                if (!state.IsDisabled)
                {
                    return;
                }

                title = ResolveSlotLabel(_hoveredSlot.Value);
                line = state.Message;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            Vector2 titleSize = _font.MeasureString(title);
            Vector2 lineSize = string.IsNullOrWhiteSpace(line) ? Vector2.Zero : _font.MeasureString(line);
            int width = (int)Math.Ceiling(Math.Max(titleSize.X, lineSize.X)) + (TOOLTIP_PADDING * 2);
            int height = (int)Math.Ceiling(titleSize.Y + (lineSize.Y > 0 ? lineSize.Y + 4 : 0)) + (TOOLTIP_PADDING * 2);
            int x = Math.Min(_lastMousePosition.X + TOOLTIP_OFFSET_X, Math.Max(TOOLTIP_PADDING, renderWidth - width - TOOLTIP_PADDING));
            int y = _lastMousePosition.Y - height - TOOLTIP_OFFSET_Y;
            if (y < TOOLTIP_PADDING)
            {
                y = Math.Min(renderHeight - height - TOOLTIP_PADDING, _lastMousePosition.Y + TOOLTIP_OFFSET_Y);
            }

            Rectangle rect = new Rectangle(x, y, width, height);
            sprite.Draw(_overlayPixel, rect, new Color(18, 18, 26, 235));
            sprite.Draw(_overlayPixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(214, 174, 82));
            sprite.Draw(_overlayPixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Color(214, 174, 82));
            sprite.Draw(_overlayPixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Color(214, 174, 82));
            sprite.Draw(_overlayPixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Color(214, 174, 82));
            sprite.DrawString(_font, title, new Vector2(rect.X + TOOLTIP_PADDING, rect.Y + TOOLTIP_PADDING), new Color(255, 220, 120));
            if (!string.IsNullOrWhiteSpace(line))
            {
                sprite.DrawString(_font, line, new Vector2(rect.X + TOOLTIP_PADDING, rect.Y + TOOLTIP_PADDING + titleSize.Y + 4), Color.White);
            }
        }

        private EquipSlot? GetSlotAtPosition(int mouseX, int mouseY)
        {
            foreach ((EquipSlot slot, Point slotPosition) in slotPositions)
            {
                Rectangle slotRect = new Rectangle(Position.X + slotPosition.X, Position.Y + slotPosition.Y, SLOT_SIZE, SLOT_SIZE);
                if (slotRect.Contains(mouseX, mouseY))
                {
                    return slot;
                }
            }

            return null;
        }

        private CharacterPart ResolveEquippedPart(EquipSlot uiSlot)
        {
            CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(uiSlot);
            return characterSlot.HasValue ? EquipSlotStateResolver.ResolveDisplayedPart(_characterBuild, characterSlot.Value) : null;
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
    }

    public class EquipSlotData
    {
        public int ItemId { get; set; }
        public Texture2D ItemTexture { get; set; }
        public IDXObject ItemIcon { get; set; }
        public string ItemName { get; set; }
        public int STR { get; set; }
        public int DEX { get; set; }
        public int INT { get; set; }
        public int LUK { get; set; }
        public int HP { get; set; }
        public int MP { get; set; }
        public int WATK { get; set; }
        public int MATK { get; set; }
        public int WDEF { get; set; }
        public int MDEF { get; set; }
        public int Speed { get; set; }
        public int Jump { get; set; }
        public int Slots { get; set; }
        public int Stars { get; set; }
    }
}
