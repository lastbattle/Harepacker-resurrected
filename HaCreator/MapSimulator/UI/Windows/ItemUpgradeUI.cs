using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Dedicated item-upgrade window scaffold backed by client UI art and a
    /// lightweight simulator enhancement loop for equipped items.
    /// </summary>
    public sealed class ItemUpgradeUI : UIWindowBase
    {
        private const int ItemIconX = 73;
        private const int ItemIconY = 63;
        private const int ItemIconSize = 32;
        private const int PrevButtonX = 22;
        private const int PrevButtonY = 75;
        private const int NextButtonX = 144;
        private const int NextButtonY = 75;
        private const int StartButtonX = 31;
        private const int StartButtonY = 180;
        private const int CancelButtonX = 104;
        private const int CancelButtonY = 180;
        private const int StatusTextX = 15;
        private const int StatusTextY = 157;
        private const int DetailTextX = 16;
        private const int DetailTextY = 97;
        private const int DetailLineGap = 15;
        private const int GaugeInsetX = 8;
        private const int GaugeInsetY = 4;
        private const int EquipEnhancementScrollId = 2049301;
        private const int AdvancedEnhancementScrollId = 2049300;

        private readonly Random _random = new Random();
        private readonly Dictionary<EquipSlot, UpgradeState> _upgradeStates = new Dictionary<EquipSlot, UpgradeState>();

        private Texture2D _backgroundOverlay;
        private Point _backgroundOverlayOffset;
        private Texture2D _headerOverlay;
        private Point _headerOverlayOffset;
        private Texture2D _gaugeBarTexture;
        private Texture2D _gaugeFillTexture;
        private Point _gaugeOffset;
        private SpriteFont _font;
        private UIObject _startButton;
        private UIObject _cancelButton;
        private UIObject _prevButton;
        private UIObject _nextButton;
        private CharacterBuild _characterBuild;
        private IInventoryRuntime _inventory;
        private int _selectedIndex;
        private int? _preferredConsumableItemId;
        private string _statusMessage = "Select equipment and begin enhancement.";
        private bool? _lastUpgradeSucceeded;

        public ItemUpgradeUI(IDXObject frame)
            : base(frame)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.ItemUpgrade;

        public override CharacterBuild CharacterBuild
        {
            get => _characterBuild;
            set
            {
                _characterBuild = value;
                ClampSelection();
            }
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetInventory(IInventoryRuntime inventory)
        {
            _inventory = inventory;
            UpdateButtonStates();
        }

        public static bool IsSupportedConsumable(int itemId)
        {
            return itemId == EquipEnhancementScrollId || itemId == AdvancedEnhancementScrollId;
        }

        public static bool CanUpgrade(EquipSlot slot, CharacterPart part)
        {
            return part != null
                   && !part.IsCash
                   && ResolveDefaultSlotCount(slot, part) > 0;
        }

        public void PrepareConsumableSelection(int itemId)
        {
            if (!IsSupportedConsumable(itemId))
            {
                return;
            }

            _preferredConsumableItemId = itemId;
            _statusMessage = $"{ResolveConsumableName(itemId)} ready. Choose equipment to enhance.";
            _lastUpgradeSucceeded = null;
            UpdateButtonStates();
        }

        public void PrepareEquipmentSelection(EquipSlot slot)
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                _selectedIndex = 0;
                _statusMessage = "No equipped item can be upgraded.";
                _lastUpgradeSucceeded = null;
                UpdateButtonStates();
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Key != slot)
                {
                    continue;
                }

                _selectedIndex = i;
                CharacterPart selectedPart = candidates[i].Value;
                _statusMessage = $"{ResolveItemName(selectedPart)} selected for enhancement.";
                _lastUpgradeSucceeded = null;
                UpdateButtonStates();
                return;
            }
        }

        public void SetDecorations(Texture2D backgroundOverlay, Point backgroundOverlayOffset, Texture2D headerOverlay, Point headerOverlayOffset)
        {
            _backgroundOverlay = backgroundOverlay;
            _backgroundOverlayOffset = backgroundOverlayOffset;
            _headerOverlay = headerOverlay;
            _headerOverlayOffset = headerOverlayOffset;
        }

        public void SetGaugeTextures(Texture2D gaugeBarTexture, Texture2D gaugeFillTexture, Point gaugeOffset)
        {
            _gaugeBarTexture = gaugeBarTexture;
            _gaugeFillTexture = gaugeFillTexture;
            _gaugeOffset = gaugeOffset;
        }

        public void InitializeUpgradeButtons(UIObject startButton, UIObject cancelButton, UIObject prevButton, UIObject nextButton)
        {
            _startButton = startButton;
            _cancelButton = cancelButton;
            _prevButton = prevButton;
            _nextButton = nextButton;

            if (_startButton != null)
            {
                _startButton.X = StartButtonX;
                _startButton.Y = StartButtonY;
                AddButton(_startButton);
                _startButton.ButtonClickReleased += _ => TryApplyUpgrade();
            }

            if (_cancelButton != null)
            {
                _cancelButton.X = CancelButtonX;
                _cancelButton.Y = CancelButtonY;
                AddButton(_cancelButton);
                _cancelButton.ButtonClickReleased += _ => Hide();
            }

            if (_prevButton != null)
            {
                _prevButton.X = PrevButtonX;
                _prevButton.Y = PrevButtonY;
                AddButton(_prevButton);
                _prevButton.ButtonClickReleased += _ => MoveSelection(-1);
            }

            if (_nextButton != null)
            {
                _nextButton.X = NextButtonX;
                _nextButton.Y = NextButtonY;
                AddButton(_nextButton);
                _nextButton.ButtonClickReleased += _ => MoveSelection(1);
            }

            UpdateButtonStates();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            ClampSelection();
            UpdateButtonStates();
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
            int TickCount)
        {
            int windowX = Position.X;
            int windowY = Position.Y;

            if (_backgroundOverlay != null)
            {
                sprite.Draw(_backgroundOverlay, new Vector2(windowX + _backgroundOverlayOffset.X, windowY + _backgroundOverlayOffset.Y), Color.White);
            }

            if (_headerOverlay != null)
            {
                sprite.Draw(_headerOverlay, new Vector2(windowX + _headerOverlayOffset.X, windowY + _headerOverlayOffset.Y), Color.White);
            }

            DrawGauge(sprite, windowX, windowY);

            if (_font == null)
            {
                return;
            }

            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                DrawShadowedText(sprite, "No eligible equips available.", new Vector2(windowX + DetailTextX, windowY + DetailTextY), Color.White);
                DrawShadowedText(sprite, "Equip non-cash gear in the equipment window first.", new Vector2(windowX + DetailTextX, windowY + DetailTextY + DetailLineGap), new Color(210, 210, 210));
                return;
            }

            KeyValuePair<EquipSlot, CharacterPart> selection = candidates[_selectedIndex];
            CharacterPart selectedPart = selection.Value;
            UpgradeState state = GetOrCreateState(selection.Key, selectedPart);
            EnhancementConsumable consumable = ResolveConsumable(state);

            DrawSelectedItem(sprite, windowX + ItemIconX, windowY + ItemIconY, selectedPart);

            string itemName = string.IsNullOrWhiteSpace(selectedPart.Name) ? $"Equip {selectedPart.ItemId}" : selectedPart.Name;
            DrawShadowedText(sprite, itemName, new Vector2(windowX + DetailTextX, windowY + 40), new Color(255, 220, 120));
            DrawShadowedText(sprite, $"{ResolveSlotLabel(selection.Key)}  [{_selectedIndex + 1}/{candidates.Count}]", new Vector2(windowX + DetailTextX, windowY + 57), Color.White);

            Color statColor = state.RemainingSlots > 0 ? new Color(191, 255, 191) : new Color(255, 180, 180);
            DrawShadowedText(sprite, $"Slots: {state.RemainingSlots}/{state.TotalSlots}", new Vector2(windowX + DetailTextX, windowY + DetailTextY), statColor);
            DrawShadowedText(sprite, $"Success: {state.SuccessCount}   Fail: {state.FailCount}", new Vector2(windowX + DetailTextX, windowY + DetailTextY + DetailLineGap), Color.White);
            DrawShadowedText(sprite, $"Bonus: ATT +{state.AttackBonus}  DEF +{state.DefenseBonus}", new Vector2(windowX + DetailTextX, windowY + DetailTextY + (DetailLineGap * 2)), new Color(181, 224, 255));
            string scrollText = consumable != null
                ? $"Scroll: {consumable.Name} x{GetConsumableCount(consumable.ItemId)}"
                : $"Scroll: None  (ESS {GetConsumableCount(EquipEnhancementScrollId)} / Adv {GetConsumableCount(AdvancedEnhancementScrollId)})";
            DrawShadowedText(sprite, scrollText, new Vector2(windowX + DetailTextX, windowY + DetailTextY + (DetailLineGap * 3)), new Color(255, 232, 173));

            Color statusColor = _lastUpgradeSucceeded switch
            {
                true => new Color(160, 255, 160),
                false => new Color(255, 170, 170),
                _ => new Color(220, 220, 220)
            };
            DrawShadowedText(sprite, _statusMessage, new Vector2(windowX + StatusTextX, windowY + StatusTextY), statusColor);
        }

        private void DrawSelectedItem(SpriteBatch sprite, int x, int y, CharacterPart selectedPart)
        {
            IDXObject icon = selectedPart?.IconRaw ?? selectedPart?.Icon;
            if (icon != null)
            {
                icon.DrawBackground(sprite, null, null, x, y, Color.White, false, null);
                return;
            }

            if (_gaugeFillTexture != null)
            {
                sprite.Draw(_gaugeFillTexture, new Rectangle(x, y, ItemIconSize, ItemIconSize), Color.White);
            }
        }

        private void DrawGauge(SpriteBatch sprite, int windowX, int windowY)
        {
            if (_gaugeBarTexture == null)
            {
                return;
            }

            int gaugeX = windowX + _gaugeOffset.X;
            int gaugeY = windowY + _gaugeOffset.Y;
            sprite.Draw(_gaugeBarTexture, new Vector2(gaugeX, gaugeY), Color.White);

            if (_gaugeFillTexture == null)
            {
                return;
            }

            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                return;
            }

            KeyValuePair<EquipSlot, CharacterPart> selection = candidates[_selectedIndex];
            UpgradeState state = GetOrCreateState(selection.Key, selection.Value);
            if (state.TotalSlots <= 0)
            {
                return;
            }

            float completionRatio = MathHelper.Clamp((float)(state.TotalSlots - state.RemainingSlots) / state.TotalSlots, 0f, 1f);
            int fillWidth = (int)Math.Round((_gaugeBarTexture.Width - (GaugeInsetX * 2)) * completionRatio);
            if (fillWidth <= 0)
            {
                return;
            }

            Rectangle destination = new Rectangle(gaugeX + GaugeInsetX, gaugeY + GaugeInsetY, fillWidth, _gaugeFillTexture.Height);
            sprite.Draw(_gaugeFillTexture, destination, Color.White);
        }

        private void TryApplyUpgrade()
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                _statusMessage = "No equipped item can be upgraded.";
                _lastUpgradeSucceeded = null;
                return;
            }

            if (_inventory == null)
            {
                _statusMessage = "Inventory runtime is unavailable.";
                _lastUpgradeSucceeded = false;
                return;
            }

            KeyValuePair<EquipSlot, CharacterPart> selection = candidates[_selectedIndex];
            CharacterPart selectedPart = selection.Value;
            UpgradeState state = GetOrCreateState(selection.Key, selectedPart);
            if (state.RemainingSlots <= 0)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} has no upgrade slots left.";
                _lastUpgradeSucceeded = false;
                return;
            }

            EnhancementConsumable consumable = ResolveConsumable(state);
            if (consumable == null)
            {
                _statusMessage = "No enhancement scrolls are available in inventory.";
                _lastUpgradeSucceeded = false;
                return;
            }

            if (!_inventory.TryConsumeItem(InventoryType.USE, consumable.ItemId, 1))
            {
                _statusMessage = $"{consumable.Name} could not be consumed.";
                _lastUpgradeSucceeded = false;
                return;
            }

            bool success = _random.NextDouble() < ResolveSuccessRate(consumable.ItemId, state);
            state.RemainingSlots--;
            state.Attempts++;

            if (success)
            {
                state.SuccessCount++;
                ApplyUpgradeBonus(selection.Key, state);
                _statusMessage = $"{ResolveItemName(selectedPart)} enhanced successfully with {consumable.Name}.";
            }
            else
            {
                state.FailCount++;
                if (ResolveDestroyOnFailure(consumable.ItemId, state))
                {
                    DestroySelectedItem(selection.Key, selectedPart, state, consumable.Name);
                }
                else
                {
                    _statusMessage = $"{ResolveItemName(selectedPart)} failed with {consumable.Name}. A slot was consumed.";
                }
            }

            _lastUpgradeSucceeded = success;
            ClampSelection();
        }

        private void ApplyUpgradeBonus(EquipSlot slot, UpgradeState state)
        {
            if (_characterBuild == null)
            {
                return;
            }

            switch (slot)
            {
                case EquipSlot.Weapon:
                    state.AttackBonus += 2;
                    _characterBuild.Attack += 2;
                    break;
                case EquipSlot.Glove:
                    state.AttackBonus += 1;
                    _characterBuild.Attack += 1;
                    break;
                default:
                    state.DefenseBonus += 1;
                    _characterBuild.Defense += 1;
                    break;
            }
        }

        private float ResolveSuccessRate(int consumableItemId, UpgradeState state)
        {
            int nextSuccessCount = state.SuccessCount + 1;
            if (consumableItemId == AdvancedEnhancementScrollId)
            {
                return nextSuccessCount switch
                {
                    <= 1 => 1.0f,
                    2 => 0.9f,
                    3 => 0.8f,
                    4 => 0.7f,
                    5 => 0.6f,
                    6 => 0.5f,
                    7 => 0.4f,
                    8 => 0.3f,
                    9 => 0.2f,
                    _ => 0.1f
                };
            }

            return nextSuccessCount switch
            {
                <= 1 => 0.8f,
                2 => 0.7f,
                3 => 0.6f,
                4 => 0.5f,
                5 => 0.4f,
                6 => 0.3f,
                7 => 0.2f,
                8 => 0.1f,
                _ => 0.05f
            };
        }

        private void MoveSelection(int delta)
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                return;
            }

            _selectedIndex = (_selectedIndex + delta) % candidates.Count;
            if (_selectedIndex < 0)
            {
                _selectedIndex += candidates.Count;
            }

            CharacterPart selectedPart = candidates[_selectedIndex].Value;
            _statusMessage = $"{ResolveItemName(selectedPart)} selected for enhancement.";
            _lastUpgradeSucceeded = null;
            UpdateButtonStates();
        }

        private void ClampSelection()
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                _selectedIndex = 0;
                return;
            }

            if (_selectedIndex >= candidates.Count)
            {
                _selectedIndex = candidates.Count - 1;
            }
            else if (_selectedIndex < 0)
            {
                _selectedIndex = 0;
            }
        }

        private void UpdateButtonStates()
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            bool hasCandidates = candidates.Count > 0;

            _prevButton?.SetEnabled(hasCandidates && candidates.Count > 1);
            _nextButton?.SetEnabled(hasCandidates && candidates.Count > 1);
            if (!hasCandidates)
            {
                _startButton?.SetEnabled(false);
                return;
            }

            KeyValuePair<EquipSlot, CharacterPart> selection = candidates[_selectedIndex];
            UpgradeState state = GetOrCreateState(selection.Key, selection.Value);
            _startButton?.SetEnabled(state.RemainingSlots > 0 && ResolveConsumable(state) != null);
        }

        private IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> GetCandidates()
        {
            if (_characterBuild?.Equipment == null || _characterBuild.Equipment.Count == 0)
            {
                return Array.Empty<KeyValuePair<EquipSlot, CharacterPart>>();
            }

            return _characterBuild.Equipment
                .Where(entry => entry.Value != null
                    && entry.Key != EquipSlot.None
                    && CanUpgrade(entry.Key, entry.Value))
                .OrderBy(entry => entry.Key)
                .ToArray();
        }

        private EnhancementConsumable ResolveConsumable(UpgradeState state)
        {
            if (_inventory == null)
            {
                return null;
            }

            if (_preferredConsumableItemId.HasValue)
            {
                int preferredItemId = _preferredConsumableItemId.Value;
                if (GetConsumableCount(preferredItemId) > 0)
                {
                    return new EnhancementConsumable(preferredItemId, ResolveConsumableName(preferredItemId));
                }

                _preferredConsumableItemId = null;
            }

            int normalCount = GetConsumableCount(EquipEnhancementScrollId);
            int advancedCount = GetConsumableCount(AdvancedEnhancementScrollId);
            if (advancedCount > 0 && (state.SuccessCount >= 5 || normalCount == 0))
            {
                return new EnhancementConsumable(AdvancedEnhancementScrollId, ResolveConsumableName(AdvancedEnhancementScrollId));
            }

            if (normalCount > 0)
            {
                return new EnhancementConsumable(EquipEnhancementScrollId, ResolveConsumableName(EquipEnhancementScrollId));
            }

            if (advancedCount > 0)
            {
                return new EnhancementConsumable(AdvancedEnhancementScrollId, ResolveConsumableName(AdvancedEnhancementScrollId));
            }

            return null;
        }

        private bool ResolveDestroyOnFailure(int consumableItemId, UpgradeState state)
        {
            float destroyRate = consumableItemId == AdvancedEnhancementScrollId
                ? state.SuccessCount switch
                {
                    >= 7 => 0.4f,
                    >= 5 => 0.3f,
                    >= 3 => 0.2f,
                    _ => 0.1f
                }
                : state.SuccessCount switch
                {
                    >= 5 => 0.25f,
                    >= 3 => 0.15f,
                    _ => 0.05f
                };

            return _random.NextDouble() < destroyRate;
        }

        private int GetConsumableCount(int itemId)
        {
            return _inventory?.GetItemCount(InventoryType.USE, itemId) ?? 0;
        }

        private static string ResolveConsumableName(int itemId)
        {
            return HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo) &&
                   !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : $"Item #{itemId}";
        }

        private void DestroySelectedItem(EquipSlot slot, CharacterPart selectedPart, UpgradeState state, string consumableName)
        {
            _characterBuild?.Equipment.Remove(slot);
            if (_characterBuild != null)
            {
                _characterBuild.Attack = Math.Max(0, _characterBuild.Attack - state.AttackBonus);
                _characterBuild.Defense = Math.Max(0, _characterBuild.Defense - state.DefenseBonus);
            }

            _upgradeStates.Remove(slot);
            _statusMessage = $"{ResolveItemName(selectedPart)} was destroyed by {consumableName}.";
        }

        private UpgradeState GetOrCreateState(EquipSlot slot, CharacterPart part)
        {
            if (!_upgradeStates.TryGetValue(slot, out UpgradeState state))
            {
                state = new UpgradeState
                {
                    TotalSlots = ResolveDefaultSlotCount(slot, part),
                    RemainingSlots = ResolveDefaultSlotCount(slot, part)
                };
                _upgradeStates[slot] = state;
            }

            return state;
        }

        private static int ResolveDefaultSlotCount(EquipSlot slot, CharacterPart part)
        {
            if (part?.UpgradeSlots > 0)
            {
                return part.UpgradeSlots;
            }

            if (part?.IsCash == true)
            {
                return 0;
            }

            return slot switch
            {
                EquipSlot.Weapon => 7,
                EquipSlot.Glove => 5,
                EquipSlot.Shield => 5,
                EquipSlot.Cap => 5,
                EquipSlot.Coat => 5,
                EquipSlot.Longcoat => 5,
                EquipSlot.Pants => 5,
                EquipSlot.Shoes => 5,
                EquipSlot.Cape => 4,
                EquipSlot.Belt => 4,
                EquipSlot.Medal => 3,
                EquipSlot.FaceAccessory => 3,
                EquipSlot.EyeAccessory => 3,
                EquipSlot.Earrings => 3,
                EquipSlot.Pendant => 3,
                EquipSlot.Ring1 => 2,
                EquipSlot.Ring2 => 2,
                EquipSlot.Ring3 => 2,
                EquipSlot.Ring4 => 2,
                _ => 4
            };
        }

        private static string ResolveItemName(CharacterPart part)
        {
            return string.IsNullOrWhiteSpace(part?.Name) ? $"Equip {part?.ItemId ?? 0}" : part.Name;
        }

        private static string ResolveSlotLabel(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.FaceAccessory => "Face Accessory",
                EquipSlot.EyeAccessory => "Eye Accessory",
                EquipSlot.Longcoat => "Overall",
                _ => slot.ToString()
            };
        }

        private void DrawShadowedText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position + Vector2.One, Color.Black);
            sprite.DrawString(_font, text, position, color);
        }

        private sealed class UpgradeState
        {
            public int TotalSlots { get; set; }
            public int RemainingSlots { get; set; }
            public int Attempts { get; set; }
            public int SuccessCount { get; set; }
            public int FailCount { get; set; }
            public int AttackBonus { get; set; }
            public int DefenseBonus { get; set; }
        }

        private sealed class EnhancementConsumable
        {
            public EnhancementConsumable(int itemId, string name)
            {
                ItemId = itemId;
                Name = name;
            }

            public int ItemId { get; }
            public string Name { get; }
        }
    }
}
