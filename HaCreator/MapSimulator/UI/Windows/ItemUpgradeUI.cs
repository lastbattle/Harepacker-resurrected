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
        private const int AlternateEquipEnhancementScrollId = 2049307;
        private const int AlternateAdvancedEnhancementScrollId = 2049306;
        private const int AlternateAdvancedEnhancementScrollId2 = 2049303;
        private const int TwoStarEnhancementScrollId = 2049309;
        private const int ThreeStarEnhancementScrollId = 2049304;
        private const int FourStarEnhancementScrollId = 2049305;
        private const int FiveStarEnhancementScrollId = 2049308;
        private const int AdvancedPotentialScrollId = 2049400;
        private const int PotentialScrollId = 2049401;
        private const int SpecialPotentialScrollId = 2049406;
        private const int MiracleCubeId = 5062000;

        private readonly Random _random = new Random();
        private readonly Dictionary<EquipSlot, UpgradeState> _upgradeStates = new Dictionary<EquipSlot, UpgradeState>();
        private static readonly IReadOnlyDictionary<int, EnhancementConsumableDefinition> ConsumableDefinitions =
            new Dictionary<int, EnhancementConsumableDefinition>
            {
                [EquipEnhancementScrollId] = new(EquipEnhancementScrollId, "Equip Enhancement Scroll", 1, true, false, 0f),
                [AdvancedEnhancementScrollId] = new(AdvancedEnhancementScrollId, "Advanced Equip Enhancement Scroll", 1, true, true, 0f),
                [AlternateEquipEnhancementScrollId] = new(AlternateEquipEnhancementScrollId, "Equip Enhancement Scroll", 1, true, false, 0f),
                [AlternateAdvancedEnhancementScrollId] = new(AlternateAdvancedEnhancementScrollId, "Advanced Equip Enhancement Scroll", 1, true, true, 0f),
                [AlternateAdvancedEnhancementScrollId2] = new(AlternateAdvancedEnhancementScrollId2, "Advanced Equip Enhancement Scroll", 1, true, true, 0f),
                [TwoStarEnhancementScrollId] = new(TwoStarEnhancementScrollId, "2-Star Enhancement Scroll", 2, false, false, 0.8f),
                [ThreeStarEnhancementScrollId] = new(ThreeStarEnhancementScrollId, "3 Star Enhancement Scroll", 3, false, false, 0.8f),
                [FourStarEnhancementScrollId] = new(FourStarEnhancementScrollId, "4 Star Enhancement Scroll", 4, false, false, 0.6f),
                [FiveStarEnhancementScrollId] = new(FiveStarEnhancementScrollId, "5 Star Enhancement Scroll", 5, false, false, 0.5f),
                [AdvancedPotentialScrollId] = new(AdvancedPotentialScrollId, "Advanced Potential Scroll", 0, false, false, 0.9f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Epic, true),
                [PotentialScrollId] = new(PotentialScrollId, "Potential Scroll", 0, false, false, 0.7f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Rare, true),
                [SpecialPotentialScrollId] = new(SpecialPotentialScrollId, "Special Potential Scroll", 0, false, false, 1.0f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Epic, false),
                [MiracleCubeId] = new(MiracleCubeId, "Miracle Cube", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.Cube, PotentialTier.Rare, false)
            };

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
            return ConsumableDefinitions.ContainsKey(itemId);
        }

        public static bool CanUpgrade(EquipSlot slot, CharacterPart part)
        {
            return part != null
                   && !part.IsCash
                   && ResolveDefaultSlotCount(slot, part) > 0;
        }

        public void PrepareConsumableSelection(int itemId)
        {
            if (!TryGetConsumableDefinition(itemId, out EnhancementConsumableDefinition definition))
            {
                return;
            }

            _preferredConsumableItemId = itemId;
            _statusMessage = $"{definition.Name} ready. Choose equipment to enhance.";
            _lastUpgradeSucceeded = null;
            UpdateButtonStates();
        }

        public void PrepareNpcLaunch()
        {
            _preferredConsumableItemId = null;
            _lastUpgradeSucceeded = null;
            ClampSelection();
            _statusMessage = GetCandidates().Count > 0
                ? "Select equipment and begin enhancement."
                : "No equipped item can be upgraded.";
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
            string potentialText = state.HasPotential
                ? $"Potential: {state.PotentialTier}  {string.Join(" / ", state.PotentialLines.Where(line => !string.IsNullOrWhiteSpace(line)))}"
                : "Potential: None";
            DrawShadowedText(sprite, potentialText, new Vector2(windowX + DetailTextX, windowY + DetailTextY + (DetailLineGap * 3)), new Color(210, 198, 255));
            string scrollText = consumable != null
                ? $"Scroll: {consumable.Name} x{GetConsumableCount(consumable.ItemId)}"
                : $"Scroll: None  ({BuildConsumableSummary()})";
            if (consumable != null &&
                consumable.EffectType == ConsumableEffectType.Enhancement &&
                state.RemainingSlots < consumable.SuccessCountGain)
            {
                scrollText += $"  Need {consumable.SuccessCountGain} slots";
            }

            DrawShadowedText(sprite, scrollText, new Vector2(windowX + DetailTextX, windowY + DetailTextY + (DetailLineGap * 4)), new Color(255, 232, 173));

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
            EnhancementConsumable consumable = ResolveConsumable(state);
            if (consumable == null)
            {
                _statusMessage = "No enhancement scrolls are available in inventory.";
                _lastUpgradeSucceeded = false;
                return;
            }

            if (consumable.EffectType == ConsumableEffectType.Enhancement &&
                state.RemainingSlots <= 0)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} has no upgrade slots left.";
                _lastUpgradeSucceeded = false;
                return;
            }

            if (consumable.EffectType == ConsumableEffectType.Enhancement &&
                state.RemainingSlots < consumable.SuccessCountGain)
            {
                _statusMessage = $"{consumable.Name} needs {consumable.SuccessCountGain} open slots on {ResolveItemName(selectedPart)}.";
                _lastUpgradeSucceeded = false;
                return;
            }

            if (consumable.EffectType == ConsumableEffectType.PotentialScroll && state.HasPotential)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} already has revealed potential.";
                _lastUpgradeSucceeded = false;
                return;
            }

            if (consumable.EffectType == ConsumableEffectType.Cube && !state.HasPotential)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} needs revealed potential before using {consumable.Name}.";
                _lastUpgradeSucceeded = false;
                return;
            }

            if (!_inventory.TryConsumeItem(consumable.InventoryType, consumable.ItemId, 1))
            {
                _statusMessage = $"{consumable.Name} could not be consumed.";
                _lastUpgradeSucceeded = false;
                return;
            }

            bool success = _random.NextDouble() < ResolveSuccessRate(consumable, state);
            state.Attempts++;

            switch (consumable.EffectType)
            {
                case ConsumableEffectType.PotentialScroll:
                    ApplyPotentialScroll(selection.Key, selectedPart, state, consumable, success);
                    break;
                case ConsumableEffectType.Cube:
                    ApplyCube(selectedPart, state, consumable);
                    success = true;
                    break;
                default:
                    ApplyEnhancementScroll(selection.Key, selectedPart, state, consumable, success);
                    break;
            }

            _lastUpgradeSucceeded = success;
            ClampSelection();
        }

        private void ApplyUpgradeBonus(EquipSlot slot, UpgradeState state, int successCountGain)
        {
            if (_characterBuild == null || successCountGain <= 0)
            {
                return;
            }

            switch (slot)
            {
                case EquipSlot.Weapon:
                    state.AttackBonus += 2 * successCountGain;
                    _characterBuild.Attack += 2 * successCountGain;
                    break;
                case EquipSlot.Glove:
                    state.AttackBonus += successCountGain;
                    _characterBuild.Attack += successCountGain;
                    break;
                default:
                    state.DefenseBonus += successCountGain;
                    _characterBuild.Defense += successCountGain;
                    break;
            }
        }

        private void ApplyEnhancementScroll(EquipSlot slot, CharacterPart selectedPart, UpgradeState state, EnhancementConsumable consumable, bool success)
        {
            if (success)
            {
                state.RemainingSlots = Math.Max(0, state.RemainingSlots - consumable.SuccessCountGain);
                state.SuccessCount += consumable.SuccessCountGain;
                ApplyUpgradeBonus(slot, state, consumable.SuccessCountGain);
                _statusMessage = $"{ResolveItemName(selectedPart)} gained {consumable.SuccessCountGain} enhancement" +
                                 (consumable.SuccessCountGain == 1 ? string.Empty : "s") +
                                 $" with {consumable.Name}.";
                return;
            }

            state.FailCount++;
            if (ResolveDestroyOnFailure(consumable))
            {
                DestroySelectedItem(slot, selectedPart, state, consumable.Name);
                return;
            }

            state.RemainingSlots = Math.Max(0, state.RemainingSlots - 1);
            _statusMessage = $"{ResolveItemName(selectedPart)} failed with {consumable.Name}. A slot was consumed.";
        }

        private void ApplyPotentialScroll(EquipSlot slot, CharacterPart selectedPart, UpgradeState state, EnhancementConsumable consumable, bool success)
        {
            if (state.HasPotential)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} already has revealed potential.";
                return;
            }

            if (success)
            {
                state.HasPotential = true;
                state.PotentialTier = consumable.PotentialTierOnSuccess;
                state.PotentialLines = RollPotentialLines(state.PotentialTier);
                _statusMessage = $"{ResolveItemName(selectedPart)} gained {state.PotentialTier} potential with {consumable.Name}.";
                return;
            }

            state.FailCount++;
            if (ResolveDestroyOnFailure(consumable))
            {
                DestroySelectedItem(slot, selectedPart, state, consumable.Name);
                return;
            }

            _statusMessage = $"{ResolveItemName(selectedPart)} failed to gain potential with {consumable.Name}.";
        }

        private void ApplyCube(CharacterPart selectedPart, UpgradeState state, EnhancementConsumable consumable)
        {
            if (!state.HasPotential)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} needs revealed potential before using {consumable.Name}.";
                return;
            }

            PotentialTier newTier = MaybeUpgradePotentialTier(state.PotentialTier);
            state.PotentialTier = newTier;
            state.PotentialLines = RollPotentialLines(newTier);
            _statusMessage = $"{ResolveItemName(selectedPart)} rerolled to {newTier} potential with {consumable.Name}.";
        }

        private float ResolveSuccessRate(EnhancementConsumable consumable, UpgradeState state)
        {
            if (consumable == null)
            {
                return 0f;
            }

            if (!consumable.Definition.UsesTieredSuccessRate)
            {
                return consumable.Definition.FlatSuccessRate;
            }

            int nextSuccessCount = state.SuccessCount + consumable.SuccessCountGain;
            if (consumable.Definition.IsAdvancedFamily)
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
            EnhancementConsumable consumable = ResolveConsumable(state);
            bool canApply = consumable != null &&
                            consumable switch
                            {
                                { EffectType: ConsumableEffectType.Enhancement } => state.RemainingSlots > 0 && state.RemainingSlots >= consumable.SuccessCountGain,
                                { EffectType: ConsumableEffectType.PotentialScroll } => !state.HasPotential,
                                { EffectType: ConsumableEffectType.Cube } => state.HasPotential,
                                _ => false
                            };
            _startButton?.SetEnabled(canApply);
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
                if (GetConsumableCount(preferredItemId) > 0 &&
                    TryGetConsumableDefinition(preferredItemId, out EnhancementConsumableDefinition preferredDefinition))
                {
                    return new EnhancementConsumable(preferredDefinition);
                }

                _preferredConsumableItemId = null;
            }

            int normalCount = GetTotalConsumableCount(EquipEnhancementScrollId, AlternateEquipEnhancementScrollId);
            int advancedCount = GetTotalConsumableCount(AdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId2);
            if (advancedCount > 0 && (state.SuccessCount >= 5 || normalCount == 0))
            {
                return GetFirstAvailableConsumable(state, AdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId2);
            }

            if (normalCount > 0)
            {
                return GetFirstAvailableConsumable(state, EquipEnhancementScrollId, AlternateEquipEnhancementScrollId);
            }

            if (advancedCount > 0)
            {
                return GetFirstAvailableConsumable(state, AdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId2);
            }

            EnhancementConsumable starConsumable = GetFirstAvailableConsumable(
                state,
                FiveStarEnhancementScrollId,
                FourStarEnhancementScrollId,
                ThreeStarEnhancementScrollId,
                TwoStarEnhancementScrollId);
            if (starConsumable != null)
            {
                return starConsumable;
            }

            return null;
        }

        private bool ResolveDestroyOnFailure(EnhancementConsumable consumable)
        {
            return consumable?.Definition.DestroysOnFailure ?? false;
        }

        private int GetConsumableCount(int itemId)
        {
            if (_inventory == null || !TryGetConsumableDefinition(itemId, out EnhancementConsumableDefinition definition))
            {
                return 0;
            }

            return _inventory.GetItemCount(definition.InventoryType, itemId);
        }

        private int GetTotalConsumableCount(params int[] itemIds)
        {
            int total = 0;
            for (int i = 0; i < itemIds.Length; i++)
            {
                total += GetConsumableCount(itemIds[i]);
            }

            return total;
        }

        private static string ResolveConsumableName(int itemId)
        {
            return HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo) &&
                   !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : (TryGetConsumableDefinition(itemId, out EnhancementConsumableDefinition definition)
                    ? definition.Name
                    : $"Item #{itemId}");
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
            if (!_upgradeStates.TryGetValue(slot, out UpgradeState state) ||
                state.ItemId != part?.ItemId)
            {
                state = new UpgradeState
                {
                    ItemId = part?.ItemId ?? 0,
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

        private string BuildConsumableSummary()
        {
            var segments = new List<string>();
            AppendConsumableSummary(segments, EquipEnhancementScrollId, AlternateEquipEnhancementScrollId, "ESS");
            AppendConsumableSummary(segments, AdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId2, "Adv");
            AppendConsumableSummary(segments, TwoStarEnhancementScrollId, "2-Star");
            AppendConsumableSummary(segments, ThreeStarEnhancementScrollId, "3-Star");
            AppendConsumableSummary(segments, FourStarEnhancementScrollId, "4-Star");
            AppendConsumableSummary(segments, FiveStarEnhancementScrollId, "5-Star");
            AppendConsumableSummary(segments, PotentialScrollId, "Pot");
            AppendConsumableSummary(segments, AdvancedPotentialScrollId, "Adv Pot");
            AppendConsumableSummary(segments, SpecialPotentialScrollId, "Spec Pot");
            AppendConsumableSummary(segments, MiracleCubeId, "Cube");
            return segments.Count > 0 ? string.Join(" / ", segments) : "No stock";
        }

        private void AppendConsumableSummary(List<string> segments, int itemId, string label)
        {
            int count = GetConsumableCount(itemId);
            if (count > 0)
            {
                segments.Add($"{label} {count}");
            }
        }

        private void AppendConsumableSummary(List<string> segments, int primaryItemId, int alternateItemId, string label)
        {
            AppendConsumableSummary(segments, new[] { primaryItemId, alternateItemId }, label);
        }

        private void AppendConsumableSummary(List<string> segments, int primaryItemId, int alternateItemId, int alternateItemId2, string label)
        {
            AppendConsumableSummary(segments, new[] { primaryItemId, alternateItemId, alternateItemId2 }, label);
        }

        private void AppendConsumableSummary(List<string> segments, IReadOnlyList<int> itemIds, string label)
        {
            int count = 0;
            for (int i = 0; i < itemIds.Count; i++)
            {
                count += GetConsumableCount(itemIds[i]);
            }

            if (count > 0)
            {
                segments.Add($"{label} {count}");
            }
        }

        private EnhancementConsumable GetFirstAvailableConsumable(UpgradeState state, params int[] itemIds)
        {
            for (int i = 0; i < itemIds.Length; i++)
            {
                int itemId = itemIds[i];
                if (GetConsumableCount(itemId) <= 0 ||
                    !TryGetConsumableDefinition(itemId, out EnhancementConsumableDefinition definition) ||
                    definition.SuccessCountGain > state.RemainingSlots)
                {
                    continue;
                }

                return new EnhancementConsumable(definition);
            }

            return null;
        }

        private static bool TryGetConsumableDefinition(int itemId, out EnhancementConsumableDefinition definition)
        {
            return ConsumableDefinitions.TryGetValue(itemId, out definition);
        }

        private PotentialTier MaybeUpgradePotentialTier(PotentialTier currentTier)
        {
            double roll = _random.NextDouble();
            return currentTier switch
            {
                PotentialTier.Rare when roll < 0.12d => PotentialTier.Epic,
                PotentialTier.Epic when roll < 0.04d => PotentialTier.Unique,
                _ => currentTier
            };
        }

        private string[] RollPotentialLines(PotentialTier tier)
        {
            string[][] pool = tier switch
            {
                PotentialTier.Unique => new[]
                {
                    new[] { "STR +6%", "DEX +6%", "INT +6%", "LUK +6%", "ATT +6", "Boss +15%" },
                    new[] { "HP +9%", "MP +9%", "Critical +6%", "All Stat +3%", "Damage +6%" },
                    new[] { "Speed +6", "Jump +6", "Accuracy +12", "Avoid +12", "Defense +12" }
                },
                PotentialTier.Epic => new[]
                {
                    new[] { "STR +3%", "DEX +3%", "INT +3%", "LUK +3%", "ATT +3", "All Stat +2%" },
                    new[] { "HP +6%", "MP +6%", "Critical +4%", "Damage +4%", "Accuracy +8" },
                    new[] { "Speed +4", "Jump +4", "Avoid +8", "Defense +8", "M.ATT +3" }
                },
                _ => new[]
                {
                    new[] { "STR +2%", "DEX +2%", "INT +2%", "LUK +2%", "ATT +2", "M.ATT +2" },
                    new[] { "HP +4%", "MP +4%", "Defense +5", "Accuracy +5", "Avoid +5" },
                    new[] { "Speed +2", "Jump +2", "All Stat +1%", "Damage +2%", "Critical +2%" }
                }
            };

            var lines = new string[3];
            for (int i = 0; i < lines.Length; i++)
            {
                string[] candidates = pool[Math.Min(i, pool.Length - 1)];
                lines[i] = candidates[_random.Next(candidates.Length)];
            }

            return lines;
        }

        private sealed class UpgradeState
        {
            public int ItemId { get; set; }
            public int TotalSlots { get; set; }
            public int RemainingSlots { get; set; }
            public int Attempts { get; set; }
            public int SuccessCount { get; set; }
            public int FailCount { get; set; }
            public int AttackBonus { get; set; }
            public int DefenseBonus { get; set; }
            public bool HasPotential { get; set; }
            public PotentialTier PotentialTier { get; set; }
            public string[] PotentialLines { get; set; } = Array.Empty<string>();
        }

        private enum ConsumableEffectType
        {
            Enhancement,
            PotentialScroll,
            Cube
        }

        private enum PotentialTier
        {
            Rare,
            Epic,
            Unique
        }

        private readonly struct EnhancementConsumableDefinition
        {
            public EnhancementConsumableDefinition(
                int itemId,
                string name,
                int successCountGain,
                bool usesTieredSuccessRate,
                bool isAdvancedFamily,
                float flatSuccessRate,
                InventoryType inventoryType = InventoryType.USE,
                ConsumableEffectType effectType = ConsumableEffectType.Enhancement,
                PotentialTier potentialTierOnSuccess = PotentialTier.Rare,
                bool destroysOnFailure = true)
            {
                ItemId = itemId;
                Name = name;
                SuccessCountGain = successCountGain;
                UsesTieredSuccessRate = usesTieredSuccessRate;
                IsAdvancedFamily = isAdvancedFamily;
                FlatSuccessRate = flatSuccessRate;
                InventoryType = inventoryType;
                EffectType = effectType;
                PotentialTierOnSuccess = potentialTierOnSuccess;
                DestroysOnFailure = destroysOnFailure;
            }

            public int ItemId { get; }
            public string Name { get; }
            public int SuccessCountGain { get; }
            public bool UsesTieredSuccessRate { get; }
            public bool IsAdvancedFamily { get; }
            public float FlatSuccessRate { get; }
            public InventoryType InventoryType { get; }
            public ConsumableEffectType EffectType { get; }
            public PotentialTier PotentialTierOnSuccess { get; }
            public bool DestroysOnFailure { get; }
        }

        private sealed class EnhancementConsumable
        {
            public EnhancementConsumable(EnhancementConsumableDefinition definition)
            {
                Definition = definition;
            }

            public EnhancementConsumableDefinition Definition { get; }
            public int ItemId => Definition.ItemId;
            public string Name => ResolveConsumableName(Definition.ItemId);
            public int SuccessCountGain => Definition.SuccessCountGain;
            public InventoryType InventoryType => Definition.InventoryType;
            public ConsumableEffectType EffectType => Definition.EffectType;
            public PotentialTier PotentialTierOnSuccess => Definition.PotentialTierOnSuccess;
        }
    }
}
