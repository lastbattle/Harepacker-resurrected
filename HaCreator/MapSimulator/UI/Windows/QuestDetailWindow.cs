using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public sealed class QuestDetailWindow : UIWindowBase
    {
        private readonly string _windowName;
        private readonly List<ButtonLabel> _buttonLabels = new();
        private readonly Dictionary<QuestWindowActionKind, ActionButtonBinding> _actionButtons = new();
        private readonly Dictionary<QuestDetailNpcButtonStyle, ActionButtonBinding> _npcButtons = new();

        private SpriteFont _font;
        private IDXObject _foreground;
        private Point _foregroundOffset;
        private IDXObject _bottomPanel;
        private Point _bottomPanelOffset;
        private UIObject _previousButton;
        private UIObject _nextButton;
        private QuestWindowDetailState _state;
        private int _navigationIndex = -1;
        private int _navigationCount;
        private UIObject _activePrimaryButton;
        private UIObject _activeSecondaryButton;
        private UIObject _activeTertiaryButton;
        private UIObject _activeQuaternaryButton;
        private bool _drawPrimaryLabel = true;
        private bool _drawSecondaryLabel = true;
        private bool _drawTertiaryLabel = true;
        private bool _drawQuaternaryLabel = true;
        private Texture2D _summaryHeaderTexture;
        private Texture2D _requirementHeaderTexture;
        private Texture2D _rewardHeaderTexture;
        private Texture2D _selectionBarTexture;
        private Texture2D _incompleteSelectionBarTexture;
        private Texture2D _progressFrameTexture;
        private Texture2D _progressGaugeTexture;
        private Texture2D _progressSpotTexture;
        private Point _progressFrameOffset;
        private Func<int, Texture2D> _itemIconProvider;
        private readonly Dictionary<int, Texture2D> _itemIconCache = new();
        private Texture2D _pixel;
        private HoveredQuestItemInfo _hoveredQuestItem;
        private Point _lastMousePosition;
        private NoticeSurface[] _noticeSurfaces = Array.Empty<NoticeSurface>();
        private NoticeAnimationFrame[] _noticeAnimationFrames = Array.Empty<NoticeAnimationFrame>();
        private Point _noticeAnimationOffset;

        public QuestDetailWindow(IDXObject frame, string windowName)
            : base(frame)
        {
            _windowName = windowName;
        }

        public override string WindowName => _windowName;

        internal event Action PreviousRequested;
        internal event Action NextRequested;
        internal event Action<QuestWindowActionKind> ActionRequested;

        public void SetForeground(IDXObject foreground, Point offset)
        {
            _foreground = foreground;
            _foregroundOffset = offset;
        }

        public void SetBottomPanel(IDXObject panel, Point offset)
        {
            _bottomPanel = panel;
            _bottomPanelOffset = offset;
        }

        public void SetSectionTextures(
            Texture2D summaryHeaderTexture,
            Texture2D requirementHeaderTexture,
            Texture2D rewardHeaderTexture,
            Texture2D selectionBarTexture,
            Texture2D incompleteSelectionBarTexture)
        {
            _summaryHeaderTexture = summaryHeaderTexture;
            _requirementHeaderTexture = requirementHeaderTexture;
            _rewardHeaderTexture = rewardHeaderTexture;
            _selectionBarTexture = selectionBarTexture;
            _incompleteSelectionBarTexture = incompleteSelectionBarTexture;
        }

        public void SetProgressTextures(Texture2D frameTexture, Texture2D gaugeTexture, Texture2D spotTexture, Point frameOffset)
        {
            _progressFrameTexture = frameTexture;
            _progressGaugeTexture = gaugeTexture;
            _progressSpotTexture = spotTexture;
            _progressFrameOffset = frameOffset;
        }

        public void SetNoticeTextures(Texture2D[] surfaces, Point[] surfaceOffsets, Texture2D[] animationFrames, int[] animationDelays, Point animationOffset)
        {
            if (surfaces != null && surfaceOffsets != null && surfaces.Length == surfaceOffsets.Length)
            {
                _noticeSurfaces = new NoticeSurface[surfaces.Length];
                for (int i = 0; i < surfaces.Length; i++)
                {
                    _noticeSurfaces[i] = new NoticeSurface(surfaces[i], surfaceOffsets[i]);
                }
            }
            else
            {
                _noticeSurfaces = Array.Empty<NoticeSurface>();
            }

            if (animationFrames != null && animationDelays != null && animationFrames.Length == animationDelays.Length)
            {
                _noticeAnimationFrames = new NoticeAnimationFrame[animationFrames.Length];
                for (int i = 0; i < animationFrames.Length; i++)
                {
                    _noticeAnimationFrames[i] = new NoticeAnimationFrame(animationFrames[i], Math.Max(1, animationDelays[i]));
                }
            }
            else
            {
                _noticeAnimationFrames = Array.Empty<NoticeAnimationFrame>();
            }

            _noticeAnimationOffset = animationOffset;
        }

        public void SetItemIconProvider(Func<int, Texture2D> itemIconProvider)
        {
            _itemIconProvider = itemIconProvider;
        }

        internal void RegisterActionButton(QuestWindowActionKind action, UIObject button, bool drawLabel = false)
        {
            if (action == QuestWindowActionKind.None || button == null)
            {
                return;
            }

            button.SetVisible(false);
            AddButton(button);
            button.ButtonClickReleased += _ =>
            {
                if (_state == null)
                {
                    return;
                }

                if (_state.PrimaryAction == action ||
                    _state.SecondaryAction == action ||
                    _state.QuaternaryAction == action)
                {
                    ActionRequested?.Invoke(action);
                }
            };

            _actionButtons[action] = new ActionButtonBinding(button, drawLabel);
        }

        internal void RegisterNpcButton(QuestDetailNpcButtonStyle style, UIObject button, bool drawLabel = false)
        {
            if (style == QuestDetailNpcButtonStyle.None || button == null)
            {
                return;
            }

            button.SetVisible(false);
            AddButton(button);
            button.ButtonClickReleased += _ =>
            {
                if (_state?.TertiaryAction == QuestWindowActionKind.LocateNpc && _state.TertiaryActionEnabled)
                {
                    ActionRequested?.Invoke(QuestWindowActionKind.LocateNpc);
                }
            };

            _npcButtons[style] = new ActionButtonBinding(button, drawLabel);
        }

        public void InitializeNavigationButtons(GraphicsDevice device)
        {
            _pixel ??= new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _previousButton = UiButtonFactory.CreateSolidButton(
                device, 48, 18,
                new Color(48, 61, 77, 220),
                new Color(74, 96, 118, 240),
                new Color(63, 80, 98, 235),
                new Color(28, 28, 28, 170));
            _previousButton.X = 16;
            _previousButton.Y = Math.Max(16, (CurrentFrame?.Height ?? 396) - 28);
            _previousButton.ButtonClickReleased += _ => PreviousRequested?.Invoke();
            AddButton(_previousButton);
            _buttonLabels.Add(new ButtonLabel(_previousButton, "Prev"));

            _nextButton = UiButtonFactory.CreateSolidButton(
                device, 48, 18,
                new Color(48, 61, 77, 220),
                new Color(74, 96, 118, 240),
                new Color(63, 80, 98, 235),
                new Color(28, 28, 28, 170));
            _nextButton.X = 70;
            _nextButton.Y = _previousButton.Y;
            _nextButton.ButtonClickReleased += _ => NextRequested?.Invoke();
            AddButton(_nextButton);
            _buttonLabels.Add(new ButtonLabel(_nextButton, "Next"));
        }

        internal void SetDetailState(QuestWindowDetailState state, int navigationIndex, int navigationCount)
        {
            _state = state;
            _navigationIndex = navigationIndex;
            _navigationCount = navigationCount;
            _activePrimaryButton = null;
            _activeSecondaryButton = null;
            _activeTertiaryButton = null;
            _activeQuaternaryButton = null;
            _drawPrimaryLabel = true;
            _drawSecondaryLabel = true;
            _drawTertiaryLabel = true;
            _drawQuaternaryLabel = true;

            foreach (ActionButtonBinding binding in _actionButtons.Values)
            {
                binding.Button.SetVisible(false);
            }

            foreach (ActionButtonBinding binding in _npcButtons.Values)
            {
                binding.Button.SetVisible(false);
            }

            if (state != null && state.PrimaryAction != QuestWindowActionKind.None &&
                _actionButtons.TryGetValue(state.PrimaryAction, out ActionButtonBinding primaryBinding))
            {
                primaryBinding.Button.SetVisible(true);
                primaryBinding.Button.SetButtonState(state.PrimaryActionEnabled ? UIObjectState.Normal : UIObjectState.Disabled);
                _activePrimaryButton = primaryBinding.Button;
                _drawPrimaryLabel = primaryBinding.DrawLabel;
            }

            if (state != null && state.SecondaryAction != QuestWindowActionKind.None &&
                _actionButtons.TryGetValue(state.SecondaryAction, out ActionButtonBinding secondaryBinding))
            {
                secondaryBinding.Button.SetVisible(true);
                secondaryBinding.Button.SetButtonState(state.SecondaryActionEnabled ? UIObjectState.Normal : UIObjectState.Disabled);
                _activeSecondaryButton = secondaryBinding.Button;
                _drawSecondaryLabel = secondaryBinding.DrawLabel;
            }

            if (state != null)
            {
                BindNpcButton(state);
                BindQuaternaryActionButton(state);
                LayoutActionButtons();
            }

            if (_previousButton != null)
            {
                bool enabled = navigationCount > 1 && navigationIndex > 0;
                _previousButton.SetVisible(navigationCount > 1);
                _previousButton.SetButtonState(enabled ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_nextButton != null)
            {
                bool enabled = navigationCount > 1 && navigationIndex >= 0 && navigationIndex < navigationCount - 1;
                _nextButton.SetVisible(navigationCount > 1);
                _nextButton.SetButtonState(enabled ? UIObjectState.Normal : UIObjectState.Disabled);
            }
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_font == null || _state == null || !IsVisible)
            {
                _hoveredQuestItem = null;
                return;
            }

            Microsoft.Xna.Framework.Input.MouseState mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            _hoveredQuestItem = ResolveHoveredQuestItem(mouseState.X, mouseState.Y);
        }

        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    Position.X + _foregroundOffset.X, Position.Y + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            if (_bottomPanel != null)
            {
                _bottomPanel.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    Position.X + _bottomPanelOffset.X, Position.Y + _bottomPanelOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            if (_font == null)
            {
                return;
            }

            if (_state == null)
            {
                sprite.DrawString(_font, "Select a quest to inspect its details.", new Vector2(Position.X + 16, Position.Y + 22), new Color(220, 220, 220));
                return;
            }

            float x = Position.X + 16;
            float y = Position.Y + 20;

            sprite.DrawString(_font, _state.Title, new Vector2(x, y), Color.White);
            y += _font.LineSpacing + 8;

            if (!string.IsNullOrWhiteSpace(_state.NpcText))
            {
                sprite.DrawString(_font, _state.NpcText, new Vector2(x, y), new Color(214, 214, 171));
                y += _font.LineSpacing + 6;
            }

            y = DrawNoticeSurface(sprite, y, TickCount);

            DrawSummarySection(sprite, ref y, x, 258f);
            DrawRequirementSection(sprite, ref y, x, 258f);
            DrawRewardSection(sprite, ref y, x, 258f);

            if (!string.IsNullOrWhiteSpace(_state.HintText))
            {
                DrawWrappedText(sprite, _state.HintText, new Vector2(x, y), 258f, new Color(243, 227, 168));
            }
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_font == null)
            {
                return;
            }

            foreach (ButtonLabel label in _buttonLabels)
            {
                if (!label.Button.ButtonVisible)
                {
                    continue;
                }

                DrawCenteredButtonLabel(sprite, label.Button, label.Text);
            }

            if (_state == null)
            {
                return;
            }

            if (_activePrimaryButton?.ButtonVisible == true && _drawPrimaryLabel)
            {
                DrawCenteredButtonLabel(sprite, _activePrimaryButton, _state.PrimaryActionLabel);
            }

            if (_activeSecondaryButton?.ButtonVisible == true && _drawSecondaryLabel)
            {
                DrawCenteredButtonLabel(sprite, _activeSecondaryButton, _state.SecondaryActionLabel);
            }

            if (_activeTertiaryButton?.ButtonVisible == true && _drawTertiaryLabel)
            {
                DrawCenteredButtonLabel(sprite, _activeTertiaryButton, _state.TertiaryActionLabel);
            }

            if (_activeQuaternaryButton?.ButtonVisible == true && _drawQuaternaryLabel)
            {
                DrawCenteredButtonLabel(sprite, _activeQuaternaryButton, _state.QuaternaryActionLabel);
            }

            if (_navigationCount > 1)
            {
                string navigationText = $"{_navigationIndex + 1}/{_navigationCount}";
                sprite.DrawString(_font, navigationText, new Vector2(Position.X + 126, Position.Y + Math.Max(16, (CurrentFrame?.Height ?? 396) - 27)), new Color(220, 220, 220));
            }

            DrawHoveredItemTooltip(sprite);
        }

        private void DrawSummarySection(SpriteBatch sprite, ref float y, float x, float maxWidth)
        {
            DrawSectionHeader(sprite, _summaryHeaderTexture, "Summary", x, ref y);
            y = DrawWrappedText(sprite, _state.SummaryText, new Vector2(x, y), maxWidth, new Color(228, 228, 228));
            y += 8;
            DrawProgress(sprite, x, ref y);
        }

        private void DrawRequirementSection(SpriteBatch sprite, ref float y, float x, float maxWidth)
        {
            if (!HasRequirementContent())
            {
                return;
            }

            DrawSectionHeader(sprite, _requirementHeaderTexture, "Requirements", x, ref y);
            if (_state.RequirementLines != null && _state.RequirementLines.Count > 0)
            {
                y = DrawConditionLines(sprite, _state.RequirementLines, x, y, maxWidth, false);
            }
            else
            {
                y = DrawWrappedText(sprite, _state.RequirementText, new Vector2(x, y), maxWidth, new Color(215, 228, 215));
            }

            y += 8;
        }

        private void DrawRewardSection(SpriteBatch sprite, ref float y, float x, float maxWidth)
        {
            if (!HasRewardContent())
            {
                return;
            }

            DrawSectionHeader(sprite, _rewardHeaderTexture, "Rewards", x, ref y);
            if (_state.RewardLines != null && _state.RewardLines.Count > 0)
            {
                y = DrawConditionLines(sprite, _state.RewardLines, x, y, maxWidth, true);
            }
            else
            {
                y = DrawWrappedText(sprite, _state.RewardText, new Vector2(x, y), maxWidth, new Color(232, 220, 176));
            }

            y += 8;
        }

        private bool HasRequirementContent()
        {
            return !string.IsNullOrWhiteSpace(_state.RequirementText) ||
                   (_state.RequirementLines != null && _state.RequirementLines.Count > 0);
        }

        private bool HasRewardContent()
        {
            return !string.IsNullOrWhiteSpace(_state.RewardText) ||
                   (_state.RewardLines != null && _state.RewardLines.Count > 0);
        }

        private void DrawSectionHeader(SpriteBatch sprite, Texture2D texture, string fallbackText, float x, ref float y)
        {
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(x, y), Color.White);
                y += texture.Height + 4;
                return;
            }

            sprite.DrawString(_font, fallbackText, new Vector2(x, y), new Color(255, 232, 166));
            y += _font.LineSpacing;
        }

        private void DrawProgress(SpriteBatch sprite, float x, ref float y)
        {
            if (_state.TotalProgress <= 0)
            {
                return;
            }

            string progressText = $"Progress: {Math.Min(_state.CurrentProgress, _state.TotalProgress)}/{_state.TotalProgress}";
            sprite.DrawString(_font, progressText, new Vector2(x, y), new Color(196, 218, 255));
            y += _font.LineSpacing + 3;

            if (_progressFrameTexture == null || _progressGaugeTexture == null)
            {
                return;
            }

            Vector2 framePosition = new(Position.X + _progressFrameOffset.X, y);
            sprite.Draw(_progressFrameTexture, framePosition, Color.White);

            float ratio = MathHelper.Clamp(_state.TotalProgress > 0
                ? (float)_state.CurrentProgress / _state.TotalProgress
                : 0f, 0f, 1f);
            int fillWidth = Math.Max(0, (int)Math.Round(ratio * (_progressFrameTexture.Width - 2)));
            if (fillWidth > 0)
            {
                Rectangle destination = new(
                    (int)framePosition.X + 1,
                    (int)framePosition.Y + 1,
                    fillWidth,
                    Math.Max(1, _progressFrameTexture.Height - 2));
                sprite.Draw(_progressGaugeTexture, destination, Color.White);

                if (_progressSpotTexture != null)
                {
                    sprite.Draw(
                        _progressSpotTexture,
                        new Vector2(destination.X + Math.Max(0, destination.Width - _progressSpotTexture.Width), destination.Y),
                        Color.White);
                }
            }

            y += _progressFrameTexture.Height + 8;
        }

        private float DrawConditionLines(SpriteBatch sprite, IReadOnlyList<QuestLogLineSnapshot> lines, float x, float y, float maxWidth, bool rewardSection)
        {
            if (lines == null || lines.Count == 0)
            {
                return y;
            }

            const float labelWidth = 38f;
            const float iconSize = 18f;

            foreach (QuestLogLineSnapshot line in lines.Where(line => line != null))
            {
                Texture2D rowTexture = !rewardSection && !line.IsComplete
                    ? _incompleteSelectionBarTexture ?? _selectionBarTexture
                    : _selectionBarTexture;
                if (rowTexture != null)
                {
                    sprite.Draw(rowTexture, new Rectangle((int)x, (int)y, Math.Min((int)maxWidth, rowTexture.Width), rowTexture.Height), Color.White);
                }

                Color labelColor = rewardSection
                    ? new Color(255, 226, 157)
                    : (line.IsComplete ? new Color(168, 224, 173) : new Color(255, 190, 137));
                Color textColor = rewardSection
                    ? new Color(244, 234, 198)
                    : (line.IsComplete ? new Color(219, 239, 219) : new Color(255, 218, 189));

                sprite.DrawString(_font, line.Label ?? string.Empty, new Vector2(x, y), labelColor);

                float lineX = x + labelWidth + 6f;
                Texture2D iconTexture = line.ItemId.HasValue && _itemIconProvider != null
                    ? _itemIconProvider(line.ItemId.Value)
                    : null;
                if (iconTexture != null)
                {
                    sprite.Draw(iconTexture, new Rectangle((int)lineX, (int)y, (int)iconSize, (int)iconSize), Color.White);
                    lineX += iconSize + 4f;
                }

                y = DrawWrappedText(sprite, line.Text, new Vector2(lineX, y), Math.Max(48f, maxWidth - (lineX - x)), textColor);
                y += 4f;
            }

            return y;
        }

        private float DrawWrappedText(SpriteBatch sprite, string text, Vector2 position, float maxWidth, Color color)
        {
            float y = position.Y;
            foreach (string line in WrapText(text, maxWidth))
            {
                sprite.DrawString(_font, line, new Vector2(position.X, y), color);
                y += _font.LineSpacing;
            }

            return y;
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            foreach (string block in text.Replace("\r", string.Empty).Split('\n'))
            {
                string[] words = block.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    yield return string.Empty;
                    continue;
                }

                string currentLine = string.Empty;
                for (int i = 0; i < words.Length; i++)
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? words[i] : $"{currentLine} {words[i]}";
                    if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                    {
                        yield return currentLine;
                        currentLine = words[i];
                    }
                    else
                    {
                        currentLine = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    yield return currentLine;
                }
            }
        }

        private HoveredQuestItemInfo ResolveHoveredQuestItem(int mouseX, int mouseY)
        {
            if (_state == null || !ContainsPoint(mouseX, mouseY))
            {
                return null;
            }

            float x = Position.X + 16;
            float y = Position.Y + 20 + _font.LineSpacing + 8;

            if (!string.IsNullOrWhiteSpace(_state.NpcText))
            {
                y += _font.LineSpacing + 6;
            }

            y = AdvanceNoticeLayout(y);
            y = AdvanceSummaryLayout(x, y, 258f);

            HoveredQuestItemInfo hovered = TryResolveHoveredConditionItem(mouseX, mouseY, _state.RequirementLines, x, ref y, 258f, false);
            if (hovered != null)
            {
                return hovered;
            }

            return TryResolveHoveredConditionItem(mouseX, mouseY, _state.RewardLines, x, ref y, 258f, true);
        }

        private float AdvanceSummaryLayout(float x, float y, float maxWidth)
        {
            y = AdvanceSectionHeader(_summaryHeaderTexture, y);
            y = AdvanceWrappedText(_state.SummaryText, maxWidth, y);
            y += 8;

            if (_state.TotalProgress > 0)
            {
                y += _font.LineSpacing + 3;
                if (_progressFrameTexture != null)
                {
                    y += _progressFrameTexture.Height + 8;
                }
            }

            return y;
        }

        private float DrawNoticeSurface(SpriteBatch sprite, float y, int tickCount)
        {
            NoticeSurface? surface = GetActiveNoticeSurface();
            if (!surface.HasValue || surface.Value.Texture == null)
            {
                return y;
            }

            Vector2 surfacePosition = new(Position.X + surface.Value.Offset.X, Position.Y + surface.Value.Offset.Y);
            sprite.Draw(surface.Value.Texture, surfacePosition, Color.White);

            NoticeAnimationFrame? animationFrame = GetActiveNoticeAnimationFrame(tickCount);
            if (animationFrame.HasValue && animationFrame.Value.Texture != null)
            {
                sprite.Draw(
                    animationFrame.Value.Texture,
                    new Vector2(Position.X + _noticeAnimationOffset.X, Position.Y + _noticeAnimationOffset.Y),
                    Color.White);
            }

            return Math.Max(y, surfacePosition.Y + surface.Value.Texture.Height + 6f);
        }

        private float AdvanceNoticeLayout(float y)
        {
            NoticeSurface? surface = GetActiveNoticeSurface();
            if (!surface.HasValue || surface.Value.Texture == null)
            {
                return y;
            }

            return Math.Max(y, Position.Y + surface.Value.Offset.Y + surface.Value.Texture.Height + 6f);
        }

        private HoveredQuestItemInfo TryResolveHoveredConditionItem(int mouseX, int mouseY, IReadOnlyList<QuestLogLineSnapshot> lines, float x, ref float y, float maxWidth, bool rewardSection)
        {
            if (lines == null || lines.Count == 0)
            {
                return null;
            }

            y = AdvanceSectionHeader(rewardSection ? _rewardHeaderTexture : _requirementHeaderTexture, y);

            const float labelWidth = 38f;
            const float iconSize = 18f;
            foreach (QuestLogLineSnapshot line in lines.Where(line => line != null))
            {
                float lineX = x + labelWidth + 6f;
                if (line.ItemId.HasValue)
                {
                    Rectangle iconRect = new Rectangle((int)lineX, (int)y, (int)iconSize, (int)iconSize);
                    if (iconRect.Contains(mouseX, mouseY))
                    {
                        return CreateHoveredQuestItem(line.ItemId.Value, line.Text);
                    }

                    lineX += iconSize + 4f;
                }

                y = AdvanceWrappedText(line.Text, Math.Max(48f, maxWidth - (lineX - x)), y);
                y += 4f;
            }

            y += 8f;
            return null;
        }

        private float AdvanceSectionHeader(Texture2D texture, float y)
        {
            return y + ((texture?.Height ?? _font.LineSpacing) + 4f);
        }

        private float AdvanceWrappedText(string text, float maxWidth, float y)
        {
            int lineCount = Math.Max(1, WrapText(text, maxWidth).Count());
            return y + (lineCount * _font.LineSpacing);
        }

        private HoveredQuestItemInfo CreateHoveredQuestItem(int itemId, string lineText)
        {
            return new HoveredQuestItemInfo
            {
                ItemId = itemId,
                Title = ResolveItemName(itemId),
                Subtitle = lineText,
                Description = ResolveItemDescription(itemId),
                Icon = ResolveItemIcon(itemId)
            };
        }

        private Texture2D ResolveItemIcon(int itemId)
        {
            if (itemId <= 0)
            {
                return null;
            }

            Texture2D providerTexture = _itemIconProvider?.Invoke(itemId);
            if (providerTexture != null)
            {
                return providerTexture;
            }

            if (_itemIconCache.TryGetValue(itemId, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            if (!InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                _itemIconCache[itemId] = null;
                return null;
            }

            MapleLib.WzLib.WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            itemImage?.ParseImage();
            string itemText = category == "Character" ? itemId.ToString("D8") : itemId.ToString("D7");
            MapleLib.WzLib.WzProperties.WzSubProperty itemProperty = itemImage?[itemText] as MapleLib.WzLib.WzProperties.WzSubProperty;
            MapleLib.WzLib.WzProperties.WzSubProperty infoProperty = itemProperty?["info"] as MapleLib.WzLib.WzProperties.WzSubProperty;
            MapleLib.WzLib.WzProperties.WzCanvasProperty iconCanvas = infoProperty?["iconRaw"] as MapleLib.WzLib.WzProperties.WzCanvasProperty
                                                                      ?? infoProperty?["icon"] as MapleLib.WzLib.WzProperties.WzCanvasProperty;
            Texture2D texture = iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_pixel?.GraphicsDevice);
            _itemIconCache[itemId] = texture;
            return texture;
        }

        private static string ResolveItemName(int itemId)
        {
            if (itemId <= 0)
            {
                return string.Empty;
            }

            return InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName)
                ? itemName
                : $"Item #{itemId}";
        }

        private static string ResolveItemDescription(int itemId)
        {
            return itemId > 0 && InventoryItemMetadataResolver.TryResolveItemDescription(itemId, out string description)
                ? description
                : string.Empty;
        }

        private void DrawHoveredItemTooltip(SpriteBatch sprite)
        {
            if (_hoveredQuestItem == null || _font == null || _pixel == null)
            {
                return;
            }

            string title = string.IsNullOrWhiteSpace(_hoveredQuestItem.Title) ? $"Item #{_hoveredQuestItem.ItemId}" : _hoveredQuestItem.Title;
            const int tooltipWidth = 220;
            const int padding = 8;
            const int iconSize = 28;
            const int gap = 8;
            float titleWidth = tooltipWidth - (padding * 2);
            float bodyWidth = tooltipWidth - ((padding * 2) + iconSize + gap);

            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            string[] wrappedSubtitle = WrapTooltipText(_hoveredQuestItem.Subtitle, bodyWidth);
            string[] wrappedDescription = WrapTooltipText(_hoveredQuestItem.Description, bodyWidth);

            float titleHeight = wrappedTitle.Length * _font.LineSpacing;
            float subtitleHeight = wrappedSubtitle.Length * _font.LineSpacing;
            float descriptionHeight = wrappedDescription.Length * _font.LineSpacing;
            float bodyHeight = subtitleHeight + (descriptionHeight > 0f ? 4f + descriptionHeight : 0f);
            int tooltipHeight = (int)Math.Ceiling((padding * 2) + titleHeight + 6f + Math.Max(iconSize, bodyHeight));

            int viewportWidth = sprite.GraphicsDevice.Viewport.Width;
            int viewportHeight = sprite.GraphicsDevice.Viewport.Height;
            int tooltipX = _lastMousePosition.X + 18;
            int tooltipY = _lastMousePosition.Y + 18;
            if (tooltipX + tooltipWidth > viewportWidth - 4)
            {
                tooltipX = Math.Max(4, _lastMousePosition.X - tooltipWidth - 18);
            }

            if (tooltipY + tooltipHeight > viewportHeight - 4)
            {
                tooltipY = Math.Max(4, _lastMousePosition.Y - tooltipHeight - 18);
            }

            Rectangle backgroundRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
            sprite.Draw(_pixel, backgroundRect, new Color(18, 24, 37, 235));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.X, backgroundRect.Y, backgroundRect.Width, 1), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.X, backgroundRect.Bottom - 1, backgroundRect.Width, 1), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.X, backgroundRect.Y, 1, backgroundRect.Height), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.Right - 1, backgroundRect.Y, 1, backgroundRect.Height), new Color(112, 146, 201));

            float textY = tooltipY + padding;
            DrawTooltipLines(sprite, wrappedTitle, new Vector2(tooltipX + padding, textY), new Color(255, 220, 120));
            textY += titleHeight + 6f;

            if (_hoveredQuestItem.Icon != null)
            {
                sprite.Draw(_hoveredQuestItem.Icon, new Rectangle(tooltipX + padding, (int)textY, iconSize, iconSize), Color.White);
            }

            float bodyX = tooltipX + padding + iconSize + gap;
            DrawTooltipLines(sprite, wrappedSubtitle, new Vector2(bodyX, textY), new Color(228, 233, 242));
            if (descriptionHeight > 0f)
            {
                DrawTooltipLines(sprite, wrappedDescription, new Vector2(bodyX, textY + subtitleHeight + 4f), new Color(199, 206, 218));
            }
        }

        private void DrawTooltipLines(SpriteBatch sprite, IReadOnlyList<string> lines, Vector2 position, Color color)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                sprite.DrawString(_font, lines[i], new Vector2(position.X, position.Y + (i * _font.LineSpacing)), color);
            }
        }

        private string[] WrapTooltipText(string text, float maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            return WrapText(text, maxWidth).ToArray();
        }

        private void DrawCenteredButtonLabel(SpriteBatch sprite, UIObject button, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            int width = Math.Max(1, button.CanvasSnapshotWidth);
            int height = Math.Max(1, button.CanvasSnapshotHeight);
            Vector2 textSize = _font.MeasureString(text);
            float x = Position.X + button.X + ((width - textSize.X) / 2f);
            float y = Position.Y + button.Y + ((height - textSize.Y) / 2f) - 1f;
            sprite.DrawString(_font, text, new Vector2(x, y), Color.White);
        }

        private void BindNpcButton(QuestWindowDetailState state)
        {
            if (state == null || state.TertiaryAction != QuestWindowActionKind.LocateNpc)
            {
                return;
            }

            ActionButtonBinding? npcBinding = null;
            if (state.NpcButtonStyle != QuestDetailNpcButtonStyle.None &&
                _npcButtons.TryGetValue(state.NpcButtonStyle, out ActionButtonBinding styledBinding))
            {
                npcBinding = styledBinding;
            }
            else if (_npcButtons.TryGetValue(QuestDetailNpcButtonStyle.GenericNpc, out ActionButtonBinding genericBinding))
            {
                npcBinding = genericBinding;
            }
            else if (_npcButtons.Count > 0)
            {
                npcBinding = _npcButtons.Values.First();
            }

            if (!npcBinding.HasValue)
            {
                return;
            }

            npcBinding.Value.Button.SetVisible(true);
            npcBinding.Value.Button.SetButtonState(state.TertiaryActionEnabled ? UIObjectState.Normal : UIObjectState.Disabled);
            _activeTertiaryButton = npcBinding.Value.Button;
            _drawTertiaryLabel = npcBinding.Value.DrawLabel;
        }

        private void BindQuaternaryActionButton(QuestWindowDetailState state)
        {
            if (state == null || state.QuaternaryAction == QuestWindowActionKind.None)
            {
                return;
            }

            if (!_actionButtons.TryGetValue(state.QuaternaryAction, out ActionButtonBinding binding))
            {
                return;
            }

            binding.Button.SetVisible(true);
            binding.Button.SetButtonState(state.QuaternaryActionEnabled ? UIObjectState.Normal : UIObjectState.Disabled);
            _activeQuaternaryButton = binding.Button;
            _drawQuaternaryLabel = binding.DrawLabel;
        }

        private void LayoutActionButtons()
        {
            List<UIObject> orderedButtons = new();
            AppendDistinctVisibleButton(orderedButtons, _activeQuaternaryButton);
            AppendDistinctVisibleButton(orderedButtons, _activeTertiaryButton);
            AppendDistinctVisibleButton(orderedButtons, _activeSecondaryButton);
            AppendDistinctVisibleButton(orderedButtons, _activePrimaryButton);

            if (orderedButtons.Count == 0)
            {
                return;
            }

            int frameWidth = CurrentFrame?.Width ?? 296;
            int frameHeight = CurrentFrame?.Height ?? 396;
            int cursorX = frameWidth - 12;

            for (int i = orderedButtons.Count - 1; i >= 0; i--)
            {
                UIObject button = orderedButtons[i];
                int buttonWidth = Math.Max(1, button.CanvasSnapshotWidth);
                int buttonHeight = Math.Max(1, button.CanvasSnapshotHeight);
                button.X = Math.Max(12, cursorX - buttonWidth);
                button.Y = Math.Max(16, frameHeight - buttonHeight - 10);
                cursorX = button.X - 8;
            }
        }

        private static void AppendDistinctVisibleButton(ICollection<UIObject> buttons, UIObject button)
        {
            if (button == null || !button.ButtonVisible || buttons.Contains(button))
            {
                return;
            }

            buttons.Add(button);
        }

        private NoticeSurface? GetActiveNoticeSurface()
        {
            if (_state == null || _noticeSurfaces.Length == 0)
            {
                return null;
            }

            int surfaceIndex = _state.State switch
            {
                MapleLib.WzLib.WzStructure.Data.QuestStructure.QuestStateType.Completed => 3,
                MapleLib.WzLib.WzStructure.Data.QuestStructure.QuestStateType.Started when _state.PrimaryAction == QuestWindowActionKind.Complete && _state.PrimaryActionEnabled => 2,
                MapleLib.WzLib.WzStructure.Data.QuestStructure.QuestStateType.Started => 1,
                MapleLib.WzLib.WzStructure.Data.QuestStructure.QuestStateType.Not_Started => 0,
                _ => -1
            };

            if (surfaceIndex < 0 || surfaceIndex >= _noticeSurfaces.Length)
            {
                return null;
            }

            return _noticeSurfaces[surfaceIndex];
        }

        private NoticeAnimationFrame? GetActiveNoticeAnimationFrame(int tickCount)
        {
            if (_noticeAnimationFrames.Length == 0)
            {
                return null;
            }

            int totalDuration = 0;
            for (int i = 0; i < _noticeAnimationFrames.Length; i++)
            {
                totalDuration += _noticeAnimationFrames[i].DelayMs;
            }

            if (totalDuration <= 0)
            {
                return _noticeAnimationFrames[0];
            }

            int normalizedTick = ((tickCount % totalDuration) + totalDuration) % totalDuration;
            int elapsed = 0;
            for (int i = 0; i < _noticeAnimationFrames.Length; i++)
            {
                elapsed += _noticeAnimationFrames[i].DelayMs;
                if (normalizedTick < elapsed)
                {
                    return _noticeAnimationFrames[i];
                }
            }

            return _noticeAnimationFrames[_noticeAnimationFrames.Length - 1];
        }

        private readonly struct ActionButtonBinding
        {
            public ActionButtonBinding(UIObject button, bool drawLabel)
            {
                Button = button;
                DrawLabel = drawLabel;
            }

            public UIObject Button { get; }
            public bool DrawLabel { get; }
        }

        private readonly struct ButtonLabel
        {
            public ButtonLabel(UIObject button, string text)
            {
                Button = button;
                Text = text;
            }

            public UIObject Button { get; }
            public string Text { get; }
        }

        private readonly struct NoticeSurface
        {
            public NoticeSurface(Texture2D texture, Point offset)
            {
                Texture = texture;
                Offset = offset;
            }

            public Texture2D Texture { get; }
            public Point Offset { get; }
        }

        private readonly struct NoticeAnimationFrame
        {
            public NoticeAnimationFrame(Texture2D texture, int delayMs)
            {
                Texture = texture;
                DelayMs = delayMs;
            }

            public Texture2D Texture { get; }
            public int DelayMs { get; }
        }
    }
}
