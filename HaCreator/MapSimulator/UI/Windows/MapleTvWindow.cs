using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class MapleTvWindow : UIWindowBase
    {
        // CUIMapleTV::Draw centers the broadcast art on a 240px-wide MapleTV surface and
        // draws the send-board item text at (39,70) with its one-pixel highlight at (40,71).
        // The in-field layer is top-edge owned; the wider chat families overhang the
        // authored 240px surface, but the client still centers that base surface rather
        // than the full overhang bounds.
        private const int WorldOverlayTopMargin = 0;
        private const int MapleTvSurfaceWidth = 240;
        private const int MapleTvIdleSurfaceHeight = 90;
        private const int MapleTvMediaSurfaceHeight = 180;
        // CUIMapleTV::OnCreate wires dialog type 1 sender-only boards to a wider edit at
        // (18,113,180,75), while flexible/dedication boards keep (40,113,135,75).
        private static readonly Rectangle SenderOnlyDraftMessageTextBounds = new(18, 113, 180, 75);
        private static readonly Rectangle DraftMessageTextBounds = new(40, 113, 135, 75);
        private static readonly Rectangle ReceiverNameBounds = new(44, 68, 150, 14);
        // The MapleTV chat and media canvases carry non-zero WZ origins, so these bounds stay
        // frame-local and are resolved from the selected frame's actual top-left at draw time.
        private static readonly Rectangle DefaultChatTextBounds = new(20, 17, 200, 58);
        private static readonly Rectangle StarChatTextBounds = new(18, 16, 224, 72);
        private static readonly Rectangle HeartChatTextBounds = new(18, 16, 224, 72);
        private static readonly Point ItemNamePosition = new(39, 70);
        private static readonly Point PreviewAnchor = new(224, 8);
        private static readonly Point IdlePreviewOffset = new(0, 45);
        private const int MessageLineHeight = 15;
        private const int PreviewLineHeight = 15;
        private const string PreviewActionName = "stand1";

        private readonly IDXObject _selfFrame;
        private readonly IDXObject _receiverFrame;
        private readonly IDXObject _selfOverlay;
        private readonly IDXObject _receiverOverlay;
        private readonly MapleTvVisualAssets _visualAssets;
        private readonly Point _selfOverlayOffset;
        private readonly Point _receiverOverlayOffset;

        private SpriteFont _font;
        private Func<MapleTvSnapshot> _snapshotProvider;
        private Func<string> _publishHandler;
        private Func<string> _clearHandler;
        private Func<string> _toggleReceiverHandler;
        private Action<string> _feedbackHandler;
        private UIObject _okButton;
        private UIObject _cancelButton;
        private UIObject _receiverButton;
        private CharacterAssembler _senderAssembler;
        private CharacterAssembler _receiverAssembler;
        private string _senderBuildKey;
        private string _receiverBuildKey;
        private MapleTvSnapshot _currentSnapshot = new();

        public MapleTvWindow(
            IDXObject selfFrame,
            IDXObject receiverFrame,
            IDXObject selfOverlay,
            Point selfOverlayOffset,
            IDXObject receiverOverlay,
            Point receiverOverlayOffset,
            MapleTvVisualAssets visualAssets = null)
            : base(selfFrame)
        {
            _selfFrame = selfFrame ?? throw new ArgumentNullException(nameof(selfFrame));
            _receiverFrame = receiverFrame ?? selfFrame;
            _selfOverlay = selfOverlay;
            _receiverOverlay = receiverOverlay;
            _selfOverlayOffset = selfOverlayOffset;
            _receiverOverlayOffset = receiverOverlayOffset;
            _visualAssets = visualAssets;
            RefreshFrame(_currentSnapshot);
        }

        public override string WindowName => MapSimulatorWindowNames.MapleTv;

        internal int DefaultMediaIndex => _visualAssets?.DefaultMediaIndex ?? 1;

        internal void SetSnapshotProvider(Func<MapleTvSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            RefreshFrame(RefreshSnapshot());
        }

        internal void SetActionHandlers(
            Func<string> publishHandler,
            Func<string> clearHandler,
            Func<string> toggleReceiverHandler,
            Action<string> feedbackHandler)
        {
            _publishHandler = publishHandler;
            _clearHandler = clearHandler;
            _toggleReceiverHandler = toggleReceiverHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void InitializeControls(UIObject okButton, UIObject cancelButton, UIObject receiverButton)
        {
            _okButton = okButton;
            _cancelButton = cancelButton;
            _receiverButton = receiverButton;

            ConfigureButton(_okButton, () => ShowFeedback(_publishHandler?.Invoke()));
            ConfigureButton(_cancelButton, () => ShowFeedback(_clearHandler?.Invoke()));
            ConfigureButton(_receiverButton, () => ShowFeedback(_toggleReceiverHandler?.Invoke()));
            UpdateButtonStates(_currentSnapshot ?? RefreshSnapshot());
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            MapleTvSnapshot snapshot = RefreshSnapshot();
            RefreshFrame(snapshot);
            UpdateButtonStates(snapshot);
            RefreshAssemblers(snapshot);
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
            MapleTvSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            DrawLayer(
                sprite,
                snapshot.UseReceiver ? _receiverOverlay : _selfOverlay,
                snapshot.UseReceiver ? _receiverOverlayOffset : _selfOverlayOffset,
                drawReflectionInfo,
                skeletonMeshRenderer,
                gameTime);

            if (_font == null)
            {
                return;
            }

            string itemLabel = snapshot.ItemId > 0
                ? $"{snapshot.ItemName} ({snapshot.ItemId})"
                : snapshot.ItemName;
            DrawShadowText(sprite, itemLabel, Position.X + ItemNamePosition.X, Position.Y + ItemNamePosition.Y, new Color(24, 24, 24), 0.4f);

            if (snapshot.UseReceiver)
            {
                DrawShadowText(
                    sprite,
                    Truncate(ResolveReceiverLabel(snapshot), ResolveMaxChars(ReceiverNameBounds.Width, 0.38f)),
                    Position.X + ReceiverNameBounds.X,
                    Position.Y + ReceiverNameBounds.Y,
                    new Color(24, 24, 24),
                    0.38f);
            }

            IReadOnlyList<string> lines = snapshot.IsShowingMessage ? snapshot.DisplayLines : snapshot.DraftLines;
            Rectangle messageBounds = ResolveMessageTextBounds(snapshot);
            int drawY = Position.Y + messageBounds.Y;
            for (int i = 0; i < lines.Count && i < 5; i++)
            {
                string line = string.IsNullOrWhiteSpace(lines[i])
                    ? string.Empty
                    : Truncate(lines[i], ResolveMaxChars(messageBounds.Width, 0.38f));
                DrawShadowText(sprite, line, Position.X + messageBounds.X, drawY, new Color(50, 50, 50), 0.38f);
                drawY += MessageLineHeight;
            }

            DrawPreview(sprite, skeletonMeshRenderer, drawReflectionInfo, gameTime, TickCount, snapshot);
        }

        internal void DrawWorldOverlay(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            ReflectionDrawableBoundary drawReflectionInfo,
            GameTime gameTime,
            int renderWidth,
            int tickCount)
        {
            MapleTvSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            if (_font == null || _visualAssets == null || (!snapshot.IsShowingMessage && !snapshot.QueueExists))
            {
                return;
            }

            RefreshAssemblers(snapshot);

            if (!snapshot.IsShowingMessage)
            {
                IReadOnlyList<MapleTvAnimationFrame> idleFrames = _visualAssets.OffFrames.Count > 0
                    ? _visualAssets.OffFrames
                    : _visualAssets.BasicFrames;
                MapleTvAnimationFrame idleFrame = SelectFrame(idleFrames, tickCount);
                Point idleOverlayOrigin = ResolveClientOwnedSurfaceOrigin(
                    renderWidth,
                    WorldOverlayTopMargin,
                    ResolveClientOwnedSurfaceBounds(MapleTvSurfaceWidth, MapleTvIdleSurfaceHeight, idleFrames));
                DrawAnimationFrame(sprite, idleFrame, idleOverlayOrigin, drawReflectionInfo, skeletonMeshRenderer, gameTime);
                return;
            }

            IReadOnlyList<MapleTvAnimationFrame> mediaFrames = _visualAssets.GetMediaFrames(snapshot.ResolvedMediaIndex);
            IReadOnlyList<MapleTvAnimationFrame> onFrames = _visualAssets.OnFrames.Count > 0
                ? _visualAssets.OnFrames
                : _visualAssets.BasicFrames;
            IReadOnlyList<MapleTvAnimationFrame> chatFrames = _visualAssets.GetChatFrames(snapshot.ResolvedMediaIndex);
            MapleTvAnimationFrame mediaFrame = SelectFrame(mediaFrames, tickCount);
            MapleTvAnimationFrame onFrame = SelectFrame(
                onFrames,
                tickCount);
            MapleTvAnimationFrame chatFrame = SelectFrame(chatFrames, tickCount);
            Point overlayOrigin = ResolveClientOwnedSurfaceOrigin(
                renderWidth,
                WorldOverlayTopMargin,
                ResolveClientOwnedSurfaceBounds(MapleTvSurfaceWidth, MapleTvMediaSurfaceHeight, mediaFrames));
            DrawAnimationFrame(sprite, mediaFrame, overlayOrigin, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawAnimationFrame(sprite, onFrame, overlayOrigin, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawAnimationFrame(sprite, chatFrame, overlayOrigin, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            DrawOverlayAvatars(
                sprite,
                skeletonMeshRenderer,
                tickCount,
                ResolveFamilyTopLeft(overlayOrigin, ResolveCompositeBounds(240, 180, mediaFrames)),
                snapshot);
            DrawChatText(
                sprite,
                snapshot.DisplayLines,
                ResolveFamilyTopLeft(overlayOrigin, ResolveCompositeBounds(240, 90, chatFrames)),
                ResolveChatBounds(snapshot.ResolvedMediaIndex),
                Color.White,
                0.39f,
                4,
                28);
        }

        private void ConfigureButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            if (action != null)
            {
                button.ButtonClickReleased += _ => action();
            }
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private void RefreshFrame(MapleTvSnapshot snapshot)
        {
            Frame = snapshot.UseReceiver ? _receiverFrame : _selfFrame;
        }

        private void UpdateButtonStates(MapleTvSnapshot snapshot)
        {
            if (_okButton != null)
            {
                _okButton.SetButtonState(snapshot.CanPublish ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_cancelButton != null)
            {
                _cancelButton.SetButtonState(snapshot.CanClear ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_receiverButton != null)
            {
                _receiverButton.SetButtonState(snapshot.CanToggleReceiver ? UIObjectState.Normal : UIObjectState.Disabled);
            }
        }

        private MapleTvSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new MapleTvSnapshot();
            return _currentSnapshot;
        }

        private void RefreshAssemblers(MapleTvSnapshot snapshot)
        {
            string senderBuildKey = CreateBuildKey(snapshot.SenderBuild);
            if (!string.Equals(_senderBuildKey, senderBuildKey, StringComparison.Ordinal))
            {
                _senderBuildKey = senderBuildKey;
                _senderAssembler = snapshot.SenderBuild != null ? new CharacterAssembler(snapshot.SenderBuild) : null;
            }

            string receiverBuildKey = CreateBuildKey(snapshot.ReceiverBuild);
            if (!string.Equals(_receiverBuildKey, receiverBuildKey, StringComparison.Ordinal))
            {
                _receiverBuildKey = receiverBuildKey;
                _receiverAssembler = snapshot.ReceiverBuild != null ? new CharacterAssembler(snapshot.ReceiverBuild) : null;
            }
        }

        private void DrawLayer(
            SpriteBatch sprite,
            IDXObject layer,
            Point offset,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            if (layer == null)
            {
                return;
            }

            layer.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + offset.X,
                Position.Y + offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private void DrawShadowText(SpriteBatch sprite, string text, float x, float y, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 position = new Vector2(x, y);
            ClientTextDrawing.DrawShadowed(sprite, text, position, color, _font, scale);
        }

        private void DrawPreview(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            ReflectionDrawableBoundary drawReflectionInfo,
            GameTime gameTime,
            int tickCount,
            MapleTvSnapshot snapshot)
        {
            if (_font == null || _visualAssets == null)
            {
                return;
            }

            Point previewOrigin = new(Position.X + PreviewAnchor.X, Position.Y + PreviewAnchor.Y);

            if (snapshot.IsShowingMessage)
            {
                IReadOnlyList<MapleTvAnimationFrame> mediaFrames = _visualAssets.GetMediaFrames(snapshot.ResolvedMediaIndex);
                IReadOnlyList<MapleTvAnimationFrame> onFrames = _visualAssets.OnFrames.Count > 0
                    ? _visualAssets.OnFrames
                    : _visualAssets.BasicFrames;
                IReadOnlyList<MapleTvAnimationFrame> chatFrames = _visualAssets.GetChatFrames(snapshot.ResolvedMediaIndex);
                MapleTvAnimationFrame mediaFrame = SelectFrame(
                    mediaFrames,
                    tickCount);
                DrawAnimationFrame(sprite, mediaFrame, previewOrigin, drawReflectionInfo, skeletonMeshRenderer, gameTime);
                MapleTvAnimationFrame onFrame = SelectFrame(
                    onFrames,
                    tickCount);
                DrawAnimationFrame(sprite, onFrame, previewOrigin, drawReflectionInfo, skeletonMeshRenderer, gameTime);
                MapleTvAnimationFrame chatFrame = SelectFrame(
                    chatFrames,
                    tickCount);
                DrawAnimationFrame(sprite, chatFrame, previewOrigin, drawReflectionInfo, skeletonMeshRenderer, gameTime);
                DrawPreviewAvatars(
                    sprite,
                    skeletonMeshRenderer,
                    tickCount,
                    ResolveFamilyTopLeft(previewOrigin, ResolveCompositeBounds(240, 180, mediaFrames)),
                    snapshot);
                DrawChatText(
                    sprite,
                    snapshot.DisplayLines,
                    ResolveFamilyTopLeft(previewOrigin, ResolveCompositeBounds(240, 90, chatFrames)),
                    ResolveChatBounds(snapshot.ResolvedMediaIndex),
                    Color.White,
                    0.4f,
                    4,
                    PreviewLineHeight);
            }
            else
            {
                IReadOnlyList<MapleTvAnimationFrame> idleFrames = snapshot.QueueExists
                    ? _visualAssets.OffFrames
                    : _visualAssets.BasicFrames;
                MapleTvAnimationFrame idleFrame = SelectFrame(idleFrames, tickCount);
                DrawAnimationFrame(
                    sprite,
                    idleFrame,
                    new Point(previewOrigin.X + IdlePreviewOffset.X, previewOrigin.Y + IdlePreviewOffset.Y),
                    drawReflectionInfo,
                    skeletonMeshRenderer,
                    gameTime);
            }
        }

        private void DrawPreviewAvatars(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            int tickCount,
            Point mediaTopLeft,
            MapleTvSnapshot snapshot)
        {
            AssembledFrame senderFrame = _senderAssembler?.GetFrameAtTime(PreviewActionName, tickCount);
            senderFrame?.Draw(sprite, skeletonMeshRenderer, mediaTopLeft.X + 70, mediaTopLeft.Y + 166, false, Color.White);

            if (!snapshot.UseReceiver)
            {
                return;
            }

            AssembledFrame receiverFrame = _receiverAssembler?.GetFrameAtTime(PreviewActionName, tickCount);
            receiverFrame?.Draw(sprite, skeletonMeshRenderer, mediaTopLeft.X + 170, mediaTopLeft.Y + 166, false, Color.White);
        }

        private void DrawOverlayAvatars(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            int tickCount,
            Point mediaTopLeft,
            MapleTvSnapshot snapshot)
        {
            AssembledFrame senderFrame = _senderAssembler?.GetFrameAtTime(PreviewActionName, tickCount);
            senderFrame?.Draw(sprite, skeletonMeshRenderer, mediaTopLeft.X + 44, mediaTopLeft.Y + 168, false, Color.White);

            if (!snapshot.UseReceiver)
            {
                return;
            }

            AssembledFrame receiverFrame = _receiverAssembler?.GetFrameAtTime(PreviewActionName, tickCount);
            receiverFrame?.Draw(sprite, skeletonMeshRenderer, mediaTopLeft.X + 186, mediaTopLeft.Y + 168, false, Color.White);
        }

        private static string CreateBuildKey(CharacterBuild build)
        {
            if (build == null)
            {
                return string.Empty;
            }

            IEnumerable<string> equipmentKeys = build.Equipment
                .OrderBy(kv => (int)kv.Key)
                .Select(kv => $"{(int)kv.Key}:{kv.Value?.ItemId ?? 0}");
            return string.Join(
                "|",
                build.Gender,
                build.Skin,
                build.Body?.ItemId ?? 0,
                build.Head?.ItemId ?? 0,
                build.Face?.ItemId ?? 0,
                build.Hair?.ItemId ?? 0,
                build.ActivePortableChair?.ItemId ?? 0,
                string.Join(",", equipmentKeys));
        }

        private static MapleTvAnimationFrame SelectFrame(IReadOnlyList<MapleTvAnimationFrame> frames, int tickCount)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            int cycleDuration = frames.Sum(frame => Math.Max(1, frame.DelayMs));
            if (cycleDuration <= 0)
            {
                return frames[0];
            }

            int animationTime = Math.Abs(tickCount % cycleDuration);
            int elapsed = 0;
            foreach (MapleTvAnimationFrame frame in frames)
            {
                elapsed += Math.Max(1, frame.DelayMs);
                if (animationTime < elapsed)
                {
                    return frame;
                }
            }

            return frames[^1];
        }

        private static void DrawAnimationFrame(
            SpriteBatch sprite,
            MapleTvAnimationFrame frame,
            Point origin,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            frame?.Drawable?.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                origin.X + frame.Offset.X,
                origin.Y + frame.Offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        internal static Point ResolveClientOwnedSurfaceOrigin(
            int renderWidth,
            int topMargin,
            Rectangle anchorSurfaceBounds)
        {
            Rectangle normalizedAnchorBounds = NormalizeBounds(anchorSurfaceBounds);
            int desiredLeft = Math.Max(0, (renderWidth - normalizedAnchorBounds.Width) / 2);
            int desiredTop = Math.Max(0, topMargin);
            return new Point(desiredLeft - normalizedAnchorBounds.Left, desiredTop - normalizedAnchorBounds.Top);
        }

        internal static Rectangle ResolveCompositeBounds(
            int fallbackWidth,
            int fallbackHeight,
            params IReadOnlyList<MapleTvAnimationFrame>[] frameGroups)
        {
            Rectangle bounds = new(0, 0, Math.Max(1, fallbackWidth), Math.Max(1, fallbackHeight));
            bool hasFrameBounds = false;
            if (frameGroups == null)
            {
                return bounds;
            }

            for (int groupIndex = 0; groupIndex < frameGroups.Length; groupIndex++)
            {
                IReadOnlyList<MapleTvAnimationFrame> frames = frameGroups[groupIndex];
                if (frames == null)
                {
                    continue;
                }

                for (int i = 0; i < frames.Count; i++)
                {
                    MapleTvAnimationFrame frame = frames[i];
                    if (frame == null)
                    {
                        continue;
                    }

                    Rectangle frameBounds = new(
                        frame.Offset.X,
                        frame.Offset.Y,
                        Math.Max(1, frame.Width),
                        Math.Max(1, frame.Height));
                    bounds = hasFrameBounds ? Rectangle.Union(bounds, frameBounds) : frameBounds;
                    hasFrameBounds = true;
                }
            }

            return bounds;
        }

        internal static Point ResolveFamilyTopLeft(Point origin, Rectangle familyBounds)
        {
            Rectangle normalizedBounds = NormalizeBounds(familyBounds);
            return new Point(origin.X + normalizedBounds.Left, origin.Y + normalizedBounds.Top);
        }

        internal static Rectangle CreateBaseSurfaceBounds(int width, int height)
        {
            return new Rectangle(0, -Math.Max(1, height), Math.Max(1, width), Math.Max(1, height));
        }

        internal static Rectangle ResolveClientOwnedSurfaceBounds(
            int fallbackWidth,
            int fallbackHeight,
            IReadOnlyList<MapleTvAnimationFrame> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return CreateBaseSurfaceBounds(fallbackWidth, fallbackHeight);
            }

            Rectangle familyBounds = ResolveCompositeBounds(fallbackWidth, fallbackHeight, frames);
            return new Rectangle(
                0,
                familyBounds.Top,
                Math.Max(1, fallbackWidth),
                Math.Max(1, familyBounds.Height));
        }

        private static Rectangle NormalizeBounds(Rectangle bounds)
        {
            return bounds.Width <= 0 || bounds.Height <= 0
                ? new Rectangle(bounds.Left, bounds.Top, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height))
                : bounds;
        }

        private void DrawChatText(
            SpriteBatch sprite,
            IReadOnlyList<string> lines,
            Point origin,
            Rectangle bounds,
            Color color,
            float scale,
            int maxLines,
            int lineHeight)
        {
            int drawY = origin.Y + bounds.Y;
            foreach (string line in lines.Take(maxLines))
            {
                string visibleLine = string.IsNullOrWhiteSpace(line) ? string.Empty : Truncate(line, ResolveMaxChars(bounds.Width, scale));
                DrawShadowText(sprite, visibleLine, origin.X + bounds.X, drawY, color, scale);
                drawY += lineHeight;
            }
        }

        private int ResolveMaxChars(int width, float scale)
        {
            if (_font == null || width <= 0)
            {
                return 24;
            }

            float glyphWidth = Math.Max(1f, ClientTextDrawing.Measure((GraphicsDevice)null, "W", scale, _font).X);
            return Math.Max(8, (int)(width / glyphWidth));
        }

        private static Rectangle ResolveChatBounds(int mediaIndex)
        {
            return mediaIndex switch
            {
                0 => StarChatTextBounds,
                2 => HeartChatTextBounds,
                _ => DefaultChatTextBounds
            };
        }

        private static Rectangle ResolveMessageTextBounds(MapleTvSnapshot snapshot)
        {
            return snapshot.MessageType == 1 ? SenderOnlyDraftMessageTextBounds : DraftMessageTextBounds;
        }

        private static string ResolveReceiverLabel(MapleTvSnapshot snapshot)
        {
            if (!snapshot.UseReceiver)
            {
                return "self";
            }

            return string.IsNullOrWhiteSpace(snapshot.ReceiverName) ? "(target pending)" : snapshot.ReceiverName;
        }

        private static string Truncate(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text ?? string.Empty;
            }

            return $"{text.Substring(0, Math.Max(0, maxChars - 3))}...";
        }
    }

    internal sealed class MapleTvAnimationFrame
    {
        internal MapleTvAnimationFrame(IDXObject drawable, Point offset, int delayMs, int width, int height)
        {
            Drawable = drawable;
            Offset = offset;
            DelayMs = delayMs;
            Width = width;
            Height = height;
        }

        internal IDXObject Drawable { get; }
        internal Point Offset { get; }
        internal int DelayMs { get; }
        internal int Width { get; }
        internal int Height { get; }
    }

    internal sealed class MapleTvVisualAssets
    {
        internal MapleTvVisualAssets(
            IReadOnlyList<MapleTvAnimationFrame> onFrames,
            IReadOnlyList<MapleTvAnimationFrame> basicFrames,
            IReadOnlyList<MapleTvAnimationFrame> offFrames,
            IReadOnlyDictionary<int, IReadOnlyList<MapleTvAnimationFrame>> chatFrames,
            IReadOnlyDictionary<int, IReadOnlyList<MapleTvAnimationFrame>> mediaFrames,
            int defaultMediaIndex)
        {
            OnFrames = onFrames ?? Array.Empty<MapleTvAnimationFrame>();
            BasicFrames = basicFrames ?? Array.Empty<MapleTvAnimationFrame>();
            OffFrames = offFrames ?? Array.Empty<MapleTvAnimationFrame>();
            ChatFrames = chatFrames ?? new Dictionary<int, IReadOnlyList<MapleTvAnimationFrame>>();
            MediaFrames = mediaFrames ?? new Dictionary<int, IReadOnlyList<MapleTvAnimationFrame>>();
            DefaultMediaIndex = defaultMediaIndex;
        }

        internal IReadOnlyList<MapleTvAnimationFrame> OnFrames { get; }
        internal IReadOnlyList<MapleTvAnimationFrame> BasicFrames { get; }
        internal IReadOnlyList<MapleTvAnimationFrame> OffFrames { get; }
        internal IReadOnlyDictionary<int, IReadOnlyList<MapleTvAnimationFrame>> ChatFrames { get; }
        internal IReadOnlyDictionary<int, IReadOnlyList<MapleTvAnimationFrame>> MediaFrames { get; }
        internal int DefaultMediaIndex { get; }

        internal IReadOnlyList<MapleTvAnimationFrame> GetChatFrames(int mediaIndex)
        {
            if (ChatFrames.TryGetValue(mediaIndex, out IReadOnlyList<MapleTvAnimationFrame> frames) && frames.Count > 0)
            {
                return frames;
            }

            if (ChatFrames.TryGetValue(DefaultMediaIndex, out frames) && frames.Count > 0)
            {
                return frames;
            }

            return Array.Empty<MapleTvAnimationFrame>();
        }

        internal IReadOnlyList<MapleTvAnimationFrame> GetMediaFrames(int mediaIndex)
        {
            if (MediaFrames.TryGetValue(mediaIndex, out IReadOnlyList<MapleTvAnimationFrame> frames) && frames.Count > 0)
            {
                return frames;
            }

            if (MediaFrames.TryGetValue(DefaultMediaIndex, out frames) && frames.Count > 0)
            {
                return frames;
            }

            return MediaFrames.Values.FirstOrDefault(value => value?.Count > 0) ?? Array.Empty<MapleTvAnimationFrame>();
        }
    }
}
