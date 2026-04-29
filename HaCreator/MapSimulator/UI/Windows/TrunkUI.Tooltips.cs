using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.UI
{
    public sealed partial class TrunkUI
    {
        private int ResolveRowIndex(int mouseX, int mouseY, bool storagePane)
        {
            int originX = Position.X + (storagePane ? StorageRowX : InventoryRowX);
            int originY = Position.Y + (storagePane ? StorageRowY : InventoryRowY);
            int rowWidth = storagePane ? StorageRowWidth : InventoryRowWidth;
            Rectangle paneRect = new Rectangle(originX, originY, rowWidth, MaxVisibleRows * RowHeight);
            if (!paneRect.Contains(mouseX, mouseY))
            {
                return -1;
            }

            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            IReadOnlyList<InventorySlotData> rows = storagePane
                ? GetStorageRows(inventoryType)
                : _inventory?.GetSlots(inventoryType) ?? Array.Empty<InventorySlotData>();
            int scrollOffset = storagePane ? _storageScrollOffset : _inventoryScrollOffset;
            int rowIndex = (mouseY - originY) / RowHeight;
            int actualIndex = scrollOffset + rowIndex;
            return actualIndex >= 0 && actualIndex < rows.Count ? actualIndex : -1;
        }

        private void DrawHoveredSlotTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (_font == null || _mesoEntryMode != MesoEntryMode.None)
            {
                return;
            }

            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            bool useStorageSlot = _hoveredPane == TrunkPane.Storage && _hoveredStorageIndex >= 0;
            bool useInventorySlot = _hoveredPane == TrunkPane.Inventory && _hoveredInventoryIndex >= 0;
            if (!useStorageSlot && !useInventorySlot)
            {
                return;
            }

            IReadOnlyList<InventorySlotData> rows = useStorageSlot
                ? GetStorageRows(inventoryType)
                : _inventory?.GetSlots(inventoryType) ?? Array.Empty<InventorySlotData>();
            int index = useStorageSlot ? _hoveredStorageIndex : _hoveredInventoryIndex;
            if (index < 0 || index >= rows.Count)
            {
                return;
            }

            InventorySlotData slot = rows[index];
            if (slot == null)
            {
                return;
            }

            if (inventoryType == InventoryType.EQUIP
                && TryResolveEquipTooltipPart(slot, out CharacterPart tooltipPart)
                && _equipTooltipAssets != null)
            {
                DrawEquipTooltip(sprite, slot, tooltipPart, renderWidth, renderHeight);
                return;
            }

            DrawItemTooltip(sprite, slot, inventoryType, renderWidth, renderHeight);
        }

        private void DrawItemTooltip(SpriteBatch sprite, InventorySlotData slot, InventoryType inventoryType, int renderWidth, int renderHeight)
        {
            InventoryItemTooltipMetadata metadata = InventoryItemMetadataResolver.ResolveTooltipMetadata(slot.ItemId, inventoryType);
            string title = ResolveDisplayText(slot.ItemName, metadata.ItemName);
            string typeLine = ResolveDisplayText(slot.ItemTypeName, ResolveDisplayText(metadata.TypeName, inventoryType.ToString()));
            string quantityLine = slot.Quantity > 1 ? $"Quantity: {slot.Quantity}" : string.Empty;
            string stackLine = InventoryItemMetadataResolver.BuildRuntimeFallbackStackLimitMetadataLine(
                slot.MaxStackSize,
                metadata.MetadataLines);
            string description = ResolveDisplayText(slot.Description, metadata.Description);
            Texture2D itemTexture = ResolveSlotItemTexture(sprite.GraphicsDevice, slot);
            Texture2D cashLabelTexture = metadata.IsCashItem ? _equipTooltipAssets?.CashLabel : null;
            Texture2D sampleTexture = ResolveInfoSampleTexture(sprite.GraphicsDevice, slot.ItemId);
            Texture2D[] iconRewardTextures = ResolveInfoIconRewardTextures(sprite.GraphicsDevice, slot.ItemId);
            Texture2D[] rewardPreviewTextures = HasDrawableBitmap(iconRewardTextures)
                ? Array.Empty<Texture2D>()
                : ResolveRewardPreviewTextures(sprite.GraphicsDevice, slot.ItemId);
            TooltipSampleUiFrame sampleUiFrame = sampleTexture == null
                ? ResolveSampleUiFrame(sprite.GraphicsDevice, slot.ItemId)
                : null;
            bool drawAuthoredSampleInFrame = sampleUiFrame?.IsDrawable == true
                                             && metadata.AuthoredSampleLines.Count > 0;

            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TooltipPadding + TooltipIconSize + TooltipIconGap;
            float titleWidth = tooltipWidth - (TooltipPadding * 2);
            float sectionWidth = tooltipWidth - textLeftOffset - TooltipPadding;

            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            float titleHeight = MeasureLinesHeight(wrappedTitle);
            List<TooltipSection> sections = new();
            if (!string.IsNullOrWhiteSpace(typeLine))
            {
                sections.Add(new TooltipSection(typeLine, new Color(180, 220, 255)));
            }

            for (int i = 0; i < metadata.EffectLines.Count; i++)
            {
                sections.Add(new TooltipSection(metadata.EffectLines[i], new Color(180, 255, 210)));
            }

            if (!string.IsNullOrWhiteSpace(quantityLine))
            {
                sections.Add(new TooltipSection(quantityLine, Color.White));
            }

            if (!string.IsNullOrWhiteSpace(stackLine))
            {
                sections.Add(new TooltipSection(stackLine, new Color(180, 255, 210)));
            }

            for (int i = 0; i < metadata.MetadataLines.Count; i++)
            {
                sections.Add(new TooltipSection(metadata.MetadataLines[i], new Color(255, 214, 156)));
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                sections.Add(new TooltipSection(description, new Color(255, 238, 196)));
            }

            if (!drawAuthoredSampleInFrame)
            {
                for (int i = 0; i < metadata.AuthoredSampleLines.Count; i++)
                {
                    sections.Add(new TooltipSection(metadata.AuthoredSampleLines[i], new Color(210, 220, 255)));
                }
            }

            List<(string[] Lines, Color Color, float Height)> wrappedSections = BuildWrappedTooltipSections(sections);
            float wrappedSectionHeight = MeasureWrappedSectionHeight(wrappedSections);
            float cashLabelHeight = cashLabelTexture?.Height ?? 0f;
            float contentHeight = wrappedSectionHeight;
            if (cashLabelHeight > 0f)
            {
                contentHeight += (contentHeight > 0f ? 2f : 0f) + cashLabelHeight;
            }

            float iconBlockHeight = Math.Max(TooltipIconSize, contentHeight);
            float sampleHeight = sampleTexture?.Height ?? MeasureSampleUiFrameHeight(sampleUiFrame, metadata.AuthoredSampleLines);
            float iconRewardHeight = MeasureHorizontalBitmapStripHeight(iconRewardTextures);
            float rewardPreviewHeight = MeasureHorizontalBitmapStripHeight(rewardPreviewTextures);
            float bitmapPreviewHeight = sampleHeight;
            if (iconRewardHeight > 0f)
            {
                bitmapPreviewHeight += (bitmapPreviewHeight > 0f ? TooltipBitmapGap : 0f) + iconRewardHeight;
            }
            else if (rewardPreviewHeight > 0f)
            {
                bitmapPreviewHeight += (bitmapPreviewHeight > 0f ? TooltipBitmapGap : 0f) + rewardPreviewHeight;
            }

            float bitmapPreviewGap = bitmapPreviewHeight > 0f ? TooltipSectionGap : 0f;
            int tooltipHeight = (int)Math.Ceiling((TooltipPadding * 2) + titleHeight + TooltipSectionGap + iconBlockHeight + bitmapPreviewGap + bitmapPreviewHeight);
            Rectangle anchorRect = ResolveHoveredTooltipAnchorRect();
            Point tooltipAnchor = new Point(anchorRect.Right + TooltipOffsetX, anchorRect.Bottom);
            Rectangle backgroundRect = ResolveTooltipRect(
                tooltipAnchor,
                tooltipWidth,
                tooltipHeight,
                renderWidth,
                renderHeight,
                stackalloc[] { 1, 0, 2 },
                out int frameIndex);
            DrawTooltipBackground(sprite, backgroundRect, frameIndex);

            int titleX = backgroundRect.X + TooltipPadding;
            int titleY = backgroundRect.Y + TooltipPadding;
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

            int contentY = backgroundRect.Y + TooltipPadding + (int)Math.Ceiling(titleHeight) + TooltipSectionGap;
            if (itemTexture != null)
            {
                sprite.Draw(itemTexture, new Rectangle(backgroundRect.X + TooltipPadding, contentY, TooltipIconSize, TooltipIconSize), Color.White);
            }

            int textX = backgroundRect.X + textLeftOffset;
            float sectionY = contentY;
            if (cashLabelHeight > 0f)
            {
                sprite.Draw(cashLabelTexture, new Vector2(textX, sectionY), Color.White);
                sectionY += cashLabelHeight;
            }

            if (wrappedSectionHeight > 0f)
            {
                if (cashLabelHeight > 0f)
                {
                    sectionY += 2f;
                }

                DrawWrappedSections(sprite, textX, sectionY, wrappedSections);
            }

            if (sampleTexture != null)
            {
                int sampleX = backgroundRect.X + Math.Max(TooltipPadding, (backgroundRect.Width - sampleTexture.Width) / 2);
                int sampleY = contentY + (int)Math.Ceiling(iconBlockHeight) + TooltipSectionGap;
                sprite.Draw(sampleTexture, new Vector2(sampleX, sampleY), Color.White);
                DrawHorizontalBitmapStrip(
                    sprite,
                    iconRewardTextures,
                    backgroundRect,
                    sampleY + sampleTexture.Height + TooltipBitmapGap);
                DrawHorizontalBitmapStrip(
                    sprite,
                    rewardPreviewTextures,
                    backgroundRect,
                    sampleY + sampleTexture.Height + TooltipBitmapGap);
            }
            else if (drawAuthoredSampleInFrame)
            {
                int sampleWidth = ResolveSampleUiFrameWidth(sampleUiFrame);
                int sampleX = backgroundRect.X + Math.Max(TooltipPadding, (backgroundRect.Width - sampleWidth) / 2);
                int sampleY = contentY + (int)Math.Ceiling(iconBlockHeight) + TooltipSectionGap;
                DrawSampleUiFrame(sprite, sampleUiFrame, metadata.AuthoredSampleLines, sampleX, sampleY);
                DrawHorizontalBitmapStrip(
                    sprite,
                    iconRewardTextures,
                    backgroundRect,
                    sampleY + (int)Math.Ceiling(sampleHeight) + TooltipBitmapGap);
                DrawHorizontalBitmapStrip(
                    sprite,
                    rewardPreviewTextures,
                    backgroundRect,
                    sampleY + (int)Math.Ceiling(sampleHeight) + TooltipBitmapGap);
            }
            else if (iconRewardHeight > 0f)
            {
                int rewardY = contentY + (int)Math.Ceiling(iconBlockHeight) + TooltipSectionGap;
                DrawHorizontalBitmapStrip(sprite, iconRewardTextures, backgroundRect, rewardY);
            }
            else if (rewardPreviewHeight > 0f)
            {
                int rewardY = contentY + (int)Math.Ceiling(iconBlockHeight) + TooltipSectionGap;
                DrawHorizontalBitmapStrip(sprite, rewardPreviewTextures, backgroundRect, rewardY);
            }
        }

        private void DrawEquipTooltip(SpriteBatch sprite, InventorySlotData slot, CharacterPart part, int renderWidth, int renderHeight)
        {
            string title = ResolveDisplayText(slot.ItemName, ResolveDisplayText(part.Name, $"Equip {slot.ItemId}"));
            string description = ResolveDisplayText(slot.Description, ResolveDisplayText(part.Description, string.Empty));
            Texture2D itemTexture = ResolveSlotItemTexture(sprite.GraphicsDevice, slot);
            IDXObject itemIcon = part.IconRaw ?? part.Icon;

            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TooltipPadding + TooltipIconSize + TooltipIconGap;
            int contentWidth = tooltipWidth - (TooltipPadding * 2);
            int sectionWidth = tooltipWidth - textLeftOffset - TooltipPadding;
            string[] wrappedTitle = WrapTooltipText(title, contentWidth);
            float titleHeight = MeasureLinesHeight(wrappedTitle);

            Texture2D categoryTexture = ResolveCategoryTexture(part);
            string categoryFallback = categoryTexture == null ? ResolveCategoryFallbackText(part) : string.Empty;
            string[] wrappedCategory = WrapTooltipText(categoryFallback, sectionWidth);
            float categoryHeight = categoryTexture?.Height ?? MeasureLinesHeight(wrappedCategory);
            Texture2D cashLabelTexture = part.IsCash ? _equipTooltipAssets.CashLabel : null;
            float cashLabelHeight = cashLabelTexture?.Height ?? 0f;
            string[] wrappedDescription = WrapTooltipText(description, sectionWidth);
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);

            float topTextHeight = categoryHeight;
            if (cashLabelHeight > 0f)
            {
                topTextHeight += (topTextHeight > 0f ? 2f : 0f) + cashLabelHeight;
            }

            if (descriptionHeight > 0f)
            {
                topTextHeight += (topTextHeight > 0f ? TooltipSectionGap : 0f) + descriptionHeight;
            }

            float topBlockHeight = Math.Max(TooltipIconSize, topTextHeight);
            List<TooltipLabeledValueRow> statRows = BuildTooltipStatRows(part);
            List<TooltipLabeledValueRow> requirementRows = BuildTooltipRequirementRows(part);
            List<Texture2D> jobBadges = BuildTooltipJobBadges(part.RequiredJobMask);
            List<(string[] Lines, Color Color, float Height)> wrappedFooters = BuildWrappedTooltipSections(
                BuildTooltipFooterSections(part, slot.Quantity, slot.MaxStackSize));

            float contentHeight = topBlockHeight;
            float statHeight = MeasureLabeledValueRowsHeight(statRows);
            float requirementHeight = MeasureLabeledValueRowsHeight(requirementRows);
            float jobBadgeHeight = jobBadges.Count > 0 ? 13f : 0f;
            float footerHeight = MeasureWrappedSectionHeight(wrappedFooters);

            if (statHeight > 0f)
            {
                contentHeight += TooltipSectionGap + statHeight;
            }

            if (requirementHeight > 0f)
            {
                contentHeight += TooltipSectionGap + requirementHeight;
            }

            if (jobBadgeHeight > 0f)
            {
                contentHeight += TooltipSectionGap + jobBadgeHeight;
            }

            if (footerHeight > 0f)
            {
                contentHeight += TooltipSectionGap + footerHeight;
            }

            int tooltipHeight = (int)Math.Ceiling((TooltipPadding * 2) + titleHeight + TooltipSectionGap + contentHeight);
            Rectangle anchorRect = ResolveHoveredTooltipAnchorRect();
            Point tooltipAnchor = new Point(anchorRect.Right + TooltipOffsetX, anchorRect.Bottom);
            Rectangle backgroundRect = ResolveTooltipRect(
                tooltipAnchor,
                tooltipWidth,
                tooltipHeight,
                renderWidth,
                renderHeight,
                stackalloc[] { 1, 0, 2 },
                out int frameIndex);
            DrawTooltipBackground(sprite, backgroundRect, frameIndex);

            int titleX = backgroundRect.X + TooltipPadding;
            int titleY = backgroundRect.Y + TooltipPadding;
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

            int contentY = backgroundRect.Y + TooltipPadding + (int)Math.Ceiling(titleHeight) + TooltipSectionGap;
            int iconX = backgroundRect.X + TooltipPadding;
            if (itemIcon != null)
            {
                itemIcon.DrawBackground(sprite, null, null, iconX, contentY, Color.White, false, null);
            }
            else if (itemTexture != null)
            {
                sprite.Draw(itemTexture, new Rectangle(iconX, contentY, TooltipIconSize, TooltipIconSize), Color.White);
            }

            int textX = backgroundRect.X + textLeftOffset;
            float topY = contentY;
            if (categoryTexture != null)
            {
                sprite.Draw(categoryTexture, new Vector2(textX, topY), Color.White);
                topY += categoryTexture.Height;
            }
            else if (categoryHeight > 0f)
            {
                DrawTooltipLines(sprite, wrappedCategory, textX, topY, new Color(181, 224, 255));
                topY += categoryHeight;
            }

            if (cashLabelHeight > 0f)
            {
                if (topY > contentY)
                {
                    topY += 2f;
                }

                sprite.Draw(cashLabelTexture, new Vector2(textX, topY), Color.White);
                topY += cashLabelHeight;
            }

            if (descriptionHeight > 0f)
            {
                if (topY > contentY)
                {
                    topY += TooltipSectionGap;
                }

                DrawTooltipLines(sprite, wrappedDescription, textX, topY, new Color(216, 216, 216));
            }

            float sectionY = contentY + topBlockHeight;
            if (statHeight > 0f)
            {
                sectionY += TooltipSectionGap;
                sectionY = DrawLabeledValueRows(sprite, backgroundRect.X + TooltipPadding, sectionY, statRows);
            }

            if (requirementHeight > 0f)
            {
                sectionY += TooltipSectionGap;
                sectionY = DrawLabeledValueRows(sprite, backgroundRect.X + TooltipPadding, sectionY, requirementRows);
            }

            if (jobBadgeHeight > 0f)
            {
                sectionY += TooltipSectionGap;
                sectionY = DrawJobBadgeRow(sprite, backgroundRect.X + TooltipPadding, sectionY, jobBadges);
            }

            if (footerHeight > 0f)
            {
                sectionY += TooltipSectionGap;
                DrawWrappedSections(sprite, backgroundRect.X + TooltipPadding, sectionY, wrappedFooters);
            }
        }

        private bool TryResolveEquipTooltipPart(InventorySlotData slot, out CharacterPart tooltipPart)
        {
            tooltipPart = slot?.TooltipPart;
            if (tooltipPart != null)
            {
                slot.ApplyTooltipInstanceFields(tooltipPart);
                return true;
            }

            if (slot == null || slot.ItemId <= 0 || _characterLoader == null)
            {
                return false;
            }

            tooltipPart = _characterLoader.LoadEquipment(slot.ItemId);
            if (tooltipPart == null)
            {
                return false;
            }

            slot.ApplyTooltipInstanceFields(tooltipPart);
            slot.TooltipPart = tooltipPart.Clone();
            return true;
        }

        private Texture2D ResolveSlotItemTexture(GraphicsDevice device, InventorySlotData slot)
        {
            if (slot == null)
            {
                return null;
            }

            if (slot.ItemTexture != null)
            {
                return slot.ItemTexture;
            }

            if (!InventoryItemMetadataResolver.TryResolveImageSource(slot.ItemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            string itemText = category == "Character" ? slot.ItemId.ToString("D8") : slot.ItemId.ToString("D7");
            WzSubProperty itemProperty = itemImage[itemText] as WzSubProperty;
            WzSubProperty infoProperty = itemProperty?["info"] as WzSubProperty;
            WzCanvasProperty iconCanvas = infoProperty?["iconRaw"] as WzCanvasProperty
                                          ?? infoProperty?["icon"] as WzCanvasProperty;
            slot.ItemTexture = iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            return slot.ItemTexture;
        }

        private Texture2D ResolveInfoSampleTexture(GraphicsDevice device, int itemId)
        {
            if (itemId <= 0 || device == null)
            {
                return null;
            }

            if (_infoSampleTextureCache.TryGetValue(itemId, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D texture = null;
            if (InventoryItemMetadataResolver.TryResolveInfoCanvas(itemId, "sample", out WzCanvasProperty sampleCanvas))
            {
                texture = sampleCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            }

            _infoSampleTextureCache[itemId] = texture;
            return texture;
        }

        private TooltipSampleUiFrame ResolveSampleUiFrame(GraphicsDevice device, int itemId)
        {
            if (itemId <= 0 || device == null)
            {
                return null;
            }

            if (_sampleUiFrameCache.TryGetValue(itemId, out TooltipSampleUiFrame cachedFrame))
            {
                return cachedFrame;
            }

            TooltipSampleUiFrame frame = null;
            if (InventoryItemMetadataResolver.TryResolveSampleUiFrame(
                    itemId,
                    out WzCanvasProperty topCanvas,
                    out WzCanvasProperty centerCanvas,
                    out WzCanvasProperty bottomCanvas))
            {
                frame = new TooltipSampleUiFrame
                {
                    Top = topCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                    Center = centerCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device),
                    Bottom = bottomCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device)
                };
            }

            _sampleUiFrameCache[itemId] = frame;
            return frame;
        }

        private Texture2D[] ResolveInfoIconRewardTextures(GraphicsDevice device, int itemId)
        {
            if (itemId <= 0 || device == null)
            {
                return Array.Empty<Texture2D>();
            }

            if (_infoIconRewardTextureCache.TryGetValue(itemId, out Texture2D[] cachedTextures))
            {
                return cachedTextures;
            }

            IReadOnlyList<WzCanvasProperty> canvases = InventoryItemMetadataResolver.ResolveInfoCanvasSequence(
                itemId,
                "iconReward",
                8);
            Texture2D[] textures = new Texture2D[canvases.Count];
            for (int i = 0; i < canvases.Count; i++)
            {
                textures[i] = canvases[i]?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            }

            _infoIconRewardTextureCache[itemId] = textures;
            return textures;
        }

        private Texture2D[] ResolveRewardPreviewTextures(GraphicsDevice device, int itemId)
        {
            if (itemId <= 0 || device == null)
            {
                return Array.Empty<Texture2D>();
            }

            if (_rewardPreviewTextureCache.TryGetValue(itemId, out Texture2D[] cachedTextures))
            {
                return cachedTextures;
            }

            IReadOnlyList<InventoryRewardPreviewItem> rewardItems =
                InventoryItemMetadataResolver.ResolveRewardPreviewItems(itemId, 8);
            var textures = new List<Texture2D>(rewardItems.Count);
            for (int i = 0; i < rewardItems.Count; i++)
            {
                if (!InventoryItemMetadataResolver.TryResolveRootCanvas(
                        rewardItems[i].ItemId,
                        "info/icon",
                        out WzCanvasProperty iconCanvas)
                    && !InventoryItemMetadataResolver.TryResolveEffectFirstCanvas(
                        rewardItems[i].EffectPath,
                        out iconCanvas))
                {
                    continue;
                }

                Texture2D texture = iconCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (texture != null)
                {
                    textures.Add(texture);
                }
            }

            Texture2D[] resolvedTextures = textures.ToArray();
            _rewardPreviewTextureCache[itemId] = resolvedTextures;
            return resolvedTextures;
        }

        private static bool HasDrawableBitmap(IReadOnlyList<Texture2D> textures)
        {
            if (textures == null)
            {
                return false;
            }

            for (int i = 0; i < textures.Count; i++)
            {
                if (textures[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static float MeasureHorizontalBitmapStripHeight(IReadOnlyList<Texture2D> textures)
        {
            if (textures == null || textures.Count == 0)
            {
                return 0f;
            }

            int height = 0;
            for (int i = 0; i < textures.Count; i++)
            {
                if (textures[i] != null)
                {
                    height = Math.Max(height, textures[i].Height);
                }
            }

            return height;
        }

        private static int MeasureHorizontalBitmapStripWidth(IReadOnlyList<Texture2D> textures)
        {
            if (textures == null || textures.Count == 0)
            {
                return 0;
            }

            int width = 0;
            int visibleCount = 0;
            for (int i = 0; i < textures.Count; i++)
            {
                if (textures[i] == null)
                {
                    continue;
                }

                if (visibleCount > 0)
                {
                    width += TooltipBitmapGap;
                }

                width += textures[i].Width;
                visibleCount++;
            }

            return width;
        }

        private static void DrawHorizontalBitmapStrip(
            SpriteBatch sprite,
            IReadOnlyList<Texture2D> textures,
            Rectangle tooltipRect,
            int y)
        {
            int stripHeight = (int)Math.Ceiling(MeasureHorizontalBitmapStripHeight(textures));
            if (sprite == null || stripHeight <= 0)
            {
                return;
            }

            int stripWidth = MeasureHorizontalBitmapStripWidth(textures);
            int x = tooltipRect.X + Math.Max(TooltipPadding, (tooltipRect.Width - stripWidth) / 2);
            for (int i = 0; i < textures.Count; i++)
            {
                Texture2D texture = textures[i];
                if (texture == null)
                {
                    continue;
                }

                int drawY = y + Math.Max(0, (stripHeight - texture.Height) / 2);
                sprite.Draw(texture, new Vector2(x, drawY), Color.White);
                x += texture.Width + TooltipBitmapGap;
            }
        }

        private float MeasureSampleUiFrameHeight(TooltipSampleUiFrame frame, IReadOnlyList<string> sampleLines)
        {
            if (frame?.IsDrawable != true || sampleLines == null || sampleLines.Count == 0)
            {
                return 0f;
            }

            return frame.Top.Height + (frame.Center.Height * sampleLines.Count) + frame.Bottom.Height;
        }

        private static int ResolveSampleUiFrameWidth(TooltipSampleUiFrame frame)
        {
            if (frame?.IsDrawable != true)
            {
                return 0;
            }

            return Math.Max(frame.Top.Width, Math.Max(frame.Center.Width, frame.Bottom.Width));
        }

        private void DrawSampleUiFrame(SpriteBatch sprite, TooltipSampleUiFrame frame, IReadOnlyList<string> sampleLines, int x, int y)
        {
            if (sprite == null || frame?.IsDrawable != true || sampleLines == null || sampleLines.Count == 0)
            {
                return;
            }

            int frameWidth = ResolveSampleUiFrameWidth(frame);
            DrawCenteredTooltipBitmap(sprite, frame.Top, x, y, frameWidth);
            int rowY = y + frame.Top.Height;
            for (int i = 0; i < sampleLines.Count; i++)
            {
                DrawCenteredTooltipBitmap(sprite, frame.Center, x, rowY, frameWidth);
                DrawTooltipText(sprite, sampleLines[i], new Vector2(x + 5, rowY + 1), new Color(48, 48, 48));
                rowY += frame.Center.Height;
            }

            DrawCenteredTooltipBitmap(sprite, frame.Bottom, x, rowY, frameWidth);
        }

        private static void DrawCenteredTooltipBitmap(SpriteBatch sprite, Texture2D texture, int x, int y, int frameWidth)
        {
            if (sprite == null || texture == null)
            {
                return;
            }

            sprite.Draw(texture, new Vector2(x + ((frameWidth - texture.Width) / 2), y), Color.White);
        }

        private int ResolveTooltipWidth()
        {
            int textureWidth = _tooltipFrames[1]?.Width ?? 0;
            return textureWidth > 0 ? textureWidth : TooltipFallbackWidth;
        }

        private Rectangle ResolveHoveredTooltipAnchorRect()
        {
            if (_hoveredPane == TrunkPane.Storage && _hoveredStorageIndex >= 0)
            {
                return ResolveRowBounds(storagePane: true, _hoveredStorageIndex);
            }

            if (_hoveredPane == TrunkPane.Inventory && _hoveredInventoryIndex >= 0)
            {
                return ResolveRowBounds(storagePane: false, _hoveredInventoryIndex);
            }

            return new Rectangle(_lastMousePosition.X, _lastMousePosition.Y, TooltipIconSize, TooltipIconSize);
        }

        private Rectangle ResolveRowBounds(bool storagePane, int actualIndex)
        {
            int originX = Position.X + (storagePane ? StorageRowX : InventoryRowX);
            int originY = Position.Y + (storagePane ? StorageRowY : InventoryRowY);
            int rowWidth = storagePane ? StorageRowWidth : InventoryRowWidth;
            int scrollOffset = storagePane ? _storageScrollOffset : _inventoryScrollOffset;
            int visibleIndex = Math.Max(0, actualIndex - scrollOffset);
            return new Rectangle(originX, originY + (visibleIndex * RowHeight), rowWidth, RowHeight);
        }

        private Rectangle CreateTooltipRectFromAnchor(Point anchorPoint, int tooltipWidth, int tooltipHeight, int tooltipFrameIndex)
        {
            Texture2D tooltipFrame = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrames.Length
                ? _tooltipFrames[tooltipFrameIndex]
                : null;
            Point origin = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrameOrigins.Length
                ? _tooltipFrameOrigins[tooltipFrameIndex]
                : Point.Zero;

            if (tooltipFrame != null && origin != Point.Zero)
            {
                float scaleX = tooltipFrame.Width > 0 ? tooltipWidth / (float)tooltipFrame.Width : 1f;
                float scaleY = tooltipFrame.Height > 0 ? tooltipHeight / (float)tooltipFrame.Height : 1f;
                return new Rectangle(
                    anchorPoint.X - (int)Math.Round(origin.X * scaleX),
                    anchorPoint.Y - (int)Math.Round(origin.Y * scaleY),
                    tooltipWidth,
                    tooltipHeight);
            }

            return tooltipFrameIndex switch
            {
                0 => new Rectangle(anchorPoint.X - tooltipWidth - TooltipOffsetX, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight),
                2 => new Rectangle(anchorPoint.X, anchorPoint.Y + TooltipOffsetY, tooltipWidth, tooltipHeight),
                _ => new Rectangle(anchorPoint.X, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight)
            };
        }

        private static int ComputeTooltipOverflow(Rectangle rect, int renderWidth, int renderHeight)
        {
            int overflow = 0;
            if (rect.Left < TooltipPadding)
                overflow += TooltipPadding - rect.Left;
            if (rect.Top < TooltipPadding)
                overflow += TooltipPadding - rect.Top;
            if (rect.Right > renderWidth - TooltipPadding)
                overflow += rect.Right - (renderWidth - TooltipPadding);
            if (rect.Bottom > renderHeight - TooltipPadding)
                overflow += rect.Bottom - (renderHeight - TooltipPadding);
            return overflow;
        }

        private static Rectangle ClampTooltipRect(Rectangle rect, int renderWidth, int renderHeight)
        {
            int minX = TooltipPadding;
            int minY = TooltipPadding;
            int maxX = Math.Max(minX, renderWidth - TooltipPadding - rect.Width);
            int maxY = Math.Max(minY, renderHeight - TooltipPadding - rect.Height);
            return new Rectangle(
                Math.Clamp(rect.X, minX, maxX),
                Math.Clamp(rect.Y, minY, maxY),
                rect.Width,
                rect.Height);
        }

        private Rectangle ResolveTooltipRect(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int renderWidth,
            int renderHeight,
            ReadOnlySpan<int> framePreference,
            out int tooltipFrameIndex)
        {
            SkillTooltipFrameLayout.FrameGeometry[] frameGeometries =
                SkillTooltipFrameLayout.BuildFrameGeometries(_tooltipFrames, _tooltipFrameOrigins);
            return SkillTooltipFrameLayout.ResolveTooltipRect(
                anchorPoint,
                tooltipWidth,
                tooltipHeight,
                renderWidth,
                renderHeight,
                frameGeometries,
                framePreference,
                TooltipPadding,
                out tooltipFrameIndex);
        }

        private void DrawTooltipBackground(SpriteBatch sprite, Rectangle rect, int tooltipFrameIndex)
        {
            SkillTooltipFrameLayout.DrawTooltipFrameOrPlainBackground(
                sprite,
                _tooltipFrames,
                tooltipFrameIndex,
                _debugTooltipTexture,
                rect);
        }

        private void DrawTooltipBorder(SpriteBatch sprite, Rectangle rect)
        {
            if (_debugTooltipTexture == null)
            {
                return;
            }

            Color borderColor = new Color(87, 100, 128);
            sprite.Draw(_debugTooltipTexture, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 1), borderColor);
            sprite.Draw(_debugTooltipTexture, new Rectangle(rect.X - 1, rect.Bottom, rect.Width + 2, 1), borderColor);
            sprite.Draw(_debugTooltipTexture, new Rectangle(rect.X - 1, rect.Y, 1, rect.Height), borderColor);
            sprite.Draw(_debugTooltipTexture, new Rectangle(rect.Right, rect.Y, 1, rect.Height), borderColor);
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
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position + Vector2.One, Color.Black, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        private float MeasureLinesHeight(string[] lines)
        {
            if (_font == null || lines == null || lines.Length == 0)
            {
                return 0f;
            }

            int nonEmptyLineCount = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    nonEmptyLineCount++;
                }
            }

            return nonEmptyLineCount > 0 ? nonEmptyLineCount * _font.LineSpacing : 0f;
        }

        private string[] WrapTooltipText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            List<string> lines = new();
            string[] paragraphs = text.Replace("\r", string.Empty).Split('\n');
            foreach (string paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    continue;
                }

                string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    continue;
                }

                string currentLine = words[0];
                for (int i = 1; i < words.Length; i++)
                {
                    string candidate = currentLine + " " + words[i];
                    if (_font.MeasureString(candidate).X <= maxWidth)
                    {
                        currentLine = candidate;
                    }
                    else
                    {
                        lines.Add(currentLine);
                        currentLine = words[i];
                    }
                }

                lines.Add(currentLine);
            }

            return lines.ToArray();
        }

        private static string ResolveDisplayText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private IReadOnlyList<TooltipSection> BuildTooltipFooterSections(CharacterPart part, int quantity, int? maxStackSize)
        {
            List<TooltipSection> sections = new();

            string summaryLine = BuildEquipmentSummaryLine(part);
            if (!string.IsNullOrWhiteSpace(summaryLine))
            {
                sections.Add(new TooltipSection(summaryLine, new Color(181, 224, 255)));
            }

            string requirementLine = BuildEquipmentRequirementLine(part);
            if (!string.IsNullOrWhiteSpace(requirementLine))
            {
                sections.Add(new TooltipSection(requirementLine, Color.White));
            }

            string detailedRequirementLine = BuildDetailedRequirementLine(part);
            if (!string.IsNullOrWhiteSpace(detailedRequirementLine))
            {
                sections.Add(new TooltipSection(detailedRequirementLine, new Color(255, 232, 176)));
            }

            string metadataLine = BuildAdditionalEquipmentMetadataLine(part);
            if (!string.IsNullOrWhiteSpace(metadataLine))
            {
                sections.Add(new TooltipSection(metadataLine, new Color(255, 214, 156)));
            }

            AppendPotentialTooltipSections(sections, part);

            string expirationLine = BuildExpirationLine(part);
            if (!string.IsNullOrWhiteSpace(expirationLine))
            {
                sections.Add(new TooltipSection(expirationLine, new Color(255, 214, 156)));
            }

            if (quantity > 1)
            {
                sections.Add(new TooltipSection($"Quantity: {quantity}", Color.White));
            }

            string stackLine = InventoryItemMetadataResolver.BuildRuntimeFallbackStackLimitMetadataLine(
                maxStackSize,
                metadataLines: null);
            if (!string.IsNullOrWhiteSpace(stackLine))
            {
                sections.Add(new TooltipSection(stackLine, new Color(180, 255, 210)));
            }

            string eligibilityLine = BuildEquipmentEligibilityLine(part);
            if (!string.IsNullOrWhiteSpace(eligibilityLine))
            {
                sections.Add(new TooltipSection(
                    eligibilityLine,
                    eligibilityLine.StartsWith("Can equip", StringComparison.Ordinal)
                        ? new Color(176, 255, 176)
                        : new Color(255, 186, 186)));
            }

            return sections;
        }

        private List<TooltipLabeledValueRow> BuildTooltipStatRows(CharacterPart part)
        {
            List<TooltipLabeledValueRow> rows = new();
            AppendStatRow(rows, "STR:", null, part.BonusSTR, new Color(176, 255, 176), true);
            AppendStatRow(rows, "DEX:", null, part.BonusDEX, new Color(176, 255, 176), true);
            AppendStatRow(rows, "INT:", null, part.BonusINT, new Color(176, 255, 176), true);
            AppendStatRow(rows, "LUK:", null, part.BonusLUK, new Color(176, 255, 176), true);
            AppendStatRow(rows, "HP:", null, part.BonusHP, new Color(176, 255, 176), true);
            AppendStatRow(rows, "MP:", null, part.BonusMP, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("6"), part.BonusWeaponAttack, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("7"), part.BonusMagicAttack, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("8"), part.BonusWeaponDefense, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("9"), part.BonusMagicDefense, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("10"), part.BonusAccuracy, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("11"), part.BonusAvoidability, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("12"), part.BonusHands, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("13"), part.BonusSpeed, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("14"), part.BonusJump, new Color(176, 255, 176), true);
            AppendEnhancementStarRow(rows, part.EnhancementStarCount);
            AppendSellPriceRow(rows, part.SellPrice);
            AppendUpgradeSlotRow(rows, part);
            if (part is WeaponPart weapon)
            {
                AppendAttackSpeedRow(rows, weapon.AttackSpeed);
            }

            AppendGrowthRows(rows, part);

            return rows;
        }

        private List<TooltipLabeledValueRow> BuildTooltipRequirementRows(CharacterPart part)
        {
            List<TooltipLabeledValueRow> rows = new();
            AppendRequirementRow(rows, "reqLEV", part.RequiredLevel, CharacterBuild?.Level ?? int.MaxValue);
            AppendRequirementRow(rows, "reqSTR", part.RequiredSTR, CharacterBuild?.TotalSTR ?? int.MaxValue);
            AppendRequirementRow(rows, "reqDEX", part.RequiredDEX, CharacterBuild?.TotalDEX ?? int.MaxValue);
            AppendRequirementRow(rows, "reqINT", part.RequiredINT, CharacterBuild?.TotalINT ?? int.MaxValue);
            AppendRequirementRow(rows, "reqLUK", part.RequiredLUK, CharacterBuild?.TotalLUK ?? int.MaxValue);
            AppendRequirementRow(rows, "reqPOP", part.RequiredFame, CharacterBuild?.Fame ?? int.MaxValue);
            if (part.Durability.HasValue)
            {
                bool canUse = !part.MaxDurability.HasValue || part.Durability.Value > 0;
                string value = part.MaxDurability.HasValue && part.MaxDurability.Value > 0
                    ? $"{Math.Max(0, part.Durability.Value)}/{part.MaxDurability.Value}"
                    : Math.Max(0, part.Durability.Value).ToString(CultureInfo.InvariantCulture);
                rows.Add(new TooltipLabeledValueRow(
                    ResolveRequirementLabel(canUse, "durability"),
                    "Durability:",
                    value,
                    canUse ? new Color(181, 224, 255) : new Color(255, 186, 186),
                    BuildTooltipValueSegments(value, canUse, false)));
            }

            return rows;
        }

        private void AppendStatRow(List<TooltipLabeledValueRow> rows, string fallbackLabel, Texture2D labelTexture, int value, Color color, bool includePlusPrefix)
        {
            if (value <= 0)
            {
                return;
            }

            string valueText = includePlusPrefix ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
            rows.Add(new TooltipLabeledValueRow(
                labelTexture,
                fallbackLabel,
                valueText,
                color,
                BuildTooltipValueSegments(valueText, true, true)));
        }

        private void AppendUpgradeSlotRow(List<TooltipLabeledValueRow> rows, CharacterPart part)
        {
            int upgradeSlots = ResolveTooltipUpgradeSlotCount(part);
            if (upgradeSlots <= 0)
            {
                return;
            }

            string valueText = part.TotalUpgradeSlotCount.HasValue && part.TotalUpgradeSlotCount.Value > 0
                ? $"{upgradeSlots}/{part.TotalUpgradeSlotCount.Value}"
                : upgradeSlots.ToString(CultureInfo.InvariantCulture);
            rows.Add(new TooltipLabeledValueRow(
                ResolvePropertyLabel("16"),
                "Upgrades Available:",
                valueText,
                new Color(255, 232, 176),
                BuildTooltipValueSegments(valueText, true, false)));
        }

        private void AppendGrowthRows(List<TooltipLabeledValueRow> rows, CharacterPart part)
        {
            if (!part?.HasGrowthInfo ?? true)
            {
                return;
            }

            int currentLevel = Math.Max(1, part.GrowthLevel);
            int maxLevel = Math.Max(currentLevel, part.GrowthMaxLevel);
            bool growthEnabled = currentLevel < maxLevel;
            rows.Add(new TooltipLabeledValueRow(
                ResolveGrowthLabel(growthEnabled, "itemLEV"),
                "Item Level:",
                currentLevel.ToString(CultureInfo.InvariantCulture),
                growthEnabled ? new Color(181, 224, 255) : new Color(192, 192, 192),
                BuildTooltipValueSegments(currentLevel.ToString(CultureInfo.InvariantCulture), growthEnabled, true)));

            string expValue = growthEnabled
                ? $"{Math.Clamp(part.GrowthExpPercent, 0, 99)}%"
                : "MAX";
            rows.Add(new TooltipLabeledValueRow(
                ResolveGrowthLabel(growthEnabled, "itemEXP"),
                "Item EXP:",
                expValue,
                growthEnabled ? new Color(181, 224, 255) : new Color(192, 192, 192),
                BuildTooltipValueSegments(expValue, growthEnabled, true)));
        }

        private void AppendEnhancementStarRow(List<TooltipLabeledValueRow> rows, int enhancementStarCount)
        {
            if (enhancementStarCount <= 0)
            {
                return;
            }

            string valueText = enhancementStarCount.ToString(CultureInfo.InvariantCulture);
            rows.Add(new TooltipLabeledValueRow(
                _equipTooltipAssets?.StarLabel,
                "Stars:",
                valueText,
                new Color(255, 232, 176),
                BuildTooltipValueSegments(valueText, true, false)));
        }

        private void AppendSellPriceRow(List<TooltipLabeledValueRow> rows, int sellPrice)
        {
            if (sellPrice <= 0)
            {
                return;
            }

            string valueText = sellPrice.ToString(CultureInfo.InvariantCulture);
            rows.Add(new TooltipLabeledValueRow(
                _equipTooltipAssets?.MesosLabel,
                "Mesos:",
                valueText,
                new Color(255, 244, 186),
                BuildTooltipValueSegments(valueText, true, false)));
        }

        private void AppendAttackSpeedRow(List<TooltipLabeledValueRow> rows, int attackSpeed)
        {
            if (attackSpeed < 0)
            {
                return;
            }

            Texture2D speedTexture = ResolveSpeedTexture(attackSpeed);
            rows.Add(new TooltipLabeledValueRow(
                ResolvePropertyLabel("4"),
                "Attack Speed:",
                ResolveAttackSpeedText(attackSpeed),
                new Color(181, 224, 255),
                speedTexture != null ? new[] { new TooltipValueSegment(speedTexture) } : null));
        }

        private void AppendRequirementRow(List<TooltipLabeledValueRow> rows, string labelKey, int requiredValue, int actualValue)
        {
            if (requiredValue <= 0)
            {
                return;
            }

            bool canUse = actualValue >= requiredValue;
            rows.Add(new TooltipLabeledValueRow(
                ResolveRequirementLabel(canUse, labelKey),
                labelKey + ":",
                requiredValue.ToString(CultureInfo.InvariantCulture),
                canUse ? new Color(181, 224, 255) : new Color(255, 186, 186),
                BuildTooltipValueSegments(requiredValue.ToString(CultureInfo.InvariantCulture), canUse, false)));
        }

        private IReadOnlyList<TooltipValueSegment> BuildTooltipValueSegments(string valueText, bool enabled, bool preferGrowthDigits)
        {
            if (string.IsNullOrWhiteSpace(valueText) || _equipTooltipAssets == null)
            {
                return null;
            }

            IReadOnlyDictionary<string, Texture2D> source = preferGrowthDigits
                ? (enabled ? _equipTooltipAssets.GrowthEnabledLabels : _equipTooltipAssets.GrowthDisabledLabels)
                : (enabled ? _equipTooltipAssets.CanLabels : _equipTooltipAssets.CannotLabels);
            if (source == null)
            {
                return null;
            }

            List<TooltipValueSegment> segments = new(valueText.Length);
            if (string.Equals(valueText, "MAX", StringComparison.OrdinalIgnoreCase))
            {
                Texture2D maxTexture = TryResolveTooltipAsset(source, "max");
                return maxTexture == null ? null : new[] { new TooltipValueSegment(maxTexture) };
            }

            for (int i = 0; i < valueText.Length; i++)
            {
                char character = valueText[i];
                if (character == '+')
                {
                    continue;
                }

                string key = character switch
                {
                    '%' => "percent",
                    _ => char.IsDigit(character) ? character.ToString() : null
                };
                if (key == null)
                {
                    if (character == '/' || character == '-' || character == '.' || character == ',')
                    {
                        segments.Add(new TooltipValueSegment(null, character.ToString()));
                        continue;
                    }

                    return null;
                }

                Texture2D texture = TryResolveTooltipAsset(source, key);
                if (texture == null)
                {
                    return null;
                }

                segments.Add(new TooltipValueSegment(texture));
            }

            return segments.Count == 0 ? null : segments;
        }

        private float MeasureTooltipValueSegmentsHeight(IReadOnlyList<TooltipValueSegment> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return 0f;
            }

            int height = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].Texture != null)
                {
                    height = Math.Max(height, segments[i].Texture.Height);
                }
                else if (!string.IsNullOrEmpty(segments[i].Text) && _font != null)
                {
                    height = Math.Max(height, _font.LineSpacing);
                }
            }

            return height;
        }

        private void DrawTooltipValueSegments(SpriteBatch sprite, IReadOnlyList<TooltipValueSegment> segments, int x, float y, Color color)
        {
            if (segments == null || segments.Count == 0)
            {
                return;
            }

            int drawX = x;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].Texture != null)
                {
                    sprite.Draw(segments[i].Texture, new Vector2(drawX, y), Color.White);
                    drawX += segments[i].Texture.Width + TooltipBitmapGap;
                }
                else if (!string.IsNullOrEmpty(segments[i].Text))
                {
                    DrawTooltipText(sprite, segments[i].Text, new Vector2(drawX, y), color);
                    drawX += (int)Math.Ceiling(_font.MeasureString(segments[i].Text).X) + TooltipBitmapGap;
                }
            }
        }

        private float MeasureLabeledValueRowsHeight(IReadOnlyList<TooltipLabeledValueRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return 0f;
            }

            float height = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                height += MeasureLabeledValueRowHeight(rows[i]);
                if (i < rows.Count - 1)
                {
                    height += 2f;
                }
            }

            return height;
        }

        private float MeasureLabeledValueRowHeight(TooltipLabeledValueRow row)
        {
            float labelHeight = row.LabelTexture?.Height ?? (_font?.LineSpacing ?? 0);
            float valueHeight = MeasureTooltipValueSegmentsHeight(row.ValueSegments);
            return Math.Max(labelHeight, Math.Max(valueHeight, _font?.LineSpacing ?? 0));
        }

        private float DrawLabeledValueRows(SpriteBatch sprite, int x, float y, IReadOnlyList<TooltipLabeledValueRow> rows)
        {
            if (rows == null)
            {
                return y;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                y = DrawLabeledValueRow(sprite, x, y, rows[i]);
                if (i < rows.Count - 1)
                {
                    y += 2f;
                }
            }

            return y;
        }

        private float DrawLabeledValueRow(SpriteBatch sprite, int x, float y, TooltipLabeledValueRow row)
        {
            int valueX = x;
            if (row.LabelTexture != null)
            {
                sprite.Draw(row.LabelTexture, new Vector2(x, y), Color.White);
                valueX = x + row.LabelTexture.Width + 6;
            }
            else if (!string.IsNullOrWhiteSpace(row.FallbackLabel))
            {
                DrawTooltipText(sprite, row.FallbackLabel, new Vector2(x, y), new Color(181, 224, 255));
                valueX = x + (int)Math.Ceiling(_font.MeasureString(row.FallbackLabel).X) + 6;
            }

            if (row.ValueSegments != null && row.ValueSegments.Count > 0)
            {
                DrawTooltipValueSegments(sprite, row.ValueSegments, valueX, y, row.ValueColor);
            }
            else if (!string.IsNullOrWhiteSpace(row.ValueText))
            {
                DrawTooltipText(sprite, row.ValueText, new Vector2(valueX, y), row.ValueColor);
            }

            return y + MeasureLabeledValueRowHeight(row);
        }

        private List<Texture2D> BuildTooltipJobBadges(int requiredJobMask)
        {
            List<Texture2D> textures = new(6);
            AppendJobBadgeTexture(textures, requiredJobMask, 1, "beginner");
            AppendJobBadgeTexture(textures, requiredJobMask, 2, "warrior");
            AppendJobBadgeTexture(textures, requiredJobMask, 4, "magician");
            AppendJobBadgeTexture(textures, requiredJobMask, 8, "bowman");
            AppendJobBadgeTexture(textures, requiredJobMask, 16, "thief");
            AppendJobBadgeTexture(textures, requiredJobMask, 32, "pirate");
            return textures;
        }

        private void AppendJobBadgeTexture(List<Texture2D> textures, int requiredJobMask, int maskBit, string key)
        {
            if ((requiredJobMask & maskBit) == 0)
            {
                return;
            }

            Texture2D texture = ResolveRequirementLabel(true, key);
            if (texture != null)
            {
                textures.Add(texture);
            }
        }

        private float DrawJobBadgeRow(SpriteBatch sprite, int x, float y, IReadOnlyList<Texture2D> textures)
        {
            int drawX = x;
            for (int i = 0; i < textures.Count; i++)
            {
                Texture2D texture = textures[i];
                if (texture == null)
                {
                    continue;
                }

                sprite.Draw(texture, new Vector2(drawX, y), Color.White);
                drawX += texture.Width + 4;
            }

            return y + 13f;
        }

        private List<(string[] Lines, Color Color, float Height)> BuildWrappedTooltipSections(IReadOnlyList<TooltipSection> sections)
        {
            List<(string[] Lines, Color Color, float Height)> wrappedSections = new();
            if (sections == null)
            {
                return wrappedSections;
            }

            int tooltipWidth = ResolveTooltipWidth();
            int contentWidth = tooltipWidth - (TooltipPadding * 2);
            for (int i = 0; i < sections.Count; i++)
            {
                string[] lines = WrapTooltipText(sections[i].Text, contentWidth);
                wrappedSections.Add((lines, sections[i].Color, MeasureLinesHeight(lines)));
            }

            return wrappedSections;
        }

        private float MeasureWrappedSectionHeight(IReadOnlyList<(string[] Lines, Color Color, float Height)> sections)
        {
            if (sections == null || sections.Count == 0)
            {
                return 0f;
            }

            float height = 0f;
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].Height <= 0f)
                {
                    continue;
                }

                if (height > 0f)
                {
                    height += TooltipSectionGap;
                }

                height += sections[i].Height;
            }

            return height;
        }

        private void DrawWrappedSections(SpriteBatch sprite, int x, float y, IReadOnlyList<(string[] Lines, Color Color, float Height)> sections)
        {
            if (sections == null)
            {
                return;
            }

            float sectionY = y;
            for (int i = 0; i < sections.Count; i++)
            {
                (string[] lines, Color color, float height) = sections[i];
                if (height <= 0f)
                {
                    continue;
                }

                if (sectionY > y)
                {
                    sectionY += TooltipSectionGap;
                }

                DrawTooltipLines(sprite, lines, x, sectionY, color);
                sectionY += height;
            }
        }

        private Texture2D ResolveRequirementLabel(bool canUse, string key)
        {
            if (_equipTooltipAssets == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            IReadOnlyDictionary<string, Texture2D> source = canUse
                ? _equipTooltipAssets.CanLabels
                : _equipTooltipAssets.CannotLabels;
            return TryResolveTooltipAsset(source, key);
        }

        private Texture2D ResolvePropertyLabel(string key)
        {
            return TryResolveTooltipAsset(_equipTooltipAssets?.PropertyLabels, key);
        }

        private Texture2D ResolveGrowthLabel(bool enabled, string key)
        {
            IReadOnlyDictionary<string, Texture2D> source = enabled
                ? _equipTooltipAssets?.GrowthEnabledLabels
                : _equipTooltipAssets?.GrowthDisabledLabels;
            return TryResolveTooltipAsset(source, key);
        }

        private Texture2D ResolveSpeedTexture(int attackSpeed)
        {
            return TryResolveTooltipAsset(_equipTooltipAssets?.SpeedLabels, Math.Clamp(attackSpeed, 0, 6).ToString(CultureInfo.InvariantCulture));
        }

        private Texture2D ResolveCategoryTexture(CharacterPart part)
        {
            if (_equipTooltipAssets == null || part == null || part.ItemId <= 0)
            {
                return null;
            }

            int itemCategory = part.ItemId / 10000;
            if (part is WeaponPart)
            {
                Texture2D weaponTexture = TryResolveTooltipAsset(
                    _equipTooltipAssets.WeaponCategoryLabels,
                    (itemCategory - 100).ToString(CultureInfo.InvariantCulture));
                if (weaponTexture != null)
                {
                    return weaponTexture;
                }
            }

            string categoryKey = itemCategory switch
            {
                100 => "1",
                101 => "2",
                102 => "3",
                103 => "4",
                104 => "5",
                105 => "21",
                106 => "6",
                107 => "7",
                108 => "8",
                109 => "10",
                110 => "9",
                111 => "12",
                _ => null
            };

            return categoryKey == null ? null : TryResolveTooltipAsset(_equipTooltipAssets.ItemCategoryLabels, categoryKey);
        }

        private static Texture2D TryResolveTooltipAsset(IReadOnlyDictionary<string, Texture2D> assets, string key)
        {
            if (assets == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return assets.TryGetValue(key, out Texture2D texture) ? texture : null;
        }

        private static string ResolveCategoryFallbackText(CharacterPart part)
        {
            if (part is WeaponPart weapon && !string.IsNullOrWhiteSpace(weapon.WeaponType))
            {
                return weapon.WeaponType;
            }

            return part?.ItemCategory ?? string.Empty;
        }

        private static string BuildEquipmentSummaryLine(CharacterPart part)
        {
            List<string> segments = new();
            if (part.ItemId > 0)
            {
                segments.Add($"Item ID: {part.ItemId}");
            }

            if (!string.IsNullOrWhiteSpace(part.ItemCategory))
            {
                segments.Add(part.ItemCategory);
            }

            AppendStatSegment(segments, "STR", part.BonusSTR);
            AppendStatSegment(segments, "DEX", part.BonusDEX);
            AppendStatSegment(segments, "INT", part.BonusINT);
            AppendStatSegment(segments, "LUK", part.BonusLUK);
            AppendStatSegment(segments, "HP", part.BonusHP);
            AppendStatSegment(segments, "MP", part.BonusMP);
            AppendStatSegment(segments, "ATT", part.BonusWeaponAttack);
            AppendStatSegment(segments, "M.ATT", part.BonusMagicAttack);
            AppendStatSegment(segments, "DEF", part.BonusWeaponDefense);
            AppendStatSegment(segments, "M.DEF", part.BonusMagicDefense);
            AppendStatSegment(segments, "ACC", part.BonusAccuracy);
            AppendStatSegment(segments, "AVOID", part.BonusAvoidability);
            AppendStatSegment(segments, "Hands", part.BonusHands);
            AppendStatSegment(segments, "Speed", part.BonusSpeed);
            AppendStatSegment(segments, "Jump", part.BonusJump);

            int upgradeSlots = ResolveTooltipUpgradeSlotCount(part);
            if (upgradeSlots > 0)
            {
                if (part.TotalUpgradeSlotCount.HasValue && part.TotalUpgradeSlotCount.Value > 0)
                {
                    segments.Add($"Slots {upgradeSlots}/{part.TotalUpgradeSlotCount.Value}");
                }
                else
                {
                    segments.Add($"Slots {upgradeSlots}");
                }
            }

            if (part is WeaponPart weapon && weapon.AttackSpeed > 0)
            {
                segments.Add($"Speed {weapon.AttackSpeed}");
            }

            return string.Join("  ", segments);
        }

        private static string BuildEquipmentRequirementLine(CharacterPart part)
        {
            List<string> segments = new();
            if (part.RequiredLevel > 0)
            {
                segments.Add($"Req Lv {part.RequiredLevel}");
            }

            AppendRequirementSegment(segments, "STR", part.RequiredSTR);
            AppendRequirementSegment(segments, "DEX", part.RequiredDEX);
            AppendRequirementSegment(segments, "INT", part.RequiredINT);
            AppendRequirementSegment(segments, "LUK", part.RequiredLUK);

            if (part.RequiredFame > 0)
            {
                segments.Add($"Req Fame {part.RequiredFame}");
            }

            return string.Join("  ", segments);
        }

        private static string BuildDetailedRequirementLine(CharacterPart part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            string requiredJobs = ResolveRequiredJobNames(part.RequiredJobMask);
            return string.IsNullOrWhiteSpace(requiredJobs)
                ? string.Empty
                : $"Req Job {requiredJobs}";
        }

        private string BuildEquipmentEligibilityLine(CharacterPart part)
        {
            if (part == null || CharacterBuild == null)
            {
                return string.Empty;
            }

            if (MeetsEquipRequirements(part, CharacterBuild))
            {
                return "Can equip";
            }

            List<string> failures = new();
            if (part.RequiredLevel > 0 && CharacterBuild.Level < part.RequiredLevel) failures.Add($"Lv {part.RequiredLevel}");
            if (part.RequiredSTR > 0 && CharacterBuild.TotalSTR < part.RequiredSTR) failures.Add($"STR {part.RequiredSTR}");
            if (part.RequiredDEX > 0 && CharacterBuild.TotalDEX < part.RequiredDEX) failures.Add($"DEX {part.RequiredDEX}");
            if (part.RequiredINT > 0 && CharacterBuild.TotalINT < part.RequiredINT) failures.Add($"INT {part.RequiredINT}");
            if (part.RequiredLUK > 0 && CharacterBuild.TotalLUK < part.RequiredLUK) failures.Add($"LUK {part.RequiredLUK}");
            if (part.RequiredFame > 0 && CharacterBuild.Fame < part.RequiredFame) failures.Add($"Fame {part.RequiredFame}");
            if (part.RequiredJobMask != 0 && !MatchesRequiredJobMask(part.RequiredJobMask, CharacterBuild.Job))
            {
                string requiredJobs = ResolveRequiredJobNames(part.RequiredJobMask);
                failures.Add(string.IsNullOrWhiteSpace(requiredJobs) ? "job" : requiredJobs);
            }

            return failures.Count == 0 ? "Cannot equip" : $"Cannot equip: {string.Join(", ", failures)}";
        }

        private static string BuildAdditionalEquipmentMetadataLine(CharacterPart part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            List<string> segments = new();
            if (part.TradeAvailable > 0)
            {
                segments.Add($"Trade available {part.TradeAvailable} time{(part.TradeAvailable == 1 ? string.Empty : "s")}");
            }

            if (part.IsTradeBlocked)
            {
                segments.Add("Untradeable");
            }

            if (part.IsOneOfAKind)
            {
                segments.Add("One-of-a-kind item");
            }

            if (part.IsUniqueEquipItem)
            {
                segments.Add("Can only be equipped once");
            }

            if (part.IsEquipTradeBlocked)
            {
                segments.Add("Untradeable after equip");
            }

            if (part.IsNotForSale)
            {
                segments.Add("Not for sale");
            }

            if (part.IsAccountSharable)
            {
                segments.Add("Account-sharable");
            }

            if (part.HasAccountShareTag)
            {
                segments.Add("Account-share tagged");
            }

            if (part.IsNoMoveToLocker)
            {
                segments.Add("Cannot be moved to storage");
            }

            if (part.KnockbackRate > 0)
            {
                segments.Add($"Knockback resistance {part.KnockbackRate}%");
            }

            if (part.IsTimeLimited)
            {
                segments.Add("Time-limited item");
            }

            if (part.Durability.HasValue)
            {
                if (part.MaxDurability.HasValue && part.MaxDurability.Value > 0)
                {
                    segments.Add($"Durability {Math.Max(0, part.Durability.Value)}/{part.MaxDurability.Value}");
                }
                else
                {
                    segments.Add($"Durability {Math.Max(0, part.Durability.Value)}");
                }
            }

            return string.Join("  ", segments);
        }

        private static bool MeetsEquipRequirements(CharacterPart part, CharacterBuild build)
        {
            if (part == null || build == null)
            {
                return true;
            }

            return (part.RequiredLevel <= 0 || build.Level >= part.RequiredLevel)
                   && (part.RequiredSTR <= 0 || build.TotalSTR >= part.RequiredSTR)
                   && (part.RequiredDEX <= 0 || build.TotalDEX >= part.RequiredDEX)
                   && (part.RequiredINT <= 0 || build.TotalINT >= part.RequiredINT)
                   && (part.RequiredLUK <= 0 || build.TotalLUK >= part.RequiredLUK)
                   && (part.RequiredFame <= 0 || build.Fame >= part.RequiredFame)
                   && (part.RequiredJobMask == 0 || MatchesRequiredJobMask(part.RequiredJobMask, build.Job));
        }

        private static bool MatchesRequiredJobMask(int requiredJobMask, int jobId)
        {
            if (requiredJobMask == 0)
            {
                return true;
            }

            int jobGroup = Math.Abs(jobId) / 100;
            return jobGroup switch
            {
                0 => (requiredJobMask & 1) != 0,
                1 => (requiredJobMask & 2) != 0,
                2 => (requiredJobMask & 4) != 0,
                3 => (requiredJobMask & 8) != 0,
                4 => (requiredJobMask & 16) != 0,
                5 => (requiredJobMask & 32) != 0,
                _ => false
            };
        }

        private static string ResolveRequiredJobNames(int requiredJobMask)
        {
            if (requiredJobMask == 0)
            {
                return string.Empty;
            }

            List<string> jobNames = new();
            AppendRequiredJobName(jobNames, requiredJobMask, 1, "Beginner");
            AppendRequiredJobName(jobNames, requiredJobMask, 2, "Warrior");
            AppendRequiredJobName(jobNames, requiredJobMask, 4, "Magician");
            AppendRequiredJobName(jobNames, requiredJobMask, 8, "Bowman");
            AppendRequiredJobName(jobNames, requiredJobMask, 16, "Thief");
            AppendRequiredJobName(jobNames, requiredJobMask, 32, "Pirate");
            return string.Join("/", jobNames);
        }

        private static void AppendRequiredJobName(List<string> jobNames, int requiredJobMask, int maskBit, string jobName)
        {
            if ((requiredJobMask & maskBit) != 0)
            {
                jobNames.Add(jobName);
            }
        }

        private static int ResolveTooltipUpgradeSlotCount(CharacterPart part)
        {
            if (part == null)
            {
                return 0;
            }

            if (part.RemainingUpgradeSlotCount.HasValue)
            {
                return Math.Max(0, part.RemainingUpgradeSlotCount.Value);
            }

            return Math.Max(0, part.UpgradeSlots);
        }

        private static void AppendPotentialTooltipSections(List<TooltipSection> sections, CharacterPart part)
        {
            if (sections == null || part == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(part.PotentialTierText))
            {
                sections.Add(new TooltipSection(part.PotentialTierText, new Color(214, 190, 255)));
            }

            if (part.PotentialLines == null)
            {
                return;
            }

            for (int i = 0; i < part.PotentialLines.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(part.PotentialLines[i]))
                {
                    sections.Add(new TooltipSection(part.PotentialLines[i], new Color(236, 224, 255)));
                }
            }
        }

        private static string BuildExpirationLine(CharacterPart part)
        {
            if (!part?.ExpirationDateUtc.HasValue ?? true)
            {
                return string.Empty;
            }

            return $"Expires {part.ExpirationDateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}";
        }

        private static void AppendStatSegment(List<string> segments, string label, int value)
        {
            if (value > 0)
            {
                segments.Add($"{label} +{value}");
            }
        }

        private static void AppendRequirementSegment(List<string> segments, string label, int value)
        {
            if (value > 0)
            {
                segments.Add($"{label} {value}");
            }
        }

        private static string ResolveAttackSpeedText(int attackSpeed)
        {
            return Math.Clamp(attackSpeed, 0, 6) switch
            {
                0 => "Fastest",
                1 => "Faster",
                2 => "Fast",
                3 => "Normal",
                4 => "Slow",
                5 => "Slower",
                6 => "Slowest",
                _ => string.Empty
            };
        }
    }
}
