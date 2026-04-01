using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public sealed class ItemMakerUI : UIWindowBase
    {
        private sealed class ItemMakerMaterial
        {
            public InventoryType InventoryType { get; init; }
            public int ItemId { get; init; }
            public int Quantity { get; init; }
        }

        private sealed class ItemMakerReward
        {
            public int ItemId { get; init; }
            public int Quantity { get; init; }
            public int ProbabilityWeight { get; init; }
        }

        private sealed class ItemMakerQuestRequirement
        {
            public int QuestId { get; init; }
            public int RequiredStateValue { get; init; }
        }

        private sealed class ItemMakerRecipe
        {
            public int BucketKey { get; init; }
            public int CategoryKey { get; init; }
            public ItemMakerRecipeFamily Family { get; init; }
            public bool IsHidden { get; init; }
            public string CategoryLabel { get; init; }
            public string Title { get; init; }
            public string Description { get; init; }
            public InventoryType OutputInventoryType { get; init; }
            public int OutputItemId { get; init; }
            public int OutputQuantity { get; init; }
            public int RequiredLevel { get; init; }
            public int RequiredSkillLevel { get; init; }
            public int RequiredItemId { get; init; }
            public int RequiredEquipItemId { get; init; }
            public int MesoCost { get; init; }
            public int CatalystItemId { get; init; }
            public bool UsesRandomReward { get; init; }
            public ItemMakerMaterial[] Materials { get; init; } = Array.Empty<ItemMakerMaterial>();
            public ItemMakerReward[] RandomRewards { get; init; } = Array.Empty<ItemMakerReward>();
            public ItemMakerQuestRequirement[] RequiredQuestStates { get; init; } = Array.Empty<ItemMakerQuestRequirement>();
        }

        private sealed class ItemMakerPage
        {
            public int CategoryKey { get; init; }
            public int SortOrder { get; init; }
            public string Label { get; init; } = string.Empty;
            public List<ItemMakerRecipe> Recipes { get; } = new();
        }

        private enum MakerLaunchFilter
        {
            None,
            ItemMaker,
            GloveMaker,
            ShoeMaker,
            ToyMaker
        }

        private enum ItemMakerDetailSection
        {
            Recipe,
            Gem
        }

        private readonly struct BackgroundLayer
        {
            public BackgroundLayer(IDXObject drawable, Point offset)
            {
                Drawable = drawable;
                Offset = offset;
            }

            public IDXObject Drawable { get; }
            public Point Offset { get; }
        }

        private const int RecipeRowHeight = 36;
        private const int SelectorDropdownRowHeight = 20;
        private const int CraftDurationMs = 1400;
        private const int VisibleRecipeCount = 6;
        private const int AllCategoryKey = -1;
        private const int HiddenCategoryKey = 999;
        private static readonly int[] ClientRecipeBuckets = { 0, 1, 2, 4, 8, 16 };

        private readonly List<BackgroundLayer> _backgroundLayers = new();
        private readonly List<ItemMakerRecipe> _allRecipes = new();
        private readonly List<ItemMakerPage> _pages = new();
        private readonly HashSet<int> _discoveredRecipeIds = new();
        private readonly HashSet<int> _unlockedHiddenRecipeIds = new();
        private readonly Random _random = new();
        private readonly Texture2D _pixel;

        private Texture2D _gaugeBarTexture;
        private Texture2D _gaugeFillTexture;
        private Point _gaugePosition = new(18, 296);
        private SpriteFont _font;
        private IInventoryRuntime _inventory;
        private Func<int, Texture2D> _itemIconProvider;
        private readonly Dictionary<int, Texture2D> _itemIconCache = new();
        private MouseState _previousMouseState;
        private int _selectedRecipeIndex;
        private int _recipeScrollOffset;
        private int _selectedPageIndex;
        private int _characterLevel = 1;
        private int _characterJobId;
        private int _makerSkillLevel;
        private ItemMakerProgressionSnapshot _progression = ItemMakerProgressionSnapshot.Default;
        private Func<int, bool> _hasRequiredEquip;
        private Func<int, int, bool> _matchesQuestRequirement;
        private string _launchContextLabel;
        private MakerLaunchFilter _launchFilter;
        private bool _isCrafting;
        private bool _isCategorySelectorExpanded;
        private bool _isItemSelectorExpanded;
        private int _craftStartTick;
        private int _craftingRecipeIndex = -1;
        private string _statusMessage = "Select a category and item to craft.";

        public ItemMakerUI(IDXObject frame, Texture2D pixel)
            : base(frame)
        {
            _pixel = pixel;
            LoadRecipes();
        }

        public override string WindowName => MapSimulatorWindowNames.ItemMaker;

        public event Action<ItemMakerCraftResult> CraftCompleted;
        public event Action<IReadOnlyCollection<int>> RecipesDiscovered;
        public event Action<IReadOnlyCollection<int>> HiddenRecipesUnlocked;

        private IReadOnlyList<ItemMakerRecipe> CurrentRecipes =>
            _selectedPageIndex >= 0 && _selectedPageIndex < _pages.Count
                ? _pages[_selectedPageIndex].Recipes
                : Array.Empty<ItemMakerRecipe>();

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetInventory(IInventoryRuntime inventory)
        {
            _inventory = inventory;
            RebuildVisiblePages();
        }

        public void SetCraftingState(
            int characterLevel,
            int makerSkillLevel,
            int characterJobId,
            ItemMakerProgressionSnapshot progression = null,
            Func<int, bool> hasRequiredEquip = null,
            Func<int, int, bool> matchesQuestRequirement = null)
        {
            _characterLevel = Math.Max(1, characterLevel);
            _makerSkillLevel = Math.Max(0, makerSkillLevel);
            _characterJobId = Math.Max(0, characterJobId);
            _progression = progression ?? ItemMakerProgressionSnapshot.Default;
            ResetRecipeState(_progression);
            _hasRequiredEquip = hasRequiredEquip;
            _matchesQuestRequirement = matchesQuestRequirement;
            RebuildVisiblePages();
        }

        public void SetItemIconProvider(Func<int, Texture2D> itemIconProvider)
        {
            _itemIconProvider = itemIconProvider;
            _itemIconCache.Clear();
        }

        public void UpdateProgression(ItemMakerProgressionSnapshot progression, string overrideStatusMessage = null)
        {
            _progression = progression ?? ItemMakerProgressionSnapshot.Default;
            ResetRecipeState(_progression);
            RebuildVisiblePages();
            if (!string.IsNullOrWhiteSpace(overrideStatusMessage))
            {
                RefreshStatusMessage(overrideStatusMessage);
            }
        }

        public void ApplyLaunchContext(string npcFunctionText)
        {
            _launchContextLabel = npcFunctionText?.Trim();
            _launchFilter = ResolveLaunchFilter(_launchContextLabel);
            RebuildVisiblePages();
            FocusLaunchContext();
        }

        public void SetGaugeTextures(Texture2D gaugeBarTexture, Texture2D gaugeFillTexture, Point gaugePosition)
        {
            _gaugeBarTexture = gaugeBarTexture;
            _gaugeFillTexture = gaugeFillTexture;
            _gaugePosition = gaugePosition;
        }

        public void AddBackgroundLayer(IDXObject drawable, Point offset)
        {
            if (drawable != null)
            {
                _backgroundLayers.Add(new BackgroundLayer(drawable, offset));
            }
        }

        public void InitializeControls(UIObject startButton, UIObject cancelButton, UIObject pageSelectorButton)
        {
            if (startButton != null)
            {
                AddButton(startButton);
                startButton.ButtonClickReleased += _ => BeginCraft();
            }

            if (cancelButton != null)
            {
                AddButton(cancelButton);
                cancelButton.ButtonClickReleased += _ => CancelCraft();
            }

            if (pageSelectorButton != null)
            {
                AddButton(pageSelectorButton);
                pageSelectorButton.ButtonClickReleased += _ => ToggleCategorySelector();
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!_isCrafting)
            {
                return;
            }

            int now = Environment.TickCount;
            if (now - _craftStartTick >= CraftDurationMs)
            {
                CompleteCraft();
            }
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            bool handled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            if (!IsVisible)
            {
                _previousMouseState = mouseState;
                return handled;
            }

            Rectangle listRect = GetRecipeListRectangle();
            Rectangle categoryRect = GetCategorySelectorRectangle();
            Rectangle categoryDropDownRect = GetCategorySelectorDropDownRectangle();
            Rectangle itemRect = GetItemSelectorRectangle();
            Rectangle itemDropDownRect = GetItemSelectorDropDownRectangle();

            if (listRect.Contains(mouseState.X, mouseState.Y) && mouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue)
            {
                ScrollRecipes(mouseState.ScrollWheelValue > _previousMouseState.ScrollWheelValue ? -1 : 1);
                _previousMouseState = mouseState;
                return true;
            }

            if (handled || _isCrafting)
            {
                _previousMouseState = mouseState;
                return handled;
            }

            bool leftJustReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            if (!leftJustReleased)
            {
                _previousMouseState = mouseState;
                return false;
            }

            if (_isCategorySelectorExpanded && categoryDropDownRect.Contains(mouseState.X, mouseState.Y))
            {
                int optionIndex = Math.Clamp((mouseState.Y - categoryDropDownRect.Y) / SelectorDropdownRowHeight, 0, _pages.Count - 1);
                SelectRecipePage(optionIndex);
                _previousMouseState = mouseState;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (_isItemSelectorExpanded && itemDropDownRect.Contains(mouseState.X, mouseState.Y))
            {
                int optionIndex = _recipeScrollOffset + Math.Clamp((mouseState.Y - itemDropDownRect.Y) / SelectorDropdownRowHeight, 0, GetVisibleRecipeSelectorCount() - 1);
                if (optionIndex >= 0 && optionIndex < CurrentRecipes.Count)
                {
                    SelectRecipe(optionIndex);
                    _previousMouseState = mouseState;
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }

            if (categoryRect.Contains(mouseState.X, mouseState.Y))
            {
                ToggleCategorySelector();
                _previousMouseState = mouseState;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (itemRect.Contains(mouseState.X, mouseState.Y))
            {
                ToggleItemSelector();
                _previousMouseState = mouseState;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (listRect.Contains(mouseState.X, mouseState.Y))
            {
                int relativeY = mouseState.Y - listRect.Y;
                int visibleIndex = relativeY / RecipeRowHeight;
                int recipeIndex = _recipeScrollOffset + visibleIndex;
                if (recipeIndex >= 0 && recipeIndex < CurrentRecipes.Count)
                {
                    SelectRecipe(recipeIndex);
                    _previousMouseState = mouseState;
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }

            _isCategorySelectorExpanded = false;
            _isItemSelectorExpanded = false;
            _previousMouseState = mouseState;
            return false;
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
            foreach (BackgroundLayer layer in _backgroundLayers)
            {
                layer.Drawable.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    Position.X + layer.Offset.X, Position.Y + layer.Offset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            if (_pixel == null || _font == null)
            {
                return;
            }

            DrawSelector(sprite, GetCategorySelectorRectangle(), SelectedPageLabel, _isCategorySelectorExpanded);
            DrawSelector(sprite, GetItemSelectorRectangle(), SelectedRecipeLabel, _isItemSelectorExpanded && CurrentRecipes.Count > 0);

            if (_isCategorySelectorExpanded)
            {
                DrawCategorySelectorDropDown(sprite);
            }

            if (_isItemSelectorExpanded && CurrentRecipes.Count > 0)
            {
                DrawItemSelectorDropDown(sprite);
            }

            Rectangle listRect = GetRecipeListRectangle();
            DrawPanel(sprite, listRect, new Color(16, 19, 28, 210), new Color(92, 105, 132, 255));

            IReadOnlyList<ItemMakerRecipe> recipes = CurrentRecipes;
            if (recipes.Count == 0)
            {
                sprite.DrawString(_font, "No craftable recipes match this build.", new Vector2(listRect.X + 8, listRect.Y + 8), new Color(230, 230, 230));
                DrawGauge(sprite);
                sprite.DrawString(_font, _statusMessage, new Vector2(Position.X + 18, Position.Y + 321), new Color(230, 230, 230));
                return;
            }

            int visibleRows = Math.Min(VisibleRecipeCount, recipes.Count - _recipeScrollOffset);
            for (int i = 0; i < visibleRows; i++)
            {
                int recipeIndex = _recipeScrollOffset + i;
                ItemMakerRecipe recipe = recipes[recipeIndex];
                bool hiddenUnlocked = !recipe.IsHidden || IsHiddenRecipeUnlocked(recipe);
                Rectangle rowRect = new(listRect.X + 4, listRect.Y + 4 + (i * RecipeRowHeight), listRect.Width - 8, RecipeRowHeight - 4);
                bool selected = recipeIndex == _selectedRecipeIndex;
                DrawPanel(sprite, rowRect,
                    selected ? new Color(55, 88, 126, 210) : new Color(27, 31, 45, 180),
                    selected ? new Color(153, 190, 230, 255) : new Color(66, 74, 95, 255));

                string rowTitle = hiddenUnlocked ? recipe.Title : "Hidden recipe";
                string requirementPrefix = hiddenUnlocked
                    ? recipe.RequiredLevel > 0
                        ? $"Lv {recipe.RequiredLevel}"
                        : $"{_progression.GetFamilyLabel(recipe.Family)} L{GetEffectiveSkillLevel(recipe)}"
                    : "???";
                string outputText = hiddenUnlocked ? $"Output x{recipe.OutputQuantity}" : "Reveal the maker hint to inspect this entry.";
                Color detailColor = hiddenUnlocked ? new Color(199, 211, 229) : new Color(187, 174, 220);
                sprite.DrawString(_font, TruncateToWidth(rowTitle, rowRect.Width - 16), new Vector2(rowRect.X + 8, rowRect.Y + 5), Color.White);
                sprite.DrawString(_font, TruncateToWidth($"{requirementPrefix}  {outputText}", rowRect.Width - 16), new Vector2(rowRect.X + 8, rowRect.Y + 18), detailColor);
            }

            if (recipes.Count > VisibleRecipeCount)
            {
                string scrollText = string.Format(CultureInfo.InvariantCulture, "{0}-{1}/{2}",
                    _recipeScrollOffset + 1,
                    Math.Min(_recipeScrollOffset + VisibleRecipeCount, recipes.Count),
                    recipes.Count);
                sprite.DrawString(_font, scrollText, new Vector2(listRect.Right - 68, listRect.Bottom + 4), new Color(182, 191, 210));
            }

            ItemMakerRecipe selectedRecipe = recipes[Math.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1)];
            bool hiddenRecipeUnlocked = !selectedRecipe.IsHidden || IsHiddenRecipeUnlocked(selectedRecipe);
            Texture2D resultIcon = ResolveItemIcon(selectedRecipe.OutputItemId, selectedRecipe.OutputInventoryType);
            Vector2 detailOrigin = new(Position.X + 18, Position.Y + 185);
            if (hiddenRecipeUnlocked)
            {
                DrawItemIcon(sprite, resultIcon, (int)detailOrigin.X, (int)detailOrigin.Y - 2, 34);
            }

            string targetSectionLabel = selectedRecipe.UsesRandomReward ? "Target Pool" : "Target Item";
            sprite.DrawString(_font, targetSectionLabel, detailOrigin, new Color(255, 223, 153));
            sprite.DrawString(_font, hiddenRecipeUnlocked ? selectedRecipe.Title : "Hidden recipe", detailOrigin + new Vector2(40, 0), Color.White);
            sprite.DrawString(
                _font,
                hiddenRecipeUnlocked ? selectedRecipe.Description : "This entry stays obscured until its hidden maker requirement is revealed.",
                detailOrigin + new Vector2(40, 18),
                new Color(207, 214, 226));

            float y = detailOrigin.Y + 48;
            if (!hiddenRecipeUnlocked)
            {
                sprite.DrawString(_font, "Hidden list entry", new Vector2(detailOrigin.X, y), new Color(214, 196, 255));
                y += 18;

                sprite.DrawString(_font, BuildHiddenRecipeLockHint(selectedRecipe), new Vector2(detailOrigin.X, y), new Color(255, 223, 153));
                y += 18;

                DrawGauge(sprite);
                sprite.DrawString(_font, _statusMessage, new Vector2(Position.X + 18, Position.Y + 321), new Color(230, 230, 230));
                return;
            }

            string masteryText = BuildMasteryDisplayText(selectedRecipe);
            sprite.DrawString(_font, masteryText, new Vector2(detailOrigin.X, y), new Color(153, 210, 255));
            y += 18;

            if (selectedRecipe.IsHidden)
            {
                string hiddenText = BuildHiddenRecipeUnlockSummary(selectedRecipe);
                sprite.DrawString(_font, hiddenText, new Vector2(detailOrigin.X, y), new Color(214, 196, 255));
                y += 18;
            }

            if (selectedRecipe.RequiredLevel > 0 || selectedRecipe.RequiredSkillLevel > 0)
            {
                string reqText = $"Req Lv {selectedRecipe.RequiredLevel}";
                if (selectedRecipe.RequiredSkillLevel > 0)
                {
                    reqText += $"  Mastery {selectedRecipe.RequiredSkillLevel}";
                }

                sprite.DrawString(_font, reqText, new Vector2(detailOrigin.X, y), new Color(255, 223, 153));
                y += 18;
            }

            ItemMakerMaterial[] recipeMaterials = selectedRecipe.Materials
                .Where(static material => ResolveDetailSection(material) == ItemMakerDetailSection.Recipe)
                .ToArray();
            ItemMakerMaterial[] gemMaterials = selectedRecipe.Materials
                .Where(static material => ResolveDetailSection(material) == ItemMakerDetailSection.Gem)
                .ToArray();

            if (recipeMaterials.Length > 0)
            {
                y = DrawSectionHeader(sprite, "Recipe", detailOrigin, y);
                foreach (ItemMakerMaterial material in recipeMaterials)
                {
                    y = DrawMaterialRequirementRow(sprite, detailOrigin, y, material);
                }
            }

            if (gemMaterials.Length > 0)
            {
                y = DrawSectionHeader(sprite, "Gem", detailOrigin, y);
                foreach (ItemMakerMaterial material in gemMaterials)
                {
                    y = DrawMaterialRequirementRow(sprite, detailOrigin, y, material);
                }
            }

            if (selectedRecipe.CatalystItemId > 0)
            {
                y = DrawSectionHeader(sprite, "Catalyst", detailOrigin, y);
                InventoryType catalystType = ResolveInventoryType(selectedRecipe.CatalystItemId);
                y = DrawOwnedRequirementRow(
                    sprite,
                    detailOrigin,
                    y,
                    catalystType,
                    selectedRecipe.CatalystItemId,
                    1,
                    "Catalyst");
            }

            if (selectedRecipe.RequiredItemId > 0)
            {
                y = DrawSectionHeader(sprite, "Unlock Item", detailOrigin, y);
                InventoryType requiredItemType = ResolveInventoryType(selectedRecipe.RequiredItemId);
                y = DrawOwnedRequirementRow(
                    sprite,
                    detailOrigin,
                    y,
                    requiredItemType,
                    selectedRecipe.RequiredItemId,
                    1,
                    "Req Item");
            }

            if (selectedRecipe.RequiredEquipItemId > 0)
            {
                y = DrawSectionHeader(sprite, "Required Equip", detailOrigin, y);
                bool hasEquip = _hasRequiredEquip?.Invoke(selectedRecipe.RequiredEquipItemId) == true;
                y = DrawTextRequirementRow(
                    sprite,
                    detailOrigin,
                    y,
                    $"Req Equip: {GetItemName(selectedRecipe.RequiredEquipItemId)}",
                    hasEquip);
            }

            for (int i = 0; i < selectedRecipe.RequiredQuestStates.Length; i++)
            {
                if (i == 0)
                {
                    y = DrawSectionHeader(sprite, "Required Quest", detailOrigin, y);
                }

                ItemMakerQuestRequirement requirement = selectedRecipe.RequiredQuestStates[i];
                bool satisfied = _matchesQuestRequirement?.Invoke(requirement.QuestId, requirement.RequiredStateValue) == true;
                y = DrawTextRequirementRow(
                    sprite,
                    detailOrigin,
                    y,
                    $"Req Quest: {GetQuestRequirementText(requirement)}",
                    satisfied);
            }

            if (selectedRecipe.MesoCost > 0)
            {
                y = DrawSectionHeader(sprite, "Meso", detailOrigin, y);
                long mesoCount = _inventory?.GetMesoCount() ?? 0L;
                y = DrawTextRequirementRow(
                    sprite,
                    detailOrigin,
                    y,
                    $"Cost: {selectedRecipe.MesoCost:N0} meso",
                    mesoCount >= selectedRecipe.MesoCost);
            }

            y = DrawSectionHeader(sprite, selectedRecipe.UsesRandomReward ? "Random Reward Pool" : "Result", detailOrigin, y);
            string resultText = selectedRecipe.UsesRandomReward
                ? $"Pool size {selectedRecipe.RandomRewards.Length}"
                : $"{GetItemName(selectedRecipe.OutputItemId)} x{selectedRecipe.OutputQuantity}";
            sprite.DrawString(_font, resultText, new Vector2(detailOrigin.X, y), new Color(189, 219, 255));
            y += 18;

            if (selectedRecipe.UsesRandomReward)
            {
                int previewCount = Math.Min(4, selectedRecipe.RandomRewards.Length);
                for (int i = 0; i < previewCount; i++)
                {
                    y = DrawRewardPreviewRow(sprite, detailOrigin, y, selectedRecipe.RandomRewards[i]);
                }
            }

            DrawGauge(sprite);

            Vector2 statusOrigin = new(Position.X + 18, Position.Y + 321);
            sprite.DrawString(_font, _statusMessage, statusOrigin, new Color(230, 230, 230));
        }

        private void DrawGauge(SpriteBatch sprite)
        {
            Rectangle gaugeRect = new(Position.X + _gaugePosition.X, Position.Y + _gaugePosition.Y, 275, 13);
            if (_gaugeBarTexture != null)
            {
                sprite.Draw(_gaugeBarTexture, gaugeRect, Color.White);
            }
            else
            {
                DrawPanel(sprite, gaugeRect, new Color(25, 28, 36, 220), new Color(83, 92, 118));
            }

            float progress = 0f;
            if (_isCrafting)
            {
                progress = MathHelper.Clamp((Environment.TickCount - _craftStartTick) / (float)CraftDurationMs, 0f, 1f);
            }

            int fillWidth = Math.Max(0, (int)Math.Round((gaugeRect.Width - 2) * progress));
            if (fillWidth <= 0)
            {
                return;
            }

            Rectangle fillRect = new(gaugeRect.X + 1, gaugeRect.Y + 1, fillWidth, gaugeRect.Height - 2);
            if (_gaugeFillTexture != null)
            {
                sprite.Draw(_gaugeFillTexture, fillRect, Color.White);
            }
            else
            {
                sprite.Draw(_pixel, fillRect, new Color(114, 201, 117));
            }
        }

        private void DrawItemIcon(SpriteBatch sprite, Texture2D icon, int x, int y, int size)
        {
            if (icon == null)
            {
                return;
            }

            sprite.Draw(icon, new Rectangle(x, y, size, size), Color.White);
        }

        private float DrawSectionHeader(SpriteBatch sprite, string label, Vector2 detailOrigin, float y)
        {
            sprite.DrawString(_font, label, new Vector2(detailOrigin.X, y), new Color(255, 223, 153));
            return y + 18;
        }

        private float DrawMaterialRequirementRow(SpriteBatch sprite, Vector2 detailOrigin, float y, ItemMakerMaterial material)
        {
            int owned = _inventory?.GetItemCount(material.InventoryType, material.ItemId) ?? 0;
            bool satisfied = owned >= material.Quantity;
            DrawItemIcon(sprite, ResolveItemIcon(material.ItemId, material.InventoryType), (int)detailOrigin.X, (int)y - 2, 16);
            sprite.DrawString(
                _font,
                $"{GetItemName(material.ItemId)} {Math.Min(owned, material.Quantity)}/{material.Quantity}",
                new Vector2(detailOrigin.X + 20, y),
                satisfied ? new Color(194, 233, 193) : new Color(240, 155, 155));
            return y + 17;
        }

        private float DrawOwnedRequirementRow(
            SpriteBatch sprite,
            Vector2 detailOrigin,
            float y,
            InventoryType inventoryType,
            int itemId,
            int requiredQuantity,
            string label)
        {
            int owned = _inventory?.GetItemCount(inventoryType, itemId) ?? 0;
            bool satisfied = owned >= requiredQuantity;
            DrawItemIcon(sprite, ResolveItemIcon(itemId, inventoryType), (int)detailOrigin.X, (int)y - 2, 16);
            sprite.DrawString(
                _font,
                $"{label}: {GetItemName(itemId)} {Math.Min(owned, requiredQuantity)}/{requiredQuantity}",
                new Vector2(detailOrigin.X + 20, y),
                satisfied ? new Color(194, 233, 193) : new Color(240, 155, 155));
            return y + 17;
        }

        private float DrawTextRequirementRow(SpriteBatch sprite, Vector2 detailOrigin, float y, string text, bool satisfied)
        {
            sprite.DrawString(
                _font,
                text,
                new Vector2(detailOrigin.X, y),
                satisfied ? new Color(194, 233, 193) : new Color(240, 155, 155));
            return y + 17;
        }

        private float DrawRewardPreviewRow(SpriteBatch sprite, Vector2 detailOrigin, float y, ItemMakerReward reward)
        {
            DrawItemIcon(sprite, ResolveItemIcon(reward.ItemId, ResolveInventoryType(reward.ItemId)), (int)detailOrigin.X, (int)y - 2, 16);
            sprite.DrawString(
                _font,
                $"- {GetItemName(reward.ItemId)} x{reward.Quantity}",
                new Vector2(detailOrigin.X + 20, y),
                new Color(205, 214, 232));
            return y + 17;
        }

        private Texture2D ResolveItemIcon(int itemId, InventoryType inventoryType)
        {
            if (itemId <= 0)
            {
                return null;
            }

            Texture2D inventoryTexture = _inventory?.GetItemTexture(inventoryType, itemId);
            if (inventoryTexture != null)
            {
                return inventoryTexture;
            }

            if (_itemIconCache.TryGetValue(itemId, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D texture = _itemIconProvider?.Invoke(itemId);
            _itemIconCache[itemId] = texture;
            return texture;
        }

        private int GetEffectiveSkillLevel(ItemMakerRecipe recipe)
        {
            return Math.Clamp(_progression?.GetLevel(recipe.Family) ?? 1, 1, ItemMakerProgressionStore.MaxMakerSkillLevel);
        }

        private void ResetRecipeState(ItemMakerProgressionSnapshot progression)
        {
            _discoveredRecipeIds.Clear();
            _unlockedHiddenRecipeIds.Clear();
            if (progression == null)
            {
                return;
            }

            foreach (int outputItemId in progression.DiscoveredRecipeIds)
            {
                if (outputItemId > 0)
                {
                    _discoveredRecipeIds.Add(outputItemId);
                }
            }

            foreach (int outputItemId in progression.UnlockedHiddenRecipeIds)
            {
                if (outputItemId > 0)
                {
                    _unlockedHiddenRecipeIds.Add(outputItemId);
                }
            }
        }

        private string BuildMasteryDisplayText(ItemMakerRecipe recipe)
        {
            int familyLevel = GetEffectiveSkillLevel(recipe);
            int progressTarget = _progression?.GetProgressTarget(recipe.Family) ?? 0;
            if (progressTarget <= 0 || familyLevel >= ItemMakerProgressionStore.MaxMakerSkillLevel)
            {
                return $"{_progression.GetFamilyLabel(recipe.Family)} mastery Lv {familyLevel}/{ItemMakerProgressionStore.MaxMakerSkillLevel}  Craft trait {_makerSkillLevel}";
            }

            int progress = _progression?.GetProgress(recipe.Family) ?? 0;
            return $"{_progression.GetFamilyLabel(recipe.Family)} mastery Lv {familyLevel}/{ItemMakerProgressionStore.MaxMakerSkillLevel}  {progress}/{progressTarget} toward Lv {familyLevel + 1}";
        }

        private void BeginCraft()
        {
            if (_isCrafting)
            {
                _statusMessage = "Crafting already in progress.";
                return;
            }

            if (_inventory == null)
            {
                _statusMessage = "Inventory runtime is unavailable.";
                return;
            }

            if (CurrentRecipes.Count == 0 || _selectedRecipeIndex < 0 || _selectedRecipeIndex >= CurrentRecipes.Count)
            {
                _statusMessage = "Select an item to craft.";
                return;
            }

            ItemMakerRecipe recipe = CurrentRecipes[_selectedRecipeIndex];
            if (!CanCraftRecipe(recipe, out string failureReason))
            {
                _statusMessage = failureReason;
                return;
            }

            _craftingRecipeIndex = _selectedRecipeIndex;
            _craftStartTick = Environment.TickCount;
            _isCrafting = true;
            _statusMessage = $"Crafting {recipe.Title}...";
        }

        private void CancelCraft()
        {
            _isCategorySelectorExpanded = false;
            _isItemSelectorExpanded = false;
            if (_isCrafting)
            {
                _isCrafting = false;
                _craftingRecipeIndex = -1;
                RefreshStatusMessage("Crafting cancelled.");
                return;
            }

            Hide();
        }

        private void CompleteCraft()
        {
            _isCrafting = false;
            IReadOnlyList<ItemMakerRecipe> recipes = CurrentRecipes;
            if (_inventory == null || _craftingRecipeIndex < 0 || _craftingRecipeIndex >= recipes.Count)
            {
                _craftingRecipeIndex = -1;
                RefreshStatusMessage("Crafting ended without a valid recipe.");
                return;
            }

            ItemMakerRecipe recipe = recipes[_craftingRecipeIndex];
            if (!CanCraftRecipe(recipe, out string failureReason))
            {
                _craftingRecipeIndex = -1;
                RefreshStatusMessage(failureReason);
                return;
            }

            if (recipe.MesoCost > 0 && !_inventory.TryConsumeMeso(recipe.MesoCost))
            {
                _craftingRecipeIndex = -1;
                RefreshStatusMessage("Not enough meso for this recipe.");
                return;
            }

            foreach (ItemMakerMaterial material in recipe.Materials)
            {
                _inventory.TryConsumeItem(material.InventoryType, material.ItemId, material.Quantity);
            }

            if (recipe.CatalystItemId > 0)
            {
                InventoryType catalystType = ResolveInventoryType(recipe.CatalystItemId);
                _inventory.TryConsumeItem(catalystType, recipe.CatalystItemId, 1);
            }

            ItemMakerReward reward = ResolveCraftReward(recipe);
            InventoryType rewardInventoryType = ResolveInventoryType(reward.ItemId);
            Texture2D outputTexture = _inventory.GetItemTexture(rewardInventoryType, reward.ItemId) ?? ResolveItemIcon(reward.ItemId, rewardInventoryType);
            _inventory.AddItem(rewardInventoryType, reward.ItemId, outputTexture, reward.Quantity);
            _craftingRecipeIndex = -1;
            RebuildVisiblePages();
            RefreshStatusMessage($"Created {GetItemName(reward.ItemId)} x{reward.Quantity}.");
            CraftCompleted?.Invoke(new ItemMakerCraftResult
            {
                Family = recipe.Family,
                IsHiddenRecipe = recipe.IsHidden,
                RecipeOutputItemId = recipe.OutputItemId,
                CraftedItemId = reward.ItemId,
                CraftedQuantity = reward.Quantity
            });
        }

        private bool CanCraftRecipe(ItemMakerRecipe recipe, out string failureReason)
        {
            if (recipe.IsHidden && !IsHiddenRecipeUnlocked(recipe))
            {
                failureReason = BuildHiddenRecipeFailureReason(recipe);
                return false;
            }

            if (_inventory == null)
            {
                failureReason = "Inventory runtime is unavailable.";
                return false;
            }

            if (recipe.RequiredLevel > 0 && _characterLevel < recipe.RequiredLevel)
            {
                failureReason = $"Requires level {recipe.RequiredLevel}.";
                return false;
            }

            if (recipe.RequiredSkillLevel > 0 && GetEffectiveSkillLevel(recipe) < recipe.RequiredSkillLevel)
            {
                failureReason = $"Requires {_progression.GetFamilyLabel(recipe.Family)} mastery {recipe.RequiredSkillLevel}.";
                return false;
            }

            foreach (ItemMakerMaterial material in recipe.Materials)
            {
                if (_inventory.GetItemCount(material.InventoryType, material.ItemId) < material.Quantity)
                {
                    failureReason = "Not enough materials for this recipe.";
                    return false;
                }
            }

            if (recipe.CatalystItemId > 0)
            {
                InventoryType catalystType = ResolveInventoryType(recipe.CatalystItemId);
                if (_inventory.GetItemCount(catalystType, recipe.CatalystItemId) < 1)
                {
                    failureReason = "Missing required catalyst.";
                    return false;
                }
            }

            if (recipe.RequiredItemId > 0)
            {
                InventoryType requiredItemType = ResolveInventoryType(recipe.RequiredItemId);
                if (_inventory.GetItemCount(requiredItemType, recipe.RequiredItemId) < 1)
                {
                    failureReason = "Missing required crafting item.";
                    return false;
                }
            }

            if (recipe.RequiredEquipItemId > 0)
            {
                if (_hasRequiredEquip == null)
                {
                    failureReason = "Equipment state is unavailable.";
                    return false;
                }

                if (!_hasRequiredEquip(recipe.RequiredEquipItemId))
                {
                    failureReason = $"Requires equipped item {GetItemName(recipe.RequiredEquipItemId)}.";
                    return false;
                }
            }

            for (int i = 0; i < recipe.RequiredQuestStates.Length; i++)
            {
                ItemMakerQuestRequirement requirement = recipe.RequiredQuestStates[i];
                if (_matchesQuestRequirement == null)
                {
                    failureReason = "Quest progress is unavailable.";
                    return false;
                }

                if (!_matchesQuestRequirement(requirement.QuestId, requirement.RequiredStateValue))
                {
                    failureReason = $"Requires quest progress: {GetQuestRequirementText(requirement)}.";
                    return false;
                }
            }

            if (recipe.MesoCost > 0 && _inventory.GetMesoCount() < recipe.MesoCost)
            {
                failureReason = "Not enough meso for this recipe.";
                return false;
            }

            if (!CanAcceptCraftReward(recipe))
            {
                failureReason = "Inventory is full for the crafting result.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private void RefreshStatusMessage(string overrideMessage = null)
        {
            if (!string.IsNullOrWhiteSpace(overrideMessage))
            {
                _statusMessage = overrideMessage;
                return;
            }

            if (_pages.Count == 0)
            {
                _statusMessage = "No client crafting recipes are available for this build.";
                return;
            }

            if (CurrentRecipes.Count == 0 || _selectedRecipeIndex < 0 || _selectedRecipeIndex >= CurrentRecipes.Count)
            {
                _statusMessage = "Select an item to craft.";
                return;
            }

            ItemMakerRecipe recipe = CurrentRecipes[_selectedRecipeIndex];
            _statusMessage = CanCraftRecipe(recipe, out string failureReason)
                ? "Ready to craft."
                : failureReason;
        }

        private string BuildHiddenRecipeLockHint(ItemMakerRecipe recipe)
        {
            if (HasExplicitHiddenUnlockGate(recipe))
            {
                return "Unlock this recipe by meeting its WZ-backed maker hint once.";
            }

            return "This hidden recipe locally reveals once its WZ level or mastery gate is met.";
        }

        private string BuildHiddenRecipeUnlockSummary(ItemMakerRecipe recipe)
        {
            if (recipe == null)
            {
                return "Hidden recipe entry.";
            }

            if (recipe.RequiredItemId > 0)
            {
                return $"Hidden recipe unlocked by {GetItemName(recipe.RequiredItemId)}.";
            }

            if (recipe.RequiredEquipItemId > 0)
            {
                return $"Hidden recipe unlocked by equipping {GetItemName(recipe.RequiredEquipItemId)}.";
            }

            if (recipe.RequiredQuestStates.Length > 0)
            {
                return "Hidden recipe unlocked by meeting its quest state.";
            }

            return "Hidden recipe unlocked by local level/mastery reveal.";
        }

        private string BuildHiddenRecipeFailureReason(ItemMakerRecipe recipe)
        {
            if (HasExplicitHiddenUnlockGate(recipe))
            {
                return "This hidden maker recipe has not been revealed yet.";
            }

            return "This hidden maker recipe is waiting for its level or mastery reveal.";
        }

        private Rectangle GetCategorySelectorRectangle()
        {
            return new Rectangle(Position.X + 18, Position.Y + 23, 112, 18);
        }

        private Rectangle GetItemSelectorRectangle()
        {
            return new Rectangle(Position.X + 136, Position.Y + 23, 140, 18);
        }

        private Rectangle GetRecipeListRectangle()
        {
            return new Rectangle(Position.X + 18, Position.Y + 43, 258, (VisibleRecipeCount * RecipeRowHeight) + 8);
        }

        private Rectangle GetCategorySelectorDropDownRectangle()
        {
            return new Rectangle(GetCategorySelectorRectangle().X, GetCategorySelectorRectangle().Bottom + 2, 148, _pages.Count * SelectorDropdownRowHeight);
        }

        private Rectangle GetItemSelectorDropDownRectangle()
        {
            return new Rectangle(GetItemSelectorRectangle().X, GetItemSelectorRectangle().Bottom + 2, 170, GetVisibleRecipeSelectorCount() * SelectorDropdownRowHeight);
        }

        private int GetVisibleRecipeSelectorCount()
        {
            return Math.Min(VisibleRecipeCount, CurrentRecipes.Count);
        }

        private string SelectedPageLabel =>
            _pages.Count == 0 || _selectedPageIndex < 0 || _selectedPageIndex >= _pages.Count
                ? "Select"
                : _pages[_selectedPageIndex].Label;

        private string SelectedRecipeLabel =>
            CurrentRecipes.Count == 0 || _selectedRecipeIndex < 0 || _selectedRecipeIndex >= CurrentRecipes.Count
                ? "Select Item"
                : GetRecipeSelectorLabel(CurrentRecipes[_selectedRecipeIndex]);

        private void ScrollRecipes(int delta)
        {
            if (CurrentRecipes.Count <= VisibleRecipeCount)
            {
                _recipeScrollOffset = 0;
                return;
            }

            int maxOffset = Math.Max(0, CurrentRecipes.Count - VisibleRecipeCount);
            _recipeScrollOffset = Math.Clamp(_recipeScrollOffset + delta, 0, maxOffset);
        }

        private void EnsureSelectedRecipeVisible()
        {
            if (_selectedRecipeIndex < _recipeScrollOffset)
            {
                _recipeScrollOffset = _selectedRecipeIndex;
            }
            else if (_selectedRecipeIndex >= _recipeScrollOffset + VisibleRecipeCount)
            {
                _recipeScrollOffset = _selectedRecipeIndex - VisibleRecipeCount + 1;
            }
        }

        private void ToggleCategorySelector()
        {
            if (_pages.Count <= 1)
            {
                _isCategorySelectorExpanded = false;
                return;
            }

            _isCategorySelectorExpanded = !_isCategorySelectorExpanded;
            if (_isCategorySelectorExpanded)
            {
                _isItemSelectorExpanded = false;
            }
        }

        private void ToggleItemSelector()
        {
            if (CurrentRecipes.Count == 0)
            {
                _isItemSelectorExpanded = false;
                return;
            }

            _isItemSelectorExpanded = !_isItemSelectorExpanded;
            if (_isItemSelectorExpanded)
            {
                _isCategorySelectorExpanded = false;
            }
        }

        private void SelectRecipePage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= _pages.Count)
            {
                return;
            }

            _selectedPageIndex = pageIndex;
            _selectedRecipeIndex = 0;
            _recipeScrollOffset = 0;
            _craftingRecipeIndex = -1;
            _isCrafting = false;
            _isCategorySelectorExpanded = false;
            _isItemSelectorExpanded = false;
            RefreshStatusMessage($"Viewing {_pages[_selectedPageIndex].Label} recipes.");
        }

        private void SelectRecipe(int recipeIndex)
        {
            if (recipeIndex < 0 || recipeIndex >= CurrentRecipes.Count)
            {
                return;
            }

            _selectedRecipeIndex = recipeIndex;
            EnsureSelectedRecipeVisible();
            _isCategorySelectorExpanded = false;
            _isItemSelectorExpanded = false;
            RefreshStatusMessage();
        }

        private void DrawSelector(SpriteBatch sprite, Rectangle rect, string label, bool expanded)
        {
            DrawPanel(sprite, rect, new Color(29, 33, 44, 220), new Color(103, 113, 139, 255));
            sprite.DrawString(_font, TruncateToWidth(label, rect.Width - 20), new Vector2(rect.X + 6, rect.Y + 1), new Color(239, 233, 213));
            sprite.DrawString(_font, expanded ? "^" : "v", new Vector2(rect.Right - 13, rect.Y + 1), new Color(239, 233, 213));
        }

        private void DrawCategorySelectorDropDown(SpriteBatch sprite)
        {
            Rectangle dropDownRect = GetCategorySelectorDropDownRectangle();
            DrawPanel(sprite, dropDownRect, new Color(17, 20, 30, 235), new Color(103, 113, 139, 255));

            for (int i = 0; i < _pages.Count; i++)
            {
                Rectangle optionRect = new(dropDownRect.X + 2, dropDownRect.Y + (i * SelectorDropdownRowHeight) + 2, dropDownRect.Width - 4, SelectorDropdownRowHeight - 2);
                bool selected = i == _selectedPageIndex;
                DrawPanel(sprite, optionRect,
                    selected ? new Color(55, 88, 126, 230) : new Color(27, 31, 45, 205),
                    selected ? new Color(153, 190, 230, 255) : new Color(66, 74, 95, 255));

                sprite.DrawString(_font, TruncateToWidth(_pages[i].Label, optionRect.Width - 12), new Vector2(optionRect.X + 6, optionRect.Y + 2), Color.White);
            }
        }

        private void DrawItemSelectorDropDown(SpriteBatch sprite)
        {
            Rectangle dropDownRect = GetItemSelectorDropDownRectangle();
            DrawPanel(sprite, dropDownRect, new Color(17, 20, 30, 235), new Color(103, 113, 139, 255));

            int visibleCount = GetVisibleRecipeSelectorCount();
            for (int i = 0; i < visibleCount; i++)
            {
                int recipeIndex = _recipeScrollOffset + i;
                if (recipeIndex >= CurrentRecipes.Count)
                {
                    break;
                }

                Rectangle optionRect = new(dropDownRect.X + 2, dropDownRect.Y + (i * SelectorDropdownRowHeight) + 2, dropDownRect.Width - 4, SelectorDropdownRowHeight - 2);
                bool selected = recipeIndex == _selectedRecipeIndex;
                DrawPanel(sprite, optionRect,
                    selected ? new Color(55, 88, 126, 230) : new Color(27, 31, 45, 205),
                    selected ? new Color(153, 190, 230, 255) : new Color(66, 74, 95, 255));

                sprite.DrawString(_font, TruncateToWidth(GetRecipeSelectorLabel(CurrentRecipes[recipeIndex]), optionRect.Width - 12), new Vector2(optionRect.X + 6, optionRect.Y + 2), Color.White);
            }
        }

        private string GetRecipeSelectorLabel(ItemMakerRecipe recipe)
        {
            return recipe != null && recipe.IsHidden && !IsHiddenRecipeUnlocked(recipe)
                ? "???"
                : recipe?.Title ?? "Select Item";
        }

        private void DrawPanel(SpriteBatch sprite, Rectangle rect, Color fill, Color border)
        {
            sprite.Draw(_pixel, rect, fill);
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
            sprite.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);
        }

        private string TruncateToWidth(string text, float maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text) || _font == null)
            {
                return text ?? string.Empty;
            }

            if (_font.MeasureString(text).X <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            string trimmed = text;
            while (trimmed.Length > 1 && _font.MeasureString(trimmed + ellipsis).X > maxWidth)
            {
                trimmed = trimmed[..^1];
            }

            return trimmed + ellipsis;
        }

        private static string GetItemName(int itemId)
        {
            return HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo) &&
                   !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : $"Item #{itemId}";
        }

        private static InventoryType ResolveInventoryType(int itemId)
        {
            int typeBucket = itemId / 1000000;
            return typeBucket switch
            {
                1 => InventoryType.EQUIP,
                2 => InventoryType.USE,
                3 => InventoryType.SETUP,
                4 => InventoryType.ETC,
                5 => InventoryType.CASH,
                _ => InventoryType.NONE
            };
        }

        private static string CreateRecipeDescription(int itemId, bool usesRandomReward)
        {
            return usesRandomReward
                ? "Client ItemMake random maker branch."
                : $"Client ItemMake recipe for {GetItemName(itemId)}.";
        }

        private static ItemMakerDetailSection ResolveDetailSection(ItemMakerMaterial material)
        {
            if (material == null)
            {
                return ItemMakerDetailSection.Recipe;
            }

            int fourDigitCategory = material.ItemId / 10000;
            return material.InventoryType == InventoryType.ETC && fourDigitCategory is 402 or 425 or 426
                ? ItemMakerDetailSection.Gem
                : ItemMakerDetailSection.Recipe;
        }

        private static string ResolveLaunchSearchTerm(string npcFunctionText)
        {
            if (string.IsNullOrWhiteSpace(npcFunctionText))
            {
                return null;
            }

            if (npcFunctionText.IndexOf("glove maker", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "glove";
            }

            if (npcFunctionText.IndexOf("shoemaker", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "shoe";
            }

            if (npcFunctionText.IndexOf("item maker", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "etc";
            }

            if (npcFunctionText.IndexOf("toy maker", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "toy";
            }

            return null;
        }

        private static MakerLaunchFilter ResolveLaunchFilter(string npcFunctionText)
        {
            if (string.IsNullOrWhiteSpace(npcFunctionText))
            {
                return MakerLaunchFilter.None;
            }

            if (npcFunctionText.IndexOf("glove maker", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MakerLaunchFilter.GloveMaker;
            }

            if (npcFunctionText.IndexOf("shoemaker", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MakerLaunchFilter.ShoeMaker;
            }

            if (npcFunctionText.IndexOf("toy maker", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MakerLaunchFilter.ToyMaker;
            }

            if (npcFunctionText.IndexOf("item maker", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MakerLaunchFilter.ItemMaker;
            }

            return MakerLaunchFilter.None;
        }

        private static string GetQuestRequirementText(ItemMakerQuestRequirement requirement)
        {
            if (requirement == null)
            {
                return "Unknown quest";
            }

            string stateLabel = requirement.RequiredStateValue switch
            {
                (int)QuestStateType.Not_Started => "not started",
                (int)QuestStateType.Started => "started",
                (int)QuestStateType.Completed => "completed",
                (int)QuestStateType.PartyQuest => "progressed",
                _ => $"state {requirement.RequiredStateValue}"
            };
            return $"Quest #{requirement.QuestId} ({stateLabel})";
        }

        private void LoadRecipes()
        {
            _allRecipes.Clear();
            _pages.Clear();
            _selectedPageIndex = 0;
            _selectedRecipeIndex = 0;
            _recipeScrollOffset = 0;

            if (!TryLoadClientRecipes())
            {
                PopulateFallbackRecipes();
            }

            RebuildVisiblePages();
        }

        private bool TryLoadClientRecipes()
        {
            WzImage itemMakeImage = HaCreator.Program.FindImage("Etc", "ItemMake.img");
            if (itemMakeImage == null)
            {
                return false;
            }

            foreach (int bucketKey in ClientRecipeBuckets)
            {
                WzSubProperty recipeBucket = itemMakeImage[bucketKey.ToString(CultureInfo.InvariantCulture)] as WzSubProperty;
                if (recipeBucket == null)
                {
                    continue;
                }

                foreach (WzImageProperty recipeProperty in recipeBucket.WzProperties)
                {
                    if (recipeProperty is not WzSubProperty recipeData || !int.TryParse(recipeData.Name, out int outputItemId))
                    {
                        continue;
                    }

                    ItemMakerRecipe recipe = CreateRecipeFromWz(recipeData, outputItemId, bucketKey);
                    if (recipe != null)
                    {
                        _allRecipes.Add(recipe);
                    }
                }
            }

            return _allRecipes.Count > 0;
        }

        private static ItemMakerRecipe CreateRecipeFromWz(WzSubProperty recipeData, int outputItemId, int bucketKey)
        {
            WzSubProperty recipeMaterialsProperty = recipeData["recipe"] as WzSubProperty;
            if (recipeMaterialsProperty == null)
            {
                return null;
            }

            List<ItemMakerMaterial> materials = new();
            foreach (WzImageProperty materialProperty in recipeMaterialsProperty.WzProperties)
            {
                if (materialProperty is not WzSubProperty materialData)
                {
                    continue;
                }

                int materialItemId = (materialData["item"] as WzIntProperty)?.Value ?? 0;
                int count = (materialData["count"] as WzIntProperty)?.Value ?? 0;
                if (materialItemId <= 0 || count <= 0)
                {
                    continue;
                }

                InventoryType inventoryType = ResolveInventoryType(materialItemId);
                if (inventoryType == InventoryType.NONE)
                {
                    continue;
                }

                materials.Add(new ItemMakerMaterial
                {
                    InventoryType = inventoryType,
                    ItemId = materialItemId,
                    Quantity = count
                });
            }

            if (materials.Count == 0)
            {
                return null;
            }

            List<ItemMakerReward> randomRewards = new();
            List<ItemMakerQuestRequirement> requiredQuestStates = new();
            if (recipeData["randomReward"] is WzSubProperty randomRewardProperty)
            {
                foreach (WzImageProperty rewardProperty in randomRewardProperty.WzProperties)
                {
                    if (rewardProperty is not WzSubProperty rewardData)
                    {
                        continue;
                    }

                    int rewardItemId = (rewardData["item"] as WzIntProperty)?.Value ?? 0;
                    int rewardQuantity = (rewardData["itemNum"] as WzIntProperty)?.Value ?? 1;
                    int probability = (rewardData["prob"] as WzIntProperty)?.Value ?? 0;
                    if (rewardItemId <= 0 || rewardQuantity <= 0)
                    {
                        continue;
                    }

                    randomRewards.Add(new ItemMakerReward
                    {
                        ItemId = rewardItemId,
                        Quantity = rewardQuantity,
                        ProbabilityWeight = Math.Max(1, probability)
                    });
                }
            }

            if (recipeData["reqQuest"] is WzSubProperty reqQuestProperty)
            {
                foreach (WzImageProperty questProperty in reqQuestProperty.WzProperties)
                {
                    if (!int.TryParse(questProperty.Name, out int questId))
                    {
                        continue;
                    }

                    int requiredStateValue = (questProperty as WzIntProperty)?.Value ?? 0;
                    if (questId <= 0)
                    {
                        continue;
                    }

                    requiredQuestStates.Add(new ItemMakerQuestRequirement
                    {
                        QuestId = questId,
                        RequiredStateValue = requiredStateValue
                    });
                }
            }

            int categoryKey = GetCategoryKey(outputItemId);
            bool usesRandomReward = randomRewards.Count > 0;
            int outputQuantity = (recipeData["itemNum"] as WzIntProperty)?.Value ?? 1;
            int requiredItemId = Math.Max(0, (recipeData["reqItem"] as WzIntProperty)?.Value ?? 0);
            bool isHidden = ((recipeData["hide"] as WzIntProperty)?.Value ?? 0) != 0;

            return new ItemMakerRecipe
            {
                BucketKey = bucketKey,
                CategoryKey = categoryKey,
                Family = ResolveRecipeFamily(outputItemId, categoryKey, ResolveInventoryType(outputItemId), GetItemName(outputItemId)),
                IsHidden = isHidden,
                CategoryLabel = ResolveCategoryLabel(categoryKey, outputItemId),
                Title = GetItemName(outputItemId),
                Description = CreateRecipeDescription(outputItemId, usesRandomReward),
                OutputInventoryType = ResolveInventoryType(outputItemId),
                OutputItemId = outputItemId,
                OutputQuantity = Math.Max(1, outputQuantity),
                RequiredLevel = (recipeData["reqLevel"] as WzIntProperty)?.Value ?? 0,
                RequiredSkillLevel = (recipeData["reqSkillLevel"] as WzIntProperty)?.Value ?? 0,
                RequiredItemId = requiredItemId,
                RequiredEquipItemId = Math.Max(0, (recipeData["reqEquip"] as WzIntProperty)?.Value ?? 0),
                MesoCost = Math.Max(0, (recipeData["meso"] as WzIntProperty)?.Value ?? 0),
                CatalystItemId = Math.Max(0, (recipeData["catalyst"] as WzIntProperty)?.Value ?? 0),
                UsesRandomReward = usesRandomReward,
                Materials = materials.ToArray(),
                RandomRewards = randomRewards.ToArray(),
                RequiredQuestStates = requiredQuestStates.ToArray()
            };
        }

        private static int GetCategoryKey(int itemId)
        {
            int fourDigitCategory = itemId / 10000;
            if (fourDigitCategory == 425 || fourDigitCategory == 426)
            {
                return fourDigitCategory;
            }

            int groupedCategory = (fourDigitCategory / 100) * 100;
            if (groupedCategory == 200 || groupedCategory == 300 || groupedCategory == 400)
            {
                return groupedCategory;
            }

            return fourDigitCategory;
        }

        private static ItemMakerRecipeFamily ResolveRecipeFamily(int outputItemId, int categoryKey, InventoryType outputInventoryType, string title)
        {
            int fourDigitCategory = outputItemId / 10000;
            if (fourDigitCategory == 108)
            {
                return ItemMakerRecipeFamily.Gloves;
            }

            if (fourDigitCategory == 107)
            {
                return ItemMakerRecipeFamily.Shoes;
            }

            if (outputInventoryType == InventoryType.SETUP ||
                (!string.IsNullOrWhiteSpace(title) && title.IndexOf("toy", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return ItemMakerRecipeFamily.Toys;
            }

            return ItemMakerRecipeFamily.Generic;
        }

        private static int GetCategorySortOrder(int categoryKey)
        {
            return categoryKey switch
            {
                HiddenCategoryKey => 995,
                426 => 20,
                425 => 30,
                200 => 500,
                300 => 510,
                400 => 520,
                _ => 100 + categoryKey
            };
        }

        private static string ResolveCategoryLabel(int categoryKey, int sampleItemId)
        {
            if (categoryKey == 425)
            {
                return "Monster Crystal";
            }

            if (categoryKey == 426)
            {
                return "Gem";
            }

            if (categoryKey == 200)
            {
                return "Use";
            }

            if (categoryKey == 300)
            {
                return "Setup";
            }

            if (categoryKey == 400)
            {
                return "Etc";
            }

            if (HaCreator.Program.InfoManager?.ItemNameCache != null &&
                HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(sampleItemId, out Tuple<string, string, string> itemInfo) &&
                !string.IsNullOrWhiteSpace(itemInfo?.Item1))
            {
                return itemInfo.Item1.Trim();
            }

            return $"Category {categoryKey}";
        }

        private void RebuildVisiblePages()
        {
            int previousCategoryKey = _pages.Count > 0 && _selectedPageIndex >= 0 && _selectedPageIndex < _pages.Count
                ? _pages[_selectedPageIndex].CategoryKey
                : -1;
            int previousRecipeId = CurrentRecipes.Count > 0 && _selectedRecipeIndex >= 0 && _selectedRecipeIndex < CurrentRecipes.Count
                ? CurrentRecipes[_selectedRecipeIndex].OutputItemId
                : 0;

            var allowedBuckets = GetAllowedBucketKeys(_characterJobId);
            List<ItemMakerRecipe> allowedRecipes = _allRecipes
                .Where(recipe => allowedBuckets.Contains(recipe.BucketKey))
                .ToList();

            List<ItemMakerRecipe> launchFilteredRecipes = ApplyLaunchFilter(allowedRecipes);
            SyncDiscoveredRecipes(launchFilteredRecipes.Where(static recipe => !recipe.IsHidden));
            SyncUnlockedHiddenRecipes(launchFilteredRecipes.Where(static recipe => recipe.IsHidden));

            List<ItemMakerRecipe> normalRecipes = launchFilteredRecipes
                .Where(ShouldExposeRecipeInSelector)
                .Where(static recipe => !recipe.IsHidden)
                .ToList();
            List<ItemMakerRecipe> hiddenRecipes = launchFilteredRecipes
                .Where(static recipe => recipe.IsHidden)
                .ToList();

            _pages.Clear();
            if (normalRecipes.Count > 0 || hiddenRecipes.Count > 0)
            {
                if (normalRecipes.Count > 0)
                {
                    AddPage(AllCategoryKey, -1000, "All", normalRecipes);
                }

                Dictionary<int, List<ItemMakerRecipe>> groupedRecipes = normalRecipes
                    .GroupBy(recipe => recipe.CategoryKey)
                    .ToDictionary(group => group.Key, group => group.ToList());

                AddCategoryPageIfPresent(groupedRecipes, 426, -900, "Gem");
                AddCategoryPageIfPresent(groupedRecipes, 425, -890, "Monster Crystal");

                foreach (IGrouping<int, ItemMakerRecipe> group in normalRecipes
                             .Where(recipe => recipe.CategoryKey is not 200 and not 300 and not 400 and not 425 and not 426)
                             .GroupBy(recipe => recipe.CategoryKey))
                {
                    AddPage(group.Key, GetCategorySortOrder(group.Key), group.First().CategoryLabel, group);
                }

                AddCategoryPageIfPresent(groupedRecipes, 200, 900, "Use");
                AddCategoryPageIfPresent(groupedRecipes, 300, 910, "Setup");
                AddCategoryPageIfPresent(groupedRecipes, 400, 920, "Etc");

                if (hiddenRecipes.Count > 0)
                {
                    AddPage(HiddenCategoryKey, GetCategorySortOrder(HiddenCategoryKey), "???", hiddenRecipes);
                }
            }

            if (_pages.Count == 0)
            {
                _selectedPageIndex = 0;
                _selectedRecipeIndex = 0;
                _recipeScrollOffset = 0;
                _isCategorySelectorExpanded = false;
                _isItemSelectorExpanded = false;
                RefreshStatusMessage();
                return;
            }

            int pageIndex = _pages.FindIndex(page => page.CategoryKey == previousCategoryKey);
            _selectedPageIndex = pageIndex >= 0 ? pageIndex : 0;

            IReadOnlyList<ItemMakerRecipe> currentRecipes = CurrentRecipes;
            int recipeIndex = currentRecipes.ToList().FindIndex(recipe => recipe.OutputItemId == previousRecipeId);
            _selectedRecipeIndex = recipeIndex >= 0 ? recipeIndex : 0;
            EnsureSelectedRecipeVisible();
            _isCategorySelectorExpanded = false;
            _isItemSelectorExpanded = false;

            FocusLaunchContext();
            RefreshStatusMessage();
        }

        private List<ItemMakerRecipe> ApplyLaunchFilter(List<ItemMakerRecipe> visibleRecipes)
        {
            if (_launchFilter == MakerLaunchFilter.None || visibleRecipes.Count == 0)
            {
                return visibleRecipes;
            }

            List<ItemMakerRecipe> filteredRecipes = visibleRecipes
                .Where(IsRecipeVisibleForLaunchFilter)
                .ToList();

            return filteredRecipes.Count > 0
                ? filteredRecipes
                : visibleRecipes;
        }

        private bool IsRecipeVisibleForLaunchFilter(ItemMakerRecipe recipe)
        {
            int fourDigitCategory = recipe.OutputItemId / 10000;
            return _launchFilter switch
            {
                MakerLaunchFilter.ItemMaker => recipe.OutputInventoryType != InventoryType.EQUIP || recipe.CategoryKey is 425 or 426,
                MakerLaunchFilter.GloveMaker => fourDigitCategory == 108,
                MakerLaunchFilter.ShoeMaker => fourDigitCategory == 107,
                MakerLaunchFilter.ToyMaker => recipe.OutputInventoryType == InventoryType.SETUP || recipe.Title.IndexOf("toy", StringComparison.OrdinalIgnoreCase) >= 0,
                _ => true
            };
        }

        private void AddCategoryPageIfPresent(
            IReadOnlyDictionary<int, List<ItemMakerRecipe>> groupedRecipes,
            int categoryKey,
            int sortOrder,
            string label)
        {
            if (groupedRecipes.TryGetValue(categoryKey, out List<ItemMakerRecipe> recipes) && recipes.Count > 0)
            {
                AddPage(categoryKey, sortOrder, label, recipes);
            }
        }

        private void AddPage(int categoryKey, int sortOrder, string label, IEnumerable<ItemMakerRecipe> recipes)
        {
            ItemMakerPage page = new()
            {
                CategoryKey = categoryKey,
                SortOrder = sortOrder,
                Label = label
            };
            page.Recipes.AddRange(recipes);
            if (page.Recipes.Count > 0)
            {
                _pages.Add(page);
            }
        }

        private static HashSet<int> GetAllowedBucketKeys(int jobId)
        {
            HashSet<int> buckets = new() { 0 };
            int jobGroup = Math.Abs(jobId % 1000) / 100;
            if (jobGroup == 9)
            {
                buckets.Add(1);
                buckets.Add(2);
                buckets.Add(4);
                buckets.Add(8);
                buckets.Add(16);
                return buckets;
            }

            if (jobGroup is >= 1 and <= 5)
            {
                buckets.Add(1 << (jobGroup - 1));
            }

            return buckets;
        }

        private bool ShouldExposeRecipeInSelector(ItemMakerRecipe recipe)
        {
            if (recipe.IsHidden)
            {
                return IsHiddenRecipeUnlocked(recipe);
            }

            if (!PassesPersistentRecipeGate(recipe))
            {
                return false;
            }

            return _discoveredRecipeIds.Contains(recipe.OutputItemId) || PassesTransientDiscoveryGate(recipe);
        }

        private bool PassesPersistentRecipeGate(ItemMakerRecipe recipe)
        {
            if (recipe.RequiredSkillLevel > 0 && GetEffectiveSkillLevel(recipe) < recipe.RequiredSkillLevel)
            {
                return false;
            }

            if (recipe.RequiredLevel > 0 && _characterLevel < recipe.RequiredLevel)
            {
                return false;
            }

            return true;
        }

        private bool PassesTransientDiscoveryGate(ItemMakerRecipe recipe)
        {
            if (recipe.RequiredItemId > 0)
            {
                InventoryType requiredItemType = ResolveInventoryType(recipe.RequiredItemId);
                if ((_inventory?.GetItemCount(requiredItemType, recipe.RequiredItemId) ?? 0) <= 0)
                {
                    return false;
                }
            }

            if (recipe.RequiredEquipItemId > 0 && _hasRequiredEquip?.Invoke(recipe.RequiredEquipItemId) != true)
            {
                return false;
            }

            for (int i = 0; i < recipe.RequiredQuestStates.Length; i++)
            {
                ItemMakerQuestRequirement requirement = recipe.RequiredQuestStates[i];
                if (_matchesQuestRequirement?.Invoke(requirement.QuestId, requirement.RequiredStateValue) != true)
                {
                    return false;
                }
            }

            return true;
        }

        private void SyncDiscoveredRecipes(IEnumerable<ItemMakerRecipe> recipes)
        {
            List<int> newlyDiscoveredIds = null;
            foreach (ItemMakerRecipe recipe in recipes)
            {
                if (!PassesPersistentRecipeGate(recipe) || !PassesTransientDiscoveryGate(recipe))
                {
                    continue;
                }

                if (_discoveredRecipeIds.Add(recipe.OutputItemId))
                {
                    newlyDiscoveredIds ??= new List<int>();
                    newlyDiscoveredIds.Add(recipe.OutputItemId);
                }
            }

            if (newlyDiscoveredIds?.Count > 0)
            {
                RecipesDiscovered?.Invoke(newlyDiscoveredIds);
            }
        }

        private void SyncUnlockedHiddenRecipes(IEnumerable<ItemMakerRecipe> recipes)
        {
            List<int> newlyUnlockedIds = null;
            foreach (ItemMakerRecipe recipe in recipes)
            {
                if (!PassesHiddenRecipeUnlockGate(recipe))
                {
                    continue;
                }

                if (_unlockedHiddenRecipeIds.Add(recipe.OutputItemId))
                {
                    newlyUnlockedIds ??= new List<int>();
                    newlyUnlockedIds.Add(recipe.OutputItemId);
                }
            }

            if (newlyUnlockedIds?.Count > 0)
            {
                HiddenRecipesUnlocked?.Invoke(newlyUnlockedIds);
            }
        }

        private bool PassesHiddenRecipeUnlockGate(ItemMakerRecipe recipe)
        {
            if (!recipe.IsHidden)
            {
                return false;
            }

            // The client keeps hidden maker targets in a dedicated list rather than letting them
            // fall out of the selector sweep. Locally reveal explicit hint-gated entries when the
            // WZ auth gate passes, and let hide-only rows surface once their persistent level or
            // mastery gate is met so they do not stay permanently obscured without the server
            // session model that normally owns those unlock decisions.
            bool passesPersistentGate = PassesPersistentRecipeGate(recipe);
            bool passesTransientGate = PassesTransientDiscoveryGate(recipe);
            return ItemMakerHiddenRecipeRevealPolicy.ShouldRevealLocally(
                recipe.IsHidden,
                passesPersistentGate,
                passesTransientGate,
                HasExplicitHiddenUnlockGate(recipe));
        }

        private bool IsHiddenRecipeUnlocked(ItemMakerRecipe recipe)
        {
            return recipe.IsHidden && _unlockedHiddenRecipeIds.Contains(recipe.OutputItemId);
        }

        private static bool HasExplicitHiddenUnlockGate(ItemMakerRecipe recipe)
        {
            return recipe != null
                   && (recipe.RequiredItemId > 0
                       || recipe.RequiredEquipItemId > 0
                       || recipe.RequiredQuestStates.Length > 0);
        }

        private void FocusLaunchContext()
        {
            if (_pages.Count == 0)
            {
                return;
            }

            string searchTerm = ResolveLaunchSearchTerm(_launchContextLabel);
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return;
            }

            for (int i = 0; i < _pages.Count; i++)
            {
                ItemMakerPage page = _pages[i];
                if (page.Label.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _selectedPageIndex = i;
                    _selectedRecipeIndex = 0;
                    _recipeScrollOffset = 0;
                    return;
                }

                int recipeIndex = page.Recipes.FindIndex(recipe => recipe.Title.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0);
                if (recipeIndex >= 0)
                {
                    _selectedPageIndex = i;
                    _selectedRecipeIndex = recipeIndex;
                    EnsureSelectedRecipeVisible();
                    return;
                }
            }
        }

        private ItemMakerReward ResolveCraftReward(ItemMakerRecipe recipe)
        {
            if (!recipe.UsesRandomReward || recipe.RandomRewards.Length == 0)
            {
                return new ItemMakerReward
                {
                    ItemId = recipe.OutputItemId,
                    Quantity = recipe.OutputQuantity,
                    ProbabilityWeight = 1
                };
            }

            int totalWeight = 0;
            for (int i = 0; i < recipe.RandomRewards.Length; i++)
            {
                totalWeight += Math.Max(1, recipe.RandomRewards[i].ProbabilityWeight);
            }

            int roll = _random.Next(totalWeight);
            for (int i = 0; i < recipe.RandomRewards.Length; i++)
            {
                ItemMakerReward reward = recipe.RandomRewards[i];
                roll -= Math.Max(1, reward.ProbabilityWeight);
                if (roll < 0)
                {
                    return new ItemMakerReward
                    {
                        ItemId = reward.ItemId,
                        Quantity = reward.Quantity,
                        ProbabilityWeight = reward.ProbabilityWeight
                    };
                }
            }

            ItemMakerReward fallbackReward = recipe.RandomRewards[recipe.RandomRewards.Length - 1];
            return new ItemMakerReward
            {
                ItemId = fallbackReward.ItemId,
                Quantity = fallbackReward.Quantity,
                ProbabilityWeight = fallbackReward.ProbabilityWeight
            };
        }

        private void PopulateFallbackRecipes()
        {
            _allRecipes.Add(new ItemMakerRecipe
            {
                BucketKey = 0,
                CategoryKey = 400,
                CategoryLabel = "Etc",
                Title = "Steel Plate",
                Description = "Fallback maker recipe when ItemMake.img is unavailable.",
                OutputInventoryType = InventoryType.ETC,
                OutputItemId = 4011001,
                OutputQuantity = 1,
                MesoCost = 3000,
                Materials = new[]
                {
                    new ItemMakerMaterial { InventoryType = InventoryType.ETC, ItemId = 4010001, Quantity = 10 }
                }
            });
            _allRecipes.Add(new ItemMakerRecipe
            {
                BucketKey = 0,
                CategoryKey = 400,
                CategoryLabel = "Etc",
                Title = "Mithril Plate",
                Description = "Fallback maker recipe when ItemMake.img is unavailable.",
                OutputInventoryType = InventoryType.ETC,
                OutputItemId = 4011002,
                OutputQuantity = 1,
                MesoCost = 3500,
                Materials = new[]
                {
                    new ItemMakerMaterial { InventoryType = InventoryType.ETC, ItemId = 4010002, Quantity = 10 }
                }
            });
            _allRecipes.Add(new ItemMakerRecipe
            {
                BucketKey = 0,
                CategoryKey = 400,
                CategoryLabel = "Etc",
                Title = "Black Crystal",
                Description = "Fallback maker recipe when ItemMake.img is unavailable.",
                OutputInventoryType = InventoryType.ETC,
                OutputItemId = 4021008,
                OutputQuantity = 1,
                MesoCost = 5000,
                Materials = new[]
                {
                    new ItemMakerMaterial { InventoryType = InventoryType.ETC, ItemId = 4020008, Quantity = 10 }
                }
            });
            _allRecipes.Add(new ItemMakerRecipe
            {
                BucketKey = 0,
                CategoryKey = 400,
                CategoryLabel = "Etc",
                Title = "Screw Batch",
                Description = "Fallback maker recipe when ItemMake.img is unavailable.",
                OutputInventoryType = InventoryType.ETC,
                OutputItemId = 4003000,
                OutputQuantity = 15,
                MesoCost = 1000,
                Materials = new[]
                {
                    new ItemMakerMaterial { InventoryType = InventoryType.ETC, ItemId = 4011001, Quantity = 1 }
                }
            });
        }

        private bool CanAcceptCraftReward(ItemMakerRecipe recipe)
        {
            if (_inventory == null)
            {
                return false;
            }

            if (!recipe.UsesRandomReward || recipe.RandomRewards.Length == 0)
            {
                return _inventory.CanAcceptItem(recipe.OutputInventoryType, recipe.OutputItemId, recipe.OutputQuantity);
            }

            for (int i = 0; i < recipe.RandomRewards.Length; i++)
            {
                ItemMakerReward reward = recipe.RandomRewards[i];
                InventoryType rewardType = ResolveInventoryType(reward.ItemId);
                if (!_inventory.CanAcceptItem(rewardType, reward.ItemId, reward.Quantity))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
