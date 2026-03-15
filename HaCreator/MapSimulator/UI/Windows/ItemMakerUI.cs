using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;

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

        private sealed class ItemMakerRecipe
        {
            public string Title { get; init; }
            public string Description { get; init; }
            public InventoryType OutputInventoryType { get; init; }
            public int OutputItemId { get; init; }
            public int OutputQuantity { get; init; }
            public int RequiredLevel { get; init; }
            public int RequiredSkillLevel { get; init; }
            public int MesoCost { get; init; }
            public int CatalystItemId { get; init; }
            public bool UsesRandomReward { get; init; }
            public ItemMakerMaterial[] Materials { get; init; } = Array.Empty<ItemMakerMaterial>();
            public ItemMakerReward[] RandomRewards { get; init; } = Array.Empty<ItemMakerReward>();
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
        private const int CraftDurationMs = 1400;
        private const int VisibleRecipeCount = 6;
        private readonly List<BackgroundLayer> _backgroundLayers = new();
        private readonly List<ItemMakerRecipe> _recipes = new();
        private readonly Random _random = new();
        private readonly Texture2D _pixel;
        private Texture2D _gaugeBarTexture;
        private Texture2D _gaugeFillTexture;
        private Point _gaugePosition = new(18, 296);
        private SpriteFont _font;
        private IInventoryRuntime _inventory;
        private MouseState _previousMouseState;
        private int _selectedRecipeIndex;
        private int _recipeScrollOffset;
        private int _characterLevel = 1;
        private int _makerSkillLevel;
        private bool _isCrafting;
        private int _craftStartTick;
        private int _craftingRecipeIndex = -1;
        private string _statusMessage = "Select a recipe and press Start.";

        public ItemMakerUI(IDXObject frame, Texture2D pixel)
            : base(frame)
        {
            _pixel = pixel;
            LoadRecipes();
        }

        public override string WindowName => MapSimulatorWindowNames.ItemMaker;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetInventory(IInventoryRuntime inventory)
        {
            _inventory = inventory;
            RefreshStatusMessage();
        }

        public void SetCraftingState(int characterLevel, int makerSkillLevel)
        {
            _characterLevel = Math.Max(1, characterLevel);
            _makerSkillLevel = Math.Max(0, makerSkillLevel);
            RefreshStatusMessage();
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

        public void InitializeControls(UIObject startButton, UIObject cancelButton)
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
            if (listRect.Contains(mouseState.X, mouseState.Y) && mouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue)
            {
                if (mouseState.ScrollWheelValue > _previousMouseState.ScrollWheelValue)
                {
                    ScrollRecipes(-1);
                }
                else
                {
                    ScrollRecipes(1);
                }

                _previousMouseState = mouseState;
                return true;
            }

            if (handled || _isCrafting)
            {
                _previousMouseState = mouseState;
                return handled;
            }

            bool leftJustReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            if (!leftJustReleased || !listRect.Contains(mouseState.X, mouseState.Y))
            {
                _previousMouseState = mouseState;
                return false;
            }

            int relativeY = mouseState.Y - listRect.Y;
            int visibleIndex = relativeY / RecipeRowHeight;
            int recipeIndex = _recipeScrollOffset + visibleIndex;
            if (recipeIndex >= 0 && recipeIndex < _recipes.Count)
            {
                _selectedRecipeIndex = recipeIndex;
                EnsureSelectedRecipeVisible();
                RefreshStatusMessage();
                _previousMouseState = mouseState;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

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

            if (_pixel == null || _font == null || _recipes.Count == 0)
            {
                return;
            }

            Rectangle listRect = GetRecipeListRectangle();
            DrawPanel(sprite, listRect, new Color(16, 19, 28, 210), new Color(92, 105, 132, 255));

            int visibleRows = Math.Min(VisibleRecipeCount, _recipes.Count - _recipeScrollOffset);
            for (int i = 0; i < visibleRows; i++)
            {
                int recipeIndex = _recipeScrollOffset + i;
                ItemMakerRecipe recipe = _recipes[recipeIndex];
                Rectangle rowRect = new(listRect.X + 4, listRect.Y + 4 + (i * RecipeRowHeight), listRect.Width - 8, RecipeRowHeight - 4);
                bool selected = recipeIndex == _selectedRecipeIndex;
                DrawPanel(sprite, rowRect,
                    selected ? new Color(55, 88, 126, 210) : new Color(27, 31, 45, 180),
                    selected ? new Color(153, 190, 230, 255) : new Color(66, 74, 95, 255));

                string requirementPrefix = recipe.RequiredLevel > 0
                    ? $"Lv {recipe.RequiredLevel}"
                    : "Maker";
                sprite.DrawString(_font, recipe.Title, new Vector2(rowRect.X + 8, rowRect.Y + 5), Color.White);
                sprite.DrawString(_font, $"{requirementPrefix}  Output x{recipe.OutputQuantity}", new Vector2(rowRect.X + 8, rowRect.Y + 18), new Color(199, 211, 229));
            }

            if (_recipes.Count > VisibleRecipeCount)
            {
                string scrollText = string.Format(CultureInfo.InvariantCulture, "{0}-{1}/{2}",
                    _recipeScrollOffset + 1,
                    Math.Min(_recipeScrollOffset + VisibleRecipeCount, _recipes.Count),
                    _recipes.Count);
                sprite.DrawString(_font, scrollText, new Vector2(listRect.Right - 68, listRect.Bottom + 4), new Color(182, 191, 210));
            }

            ItemMakerRecipe selectedRecipe = _recipes[_selectedRecipeIndex];
            Vector2 detailOrigin = new(Position.X + 18, Position.Y + 185);
            sprite.DrawString(_font, selectedRecipe.Title, detailOrigin, Color.White);
            sprite.DrawString(_font, selectedRecipe.Description, detailOrigin + new Vector2(0, 18), new Color(207, 214, 226));

            float y = detailOrigin.Y + 48;
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

            sprite.DrawString(_font, "Materials", new Vector2(detailOrigin.X, y), new Color(255, 223, 153));
            y += 18;
            foreach (ItemMakerMaterial material in selectedRecipe.Materials)
            {
                int owned = _inventory?.GetItemCount(material.InventoryType, material.ItemId) ?? 0;
                Color color = owned >= material.Quantity ? new Color(194, 233, 193) : new Color(240, 155, 155);
                sprite.DrawString(
                    _font,
                    $"{GetItemName(material.ItemId)} {Math.Min(owned, material.Quantity)}/{material.Quantity}",
                    new Vector2(detailOrigin.X, y),
                    color);
                y += 17;
            }

            if (selectedRecipe.CatalystItemId > 0)
            {
                int owned = _inventory?.GetItemCount(ResolveInventoryType(selectedRecipe.CatalystItemId), selectedRecipe.CatalystItemId) ?? 0;
                Color color = owned > 0 ? new Color(194, 233, 193) : new Color(240, 155, 155);
                sprite.DrawString(
                    _font,
                    $"Catalyst: {GetItemName(selectedRecipe.CatalystItemId)} {Math.Min(owned, 1)}/1",
                    new Vector2(detailOrigin.X, y),
                    color);
                y += 17;
            }

            if (selectedRecipe.MesoCost > 0)
            {
                long mesoCount = _inventory?.GetMesoCount() ?? 0L;
                Color color = mesoCount >= selectedRecipe.MesoCost ? new Color(194, 233, 193) : new Color(240, 155, 155);
                sprite.DrawString(
                    _font,
                    $"Cost: {selectedRecipe.MesoCost:N0} meso",
                    new Vector2(detailOrigin.X, y),
                    color);
                y += 17;
            }

            y += 4;
            string resultText = selectedRecipe.UsesRandomReward
                ? $"Random result pool x{selectedRecipe.OutputQuantity}"
                : $"Result: {GetItemName(selectedRecipe.OutputItemId)} x{selectedRecipe.OutputQuantity}";
            sprite.DrawString(_font, resultText, new Vector2(detailOrigin.X, y), new Color(189, 219, 255));
            y += 18;

            if (selectedRecipe.UsesRandomReward)
            {
                int previewCount = Math.Min(3, selectedRecipe.RandomRewards.Length);
                for (int i = 0; i < previewCount; i++)
                {
                    ItemMakerReward reward = selectedRecipe.RandomRewards[i];
                    sprite.DrawString(
                        _font,
                        $"- {GetItemName(reward.ItemId)} x{reward.Quantity}",
                        new Vector2(detailOrigin.X, y),
                        new Color(205, 214, 232));
                    y += 17;
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

            ItemMakerRecipe recipe = _recipes[_selectedRecipeIndex];
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
            if (_inventory == null || _craftingRecipeIndex < 0 || _craftingRecipeIndex >= _recipes.Count)
            {
                _craftingRecipeIndex = -1;
                RefreshStatusMessage("Crafting ended without a valid recipe.");
                return;
            }

            ItemMakerRecipe recipe = _recipes[_craftingRecipeIndex];
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
            Texture2D outputTexture = _inventory.GetItemTexture(rewardInventoryType, reward.ItemId);
            _inventory.AddItem(rewardInventoryType, reward.ItemId, outputTexture, reward.Quantity);
            _craftingRecipeIndex = -1;
            RefreshStatusMessage($"Created {GetItemName(reward.ItemId)} x{reward.Quantity}.");
        }

        private bool CanCraftRecipe(ItemMakerRecipe recipe, out string failureReason)
        {
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

            if (recipe.RequiredSkillLevel > 0 && _makerSkillLevel < recipe.RequiredSkillLevel)
            {
                failureReason = $"Requires maker mastery {recipe.RequiredSkillLevel}.";
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

            if (recipe.MesoCost > 0 && _inventory.GetMesoCount() < recipe.MesoCost)
            {
                failureReason = "Not enough meso for this recipe.";
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

            if (_recipes.Count == 0)
            {
                _statusMessage = "No crafting recipes loaded.";
                return;
            }

            ItemMakerRecipe recipe = _recipes[_selectedRecipeIndex];
            _statusMessage = CanCraftRecipe(recipe, out string failureReason)
                ? "Ready to craft."
                : failureReason;
        }

        private Rectangle GetRecipeListRectangle()
        {
            return new Rectangle(Position.X + 18, Position.Y + 43, 258, (VisibleRecipeCount * RecipeRowHeight) + 8);
        }

        private void ScrollRecipes(int delta)
        {
            if (_recipes.Count <= VisibleRecipeCount)
            {
                _recipeScrollOffset = 0;
                return;
            }

            int maxOffset = Math.Max(0, _recipes.Count - VisibleRecipeCount);
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

        private void DrawPanel(SpriteBatch sprite, Rectangle rect, Color fill, Color border)
        {
            sprite.Draw(_pixel, rect, fill);
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
            sprite.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);
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

        private void LoadRecipes()
        {
            _recipes.Clear();

            if (!TryLoadClientRecipes())
            {
                PopulateFallbackRecipes();
            }
        }

        private bool TryLoadClientRecipes()
        {
            WzImage itemMakeImage = HaCreator.Program.FindImage("Etc", "ItemMake.img");
            if (itemMakeImage == null)
            {
                return false;
            }

            WzSubProperty etcRecipeBucket = itemMakeImage["0"] as WzSubProperty;
            if (etcRecipeBucket == null)
            {
                return false;
            }

            foreach (WzImageProperty recipeProperty in etcRecipeBucket.WzProperties)
            {
                if (recipeProperty is not WzSubProperty recipeData || !int.TryParse(recipeData.Name, out int outputItemId))
                {
                    continue;
                }

                ItemMakerRecipe recipe = CreateRecipeFromWz(recipeData, outputItemId);
                if (recipe != null)
                {
                    _recipes.Add(recipe);
                }
            }

            _recipes.Sort(static (left, right) =>
            {
                int levelCompare = left.RequiredLevel.CompareTo(right.RequiredLevel);
                return levelCompare != 0
                    ? levelCompare
                    : string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
            });

            return _recipes.Count > 0;
        }

        private static ItemMakerRecipe CreateRecipeFromWz(WzSubProperty recipeData, int outputItemId)
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

            int outputQuantity = (recipeData["itemNum"] as WzIntProperty)?.Value ?? 1;
            bool usesRandomReward = randomRewards.Count > 0;
            return new ItemMakerRecipe
            {
                Title = GetItemName(outputItemId),
                Description = CreateRecipeDescription(outputItemId, usesRandomReward),
                OutputInventoryType = ResolveInventoryType(outputItemId),
                OutputItemId = outputItemId,
                OutputQuantity = Math.Max(1, outputQuantity),
                RequiredLevel = (recipeData["reqLevel"] as WzIntProperty)?.Value ?? 0,
                RequiredSkillLevel = (recipeData["reqSkillLevel"] as WzIntProperty)?.Value ?? 0,
                MesoCost = Math.Max(0, (recipeData["meso"] as WzIntProperty)?.Value ?? 0),
                CatalystItemId = Math.Max(0, (recipeData["catalyst"] as WzIntProperty)?.Value ?? 0),
                UsesRandomReward = usesRandomReward,
                Materials = materials.ToArray(),
                RandomRewards = randomRewards.ToArray()
            };
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
            _recipes.Add(new ItemMakerRecipe
            {
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
            _recipes.Add(new ItemMakerRecipe
            {
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
            _recipes.Add(new ItemMakerRecipe
            {
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
            _recipes.Add(new ItemMakerRecipe
            {
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
    }
}
