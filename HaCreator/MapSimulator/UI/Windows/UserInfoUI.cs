using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Companions;
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
    /// Character profile window backed by UI.wz/UIWindow(.2).img/UserInfo.
    /// Big Bang builds expose the client's user-info subpages while simulator
    /// runtime data continues to back only the subset that already exists locally.
    /// </summary>
    public sealed class UserInfoUI : UIWindowBase
    {
        private enum UserInfoPage
        {
            Character,
            Ride,
            Pet,
            Collect,
            Personality
        }

        private readonly struct PageLayer
        {
            public PageLayer(IDXObject texture, Point offset)
            {
                Texture = texture;
                Offset = offset;
            }

            public IDXObject Texture { get; }
            public Point Offset { get; }
        }

        private sealed class PageVisual
        {
            public IDXObject Frame { get; set; }
            public List<PageLayer> Layers { get; } = new List<PageLayer>();
            public IDXObject Icon { get; set; }
            public Point IconOffset { get; set; }
        }

        private readonly bool _isBigBang;
        private readonly IDXObject _defaultFrame;
        private readonly Dictionary<UserInfoPage, UIObject> _pageButtons = new Dictionary<UserInfoPage, UIObject>();
        private readonly Dictionary<UserInfoPage, PageVisual> _pageVisuals = new Dictionary<UserInfoPage, PageVisual>();

        private IDXObject _foreground;
        private Point _foregroundOffset;
        private IDXObject _nameBanner;
        private Point _nameBannerOffset;
        private SpriteFont _font;
        private CharacterBuild _characterBuild;
        private PetController _petController;
        private UserInfoPage _currentPage = UserInfoPage.Character;
        private string _statusMessage = "Character actions are available from this profile window.";

        private static readonly Color ValueColor = new Color(45, 45, 45);
        private static readonly Color SecondaryColor = new Color(96, 96, 96);
        private static readonly Color BannerTextColor = new Color(248, 248, 248);
        private static readonly Color HeaderColor = new Color(67, 50, 22);
        private static readonly Color MutedColor = new Color(122, 111, 90);
        private static readonly Color SuccessColor = new Color(44, 110, 52);
        private static readonly Color WarningColor = new Color(133, 82, 42);

        private static readonly Point BigBangNamePos = new Point(57, 154);
        private static readonly Point BigBangPortraitSummaryPos = new Point(19, 173);
        private static readonly Point BigBangFieldValuePos = new Point(131, 41);
        private const int BigBangFieldRowHeight = 23;
        private const int BigBangFieldMaxWidth = 116;

        private static readonly Point PreBigBangNamePos = new Point(16, 19);
        private static readonly Point PreBigBangPortraitSummaryPos = new Point(16, 146);
        private static readonly Point PreBigBangFieldValuePos = new Point(127, 54);
        private const int PreBigBangFieldRowHeight = 22;
        private const int PreBigBangFieldMaxWidth = 132;

        public UserInfoUI(IDXObject frame, bool isBigBang)
            : base(frame)
        {
            _defaultFrame = frame;
            _isBigBang = isBigBang;
        }

        public override string WindowName => MapSimulatorWindowNames.CharacterInfo;
        public Action MiniRoomRequested { get; set; }
        public Action TradingRoomRequested { get; set; }

        public override CharacterBuild CharacterBuild
        {
            get => _characterBuild;
            set => _characterBuild = value;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetForeground(IDXObject foreground, int offsetX, int offsetY)
        {
            _foreground = foreground;
            _foregroundOffset = new Point(offsetX, offsetY);
        }

        public void SetNameBanner(IDXObject nameBanner, int offsetX, int offsetY)
        {
            _nameBanner = nameBanner;
            _nameBannerOffset = new Point(offsetX, offsetY);
        }

        public void RegisterPageFrame(string pageName, IDXObject frame)
        {
            if (!TryGetPage(pageName, out UserInfoPage page))
            {
                return;
            }

            GetOrCreateVisual(page).Frame = frame;
            if (_currentPage == page)
            {
                ApplyCurrentPageFrame();
            }
        }

        public void AddPageLayer(string pageName, IDXObject layer, int offsetX, int offsetY)
        {
            if (layer == null || !TryGetPage(pageName, out UserInfoPage page))
            {
                return;
            }

            GetOrCreateVisual(page).Layers.Add(new PageLayer(layer, new Point(offsetX, offsetY)));
        }

        public void SetPageIcon(string pageName, IDXObject icon, int offsetX, int offsetY)
        {
            if (icon == null || !TryGetPage(pageName, out UserInfoPage page))
            {
                return;
            }

            PageVisual visual = GetOrCreateVisual(page);
            visual.Icon = icon;
            visual.IconOffset = new Point(offsetX, offsetY);
        }

        public void InitializePrimaryButtons(UIObject partyButton, UIObject tradeButton, UIObject itemButton, UIObject wishButton, UIObject familyButton)
        {
            BindActionButton(partyButton, "Party invite is not simulated yet.");
            BindActionButton(tradeButton, "Trading-room shell opened.", () => TradingRoomRequested?.Invoke());
            BindActionButton(itemButton, "Mini-room shell opened.", () => MiniRoomRequested?.Invoke());
            BindActionButton(wishButton, "Wishlist and present flow is not simulated yet.");
            BindActionButton(familyButton, "Family chart flow is not simulated yet.");
        }

        public void InitializePageButtons(UIObject rideButton, UIObject petButton, UIObject collectButton, UIObject personalityButton)
        {
            BindPageButton(UserInfoPage.Ride, rideButton, "Ride page ready.");
            BindPageButton(UserInfoPage.Pet, petButton, "Pet page ready.");
            BindPageButton(UserInfoPage.Collect, collectButton, "Collection page ready.");
            BindPageButton(UserInfoPage.Personality, personalityButton, "Personality page ready.");
            UpdateButtonStates();
        }

        public void SetPetController(PetController petController)
        {
            _petController = petController;
        }

        public override void Update(GameTime gameTime)
        {
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
            DrawCurrentPageVisuals(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);

            if (_font == null)
            {
                return;
            }

            switch (_currentPage)
            {
                case UserInfoPage.Ride:
                    DrawRidePage(sprite);
                    break;
                case UserInfoPage.Pet:
                    DrawPetPage(sprite);
                    break;
                case UserInfoPage.Collect:
                    DrawCollectPage(sprite);
                    break;
                case UserInfoPage.Personality:
                    DrawPersonalityPage(sprite);
                    break;
                default:
                    DrawCharacterPage(sprite);
                    break;
            }

            DrawStatusMessage(sprite);
        }

        private void DrawValueColumn(SpriteBatch sprite, Point start, int rowHeight, int maxWidth, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                DrawPlainText(
                    sprite,
                    FitText(values[i], maxWidth),
                    new Vector2(Position.X + start.X, Position.Y + start.Y + (i * rowHeight)),
                    ValueColor,
                    0.7f);
            }
        }

        private void DrawSummaryText(SpriteBatch sprite, string name, string job, Point position, int maxWidth)
        {
            DrawPlainText(
                sprite,
                FitText(name, maxWidth),
                new Vector2(Position.X + position.X, Position.Y + position.Y),
                SecondaryColor,
                0.65f);
            DrawPlainText(
                sprite,
                FitText(job, maxWidth),
                new Vector2(Position.X + position.X, Position.Y + position.Y + 14),
                SecondaryColor,
                0.6f);
        }

        private void DrawCenteredText(SpriteBatch sprite, string text, Rectangle bounds, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 textSize = _font.MeasureString(text) * scale;
            Vector2 position = new Vector2(
                bounds.X + Math.Max(0f, (bounds.Width - textSize.X) * 0.5f),
                bounds.Y + Math.Max(0f, (bounds.Height - textSize.Y) * 0.5f) - 1f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawPlainText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawCurrentPageVisuals(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            if (_currentPage == UserInfoPage.Character)
            {
                DrawLayer(_foreground, _foregroundOffset, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
                DrawLayer(_nameBanner, _nameBannerOffset, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
                return;
            }

            if (!_pageVisuals.TryGetValue(_currentPage, out PageVisual visual))
            {
                return;
            }

            foreach (PageLayer layer in visual.Layers)
            {
                DrawLayer(layer.Texture, layer.Offset, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
            }

            DrawLayer(visual.Icon, visual.IconOffset, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
        }

        private void DrawCharacterPage(SpriteBatch sprite)
        {
            string name = _characterBuild?.Name;
            string job = _characterBuild?.JobName;
            string level = _characterBuild != null ? _characterBuild.Level.ToString() : "-";
            string fame = _characterBuild != null ? _characterBuild.Fame.ToString() : "-";
            string rank = FormatRank(_characterBuild?.WorldRank ?? 0);
            string guild = _characterBuild?.GuildDisplayText ?? "-";
            string alliance = _characterBuild?.AllianceDisplayText ?? "-";

            if (_isBigBang)
            {
                if (_nameBanner != null)
                {
                    Rectangle bannerBounds = new Rectangle(
                        Position.X + _nameBannerOffset.X,
                        Position.Y + _nameBannerOffset.Y,
                        _nameBanner.Width,
                        _nameBanner.Height);
                    DrawCenteredText(sprite, FitText(name, 72), bannerBounds, BannerTextColor, 0.6f);
                }

                DrawSummaryText(sprite, name, job, BigBangPortraitSummaryPos, 116);
                DrawValueColumn(sprite, BigBangFieldValuePos, BigBangFieldRowHeight, BigBangFieldMaxWidth,
                    level,
                    job,
                    rank,
                    fame,
                    guild,
                    alliance);
                return;
            }

            DrawPlainText(sprite, FitText(name, 110),
                new Vector2(Position.X + PreBigBangNamePos.X, Position.Y + PreBigBangNamePos.Y),
                SecondaryColor,
                0.75f);
            DrawSummaryText(sprite, name, job, PreBigBangPortraitSummaryPos, 98);
            DrawValueColumn(sprite, PreBigBangFieldValuePos, PreBigBangFieldRowHeight, PreBigBangFieldMaxWidth,
                level,
                job,
                fame,
                guild,
                alliance);
        }

        private void DrawRidePage(SpriteBatch sprite)
        {
            string mountName = GetEquippedItemName(EquipSlot.TamingMob);
            string saddleName = GetEquippedItemName(EquipSlot.Saddle);
            bool ridingReady = _characterBuild?.HasMonsterRiding == true && !string.Equals(mountName, "-", StringComparison.Ordinal);

            DrawSectionHeader(sprite, "Ride");
            DrawLabeledRow(sprite, 42, "Status", ridingReady ? "Ride available" : "No active mount slot", ridingReady ? SuccessColor : WarningColor);
            DrawLabeledRow(sprite, 66, "Mount", mountName, ValueColor);
            DrawLabeledRow(sprite, 90, "Saddle", saddleName, ValueColor);
            DrawLabeledRow(sprite, 114, "Job", _characterBuild?.JobName ?? "-", ValueColor);
            DrawLabeledRow(sprite, 138, "Notes", ridingReady
                ? "Taming-mob equipment is present in the simulator build."
                : "Equip a taming mob and saddle to mirror the client ride page.", MutedColor, 144);
        }

        private void DrawPetPage(SpriteBatch sprite)
        {
            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;

            DrawSectionHeader(sprite, "Pet");
            if (pets == null || pets.Count == 0)
            {
                DrawPlainText(sprite,
                    "No active pets. Summon a simulator pet to populate this page.",
                    new Vector2(Position.X + 20, Position.Y + 54),
                    MutedColor,
                    0.62f);
                return;
            }

            for (int i = 0; i < Math.Min(3, pets.Count); i++)
            {
                PetRuntime pet = pets[i];
                int baseY = 48 + (i * 42);
                DrawLayer(pet.Definition?.IconRaw ?? pet.Definition?.Icon,
                    new Point(20, baseY),
                    sprite,
                    null,
                    null,
                    null);
                DrawLabeledRow(sprite, baseY + 2, $"Pet {i + 1}", pet.Name, ValueColor, 120);
                DrawLabeledRow(sprite, baseY + 16, "Level", $"Command Lv. {pet.CommandLevel}", MutedColor, 120);
                DrawLabeledRow(sprite, baseY + 30, "Mode", $"{(pet.AutoLootEnabled ? "Auto-loot enabled" : "Auto-loot disabled")}  Balloon {pet.ChatBalloonStyle}",
                    pet.AutoLootEnabled ? SuccessColor : WarningColor, 120);
            }
        }

        private void DrawCollectPage(SpriteBatch sprite)
        {
            int equippedCount = _characterBuild?.Equipment?.Count ?? 0;
            string weapon = _characterBuild?.GetWeapon()?.Name ?? "-";
            string cap = GetEquippedItemName(EquipSlot.Cap);
            string outfit = ResolveOutfitName();
            string shoes = GetEquippedItemName(EquipSlot.Shoes);

            DrawSectionHeader(sprite, "Collect");
            DrawLabeledRow(sprite, 40, "Equipped", equippedCount.ToString(), ValueColor);
            DrawLabeledRow(sprite, 64, "Weapon", weapon, ValueColor, 144);
            DrawLabeledRow(sprite, 88, "Cap", cap, ValueColor, 144);
            DrawLabeledRow(sprite, 112, "Outfit", outfit, ValueColor, 144);
            DrawLabeledRow(sprite, 136, "Shoes", shoes, ValueColor, 144);
            DrawLabeledRow(sprite, 160, "EXP", _characterBuild?.ExpDisplayText ?? "-", MutedColor, 144);
        }

        private void DrawPersonalityPage(SpriteBatch sprite)
        {
            DrawSectionHeader(sprite, "Personality");

            (string Label, int Value)[] traits =
            {
                ("Charisma", _characterBuild?.TraitCharisma ?? 0),
                ("Insight", _characterBuild?.TraitInsight ?? 0),
                ("Will", _characterBuild?.TraitWill ?? 0),
                ("Craft", _characterBuild?.TraitCraft ?? 0),
                ("Sense", _characterBuild?.TraitSense ?? 0),
                ("Charm", _characterBuild?.TraitCharm ?? 0)
            };

            int topValue = traits.Max(entry => entry.Value);
            for (int i = 0; i < traits.Length; i++)
            {
                (string label, int value) = traits[i];
                Color color = value > 0 && value == topValue ? SuccessColor : ValueColor;
                DrawLabeledRow(sprite, 44 + (i * 24), label, value.ToString(), color, 84);
            }

            if (topValue <= 0)
            {
                DrawPlainText(sprite,
                    "No simulator-side personality progression is recorded yet.",
                    new Vector2(Position.X + 20, Position.Y + 198),
                    MutedColor,
                    0.58f);
            }
        }

        private void DrawSectionHeader(SpriteBatch sprite, string title)
        {
            DrawPlainText(sprite,
                title,
                new Vector2(Position.X + 20, Position.Y + 20),
                HeaderColor,
                0.78f);
        }

        private void DrawLabeledRow(SpriteBatch sprite, int y, string label, string value, Color valueColor, int maxWidth = 132)
        {
            DrawPlainText(sprite,
                label,
                new Vector2(Position.X + 20, Position.Y + y),
                SecondaryColor,
                0.6f);
            DrawPlainText(sprite,
                FitText(value, maxWidth),
                new Vector2(Position.X + 92, Position.Y + y),
                valueColor,
                0.62f);
        }

        private void DrawStatusMessage(SpriteBatch sprite)
        {
            if (string.IsNullOrWhiteSpace(_statusMessage))
            {
                return;
            }

            DrawPlainText(
                sprite,
                FitText(_statusMessage, 226),
                new Vector2(Position.X + 20, Position.Y + CurrentFrame.Height - 20),
                MutedColor,
                0.55f);
        }

        private void BindActionButton(UIObject button, string message, Action action = null)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ =>
            {
                _statusMessage = message;
                action?.Invoke();
            };
        }

        private void BindPageButton(UserInfoPage page, UIObject button, string message)
        {
            if (button == null)
            {
                return;
            }

            _pageButtons[page] = button;
            AddButton(button);
            button.ButtonClickReleased += _ =>
            {
                SwitchPage(page);
                _statusMessage = message;
            };
        }

        private void SwitchPage(UserInfoPage page)
        {
            if (_currentPage == page)
            {
                _currentPage = UserInfoPage.Character;
            }
            else
            {
                _currentPage = page;
            }

            ApplyCurrentPageFrame();
            UpdateButtonStates();
        }

        private void ApplyCurrentPageFrame()
        {
            if (_currentPage != UserInfoPage.Character &&
                _pageVisuals.TryGetValue(_currentPage, out PageVisual visual) &&
                visual.Frame != null)
            {
                Frame = visual.Frame;
                return;
            }

            Frame = _defaultFrame;
        }

        private void UpdateButtonStates()
        {
            foreach ((UserInfoPage page, UIObject button) in _pageButtons)
            {
                button.SetButtonState(_currentPage == page ? UIObjectState.Pressed : UIObjectState.Normal);
            }
        }

        private PageVisual GetOrCreateVisual(UserInfoPage page)
        {
            if (!_pageVisuals.TryGetValue(page, out PageVisual visual))
            {
                visual = new PageVisual();
                _pageVisuals[page] = visual;
            }

            return visual;
        }

        private static bool TryGetPage(string pageName, out UserInfoPage page)
        {
            switch (pageName)
            {
                case "ride":
                    page = UserInfoPage.Ride;
                    return true;
                case "pet":
                    page = UserInfoPage.Pet;
                    return true;
                case "collect":
                    page = UserInfoPage.Collect;
                    return true;
                case "personality":
                    page = UserInfoPage.Personality;
                    return true;
                default:
                    page = UserInfoPage.Character;
                    return false;
            }
        }

        private string GetEquippedItemName(EquipSlot slot)
        {
            if (_characterBuild?.Equipment != null &&
                _characterBuild.Equipment.TryGetValue(slot, out CharacterPart part) &&
                !string.IsNullOrWhiteSpace(part?.Name))
            {
                return part.Name;
            }

            return "-";
        }

        private string ResolveOutfitName()
        {
            string longcoat = GetEquippedItemName(EquipSlot.Longcoat);
            if (!string.Equals(longcoat, "-", StringComparison.Ordinal))
            {
                return longcoat;
            }

            string coat = GetEquippedItemName(EquipSlot.Coat);
            string pants = GetEquippedItemName(EquipSlot.Pants);
            if (string.Equals(coat, "-", StringComparison.Ordinal) && string.Equals(pants, "-", StringComparison.Ordinal))
            {
                return "-";
            }

            return $"{coat} / {pants}";
        }

        private static string FormatRank(int rank)
        {
            return rank > 0 ? $"#{rank:N0}" : "-";
        }

        private string FitText(string text, float maxWidth)
        {
            string safeText = string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
            if (_font == null || _font.MeasureString(safeText).X <= maxWidth)
            {
                return safeText;
            }

            const string ellipsis = "...";
            for (int length = safeText.Length - 1; length > 0; length--)
            {
                string candidate = safeText.Substring(0, length) + ellipsis;
                if (_font.MeasureString(candidate).X <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private void DrawLayer(
            IDXObject layer,
            Point offset,
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo)
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
    }
}
