using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Util;
using MapleLib.PacketLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class FieldMessageBoxRuntime
    {
        private const int DefaultItemId = 5370000;
        private const int DefaultFrameDelayMs = 100;
        private const int DefaultLeaveFadeMs = 1000;
        private const int DefaultBoxOffsetX = -3;
        private const int DefaultBoxOffsetY = -100;
        private const float DefaultBobAmplitude = 3f;
        private const float DefaultBobPeriodMs = 360f;
        private const int MinBoardWidth = 92;
        private const int MaxBodyLineCount = 4;
        private const int CreateFailedStringPoolId = 0x1EA;
        private const string ClientMessageBoxPropertyName = "messageBox";
        private const string ChalkboardSamplePropertyName = "sample";
        private const string UiPropertyName = "ui";
        private const string UiTopPropertyName = "t";
        private const string UiCenterPropertyName = "c";
        private const string UiBottomPropertyName = "s";
        private const string CreateFailedFallbackText = "The client refused to create the field message-box.";

        private readonly Dictionary<int, FieldMessageBoxEntry> _entries = new();
        private readonly List<LeavingMessageBoxEntry> _leavingEntries = new();
        private readonly Dictionary<int, MessageBoxVisual> _visualCache = new();
        private readonly Dictionary<int, string> _itemNameCache = new();
        private GraphicsDevice _graphicsDevice;
        private Texture2D _pixelTexture;
        private int _nextLocalMessageBoxId = 1;
        private string _statusMessage = "Field message-box pool idle.";

        internal int ActiveCount => _entries.Count + _leavingEntries.Count;

        internal void Initialize(GraphicsDevice graphicsDevice)
        {
            if (_graphicsDevice == graphicsDevice && _pixelTexture != null)
            {
                return;
            }

            _graphicsDevice = graphicsDevice;
            _pixelTexture = graphicsDevice == null ? null : new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture?.SetData(new[] { Color.White });
        }

        internal void Clear()
        {
            _entries.Clear();
            _leavingEntries.Clear();
            _statusMessage = "Field message-box pool cleared.";
        }

        internal string DescribeStatus()
        {
            if (_entries.Count == 0)
            {
                return _statusMessage;
            }

            return $"{_entries.Count} field message-box entr{(_entries.Count == 1 ? "y" : "ies")} active. {_statusMessage}";
        }

        internal string CreateLocalMessageBox(
            int itemId,
            string messageText,
            string characterName,
            Point hostPosition,
            int currentTick,
            int? messageBoxId = null)
        {
            int resolvedItemId = itemId > 0 ? itemId : DefaultItemId;
            int resolvedId = messageBoxId.GetValueOrDefault();
            if (resolvedId <= 0)
            {
                resolvedId = _nextLocalMessageBoxId++;
            }

            string trimmedMessage = string.IsNullOrWhiteSpace(messageText) ? "..." : messageText.Trim();
            string trimmedName = string.IsNullOrWhiteSpace(characterName) ? "Player" : characterName.Trim();

            var entry = new FieldMessageBoxEntry(
                resolvedId,
                resolvedItemId,
                trimmedMessage,
                trimmedName,
                hostPosition,
                new Point(hostPosition.X + DefaultBoxOffsetX, hostPosition.Y + DefaultBoxOffsetY),
                ResolveVisual(resolvedItemId),
                ResolveItemName(resolvedItemId),
                currentTick);

            _entries[resolvedId] = entry;
            _statusMessage = $"Registered field message-box {resolvedId} for {trimmedName} using item {resolvedItemId}.";
            return _statusMessage;
        }

        internal string RemoveMessageBox(int messageBoxId, bool immediate, int currentTick)
        {
            if (!_entries.TryGetValue(messageBoxId, out FieldMessageBoxEntry entry))
            {
                int leavingIndex = _leavingEntries.FindIndex(leaving => leaving.Id == messageBoxId);
                if (leavingIndex >= 0)
                {
                    _leavingEntries.RemoveAt(leavingIndex);
                    _statusMessage = $"Removed field message-box {messageBoxId} from the leave-field queue.";
                    return _statusMessage;
                }

                return $"Field message-box {messageBoxId} is not active.";
            }

            _entries.Remove(messageBoxId);
            if (immediate)
            {
                _statusMessage = $"Removed field message-box {messageBoxId} immediately.";
                return _statusMessage;
            }

            _leavingEntries.Add(LeavingMessageBoxEntry.FromEntry(entry, currentTick));
            _statusMessage = $"Field message-box {messageBoxId} began its leave-field fade.";
            return _statusMessage;
        }

        internal string ApplyCreateFailed()
        {
            _statusMessage = $"{CreateFailedFallbackText} [client notice StringPool 0x{CreateFailedStringPoolId:X}]";
            return _statusMessage;
        }

        internal bool TryApplyPacket(int packetType, byte[] payload, int currentTick, out string message)
        {
            message = string.Empty;

            switch (packetType)
            {
                case 325:
                    message = ApplyCreateFailed();
                    return true;

                case 326:
                    return TryApplyEnterFieldPacket(payload, currentTick, out message);

                case 327:
                    return TryApplyLeaveFieldPacket(payload, currentTick, out message);

                default:
                    message = $"Unsupported message-box packet type {packetType}.";
                    return false;
            }
        }

        internal void Update(int currentTick)
        {
            if (_entries.Count == 0 && _leavingEntries.Count == 0)
            {
                return;
            }

            foreach (FieldMessageBoxEntry entry in _entries.Values)
            {
                entry.Update(currentTick);
            }

            int removedLeavingCount = _leavingEntries.RemoveAll(leaving => leaving.ShouldRemove(currentTick));
            if (removedLeavingCount == 0)
            {
                return;
            }

            _statusMessage = removedLeavingCount == 1
                ? "Field message-box leave animation finished."
                : $"{removedLeavingCount} field message-box leave animations finished.";
        }

        internal void Draw(
            SpriteBatch spriteBatch,
            SpriteFont font,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY,
            int renderWidth,
            int renderHeight,
            int currentTick)
        {
            if (spriteBatch == null || font == null || _pixelTexture == null || (_entries.Count == 0 && _leavingEntries.Count == 0))
            {
                return;
            }

            IEnumerable<IMessageBoxDrawableEntry> drawEntries = _entries.Values.Cast<IMessageBoxDrawableEntry>()
                .Concat(_leavingEntries)
                .OrderBy(messageBox => messageBox.LayerPosition.Y);

            foreach (IMessageBoxDrawableEntry entry in drawEntries)
            {
                DrawEntry(spriteBatch, font, entry, mapShiftX, mapShiftY, mapCenterX, mapCenterY, renderWidth, renderHeight, currentTick);
            }
        }

        private bool TryApplyEnterFieldPacket(byte[] payload, int currentTick, out string message)
        {
            message = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                message = "Message-box enter-field payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                int messageBoxId = reader.ReadInt();
                int itemId = reader.ReadInt();
                string text = reader.ReadMapleString();
                string characterName = reader.ReadMapleString();
                short hostX = reader.ReadShort();
                short hostY = reader.ReadShort();

                CreateLocalMessageBox(
                    itemId,
                    text,
                    characterName,
                    new Point(hostX, hostY),
                    currentTick,
                    messageBoxId);

                message = $"Applied message-box enter-field packet for {characterName} ({messageBoxId}).";
                return true;
            }
            catch (EndOfStreamException)
            {
                message = "Message-box enter-field packet ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                message = "Message-box enter-field packet could not be read.";
                return false;
            }
        }

        private bool TryApplyLeaveFieldPacket(byte[] payload, int currentTick, out string message)
        {
            message = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                message = "Message-box leave-field payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                bool immediate = reader.ReadByte() != 0;
                int messageBoxId = reader.ReadInt();
                message = RemoveMessageBox(messageBoxId, immediate, currentTick);
                return true;
            }
            catch (EndOfStreamException)
            {
                message = "Message-box leave-field packet ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                message = "Message-box leave-field packet could not be read.";
                return false;
            }
        }

        private void DrawEntry(
            SpriteBatch spriteBatch,
            SpriteFont font,
            IMessageBoxDrawableEntry entry,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY,
            int renderWidth,
            int renderHeight,
            int currentTick)
        {
            Point drawAnchor = new(
                entry.LayerPosition.X - mapShiftX + mapCenterX,
                entry.LayerPosition.Y - mapShiftY + mapCenterY);

            float alpha = entry.GetAlpha(currentTick);
            if (alpha <= 0f)
            {
                return;
            }

            MessageBoxVisual visual = entry.Visual;
            Texture2D frameTexture = entry.GetDisplayTexture();
            Point frameOrigin = entry.GetDisplayOrigin();
            Rectangle boardBounds;
            int bobOffsetY = entry.GetVerticalFloatOffset(currentTick);
            Point floatedAnchor = new(drawAnchor.X, drawAnchor.Y + bobOffsetY);

            if (frameTexture != null)
            {
                Vector2 framePosition = new(floatedAnchor.X - frameOrigin.X, floatedAnchor.Y - frameOrigin.Y);
                boardBounds = new Rectangle((int)framePosition.X, (int)framePosition.Y, frameTexture.Width, frameTexture.Height);
                if (boardBounds.Right < 0 || boardBounds.Bottom < 0 || boardBounds.Left > renderWidth || boardBounds.Top > renderHeight)
                {
                    return;
                }

                spriteBatch.Draw(frameTexture, framePosition, Color.White * alpha);
            }
            else
            {
                boardBounds = BuildFallbackBounds(floatedAnchor, font, entry);
                if (boardBounds.Right < 0 || boardBounds.Bottom < 0 || boardBounds.Left > renderWidth || boardBounds.Top > renderHeight)
                {
                    return;
                }

                DrawFallbackBoard(spriteBatch, boardBounds, alpha);
            }

            DrawBoardText(spriteBatch, font, boardBounds, entry, alpha);
        }

        private void DrawFallbackBoard(SpriteBatch spriteBatch, Rectangle bounds, float alpha)
        {
            Color background = new Color(62, 86, 63) * (0.94f * alpha);
            Color inner = new Color(31, 46, 31) * (0.96f * alpha);
            Color border = new Color(204, 190, 123) * alpha;

            spriteBatch.Draw(_pixelTexture, bounds, background);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8), inner);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), border);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), border);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), border);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), border);
        }

        private void DrawBoardText(SpriteBatch spriteBatch, SpriteFont font, Rectangle boardBounds, IMessageBoxDrawableEntry entry, float alpha)
        {
            MessageBoxTextLayout layout = entry.Visual?.TextLayout ?? MessageBoxTextLayout.Default;
            int textRegionWidth = Math.Max(MinBoardWidth, boardBounds.Width - layout.PaddingLeft - layout.PaddingRight);
            string[] bodyLines = WrapText(font, entry.MessageText, textRegionWidth).Take(layout.MaxLineCount).ToArray();
            if (bodyLines.Length == 0)
            {
                bodyLines = new[] { "..." };
            }

            int totalHeight = bodyLines.Length * font.LineSpacing;
            float startY = boardBounds.Y + layout.PaddingTop;
            int availableHeight = Math.Max(font.LineSpacing, boardBounds.Height - layout.PaddingTop - layout.PaddingBottom);
            if (layout.CenterVertically && totalHeight < availableHeight)
            {
                startY += (availableHeight - totalHeight) * 0.5f;
            }

            Color textColor = layout.TextColor * alpha;
            for (int i = 0; i < bodyLines.Length; i++)
            {
                string line = bodyLines[i];
                Vector2 size = font.MeasureString(line);
                float x = layout.CenterHorizontally
                    ? boardBounds.X + (boardBounds.Width - size.X) * 0.5f
                    : boardBounds.X + layout.PaddingLeft;
                float y = startY + (i * font.LineSpacing);
                spriteBatch.DrawString(font, line, new Vector2((float)Math.Round(x), (float)Math.Round(y)), textColor);
            }
        }

        private Rectangle BuildFallbackBounds(Point drawAnchor, SpriteFont font, IMessageBoxDrawableEntry entry)
        {
            const int width = 220;
            string[] bodyLines = WrapText(font, entry.MessageText, width - 26).Take(MaxBodyLineCount).ToArray();
            int lineCount = Math.Max(1, bodyLines.Length);
            int height = 16 + (font.LineSpacing * lineCount) + 8;
            return new Rectangle(drawAnchor.X - (width / 2), drawAnchor.Y - height, width, height);
        }

        private MessageBoxVisual ResolveVisual(int itemId)
        {
            if (_visualCache.TryGetValue(itemId, out MessageBoxVisual cached))
            {
                return cached;
            }

            MessageBoxVisual visual = LoadVisual(itemId);
            _visualCache[itemId] = visual;
            return visual;
        }

        private MessageBoxVisual LoadVisual(int itemId)
        {
            if (_graphicsDevice == null ||
                itemId <= 0 ||
                !InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            string itemNodeName = itemId.ToString("D7", CultureInfo.InvariantCulture);
            WzSubProperty itemProperty = itemImage[itemNodeName] as WzSubProperty;
            if (itemProperty == null)
            {
                return null;
            }

            WzSubProperty infoProperty = itemProperty["info"] as WzSubProperty;
            Texture2D iconTexture = LoadCanvasTexture(infoProperty?["iconRaw"] as WzCanvasProperty)
                                    ?? LoadCanvasTexture(infoProperty?["icon"] as WzCanvasProperty);

            if (TryLoadClientMessageBoxVisual(itemProperty, infoProperty, itemId, iconTexture, out MessageBoxVisual visual))
            {
                return visual;
            }

            return iconTexture == null
                ? null
                : new MessageBoxVisual(Array.Empty<Texture2D>(), Array.Empty<Point>(), Array.Empty<int>(), iconTexture, MessageBoxTextLayout.Default);
        }

        private bool TryLoadClientMessageBoxVisual(
            WzSubProperty itemProperty,
            WzSubProperty infoProperty,
            int itemId,
            Texture2D iconTexture,
            out MessageBoxVisual visual)
        {
            visual = null;

            if (TryLoadNamedVisual(itemProperty, ClientMessageBoxPropertyName, ClientMessageBoxPropertyName, out visual) ||
                TryLoadNamedVisual(infoProperty, ClientMessageBoxPropertyName, ClientMessageBoxPropertyName, out visual))
            {
                visual = visual with { IconTexture = iconTexture ?? visual.IconTexture };
                return true;
            }

            if (IsKnownChalkboardItem(itemId) &&
                (TryLoadNamedVisual(infoProperty, ChalkboardSamplePropertyName, ClientMessageBoxPropertyName, out visual) ||
                 TryLoadNamedVisual(itemProperty, ChalkboardSamplePropertyName, ClientMessageBoxPropertyName, out visual)))
            {
                visual = visual with { IconTexture = iconTexture ?? visual.IconTexture };
                return true;
            }

            if (TryLoadUiComposedVisual(itemProperty, out visual))
            {
                visual = visual with { IconTexture = iconTexture ?? visual.IconTexture };
                return true;
            }

            foreach (WzImageProperty child in itemProperty.WzProperties)
            {
                if (child is not WzImageProperty property)
                {
                    continue;
                }

                WzImageProperty linked = property.GetLinkedWzImageProperty();
                if (linked is WzCanvasProperty canvas && string.Equals(property.Name, ChalkboardSamplePropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    if (TryLoadVisualFromCanvas(canvas, ClientMessageBoxPropertyName, out visual))
                    {
                        visual = visual with { IconTexture = iconTexture ?? visual.IconTexture };
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryLoadUiComposedVisual(WzSubProperty itemProperty, out MessageBoxVisual visual)
        {
            visual = null;
            if (_graphicsDevice == null || itemProperty == null || itemProperty[UiPropertyName] is not WzSubProperty uiProperty)
            {
                return false;
            }

            Texture2D topTexture = LoadCanvasTexture(uiProperty[UiTopPropertyName] as WzCanvasProperty);
            Texture2D centerTexture = LoadCanvasTexture(uiProperty[UiCenterPropertyName] as WzCanvasProperty);
            Texture2D bottomTexture = LoadCanvasTexture(uiProperty[UiBottomPropertyName] as WzCanvasProperty);
            if (topTexture == null || centerTexture == null || bottomTexture == null)
            {
                return false;
            }

            int lineCount = ResolveUiSampleLineCount(itemProperty);
            Texture2D composedTexture = ComposeUiBoardTexture(topTexture, centerTexture, bottomTexture, lineCount);
            if (composedTexture == null)
            {
                return false;
            }

            MessageBoxTextLayout textLayout = ResolveTextLayout(UiPropertyName, composedTexture) with
            {
                MaxLineCount = lineCount
            };

            visual = new MessageBoxVisual(
                new[] { composedTexture },
                new[] { new Point(composedTexture.Width / 2, composedTexture.Height) },
                new[] { DefaultFrameDelayMs },
                null,
                textLayout);
            return true;
        }

        private Texture2D ComposeUiBoardTexture(Texture2D topTexture, Texture2D centerTexture, Texture2D bottomTexture, int lineCount)
        {
            if (_graphicsDevice == null || topTexture == null || centerTexture == null || bottomTexture == null)
            {
                return null;
            }

            int safeLineCount = Math.Clamp(lineCount, 1, MaxBodyLineCount);
            int width = Math.Max(topTexture.Width, Math.Max(centerTexture.Width, bottomTexture.Width));
            int height = topTexture.Height + (centerTexture.Height * safeLineCount) + bottomTexture.Height;
            var renderTarget = new RenderTarget2D(_graphicsDevice, width, height);

            RenderTargetBinding[] previousTargets = _graphicsDevice.GetRenderTargets();
            PresentationParameters presentationParameters = _graphicsDevice.PresentationParameters;
            Viewport previousViewport = _graphicsDevice.Viewport;

            try
            {
                _graphicsDevice.SetRenderTarget(renderTarget);
                _graphicsDevice.Viewport = new Viewport(0, 0, width, height);
                _graphicsDevice.Clear(Color.Transparent);

                using SpriteBatch spriteBatch = new(_graphicsDevice);
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

                int topX = (width - topTexture.Width) / 2;
                int centerX = (width - centerTexture.Width) / 2;
                int bottomX = (width - bottomTexture.Width) / 2;
                int currentY = 0;

                spriteBatch.Draw(topTexture, new Vector2(topX, currentY), Color.White);
                currentY += topTexture.Height;

                for (int lineIndex = 0; lineIndex < safeLineCount; lineIndex++)
                {
                    spriteBatch.Draw(centerTexture, new Vector2(centerX, currentY), Color.White);
                    currentY += centerTexture.Height;
                }

                spriteBatch.Draw(bottomTexture, new Vector2(bottomX, currentY), Color.White);
                spriteBatch.End();
            }
            finally
            {
                _graphicsDevice.SetRenderTargets(previousTargets);
                _graphicsDevice.Viewport = previousViewport;
            }

            return renderTarget;
        }

        private static int ResolveUiSampleLineCount(WzSubProperty itemProperty)
        {
            if (itemProperty?[ChalkboardSamplePropertyName] is not WzSubProperty sampleProperty)
            {
                return 3;
            }

            foreach (WzImageProperty child in sampleProperty.WzProperties)
            {
                if (child is WzSubProperty lineTemplate && lineTemplate.WzProperties.Count > 0)
                {
                    return Math.Clamp(lineTemplate.WzProperties.Count, 1, MaxBodyLineCount);
                }
            }

            return 3;
        }

        private bool TryLoadNamedVisual(WzSubProperty parent, string propertyName, string layoutKey, out MessageBoxVisual visual)
        {
            visual = null;
            if (parent == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            if (parent[propertyName] is WzImageProperty property)
            {
                WzImageProperty linked = property.GetLinkedWzImageProperty();
                if (linked is WzCanvasProperty canvas && TryLoadVisualFromCanvas(canvas, layoutKey, out visual))
                {
                    return true;
                }

                if (linked is WzSubProperty subProperty && TryLoadVisualFromProperty(subProperty, layoutKey, out visual))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryLoadVisualFromProperty(WzSubProperty property, string layoutKey, out MessageBoxVisual visual)
        {
            visual = null;
            if (property == null)
            {
                return false;
            }

            if (TryCollectFrames(property, layoutKey, out List<Texture2D> textures, out List<Point> origins, out List<int> delays, out MessageBoxTextLayout textLayout))
            {
                visual = new MessageBoxVisual(textures, origins, delays, null, textLayout);
                return true;
            }

            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child is not WzImageProperty nestedCandidate)
                {
                    continue;
                }

                WzImageProperty linked = nestedCandidate.GetLinkedWzImageProperty();
                if (linked is WzSubProperty nestedProperty &&
                    TryCollectFrames(nestedProperty, layoutKey, out List<Texture2D> nestedTextures, out List<Point> nestedOrigins, out List<int> nestedDelays, out MessageBoxTextLayout nestedLayout))
                {
                    visual = new MessageBoxVisual(nestedTextures, nestedOrigins, nestedDelays, null, nestedLayout);
                    return true;
                }
            }

            return false;
        }

        private bool TryLoadVisualFromCanvas(WzCanvasProperty canvas, string propertyName, out MessageBoxVisual visual)
        {
            visual = null;
            if (canvas == null)
            {
                return false;
            }

            Texture2D texture = LoadCanvasTexture(canvas);
            if (texture == null)
            {
                return false;
            }

            Point origin = ResolveCanvasOrigin(canvas, texture);
            int delay = ResolveCanvasDelay(canvas, null);
            MessageBoxTextLayout textLayout = ResolveTextLayout(propertyName, texture);
            visual = new MessageBoxVisual(new[] { texture }, new[] { origin }, new[] { delay }, null, textLayout);
            return true;
        }

        private bool TryCollectFrames(
            WzSubProperty property,
            string layoutKey,
            out List<Texture2D> textures,
            out List<Point> origins,
            out List<int> delays,
            out MessageBoxTextLayout textLayout)
        {
            textures = new List<Texture2D>();
            origins = new List<Point>();
            delays = new List<int>();
            textLayout = MessageBoxTextLayout.Default;

            List<(int Index, WzCanvasProperty Canvas)> orderedCanvases = new();
            int fallbackIndex = 0;
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child is not WzImageProperty imageProperty)
                {
                    continue;
                }

                WzImageProperty linked = imageProperty.GetLinkedWzImageProperty();
                if (linked is not WzCanvasProperty canvas)
                {
                    continue;
                }

                int frameIndex = int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedIndex)
                    ? parsedIndex
                    : fallbackIndex++;
                orderedCanvases.Add((frameIndex, canvas));
            }

            if (orderedCanvases.Count == 0)
            {
                return false;
            }

            foreach ((int _, WzCanvasProperty canvas) in orderedCanvases.OrderBy(frame => frame.Index))
            {
                Texture2D texture = LoadCanvasTexture(canvas);
                if (texture == null)
                {
                    continue;
                }

                Point origin = ResolveCanvasOrigin(canvas, texture);
                int delay = ResolveCanvasDelay(canvas, property);
                textures.Add(texture);
                origins.Add(origin);
                delays.Add(delay);
            }

            if (textures.Count == 0)
            {
                return false;
            }

            string resolvedLayoutKey = string.IsNullOrWhiteSpace(layoutKey) ? property.Name : layoutKey;
            if (orderedCanvases.Count == 1)
            {
                resolvedLayoutKey = orderedCanvases[0].Canvas.Name;
            }

            textLayout = ResolveTextLayout(resolvedLayoutKey, textures[0]);

            if (property.Name.Equals("info", StringComparison.OrdinalIgnoreCase) &&
                textures.Count > 1 &&
                orderedCanvases.Any(frame => frame.Canvas.Name.Equals(ChalkboardSamplePropertyName, StringComparison.OrdinalIgnoreCase)))
            {
                int sampleIndex = orderedCanvases
                    .Select((frame, index) => new { frame.Canvas.Name, index })
                    .First(candidate => candidate.Name.Equals(ChalkboardSamplePropertyName, StringComparison.OrdinalIgnoreCase))
                    .index;
                Texture2D sampleTexture = textures[Math.Clamp(sampleIndex, 0, textures.Count - 1)];
                Point sampleOrigin = origins[Math.Clamp(sampleIndex, 0, origins.Count - 1)];
                int sampleDelay = delays[Math.Clamp(sampleIndex, 0, delays.Count - 1)];
                textures = new List<Texture2D> { sampleTexture };
                origins = new List<Point> { sampleOrigin };
                delays = new List<int> { sampleDelay };
                textLayout = ResolveTextLayout(ClientMessageBoxPropertyName, sampleTexture);
            }

            return textures.Count > 0;
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            return canvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_graphicsDevice);
        }

        private static Point ResolveCanvasOrigin(WzCanvasProperty canvas, Texture2D texture)
        {
            if (canvas != null && canvas["origin"] != null)
            {
                var originPoint = canvas.GetCanvasOriginPosition();
                return new Point((int)Math.Round(originPoint.X), (int)Math.Round(originPoint.Y));
            }

            return texture == null
                ? Point.Zero
                : new Point(texture.Width / 2, texture.Height);
        }

        private static int ResolveCanvasDelay(WzCanvasProperty canvas, WzSubProperty parent)
        {
            int? delay =
                TryReadInt(canvas?["delay"]) ??
                TryReadInt(parent?["delay"]);
            return Math.Max(30, delay ?? DefaultFrameDelayMs);
        }

        private static bool IsKnownChalkboardItem(int itemId)
        {
            return itemId is >= 5370000 and <= 5370002;
        }

        private static MessageBoxTextLayout ResolveTextLayout(string propertyName, Texture2D texture)
        {
            if (TryResolveTextureBackedTextLayout(texture, out MessageBoxTextLayout textureBackedLayout))
            {
                return textureBackedLayout;
            }

            string name = propertyName ?? string.Empty;
            if (string.Equals(name, UiPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                int paddingLeft = Math.Clamp(texture?.Width / 12 ?? 6, 6, 12);
                int paddingRight = paddingLeft;
                int paddingTop = texture == null ? 6 : Math.Clamp(texture.Height / 8, 6, 12);
                int paddingBottom = Math.Clamp(texture?.Height / 8 ?? 6, 6, 12);
                return new MessageBoxTextLayout(
                    paddingLeft,
                    paddingTop,
                    paddingRight,
                    paddingBottom,
                    CenterHorizontally: true,
                    CenterVertically: true,
                    MaxLineCount: 3,
                    TextColor: Color.White);
            }

            if (name.IndexOf("sample", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("message", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("board", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("chalk", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int paddingLeft = texture?.Width >= 72 ? 8 : 6;
                int paddingRight = paddingLeft;
                int paddingTop = texture?.Height >= 56 ? 10 : 8;
                int paddingBottom = texture?.Height >= 56 ? 14 : 10;
                return new MessageBoxTextLayout(
                    paddingLeft,
                    paddingTop,
                    paddingRight,
                    paddingBottom,
                    CenterHorizontally: true,
                    CenterVertically: true,
                    MaxLineCount: MaxBodyLineCount,
                    TextColor: new Color(244, 246, 232));
            }

            return MessageBoxTextLayout.Default;
        }

        private static bool TryResolveTextureBackedTextLayout(Texture2D texture, out MessageBoxTextLayout layout)
        {
            layout = default;
            if (texture == null)
            {
                return false;
            }

            Color[] pixels = new Color[texture.Width * texture.Height];
            try
            {
                texture.GetData(pixels);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }

            if (!TryFindDominantOpaqueBounds(pixels, texture.Width, texture.Height, out Rectangle fillBounds, out Color fillColor))
            {
                return false;
            }

            int paddingLeft = Math.Clamp(fillBounds.Left, 4, texture.Width / 2);
            int paddingTop = Math.Clamp(fillBounds.Top, 4, texture.Height / 2);
            int paddingRight = Math.Clamp(texture.Width - fillBounds.Right - 1, 4, texture.Width / 2);
            int paddingBottom = Math.Clamp(texture.Height - fillBounds.Bottom - 1, 4, texture.Height / 2);
            Color textColor = ChooseContrastingTextColor(fillColor);
            layout = new MessageBoxTextLayout(
                paddingLeft,
                paddingTop,
                paddingRight,
                paddingBottom,
                CenterHorizontally: true,
                CenterVertically: true,
                MaxLineCount: MaxBodyLineCount,
                TextColor: textColor);
            return true;
        }

        private static bool TryFindDominantOpaqueBounds(Color[] pixels, int width, int height, out Rectangle bounds, out Color fillColor)
        {
            bounds = Rectangle.Empty;
            fillColor = Color.Transparent;
            if (pixels == null || pixels.Length == 0 || width <= 0 || height <= 0)
            {
                return false;
            }

            Dictionary<int, int> colorCounts = new();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                if (pixel.A < 220)
                {
                    continue;
                }

                int key = pixel.PackedValue.GetHashCode();
                colorCounts[key] = colorCounts.TryGetValue(key, out int count) ? count + 1 : 1;
            }

            if (colorCounts.Count == 0)
            {
                return false;
            }

            int dominantKey = colorCounts.OrderByDescending(pair => pair.Value).First().Key;
            fillColor = pixels.First(pixel => pixel.A >= 220 && pixel.PackedValue.GetHashCode() == dominantKey);

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = pixels[(y * width) + x];
                    if (pixel.A < 220 || pixel.PackedValue.GetHashCode() != dominantKey)
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return false;
            }

            bounds = new Rectangle(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
            return bounds.Width >= 24 && bounds.Height >= 20;
        }

        private static Color ChooseContrastingTextColor(Color background)
        {
            float luminance = (0.2126f * background.R) + (0.7152f * background.G) + (0.0722f * background.B);
            return luminance >= 140f
                ? new Color(32, 32, 32)
                : new Color(244, 246, 232);
        }

        private string ResolveItemName(int itemId)
        {
            if (_itemNameCache.TryGetValue(itemId, out string cachedName))
            {
                return cachedName;
            }

            string resolvedName = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName) && !string.IsNullOrWhiteSpace(itemName)
                ? itemName
                : $"Item #{itemId}";
            _itemNameCache[itemId] = resolvedName;
            return resolvedName;
        }

        private static int? TryReadInt(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzFloatProperty floatProperty => (int)Math.Round(floatProperty.Value),
                _ => null
            };
        }

        private static IEnumerable<string> WrapText(SpriteFont font, string text, float maxWidth)
        {
            if (font == null)
            {
                return Array.Empty<string>();
            }

            string normalized = string.IsNullOrWhiteSpace(text) ? "..." : text.Trim();
            List<string> lines = new();
            foreach (string sourceLine in normalized.Replace("\r", string.Empty).Split('\n'))
            {
                string line = sourceLine.Trim();
                if (line.Length == 0)
                {
                    lines.Add(string.Empty);
                    continue;
                }

                string[] words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string current = string.Empty;
                foreach (string word in words)
                {
                    string candidate = current.Length == 0 ? word : $"{current} {word}";
                    if (font.MeasureString(candidate).X <= maxWidth || current.Length == 0)
                    {
                        current = candidate;
                        continue;
                    }

                    lines.Add(current);
                    current = word;
                }

                if (current.Length > 0)
                {
                    lines.Add(current);
                }
            }

            return lines.Count == 0 ? new[] { normalized } : lines;
        }

        private interface IMessageBoxDrawableEntry
        {
            int Id { get; }
            string MessageText { get; }
            Point LayerPosition { get; }
            MessageBoxVisual Visual { get; }
            Texture2D GetDisplayTexture();
            Point GetDisplayOrigin();
            float GetAlpha(int currentTick);
            int GetVerticalFloatOffset(int currentTick);
        }

        private sealed class FieldMessageBoxEntry : IMessageBoxDrawableEntry
        {
            private int _frameIndex;
            private int _nextFrameTick;
            private readonly int _createdAt;

            public FieldMessageBoxEntry(
                int id,
                int itemId,
                string messageText,
                string characterName,
                Point hostPosition,
                Point layerPosition,
                MessageBoxVisual visual,
                string itemName,
                int currentTick)
            {
                Id = id;
                ItemId = itemId;
                MessageText = messageText ?? string.Empty;
                CharacterName = characterName ?? string.Empty;
                HostPosition = hostPosition;
                LayerPosition = layerPosition;
                Visual = visual;
                ItemName = itemName ?? string.Empty;
                _createdAt = currentTick;
                _nextFrameTick = currentTick + (visual?.GetFrameDelay(0) ?? DefaultFrameDelayMs);
            }

            public int Id { get; }
            public int ItemId { get; }
            public string MessageText { get; }
            public string CharacterName { get; }
            public Point HostPosition { get; }
            public Point LayerPosition { get; }
            public MessageBoxVisual Visual { get; }
            public string ItemName { get; }
            public int FrameIndex => _frameIndex;

            public void Update(int currentTick)
            {
                if (Visual == null || Visual.FrameCount <= 1 || currentTick < _nextFrameTick)
                {
                    return;
                }

                _frameIndex = (_frameIndex + 1) % Visual.FrameCount;
                _nextFrameTick = currentTick + Visual.GetFrameDelay(_frameIndex);
            }

            public float GetAlpha(int currentTick)
            {
                return 1f;
            }

            public int GetVerticalFloatOffset(int currentTick)
            {
                float phase = ((currentTick - _createdAt) % DefaultBobPeriodMs) / DefaultBobPeriodMs;
                return (int)Math.Round(Math.Sin(phase * MathHelper.TwoPi) * DefaultBobAmplitude);
            }

            public Texture2D GetDisplayTexture()
            {
                return Visual?.GetFrameTexture(_frameIndex);
            }

            public Point GetDisplayOrigin()
            {
                return Visual?.GetFrameOrigin(_frameIndex) ?? Point.Zero;
            }
        }

        private sealed class LeavingMessageBoxEntry : IMessageBoxDrawableEntry
        {
            private readonly int _leaveStartedAt;
            private readonly Texture2D _leaveTexture;
            private readonly Point _leaveOrigin;

            private LeavingMessageBoxEntry(int id, string messageText, Point layerPosition, MessageBoxVisual visual, Texture2D leaveTexture, Point leaveOrigin, int leaveStartedAt)
            {
                Id = id;
                MessageText = messageText;
                LayerPosition = layerPosition;
                Visual = visual;
                _leaveTexture = leaveTexture;
                _leaveOrigin = leaveOrigin;
                _leaveStartedAt = leaveStartedAt;
            }

            public int Id { get; }
            public string MessageText { get; }
            public Point LayerPosition { get; }
            public MessageBoxVisual Visual { get; }

            public static LeavingMessageBoxEntry FromEntry(FieldMessageBoxEntry entry, int currentTick)
            {
                return new LeavingMessageBoxEntry(
                    entry.Id,
                    entry.MessageText,
                    entry.LayerPosition,
                    entry.Visual,
                    entry.GetDisplayTexture(),
                    entry.GetDisplayOrigin(),
                    currentTick);
            }

            public bool ShouldRemove(int currentTick)
            {
                return currentTick - _leaveStartedAt >= DefaultLeaveFadeMs;
            }

            public Texture2D GetDisplayTexture()
            {
                return _leaveTexture;
            }

            public Point GetDisplayOrigin()
            {
                return _leaveOrigin;
            }

            public float GetAlpha(int currentTick)
            {
                float progress = Math.Clamp((currentTick - _leaveStartedAt) / (float)DefaultLeaveFadeMs, 0f, 1f);
                return 1f - progress;
            }

            public int GetVerticalFloatOffset(int currentTick)
            {
                return 0;
            }
        }

        private sealed record MessageBoxVisual(
            IReadOnlyList<Texture2D> Frames,
            IReadOnlyList<Point> Origins,
            IReadOnlyList<int> Delays,
            Texture2D IconTexture,
            MessageBoxTextLayout TextLayout)
        {
            public int FrameCount => Frames?.Count ?? 0;

            public Texture2D GetFrameTexture(int frameIndex)
            {
                if (Frames == null || Frames.Count == 0)
                {
                    return null;
                }

                int index = Math.Clamp(frameIndex, 0, Frames.Count - 1);
                return Frames[index];
            }

            public Point GetFrameOrigin(int frameIndex)
            {
                if (Origins == null || Origins.Count == 0)
                {
                    return Point.Zero;
                }

                int index = Math.Clamp(frameIndex, 0, Origins.Count - 1);
                return Origins[index];
            }

            public int GetFrameDelay(int frameIndex)
            {
                if (Delays == null || Delays.Count == 0)
                {
                    return DefaultFrameDelayMs;
                }

                int index = Math.Clamp(frameIndex, 0, Delays.Count - 1);
                return Math.Max(30, Delays[index]);
            }
        }

        private readonly record struct MessageBoxTextLayout(
            int PaddingLeft,
            int PaddingTop,
            int PaddingRight,
            int PaddingBottom,
            bool CenterHorizontally,
            bool CenterVertically,
            int MaxLineCount,
            Color TextColor)
        {
            public static MessageBoxTextLayout Default { get; } = new(
                PaddingLeft: 12,
                PaddingTop: 10,
                PaddingRight: 12,
                PaddingBottom: 10,
                CenterHorizontally: false,
                CenterVertically: false,
                MaxLineCount: MaxBodyLineCount,
                TextColor: Color.White);
        }
    }
}
