using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
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
    /// <summary>
    /// Dedicated book-style collection owner shaped after UIWindow(.2).img/Book.
    /// The client pages in two-page spreads, so the simulator keeps the same cadence.
    /// </summary>
    public sealed class BookCollectionWindow : UIWindowBase
    {
        private sealed class BookEntry
        {
            public BookEntry(string label, string value, Color color)
            {
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Color = color;
            }

            public string Label { get; }
            public string Value { get; }
            public Color Color { get; }
        }

        private sealed class BookPage
        {
            public BookPage(string title, string subtitle, IEnumerable<BookEntry> entries)
            {
                Title = title ?? string.Empty;
                Subtitle = subtitle ?? string.Empty;
                Entries = entries?.ToList() ?? new List<BookEntry>();
            }

            public string Title { get; }
            public string Subtitle { get; }
            public List<BookEntry> Entries { get; }
        }

        private const int PrevButtonId = 1000;
        private const int NextButtonId = 1001;
        private const int PageStride = 2;
        private const int EntriesPerEquipmentPage = 6;
        private const int EntriesPerRecipePage = 6;

        private readonly Texture2D _pixel;
        private readonly Action _closeRequested;
        private readonly Dictionary<int, Action> _buttonActions = new();
        private readonly List<BookPage> _pages = new();

        private Texture2D _pageMarkerActiveTexture;
        private Texture2D _pageMarkerInactiveTexture;
        private SpriteFont _font;
        private Func<ItemMakerProgressionSnapshot> _snapshotProvider;
        private int _currentSpreadStart;
        private UIObject _prevButton;
        private UIObject _nextButton;

        private static readonly Point LeftPageOrigin = new(30, 42);
        private static readonly Point RightPageOrigin = new(257, 42);
        private static readonly Point PageContentSize = new(189, 223);
        private static readonly Rectangle PageIndexBounds = new(144, 289, 190, 18);
        private static readonly Color PaperTint = new(255, 250, 239, 160);
        private static readonly Color PageBorderColor = new(118, 92, 55, 180);
        private static readonly Color TitleColor = new(78, 51, 26);
        private static readonly Color SubtitleColor = new(132, 109, 83);
        private static readonly Color LabelColor = new(96, 72, 45);
        private static readonly Color ValueColor = new(43, 43, 43);
        private static readonly Color AccentColor = new(158, 80, 36);
        private static readonly Color MarkerColor = new(124, 86, 40);
        private static readonly Color MarkerOffColor = new(188, 169, 137);

        public BookCollectionWindow(IDXObject frame, Texture2D pixel, Action closeRequested = null)
            : base(frame)
        {
            _pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
            _closeRequested = closeRequested;
        }

        public override string WindowName => MapSimulatorWindowNames.BookCollection;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetCollectionSnapshotProvider(Func<ItemMakerProgressionSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
        }

        public void SetPageMarkerTextures(Texture2D activeMarkerTexture, Texture2D inactiveMarkerTexture)
        {
            _pageMarkerActiveTexture = activeMarkerTexture;
            _pageMarkerInactiveTexture = inactiveMarkerTexture;
        }

        public void InitializeButtons(UIObject prevButton, UIObject nextButton, UIObject closeButton)
        {
            _prevButton = prevButton;
            _nextButton = nextButton;

            RegisterButton(prevButton, PrevButtonId, () => MoveSpread(-PageStride));
            RegisterButton(nextButton, NextButtonId, () => MoveSpread(PageStride));
            InitializeCloseButton(closeButton);
        }

        public override void Show()
        {
            RefreshPages();
            ClampSpreadStart();
            UpdateButtonStates();
            base.Show();
        }

        public override void Update(GameTime gameTime)
        {
            RefreshPages();
            ClampSpreadStart();
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
            if (_font == null)
            {
                return;
            }

            DrawPage(sprite, LeftPageOrigin, GetPage(_currentSpreadStart));
            DrawPage(sprite, RightPageOrigin, GetPage(_currentSpreadStart + 1));
            DrawPageMarkers(sprite);
            DrawPageIndex(sprite);
        }

        protected override void OnCloseButtonClicked(UIObject sender)
        {
            base.OnCloseButtonClicked(sender);
            _closeRequested?.Invoke();
        }

        private void RegisterButton(UIObject button, int buttonId, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            _buttonActions[buttonId] = action;
            button.ButtonClickReleased += _ =>
            {
                if (_buttonActions.TryGetValue(buttonId, out Action handler))
                {
                    handler?.Invoke();
                }
            };
        }

        private void MoveSpread(int delta)
        {
            RefreshPages();

            int nextSpread = _currentSpreadStart + delta;
            if (nextSpread < 0)
            {
                nextSpread = 0;
            }

            int lastSpreadStart = Math.Max(0, ((_pages.Count - 1) / PageStride) * PageStride);
            if (nextSpread > lastSpreadStart)
            {
                nextSpread = lastSpreadStart;
            }

            _currentSpreadStart = nextSpread;
            UpdateButtonStates();
        }

        private void RefreshPages()
        {
            _pages.Clear();

            CharacterBuild build = CharacterBuild;
            ItemMakerProgressionSnapshot snapshot = _snapshotProvider?.Invoke() ?? ItemMakerProgressionSnapshot.Default;

            _pages.Add(new BookPage(
                "Collection Overview",
                build?.Name ?? "Simulator Profile",
                new[]
                {
                    new BookEntry("Job", build?.JobName ?? "-", ValueColor),
                    new BookEntry("Level", (build?.Level ?? 0).ToString(), ValueColor),
                    new BookEntry("Maker", $"Lv {snapshot.GenericLevel}", AccentColor),
                    new BookEntry("Crafts", snapshot.SuccessfulCrafts.ToString(), ValueColor),
                    new BookEntry("Recipes", snapshot.DiscoveredRecipeIds.Count.ToString(), ValueColor),
                    new BookEntry("Trait Craft", (build?.TraitCraft ?? snapshot.TraitCraft).ToString(), ValueColor)
                }));

            _pages.Add(new BookPage(
                "Crafting Families",
                "Two-page spread entries in the client map cleanly onto local family summaries.",
                new[]
                {
                    CreateFamilyEntry(snapshot, ItemMakerRecipeFamily.Generic),
                    CreateFamilyEntry(snapshot, ItemMakerRecipeFamily.Gloves),
                    CreateFamilyEntry(snapshot, ItemMakerRecipeFamily.Shoes),
                    CreateFamilyEntry(snapshot, ItemMakerRecipeFamily.Toys)
                }));

            _pages.Add(new BookPage(
                "Traits",
                "Personality values already loaded into UserInfo stay visible here as well.",
                new[]
                {
                    new BookEntry("Charisma", (build?.TraitCharisma ?? 0).ToString(), ValueColor),
                    new BookEntry("Insight", (build?.TraitInsight ?? 0).ToString(), ValueColor),
                    new BookEntry("Will", (build?.TraitWill ?? 0).ToString(), ValueColor),
                    new BookEntry("Craft", (build?.TraitCraft ?? snapshot.TraitCraft).ToString(), AccentColor),
                    new BookEntry("Sense", (build?.TraitSense ?? 0).ToString(), ValueColor),
                    new BookEntry("Charm", (build?.TraitCharm ?? 0).ToString(), ValueColor)
                }));

            IReadOnlyList<BookEntry> equipmentEntries = BuildEquipmentEntries(build);
            AppendPagedSection("Equipment Ledger", "Visible equipment mirrors the live simulator build.", equipmentEntries, EntriesPerEquipmentPage);

            IReadOnlyList<BookEntry> recipeEntries = BuildRecipeEntries(snapshot);
            AppendPagedSection("Recipe Ledger", "Discovered maker outputs grouped into client-like rows.", recipeEntries, EntriesPerRecipePage);

            if (_pages.Count == 0)
            {
                _pages.Add(new BookPage(
                    "Collection",
                    "No book entries are available for this character yet.",
                    new[] { new BookEntry("Status", "No local collection data.", SubtitleColor) }));
            }
        }

        private void AppendPagedSection(string title, string subtitle, IReadOnlyList<BookEntry> entries, int pageSize)
        {
            if (entries == null || entries.Count == 0)
            {
                _pages.Add(new BookPage(title, subtitle, new[] { new BookEntry("Status", "No entries recorded.", SubtitleColor) }));
                return;
            }

            for (int offset = 0; offset < entries.Count; offset += pageSize)
            {
                List<BookEntry> chunk = entries.Skip(offset).Take(pageSize).ToList();
                _pages.Add(new BookPage(title, subtitle, chunk));
            }
        }

        private static BookEntry CreateFamilyEntry(ItemMakerProgressionSnapshot snapshot, ItemMakerRecipeFamily family)
        {
            int level = snapshot.GetLevel(family);
            int progress = snapshot.GetProgress(family);
            int target = snapshot.GetProgressTarget(family);
            string value = target > 0 ? $"Lv {level}  {progress}/{target}" : $"Lv {level}";
            Color color = level > 1 || progress > 0 ? AccentColor : ValueColor;
            return new BookEntry(snapshot.GetFamilyLabel(family), value, color);
        }

        private static IReadOnlyList<BookEntry> BuildEquipmentEntries(CharacterBuild build)
        {
            if (build?.Equipment == null || build.Equipment.Count == 0)
            {
                return new[] { new BookEntry("Status", "No equipped items.", SubtitleColor) };
            }

            return build.Equipment
                .OrderBy(entry => entry.Key.ToString(), StringComparer.OrdinalIgnoreCase)
                .Select(entry => new BookEntry(
                    entry.Key.ToString(),
                    string.IsNullOrWhiteSpace(entry.Value?.Name) ? "-" : entry.Value.Name,
                    ValueColor))
                .ToList();
        }

        private static IReadOnlyList<BookEntry> BuildRecipeEntries(ItemMakerProgressionSnapshot snapshot)
        {
            if (snapshot.DiscoveredRecipeIds == null || snapshot.DiscoveredRecipeIds.Count == 0)
            {
                return new[] { new BookEntry("Status", "No discovered recipes.", SubtitleColor) };
            }

            return snapshot.DiscoveredRecipeIds
                .OrderBy(id => id)
                .Select((id, index) => new BookEntry($"Recipe {index + 1}", id.ToString(), ValueColor))
                .ToList();
        }

        private BookPage GetPage(int pageIndex)
        {
            return pageIndex >= 0 && pageIndex < _pages.Count ? _pages[pageIndex] : null;
        }

        private void ClampSpreadStart()
        {
            if (_pages.Count <= 1)
            {
                _currentSpreadStart = 0;
                return;
            }

            int lastSpreadStart = Math.Max(0, ((_pages.Count - 1) / PageStride) * PageStride);
            _currentSpreadStart = Math.Clamp(_currentSpreadStart, 0, lastSpreadStart);
            _currentSpreadStart &= ~1;
        }

        private void UpdateButtonStates()
        {
            bool hasPrevious = _currentSpreadStart > 0;
            bool hasNext = _currentSpreadStart + PageStride < _pages.Count;

            _prevButton?.SetEnabled(hasPrevious);
            _nextButton?.SetEnabled(hasNext);
        }

        private void DrawPage(SpriteBatch sprite, Point origin, BookPage page)
        {
            Rectangle bounds = new(
                Position.X + origin.X,
                Position.Y + origin.Y,
                PageContentSize.X,
                PageContentSize.Y);

            sprite.Draw(_pixel, bounds, PaperTint);
            DrawFrame(sprite, bounds, PageBorderColor);

            if (page == null)
            {
                DrawCenteredString(sprite, "Blank", new Rectangle(bounds.X, bounds.Y + 96, bounds.Width, 18), SubtitleColor, 0.72f);
                return;
            }

            DrawTrimmedString(sprite, page.Title, new Vector2(bounds.X + 12, bounds.Y + 12), TitleColor, 0.76f, bounds.Width - 24);
            DrawWrappedText(sprite, page.Subtitle, new Rectangle(bounds.X + 12, bounds.Y + 38, bounds.Width - 24, 34), SubtitleColor, 0.52f);

            int y = bounds.Y + 82;
            foreach (BookEntry entry in page.Entries)
            {
                DrawEntryRow(sprite, bounds, y, entry);
                y += 24;
            }
        }

        private void DrawEntryRow(SpriteBatch sprite, Rectangle pageBounds, int y, BookEntry entry)
        {
            Rectangle rowBounds = new(pageBounds.X + 8, y, pageBounds.Width - 16, 20);
            sprite.Draw(_pixel, rowBounds, new Color(255, 255, 255, 42));
            sprite.Draw(_pixel, new Rectangle(rowBounds.X, rowBounds.Bottom - 1, rowBounds.Width, 1), new Color(142, 118, 84, 90));

            DrawTrimmedString(sprite, entry.Label, new Vector2(rowBounds.X + 6, rowBounds.Y + 3), LabelColor, 0.56f, 72f);
            DrawTrimmedString(sprite, entry.Value, new Vector2(rowBounds.X + 78, rowBounds.Y + 3), entry.Color, 0.56f, rowBounds.Width - 86f);
        }

        private void DrawPageMarkers(SpriteBatch sprite)
        {
            int markerY = Position.Y + 276;
            DrawMarker(sprite, Position.X + 222, markerY, GetPage(_currentSpreadStart) != null);
            DrawMarker(sprite, Position.X + 248, markerY, GetPage(_currentSpreadStart + 1) != null);
        }

        private void DrawMarker(SpriteBatch sprite, int x, int y, bool active)
        {
            Texture2D texture = active ? _pageMarkerActiveTexture : _pageMarkerInactiveTexture;
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(x, y), Color.White);
                return;
            }

            Color color = active ? MarkerColor : MarkerOffColor;
            sprite.Draw(_pixel, new Rectangle(x, y, 7, 7), color);
        }

        private void DrawPageIndex(SpriteBatch sprite)
        {
            int spreadNumber = (_currentSpreadStart / PageStride) + 1;
            int spreadCount = Math.Max(1, (_pages.Count + 1) / PageStride);
            Rectangle bounds = new(
                Position.X + PageIndexBounds.X,
                Position.Y + PageIndexBounds.Y,
                PageIndexBounds.Width,
                PageIndexBounds.Height);

            DrawCenteredString(sprite, $"{spreadNumber}/{spreadCount}", bounds, AccentColor, 0.66f);
        }

        private void DrawFrame(SpriteBatch sprite, Rectangle bounds, Color color)
        {
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            sprite.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        private void DrawWrappedText(SpriteBatch sprite, string text, Rectangle bounds, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float y = bounds.Y;
            foreach (string line in WrapText(text, bounds.Width, scale))
            {
                sprite.DrawString(_font, line, new Vector2(bounds.X, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += (_font.LineSpacing * scale) - 1f;
                if (y > bounds.Bottom)
                {
                    break;
                }
            }
        }

        private void DrawCenteredString(SpriteBatch sprite, string text, Rectangle bounds, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = _font.MeasureString(text) * scale;
            Vector2 position = new(
                bounds.X + Math.Max(0f, (bounds.Width - size.X) / 2f),
                bounds.Y + Math.Max(0f, (bounds.Height - size.Y) / 2f));
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawTrimmedString(SpriteBatch sprite, string text, Vector2 position, Color color, float scale, float maxWidth)
        {
            string trimmed = TrimToWidth(text, maxWidth, scale);
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                sprite.DrawString(_font, trimmed, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            string[] words = (text ?? string.Empty)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
            {
                yield break;
            }

            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && Measure(candidate, scale) > maxWidth)
                {
                    yield return currentLine;
                    currentLine = word;
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

        private string TrimToWidth(string text, float maxWidth, float scale)
        {
            string safeText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            if (string.IsNullOrEmpty(safeText) || Measure(safeText, scale) <= maxWidth)
            {
                return safeText;
            }

            const string ellipsis = "...";
            for (int length = safeText.Length - 1; length > 0; length--)
            {
                string candidate = safeText.Substring(0, length) + ellipsis;
                if (Measure(candidate, scale) <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private float Measure(string text, float scale)
        {
            return _font.MeasureString(text).X * scale;
        }
    }
}
