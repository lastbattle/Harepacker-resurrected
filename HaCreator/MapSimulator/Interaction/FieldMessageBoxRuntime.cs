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
        private const int ClientLeaveCanvasFadeDurationMs = 1000;
        private const int ClientLeaveCanvasReinsertDelayMs = 0;
        private const int ClientLeaveGetCanvasIndex = 0;
        private const int ClientLeaveRemoveCanvasIndex = -2;
        private const int ClientLeaveInsertCanvasStartAlphaValue = -1;
        private const int ClientLeaveInsertCanvasEndAlphaValue = 0;
        private const int ClientLayerRepeatAnimation = 1;
        private const int ClientLayerStopAnimation = 0;
        private const int ClientLayerVectorObjectStringPoolId = 0x3D2;
        private const int ClientLayerColorValue = -1073343424;
        private const int ClientLayerInitialAlpha = 255;
        private const int DefaultBoxOffsetX = -3;
        private const int DefaultBoxOffsetY = -100;
        private const float DefaultBobAmplitude = 3f;
        private const float ClientBobAngleStepRadians = MathHelper.Pi * 30f / 1000f * 0.5f;
        private const int ClientBobInitialPhaseModulo = 0x168;
        private const int MinBoardWidth = 92;
        private const int MaxBodyLineCount = 4;
        private const int CreateFailedStringPoolId = 0x1EA;
        private const int ClientMessageBoxPropertyStringPoolId = 0x660;
        private const string ClientMessageBoxPropertyName = "messageBox";
        private const string ChalkboardSamplePropertyName = "sample";
        private const string InfoPropertyName = "info";
        private const string TextPropertyName = "text";
        private const string MessagePropertyName = "message";
        private const string MessageRectPropertyName = "messageRect";
        private const string UiPropertyName = "ui";
        private const string UiTopPropertyName = "t";
        private const string UiCenterPropertyName = "c";
        private const string UiBottomPropertyName = "s";
        private const int DefaultUiCenterRepeatCount = 1;
        private const int ClientLeaveStartAlpha = 255;
        private const int ClientLeaveEndAlpha = 0;
        private static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> ExactChalkboardVisualFallbackPaths = new Dictionary<int, IReadOnlyList<string>>
        {
            [5077000] = new[] { ChalkboardSamplePropertyName, UiPropertyName },
            [5370000] = new[] { $"{InfoPropertyName}/{ChalkboardSamplePropertyName}" },
            [5370001] = new[] { $"{InfoPropertyName}/{ChalkboardSamplePropertyName}" },
            [5370002] = new[] { $"{InfoPropertyName}/{ChalkboardSamplePropertyName}" }
        };

        private readonly Dictionary<int, FieldMessageBoxEntry> _entries = new();
        private readonly List<LeavingMessageBoxEntry> _leavingEntries = new();
        private readonly Dictionary<int, MessageBoxVisual> _visualCache = new();
        private readonly Dictionary<int, string> _itemNameCache = new();
        private readonly Random _random = new();
        private GraphicsDevice _graphicsDevice;
        private Texture2D _pixelTexture;
        private SpriteBatch _snapshotSpriteBatch;
        private SpriteFont _snapshotFont;
        private int _nextLocalMessageBoxId = 1;
        private string _statusMessage = "Field message-box pool idle.";
        internal Action<string, int> SocialChatObserved { get; set; }

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
            _snapshotSpriteBatch = graphicsDevice == null ? null : new SpriteBatch(graphicsDevice);
        }

        internal void Clear()
        {
            _entries.Clear();
            _leavingEntries.Clear();
            _statusMessage = "Field message-box pool cleared.";
        }

        internal string DescribeStatus()
        {
            if (_entries.Count == 0 && _leavingEntries.Count == 0)
            {
                return _statusMessage;
            }

            int localActiveCount = _entries.Values.Count(entry => entry.Source == MessageBoxEntrySource.LocalCommand);
            int packetActiveCount = _entries.Count - localActiveCount;
            int leavingCount = _leavingEntries.Count;
            string activeSummary = $"{_entries.Count} active ({packetActiveCount} packet, {localActiveCount} local)";
            return leavingCount > 0
                ? $"{activeSummary}, {leavingCount} leaving. {_statusMessage}"
                : $"{activeSummary}. {_statusMessage}";
        }

        internal string CreateLocalMessageBox(
            int itemId,
            string messageText,
            string characterName,
            Point hostPosition,
            int currentTick,
            int? messageBoxId = null,
            MessageBoxEntrySource source = MessageBoxEntrySource.LocalCommand)
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
                ComputeInitialBobAngleRadians(),
                currentTick,
                source);

            _entries[resolvedId] = entry;
            _statusMessage = $"Registered {source.GetLabel()} field message-box {resolvedId} for {trimmedName} using item {resolvedItemId}.";
            NotifySocialChatObserved(trimmedMessage, currentTick);
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
                entry.ClientLayer.RemoveFromPool(immediate: true);
                _statusMessage = $"Removed field message-box {messageBoxId} immediately.";
                return _statusMessage;
            }

            FrozenMessageBoxRenderState leaveRenderState = CaptureLeaveRenderState(entry);
            _leavingEntries.Add(LeavingMessageBoxEntry.FromEntry(entry, leaveRenderState, currentTick));
            _statusMessage = $"{entry.Source.GetLabel()} field message-box {messageBoxId} began its client leave-field fade.";
            return _statusMessage;
        }

        internal string ApplyCreateFailed()
        {
            _statusMessage = $"{ResolveCreateFailedNoticeText()} [client notice StringPool 0x{CreateFailedStringPoolId:X}]";
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

            foreach (LeavingMessageBoxEntry leavingEntry in _leavingEntries)
            {
                leavingEntry.Update(currentTick);
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

            _snapshotFont = font;

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

                if (_entries.ContainsKey(messageBoxId))
                {
                    message = $"Ignored duplicate packet-owned message-box enter-field packet for id {messageBoxId}.";
                    return true;
                }

                CreateLocalMessageBox(
                    itemId,
                    text,
                    characterName,
                    new Point(hostX, hostY),
                    currentTick,
                    messageBoxId,
                    MessageBoxEntrySource.PacketEnterField);

                message = $"Applied packet-owned message-box enter-field packet for {characterName} ({messageBoxId}).";
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

        private void NotifySocialChatObserved(string message, int tickCount)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            SocialChatObserved?.Invoke(message.Trim(), tickCount);
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

            if (entry.ShouldDrawText)
            {
                DrawBoardText(spriteBatch, font, boardBounds, entry, alpha);
            }
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
            MessageBoxTextLayout layout = entry.GetDisplayTextLayout();
            Rectangle textBounds = layout.GetContentBounds(boardBounds);
            int textRegionWidth = Math.Max(MinBoardWidth, textBounds.Width);
            string[] bodyLines = WrapText(font, entry.MessageText, textRegionWidth).Take(layout.MaxLineCount).ToArray();
            if (bodyLines.Length == 0)
            {
                bodyLines = new[] { "..." };
            }

            int totalHeight = bodyLines.Length * font.LineSpacing;
            float startY = textBounds.Y;
            int availableHeight = Math.Max(font.LineSpacing, textBounds.Height);
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
                    ? textBounds.X + (textBounds.Width - size.X) * 0.5f
                    : textBounds.X;
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

            if (!TryLoadItemProperty(category, imagePath, itemId, out WzSubProperty itemProperty))
            {
                return null;
            }

            WzSubProperty infoProperty = itemProperty[InfoPropertyName] as WzSubProperty;
            WzImageProperty resolvedItemProperty = TryLoadResolvedVisualProperty(itemId, out WzImageProperty sharedProperty)
                ? sharedProperty
                : itemProperty;
            WzSubProperty resolvedInfoProperty = (resolvedItemProperty as WzSubProperty)?[InfoPropertyName] as WzSubProperty;
            Texture2D iconTexture = LoadCanvasTexture(infoProperty?["iconRaw"] as WzCanvasProperty)
                                    ?? LoadCanvasTexture(infoProperty?["icon"] as WzCanvasProperty);
            iconTexture ??= LoadCanvasTexture(resolvedInfoProperty?["iconRaw"] as WzCanvasProperty)
                            ?? LoadCanvasTexture(resolvedInfoProperty?["icon"] as WzCanvasProperty);

            bool usingResolvedSharedProperty = !ReferenceEquals(resolvedItemProperty, itemProperty);
            if (TryLoadClientMessageBoxVisual(
                    resolvedItemProperty,
                    null,
                    itemId,
                    iconTexture,
                    allowChalkboardSampleFallback: !usingResolvedSharedProperty,
                    out MessageBoxVisual visual))
            {
                return visual;
            }

            if (!ReferenceEquals(resolvedItemProperty, itemProperty) &&
                TryLoadClientMessageBoxVisual(
                    itemProperty,
                    null,
                    itemId,
                    iconTexture,
                    allowChalkboardSampleFallback: true,
                    out visual))
            {
                return visual;
            }

            return iconTexture == null
                ? null
                : new MessageBoxVisual(
                    Array.Empty<Texture2D>(),
                    Array.Empty<Point>(),
                    Array.Empty<int>(),
                    Array.Empty<MessageBoxTextLayout>(),
                    iconTexture,
                    MessageBoxTextLayout.Default);
        }

        private bool TryLoadClientMessageBoxVisual(
            WzImageProperty itemProperty,
            string resolvedPath,
            int itemId,
            Texture2D iconTexture,
            bool allowChalkboardSampleFallback,
            out MessageBoxVisual visual)
        {
            visual = null;

            if (TryResolveClientMessageBoxProperty(itemProperty, out WzImageProperty clientMessageBoxProperty) &&
                TryLoadVisualFromImageProperty(clientMessageBoxProperty, ClientMessageBoxPropertyName, out visual))
            {
                visual = visual with { IconTexture = iconTexture ?? visual.IconTexture };
                return true;
            }

            if (!allowChalkboardSampleFallback)
            {
                return false;
            }

            foreach (string candidatePath in GetFallbackVisualPropertyPaths(itemId))
            {
                if (!TryLoadVisualAtPath(itemProperty as WzSubProperty, candidatePath, out visual))
                {
                    continue;
                }

                visual = visual with { IconTexture = iconTexture ?? visual.IconTexture };
                return true;
            }

            return false;
        }

        internal static IReadOnlyList<string> GetPreferredVisualPropertyPaths(int itemId)
        {
            return Array.Empty<string>();
        }

        internal static IReadOnlyList<string> GetFallbackVisualPropertyPathsForTest(int itemId)
        {
            return GetFallbackVisualPropertyPaths(itemId);
        }

        private static IReadOnlyList<string> GetFallbackVisualPropertyPaths(int itemId)
        {
            return ExactChalkboardVisualFallbackPaths.TryGetValue(itemId, out IReadOnlyList<string> candidatePaths)
                ? candidatePaths
                : Array.Empty<string>();
        }

        internal static string ResolveCreateFailedNoticeText()
        {
            return MessageBoxOwnerStringPoolText.GetCreateFailedNotice();
        }

        private static bool IsKnownChalkboardItem(int itemId)
        {
            return itemId / 10000 == 537;
        }

        private bool TryLoadItemProperty(string category, string imagePath, int itemId, out WzSubProperty itemProperty)
        {
            itemProperty = null;

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return false;
            }

            itemImage.ParseImage();
            foreach (string itemNodeName in EnumerateItemNodeNames(itemId))
            {
                if (itemImage[itemNodeName] is WzSubProperty candidate)
                {
                    itemProperty = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryLoadResolvedVisualProperty(int itemId, out WzImageProperty itemProperty)
        {
            itemProperty = null;
            if (!InventoryItemMetadataResolver.TryResolveItemInfoPath(itemId, out string path) ||
                string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return TryLoadPropertyByWzPath(path.Trim(), out itemProperty);
        }

        private bool TryLoadPropertyByWzPath(string path, out WzImageProperty property)
        {
            property = null;
            if (!TryParseWzPropertyPath(path, out string category, out string imagePath, out string propertyPath))
            {
                return false;
            }

            WzImage image = global::HaCreator.Program.FindImage(category, imagePath);
            if (image == null)
            {
                return false;
            }

            image.ParseImage();
            object current = image;
            foreach (string segment in propertyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = GetChildProperty(current, segment);
                if (current == null)
                {
                    return false;
                }
            }

            property = current as WzImageProperty;
            return property != null;
        }

        private static bool TryResolveClientMessageBoxProperty(WzImageProperty itemProperty, out WzImageProperty messageBoxProperty)
        {
            messageBoxProperty = null;
            if (itemProperty == null)
            {
                return false;
            }

            if (string.Equals(itemProperty.Name, ClientMessageBoxPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                messageBoxProperty = itemProperty.GetLinkedWzImageProperty();
                return messageBoxProperty != null;
            }

            if (itemProperty is not WzSubProperty subProperty)
            {
                return false;
            }

            if (subProperty[ClientMessageBoxPropertyName] is not WzImageProperty directProperty)
            {
                return false;
            }

            messageBoxProperty = directProperty.GetLinkedWzImageProperty();
            return messageBoxProperty != null;
        }

        private static bool TryParseWzPropertyPath(string path, out string category, out string imagePath, out string propertyPath)
        {
            category = null;
            imagePath = null;
            propertyPath = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/').Trim().Trim('/');
            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                return false;
            }

            int imageIndex = Array.FindIndex(segments, segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
            if (imageIndex < 1 || imageIndex >= segments.Length - 1)
            {
                return false;
            }

            category = segments[0];
            imagePath = string.Join("/", segments.Skip(1).Take(imageIndex));
            propertyPath = string.Join("/", segments.Skip(imageIndex + 1));
            return !string.IsNullOrWhiteSpace(category)
                   && !string.IsNullOrWhiteSpace(imagePath)
                   && !string.IsNullOrWhiteSpace(propertyPath);
        }

        private static WzImageProperty GetChildProperty(object parent, string propertyName)
        {
            return parent switch
            {
                WzImage image => image[propertyName] as WzImageProperty,
                WzSubProperty subProperty => subProperty[propertyName] as WzImageProperty,
                WzCanvasProperty canvasProperty => canvasProperty[propertyName] as WzImageProperty,
                _ => null
            };
        }

        private static IEnumerable<string> EnumerateItemNodeNames(int itemId)
        {
            string nodeName8 = itemId.ToString("D8", CultureInfo.InvariantCulture);
            yield return nodeName8;

            string nodeName7 = itemId.ToString("D7", CultureInfo.InvariantCulture);
            if (!string.Equals(nodeName8, nodeName7, StringComparison.Ordinal))
            {
                yield return nodeName7;
            }
        }

        private bool TryLoadVisualAtPath(WzSubProperty rootProperty, string propertyPath, out MessageBoxVisual visual)
        {
            visual = null;
            if (rootProperty == null || string.IsNullOrWhiteSpace(propertyPath))
            {
                return false;
            }

            string[] pathSegments = propertyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length == 0)
            {
                return false;
            }

            WzImageProperty current = rootProperty;
            foreach (string segment in pathSegments)
            {
                if (current is not WzSubProperty currentSubProperty || currentSubProperty[segment] is not WzImageProperty childProperty)
                {
                    return false;
                }

                current = childProperty;
            }

            return TryLoadVisualFromImageProperty(current, pathSegments[^1], out visual);
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
                return TryLoadVisualFromImageProperty(property, layoutKey, out visual);
            }

            return false;
        }

        private bool TryLoadVisualFromImageProperty(WzImageProperty property, string layoutKey, out MessageBoxVisual visual)
        {
            visual = null;
            if (property == null)
            {
                return false;
            }

            WzImageProperty linked = property.GetLinkedWzImageProperty();
            if (linked is WzCanvasProperty canvas && TryLoadVisualFromCanvas(canvas, layoutKey, property, out visual))
            {
                return true;
            }

            if (linked is WzSubProperty subProperty && TryLoadVisualFromProperty(subProperty, layoutKey, out visual))
            {
                return true;
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

            if (TryCollectFrames(
                    property,
                    layoutKey,
                    metadataFallbackProperty: null,
                    out List<Texture2D> textures,
                    out List<Point> origins,
                    out List<int> delays,
                    out List<MessageBoxTextLayout> textLayouts,
                    out MessageBoxTextLayout textLayout))
            {
                visual = new MessageBoxVisual(textures, origins, delays, textLayouts, null, textLayout);
                return true;
            }

            if (TryLoadUiBoardVisual(property, out visual))
            {
                return true;
            }

            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child is not WzImageProperty nestedCandidate)
                {
                    continue;
                }

                WzImageProperty linked = nestedCandidate.GetLinkedWzImageProperty();
                if (linked is not WzSubProperty nestedProperty)
                {
                    continue;
                }

                if (TryCollectFrames(
                        nestedProperty,
                        layoutKey,
                        metadataFallbackProperty: property,
                        out List<Texture2D> nestedTextures,
                        out List<Point> nestedOrigins,
                        out List<int> nestedDelays,
                        out List<MessageBoxTextLayout> nestedTextLayouts,
                        out MessageBoxTextLayout nestedLayout))
                {
                    visual = new MessageBoxVisual(nestedTextures, nestedOrigins, nestedDelays, nestedTextLayouts, null, nestedLayout);
                    return true;
                }

                if (string.Equals(nestedCandidate.Name, UiPropertyName, StringComparison.OrdinalIgnoreCase) &&
                    TryLoadUiBoardVisual(nestedProperty, out visual))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryLoadVisualFromCanvas(WzCanvasProperty canvas, string propertyName, WzImageProperty sourceProperty, out MessageBoxVisual visual)
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
            MessageBoxTextLayout textLayout = ResolveTextLayout(propertyName, texture, sourceProperty);
            visual = new MessageBoxVisual(new[] { texture }, new[] { origin }, new[] { delay }, new[] { textLayout }, null, textLayout);
            return true;
        }

        private bool TryLoadUiBoardVisual(WzSubProperty uiProperty, out MessageBoxVisual visual)
        {
            visual = null;
            if (uiProperty == null)
            {
                return false;
            }

            Texture2D topTexture = LoadCanvasTexture(uiProperty[UiTopPropertyName] as WzCanvasProperty);
            Texture2D centerTexture = LoadCanvasTexture(uiProperty[UiCenterPropertyName] as WzCanvasProperty);
            Texture2D bottomTexture = LoadCanvasTexture(uiProperty[UiBottomPropertyName] as WzCanvasProperty);
            if (topTexture == null || centerTexture == null || bottomTexture == null || _graphicsDevice == null)
            {
                return false;
            }

            int centerRepeatCount = ResolveUiCenterRepeatCount(uiProperty);
            int width = Math.Max(topTexture.Width, Math.Max(centerTexture.Width, bottomTexture.Width));
            int height = topTexture.Height + (centerTexture.Height * centerRepeatCount) + bottomTexture.Height;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            RenderTargetBinding[] previousTargets = _graphicsDevice.GetRenderTargets();
            Viewport previousViewport = _graphicsDevice.Viewport;
            RenderTarget2D boardTexture = new(
                _graphicsDevice,
                width,
                height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None);

            try
            {
                _graphicsDevice.SetRenderTarget(boardTexture);
                _graphicsDevice.Clear(Color.Transparent);
                _snapshotSpriteBatch?.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

                int centerX = (width - topTexture.Width) / 2;
                _snapshotSpriteBatch?.Draw(topTexture, new Vector2(centerX, 0f), Color.White);

                centerX = (width - centerTexture.Width) / 2;
                for (int i = 0; i < centerRepeatCount; i++)
                {
                    _snapshotSpriteBatch?.Draw(centerTexture, new Vector2(centerX, topTexture.Height + (i * centerTexture.Height)), Color.White);
                }

                centerX = (width - bottomTexture.Width) / 2;
                _snapshotSpriteBatch?.Draw(bottomTexture, new Vector2(centerX, topTexture.Height + (centerTexture.Height * centerRepeatCount)), Color.White);
                _snapshotSpriteBatch?.End();
            }
            finally
            {
                if (previousTargets.Length > 0)
                {
                    _graphicsDevice.SetRenderTargets(previousTargets);
                }
                else
                {
                    _graphicsDevice.SetRenderTarget(null);
                }

                _graphicsDevice.Viewport = previousViewport;
            }

            MessageBoxTextLayout textLayout = BuildUiBoardTextLayout(width, height, centerTexture, topTexture.Height, bottomTexture.Height);
            visual = new MessageBoxVisual(
                new[] { (Texture2D)boardTexture },
                new[] { new Point(width / 2, height) },
                new[] { DefaultFrameDelayMs },
                new[] { textLayout },
                null,
                textLayout);
            return true;
        }

        private static int ResolveUiCenterRepeatCount(WzSubProperty uiProperty)
        {
            if (uiProperty?.Parent is not WzSubProperty itemProperty ||
                itemProperty[ChalkboardSamplePropertyName] is not WzSubProperty sampleProperty)
            {
                return DefaultUiCenterRepeatCount;
            }

            int maxSampleLineCount = 0;
            foreach (WzImageProperty sampleVariant in sampleProperty.WzProperties)
            {
                if (sampleVariant is not WzSubProperty variantProperty)
                {
                    continue;
                }

                int lineCount = variantProperty.WzProperties.Count(line => TryReadString(line) != null);
                maxSampleLineCount = Math.Max(maxSampleLineCount, lineCount);
            }

            return Math.Clamp(maxSampleLineCount, DefaultUiCenterRepeatCount, MaxBodyLineCount);
        }

        private bool TryCollectFrames(
            WzSubProperty property,
            string layoutKey,
            WzImageProperty metadataFallbackProperty,
            out List<Texture2D> textures,
            out List<Point> origins,
            out List<int> delays,
            out List<MessageBoxTextLayout> textLayouts,
            out MessageBoxTextLayout textLayout)
        {
            textures = new List<Texture2D>();
            origins = new List<Point>();
            delays = new List<int>();
            textLayouts = new List<MessageBoxTextLayout>();
            textLayout = MessageBoxTextLayout.Default;

            List<(int Index, string PropertyName, WzCanvasProperty Canvas)> orderedCanvases = new();
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
                orderedCanvases.Add((frameIndex, child.Name, canvas));
            }

            if (orderedCanvases.Count == 0)
            {
                return false;
            }

            foreach ((int _, string propertyName, WzCanvasProperty canvas) in orderedCanvases.OrderBy(frame => frame.Index))
            {
                Texture2D texture = LoadCanvasTexture(canvas);
                if (texture == null)
                {
                    continue;
                }

                Point origin = ResolveCanvasOrigin(canvas, texture);
                int delay = ResolveCanvasDelay(canvas, property);
                string frameLayoutKey = string.IsNullOrWhiteSpace(layoutKey)
                    ? propertyName
                    : $"{layoutKey}/{propertyName}";
                MessageBoxTextLayout frameTextLayout = ResolveTextLayout(frameLayoutKey, texture, canvas, property, metadataFallbackProperty);
                textures.Add(texture);
                origins.Add(origin);
                delays.Add(delay);
                textLayouts.Add(frameTextLayout);
            }

            if (textures.Count == 0)
            {
                return false;
            }

            textLayout = textLayouts.Count > 0
                ? textLayouts[0]
                : ResolveTextLayout(string.IsNullOrWhiteSpace(layoutKey) ? property.Name : layoutKey, textures[0], property, metadataFallbackProperty);

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

        private static MessageBoxTextLayout ResolveTextLayout(string propertyName, Texture2D texture, params WzImageProperty[] sourceProperties)
        {
            if (sourceProperties != null)
            {
                foreach (WzImageProperty sourceProperty in sourceProperties)
                {
                    if (TryResolveMetadataBackedTextLayout(sourceProperty, texture, out MessageBoxTextLayout metadataLayout))
                    {
                        return metadataLayout;
                    }
                }
            }

            if (TryResolveTextureBackedTextLayout(texture, out MessageBoxTextLayout textureBackedLayout))
            {
                return textureBackedLayout;
            }

            string name = propertyName ?? string.Empty;
            int lastPathSeparator = name.LastIndexOf('/');
            if (lastPathSeparator >= 0 && lastPathSeparator + 1 < name.Length)
            {
                name = name[(lastPathSeparator + 1)..];
            }

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
                int paddingLeft = texture?.Width >= 72 ? 6 : 5;
                int paddingRight = paddingLeft;
                int paddingTop = texture?.Height >= 64 ? 8 : 6;
                int paddingBottom = texture?.Height >= 64 ? 10 : 8;
                return new MessageBoxTextLayout(
                    paddingLeft,
                    paddingTop,
                    paddingRight,
                    paddingBottom,
                    CenterHorizontally: true,
                    CenterVertically: true,
                    MaxLineCount: texture?.Height >= 68 ? MaxBodyLineCount : 3,
                    TextColor: new Color(244, 246, 232));
            }

            return MessageBoxTextLayout.Default;
        }

        private static bool TryResolveMetadataBackedTextLayout(WzImageProperty sourceProperty, Texture2D texture, out MessageBoxTextLayout layout)
        {
            layout = default;
            if (texture == null || sourceProperty is not WzSubProperty subProperty)
            {
                return false;
            }

            foreach (string candidateName in new[] { TextPropertyName, MessagePropertyName, MessageRectPropertyName })
            {
                if (TryBuildMetadataBackedTextLayout(subProperty[candidateName], texture, out layout))
                {
                    return true;
                }
            }

            return TryBuildMetadataBackedTextLayout(subProperty, texture, out layout);
        }

        private static bool TryBuildMetadataBackedTextLayout(WzImageProperty property, Texture2D texture, out MessageBoxTextLayout layout)
        {
            layout = default;
            if (texture == null || property is not WzSubProperty metadataProperty || !TryResolveLayoutRect(metadataProperty, out Rectangle textRect))
            {
                return false;
            }

            layout = BuildRectBackedTextLayout(texture, textRect, metadataProperty);
            return true;
        }

        private static bool TryResolveLayoutRect(WzImageProperty property, out Rectangle rect)
        {
            rect = Rectangle.Empty;
            if (property == null)
            {
                return false;
            }

            if (property is WzSubProperty subProperty)
            {
                if (TryReadVector(subProperty, "lt", out Point lt) &&
                    TryReadVector(subProperty, "rb", out Point rb))
                {
                    rect = new Rectangle(lt.X, lt.Y, rb.X - lt.X, rb.Y - lt.Y);
                    return rect.Width > 0 && rect.Height > 0;
                }

                int? x = TryReadInt(subProperty["x"]);
                int? y = TryReadInt(subProperty["y"]);
                int? width = TryReadInt(subProperty["width"]) ?? TryReadInt(subProperty["w"]);
                int? height = TryReadInt(subProperty["height"]) ?? TryReadInt(subProperty["h"]);
                if (x.HasValue && y.HasValue && width.HasValue && height.HasValue &&
                    width.Value > 0 &&
                    height.Value > 0)
                {
                    rect = new Rectangle(x.Value, y.Value, width.Value, height.Value);
                    return true;
                }
            }

            return false;
        }

        private static MessageBoxTextLayout BuildRectBackedTextLayout(Texture2D texture, Rectangle textRect, WzSubProperty metadataProperty)
        {
            int paddingLeft = Math.Clamp(textRect.Left, 0, texture.Width);
            int paddingTop = Math.Clamp(textRect.Top, 0, texture.Height);
            int paddingRight = Math.Clamp(texture.Width - textRect.Right, 0, texture.Width);
            int paddingBottom = Math.Clamp(texture.Height - textRect.Bottom, 0, texture.Height);
            int lineHeight = Math.Max(1, ResolveMetadataLineHeight(metadataProperty) ?? 14);
            int derivedLineCount = textRect.Height <= 0
                ? 1
                : Math.Max(1, Math.Min(MaxBodyLineCount, textRect.Height / lineHeight));
            int lineCount = Math.Clamp(ResolveMetadataMaxLineCount(metadataProperty) ?? derivedLineCount, 1, MaxBodyLineCount);
            return new MessageBoxTextLayout(
                paddingLeft,
                paddingTop,
                paddingRight,
                paddingBottom,
                CenterHorizontally: ResolveMetadataCenterHorizontally(metadataProperty),
                CenterVertically: ResolveMetadataCenterVertically(metadataProperty),
                MaxLineCount: lineCount,
                TextColor: new Color(244, 246, 232));
        }

        private static bool ResolveMetadataCenterHorizontally(WzSubProperty metadataProperty)
        {
            string alignment = ResolveMetadataAlignment(metadataProperty);
            if (!string.IsNullOrWhiteSpace(alignment))
            {
                if (alignment.Contains("center", StringComparison.OrdinalIgnoreCase) ||
                    alignment.Contains("middle", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (alignment.Contains("left", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return TryReadBool(metadataProperty?["centerX"])
                ?? TryReadBool(metadataProperty?["centerHorizontal"])
                ?? TryReadBool(metadataProperty?["center"])
                ?? TryReadBool(metadataProperty?["centerText"])
                ?? false;
        }

        private static bool ResolveMetadataCenterVertically(WzSubProperty metadataProperty)
        {
            string alignment = ResolveMetadataVerticalAlignment(metadataProperty);
            if (!string.IsNullOrWhiteSpace(alignment))
            {
                if (alignment.Contains("center", StringComparison.OrdinalIgnoreCase) ||
                    alignment.Contains("middle", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (alignment.Contains("top", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return TryReadBool(metadataProperty?["centerY"])
                ?? TryReadBool(metadataProperty?["centerVertical"])
                ?? TryReadBool(metadataProperty?["center"])
                ?? TryReadBool(metadataProperty?["centerText"])
                ?? false;
        }

        private static string ResolveMetadataAlignment(WzSubProperty metadataProperty)
        {
            return TryReadString(metadataProperty?["textAlign"])
                ?? TryReadString(metadataProperty?["align"])
                ?? TryReadString(metadataProperty?["horizontalAlign"])
                ?? TryReadString(metadataProperty?["hAlign"]);
        }

        private static string ResolveMetadataVerticalAlignment(WzSubProperty metadataProperty)
        {
            return TryReadString(metadataProperty?["verticalAlign"])
                ?? TryReadString(metadataProperty?["vAlign"])
                ?? ResolveMetadataAlignment(metadataProperty);
        }

        private static int? ResolveMetadataLineHeight(WzSubProperty metadataProperty)
        {
            return TryReadInt(metadataProperty?["lineHeight"])
                ?? TryReadInt(metadataProperty?["lineSpace"])
                ?? TryReadInt(metadataProperty?["fontHeight"]);
        }

        private static int? ResolveMetadataMaxLineCount(WzSubProperty metadataProperty)
        {
            return TryReadInt(metadataProperty?["maxLine"])
                ?? TryReadInt(metadataProperty?["maxLineCount"])
                ?? TryReadInt(metadataProperty?["lineCount"])
                ?? TryReadInt(metadataProperty?["line"]);
        }

        private static bool TryReadVector(WzSubProperty property, string name, out Point point)
        {
            point = Point.Zero;
            if (property?[name] is not WzVectorProperty vectorProperty)
            {
                return false;
            }

            point = new Point(vectorProperty.X?.Value ?? 0, vectorProperty.Y?.Value ?? 0);
            return true;
        }

        private static MessageBoxTextLayout BuildUiBoardTextLayout(int boardWidth, int boardHeight, Texture2D centerTexture, int topHeight, int bottomHeight)
        {
            int horizontalInset = Math.Clamp((boardWidth - (centerTexture?.Width ?? 0)) / 2 + 6, 6, Math.Max(6, boardWidth / 4));
            int paddingTop = Math.Max(6, topHeight + 4);
            int paddingBottom = Math.Max(6, bottomHeight + 4);
            int maxLineCount = 3;
            Color textColor = Color.White;

            if (centerTexture != null && TryResolveTextureBackedTextLayout(centerTexture, out MessageBoxTextLayout centerLayout))
            {
                horizontalInset = Math.Max(horizontalInset, centerLayout.PaddingLeft);
                paddingTop = Math.Max(paddingTop, topHeight + centerLayout.PaddingTop);
                paddingBottom = Math.Max(paddingBottom, bottomHeight + centerLayout.PaddingBottom);
                maxLineCount = Math.Max(3, centerLayout.MaxLineCount);
                textColor = centerLayout.TextColor;
            }
            else if (centerTexture != null)
            {
                int verticalInset = centerTexture.Height >= 36 ? 8 : 6;
                paddingTop = Math.Max(paddingTop, topHeight + verticalInset);
                paddingBottom = Math.Max(paddingBottom, bottomHeight + verticalInset);
            }

            int availableHeight = Math.Max(1, boardHeight - paddingTop - paddingBottom);
            if (centerTexture != null && centerTexture.Height > 0)
            {
                maxLineCount = Math.Max(1, Math.Min(maxLineCount, Math.Max(1, availableHeight / Math.Max(1, centerTexture.Height / 3))));
            }

            return new MessageBoxTextLayout(
                PaddingLeft: horizontalInset,
                PaddingTop: paddingTop,
                PaddingRight: horizontalInset,
                PaddingBottom: paddingBottom,
                CenterHorizontally: true,
                CenterVertically: true,
                MaxLineCount: maxLineCount,
                TextColor: textColor);
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
                MaxLineCount: texture.Height <= 72 ? 3 : MaxBodyLineCount,
                TextColor: textColor);
            return true;
        }

        internal static IReadOnlyList<string> GetPreferredVisualPropertyPathsForTest(int itemId)
        {
            return GetPreferredVisualPropertyPaths(itemId);
        }

        internal static bool TryParseWzPropertyPathForTest(string path, out string category, out string imagePath, out string propertyPath)
        {
            return TryParseWzPropertyPath(path, out category, out imagePath, out propertyPath);
        }

        internal static float ComputeLeaveFadeAlphaForTest(int elapsedMs)
        {
            return ComputeLeaveFadeAlpha(elapsedMs);
        }

        internal static float GetClientBobAngleStepRadiansForTest()
        {
            return ClientBobAngleStepRadians;
        }

        internal static int ComputeClientBobOffsetForTest(float angleRadians)
        {
            return ComputeClientBobOffset(angleRadians);
        }

        internal static Rectangle ComputeTextBoundsForTest(
            int boardWidth,
            int boardHeight,
            int paddingLeft,
            int paddingTop,
            int paddingRight,
            int paddingBottom)
        {
            MessageBoxTextLayout layout = new(
                paddingLeft,
                paddingTop,
                paddingRight,
                paddingBottom,
                CenterHorizontally: true,
                CenterVertically: true,
                MaxLineCount: MaxBodyLineCount,
                TextColor: Color.White);
            return layout.GetContentBounds(new Rectangle(0, 0, boardWidth, boardHeight));
        }

        internal static (int PaddingLeft, int PaddingTop, int PaddingRight, int PaddingBottom, int MaxLineCount) ComputeMetadataBackedTextLayoutWithParentFallbackForTest(
            int textureWidth,
            int textureHeight,
            int left,
            int top,
            int right,
            int bottom)
        {
            using Texture2D texture = new Texture2D(GraphicsDeviceServiceForTests.Instance, textureWidth, textureHeight);

            WzSubProperty childWithoutLayoutMetadata = new("0");
            WzSubProperty messageBoxProperty = new(ClientMessageBoxPropertyName);
            WzSubProperty textProperty = new(TextPropertyName);
            textProperty.AddProperty(new WzVectorProperty("lt", left, top));
            textProperty.AddProperty(new WzVectorProperty("rb", right, bottom));
            messageBoxProperty.AddProperty(textProperty);

            MessageBoxTextLayout layout = ResolveTextLayout("0", texture, childWithoutLayoutMetadata, messageBoxProperty);
            return (layout.PaddingLeft, layout.PaddingTop, layout.PaddingRight, layout.PaddingBottom, layout.MaxLineCount);
        }

        internal static (int PaddingLeft, int PaddingTop, int PaddingRight, int PaddingBottom, bool CenterHorizontally, bool CenterVertically, int MaxLineCount) ComputeMetadataBackedTextLayoutForTest(
            int textureWidth,
            int textureHeight,
            int left,
            int top,
            int right,
            int bottom,
            bool? centerX = null,
            bool? centerY = null,
            int? maxLineCount = null)
        {
            using Texture2D texture = new Texture2D(GraphicsDeviceServiceForTests.Instance, textureWidth, textureHeight);
            WzSubProperty textProperty = new(TextPropertyName);
            textProperty.AddProperty(new WzVectorProperty("lt", left, top));
            textProperty.AddProperty(new WzVectorProperty("rb", right, bottom));
            if (centerX.HasValue)
            {
                textProperty.AddProperty(new WzIntProperty("centerX", centerX.Value ? 1 : 0));
            }

            if (centerY.HasValue)
            {
                textProperty.AddProperty(new WzIntProperty("centerY", centerY.Value ? 1 : 0));
            }

            if (maxLineCount.HasValue)
            {
                textProperty.AddProperty(new WzIntProperty("maxLine", maxLineCount.Value));
            }

            WzSubProperty messageBoxProperty = new(ClientMessageBoxPropertyName);
            messageBoxProperty.AddProperty(textProperty);

            MessageBoxTextLayout layout = ResolveTextLayout(ClientMessageBoxPropertyName, texture, messageBoxProperty);
            return (layout.PaddingLeft, layout.PaddingTop, layout.PaddingRight, layout.PaddingBottom, layout.CenterHorizontally, layout.CenterVertically, layout.MaxLineCount);
        }

        internal static (float AlphaBeforeUpdate, float AlphaAfterUpdateSameTick, float AlphaAtHalfFade, bool ShouldRemoveAtFadeDuration, bool DrawsTextDuringReinsertedFade) ComputeLeaveFieldSequenceForTest()
        {
            LeavingMessageBoxEntry entry = new(
                id: 1,
                messageText: "test",
                layerPosition: Point.Zero,
                visual: null,
                leaveRenderState: new FrozenMessageBoxRenderState(
                    DisplayTexture: new Texture2D(GraphicsDeviceServiceForTests.Instance, 1, 1),
                    DisplayOrigin: Point.Zero,
                    TextLayout: MessageBoxTextLayout.Default,
                    DrawTextOverTexture: false),
                leaveStartedAt: 100,
                source: MessageBoxEntrySource.LocalCommand);

            float alphaBeforeUpdate = entry.GetAlpha(100);
            entry.Update(100);
            float alphaAfterUpdate = entry.GetAlpha(100);
            float alphaAtHalfFade = entry.GetAlpha(600);
            bool shouldRemoveAtFadeDuration = entry.ShouldRemove(1100);
            return (alphaBeforeUpdate, alphaAfterUpdate, alphaAtHalfFade, shouldRemoveAtFadeDuration, entry.ShouldDrawText);
        }

        internal static (int RemoveCanvasIndex, int FadeDurationMs, int InsertCanvasStartAlphaValue, int InsertCanvasEndAlphaValue, bool StopsLayerAnimation, bool RegistersOneTimeAnimation) GetLeaveFieldRegistrationTraceForTest()
        {
            return (
                ClientLeaveRemoveCanvasIndex,
                ClientLeaveCanvasFadeDurationMs,
                ClientLeaveInsertCanvasStartAlphaValue,
                ClientLeaveInsertCanvasEndAlphaValue,
                StopsLayerAnimation: true,
                RegistersOneTimeAnimation: true);
        }

        internal static (int VectorObjectStringPoolId, int LayerColorValue, int InitialAlpha, int AnimationMode, bool CreatesVector, bool CreatesLayer, bool LoadsAnimationLayer, bool AttachesLayerToVector, bool LoadsCanvasObject, int? LoadedCanvasIndex) GetEnterFieldRegistrationTraceForTest()
        {
            ManagedMessageBoxLayer layer = ManagedMessageBoxLayer.CreateLoaded(Point.Zero, currentTick: 0);
            return (
                layer.VectorObjectStringPoolId.GetValueOrDefault(),
                layer.LayerColorValue.GetValueOrDefault(),
                layer.InitialAlpha.GetValueOrDefault(),
                layer.AnimationMode,
                layer.CreatedVector,
                layer.CreatedLayer,
                layer.LoadedAnimationLayer,
                layer.AttachedLayerToVector,
                layer.LoadedCanvasObject,
                layer.LoadedCanvasIndex);
        }

        internal static ((int PaddingLeft, int PaddingTop, int PaddingRight, int PaddingBottom) FirstFrame, (int PaddingLeft, int PaddingTop, int PaddingRight, int PaddingBottom) SecondFrame) ComputeAnimatedFrameTextLayoutsForTest(
            int textureWidth,
            int textureHeight,
            Rectangle firstRect,
            Rectangle secondRect)
        {
            using Texture2D texture = new Texture2D(GraphicsDeviceServiceForTests.Instance, textureWidth, textureHeight);

            WzSubProperty root = new(ClientMessageBoxPropertyName);
            root.AddProperty(CreateFrameProperty("0", firstRect));
            root.AddProperty(CreateFrameProperty("1", secondRect));

            FieldMessageBoxRuntime runtime = new();
            runtime.Initialize(GraphicsDeviceServiceForTests.Instance);
            bool loaded = runtime.TryCollectFrames(
                root,
                ClientMessageBoxPropertyName,
                metadataFallbackProperty: null,
                out _,
                out _,
                out _,
                out List<MessageBoxTextLayout> textLayouts,
                out _);

            if (!loaded || textLayouts.Count < 2)
            {
                return (default, default);
            }

            MessageBoxTextLayout first = textLayouts[0];
            MessageBoxTextLayout second = textLayouts[1];
            return (
                (first.PaddingLeft, first.PaddingTop, first.PaddingRight, first.PaddingBottom),
                (second.PaddingLeft, second.PaddingTop, second.PaddingRight, second.PaddingBottom));
        }

        private static WzCanvasProperty CreateFrameProperty(string name, Rectangle textRect)
        {
            WzCanvasProperty canvas = new(name);
            System.Drawing.Bitmap bitmap = new(Math.Max(1, textRect.Right), Math.Max(1, textRect.Bottom));
            canvas.PngProperty = new WzPngProperty { PNG = bitmap };

            WzSubProperty text = new(TextPropertyName);
            text.AddProperty(new WzVectorProperty("lt", textRect.Left, textRect.Top));
            text.AddProperty(new WzVectorProperty("rb", textRect.Right, textRect.Bottom));
            canvas.AddProperty(text);
            return canvas;
        }

        internal static (int PaddingLeft, int PaddingTop, int PaddingRight, int PaddingBottom, int MaxLineCount) ComputeUiBoardTextLayoutForTest(
            int boardWidth,
            int boardHeight,
            int centerWidth,
            int centerHeight,
            int topHeight,
            int bottomHeight)
        {
            using Texture2D centerTexture = centerWidth > 0 && centerHeight > 0
                ? new Texture2D(GraphicsDeviceServiceForTests.Instance, centerWidth, centerHeight)
                : null;
            MessageBoxTextLayout layout = BuildUiBoardTextLayout(boardWidth, boardHeight, centerTexture, topHeight, bottomHeight);
            return (layout.PaddingLeft, layout.PaddingTop, layout.PaddingRight, layout.PaddingBottom, layout.MaxLineCount);
        }

        internal static int ResolveUiCenterRepeatCountForTest(WzSubProperty uiProperty)
        {
            return ResolveUiCenterRepeatCount(uiProperty);
        }

        internal static (string LeaveState, int? RemovedCanvasIndex, int? InsertCanvasDuration, int? InsertCanvasStartAlphaValue, int? InsertCanvasEndAlphaValue, int AnimationMode, bool RegisteredOneTimeAnimation, bool ShouldRemoveAtFadeEnd) SimulateClientLeaveLayerForTest(int currentTick)
        {
            ManagedMessageBoxLayer layer = ManagedMessageBoxLayer.CreateLoaded(Point.Zero, currentTick);
            layer.BeginLeave(Point.Zero, currentTick);
            return (
                layer.LeaveState.ToString(),
                layer.RemovedCanvasIndex,
                layer.InsertCanvasDuration,
                layer.InsertCanvasStartAlphaValue,
                layer.InsertCanvasEndAlphaValue,
                layer.AnimationMode,
                layer.RegisteredOneTimeAnimation,
                layer.ShouldRemoveAfterLeave(currentTick + ClientLeaveCanvasFadeDurationMs));
        }

        internal static (int? GetCanvasIndex, int? RemovedCanvasIndex, bool RemovedCanvasCaptured, int? InsertCanvasDuration, int? InsertCanvasStartAlphaValue, int? InsertCanvasEndAlphaValue, bool InsertedCapturedCanvas, bool VectorResetForLeave, int AnimationMode, bool RegisteredOneTimeAnimation, bool RegisteredLayerObject) SimulateClientLeaveCanvasOwnershipForTest(int currentTick)
        {
            ManagedMessageBoxLayer layer = ManagedMessageBoxLayer.CreateLoaded(new Point(11, 29), currentTick);
            layer.BeginLeave(new Point(11, 29), currentTick);
            return (
                layer.GetCanvasIndex,
                layer.RemovedCanvasIndex,
                layer.RemovedCanvasCaptured,
                layer.InsertCanvasDuration,
                layer.InsertCanvasStartAlphaValue,
                layer.InsertCanvasEndAlphaValue,
                layer.InsertedCapturedCanvas,
                layer.VectorResetForLeave,
                layer.AnimationMode,
                layer.RegisteredOneTimeAnimation,
                layer.RegisteredOneTimeAnimationLayerObject);
        }

        private FrozenMessageBoxRenderState CaptureLeaveRenderState(FieldMessageBoxEntry entry)
        {
            Texture2D displayTexture = entry.GetDisplayTexture();
            Point displayOrigin = entry.GetDisplayOrigin();
            MessageBoxTextLayout textLayout = entry.GetDisplayTextLayout();
            bool drawTextOverTexture = true;
            if (TryCreateLeaveSnapshot(entry, out Texture2D snapshotTexture, out Point snapshotOrigin))
            {
                displayTexture = snapshotTexture;
                displayOrigin = snapshotOrigin;
                drawTextOverTexture = false;
            }

            return new FrozenMessageBoxRenderState(displayTexture, displayOrigin, textLayout, drawTextOverTexture);
        }

        private bool TryCreateLeaveSnapshot(FieldMessageBoxEntry entry, out Texture2D snapshotTexture, out Point snapshotOrigin)
        {
            snapshotTexture = null;
            snapshotOrigin = Point.Zero;
            if (_graphicsDevice == null || _snapshotSpriteBatch == null || _snapshotFont == null)
            {
                return false;
            }

            Texture2D frameTexture = entry.GetDisplayTexture();
            Point frameOrigin = entry.GetDisplayOrigin();
            Rectangle boardBounds = frameTexture != null
                ? new Rectangle(-frameOrigin.X, -frameOrigin.Y, frameTexture.Width, frameTexture.Height)
                : BuildFallbackBounds(Point.Zero, _snapshotFont, entry);
            if (boardBounds.Width <= 0 || boardBounds.Height <= 0)
            {
                return false;
            }

            RenderTargetBinding[] previousTargets = _graphicsDevice.GetRenderTargets();
            Viewport previousViewport = _graphicsDevice.Viewport;
            RenderTarget2D snapshot = new(
                _graphicsDevice,
                boardBounds.Width,
                boardBounds.Height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None);

            try
            {
                _graphicsDevice.SetRenderTarget(snapshot);
                _graphicsDevice.Clear(Color.Transparent);
                _snapshotSpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

                Rectangle localBounds = new(0, 0, boardBounds.Width, boardBounds.Height);
                if (frameTexture != null)
                {
                    Vector2 framePosition = new(-frameOrigin.X - boardBounds.X, -frameOrigin.Y - boardBounds.Y);
                    _snapshotSpriteBatch.Draw(frameTexture, framePosition, Color.White);
                }
                else
                {
                    DrawFallbackBoard(_snapshotSpriteBatch, localBounds, 1f);
                }

                DrawBoardText(_snapshotSpriteBatch, _snapshotFont, localBounds, entry, 1f);
                _snapshotSpriteBatch.End();
            }
            finally
            {
                if (previousTargets.Length > 0)
                {
                    _graphicsDevice.SetRenderTargets(previousTargets);
                }
                else
                {
                    _graphicsDevice.SetRenderTarget(null);
                }

                _graphicsDevice.Viewport = previousViewport;
            }

            snapshotTexture = snapshot;
            snapshotOrigin = new Point(-boardBounds.X, -boardBounds.Y);
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

        private float ComputeInitialBobAngleRadians()
        {
            return _random.Next(ClientBobInitialPhaseModulo);
        }

        private static int ComputeClientBobOffset(float angleRadians)
        {
            return (int)Math.Round(Math.Sin(angleRadians) * DefaultBobAmplitude);
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

        private static bool? TryReadBool(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value != 0,
                WzShortProperty shortProperty => shortProperty.Value != 0,
                WzFloatProperty floatProperty => Math.Abs(floatProperty.Value) > float.Epsilon,
                WzStringProperty stringProperty when bool.TryParse(stringProperty.Value, out bool boolValue) => boolValue,
                WzStringProperty stringProperty when int.TryParse(stringProperty.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue) => intValue != 0,
                _ => null
            };
        }

        private static string TryReadString(WzImageProperty property)
        {
            return property switch
            {
                WzStringProperty stringProperty => stringProperty.Value,
                WzUOLProperty uolProperty => uolProperty.Value,
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
            bool ShouldDrawText { get; }
            Texture2D GetDisplayTexture();
            Point GetDisplayOrigin();
            MessageBoxTextLayout GetDisplayTextLayout();
            float GetAlpha(int currentTick);
            int GetVerticalFloatOffset(int currentTick);
        }

        private sealed class FieldMessageBoxEntry : IMessageBoxDrawableEntry
        {
            private int _frameIndex;
            private int _nextFrameTick;
            private float _bobAngleRadians;

            public FieldMessageBoxEntry(
                int id,
                int itemId,
                string messageText,
                string characterName,
                Point hostPosition,
                Point layerPosition,
                MessageBoxVisual visual,
                string itemName,
                float initialBobAngleRadians,
                int currentTick,
                MessageBoxEntrySource source)
            {
                Id = id;
                ItemId = itemId;
                MessageText = messageText ?? string.Empty;
                CharacterName = characterName ?? string.Empty;
                HostPosition = hostPosition;
                LayerPosition = layerPosition;
                Visual = visual;
                ItemName = itemName ?? string.Empty;
                Source = source;
                _bobAngleRadians = initialBobAngleRadians;
                _nextFrameTick = currentTick + (visual?.GetFrameDelay(0) ?? DefaultFrameDelayMs);
                ClientLayer = ManagedMessageBoxLayer.CreateLoaded(layerPosition, currentTick);
            }

            public int Id { get; }
            public int ItemId { get; }
            public string MessageText { get; }
            public string CharacterName { get; }
            public Point HostPosition { get; }
            public Point LayerPosition { get; }
            public MessageBoxVisual Visual { get; }
            public string ItemName { get; }
            public MessageBoxEntrySource Source { get; }
            public ManagedMessageBoxLayer ClientLayer { get; }
            public int FrameIndex => _frameIndex;
            public bool ShouldDrawText => true;

            public void Update(int currentTick)
            {
                _bobAngleRadians += ClientBobAngleStepRadians;
                int bobOffsetY = ComputeClientBobOffset(_bobAngleRadians);
                ClientLayer.RelMove(new Point(LayerPosition.X, LayerPosition.Y + bobOffsetY), currentTick);
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
                return ClientLayer.CurrentPosition.Y - LayerPosition.Y;
            }

            public Texture2D GetDisplayTexture()
            {
                return Visual?.GetFrameTexture(_frameIndex);
            }

            public Point GetDisplayOrigin()
            {
                return Visual?.GetFrameOrigin(_frameIndex) ?? Point.Zero;
            }

            public MessageBoxTextLayout GetDisplayTextLayout() => Visual?.GetFrameTextLayout(_frameIndex) ?? MessageBoxTextLayout.Default;
        }

        private sealed class LeavingMessageBoxEntry : IMessageBoxDrawableEntry
        {
            private readonly ManagedMessageBoxLayer _clientLayer;
            private readonly FrozenMessageBoxRenderState _leaveRenderState;

            internal LeavingMessageBoxEntry(int id, string messageText, Point layerPosition, MessageBoxVisual visual, FrozenMessageBoxRenderState leaveRenderState, int leaveStartedAt, MessageBoxEntrySource source)
                : this(id, messageText, layerPosition, visual, null, leaveRenderState, leaveStartedAt, source)
            {
            }

            internal LeavingMessageBoxEntry(int id, string messageText, Point layerPosition, MessageBoxVisual visual, ManagedMessageBoxLayer clientLayer, FrozenMessageBoxRenderState leaveRenderState, int leaveStartedAt, MessageBoxEntrySource source)
            {
                Id = id;
                MessageText = messageText;
                LayerPosition = layerPosition;
                Visual = visual;
                _clientLayer = clientLayer ?? ManagedMessageBoxLayer.CreateLoaded(layerPosition, leaveStartedAt);
                _leaveRenderState = leaveRenderState;
                Source = source;
                _clientLayer.BeginLeave(layerPosition, leaveStartedAt);
            }

            public int Id { get; }
            public string MessageText { get; }
            public Point LayerPosition { get; }
            public MessageBoxVisual Visual { get; }
            public MessageBoxEntrySource Source { get; }
            public bool ShouldDrawText => _clientLayer.LeaveState == LeaveCanvasState.ReinsertedFade && _leaveRenderState.DrawTextOverTexture;

            public void Update(int currentTick)
            {
                _clientLayer.UpdateLeave(currentTick);
            }

            public static LeavingMessageBoxEntry FromEntry(FieldMessageBoxEntry entry, FrozenMessageBoxRenderState leaveRenderState, int currentTick)
            {
                return new LeavingMessageBoxEntry(
                    entry.Id,
                    entry.MessageText,
                    entry.LayerPosition,
                    entry.Visual,
                    entry.ClientLayer,
                    leaveRenderState,
                    currentTick,
                    entry.Source);
            }

            public bool ShouldRemove(int currentTick)
            {
                return _clientLayer.ShouldRemoveAfterLeave(currentTick);
            }

            public Texture2D GetDisplayTexture()
            {
                return _clientLayer.LeaveState == LeaveCanvasState.ReinsertedFade
                    ? _leaveRenderState.DisplayTexture
                    : null;
            }

            public Point GetDisplayOrigin()
            {
                return _leaveRenderState.DisplayOrigin;
            }

            public MessageBoxTextLayout GetDisplayTextLayout() => _leaveRenderState.TextLayout;

            public float GetAlpha(int currentTick)
            {
                if (_clientLayer.LeaveState == LeaveCanvasState.Removed)
                {
                    return 0f;
                }

                return ComputeLeaveFadeAlpha(_clientLayer.GetLeaveFadeElapsed(currentTick));
            }

            public int GetVerticalFloatOffset(int currentTick)
            {
                return 0;
            }
        }

        private sealed class ManagedMessageBoxLayer
        {
            private readonly ManagedMessageBoxLayerObject _layerObject;
            private int _leaveStartedAt;

            private ManagedMessageBoxLayer(Point currentPosition)
            {
                CurrentPosition = currentPosition;
                LeaveState = LeaveCanvasState.NotLeaving;
                AnimationMode = ClientLayerRepeatAnimation;
                _layerObject = new ManagedMessageBoxLayerObject();
            }

            public Point CurrentPosition { get; private set; }
            public LeaveCanvasState LeaveState { get; private set; }
            public int AnimationMode { get; private set; }
            public int? RemovedCanvasIndex { get; private set; }
            public int? InsertCanvasDuration { get; private set; }
            public int? InsertCanvasStartAlphaValue { get; private set; }
            public int? InsertCanvasEndAlphaValue { get; private set; }
            public bool VectorResetForLeave { get; private set; }
            public bool RegisteredOneTimeAnimation { get; private set; }
            public bool RemovedFromPool { get; private set; }
            public int? VectorObjectStringPoolId { get; private set; }
            public int? LayerColorValue { get; private set; }
            public int? InitialAlpha { get; private set; }
            public bool CreatedVector { get; private set; }
            public bool CreatedLayer { get; private set; }
            public bool LoadedAnimationLayer { get; private set; }
            public bool AttachedLayerToVector { get; private set; }
            public bool LoadedCanvasObject { get; private set; }
            public int? LoadedCanvasIndex { get; private set; }
            public int? GetCanvasIndex { get; private set; }
            public bool RemovedCanvasCaptured { get; private set; }
            public bool InsertedCapturedCanvas { get; private set; }
            public bool RegisteredOneTimeAnimationLayerObject { get; private set; }

            public static ManagedMessageBoxLayer CreateLoaded(Point position, int currentTick)
            {
                ManagedMessageBoxLayer layer = new(position)
                {
                    _leaveStartedAt = currentTick
                };
                layer.CreateVector(ClientLayerVectorObjectStringPoolId);
                layer.RelMove(position, currentTick);
                layer.CreateLayer();
                layer.LoadAnimationLayer();
                layer.SetLayerColor(ClientLayerColorValue);
                layer.SetAlpha(ClientLayerInitialAlpha);
                layer.AttachLayerToVector();
                layer.Animate(ClientLayerRepeatAnimation);
                return layer;
            }

            public void RelMove(Point position, int currentTick)
            {
                CurrentPosition = position;
            }

            public void BeginLeave(Point layerPosition, int currentTick)
            {
                RemoveFromPool(immediate: false);
                ResetVectorForLeave(layerPosition);
                ManagedMessageBoxCanvas removedCanvas = GetCanvas(ClientLeaveGetCanvasIndex);
                RemoveCanvas(ClientLeaveRemoveCanvasIndex, removedCanvas);
                InsertCanvas(
                    removedCanvas,
                    ClientLeaveCanvasFadeDurationMs,
                    ClientLeaveInsertCanvasStartAlphaValue,
                    ClientLeaveInsertCanvasEndAlphaValue,
                    currentTick + ClientLeaveCanvasReinsertDelayMs);
                Animate(ClientLayerStopAnimation);
                RegisterOneTimeAnimation(_layerObject);
            }

            public void RemoveFromPool(bool immediate)
            {
                RemovedFromPool = true;
                if (immediate)
                {
                    LeaveState = LeaveCanvasState.Removed;
                    Animate(ClientLayerStopAnimation);
                }
            }

            private ManagedMessageBoxCanvas GetCanvas(int index)
            {
                GetCanvasIndex = index;
                return _layerObject.Canvas;
            }

            private void RemoveCanvas(int index, ManagedMessageBoxCanvas canvas)
            {
                RemovedCanvasIndex = index;
                RemovedCanvasCaptured = canvas != null && ReferenceEquals(canvas, _layerObject.Canvas);
                if (RemovedCanvasCaptured)
                {
                    _layerObject.Canvas = null;
                }

                LeaveState = LeaveCanvasState.CanvasRemoved;
            }

            private void InsertCanvas(ManagedMessageBoxCanvas canvas, int duration, int startAlphaValue, int endAlphaValue, int fadeStartedAt)
            {
                InsertCanvasDuration = duration;
                InsertCanvasStartAlphaValue = startAlphaValue;
                InsertCanvasEndAlphaValue = endAlphaValue;
                _leaveStartedAt = fadeStartedAt;
                InsertedCapturedCanvas = canvas != null;
                if (canvas != null)
                {
                    _layerObject.Canvas = canvas;
                }

                LeaveState = LeaveCanvasState.ReinsertedFade;
            }

            private void Animate(int mode)
            {
                AnimationMode = mode;
            }

            private void CreateVector(int stringPoolId)
            {
                CreatedVector = true;
                VectorObjectStringPoolId = stringPoolId;
            }

            private void CreateLayer()
            {
                CreatedLayer = true;
            }

            private void LoadAnimationLayer()
            {
                LoadedAnimationLayer = true;
                LoadedCanvasIndex = ClientLeaveGetCanvasIndex;
                LoadedCanvasObject = true;
                _layerObject.Canvas = new ManagedMessageBoxCanvas(LoadedCanvasIndex.Value);
            }

            private void SetLayerColor(int colorValue)
            {
                LayerColorValue = colorValue;
            }

            private void SetAlpha(int alpha)
            {
                InitialAlpha = alpha;
            }

            private void AttachLayerToVector()
            {
                AttachedLayerToVector = true;
            }

            private void ResetVectorForLeave(Point layerPosition)
            {
                CurrentPosition = layerPosition;
                VectorResetForLeave = true;
            }

            private void RegisterOneTimeAnimation(ManagedMessageBoxLayerObject layerObject)
            {
                RegisteredOneTimeAnimation = true;
                RegisteredOneTimeAnimationLayerObject = ReferenceEquals(layerObject, _layerObject);
            }

            public void UpdateLeave(int currentTick)
            {
                if (ShouldRemoveAfterLeave(currentTick))
                {
                    LeaveState = LeaveCanvasState.Removed;
                }
            }

            public int GetLeaveFadeElapsed(int currentTick)
            {
                return Math.Max(0, currentTick - _leaveStartedAt);
            }

            public bool ShouldRemoveAfterLeave(int currentTick)
            {
                return GetLeaveFadeElapsed(currentTick) >= ClientLeaveCanvasFadeDurationMs;
            }
        }

        private sealed class ManagedMessageBoxLayerObject
        {
            public ManagedMessageBoxCanvas Canvas { get; set; }
        }

        private sealed record ManagedMessageBoxCanvas(int Index);

        private sealed record MessageBoxVisual(
            IReadOnlyList<Texture2D> Frames,
            IReadOnlyList<Point> Origins,
            IReadOnlyList<int> Delays,
            IReadOnlyList<MessageBoxTextLayout> TextLayouts,
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

            public MessageBoxTextLayout GetFrameTextLayout(int frameIndex)
            {
                if (TextLayouts == null || TextLayouts.Count == 0)
                {
                    return TextLayout;
                }

                int index = Math.Clamp(frameIndex, 0, TextLayouts.Count - 1);
                return TextLayouts[index];
            }
        }

        private readonly record struct FrozenMessageBoxRenderState(
            Texture2D DisplayTexture,
            Point DisplayOrigin,
            MessageBoxTextLayout TextLayout,
            bool DrawTextOverTexture);

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

            public Rectangle GetContentBounds(Rectangle boardBounds)
            {
                int left = Math.Clamp(boardBounds.X + PaddingLeft, boardBounds.Left, boardBounds.Right);
                int top = Math.Clamp(boardBounds.Y + PaddingTop, boardBounds.Top, boardBounds.Bottom);
                int right = Math.Clamp(boardBounds.Right - PaddingRight, left, boardBounds.Right);
                int bottom = Math.Clamp(boardBounds.Bottom - PaddingBottom, top, boardBounds.Bottom);
                return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
            }
        }

        private static float ComputeLeaveFadeAlpha(int elapsedMs)
        {
            float progress = Math.Clamp(elapsedMs / (float)ClientLeaveCanvasFadeDurationMs, 0f, 1f);
            int alpha = (int)Math.Round(ClientLeaveStartAlpha + ((ClientLeaveEndAlpha - ClientLeaveStartAlpha) * progress));
            return Math.Clamp(alpha / 255f, 0f, 1f);
        }

        internal enum MessageBoxEntrySource
        {
            LocalCommand,
            PacketEnterField
        }

        private enum LeaveCanvasState
        {
            NotLeaving,
            RegisteredOneTimeAnimation,
            CanvasRemoved,
            Removed,
            ReinsertedFade
        }
    }

    internal static class FieldMessageBoxRuntimeExtensions
    {
        internal static string GetLabel(this FieldMessageBoxRuntime.MessageBoxEntrySource source)
        {
            return source == FieldMessageBoxRuntime.MessageBoxEntrySource.PacketEnterField
                ? "packet-owned"
                : "local";
        }
    }

    internal static class GraphicsDeviceServiceForTests
    {
        private static GraphicsDevice _instance;

        internal static GraphicsDevice Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                PresentationParameters presentationParameters = new()
                {
                    BackBufferWidth = 1,
                    BackBufferHeight = 1,
                    DeviceWindowHandle = IntPtr.Zero,
                    PresentationInterval = PresentInterval.Immediate,
                    IsFullScreen = false
                };
                _instance = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.Reach, presentationParameters);
                return _instance;
            }
        }
    }
}
