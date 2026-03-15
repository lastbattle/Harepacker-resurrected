using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

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

        private sealed class ItemMakerRecipe
        {
            public string Title { get; init; }
            public string Description { get; init; }
            public InventoryType OutputInventoryType { get; init; }
            public int OutputItemId { get; init; }
            public int OutputQuantity { get; init; }
            public ItemMakerMaterial[] Materials { get; init; } = Array.Empty<ItemMakerMaterial>();
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
        private readonly List<BackgroundLayer> _backgroundLayers = new List<BackgroundLayer>();
        private readonly List<ItemMakerRecipe> _recipes = new List<ItemMakerRecipe>();
        private readonly Texture2D _pixel;
        private Texture2D _gaugeBarTexture;
        private Texture2D _gaugeFillTexture;
        private Point _gaugePosition = new Point(18, 296);
        private SpriteFont _font;
        private IInventoryRuntime _inventory;
        private MouseState _previousMouseState;
        private int _selectedRecipeIndex;
        private bool _isCrafting;
        private int _craftStartTick;
        private int _craftingRecipeIndex = -1;
        private string _statusMessage = "Select a recipe and press Start.";

        public ItemMakerUI(IDXObject frame, Texture2D pixel)
            : base(frame)
        {
            _pixel = pixel;
            PopulateSampleRecipes();
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
            if (handled || !IsVisible || _isCrafting)
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

            Rectangle listRect = GetRecipeListRectangle();
            if (!listRect.Contains(mouseState.X, mouseState.Y))
            {
                _previousMouseState = mouseState;
                return false;
            }

            int relativeY = mouseState.Y - listRect.Y;
            int index = relativeY / RecipeRowHeight;
            if (index >= 0 && index < _recipes.Count)
            {
                _selectedRecipeIndex = index;
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

            for (int i = 0; i < _recipes.Count; i++)
            {
                ItemMakerRecipe recipe = _recipes[i];
                Rectangle rowRect = new Rectangle(listRect.X + 4, listRect.Y + 4 + (i * RecipeRowHeight), listRect.Width - 8, RecipeRowHeight - 4);
                bool selected = i == _selectedRecipeIndex;
                DrawPanel(sprite, rowRect,
                    selected ? new Color(55, 88, 126, 210) : new Color(27, 31, 45, 180),
                    selected ? new Color(153, 190, 230, 255) : new Color(66, 74, 95, 255));

                sprite.DrawString(_font, recipe.Title, new Vector2(rowRect.X + 8, rowRect.Y + 5), Color.White);
                sprite.DrawString(_font, $"Output x{recipe.OutputQuantity}", new Vector2(rowRect.X + 8, rowRect.Y + 18), new Color(199, 211, 229));
            }

            ItemMakerRecipe selectedRecipe = _recipes[_selectedRecipeIndex];
            Vector2 detailOrigin = new Vector2(Position.X + 18, Position.Y + 185);
            sprite.DrawString(_font, selectedRecipe.Title, detailOrigin, Color.White);
            sprite.DrawString(_font, selectedRecipe.Description, detailOrigin + new Vector2(0, 18), new Color(207, 214, 226));

            float y = detailOrigin.Y + 48;
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

            y += 4;
            sprite.DrawString(
                _font,
                $"Result: {GetItemName(selectedRecipe.OutputItemId)} x{selectedRecipe.OutputQuantity}",
                new Vector2(detailOrigin.X, y),
                new Color(189, 219, 255));

            DrawGauge(sprite);

            Vector2 statusOrigin = new Vector2(Position.X + 18, Position.Y + 321);
            sprite.DrawString(_font, _statusMessage, statusOrigin, new Color(230, 230, 230));
        }

        private void DrawGauge(SpriteBatch sprite)
        {
            Rectangle gaugeRect = new Rectangle(Position.X + _gaugePosition.X, Position.Y + _gaugePosition.Y, 275, 13);
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

            Rectangle fillRect = new Rectangle(gaugeRect.X + 1, gaugeRect.Y + 1, fillWidth, gaugeRect.Height - 2);
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
            if (!HasRequiredMaterials(recipe))
            {
                _statusMessage = "Not enough materials for this recipe.";
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
            if (!HasRequiredMaterials(recipe))
            {
                _craftingRecipeIndex = -1;
                RefreshStatusMessage("Materials changed before completion.");
                return;
            }

            foreach (ItemMakerMaterial material in recipe.Materials)
            {
                _inventory.TryConsumeItem(material.InventoryType, material.ItemId, material.Quantity);
            }

            Texture2D outputTexture = _inventory.GetItemTexture(recipe.OutputInventoryType, recipe.OutputItemId);
            _inventory.AddItem(recipe.OutputInventoryType, recipe.OutputItemId, outputTexture, recipe.OutputQuantity);
            _craftingRecipeIndex = -1;
            RefreshStatusMessage($"Created {GetItemName(recipe.OutputItemId)} x{recipe.OutputQuantity}.");
        }

        private bool HasRequiredMaterials(ItemMakerRecipe recipe)
        {
            if (_inventory == null)
            {
                return false;
            }

            foreach (ItemMakerMaterial material in recipe.Materials)
            {
                if (_inventory.GetItemCount(material.InventoryType, material.ItemId) < material.Quantity)
                {
                    return false;
                }
            }

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
            _statusMessage = HasRequiredMaterials(recipe)
                ? "Ready to craft."
                : "Missing required materials.";
        }

        private Rectangle GetRecipeListRectangle()
        {
            return new Rectangle(Position.X + 18, Position.Y + 43, 258, (_recipes.Count * RecipeRowHeight) + 8);
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

        private void PopulateSampleRecipes()
        {
            _recipes.Add(new ItemMakerRecipe
            {
                Title = "Steel Plate",
                Description = "Refine steel ore into a plate used by several crafting paths.",
                OutputInventoryType = InventoryType.ETC,
                OutputItemId = 4011001,
                OutputQuantity = 1,
                Materials = new[]
                {
                    new ItemMakerMaterial { InventoryType = InventoryType.ETC, ItemId = 4010001, Quantity = 10 }
                }
            });
            _recipes.Add(new ItemMakerRecipe
            {
                Title = "Mithril Plate",
                Description = "Converts mithril ore into a higher tier crafting plate.",
                OutputInventoryType = InventoryType.ETC,
                OutputItemId = 4011002,
                OutputQuantity = 1,
                Materials = new[]
                {
                    new ItemMakerMaterial { InventoryType = InventoryType.ETC, ItemId = 4010002, Quantity = 10 }
                }
            });
            _recipes.Add(new ItemMakerRecipe
            {
                Title = "Black Crystal",
                Description = "Polish black crystal ore into a finished gem.",
                OutputInventoryType = InventoryType.ETC,
                OutputItemId = 4021008,
                OutputQuantity = 1,
                Materials = new[]
                {
                    new ItemMakerMaterial { InventoryType = InventoryType.ETC, ItemId = 4020008, Quantity = 10 }
                }
            });
            _recipes.Add(new ItemMakerRecipe
            {
                Title = "Screw Batch",
                Description = "Cuts one steel plate into a batch of screws.",
                OutputInventoryType = InventoryType.ETC,
                OutputItemId = 4003000,
                OutputQuantity = 15,
                Materials = new[]
                {
                    new ItemMakerMaterial { InventoryType = InventoryType.ETC, ItemId = 4011001, Quantity = 1 }
                }
            });
        }
    }
}
