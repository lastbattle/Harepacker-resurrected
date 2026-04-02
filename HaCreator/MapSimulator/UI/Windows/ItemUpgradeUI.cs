using MapleLib;
using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
        private const int SpecialPotentialScrollIdLegacy = 2049402;
        private const int SpecialPotentialScrollId = 2049406;
        private const int AdvancedPotentialScrollId2 = 2049407;
        private const int PotentialScrollId2 = 2049408;
        private const int CarvedGoldenSealId = 2049500;
        private const int CarvedSilverSealId = 2049501;
        private const int EpicPotentialScrollId = 2049700;
        private const int EpicPotentialScrollId2 = 2049701;
        private const int EpicPotentialScrollId3 = 2049702;
        private const int EpicPotentialScrollId4 = 2049703;
        private const int MiracleCubeId = 5062000;
        private const int PremiumMiracleCubeId = 5062001;
        private const int SuperMiracleCubeId = 5062002;
        private const int RevolutionaryMiracleCubeId = 5062003;
        private const int GoldenMiracleCubeId = 5062004;
        private const int EnlighteningMiracleCubeId = 5062005;
        private const int MapleMiracleCubeId = 5062100;
        private const int MiracleCubeFragmentId = 2430112;
        private const int SuperMiracleCubeFragmentId = 2430481;
        private const int EnlighteningMiracleCubeShardId = 2430759;
        private const int UretesTimeLabId = 5534000;
        private const int VegasSpellTenPercentId = 5610000;
        private const int VegasSpellSixtyPercentId = 5610001;
        private const int ViciousHammerId = 5570000;
        private static readonly int[] CleanSlateScrollIds =
        {
            2049000, 2049001, 2049002, 2049003, 2049004, 2049005, 2049006,
            2049007, 2049008, 2049009, 2049010, 2049011
        };
        private static readonly int[] InnocenceScrollIds = { 2049600, 2049601, 2049604 };
        private static readonly int[] GoldenHammerIds = { 2470000, 2470001, 2470002 };
        private static readonly int[] HorntailNecklaceIds = { 1122000, 1122001, 1122002, 1122003 };

        private readonly Random _random = new Random();
        private readonly Dictionary<EquipSlot, UpgradeState> _upgradeStates = new Dictionary<EquipSlot, UpgradeState>();
        private static readonly Dictionary<int, IReadOnlyCollection<int>> ConsumableRequirementCache = new Dictionary<int, IReadOnlyCollection<int>>();
        private static readonly Dictionary<int, string> ConsumableOwnerPathCache = new Dictionary<int, string>();
        private static readonly Dictionary<int, EnhancementConsumableDefinition> DynamicConsumableDefinitionCache = new Dictionary<int, EnhancementConsumableDefinition>();
        private static readonly Dictionary<int, VegaModifierProfile> VegaModifierProfileCache = new Dictionary<int, VegaModifierProfile>();
        private static readonly Dictionary<int, VegaCompatibleScrollProfile> VegaCompatibleScrollProfileCache = new Dictionary<int, VegaCompatibleScrollProfile>();
        private static readonly Dictionary<int, IReadOnlyCollection<EquipSlot>> ScrollTargetSlotCache = new Dictionary<int, IReadOnlyCollection<EquipSlot>>();
        private static readonly Regex PercentRateRegex = new Regex(@"Success\s*rate\s*:\s*(\d+)\s*%", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex VegaModifierRegex = new Regex(@"enables\s+a\s+(\d+)\s*%\s+success\s+rate\s+on\s+a\s+(\d+)\s*%\s+scroll", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ScrollTargetRegex = new Regex(@"Scroll\s+for\s+(.+?)\s+for\s", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PercentChanceRegex = new Regex(@"(\d+)\s*%\s+chance", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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
                [AdvancedPotentialScrollId] = new(AdvancedPotentialScrollId, "Advanced Potential Scroll", 0, false, false, 0.9f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Epic, 1.0f),
                [PotentialScrollId] = new(PotentialScrollId, "Potential Scroll", 0, false, false, 0.7f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Rare, 1.0f),
                [SpecialPotentialScrollIdLegacy] = new(SpecialPotentialScrollIdLegacy, "Special Potential Scroll", 0, false, false, 1.0f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Epic, 0f),
                [SpecialPotentialScrollId] = new(SpecialPotentialScrollId, "Special Potential Scroll", 0, false, false, 1.0f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Epic, 0f),
                [AdvancedPotentialScrollId2] = new(AdvancedPotentialScrollId2, "Advanced Potential Scroll", 0, false, false, 0.9f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Epic, 1.0f),
                [PotentialScrollId2] = new(PotentialScrollId2, "Potential Scroll", 0, false, false, 0.7f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Rare, 1.0f),
                [EpicPotentialScrollId] = new(EpicPotentialScrollId, "Epic Potential Scroll 100%", 0, false, false, 1.0f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Epic, 0f),
                [EpicPotentialScrollId2] = new(EpicPotentialScrollId2, "Epic Potential Scroll 80%", 0, false, false, 0.8f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Epic, 0.2f),
                [EpicPotentialScrollId3] = new(EpicPotentialScrollId3, "Epic Potential Scroll 100%", 0, false, false, 1.0f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Epic, 0f),
                [EpicPotentialScrollId4] = new(EpicPotentialScrollId4, "Epic Potential Scroll 100%", 0, false, false, 1.0f, InventoryType.USE, ConsumableEffectType.PotentialScroll, PotentialTier.Epic, 0f),
                [CarvedGoldenSealId] = new(CarvedGoldenSealId, "Carved Golden Seal", 0, false, false, 0.8f, InventoryType.USE, ConsumableEffectType.PotentialStamp, PotentialTier.Rare, 0f),
                [CarvedSilverSealId] = new(CarvedSilverSealId, "Carved Silver Seal", 0, false, false, 0.5f, InventoryType.USE, ConsumableEffectType.PotentialStamp, PotentialTier.Rare, 0f),
                [UretesTimeLabId] = new(UretesTimeLabId, "Urete's Time Lab", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.PotentialScroll, PotentialTier.Rare, 0f),
                [MiracleCubeId] = new(MiracleCubeId, "Miracle Cube", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.Cube, PotentialTier.Rare, 0f, CubeBehavior.Miracle),
                [PremiumMiracleCubeId] = new(PremiumMiracleCubeId, "Premium Miracle Cube", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.Cube, PotentialTier.Rare, 0f, CubeBehavior.Premium),
                [SuperMiracleCubeId] = new(SuperMiracleCubeId, "Super Miracle Cube", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.Cube, PotentialTier.Rare, 0f, CubeBehavior.Super),
                [RevolutionaryMiracleCubeId] = new(RevolutionaryMiracleCubeId, "Revolutionary Miracle Cube", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.Cube, PotentialTier.Rare, 0f, CubeBehavior.Revolutionary, ModifierBehavior.None, 0f, SuperMiracleCubeFragmentId),
                [GoldenMiracleCubeId] = new(GoldenMiracleCubeId, "Golden Miracle Cube", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.Cube, PotentialTier.Rare, 0f, CubeBehavior.Golden, ModifierBehavior.None, 0f, MiracleCubeFragmentId),
                // WZ exposes a matching shard item in String/Consume.img/2430759.
                [EnlighteningMiracleCubeId] = new(EnlighteningMiracleCubeId, "Enlightening Miracle Cube", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.Cube, PotentialTier.Rare, 0f, CubeBehavior.Enlightening, ModifierBehavior.None, 0f, EnlighteningMiracleCubeShardId),
                // WZ Item/Cash/0506.img/05062100 carries a req list and a dedicated MiracleCube_8th UI path.
                [MapleMiracleCubeId] = new(MapleMiracleCubeId, "Maple Miracle Cube", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.Cube, PotentialTier.Rare, 0f, CubeBehavior.Maple),
                [VegasSpellTenPercentId] = new(VegasSpellTenPercentId, "Vega's Spell(10%)", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.Modifier, PotentialTier.Rare, 0f, CubeBehavior.Miracle, ModifierBehavior.VegaTenPercent, 0.3f),
                [VegasSpellSixtyPercentId] = new(VegasSpellSixtyPercentId, "Vega's Spell(60%)", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.Modifier, PotentialTier.Rare, 0f, CubeBehavior.Miracle, ModifierBehavior.VegaSixtyPercent, 0.9f),
                // Item/Cash/0557.img/05570000 is the Vicious' Hammer cash variant described by String/Cash.img/5570000.
                [ViciousHammerId] = new(ViciousHammerId, "Vicious' Hammer", 0, false, false, 1.0f, InventoryType.CASH, ConsumableEffectType.Hammer, PotentialTier.Rare, 0f, CubeBehavior.Miracle, ModifierBehavior.None, 0f, 0, HammerBehavior.Vicious)
            };

        private Texture2D _backgroundOverlay;
        private Point _backgroundOverlayOffset;
        private Texture2D _headerOverlay;
        private Point _headerOverlayOffset;
        private Texture2D _gaugeBarTexture;
        private Texture2D _gaugeFillTexture;
        private Point _gaugeOffset;
        private readonly Dictionary<VisualThemeKind, WindowVisualTheme> _visualThemes = new Dictionary<VisualThemeKind, WindowVisualTheme>();
        private SpriteFont _font;
        private UIObject _startButton;
        private UIObject _cancelButton;
        private UIObject _prevButton;
        private UIObject _nextButton;
        private UIObject _themeActionButton;
        private UIObject _themeCancelButton;
        private CharacterBuild _characterBuild;
        private IInventoryRuntime _inventory;
        private int _selectedIndex;
        private int? _preferredConsumableItemId;
        private int? _preferredModifierItemId;
        private string _statusMessage = "Select equipment and begin enhancement.";
        private bool? _lastUpgradeSucceeded;
        private VisualThemeKind _activeThemeKind = VisualThemeKind.Enhancement;
        private WindowPresentationState _presentationState = WindowPresentationState.Idle;
        private int _presentationElapsedMs;
        private ItemUpgradeAttemptResult _presentationResult;
        private VisualThemeKind? _lockedThemeKind;

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

        public static bool IsVegaSpellConsumable(int itemId)
        {
            return itemId == VegasSpellTenPercentId || itemId == VegasSpellSixtyPercentId;
        }

        public static bool CanUpgrade(EquipSlot slot, CharacterPart part)
        {
            return part != null
                   && !part.IsCash
                   && ResolveDefaultSlotCount(slot, part) > 0;
        }

        public void PrepareConsumableSelection(int itemId)
        {
            ClearPresentationState();
            if (!TryGetConsumableDefinition(itemId, out EnhancementConsumableDefinition definition))
            {
                return;
            }

            if (definition.EffectType == ConsumableEffectType.Modifier)
            {
                _preferredModifierItemId = itemId;
                _statusMessage = $"{definition.Name} ready. Choose equipment and a compatible scroll.";
            }
            else
            {
                _preferredConsumableItemId = itemId;
                _statusMessage = $"{definition.Name} ready. Choose equipment to enhance.";
            }

            _lastUpgradeSucceeded = null;
            UpdateButtonStates();
        }

        public void PrepareNpcLaunch()
        {
            ClearPresentationState();
            _preferredConsumableItemId = null;
            _preferredModifierItemId = null;
            _lastUpgradeSucceeded = null;
            ClampSelection();
            _statusMessage = GetCandidates().Count > 0
                ? "Select equipment and begin enhancement."
                : "No equipped item can be upgraded.";
            UpdateButtonStates();
        }

        public void PrepareEquipmentSelection(EquipSlot slot)
        {
            ClearPresentationState();
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

        public void RegisterVisualTheme(VisualThemeKind themeKind, WindowVisualTheme theme)
        {
            if (theme == null)
            {
                return;
            }

            if (theme.ActionButton != null)
            {
                theme.ActionButton.ButtonVisible = false;
                AddButton(theme.ActionButton);
                theme.ActionButton.ButtonClickReleased += _ => HandleThemeActionButtonClick();
            }

            if (theme.CancelButton != null)
            {
                theme.CancelButton.ButtonVisible = false;
                AddButton(theme.CancelButton);
                theme.CancelButton.ButtonClickReleased += _ => Hide();
            }

            _visualThemes[themeKind] = theme;
            if (_visualThemes.Count == 1 || _activeThemeKind == themeKind)
            {
                ApplyVisualTheme(themeKind);
            }
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
            RefreshVisualTheme();

            if (_presentationState == WindowPresentationState.Casting)
            {
                _presentationElapsedMs += (int)gameTime.ElapsedGameTime.TotalMilliseconds;
                if (_presentationElapsedMs >= ResolveThemeEffectDuration())
                {
                    _presentationState = WindowPresentationState.Result;
                    _presentationElapsedMs = 0;
                    _statusMessage = _presentationResult.StatusMessage;
                }
            }

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
            EnhancementConsumable consumable = ResolveConsumable(state, selectedPart);
            EnhancementConsumable modifier = ResolveModifier(consumable);

            DrawSelectedItem(sprite, windowX + ItemIconX, windowY + ItemIconY, selectedPart);
            DrawThemeEffect(sprite, windowX, windowY);

            string itemName = string.IsNullOrWhiteSpace(selectedPart.Name) ? $"Equip {selectedPart.ItemId}" : selectedPart.Name;
            DrawShadowedText(sprite, itemName, new Vector2(windowX + DetailTextX, windowY + 40), new Color(255, 220, 120));
            DrawShadowedText(sprite, $"{ResolveSlotLabel(selection.Key)}  [{_selectedIndex + 1}/{candidates.Count}]", new Vector2(windowX + DetailTextX, windowY + 57), Color.White);

            Color statColor = state.RemainingSlots > 0 ? new Color(191, 255, 191) : new Color(255, 180, 180);
            DrawShadowedText(sprite, $"Slots: {state.RemainingSlots}/{state.TotalSlots}", new Vector2(windowX + DetailTextX, windowY + DetailTextY), statColor);
            DrawShadowedText(sprite, $"Success: {state.SuccessCount}   Fail: {state.FailCount}", new Vector2(windowX + DetailTextX, windowY + DetailTextY + DetailLineGap), Color.White);
            DrawShadowedText(sprite, $"Bonus: ATT +{state.AttackBonus}  DEF +{state.DefenseBonus}", new Vector2(windowX + DetailTextX, windowY + DetailTextY + (DetailLineGap * 2)), new Color(181, 224, 255));
            string potentialText = state.HasPotential
                ? $"Potential: {state.PotentialTier} ({state.PotentialLineCount})  {string.Join(" / ", EnumerateVisiblePotentialLines(state))}"
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

            if (modifier != null)
            {
                scrollText += $" + {modifier.Name} x{GetConsumableCount(modifier.ItemId)}";
            }

            DrawShadowedText(sprite, scrollText, new Vector2(windowX + DetailTextX, windowY + DetailTextY + (DetailLineGap * 4)), new Color(255, 232, 173));
            DrawShadowedText(
                sprite,
                $"Recovery: {state.RecoveredSlotCount}  Golden/Vicious: {state.GoldenHammerCount}/{state.ViciousHammerCount}",
                new Vector2(windowX + DetailTextX, windowY + DetailTextY + (DetailLineGap * 5)),
                new Color(255, 214, 170));

            Color statusColor = _lastUpgradeSucceeded switch
            {
                _ when _presentationState == WindowPresentationState.Casting => new Color(255, 232, 150),
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
            if (_presentationState == WindowPresentationState.Casting)
            {
                return;
            }

            if (_presentationState == WindowPresentationState.Result)
            {
                ResetPresentationState();
                return;
            }

            EnhancementConsumable preparedConsumable = TryResolveCurrentConsumable(out _) ? ResolveCurrentConsumable() : null;
            VisualThemeKind presentationThemeKind = preparedConsumable != null
                ? ResolveVisualThemeKind(preparedConsumable.Definition)
                : ResolveCurrentVisualThemeKind();

            ItemUpgradeAttemptResult result = TryApplyPreparedUpgrade();
            if (preparedConsumable?.EffectType == ConsumableEffectType.Cube &&
                result.Success.HasValue &&
                TryStartThemePresentation(presentationThemeKind, result))
            {
                return;
            }
        }

        public ItemUpgradeAttemptResult TryApplyPreparedUpgrade()
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                _statusMessage = "No equipped item can be upgraded.";
                _lastUpgradeSucceeded = null;
                return new ItemUpgradeAttemptResult(null, _statusMessage, 0);
            }

            if (_inventory == null)
            {
                _statusMessage = "Inventory runtime is unavailable.";
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, 0);
            }

            KeyValuePair<EquipSlot, CharacterPart> selection = candidates[_selectedIndex];
            CharacterPart selectedPart = selection.Value;
            UpgradeState state = GetOrCreateState(selection.Key, selectedPart);
            EnhancementConsumable consumable = ResolveConsumable(state, selectedPart);
            if (consumable == null)
            {
                _statusMessage = _preferredModifierItemId.HasValue
                    ? "No compatible enhancement scroll is available for the selected Vega modifier."
                    : "No enhancement scrolls are available in inventory.";
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, 0);
            }

            EnhancementConsumable modifier = ResolveModifier(consumable);
            if (_preferredModifierItemId.HasValue && modifier == null)
            {
                _statusMessage = BuildModifierCompatibilityMessage(consumable);
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
            }

            if (consumable.EffectType == ConsumableEffectType.Enhancement &&
                state.RemainingSlots <= 0)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} has no upgrade slots left.";
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
            }

            if (consumable.EffectType == ConsumableEffectType.Enhancement &&
                state.RemainingSlots < consumable.SuccessCountGain)
            {
                _statusMessage = $"{consumable.Name} needs {consumable.SuccessCountGain} open slots on {ResolveItemName(selectedPart)}.";
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
            }

            if (!TryGetConsumableCompatibilityBlockReason(consumable, selectedPart, out string compatibilityBlockReason))
            {
                _statusMessage = compatibilityBlockReason;
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
            }

            if (consumable.EffectType == ConsumableEffectType.PotentialScroll && state.HasPotential)
            {
                if (consumable.PotentialTierOnSuccess != PotentialTier.Epic || state.PotentialTier > PotentialTier.Rare)
                {
                    _statusMessage = $"{ResolveItemName(selectedPart)} already has revealed potential.";
                    _lastUpgradeSucceeded = false;
                    return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
                }
            }

            if (consumable.EffectType == ConsumableEffectType.PotentialStamp)
            {
                if (!state.HasPotential)
                {
                    _statusMessage = $"{ResolveItemName(selectedPart)} needs potential before using {consumable.Name}.";
                    _lastUpgradeSucceeded = false;
                    return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
                }

                if (state.PotentialLineCount >= UpgradeState.MaxPotentialLines)
                {
                    _statusMessage = $"{ResolveItemName(selectedPart)} already has {UpgradeState.MaxPotentialLines} potential lines.";
                    _lastUpgradeSucceeded = false;
                    return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
                }

                if (state.PotentialLineCount > 2)
                {
                    _statusMessage = $"{consumable.Name} only applies when {ResolveItemName(selectedPart)} has two potential lines or fewer.";
                    _lastUpgradeSucceeded = false;
                    return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
                }
            }

            if (consumable.EffectType == ConsumableEffectType.PotentialScroll &&
                consumable.PotentialTierOnSuccess == PotentialTier.Epic &&
                state.HasPotential &&
                state.PotentialTier > PotentialTier.Rare)
            {
                _statusMessage = $"{consumable.Name} only applies to equipment that is Rare or below.";
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
            }

            if (consumable.EffectType == ConsumableEffectType.Cube && !state.HasPotential)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} needs revealed potential before using {consumable.Name}.";
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
            }

            if (consumable.EffectType == ConsumableEffectType.Cube &&
                consumable.CubeBehavior == CubeBehavior.Golden &&
                state.PotentialTier > PotentialTier.Unique)
            {
                _statusMessage = $"{consumable.Name} only applies to Rare through Unique equipment.";
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
            }

            if (consumable.EffectType == ConsumableEffectType.SlotRecovery &&
                state.RemainingSlots >= state.TotalSlots)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} has no lost upgrade slots to recover.";
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
            }

            if (consumable.EffectType == ConsumableEffectType.Hammer &&
                !TryGetHammerBlockReason(consumable, selectedPart, state, out string hammerBlockReason))
            {
                _statusMessage = hammerBlockReason;
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
            }

            if (TryGetCubeRewardBlockReason(consumable, out string cubeRewardBlockReason))
            {
                _statusMessage = cubeRewardBlockReason;
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
            }

            if (!_inventory.TryConsumeItem(consumable.InventoryType, consumable.ItemId, 1))
            {
                _statusMessage = $"{consumable.Name} could not be consumed.";
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId);
            }

            if (modifier != null && !_inventory.TryConsumeItem(modifier.InventoryType, modifier.ItemId, 1))
            {
                _statusMessage = $"{modifier.Name} could not be consumed.";
                _lastUpgradeSucceeded = false;
                return new ItemUpgradeAttemptResult(false, _statusMessage, consumable.ItemId, modifier.ItemId);
            }

            bool success = _random.NextDouble() < ResolveSuccessRate(consumable, state, modifier);
            state.Attempts++;

            switch (consumable.EffectType)
            {
                case ConsumableEffectType.SlotRecovery:
                    ApplySlotRecovery(selectedPart, state, consumable, success);
                    break;
                case ConsumableEffectType.Reset:
                    ApplyResetScroll(selection.Key, selectedPart, state, consumable, success);
                    break;
                case ConsumableEffectType.PotentialScroll:
                    ApplyPotentialScroll(selection.Key, selectedPart, state, consumable, success);
                    break;
                case ConsumableEffectType.PotentialStamp:
                    ApplyPotentialStamp(selectedPart, state, consumable, success);
                    break;
                case ConsumableEffectType.Cube:
                    ApplyCube(selectedPart, state, consumable);
                    success = true;
                    break;
                case ConsumableEffectType.Hammer:
                    ApplyHammer(selectedPart, state, consumable, success);
                    break;
                default:
                    ApplyEnhancementScroll(selection.Key, selectedPart, state, consumable, modifier, success);
                    break;
            }

            if (_characterBuild?.Equipment != null &&
                _characterBuild.Equipment.TryGetValue(selection.Key, out CharacterPart currentPart) &&
                ReferenceEquals(currentPart, selectedPart))
            {
                SyncStateToPart(currentPart, state);
            }

            _lastUpgradeSucceeded = success;
            ClampSelection();
            _preferredModifierItemId = null;
            return new ItemUpgradeAttemptResult(success, _statusMessage, consumable.ItemId, modifier?.ItemId ?? 0);
        }

        private void ApplyUpgradeBonus(EquipSlot slot, CharacterPart selectedPart, UpgradeState state, int successCountGain)
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
                    if (selectedPart != null)
                    {
                        selectedPart.BonusWeaponAttack += 2 * successCountGain;
                    }
                    break;
                case EquipSlot.Glove:
                    state.AttackBonus += successCountGain;
                    _characterBuild.Attack += successCountGain;
                    if (selectedPart != null)
                    {
                        selectedPart.BonusWeaponAttack += successCountGain;
                    }
                    break;
                default:
                    state.DefenseBonus += successCountGain;
                    _characterBuild.Defense += successCountGain;
                    if (selectedPart != null)
                    {
                        selectedPart.BonusWeaponDefense += successCountGain;
                    }
                    break;
            }
        }

        private void ApplyEnhancementScroll(EquipSlot slot, CharacterPart selectedPart, UpgradeState state, EnhancementConsumable consumable, EnhancementConsumable modifier, bool success)
        {
            string modifierSuffix = modifier != null ? $" with {modifier.Name}" : string.Empty;
            if (success)
            {
                state.RemainingSlots = Math.Max(0, state.RemainingSlots - consumable.SuccessCountGain);
                state.SuccessCount += consumable.SuccessCountGain;
                ApplyUpgradeBonus(slot, selectedPart, state, consumable.SuccessCountGain);
                _statusMessage = $"{ResolveItemName(selectedPart)} gained {consumable.SuccessCountGain} enhancement" +
                                 (consumable.SuccessCountGain == 1 ? string.Empty : "s") +
                                 $" with {consumable.Name}{modifierSuffix}.";
                return;
            }

            state.FailCount++;
            if (ResolveDestroyOnFailure(consumable))
            {
                DestroySelectedItem(slot, selectedPart, state, consumable.Name);
                return;
            }

            state.RemainingSlots = Math.Max(0, state.RemainingSlots - 1);
            _statusMessage = $"{ResolveItemName(selectedPart)} failed with {consumable.Name}{modifierSuffix}. A slot was consumed.";
        }

        private void ApplyPotentialScroll(EquipSlot slot, CharacterPart selectedPart, UpgradeState state, EnhancementConsumable consumable, bool success)
        {
            if (state.HasPotential && (consumable.PotentialTierOnSuccess != PotentialTier.Epic || state.PotentialTier > PotentialTier.Rare))
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} already has revealed potential.";
                return;
            }

            if (success)
            {
                state.HasPotential = true;
                state.PotentialTier = consumable.PotentialTierOnSuccess;
                state.PotentialLineCount = Math.Max(2, state.PotentialLineCount);
                state.PotentialLines = RollPotentialLines(state.PotentialTier, state.PotentialLineCount);
                _statusMessage = $"{ResolveItemName(selectedPart)} gained {state.PotentialTier} potential with {consumable.Name}.";
                return;
            }

            state.FailCount++;
            if (ResolveDestroyOnFailure(consumable))
            {
                DestroySelectedItem(slot, selectedPart, state, consumable.Name);
                return;
            }

            _statusMessage = consumable.Definition.DestroyChanceOnFailure > 0f &&
                             consumable.Definition.DestroyChanceOnFailure < 1.0f
                ? $"{ResolveItemName(selectedPart)} failed to gain potential with {consumable.Name}, but it survived."
                : $"{ResolveItemName(selectedPart)} failed to gain potential with {consumable.Name}.";
        }

        private void ApplyPotentialStamp(CharacterPart selectedPart, UpgradeState state, EnhancementConsumable consumable, bool success)
        {
            if (!state.HasPotential)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} needs potential before using {consumable.Name}.";
                return;
            }

            if (state.PotentialLineCount >= UpgradeState.MaxPotentialLines)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} already has {UpgradeState.MaxPotentialLines} potential lines.";
                return;
            }

            if (success)
            {
                string[] previousLines = CopyPotentialLines(state);
                state.PotentialLineCount = Math.Min(UpgradeState.MaxPotentialLines, state.PotentialLineCount + 1);
                state.PotentialLines = PreservePotentialLinesWithExtraLine(previousLines, state.PotentialTier, state.PotentialLineCount);
                _statusMessage = $"{ResolveItemName(selectedPart)} gained an extra potential line with {consumable.Name}.";
                return;
            }

            state.FailCount++;
            _statusMessage = $"{ResolveItemName(selectedPart)} failed to gain an extra potential line with {consumable.Name}.";
        }

        private void ApplySlotRecovery(CharacterPart selectedPart, UpgradeState state, EnhancementConsumable consumable, bool success)
        {
            if (state.RemainingSlots >= state.TotalSlots)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} has no lost upgrade slots to recover.";
                return;
            }

            if (success)
            {
                int recoveredSlots = Math.Min(consumable.SuccessCountGain, state.TotalSlots - state.RemainingSlots);
                state.RemainingSlots += recoveredSlots;
                state.RecoveredSlotCount += recoveredSlots;
                _statusMessage = $"{ResolveItemName(selectedPart)} recovered {recoveredSlots} upgrade slot" +
                                 (recoveredSlots == 1 ? string.Empty : "s") +
                                 $" with {consumable.Name}.";
                return;
            }

            state.FailCount++;
            if (ResolveDestroyOnFailure(consumable))
            {
                DestroySelectedItem(selectedPart?.Slot ?? EquipSlot.None, selectedPart, state, consumable.Name);
                return;
            }

            _statusMessage = $"{ResolveItemName(selectedPart)} failed to recover an upgrade slot with {consumable.Name}.";
        }

        private void ApplyResetScroll(EquipSlot slot, CharacterPart selectedPart, UpgradeState state, EnhancementConsumable consumable, bool success)
        {
            if (success)
            {
                ResetAppliedEnhancementState(slot, selectedPart, state);
                _statusMessage = $"{ResolveItemName(selectedPart)} was reset with {consumable.Name} while keeping its potential.";
                return;
            }

            state.FailCount++;
            if (ResolveDestroyOnFailure(consumable))
            {
                DestroySelectedItem(slot, selectedPart, state, consumable.Name);
                return;
            }

            _statusMessage = $"{ResolveItemName(selectedPart)} failed to reset with {consumable.Name}.";
        }

        private void ApplyHammer(CharacterPart selectedPart, UpgradeState state, EnhancementConsumable consumable, bool success)
        {
            if (!TryGetHammerBlockReason(consumable, selectedPart, state, out string hammerBlockReason))
            {
                _statusMessage = hammerBlockReason;
                return;
            }

            if (success)
            {
                state.TotalSlots++;
                state.RemainingSlots++;
                if (consumable.Definition.HammerBehavior == HammerBehavior.Vicious)
                {
                    state.ViciousHammerCount++;
                }
                else
                {
                    state.GoldenHammerCount++;
                }

                _statusMessage = $"{ResolveItemName(selectedPart)} gained an extra upgrade slot with {consumable.Name}.";
                return;
            }

            state.FailCount++;
            _statusMessage = $"{ResolveItemName(selectedPart)} failed to gain an extra slot with {consumable.Name}.";
        }

        private void ApplyCube(CharacterPart selectedPart, UpgradeState state, EnhancementConsumable consumable)
        {
            if (!state.HasPotential)
            {
                _statusMessage = $"{ResolveItemName(selectedPart)} needs revealed potential before using {consumable.Name}.";
                return;
            }

            PotentialTier previousTier = state.PotentialTier;
            PotentialTier newTier = MaybeUpgradePotentialTier(state.PotentialTier, consumable.CubeBehavior);
            state.PotentialTier = newTier;
            state.PotentialLineCount = ResolveCubePotentialLineCount(state.PotentialLineCount, consumable.CubeBehavior);
            state.PotentialLines = RollPotentialLines(newTier, state.PotentialLineCount);
            string baseMessage = newTier > previousTier
                ? $"{ResolveItemName(selectedPart)} ranked up to {newTier} potential with {consumable.Name}."
                : $"{ResolveItemName(selectedPart)} rerolled {state.PotentialLineCount} potential line(s) with {consumable.Name}.";

            if (TryGrantCubeReward(consumable, out int rewardItemId))
            {
                _statusMessage = $"{baseMessage} Obtained {ResolveConsumableName(rewardItemId)}.";
                return;
            }

            _statusMessage = baseMessage;
        }

        private float ResolveSuccessRate(EnhancementConsumable consumable, UpgradeState state, EnhancementConsumable modifier)
        {
            if (consumable == null)
            {
                return 0f;
            }

            if (modifier != null &&
                TryResolveVegaModifierProfile(modifier.ItemId, out VegaModifierProfile modifierProfile) &&
                TryResolveVegaCompatibleScrollProfile(consumable.ItemId, out VegaCompatibleScrollProfile scrollProfile) &&
                AreRatesEquivalent(scrollProfile.BaseSuccessRate, modifierProfile.RequiredBaseSuccessRate))
            {
                return modifierProfile.ModifiedSuccessRate;
            }

            if (!consumable.Definition.UsesTieredSuccessRate)
            {
                return modifier != null
                    ? modifier.Definition.ModifiedSuccessRate
                    : consumable.Definition.FlatSuccessRate;
            }

            int nextSuccessCount = state.SuccessCount + consumable.SuccessCountGain;
            float baseRate;
            if (consumable.Definition.IsAdvancedFamily)
            {
                baseRate = nextSuccessCount switch
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
            else
            {
                baseRate = nextSuccessCount switch
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

            if (modifier == null)
            {
                return baseRate;
            }

            return IsModifierCompatible(modifier, consumable)
                ? Math.Max(baseRate, modifier.Definition.ModifiedSuccessRate)
                : baseRate;
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
            RefreshVisualTheme();
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
            bool hasThemeButtons = _themeActionButton != null || _themeCancelButton != null;
            bool idle = _presentationState == WindowPresentationState.Idle;
            bool canCycle = idle && hasCandidates && candidates.Count > 1;

            if (_prevButton != null)
            {
                _prevButton.SetEnabled(canCycle);
                _prevButton.ButtonVisible = canCycle;
            }

            if (_nextButton != null)
            {
                _nextButton.SetEnabled(canCycle);
                _nextButton.ButtonVisible = canCycle;
            }

            if (_startButton != null)
            {
                _startButton.ButtonVisible = !hasThemeButtons;
            }

            if (_cancelButton != null)
            {
                _cancelButton.ButtonVisible = !hasThemeButtons;
                _cancelButton.SetEnabled(idle);
            }

            if (!hasCandidates)
            {
                _startButton?.SetEnabled(false);
                _themeActionButton?.SetEnabled(false);
                _themeCancelButton?.SetEnabled(false);
                return;
            }

            KeyValuePair<EquipSlot, CharacterPart> selection = candidates[_selectedIndex];
            UpgradeState state = GetOrCreateState(selection.Key, selection.Value);
            EnhancementConsumable consumable = ResolveConsumable(state, selection.Value);
            bool modifierReady = !_preferredModifierItemId.HasValue || ResolveModifier(consumable) != null;
            bool canApply = consumable != null &&
                            TryGetConsumableCompatibilityBlockReason(consumable, selection.Value, out _) &&
                            modifierReady &&
                            consumable switch
                            {
                                { EffectType: ConsumableEffectType.Enhancement } => state.RemainingSlots > 0 && state.RemainingSlots >= consumable.SuccessCountGain,
                                { EffectType: ConsumableEffectType.SlotRecovery } => state.RemainingSlots < state.TotalSlots,
                                { EffectType: ConsumableEffectType.Reset } => true,
                                { EffectType: ConsumableEffectType.PotentialScroll } => !state.HasPotential || (consumable.PotentialTierOnSuccess == PotentialTier.Epic && state.PotentialTier <= PotentialTier.Rare),
                                { EffectType: ConsumableEffectType.PotentialStamp } => state.HasPotential && state.PotentialLineCount < UpgradeState.MaxPotentialLines && state.PotentialLineCount <= 2,
                                { EffectType: ConsumableEffectType.Cube } => state.HasPotential &&
                                                                              (consumable.CubeBehavior != CubeBehavior.Golden || state.PotentialTier <= PotentialTier.Unique) &&
                                                                              !TryGetCubeRewardBlockReason(consumable, out _),
                                { EffectType: ConsumableEffectType.Hammer } => TryGetHammerBlockReason(consumable, selection.Value, state, out _),
                                _ => false
                            };
            _startButton?.SetEnabled(idle && canApply);

            if (_themeActionButton != null)
            {
                _themeActionButton.ButtonVisible = _presentationState != WindowPresentationState.Casting;
                _themeActionButton.SetEnabled((_presentationState == WindowPresentationState.Result) || (idle && canApply));
            }

            if (_themeCancelButton != null)
            {
                _themeCancelButton.ButtonVisible = _presentationState != WindowPresentationState.Casting;
                _themeCancelButton.SetEnabled(idle || _presentationState == WindowPresentationState.Result);
            }
        }

        private void RefreshVisualTheme()
        {
            ApplyVisualTheme(_lockedThemeKind ?? ResolveCurrentVisualThemeKind());
        }

        private VisualThemeKind ResolveCurrentVisualThemeKind()
        {
            if (_preferredConsumableItemId.HasValue &&
                TryGetConsumableDefinition(_preferredConsumableItemId.Value, out EnhancementConsumableDefinition preferredDefinition) &&
                preferredDefinition.EffectType != ConsumableEffectType.Modifier)
            {
                return ResolveVisualThemeKind(preferredDefinition);
            }

            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                return VisualThemeKind.Enhancement;
            }

            KeyValuePair<EquipSlot, CharacterPart> selection = candidates[_selectedIndex];
            UpgradeState state = GetOrCreateState(selection.Key, selection.Value);
            EnhancementConsumable consumable = ResolveConsumable(state, selection.Value);
            return consumable != null
                ? ResolveVisualThemeKind(consumable.Definition)
                : VisualThemeKind.Enhancement;
        }

        private VisualThemeKind ResolveVisualThemeKind(EnhancementConsumableDefinition definition)
        {
            return definition.EffectType switch
            {
                ConsumableEffectType.PotentialScroll => VisualThemeKind.Potential,
                ConsumableEffectType.PotentialStamp => VisualThemeKind.PotentialStamp,
                ConsumableEffectType.Cube => ResolveCubeVisualThemeKind(definition.ItemId),
                _ => VisualThemeKind.Enhancement
            };
        }

        private VisualThemeKind ResolveCubeVisualThemeKind(int itemId)
        {
            string ownerPath = ResolveConsumableOwnerPath(itemId);
            if (!string.IsNullOrWhiteSpace(ownerPath) &&
                ownerPath.IndexOf("MiracleCube_8th", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return VisualThemeKind.MapleMiracleCube;
            }

            if (!string.IsNullOrWhiteSpace(ownerPath) &&
                (ownerPath.IndexOf("HyperMiracleCube", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 ownerPath.IndexOf("MiracleCube_Master", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 ownerPath.IndexOf("MiracleCube_Amazing", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return VisualThemeKind.HyperMiracleCube;
            }

            return VisualThemeKind.MiracleCube;
        }

        private void ApplyVisualTheme(VisualThemeKind themeKind)
        {
            if (!_visualThemes.TryGetValue(themeKind, out WindowVisualTheme theme) &&
                !_visualThemes.TryGetValue(VisualThemeKind.Enhancement, out theme))
            {
                return;
            }

            _activeThemeKind = _visualThemes.ContainsKey(themeKind)
                ? themeKind
                : VisualThemeKind.Enhancement;
            Frame = theme.Frame;
            _backgroundOverlay = theme.BackgroundOverlay;
            _backgroundOverlayOffset = theme.BackgroundOverlayOffset;
            _headerOverlay = theme.HeaderOverlay;
            _headerOverlayOffset = theme.HeaderOverlayOffset;
            _gaugeBarTexture = theme.GaugeBarTexture;
            _gaugeFillTexture = theme.GaugeFillTexture;
            _gaugeOffset = theme.GaugeOffset;
            _themeActionButton = theme.ActionButton;
            _themeCancelButton = theme.CancelButton;
        }

        private bool TryResolveCurrentConsumable(out EnhancementConsumable consumable)
        {
            consumable = ResolveCurrentConsumable();
            return consumable != null;
        }

        private EnhancementConsumable ResolveCurrentConsumable()
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                return null;
            }

            KeyValuePair<EquipSlot, CharacterPart> selection = candidates[_selectedIndex];
            UpgradeState state = GetOrCreateState(selection.Key, selection.Value);
            return ResolveConsumable(state, selection.Value);
        }

        private void HandleThemeActionButtonClick()
        {
            if (_presentationState == WindowPresentationState.Result)
            {
                ResetPresentationState();
                return;
            }

            TryApplyUpgrade();
        }

        private bool TryStartThemePresentation(VisualThemeKind themeKind, ItemUpgradeAttemptResult result)
        {
            if (!_visualThemes.TryGetValue(themeKind, out WindowVisualTheme theme) ||
                theme.EffectFrames == null ||
                theme.EffectFrames.Count == 0)
            {
                return false;
            }

            _lockedThemeKind = themeKind;
            _presentationState = WindowPresentationState.Casting;
            _presentationElapsedMs = 0;
            _presentationResult = result;
            _statusMessage = $"Using {ResolveConsumableName(result.ConsumableItemId)}...";
            ApplyVisualTheme(themeKind);
            return true;
        }

        private void ResetPresentationState()
        {
            ClearPresentationState();
            _statusMessage = BuildReadyStatusMessage();
            RefreshVisualTheme();
            UpdateButtonStates();
        }

        private void ClearPresentationState()
        {
            _presentationState = WindowPresentationState.Idle;
            _presentationElapsedMs = 0;
            _presentationResult = default;
            _lockedThemeKind = null;
            _lastUpgradeSucceeded = null;
        }

        private string BuildReadyStatusMessage()
        {
            IReadOnlyList<KeyValuePair<EquipSlot, CharacterPart>> candidates = GetCandidates();
            if (candidates.Count == 0)
            {
                return "No equipped item can be upgraded.";
            }

            if (_preferredModifierItemId.HasValue &&
                TryGetConsumableDefinition(_preferredModifierItemId.Value, out EnhancementConsumableDefinition modifierDefinition) &&
                GetConsumableCount(_preferredModifierItemId.Value) > 0)
            {
                return $"{modifierDefinition.Name} ready. Choose equipment and a compatible scroll.";
            }

            if (_preferredConsumableItemId.HasValue &&
                TryGetConsumableDefinition(_preferredConsumableItemId.Value, out EnhancementConsumableDefinition consumableDefinition) &&
                GetConsumableCount(_preferredConsumableItemId.Value) > 0)
            {
                return $"{consumableDefinition.Name} ready. Choose equipment to enhance.";
            }

            return "Select equipment and begin enhancement.";
        }

        private void DrawThemeEffect(SpriteBatch sprite, int windowX, int windowY)
        {
            if (_presentationState != WindowPresentationState.Casting ||
                !_visualThemes.TryGetValue(_lockedThemeKind ?? _activeThemeKind, out WindowVisualTheme theme) ||
                theme.EffectFrames == null ||
                theme.EffectFrames.Count == 0)
            {
                return;
            }

            VegaSpellUI.VegaAnimationFrame frame = SelectThemeEffectFrame(theme.EffectFrames, _presentationElapsedMs);
            if (frame?.Texture == null)
            {
                return;
            }

            Vector2 position = new Vector2(windowX + frame.Offset.X, windowY + frame.Offset.Y);
            sprite.Draw(frame.Texture, position, Color.White);
        }

        private static VegaSpellUI.VegaAnimationFrame SelectThemeEffectFrame(IReadOnlyList<VegaSpellUI.VegaAnimationFrame> frames, int elapsedMs)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            int animationDuration = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                animationDuration += Math.Max(1, frames[i].DelayMs);
            }

            if (animationDuration <= 0)
            {
                return frames[0];
            }

            int localElapsed = Math.Max(0, elapsedMs) % animationDuration;
            int running = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                running += Math.Max(1, frames[i].DelayMs);
                if (localElapsed < running)
                {
                    return frames[i];
                }
            }

            return frames[^1];
        }

        private int ResolveThemeEffectDuration()
        {
            if (!_visualThemes.TryGetValue(_lockedThemeKind ?? _activeThemeKind, out WindowVisualTheme theme) ||
                theme.EffectFrames == null ||
                theme.EffectFrames.Count == 0)
            {
                return 0;
            }

            int duration = 0;
            for (int i = 0; i < theme.EffectFrames.Count; i++)
            {
                duration += Math.Max(1, theme.EffectFrames[i].DelayMs);
            }

            return Math.Max(duration, 1);
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

        private EnhancementConsumable ResolveConsumable(UpgradeState state, CharacterPart selectedPart)
        {
            if (_inventory == null)
            {
                return null;
            }

            EnhancementConsumable preparedModifierConsumable = null;
            if (_preferredModifierItemId.HasValue &&
                TryGetConsumableDefinition(_preferredModifierItemId.Value, out EnhancementConsumableDefinition modifierDefinition) &&
                modifierDefinition.EffectType == ConsumableEffectType.Modifier)
            {
                preparedModifierConsumable = new EnhancementConsumable(modifierDefinition);
                EnhancementConsumable compatibleConsumable = ResolveConsumableForModifier(state, selectedPart, modifierDefinition);
                if (compatibleConsumable != null)
                {
                    return compatibleConsumable;
                }
            }

            if (_preferredConsumableItemId.HasValue)
            {
                int preferredItemId = _preferredConsumableItemId.Value;
                if (GetConsumableCount(preferredItemId) > 0 &&
                    TryGetConsumableDefinition(preferredItemId, out EnhancementConsumableDefinition preferredDefinition) &&
                    preferredDefinition.EffectType != ConsumableEffectType.Modifier &&
                    IsConsumableCompatibleWithItem(preferredDefinition, selectedPart?.ItemId ?? 0) &&
                    (preparedModifierConsumable == null || IsModifierCompatible(preparedModifierConsumable, new EnhancementConsumable(preferredDefinition))))
                {
                    return new EnhancementConsumable(preferredDefinition);
                }

                _preferredConsumableItemId = null;
            }

            int normalCount = GetTotalConsumableCount(EquipEnhancementScrollId, AlternateEquipEnhancementScrollId);
            int advancedCount = GetTotalConsumableCount(AdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId2);
            if (advancedCount > 0 && (state.SuccessCount >= 5 || normalCount == 0))
            {
                return GetFirstAvailableConsumable(state, selectedPart, AdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId2);
            }

            if (normalCount > 0)
            {
                return GetFirstAvailableConsumable(state, selectedPart, EquipEnhancementScrollId, AlternateEquipEnhancementScrollId);
            }

            if (advancedCount > 0)
            {
                return GetFirstAvailableConsumable(state, selectedPart, AdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId2);
            }

            EnhancementConsumable starConsumable = GetFirstAvailableConsumable(
                state,
                selectedPart,
                FiveStarEnhancementScrollId,
                FourStarEnhancementScrollId,
                ThreeStarEnhancementScrollId,
                TwoStarEnhancementScrollId);
            if (starConsumable != null)
            {
                return starConsumable;
            }

            EnhancementConsumable slotRecoveryConsumable = GetFirstAvailableConsumable(state, selectedPart, CleanSlateScrollIds);
            if (slotRecoveryConsumable != null)
            {
                return slotRecoveryConsumable;
            }

            EnhancementConsumable innocenceConsumable = GetFirstAvailableConsumable(state, selectedPart, InnocenceScrollIds);
            if (innocenceConsumable != null)
            {
                return innocenceConsumable;
            }

            EnhancementConsumable hammerConsumable = GetFirstAvailableConsumable(state, selectedPart, GoldenHammerIds.Concat(new[] { ViciousHammerId }).ToArray());
            if (hammerConsumable != null)
            {
                return hammerConsumable;
            }

            EnhancementConsumable potentialStamp = GetFirstAvailableConsumable(state, selectedPart, CarvedGoldenSealId, CarvedSilverSealId);
            if (potentialStamp != null)
            {
                return potentialStamp;
            }

            EnhancementConsumable epicPotentialScroll = GetFirstAvailableConsumable(state, selectedPart, EpicPotentialScrollId, EpicPotentialScrollId2, EpicPotentialScrollId3, EpicPotentialScrollId4);
            if (epicPotentialScroll != null)
            {
                return epicPotentialScroll;
            }

            EnhancementConsumable potentialConsumable = GetFirstAvailableConsumable(
                state,
                selectedPart,
                SpecialPotentialScrollIdLegacy,
                SpecialPotentialScrollId,
                AdvancedPotentialScrollId,
                AdvancedPotentialScrollId2,
                PotentialScrollId,
                PotentialScrollId2,
                UretesTimeLabId);
            if (potentialConsumable != null)
            {
                return potentialConsumable;
            }

            EnhancementConsumable cubeConsumable = GetFirstAvailableConsumable(
                state,
                selectedPart,
                MapleMiracleCubeId,
                EnlighteningMiracleCubeId,
                SuperMiracleCubeId,
                PremiumMiracleCubeId,
                RevolutionaryMiracleCubeId,
                GoldenMiracleCubeId,
                MiracleCubeId);
            if (cubeConsumable != null)
            {
                return cubeConsumable;
            }

            return null;
        }

        public bool TryGetModifierPreview(EquipSlot slot, int modifierItemId, out ModifierPreview preview)
        {
            preview = default;
            if (_characterBuild?.Equipment == null ||
                !_characterBuild.Equipment.TryGetValue(slot, out CharacterPart part) ||
                !CanUpgrade(slot, part) ||
                !TryGetConsumableDefinition(modifierItemId, out EnhancementConsumableDefinition modifierDefinition) ||
                modifierDefinition.EffectType != ConsumableEffectType.Modifier)
            {
                return false;
            }

            UpgradeState state = GetOrCreateState(slot, part);
            EnhancementConsumable consumable = ResolveConsumableForModifier(state, part, modifierDefinition);
            if (consumable == null)
            {
                return false;
            }

            EnhancementConsumable modifier = new EnhancementConsumable(modifierDefinition);
            preview = new ModifierPreview(
                consumable.ItemId,
                consumable.Name,
                GetConsumableCount(consumable.ItemId),
                ResolveSuccessRate(consumable, state, null),
                ResolveSuccessRate(consumable, state, modifier));
            return true;
        }

        private EnhancementConsumable ResolveModifier(EnhancementConsumable consumable)
        {
            if (_inventory == null || !_preferredModifierItemId.HasValue || consumable == null)
            {
                return null;
            }

            int modifierItemId = _preferredModifierItemId.Value;
            if (GetConsumableCount(modifierItemId) <= 0 ||
                !TryGetConsumableDefinition(modifierItemId, out EnhancementConsumableDefinition definition) ||
                definition.EffectType != ConsumableEffectType.Modifier)
            {
                _preferredModifierItemId = null;
                return null;
            }

            EnhancementConsumable modifier = new EnhancementConsumable(definition);
            return IsModifierCompatible(modifier, consumable)
                ? modifier
                : null;
        }

        private static bool IsModifierCompatible(EnhancementConsumable modifier, EnhancementConsumable consumable)
        {
            if (modifier == null || consumable == null || consumable.EffectType != ConsumableEffectType.Enhancement)
            {
                return false;
            }

            if (TryResolveVegaModifierProfile(modifier.ItemId, out VegaModifierProfile modifierProfile))
            {
                return TryResolveVegaCompatibleScrollProfile(consumable.ItemId, out VegaCompatibleScrollProfile scrollProfile)
                    && AreRatesEquivalent(scrollProfile.BaseSuccessRate, modifierProfile.RequiredBaseSuccessRate);
            }

            return modifier.Definition.ModifierBehavior switch
            {
                ModifierBehavior.VegaTenPercent => consumable.Definition.UsesTieredSuccessRate && consumable.Definition.IsAdvancedFamily,
                ModifierBehavior.VegaSixtyPercent => consumable.Definition.UsesTieredSuccessRate && !consumable.Definition.IsAdvancedFamily,
                _ => false
            };
        }

        private EnhancementConsumable ResolveConsumableForModifier(UpgradeState state, CharacterPart selectedPart, EnhancementConsumableDefinition modifierDefinition)
        {
            if (_inventory != null)
            {
                EnhancementConsumable modifier = new EnhancementConsumable(modifierDefinition);
                foreach (InventoryType inventoryType in new[] { InventoryType.USE, InventoryType.CASH })
                {
                    foreach (KeyValuePair<int, int> entry in _inventory.GetItems(inventoryType))
                    {
                        if (entry.Value <= 0 ||
                            !TryGetConsumableDefinition(entry.Key, out EnhancementConsumableDefinition candidateDefinition) ||
                            candidateDefinition.EffectType != ConsumableEffectType.Enhancement)
                        {
                            continue;
                        }

                        EnhancementConsumable candidate = new EnhancementConsumable(candidateDefinition);
                        if (!IsModifierCompatible(modifier, candidate) ||
                            !IsConsumableCompatibleWithItem(candidateDefinition, selectedPart?.ItemId ?? 0) ||
                            !TryGetConsumableCompatibilityBlockReason(candidate, selectedPart, out _))
                        {
                            continue;
                        }

                        if (state.RemainingSlots <= 0 || state.RemainingSlots < candidate.SuccessCountGain)
                        {
                            continue;
                        }

                        return candidate;
                    }
                }
            }

            if (TryResolveVegaModifierProfile(modifierDefinition.ItemId, out _))
            {
                return null;
            }

            return modifierDefinition.ModifierBehavior switch
            {
                ModifierBehavior.VegaTenPercent => GetFirstAvailableConsumable(state, selectedPart, AdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId, AlternateAdvancedEnhancementScrollId2),
                ModifierBehavior.VegaSixtyPercent => GetFirstAvailableConsumable(state, selectedPart, EquipEnhancementScrollId, AlternateEquipEnhancementScrollId),
                _ => null
            };
        }

        private string BuildModifierCompatibilityMessage(EnhancementConsumable consumable)
        {
            if (!_preferredModifierItemId.HasValue ||
                !TryGetConsumableDefinition(_preferredModifierItemId.Value, out EnhancementConsumableDefinition definition))
            {
                return consumable != null
                    ? $"{consumable.Name} is not compatible with the selected modifier."
                    : "No compatible modifier could be applied.";
            }

            string requiredScroll = definition.ModifierBehavior switch
            {
                ModifierBehavior.VegaTenPercent when TryResolveVegaModifierProfile(definition.ItemId, out VegaModifierProfile tenProfile)
                    => $"a Vega-enabled {(int)Math.Round(tenProfile.RequiredBaseSuccessRate * 100f)}% scroll",
                ModifierBehavior.VegaSixtyPercent when TryResolveVegaModifierProfile(definition.ItemId, out VegaModifierProfile sixtyProfile)
                    => $"a Vega-enabled {(int)Math.Round(sixtyProfile.RequiredBaseSuccessRate * 100f)}% scroll",
                ModifierBehavior.VegaTenPercent => "a Vega-enabled 10% scroll",
                ModifierBehavior.VegaSixtyPercent => "a Vega-enabled 60% scroll",
                _ => "a compatible enhancement scroll"
            };

            return $"{definition.Name} requires {requiredScroll}.";
        }

        private bool ResolveDestroyOnFailure(EnhancementConsumable consumable)
        {
            if (consumable == null)
            {
                return false;
            }

            float destroyChance = MathHelper.Clamp(consumable.Definition.DestroyChanceOnFailure, 0f, 1.0f);
            return destroyChance > 0f && _random.NextDouble() < destroyChance;
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
                    : ResolveCachedItemNameOrFallback(itemId));
        }

        private static string ResolveCachedItemNameOrFallback(int itemId)
        {
            return HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo) &&
                   !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : $"Item #{itemId}";
        }

        private static string ResolveCachedItemDescription(int itemId)
        {
            return InventoryItemMetadataResolver.TryResolveItemDescription(itemId, out string description)
                ? description
                : string.Empty;
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
                int defaultSlotCount = ResolveDefaultSlotCount(slot, part);
                int totalSlots = Math.Max(defaultSlotCount, part?.TotalUpgradeSlotCount ?? 0);
                int remainingSlots = part?.RemainingUpgradeSlotCount ?? (part?.UpgradeSlots ?? totalSlots);
                remainingSlots = Math.Clamp(remainingSlots, 0, Math.Max(totalSlots, remainingSlots));
                int potentialLineCount = CountVisiblePotentialLines(part);
                state = new UpgradeState
                {
                    ItemId = part?.ItemId ?? 0,
                    TotalSlots = Math.Max(totalSlots, remainingSlots),
                    RemainingSlots = remainingSlots,
                    SuccessCount = Math.Max(0, Math.Max(totalSlots, remainingSlots) - remainingSlots),
                    OriginalSuccessfulUpgradeCount = Math.Max(0, Math.Max(totalSlots, remainingSlots) - remainingSlots),
                    HasPotential = potentialLineCount > 0 || !string.IsNullOrWhiteSpace(part?.PotentialTierText),
                    PotentialTier = ParsePotentialTier(part?.PotentialTierText),
                    PotentialLineCount = potentialLineCount,
                    PotentialLines = CopyPotentialLines(part?.PotentialLines, potentialLineCount)
                };
                _upgradeStates[slot] = state;
            }

            return state;
        }

        private static void SyncStateToPart(CharacterPart part, UpgradeState state)
        {
            if (part == null || state == null)
            {
                return;
            }

            part.TotalUpgradeSlotCount = state.TotalSlots;
            part.RemainingUpgradeSlotCount = state.RemainingSlots;
            part.UpgradeSlots = state.RemainingSlots;
            part.EnhancementStarCount = Math.Max(0, state.SuccessCount);

            if (state.HasPotential)
            {
                part.PotentialTierText = $"{state.PotentialTier} Potential";
                part.PotentialLines = new List<string>(EnumerateVisiblePotentialLines(state));
            }
            else
            {
                part.PotentialTierText = null;
                part.PotentialLines = new List<string>();
            }
        }

        private static int CountVisiblePotentialLines(CharacterPart part)
        {
            if (part?.PotentialLines == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < part.PotentialLines.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(part.PotentialLines[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static string[] CopyPotentialLines(IReadOnlyList<string> lines, int visibleCount)
        {
            var copy = new string[UpgradeState.MaxPotentialLines];
            if (lines == null || visibleCount <= 0)
            {
                return copy;
            }

            int max = Math.Min(Math.Min(lines.Count, visibleCount), copy.Length);
            for (int i = 0; i < max; i++)
            {
                copy[i] = lines[i];
            }

            return copy;
        }

        private static PotentialTier ParsePotentialTier(string potentialTierText)
        {
            if (string.IsNullOrWhiteSpace(potentialTierText))
            {
                return PotentialTier.Rare;
            }

            string normalized = potentialTierText.Trim();
            if (normalized.IndexOf("Legendary", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PotentialTier.Legendary;
            }

            if (normalized.IndexOf("Unique", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PotentialTier.Unique;
            }

            if (normalized.IndexOf("Epic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PotentialTier.Epic;
            }

            return PotentialTier.Rare;
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
                EquipSlot.Ring1 or EquipSlot.Ring2 or EquipSlot.Ring3 or EquipSlot.Ring4 => "Ring",
                EquipSlot.Pendant or EquipSlot.Pendant2 => "Pendant",
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
            AppendConsumableSummary(segments, CleanSlateScrollIds, "Clean Slate");
            AppendConsumableSummary(segments, InnocenceScrollIds, "Innocence");
            AppendConsumableSummary(segments, GoldenHammerIds, "Gold Hammer");
            AppendConsumableSummary(segments, ViciousHammerId, "Vicious");
            AppendConsumableSummary(segments, PotentialScrollId, "Pot");
            AppendConsumableSummary(segments, PotentialScrollId2, "Pot");
            AppendConsumableSummary(segments, AdvancedPotentialScrollId, "Adv Pot");
            AppendConsumableSummary(segments, AdvancedPotentialScrollId2, "Adv Pot");
            AppendConsumableSummary(segments, SpecialPotentialScrollIdLegacy, "Spec Pot");
            AppendConsumableSummary(segments, SpecialPotentialScrollId, "Spec Pot");
            AppendConsumableSummary(segments, EpicPotentialScrollId, EpicPotentialScrollId2, "Epic Pot");
            AppendConsumableSummary(segments, EpicPotentialScrollId3, EpicPotentialScrollId4, "Epic Pot");
            AppendConsumableSummary(segments, CarvedGoldenSealId, "Gold Seal");
            AppendConsumableSummary(segments, CarvedSilverSealId, "Silver Seal");
            AppendConsumableSummary(segments, UretesTimeLabId, "Time Lab");
            AppendConsumableSummary(segments, MiracleCubeId, "Cube");
            AppendConsumableSummary(segments, PremiumMiracleCubeId, "Premium");
            AppendConsumableSummary(segments, SuperMiracleCubeId, "Super");
            AppendConsumableSummary(segments, RevolutionaryMiracleCubeId, "Revo");
            AppendConsumableSummary(segments, GoldenMiracleCubeId, "Golden");
            AppendConsumableSummary(segments, EnlighteningMiracleCubeId, "Enlight");
            AppendConsumableSummary(segments, MapleMiracleCubeId, "Maple");
            AppendConsumableSummary(segments, VegasSpellTenPercentId, "Vega10");
            AppendConsumableSummary(segments, VegasSpellSixtyPercentId, "Vega60");
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

        private EnhancementConsumable GetFirstAvailableConsumable(UpgradeState state, CharacterPart selectedPart, params int[] itemIds)
        {
            for (int i = 0; i < itemIds.Length; i++)
            {
                int itemId = itemIds[i];
                if (GetConsumableCount(itemId) <= 0 ||
                    !TryGetConsumableDefinition(itemId, out EnhancementConsumableDefinition definition) ||
                    (definition.EffectType == ConsumableEffectType.Enhancement && definition.SuccessCountGain > state.RemainingSlots) ||
                    !IsConsumableCompatibleWithItem(definition, selectedPart?.ItemId ?? 0))
                {
                    continue;
                }

                if (definition.EffectType == ConsumableEffectType.SlotRecovery &&
                    state.RemainingSlots >= state.TotalSlots)
                {
                    continue;
                }

                if (definition.EffectType == ConsumableEffectType.Hammer &&
                    !TryGetHammerBlockReason(new EnhancementConsumable(definition), selectedPart, state, out _))
                {
                    continue;
                }

                return new EnhancementConsumable(definition);
            }

            return null;
        }

        private static bool TryGetConsumableDefinition(int itemId, out EnhancementConsumableDefinition definition)
        {
            if (ConsumableDefinitions.TryGetValue(itemId, out definition))
            {
                return true;
            }

            if (DynamicConsumableDefinitionCache.TryGetValue(itemId, out definition))
            {
                return true;
            }

            if (TryCreateDynamicConsumableDefinition(itemId, out definition))
            {
                DynamicConsumableDefinitionCache[itemId] = definition;
                return true;
            }

            return false;
        }

        private static bool IsConsumableCompatibleWithItem(EnhancementConsumableDefinition definition, int equipItemId)
        {
            if (definition.ItemId <= 0 || equipItemId <= 0)
            {
                return true;
            }

            IReadOnlyCollection<int> requiredItems = GetRequiredEquipItemIds(definition);
            return requiredItems.Count == 0 || requiredItems.Contains(equipItemId);
        }

        private static IReadOnlyCollection<int> GetRequiredEquipItemIds(EnhancementConsumableDefinition definition)
        {
            if (ConsumableRequirementCache.TryGetValue(definition.ItemId, out IReadOnlyCollection<int> cached))
            {
                return cached;
            }

            var requiredItems = new HashSet<int>();
            if (definition.InventoryType == InventoryType.CASH)
            {
                string imagePath = $"Cash/{(definition.ItemId / 10000):D4}.img";
                WzImage image = HaCreator.Program.DataSource?.GetImage("Item", imagePath);
                if (image != null)
                {
                    image.ParseImage();
                    if (image.GetFromPath($"{definition.ItemId:D8}/req") is WzSubProperty reqProperty)
                    {
                        foreach (WzIntProperty itemProperty in reqProperty.WzProperties.OfType<WzIntProperty>())
                        {
                            if (itemProperty.Value > 0)
                            {
                                requiredItems.Add(itemProperty.Value);
                            }
                        }
                    }
                }
            }

            IReadOnlyCollection<int> result = requiredItems.Count > 0
                ? requiredItems.ToArray()
                : Array.Empty<int>();
            ConsumableRequirementCache[definition.ItemId] = result;
            return result;
        }

        private static bool TryGetConsumableCompatibilityBlockReason(EnhancementConsumable consumable, CharacterPart selectedPart, out string reason)
        {
            reason = null;
            if (consumable == null || selectedPart == null)
            {
                return true;
            }

            bool matchesRequiredItem = IsConsumableCompatibleWithItem(consumable.Definition, selectedPart.ItemId);
            bool matchesTargetSlot = !TryGetScrollTargetSlots(consumable.Definition.ItemId, out IReadOnlyCollection<EquipSlot> targetSlots) ||
                                     targetSlots.Count == 0 ||
                                     targetSlots.Contains(selectedPart.Slot);
            if (matchesRequiredItem && matchesTargetSlot)
            {
                return true;
            }

            reason = consumable.Definition.ItemId == MapleMiracleCubeId
                ? $"{consumable.Name} only applies to Maple 8th Anniversary Crimson equipment."
                : $"{consumable.Name} does not apply to {ResolveItemName(selectedPart)}.";
            if (!matchesTargetSlot &&
                TryGetScrollTargetSlots(consumable.Definition.ItemId, out targetSlots) &&
                targetSlots.Count > 0)
            {
                string targetText = string.Join(", ", targetSlots.Select(ResolveSlotLabel).Distinct());
                reason = $"{consumable.Name} only applies to {targetText} equipment.";
            }

            return false;
        }

        private bool TryGetHammerBlockReason(EnhancementConsumable consumable, CharacterPart selectedPart, UpgradeState state, out string reason)
        {
            reason = null;
            if (consumable == null || consumable.EffectType != ConsumableEffectType.Hammer)
            {
                return true;
            }

            if (selectedPart == null || state == null)
            {
                reason = "No equipped item is selected for hammer use.";
                return false;
            }

            bool isViciousHammer = consumable.Definition.HammerBehavior == HammerBehavior.Vicious;
            if (!isViciousHammer && state.ViciousHammerCount > 0)
            {
                reason = $"{consumable.Name} cannot be used after Vicious' Hammer has already tempered {ResolveItemName(selectedPart)}.";
                return false;
            }

            if (!isViciousHammer && state.GoldenHammerCount > 0)
            {
                reason = $"{ResolveItemName(selectedPart)} already used its Golden Hammer chance.";
                return false;
            }

            if (isViciousHammer && state.GoldenHammerCount > 0)
            {
                reason = $"{consumable.Name} cannot be used after Golden Hammer has already tempered {ResolveItemName(selectedPart)}.";
                return false;
            }

            if (isViciousHammer && state.ViciousHammerCount >= 2)
            {
                reason = $"{ResolveItemName(selectedPart)} already used the maximum two Vicious' Hammer applications.";
                return false;
            }

            if (isViciousHammer && HorntailNecklaceIds.Contains(selectedPart.ItemId))
            {
                reason = $"{consumable.Name} cannot be used on the Horntail Necklace family.";
                return false;
            }

            return true;
        }

        private bool TryGetCubeRewardBlockReason(EnhancementConsumable consumable, out string reason)
        {
            reason = null;
            int rewardItemId = ResolveCubeRewardItemId(consumable);
            if (rewardItemId <= 0 || _inventory == null)
            {
                return false;
            }

            if (_inventory.CanAcceptItem(InventoryType.USE, rewardItemId, 1))
            {
                return false;
            }

            reason = $"{ResolveConsumableName(rewardItemId)} cannot be received because the USE inventory is full.";
            return true;
        }

        private bool TryGrantCubeReward(EnhancementConsumable consumable, out int rewardItemId)
        {
            rewardItemId = ResolveCubeRewardItemId(consumable);
            if (rewardItemId <= 0 || _inventory == null)
            {
                return false;
            }

            _inventory.AddItem(InventoryType.USE, rewardItemId, null, 1);
            return true;
        }

        private static int ResolveCubeRewardItemId(EnhancementConsumable consumable)
        {
            if (consumable == null || consumable.EffectType != ConsumableEffectType.Cube)
            {
                return 0;
            }

            return consumable.Definition.RewardItemId;
        }

        private static string ResolveConsumableOwnerPath(int itemId)
        {
            if (ConsumableOwnerPathCache.TryGetValue(itemId, out string cachedPath))
            {
                return cachedPath;
            }

            string ownerPath = null;
            if (TryGetConsumableDefinition(itemId, out EnhancementConsumableDefinition definition) &&
                definition.InventoryType == InventoryType.CASH)
            {
                string imagePath = $"Cash/{(itemId / 10000):D4}.img";
                WzImage image = HaCreator.Program.DataSource?.GetImage("Item", imagePath);
                if (image != null)
                {
                    image.ParseImage();
                    ownerPath = (image.GetFromPath($"{itemId:D8}/info/path") as WzStringProperty)?.GetString();
                }
            }

            ConsumableOwnerPathCache[itemId] = ownerPath;
            return ownerPath;
        }

        private PotentialTier MaybeUpgradePotentialTier(PotentialTier currentTier, CubeBehavior behavior)
        {
            double roll = _random.NextDouble();
            (double rareToEpic, double epicToUnique, double uniqueToLegendary, PotentialTier maxTier, bool jumpToMaxTier) = behavior switch
            {
                CubeBehavior.Premium => (0.15d, 0.05d, 0.01d, PotentialTier.Legendary, false),
                CubeBehavior.Super => (0.22d, 0.08d, 0.02d, PotentialTier.Legendary, false),
                CubeBehavior.Revolutionary => (0.18d, 0.06d, 0.015d, PotentialTier.Legendary, false),
                CubeBehavior.Golden => (0.10d, 0.03d, 0d, PotentialTier.Unique, false),
                CubeBehavior.Enlightening => (0.28d, 0.12d, 0.05d, PotentialTier.Legendary, true),
                // WZ only states Maple Miracle Cube can reveal higher potentials than the base Miracle Cube.
                CubeBehavior.Maple => (0.16d, 0.06d, 0.015d, PotentialTier.Legendary, false),
                _ => (0.12d, 0.04d, 0d, PotentialTier.Unique, false)
            };

            return currentTier switch
            {
                PotentialTier.Rare when roll < rareToEpic => jumpToMaxTier ? maxTier : PotentialTier.Epic,
                PotentialTier.Epic when roll < epicToUnique => jumpToMaxTier ? maxTier : PotentialTier.Unique,
                PotentialTier.Unique when maxTier >= PotentialTier.Legendary && roll < uniqueToLegendary => PotentialTier.Legendary,
                _ => currentTier
            };
        }

        private int ResolveCubePotentialLineCount(int currentCount, CubeBehavior behavior)
        {
            return behavior switch
            {
                CubeBehavior.Premium => RollPotentialLineCount(),
                CubeBehavior.Revolutionary => MathHelper.Clamp(currentCount + _random.Next(-1, 2), 1, UpgradeState.MaxPotentialLines),
                _ => Math.Max(1, currentCount)
            };
        }

        private int RollPotentialLineCount()
        {
            double roll = _random.NextDouble();
            if (roll < 0.1d)
            {
                return 1;
            }

            if (roll < 0.75d)
            {
                return 2;
            }

            return UpgradeState.MaxPotentialLines;
        }

        private string[] RollPotentialLines(PotentialTier tier, int lineCount)
        {
            string[][] pool = tier switch
            {
                PotentialTier.Legendary => new[]
                {
                    new[] { "STR +9%", "DEX +9%", "INT +9%", "LUK +9%", "ATT +9", "Boss +30%" },
                    new[] { "HP +12%", "MP +12%", "Critical +9%", "All Stat +6%", "Damage +9%" },
                    new[] { "Ignore DEF +15%", "ATT +6%", "M.ATT +6", "IED +10%", "Boss +20%" }
                },
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

            var lines = new string[UpgradeState.MaxPotentialLines];
            for (int i = 0; i < lineCount; i++)
            {
                string[] candidates = pool[Math.Min(i, pool.Length - 1)];
                lines[i] = candidates[_random.Next(candidates.Length)];
            }

            return lines;
        }

        private static IEnumerable<string> EnumerateVisiblePotentialLines(UpgradeState state)
        {
            if (state?.PotentialLines == null || state.PotentialLineCount <= 0)
            {
                return Enumerable.Empty<string>();
            }

            return state.PotentialLines
                .Take(state.PotentialLineCount)
                .Where(line => !string.IsNullOrWhiteSpace(line));
        }

        private void ResetAppliedEnhancementState(EquipSlot slot, CharacterPart selectedPart, UpgradeState state)
        {
            if (_characterBuild != null)
            {
                _characterBuild.Attack = Math.Max(0, _characterBuild.Attack - state.AttackBonus);
                _characterBuild.Defense = Math.Max(0, _characterBuild.Defense - state.DefenseBonus);
            }

            if (selectedPart != null)
            {
                selectedPart.BonusWeaponAttack = Math.Max(0, selectedPart.BonusWeaponAttack - state.AttackBonus);
                selectedPart.BonusWeaponDefense = Math.Max(0, selectedPart.BonusWeaponDefense - state.DefenseBonus);
            }

            state.AttackBonus = 0;
            state.DefenseBonus = 0;
            state.SuccessCount = state.OriginalSuccessfulUpgradeCount;
            state.FailCount = 0;
            state.RemainingSlots = state.TotalSlots;

            if (_characterBuild?.Equipment != null &&
                _characterBuild.Equipment.TryGetValue(slot, out CharacterPart currentPart) &&
                ReferenceEquals(currentPart, selectedPart))
            {
                SyncStateToPart(currentPart, state);
            }
        }

        private static bool TryCreateDynamicConsumableDefinition(int itemId, out EnhancementConsumableDefinition definition)
        {
            definition = default;
            if (CleanSlateScrollIds.Contains(itemId))
            {
                return TryCreateWzConsumeDefinition(itemId, ConsumableEffectType.SlotRecovery, out definition);
            }

            if (InnocenceScrollIds.Contains(itemId))
            {
                return TryCreateWzConsumeDefinition(itemId, ConsumableEffectType.Reset, out definition);
            }

            if (GoldenHammerIds.Contains(itemId))
            {
                return TryCreateWzHammerDefinition(itemId, InventoryType.USE, HammerBehavior.None, out definition);
            }

            if (itemId == ViciousHammerId)
            {
                return TryCreateWzHammerDefinition(itemId, InventoryType.CASH, HammerBehavior.Vicious, out definition);
            }

            if (TryCreateStringBackedEnhancementDefinition(itemId, out definition))
            {
                return true;
            }

            return false;
        }

        private static bool TryCreateStringBackedEnhancementDefinition(int itemId, out EnhancementConsumableDefinition definition)
        {
            definition = default;
            if (InventoryItemMetadataResolver.ResolveInventoryType(itemId) != InventoryType.USE ||
                !TryResolveVegaCompatibleScrollProfile(itemId, out VegaCompatibleScrollProfile profile))
            {
                return false;
            }

            definition = new EnhancementConsumableDefinition(
                itemId,
                ResolveCachedItemNameOrFallback(itemId),
                1,
                false,
                false,
                profile.BaseSuccessRate,
                InventoryType.USE,
                ConsumableEffectType.Enhancement,
                PotentialTier.Rare,
                0f);
            return true;
        }

        private static bool TryCreateWzConsumeDefinition(int itemId, ConsumableEffectType effectType, out EnhancementConsumableDefinition definition)
        {
            definition = default;
            string imagePath = $"Consume/{(itemId / 10000):D4}.img";
            WzImage image = HaCreator.Program.DataSource?.GetImage("Item", imagePath);
            if (image == null)
            {
                return false;
            }

            image.ParseImage();
            WzSubProperty info = image.GetFromPath($"{itemId:D8}/info") as WzSubProperty;
            if (info == null)
            {
                return false;
            }

            int success = Math.Max(0, (info["success"] as WzIntProperty)?.Value ?? 100);
            int cursed = Math.Max(0, (info["cursed"] as WzIntProperty)?.Value ?? 0);
            int recover = Math.Max(1, (info["recover"] as WzIntProperty)?.Value ?? 1);
            definition = new EnhancementConsumableDefinition(
                itemId,
                ResolveCachedItemNameOrFallback(itemId),
                effectType == ConsumableEffectType.SlotRecovery ? recover : 0,
                false,
                false,
                MathHelper.Clamp(success / 100f, 0f, 1.0f),
                InventoryType.USE,
                effectType,
                PotentialTier.Rare,
                MathHelper.Clamp(cursed / 100f, 0f, 1.0f));
            return true;
        }

        private static bool TryCreateWzHammerDefinition(int itemId, InventoryType inventoryType, HammerBehavior hammerBehavior, out EnhancementConsumableDefinition definition)
        {
            definition = default;
            string category = inventoryType == InventoryType.CASH ? "Cash" : "Consume";
            string imagePath = $"{category}/{(itemId / 10000):D4}.img";
            WzImage image = HaCreator.Program.DataSource?.GetImage("Item", imagePath);
            if (image == null)
            {
                return false;
            }

            image.ParseImage();
            WzSubProperty info = image.GetFromPath($"{itemId:D8}/info") as WzSubProperty;
            if (info == null)
            {
                return false;
            }

            int? explicitSuccess = (info["success"] as WzIntProperty)?.Value;
            int success = explicitSuccess.HasValue
                ? Math.Max(0, explicitSuccess.Value)
                : ResolveHammerSuccessRateFromDescription(itemId);
            definition = new EnhancementConsumableDefinition(
                itemId,
                ResolveCachedItemNameOrFallback(itemId),
                1,
                false,
                false,
                MathHelper.Clamp(success / 100f, 0f, 1.0f),
                inventoryType,
                ConsumableEffectType.Hammer,
                PotentialTier.Rare,
                0f,
                CubeBehavior.Miracle,
                ModifierBehavior.None,
                0f,
                0,
                hammerBehavior);
            return true;
        }

        private static int ResolveHammerSuccessRateFromDescription(int itemId)
        {
            string description = ResolveCachedItemDescription(itemId);
            if (!string.IsNullOrWhiteSpace(description))
            {
                Match explicitChanceMatch = PercentChanceRegex.Match(description);
                if (explicitChanceMatch.Success &&
                    int.TryParse(explicitChanceMatch.Groups[1].Value, out int explicitChance))
                {
                    return Math.Clamp(explicitChance, 0, 100);
                }

                Match genericPercentMatch = PercentRateRegex.Match(description);
                if (genericPercentMatch.Success &&
                    int.TryParse(genericPercentMatch.Groups[1].Value, out int genericPercent))
                {
                    return Math.Clamp(genericPercent, 0, 100);
                }
            }

            return 100;
        }

        private sealed class UpgradeState
        {
            public const int MaxPotentialLines = 3;

            public int ItemId { get; set; }
            public int TotalSlots { get; set; }
            public int RemainingSlots { get; set; }
            public int Attempts { get; set; }
            public int SuccessCount { get; set; }
            public int OriginalSuccessfulUpgradeCount { get; set; }
            public int FailCount { get; set; }
            public int RecoveredSlotCount { get; set; }
            public int GoldenHammerCount { get; set; }
            public int ViciousHammerCount { get; set; }
            public int AttackBonus { get; set; }
            public int DefenseBonus { get; set; }
            public bool HasPotential { get; set; }
            public PotentialTier PotentialTier { get; set; }
            public int PotentialLineCount { get; set; }
            public string[] PotentialLines { get; set; } = Array.Empty<string>();
        }

        private enum ConsumableEffectType
        {
            Enhancement,
            SlotRecovery,
            Reset,
            PotentialScroll,
            PotentialStamp,
            Cube,
            Modifier,
            Hammer
        }

        private enum PotentialTier
        {
            Rare,
            Epic,
            Unique,
            Legendary
        }

        private enum CubeBehavior
        {
            Miracle,
            Premium,
            Super,
            Revolutionary,
            Golden,
            Enlightening,
            Maple
        }

        private enum ModifierBehavior
        {
            None,
            VegaTenPercent,
            VegaSixtyPercent
        }

        private enum HammerBehavior
        {
            None,
            Vicious
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
                float destroyChanceOnFailure = 1.0f,
                CubeBehavior cubeBehavior = CubeBehavior.Miracle,
                ModifierBehavior modifierBehavior = ModifierBehavior.None,
                float modifiedSuccessRate = 0f,
                int rewardItemId = 0,
                HammerBehavior hammerBehavior = HammerBehavior.None)
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
                DestroyChanceOnFailure = destroyChanceOnFailure;
                CubeBehavior = cubeBehavior;
                ModifierBehavior = modifierBehavior;
                ModifiedSuccessRate = modifiedSuccessRate;
                RewardItemId = rewardItemId;
                HammerBehavior = hammerBehavior;
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
            public float DestroyChanceOnFailure { get; }
            public CubeBehavior CubeBehavior { get; }
            public ModifierBehavior ModifierBehavior { get; }
            public float ModifiedSuccessRate { get; }
            public int RewardItemId { get; }
            public HammerBehavior HammerBehavior { get; }
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
            public CubeBehavior CubeBehavior => Definition.CubeBehavior;
        }

        private static string[] CopyPotentialLines(UpgradeState state)
        {
            string[] copy = new string[UpgradeState.MaxPotentialLines];
            if (state?.PotentialLines == null)
            {
                return copy;
            }

            Array.Copy(state.PotentialLines, copy, Math.Min(state.PotentialLines.Length, copy.Length));
            return copy;
        }

        private string[] PreservePotentialLinesWithExtraLine(string[] existingLines, PotentialTier tier, int lineCount)
        {
            string[] lines = new string[UpgradeState.MaxPotentialLines];
            if (existingLines != null)
            {
                Array.Copy(existingLines, lines, Math.Min(existingLines.Length, lines.Length));
            }

            int nextLineIndex = Math.Clamp(lineCount - 1, 0, UpgradeState.MaxPotentialLines - 1);
            string[] generatedLines = RollPotentialLines(tier, lineCount);
            lines[nextLineIndex] = generatedLines[nextLineIndex];
            return lines;
        }

        public readonly struct ItemUpgradeAttemptResult
        {
            public ItemUpgradeAttemptResult(bool? success, string statusMessage, int consumableItemId, int modifierItemId = 0)
            {
                Success = success;
                StatusMessage = statusMessage ?? string.Empty;
                ConsumableItemId = consumableItemId;
                ModifierItemId = modifierItemId;
            }

            public bool? Success { get; }
            public string StatusMessage { get; }
            public int ConsumableItemId { get; }
            public int ModifierItemId { get; }
        }

        public enum VisualThemeKind
        {
            Enhancement,
            Potential,
            PotentialStamp,
            MiracleCube,
            HyperMiracleCube,
            MapleMiracleCube
        }

        public sealed class WindowVisualTheme
        {
            public WindowVisualTheme(
                IDXObject frame,
                Texture2D backgroundOverlay,
                Point backgroundOverlayOffset,
                Texture2D headerOverlay,
                Point headerOverlayOffset,
                Texture2D gaugeBarTexture,
                Texture2D gaugeFillTexture,
                Point gaugeOffset,
                UIObject actionButton = null,
                UIObject cancelButton = null,
                IReadOnlyList<VegaSpellUI.VegaAnimationFrame> effectFrames = null)
            {
                Frame = frame;
                BackgroundOverlay = backgroundOverlay;
                BackgroundOverlayOffset = backgroundOverlayOffset;
                HeaderOverlay = headerOverlay;
                HeaderOverlayOffset = headerOverlayOffset;
                GaugeBarTexture = gaugeBarTexture;
                GaugeFillTexture = gaugeFillTexture;
                GaugeOffset = gaugeOffset;
                ActionButton = actionButton;
                CancelButton = cancelButton;
                EffectFrames = effectFrames ?? Array.Empty<VegaSpellUI.VegaAnimationFrame>();
            }

            public IDXObject Frame { get; }
            public Texture2D BackgroundOverlay { get; }
            public Point BackgroundOverlayOffset { get; }
            public Texture2D HeaderOverlay { get; }
            public Point HeaderOverlayOffset { get; }
            public Texture2D GaugeBarTexture { get; }
            public Texture2D GaugeFillTexture { get; }
            public Point GaugeOffset { get; }
            public UIObject ActionButton { get; }
            public UIObject CancelButton { get; }
            public IReadOnlyList<VegaSpellUI.VegaAnimationFrame> EffectFrames { get; }
        }

        public readonly struct ModifierPreview
        {
            public ModifierPreview(int consumableItemId, string consumableName, int consumableCount, float baseSuccessRate, float modifiedSuccessRate)
            {
                ConsumableItemId = consumableItemId;
                ConsumableName = consumableName ?? string.Empty;
                ConsumableCount = consumableCount;
                BaseSuccessRate = baseSuccessRate;
                ModifiedSuccessRate = modifiedSuccessRate;
            }

            public int ConsumableItemId { get; }
            public string ConsumableName { get; }
            public int ConsumableCount { get; }
            public float BaseSuccessRate { get; }
            public float ModifiedSuccessRate { get; }
        }

        private static bool TryResolveVegaModifierProfile(int itemId, out VegaModifierProfile profile)
        {
            if (VegaModifierProfileCache.TryGetValue(itemId, out profile))
            {
                return profile.IsValid;
            }

            profile = default;
            Match match = VegaModifierRegex.Match(ResolveCachedItemDescription(itemId) ?? string.Empty);
            if (match.Success &&
                int.TryParse(match.Groups[1].Value, out int modifiedPercent) &&
                int.TryParse(match.Groups[2].Value, out int requiredPercent))
            {
                profile = new VegaModifierProfile(requiredPercent / 100f, modifiedPercent / 100f);
            }

            VegaModifierProfileCache[itemId] = profile;
            return profile.IsValid;
        }

        private static bool TryResolveVegaCompatibleScrollProfile(int itemId, out VegaCompatibleScrollProfile profile)
        {
            if (VegaCompatibleScrollProfileCache.TryGetValue(itemId, out profile))
            {
                return profile.IsValid;
            }

            profile = default;
            string description = ResolveCachedItemDescription(itemId);
            if (!string.IsNullOrWhiteSpace(description) &&
                description.IndexOf("Vega's Spell", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Match match = PercentRateRegex.Match(description);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int basePercent))
                {
                    profile = new VegaCompatibleScrollProfile(basePercent / 100f);
                }
            }

            VegaCompatibleScrollProfileCache[itemId] = profile;
            return profile.IsValid;
        }

        private static bool TryGetScrollTargetSlots(int itemId, out IReadOnlyCollection<EquipSlot> slots)
        {
            if (ScrollTargetSlotCache.TryGetValue(itemId, out slots))
            {
                return slots.Count > 0;
            }

            HashSet<EquipSlot> resolvedSlots = ResolveTargetSlotsFromText(ResolveCachedItemDescription(itemId));
            string name = ResolveCachedItemNameOrFallback(itemId);
            if (resolvedSlots.Count == 0)
            {
                Match nameMatch = ScrollTargetRegex.Match(name ?? string.Empty);
                resolvedSlots = nameMatch.Success
                    ? ResolveTargetSlotsFromText(nameMatch.Groups[1].Value)
                    : ResolveTargetSlotsFromText(name);
            }

            slots = resolvedSlots.Count > 0
                ? resolvedSlots.ToArray()
                : Array.Empty<EquipSlot>();
            ScrollTargetSlotCache[itemId] = slots;
            return slots.Count > 0;
        }

        // Some WZ item names are misleading; the description text is the more reliable target hint.
        private static HashSet<EquipSlot> ResolveTargetSlotsFromText(string text)
        {
            var targetSlots = new HashSet<EquipSlot>();
            AddTargetSlotsFromText(text, targetSlots);
            return targetSlots;
        }

        private static void AddTargetSlotsFromText(string text, ISet<EquipSlot> targetSlots)
        {
            if (targetSlots == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string normalized = text.Trim();
            if (normalized.IndexOf("face accessory", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.FaceAccessory);
            }

            if (normalized.IndexOf("eye accessory", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.EyeAccessory);
            }

            if (normalized.IndexOf("earring", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Earrings);
            }

            if (normalized.IndexOf("glove", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Glove);
            }

            if (normalized.IndexOf("shoe", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Shoes);
            }

            if (normalized.IndexOf("cape", StringComparison.OrdinalIgnoreCase) >= 0 || normalized.IndexOf("mantle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Cape);
            }

            if (normalized.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Shield);
            }

            if (normalized.IndexOf("ring", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Ring1);
                targetSlots.Add(EquipSlot.Ring2);
                targetSlots.Add(EquipSlot.Ring3);
                targetSlots.Add(EquipSlot.Ring4);
            }

            if (normalized.IndexOf("pendant", StringComparison.OrdinalIgnoreCase) >= 0 || normalized.IndexOf("necklace", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Pendant);
                targetSlots.Add(EquipSlot.Pendant2);
            }

            if (normalized.IndexOf("belt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Belt);
            }

            if (normalized.IndexOf("overall", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Longcoat);
            }

            if (normalized.IndexOf("top", StringComparison.OrdinalIgnoreCase) >= 0 || normalized.IndexOf("coat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Coat);
            }

            if (normalized.IndexOf("bottom", StringComparison.OrdinalIgnoreCase) >= 0 || normalized.IndexOf("pants", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Pants);
            }

            if (normalized.IndexOf("helmet", StringComparison.OrdinalIgnoreCase) >= 0 || normalized.IndexOf("cap", StringComparison.OrdinalIgnoreCase) >= 0 || normalized.IndexOf("hat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetSlots.Add(EquipSlot.Cap);
            }

            string[] weaponKeywords =
            {
                "sword", "axe", "blunt", "dagger", "spear", "pole arm", "bow",
                "crossbow", "staff", "wand", "claw", "knuckle", "gun"
            };
            if (weaponKeywords.Any(keyword => normalized.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                targetSlots.Add(EquipSlot.Weapon);
            }
        }

        private static bool AreRatesEquivalent(float left, float right)
        {
            return Math.Abs(left - right) < 0.0001f;
        }

        private readonly struct VegaModifierProfile
        {
            public VegaModifierProfile(float requiredBaseSuccessRate, float modifiedSuccessRate)
            {
                RequiredBaseSuccessRate = requiredBaseSuccessRate;
                ModifiedSuccessRate = modifiedSuccessRate;
            }

            public float RequiredBaseSuccessRate { get; }
            public float ModifiedSuccessRate { get; }
            public bool IsValid => RequiredBaseSuccessRate > 0f && ModifiedSuccessRate > 0f;
        }

        private readonly struct VegaCompatibleScrollProfile
        {
            public VegaCompatibleScrollProfile(float baseSuccessRate)
            {
                BaseSuccessRate = baseSuccessRate;
            }

            public float BaseSuccessRate { get; }
            public bool IsValid => BaseSuccessRate > 0f;
        }

        private enum WindowPresentationState
        {
            Idle,
            Casting,
            Result
        }
    }
}
