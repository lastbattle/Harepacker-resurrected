using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class AvatarMegaphoneRuntime
    {
        private const int DefaultItemId = 5390000;
        private const int PanelWidth = 225;
        private const int PanelOffscreenPadding = 100;
        private const int SlideDurationMs = 325;
        private const int NameFadeDurationMs = 1500;
        private const string PreviewActionName = "stand1";
        private const int MessageTextX = 124;
        private static readonly int[] MessageTextY = { 14, 32, 50, 68 };
        private const int NameCenterX = 78;
        private const int NameTextY = 84;
        private const int ShortNameTagX = 48;
        private const int LongNameTagX = 3;
        private const int NameTagY = 84;
        private const int NameTagThresholdWidth = 70;
        private const int PreviewX = 82;
        private const int PreviewY = 85;
        private const int ShakeStepMs = 65;
        private const int ShakeOffsetMagnitude = 5;

        private static readonly IReadOnlyDictionary<int, AvatarMegaphoneItemProfile> FallbackProfiles =
            new Dictionary<int, AvatarMegaphoneItemProfile>
            {
                [5390000] = new(5390000, "Map/MapHelper.img/AvatarMegaphone/Burning", 5, false),
                [5390001] = new(5390001, "Map/MapHelper.img/AvatarMegaphone/Bright", 2, false),
                [5390002] = new(5390002, "Map/MapHelper.img/AvatarMegaphone/Heart", 17, false),
                [5390003] = new(5390003, "Map/MapHelper.img/AvatarMegaphone/Newyear1", 17, false),
                [5390004] = new(5390004, "Map/MapHelper.img/AvatarMegaphone/Newyear2", 17, false),
                [5390005] = new(5390005, "Map/MapHelper.img/AvatarMegaphone/Tiger1", 17, false),
                [5390006] = new(5390006, "Map/MapHelper.img/AvatarMegaphone/Tiger2", 15, true),
                [5390007] = new(5390007, "Map/MapHelper.img/AvatarMegaphone/Goal", 15, true),
                [5390008] = new(5390008, "Map/MapHelper.img/AvatarMegaphone/WorldCup", 15, true)
            };

        private readonly Dictionary<int, AvatarMegaphoneItemProfile> _profileCache = new();
        private readonly Dictionary<string, IReadOnlyList<AvatarMegaphoneAnimationFrame>> _animationCache = new(StringComparer.OrdinalIgnoreCase);

        private string _draftSender = "ExplorerGM";
        private readonly string[] _draftMessageFragments = new string[4];
        private int _draftItemId = DefaultItemId;
        private bool _draftWhisper;
        private int _draftChannelId = -1;
        private CharacterBuild _localAvatarTemplate;

        private AvatarMegaphonePresentation _activePresentation;
        private int _presentationStartedAt = int.MinValue;
        private int _dismissStartedAt = int.MinValue;
        private string _statusMessage = "Avatar megaphone owner idle.";

        internal void UpdateLocalContext(CharacterBuild build)
        {
            _localAvatarTemplate = build?.Clone();
            if (_localAvatarTemplate != null && string.IsNullOrWhiteSpace(_draftSender))
            {
                _draftSender = string.IsNullOrWhiteSpace(_localAvatarTemplate.Name)
                    ? "ExplorerGM"
                    : _localAvatarTemplate.Name.Trim();
            }
        }

        internal string DescribeStatus(int currentTick, int screenWidth)
        {
            if (_activePresentation == null)
            {
                AvatarMegaphoneItemProfile draftProfile = ResolveItemProfile(_draftItemId);
                return $"Avatar megaphone idle: item {_draftItemId} ({draftProfile?.ResourcePath ?? "unresolved"}), sender '{_draftSender}', whisper={(_draftWhisper ? 1 : 0)}, channel={_draftChannelId}. {_statusMessage}";
            }

            AvatarMegaphoneLayout layout = BuildLayout(currentTick, screenWidth);
            return $"Avatar megaphone active: item {_activePresentation.ItemId} ({_activePresentation.ItemProfile.ResourcePath}), sender '{_activePresentation.Sender}', x={layout.PanelX}, alpha={layout.NameAlpha}, whisper={(_activePresentation.Whisper ? 1 : 0)}, channel={_activePresentation.ChannelId}. {_statusMessage}";
        }

        internal string LoadSample(CharacterBuild build, string mapName)
        {
            UpdateLocalContext(build);
            _draftItemId = 5390002;
            _draftSender = string.IsNullOrWhiteSpace(build?.Name) ? "ExplorerGM" : build.Name.Trim();
            _draftMessageFragments[0] = "MapSimulator now mirrors";
            _draftMessageFragments[1] = "the avatar megaphone owner.";
            _draftMessageFragments[2] = string.IsNullOrWhiteSpace(mapName) ? "Previewing the field overlay." : $"Field: {mapName}";
            _draftMessageFragments[3] = "Check chat type 18 and shake parity.";
            _draftWhisper = false;
            _draftChannelId = -1;
            _statusMessage = $"Loaded avatar megaphone sample for {_draftSender}.";
            return _statusMessage;
        }

        internal string SetSender(string sender)
        {
            if (string.IsNullOrWhiteSpace(sender))
            {
                return "Avatar megaphone sender must not be empty.";
            }

            _draftSender = sender.Trim();
            _statusMessage = $"Avatar megaphone sender set to {_draftSender}.";
            return _statusMessage;
        }

        internal string SetDraftLine(int index, string text)
        {
            if (index < 1 || index > _draftMessageFragments.Length)
            {
                return "Avatar megaphone line index must be between 1 and 4.";
            }

            _draftMessageFragments[index - 1] = text?.Trim() ?? string.Empty;
            _statusMessage = $"Avatar megaphone line {index.ToString(CultureInfo.InvariantCulture)} updated.";
            return _statusMessage;
        }

        internal string SetItem(int itemId)
        {
            AvatarMegaphoneItemProfile profile = ResolveItemProfile(itemId);
            if (profile == null)
            {
                return $"Avatar megaphone item {itemId} is not supported by the v95 item/Cash/0539 owner set.";
            }

            _draftItemId = itemId;
            _statusMessage = $"Avatar megaphone item set to {itemId} ({profile.ResourcePath}, emotion {profile.EmotionId}).";
            return _statusMessage;
        }

        internal string SetWhisper(bool whisper)
        {
            _draftWhisper = whisper;
            _statusMessage = $"Avatar megaphone whisper flag set to {(whisper ? 1 : 0)}.";
            return _statusMessage;
        }

        internal string SetChannel(int channelId)
        {
            _draftChannelId = channelId;
            _statusMessage = $"Avatar megaphone channel id set to {channelId}.";
            return _statusMessage;
        }

        internal bool TryActivate(int currentTick, out string chatLogLine, out string message)
        {
            chatLogLine = string.Empty;
            message = string.Empty;

            AvatarMegaphoneItemProfile profile = ResolveItemProfile(_draftItemId);
            if (profile == null)
            {
                message = $"Avatar megaphone item {_draftItemId} is not supported.";
                return false;
            }

            CharacterBuild presentationBuild = _localAvatarTemplate?.Clone();
            if (presentationBuild == null)
            {
                message = "Avatar megaphone preview requires an active local character build.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_draftSender))
            {
                presentationBuild.Name = _draftSender;
            }

            string[] fragments = _draftMessageFragments
                .Select(fragment => fragment?.Trim() ?? string.Empty)
                .ToArray();

            _activePresentation = new AvatarMegaphonePresentation(
                _draftItemId,
                profile,
                _draftSender,
                fragments,
                _draftWhisper,
                _draftChannelId,
                presentationBuild);
            _presentationStartedAt = currentTick;
            _dismissStartedAt = int.MinValue;

            string joinedMessage = string.Concat(fragments);
            string filteredMessage = ClientCurseProcessParity.FilterTextForClientDisplay(joinedMessage);
            chatLogLine = $"{_draftSender} : {filteredMessage}";
            _statusMessage = $"Avatar megaphone activated for {_draftSender} with item {_draftItemId}.";
            message = _statusMessage;
            return true;
        }

        internal string Clear()
        {
            if (_activePresentation == null)
            {
                _statusMessage = "Avatar megaphone is already idle.";
                return _statusMessage;
            }

            if (_dismissStartedAt == int.MinValue)
            {
                _dismissStartedAt = Environment.TickCount;
                _statusMessage = "Avatar megaphone started the client-owned Bye transition.";
            }

            return _statusMessage;
        }

        internal void Update(int currentTick)
        {
            if (_activePresentation == null || _dismissStartedAt == int.MinValue)
            {
                return;
            }

            if (currentTick - _dismissStartedAt < SlideDurationMs)
            {
                return;
            }

            _activePresentation = null;
            _presentationStartedAt = int.MinValue;
            _dismissStartedAt = int.MinValue;
            _statusMessage = "Avatar megaphone owner idle.";
        }

        internal bool ShouldTriggerTremble => _activePresentation?.ItemProfile?.TriggersTremble == true;

        internal void Draw(
            SpriteBatch spriteBatch,
            SpriteFont font,
            GraphicsDevice device,
            int currentTick,
            int screenWidth)
        {
            if (_activePresentation == null || spriteBatch == null || font == null || device == null)
            {
                return;
            }

            AvatarMegaphoneLayout layout = BuildLayout(currentTick, screenWidth);
            if (layout.PanelX >= screenWidth + PanelOffscreenPadding)
            {
                return;
            }

            Texture2D backgroundTexture = ResolveBackgroundTexture(device);
            Texture2D shortNameTagTexture = ResolveNameTagTexture(device, longTag: false);
            Texture2D longNameTagTexture = ResolveNameTagTexture(device, longTag: true);
            IReadOnlyList<AvatarMegaphoneAnimationFrame> itemFrames = ResolveItemFrames(_activePresentation.ItemProfile.ResourcePath, device);

            int senderWidth = (int)Math.Ceiling(font.MeasureString(_activePresentation.Sender ?? string.Empty).X * 0.4f);
            bool useLongNameTag = senderWidth >= NameTagThresholdWidth;
            Texture2D nameTagTexture = useLongNameTag ? longNameTagTexture : shortNameTagTexture;
            int nameTagX = layout.PanelX + (useLongNameTag ? LongNameTagX : ShortNameTagX);

            if (backgroundTexture != null)
            {
                spriteBatch.Draw(backgroundTexture, new Vector2(layout.PanelX, 0f), Color.White);
            }

            AvatarMegaphoneAnimationFrame itemFrame = SelectFrame(itemFrames, currentTick);
            if (itemFrame?.Texture != null)
            {
                Vector2 itemPosition = new(layout.PanelX - itemFrame.Origin.X, -itemFrame.Origin.Y);
                spriteBatch.Draw(itemFrame.Texture, itemPosition, Color.White);
            }

            DrawPreviewAvatar(spriteBatch, currentTick, layout.PanelX);

            if (nameTagTexture != null)
            {
                Color nameTagColor = Color.White * (layout.NameAlpha / 255f);
                spriteBatch.Draw(nameTagTexture, new Vector2(nameTagX, NameTagY), nameTagColor);
            }

            for (int i = 0; i < _activePresentation.MessageFragments.Length && i < MessageTextY.Length; i++)
            {
                string fragment = _activePresentation.MessageFragments[i];
                if (string.IsNullOrWhiteSpace(fragment))
                {
                    continue;
                }

                ClientTextDrawing.DrawShadowed(
                    spriteBatch,
                    fragment,
                    new Vector2(layout.PanelX + MessageTextX, MessageTextY[i]),
                    new Color(20, 20, 20),
                    font,
                    0.38f);
            }

            if (!string.IsNullOrWhiteSpace(_activePresentation.Sender))
            {
                float centeredX = layout.PanelX + NameCenterX - ((font.MeasureString(_activePresentation.Sender).X * 0.4f) * 0.5f);
                ClientTextDrawing.DrawShadowed(
                    spriteBatch,
                    _activePresentation.Sender,
                    new Vector2(centeredX, NameTextY),
                    Color.White * (layout.NameAlpha / 255f),
                    font,
                    0.4f);
            }
        }

        private void DrawPreviewAvatar(SpriteBatch spriteBatch, int currentTick, int panelX)
        {
            if (_activePresentation?.Assembler == null)
            {
                return;
            }

            AssembledFrame frame = _activePresentation.Assembler.GetFrameAtTime(PreviewActionName, currentTick);
            frame?.Draw(spriteBatch, null, panelX + PreviewX, PreviewY, false, Color.White);
        }

        private AvatarMegaphoneLayout BuildLayout(int currentTick, int screenWidth)
        {
            int targetX = screenWidth - PanelWidth;
            int offscreenX = screenWidth + PanelOffscreenPadding;

            if (_dismissStartedAt != int.MinValue)
            {
                float dismissProgress = MathHelper.Clamp((currentTick - _dismissStartedAt) / (float)SlideDurationMs, 0f, 1f);
                int dismissX = (int)Math.Round(MathHelper.Lerp(targetX, offscreenX, dismissProgress));
                return new AvatarMegaphoneLayout(dismissX, 255);
            }

            int elapsed = Math.Max(0, currentTick - _presentationStartedAt);
            float enterProgress = MathHelper.Clamp(elapsed / (float)SlideDurationMs, 0f, 1f);
            int panelX = (int)Math.Round(MathHelper.Lerp(offscreenX, targetX, enterProgress));
            panelX += ResolveShakeOffset(elapsed);
            int nameAlpha = Math.Clamp((int)Math.Round(255f * MathHelper.Clamp(elapsed / (float)NameFadeDurationMs, 0f, 1f)), 0, 255);
            return new AvatarMegaphoneLayout(panelX, nameAlpha);
        }

        private static int ResolveShakeOffset(int elapsedMs)
        {
            for (int i = 1; i <= 6; i++)
            {
                int start = i * ShakeStepMs;
                int end = start + ShakeStepMs;
                if (elapsedMs >= start && elapsedMs < end)
                {
                    return (i % 2 != 0 ? ShakeOffsetMagnitude : -ShakeOffsetMagnitude);
                }
            }

            return 0;
        }

        private Texture2D ResolveBackgroundTexture(GraphicsDevice device)
        {
            if (_animationCache.TryGetValue("__background__", out IReadOnlyList<AvatarMegaphoneAnimationFrame> cachedFrames)
                && cachedFrames.Count > 0)
            {
                return cachedFrames[0].Texture;
            }

            WzCanvasProperty canvas = ResolveCanvas(MapleStoryStringPool.GetOrFallback(0x0FAE, "UI/UIWindow.img/AvatarMegaphone/backgrnd"));
            Texture2D texture = LoadTexture(canvas, device);
            if (texture != null)
            {
                _animationCache["__background__"] = new[] { new AvatarMegaphoneAnimationFrame(texture, Point.Zero, 0) };
            }

            return texture;
        }

        private Texture2D ResolveNameTagTexture(GraphicsDevice device, bool longTag)
        {
            string cacheKey = longTag ? "__nametag_long__" : "__nametag_short__";
            if (_animationCache.TryGetValue(cacheKey, out IReadOnlyList<AvatarMegaphoneAnimationFrame> cachedFrames)
                && cachedFrames.Count > 0)
            {
                return cachedFrames[0].Texture;
            }

            int stringPoolId = longTag ? 0x0FB1 : 0x0FB0;
            WzCanvasProperty canvas = ResolveCanvas(MapleStoryStringPool.GetOrFallback(
                stringPoolId,
                longTag ? "Map/MapHelper.img/AvatarMegaphone/name/1" : "Map/MapHelper.img/AvatarMegaphone/name/0"));
            Texture2D texture = LoadTexture(canvas, device);
            if (texture != null)
            {
                _animationCache[cacheKey] = new[] { new AvatarMegaphoneAnimationFrame(texture, Point.Zero, 0) };
            }

            return texture;
        }

        private IReadOnlyList<AvatarMegaphoneAnimationFrame> ResolveItemFrames(string resourcePath, GraphicsDevice device)
        {
            if (string.IsNullOrWhiteSpace(resourcePath) || device == null)
            {
                return Array.Empty<AvatarMegaphoneAnimationFrame>();
            }

            if (_animationCache.TryGetValue(resourcePath, out IReadOnlyList<AvatarMegaphoneAnimationFrame> cachedFrames))
            {
                return cachedFrames;
            }

            WzImageProperty root = ResolveClientResourcePath(resourcePath);
            List<AvatarMegaphoneAnimationFrame> frames = new();
            if (root?.WzProperties != null)
            {
                foreach (WzImageProperty child in root.WzProperties.OrderBy(ParseFrameOrder))
                {
                    WzCanvasProperty canvas = ResolveCanvas(child);
                    Texture2D texture = LoadTexture(canvas, device);
                    if (texture == null)
                    {
                        continue;
                    }

                    Point origin = ResolveCanvasOrigin(canvas);
                    int delay = Math.Max(1, child?["delay"]?.GetInt() ?? canvas?["delay"]?.GetInt() ?? 100);
                    frames.Add(new AvatarMegaphoneAnimationFrame(texture, origin, delay));
                }
            }

            if (frames.Count == 0)
            {
                WzCanvasProperty canvas = ResolveCanvas(root);
                Texture2D texture = LoadTexture(canvas, device);
                if (texture != null)
                {
                    frames.Add(new AvatarMegaphoneAnimationFrame(texture, ResolveCanvasOrigin(canvas), 100));
                }
            }

            IReadOnlyList<AvatarMegaphoneAnimationFrame> resolvedFrames = frames.Count > 0
                ? frames
                : Array.Empty<AvatarMegaphoneAnimationFrame>();
            _animationCache[resourcePath] = resolvedFrames;
            return resolvedFrames;
        }

        private AvatarMegaphoneItemProfile ResolveItemProfile(int itemId)
        {
            if (_profileCache.TryGetValue(itemId, out AvatarMegaphoneItemProfile cachedProfile))
            {
                return cachedProfile;
            }

            AvatarMegaphoneItemProfile fallback = FallbackProfiles.TryGetValue(itemId, out AvatarMegaphoneItemProfile fallbackProfile)
                ? fallbackProfile
                : null;

            WzImage itemImage = HaCreator.Program.FindImage("Item", "Cash/0539.img") ?? HaCreator.Program.FindImage("Item", "Cash/0539");
            WzSubProperty info = itemImage?[$"{itemId:D8}"]?["info"] as WzSubProperty;
            string path = (info?["path"] as WzStringProperty)?.Value ?? fallback?.ResourcePath;
            int emotion = (info?["emotion"] as WzIntProperty)?.Value ?? fallback?.EmotionId ?? 0;
            bool tremble = itemId is 5390006 or 5390007 or 5390008;

            if (string.IsNullOrWhiteSpace(path) || emotion <= 0)
            {
                return fallback;
            }

            AvatarMegaphoneItemProfile resolved = new(itemId, path, emotion, tremble);
            _profileCache[itemId] = resolved;
            return resolved;
        }

        private static AvatarMegaphoneAnimationFrame SelectFrame(IReadOnlyList<AvatarMegaphoneAnimationFrame> frames, int currentTick)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            if (frames.Count == 1)
            {
                return frames[0];
            }

            int totalDuration = frames.Sum(frame => Math.Max(1, frame.DelayMs));
            if (totalDuration <= 0)
            {
                return frames[0];
            }

            int animationTime = Math.Abs(currentTick % totalDuration);
            int elapsed = 0;
            foreach (AvatarMegaphoneAnimationFrame frame in frames)
            {
                elapsed += Math.Max(1, frame.DelayMs);
                if (animationTime < elapsed)
                {
                    return frame;
                }
            }

            return frames[^1];
        }

        private static int ParseFrameOrder(WzImageProperty property)
        {
            return int.TryParse(property?.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int order)
                ? order
                : int.MaxValue;
        }

        private static Texture2D LoadTexture(WzCanvasProperty canvas, GraphicsDevice device)
        {
            return canvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
        }

        private static Point ResolveCanvasOrigin(WzCanvasProperty canvas)
        {
            if (canvas == null)
            {
                return Point.Zero;
            }

            try
            {
                System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
                return new Point((int)origin.X, (int)origin.Y);
            }
            catch
            {
                return Point.Zero;
            }
        }

        private static WzCanvasProperty ResolveCanvas(string resourcePath)
        {
            return ResolveCanvas(ResolveClientResourcePath(resourcePath));
        }

        private static WzCanvasProperty ResolveCanvas(WzImageProperty property)
        {
            WzImageProperty realProperty = WzInfoTools.GetRealProperty(property);
            return realProperty as WzCanvasProperty;
        }

        private static WzImageProperty ResolveClientResourcePath(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            string normalized = resourcePath.Replace('\\', '/').Trim();
            int firstSeparator = normalized.IndexOf('/');
            if (firstSeparator <= 0 || firstSeparator >= normalized.Length - 1)
            {
                return null;
            }

            string category = normalized[..firstSeparator];
            string remainder = normalized[(firstSeparator + 1)..];
            int imageIndex = remainder.IndexOf(".img", StringComparison.OrdinalIgnoreCase);
            if (imageIndex < 0)
            {
                return null;
            }

            string imageName = remainder[..(imageIndex + 4)];
            string propertyPath = imageIndex + 4 < remainder.Length
                ? remainder[(imageIndex + 4)..].TrimStart('/')
                : string.Empty;

            foreach (string candidateImageName in EnumerateResourceImageCandidates(category, imageName))
            {
                WzImage image = HaCreator.Program.FindImage(category, candidateImageName);
                if (image == null || string.IsNullOrWhiteSpace(propertyPath))
                {
                    continue;
                }

                WzImageProperty property = ResolvePropertyPath(image, propertyPath);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateResourceImageCandidates(string category, string imageName)
        {
            if (!string.IsNullOrWhiteSpace(imageName))
            {
                yield return imageName;
            }

            if (!string.Equals(category, "UI", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            if (string.Equals(imageName, "UIWindow.img", StringComparison.OrdinalIgnoreCase))
            {
                yield return "UIWindow2.img";
            }
            else if (string.Equals(imageName, "UIWindow2.img", StringComparison.OrdinalIgnoreCase))
            {
                yield return "UIWindow.img";
            }
        }

        private static WzImageProperty ResolvePropertyPath(WzImage image, string relativePath)
        {
            if (image?.WzProperties == null || string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            WzImageProperty current = null;
            foreach (string segment in relativePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = current == null ? image[segment] : current[segment];
                if (current == null)
                {
                    return null;
                }
            }

            return current;
        }

        private sealed record AvatarMegaphonePresentation(
            int ItemId,
            AvatarMegaphoneItemProfile ItemProfile,
            string Sender,
            string[] MessageFragments,
            bool Whisper,
            int ChannelId,
            CharacterBuild Build)
        {
            internal CharacterAssembler Assembler { get; } = CreateAssembler(Build, ItemProfile);

            private static CharacterAssembler CreateAssembler(CharacterBuild build, AvatarMegaphoneItemProfile profile)
            {
                if (build == null)
                {
                    return null;
                }

                CharacterAssembler assembler = new(build);
                if (PacketOwnedAvatarEmotionResolver.TryResolveEmotionName(profile?.EmotionId ?? 0, out string emotionName))
                {
                    assembler.FaceExpressionName = emotionName;
                }

                return assembler;
            }
        }

        private sealed record AvatarMegaphoneItemProfile(int ItemId, string ResourcePath, int EmotionId, bool TriggersTremble);
        private sealed record AvatarMegaphoneAnimationFrame(Texture2D Texture, Point Origin, int DelayMs);
        private readonly record struct AvatarMegaphoneLayout(int PanelX, int NameAlpha);
    }
}
