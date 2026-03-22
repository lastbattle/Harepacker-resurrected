using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Companions;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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

        private sealed class ExceptionPopupVisual
        {
            public IDXObject Frame { get; set; }
            public List<PageLayer> Layers { get; } = new List<PageLayer>();
        }

        private sealed class AuxiliaryPopupVisual
        {
            public IDXObject Frame { get; set; }
            public List<PageLayer> Layers { get; } = new List<PageLayer>();
        }

        private enum AuxiliaryPopupKind
        {
            None,
            Item,
            Wish
        }

        private readonly bool _isBigBang;
        private readonly IDXObject _defaultFrame;
        private readonly Dictionary<UserInfoPage, UIObject> _pageButtons = new Dictionary<UserInfoPage, UIObject>();
        private readonly List<UIObject> _primaryButtons = new List<UIObject>();
        private readonly Dictionary<UserInfoPage, PageVisual> _pageVisuals = new Dictionary<UserInfoPage, PageVisual>();
        private readonly List<string> _petExceptionEntries = new List<string> { "Meso Bag", "Throwing Star" };
        private readonly string[] _itemPopupEntries = { "Mini Room", "Personal Shop", "Entrusted Shop" };
        private readonly List<string> _wishEntries = new List<string> { "White Scroll", "Brown Work Gloves", "Ilbi Throwing-Star" };

        private IDXObject _foreground;
        private Point _foregroundOffset;
        private IDXObject _nameBanner;
        private Point _nameBannerOffset;
        private SpriteFont _font;
        private CharacterBuild _characterBuild;
        private PetController _petController;
        private UserInfoPage _currentPage = UserInfoPage.Character;
        private string _statusMessage = "Character actions are available from this profile window.";
        private UIObject _petExceptionButton;
        private UIObject _collectSortButton;
        private UIObject _collectClaimButton;
        private ExceptionPopupVisual _exceptionPopupVisual;
        private UIObject _exceptionRegisterButton;
        private UIObject _exceptionDeleteButton;
        private UIObject _exceptionMesoButton;
        private bool _exceptionPopupOpen;
        private AuxiliaryPopupVisual _itemPopupVisual;
        private AuxiliaryPopupVisual _wishPopupVisual;
        private UIObject _wishPresentButton;
        private AuxiliaryPopupKind _activePopup;
        private int _selectedItemPopupIndex;
        private int _selectedWishIndex;
        private bool _petExceptionBlocksMeso = true;
        private int _selectedPetExceptionIndex = -1;
        private bool _collectSortByName;
        private bool _collectRewardClaimed;
        private MouseState _previousMouseState;

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
        public Action PartyRequested { get; set; }
        public Action TradingRoomRequested { get; set; }
        public Action FamilyRequested { get; set; }
        public Action PersonalShopRequested { get; set; }
        public Action EntrustedShopRequested { get; set; }

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
            BindActionButton(partyButton, "Party list opened from the profile window.", () => PartyRequested?.Invoke(), true);
            BindActionButton(tradeButton, "Trading-room shell opened.", () => TradingRoomRequested?.Invoke(), true);
            BindActionButton(itemButton, "Item list opened.", ToggleItemPopup, true);
            BindActionButton(wishButton, "Wish list opened.", ToggleWishPopup, true);
            BindActionButton(familyButton, "Family chart opened from the profile window.", () => FamilyRequested?.Invoke(), true);
        }

        public void InitializePageButtons(UIObject rideButton, UIObject petButton, UIObject collectButton, UIObject personalityButton)
        {
            BindPageButton(UserInfoPage.Ride, rideButton, "Ride page ready.");
            BindPageButton(UserInfoPage.Pet, petButton, "Pet page ready.");
            BindPageButton(UserInfoPage.Collect, collectButton, "Collection page ready.");
            BindPageButton(UserInfoPage.Personality, personalityButton, "Personality page ready.");
            UpdateButtonStates();
        }

        public bool ShowPage(string pageName)
        {
            if (!TryGetPage(pageName, out UserInfoPage page) || !IsPageAvailable(page))
            {
                return false;
            }

            _statusMessage = page switch
            {
                UserInfoPage.Ride => "Ride page ready.",
                UserInfoPage.Pet => "Pet page ready.",
                UserInfoPage.Collect => "Collection page ready.",
                UserInfoPage.Personality => "Personality page ready.",
                _ => "Character actions are available from this profile window."
            };

            _currentPage = page;
            _exceptionPopupOpen = false;
            ApplyCurrentPageFrame();
            UpdateButtonStates();
            return true;
        }

        public void InitializePageActionButtons(UIObject petExceptionButton, UIObject collectSortButton, UIObject collectClaimButton)
        {
            _petExceptionButton = petExceptionButton;
            _collectSortButton = collectSortButton;
            _collectClaimButton = collectClaimButton;

            BindActionButton(_petExceptionButton, "Pet exception list opened.", ToggleExceptionPopup);
            BindActionButton(_collectSortButton, "Collection entries sorted by name.", ToggleCollectSortMode);
            BindActionButton(_collectClaimButton, "Collection reward is not ready yet.", ClaimCollectReward);
            UpdateButtonStates();
        }

        public void InitializeExceptionPopup(
            IDXObject frame,
            IEnumerable<(IDXObject layer, Point offset)> layers,
            UIObject registerButton,
            UIObject deleteButton,
            UIObject mesoButton)
        {
            _exceptionPopupVisual = new ExceptionPopupVisual
            {
                Frame = frame
            };

            if (layers != null)
            {
                foreach ((IDXObject layer, Point offset) in layers)
                {
                    if (layer != null)
                    {
                        _exceptionPopupVisual.Layers.Add(new PageLayer(layer, offset));
                    }
                }
            }

            _exceptionRegisterButton = registerButton;
            _exceptionDeleteButton = deleteButton;
            _exceptionMesoButton = mesoButton;

            BindActionButton(_exceptionRegisterButton, "Pet exception item registered.", RegisterPetExceptionEntry);
            BindActionButton(_exceptionDeleteButton, "Pet exception item removed.", DeletePetExceptionEntry);
            BindActionButton(_exceptionMesoButton, "Pet meso pickup preference updated.", TogglePetMesoException);
            UpdateButtonStates();
        }

        public void InitializeItemPopup(IDXObject frame, IEnumerable<(IDXObject layer, Point offset)> layers)
        {
            _itemPopupVisual = CreateAuxiliaryPopupVisual(frame, layers);
        }

        public void InitializeWishPopup(
            IDXObject frame,
            IEnumerable<(IDXObject layer, Point offset)> layers,
            UIObject presentButton)
        {
            _wishPopupVisual = CreateAuxiliaryPopupVisual(frame, layers);
            _wishPresentButton = presentButton;
            BindActionButton(_wishPresentButton, "Present flow still needs the packet-backed gifting path.", PresentWishEntry);
            UpdateButtonStates();
        }

        public void SetPetController(PetController petController)
        {
            _petController = petController;
        }

        public override void Update(GameTime gameTime)
        {
            if (_currentPage != UserInfoPage.Character && !IsPageAvailable(_currentPage))
            {
                _currentPage = UserInfoPage.Character;
                _exceptionPopupOpen = false;
                _activePopup = AuxiliaryPopupKind.None;
                ApplyCurrentPageFrame();
            }

            if (_currentPage != UserInfoPage.Character)
            {
                _activePopup = AuxiliaryPopupKind.None;
            }

            HandlePopupMouseInput();

            UpdateButtonStates();
            _previousMouseState = Mouse.GetState();
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

            DrawExceptionPopup(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
            DrawAuxiliaryPopup(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
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
            List<(string Label, string Value)> entries = BuildCollectEntries();

            DrawSectionHeader(sprite, "Collect");
            for (int i = 0; i < entries.Count; i++)
            {
                (string label, string value) = entries[i];
                DrawLabeledRow(sprite, 40 + (i * 24), label, value, ValueColor, 144);
            }

            string rewardStatus = CanClaimCollectReward()
                ? (_collectRewardClaimed ? "Reward claimed" : "Reward ready")
                : "Need 5 equipped items";
            DrawPlainText(
                sprite,
                rewardStatus,
                new Vector2(Position.X + 20, Position.Y + 188),
                CanClaimCollectReward() && !_collectRewardClaimed ? SuccessColor : MutedColor,
                0.58f);
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

        private void DrawExceptionPopup(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            if (!_exceptionPopupOpen || _exceptionPopupVisual?.Frame == null)
            {
                return;
            }

            Point popupPosition = GetExceptionPopupPosition();
            _exceptionPopupVisual.Frame.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                popupPosition.X,
                popupPosition.Y,
                Color.White,
                false,
                drawReflectionInfo);

            foreach (PageLayer layer in _exceptionPopupVisual.Layers)
            {
                layer.Texture?.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    popupPosition.X + layer.Offset.X,
                    popupPosition.Y + layer.Offset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }

            if (_font == null)
            {
                return;
            }

            DrawPlainText(sprite, "Pet exception", new Vector2(popupPosition.X + 12, popupPosition.Y + 10), HeaderColor, 0.7f);
            DrawPlainText(sprite,
                _petExceptionBlocksMeso ? "Meso pickup blocked" : "Meso pickup allowed",
                new Vector2(popupPosition.X + 12, popupPosition.Y + 30),
                _petExceptionBlocksMeso ? WarningColor : SuccessColor,
                0.56f);

            if (_petExceptionEntries.Count == 0)
            {
                DrawPlainText(sprite,
                    "No exception entries.",
                    new Vector2(popupPosition.X + 12, popupPosition.Y + 52),
                    MutedColor,
                    0.56f);
                return;
            }

            for (int i = 0; i < Math.Min(3, _petExceptionEntries.Count); i++)
            {
                int entryIndex = i;
                string entry = _petExceptionEntries[entryIndex];
                Color color = entryIndex == _selectedPetExceptionIndex ? SuccessColor : ValueColor;
                DrawPlainText(sprite,
                    FitText($"[{entryIndex + 1}] {entry}", 118),
                    new Vector2(popupPosition.X + 12, popupPosition.Y + 52 + (i * 18)),
                    color,
                    0.56f);
            }
        }

        private void DrawAuxiliaryPopup(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            AuxiliaryPopupVisual visual = GetActivePopupVisual();
            if (visual?.Frame == null)
            {
                return;
            }

            Point popupPosition = GetAuxiliaryPopupPosition(visual);
            visual.Frame.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                popupPosition.X,
                popupPosition.Y,
                Color.White,
                false,
                drawReflectionInfo);

            foreach (PageLayer layer in visual.Layers)
            {
                layer.Texture?.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    popupPosition.X + layer.Offset.X,
                    popupPosition.Y + layer.Offset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }

            if (_font == null)
            {
                return;
            }

            switch (_activePopup)
            {
                case AuxiliaryPopupKind.Item:
                    DrawItemPopupContents(sprite, popupPosition);
                    break;
                case AuxiliaryPopupKind.Wish:
                    DrawWishPopupContents(sprite, popupPosition);
                    break;
            }
        }

        private void DrawItemPopupContents(SpriteBatch sprite, Point popupPosition)
        {
            DrawPlainText(sprite, "Preview windows", new Vector2(popupPosition.X + 12, popupPosition.Y + 28), SecondaryColor, 0.58f);
            for (int i = 0; i < _itemPopupEntries.Length; i++)
            {
                Rectangle rowBounds = GetItemPopupRowBounds(i);
                bool selected = i == _selectedItemPopupIndex;
                DrawPlainText(
                    sprite,
                    FitText(_itemPopupEntries[i], 110),
                    new Vector2(rowBounds.X + 6, rowBounds.Y + 3),
                    selected ? SuccessColor : ValueColor,
                    0.58f);
                DrawPlainText(
                    sprite,
                    GetItemPopupDescription(i),
                    new Vector2(rowBounds.X + 14, rowBounds.Y + 17),
                    MutedColor,
                    0.48f);
            }
        }

        private void DrawWishPopupContents(SpriteBatch sprite, Point popupPosition)
        {
            DrawPlainText(sprite, "Selected gifts", new Vector2(popupPosition.X + 12, popupPosition.Y + 28), SecondaryColor, 0.58f);
            for (int i = 0; i < _wishEntries.Count; i++)
            {
                Rectangle rowBounds = GetWishPopupRowBounds(i);
                bool selected = i == _selectedWishIndex;
                DrawPlainText(
                    sprite,
                    FitText(_wishEntries[i], 118),
                    new Vector2(rowBounds.X + 6, rowBounds.Y + 3),
                    selected ? SuccessColor : ValueColor,
                    0.58f);
            }

            DrawPlainText(
                sprite,
                "Present sends a local preview only.",
                new Vector2(popupPosition.X + 12, popupPosition.Y + 112),
                MutedColor,
                0.5f);
        }

        private void BindActionButton(UIObject button, string message, Action action = null, bool trackAsPrimary = false)
        {
            if (button == null)
            {
                return;
            }

            if (trackAsPrimary)
            {
                _primaryButtons.Add(button);
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
            _exceptionPopupOpen = false;
            _activePopup = AuxiliaryPopupKind.None;

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
                button.ButtonVisible = _isBigBang;
                bool pageAvailable = IsPageAvailable(page);
                button.SetEnabled(pageAvailable);
                if (pageAvailable)
                {
                    button.SetButtonState(_currentPage == page ? UIObjectState.Pressed : UIObjectState.Normal);
                }
            }

            bool characterPage = _currentPage == UserInfoPage.Character;
            foreach (UIObject button in _primaryButtons)
            {
                if (button == null)
                {
                    continue;
                }

                button.ButtonVisible = characterPage;
                button.SetEnabled(characterPage && !_exceptionPopupOpen);
            }

            if (_petExceptionButton != null)
            {
                bool showPetException = _currentPage == UserInfoPage.Pet;
                _petExceptionButton.ButtonVisible = showPetException;
                _petExceptionButton.SetEnabled(showPetException && HasActivePets() && !_exceptionPopupOpen);
            }

            if (_collectSortButton != null)
            {
                bool showCollectButtons = _currentPage == UserInfoPage.Collect;
                _collectSortButton.ButtonVisible = showCollectButtons;
                _collectSortButton.SetEnabled(showCollectButtons && !_exceptionPopupOpen);
            }

            if (_collectClaimButton != null)
            {
                bool showCollectButtons = _currentPage == UserInfoPage.Collect;
                _collectClaimButton.ButtonVisible = showCollectButtons;
                _collectClaimButton.SetEnabled(showCollectButtons && CanClaimCollectReward() && !_collectRewardClaimed && !_exceptionPopupOpen);
            }

            if (_exceptionRegisterButton != null)
            {
                _exceptionRegisterButton.ButtonVisible = _exceptionPopupOpen;
                _exceptionRegisterButton.SetEnabled(_exceptionPopupOpen);
            }

            if (_exceptionDeleteButton != null)
            {
                _exceptionDeleteButton.ButtonVisible = _exceptionPopupOpen;
                _exceptionDeleteButton.SetEnabled(_exceptionPopupOpen && _selectedPetExceptionIndex >= 0 && _selectedPetExceptionIndex < _petExceptionEntries.Count);
            }

            if (_exceptionMesoButton != null)
            {
                _exceptionMesoButton.ButtonVisible = _exceptionPopupOpen;
                _exceptionMesoButton.SetEnabled(_exceptionPopupOpen);
                _exceptionMesoButton.SetButtonState(_petExceptionBlocksMeso ? UIObjectState.Pressed : UIObjectState.Normal);
            }

            if (_wishPresentButton != null)
            {
                bool wishOpen = _activePopup == AuxiliaryPopupKind.Wish;
                _wishPresentButton.ButtonVisible = wishOpen;
                _wishPresentButton.SetEnabled(wishOpen && _wishEntries.Count > 0);
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

        private bool HasActivePets()
        {
            return _petController?.ActivePets?.Count > 0;
        }

        private bool IsPageAvailable(UserInfoPage page)
        {
            return page switch
            {
                UserInfoPage.Ride => _characterBuild?.HasMonsterRiding == true || !string.Equals(GetEquippedItemName(EquipSlot.TamingMob), "-", StringComparison.Ordinal),
                UserInfoPage.Pet => HasActivePets(),
                UserInfoPage.Collect => true,
                UserInfoPage.Personality => true,
                _ => true
            };
        }

        private void ToggleExceptionPopup()
        {
            _activePopup = AuxiliaryPopupKind.None;
            _exceptionPopupOpen = !_exceptionPopupOpen;
            if (_exceptionPopupOpen && _petExceptionEntries.Count > 0 && _selectedPetExceptionIndex < 0)
            {
                _selectedPetExceptionIndex = 0;
            }

            UpdateButtonStates();
        }

        private void ToggleItemPopup()
        {
            ToggleAuxiliaryPopup(AuxiliaryPopupKind.Item);
        }

        private void ToggleWishPopup()
        {
            ToggleAuxiliaryPopup(AuxiliaryPopupKind.Wish);
        }

        private void ToggleAuxiliaryPopup(AuxiliaryPopupKind popupKind)
        {
            _exceptionPopupOpen = false;
            _activePopup = _activePopup == popupKind ? AuxiliaryPopupKind.None : popupKind;
            UpdateButtonStates();
        }

        private void RegisterPetExceptionEntry()
        {
            string candidate = ResolveNextPetExceptionEntry();
            if (_petExceptionEntries.Any(entry => string.Equals(entry, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                _statusMessage = $"{candidate} is already in the pet exception list.";
            }
            else
            {
                _petExceptionEntries.Add(candidate);
                _selectedPetExceptionIndex = _petExceptionEntries.Count - 1;
                _statusMessage = $"{candidate} added to the pet exception list.";
            }

            UpdateButtonStates();
        }

        private void DeletePetExceptionEntry()
        {
            if (_selectedPetExceptionIndex < 0 || _selectedPetExceptionIndex >= _petExceptionEntries.Count)
            {
                _statusMessage = "No pet exception entry is selected.";
                return;
            }

            string removed = _petExceptionEntries[_selectedPetExceptionIndex];
            _petExceptionEntries.RemoveAt(_selectedPetExceptionIndex);
            _selectedPetExceptionIndex = _petExceptionEntries.Count > 0 ? Math.Min(_selectedPetExceptionIndex, _petExceptionEntries.Count - 1) : -1;
            _statusMessage = $"{removed} removed from the pet exception list.";
            UpdateButtonStates();
        }

        private void TogglePetMesoException()
        {
            _petExceptionBlocksMeso = !_petExceptionBlocksMeso;
            _statusMessage = _petExceptionBlocksMeso
                ? "Pets will skip meso pickup while the exception list is active."
                : "Pets may pick up meso while the exception list is active.";
            UpdateButtonStates();
        }

        private void ToggleCollectSortMode()
        {
            _collectSortByName = !_collectSortByName;
            _statusMessage = _collectSortByName
                ? "Collection summary sorted by name."
                : "Collection summary sorted by equipped slot order.";
        }

        private void ClaimCollectReward()
        {
            if (!CanClaimCollectReward())
            {
                _statusMessage = "Collection reward is not ready yet.";
                return;
            }

            if (_collectRewardClaimed)
            {
                _statusMessage = "Collection reward has already been claimed in this simulator session.";
                return;
            }

            _collectRewardClaimed = true;
            _statusMessage = "Collection reward claim acknowledged locally.";
            UpdateButtonStates();
        }

        private void PresentWishEntry()
        {
            if (_selectedWishIndex < 0 || _selectedWishIndex >= _wishEntries.Count)
            {
                _statusMessage = "Select a wish entry before previewing a present.";
                return;
            }

            _statusMessage = $"Present preview prepared for {_wishEntries[_selectedWishIndex]}. Packet-backed gifting still remains.";
        }

        private bool CanClaimCollectReward()
        {
            return (_characterBuild?.Equipment?.Count ?? 0) >= 5;
        }

        private List<(string Label, string Value)> BuildCollectEntries()
        {
            List<(string Label, string Value)> entries = new List<(string Label, string Value)>
            {
                ("Equipped", (_characterBuild?.Equipment?.Count ?? 0).ToString()),
                ("Weapon", _characterBuild?.GetWeapon()?.Name ?? "-"),
                ("Cap", GetEquippedItemName(EquipSlot.Cap)),
                ("Outfit", ResolveOutfitName()),
                ("Shoes", GetEquippedItemName(EquipSlot.Shoes)),
                ("EXP", _characterBuild?.ExpDisplayText ?? "-")
            };

            return _collectSortByName
                ? entries.OrderBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase).ToList()
                : entries;
        }

        private string ResolveNextPetExceptionEntry()
        {
            string[] candidates =
            {
                "Meso Bag",
                "Arrow Bundle",
                "Throwing Star",
                "Pet Food",
                "Return Scroll"
            };

            foreach (string candidate in candidates)
            {
                if (!_petExceptionEntries.Any(entry => string.Equals(entry, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }

            return $"Filtered Item {_petExceptionEntries.Count + 1}";
        }

        private AuxiliaryPopupVisual CreateAuxiliaryPopupVisual(IDXObject frame, IEnumerable<(IDXObject layer, Point offset)> layers)
        {
            AuxiliaryPopupVisual visual = new AuxiliaryPopupVisual
            {
                Frame = frame
            };

            if (layers == null)
            {
                return visual;
            }

            foreach ((IDXObject layer, Point offset) in layers)
            {
                if (layer != null)
                {
                    visual.Layers.Add(new PageLayer(layer, offset));
                }
            }

            return visual;
        }

        private void HandlePopupMouseInput()
        {
            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                _previousMouseState.LeftButton == ButtonState.Pressed;
            if (!leftReleased || _currentPage != UserInfoPage.Character)
            {
                return;
            }

            switch (_activePopup)
            {
                case AuxiliaryPopupKind.Item:
                    HandleItemPopupClick(mouseState.Position);
                    break;
                case AuxiliaryPopupKind.Wish:
                    HandleWishPopupClick(mouseState.Position);
                    break;
            }
        }

        private void HandleItemPopupClick(Point mousePosition)
        {
            for (int i = 0; i < _itemPopupEntries.Length; i++)
            {
                if (!GetItemPopupRowBounds(i).Contains(mousePosition))
                {
                    continue;
                }

                _selectedItemPopupIndex = i;
                OpenItemPopupSelection(i);
                return;
            }
        }

        private void HandleWishPopupClick(Point mousePosition)
        {
            for (int i = 0; i < _wishEntries.Count; i++)
            {
                if (GetWishPopupRowBounds(i).Contains(mousePosition))
                {
                    _selectedWishIndex = i;
                    return;
                }
            }
        }

        private void OpenItemPopupSelection(int index)
        {
            switch (index)
            {
                case 0:
                    MiniRoomRequested?.Invoke();
                    _statusMessage = "Mini-room preview opened from the item list.";
                    break;
                case 1:
                    PersonalShopRequested?.Invoke();
                    _statusMessage = "Personal-shop preview opened from the item list.";
                    break;
                case 2:
                    EntrustedShopRequested?.Invoke();
                    _statusMessage = "Entrusted-shop preview opened from the item list.";
                    break;
                default:
                    return;
            }

            _activePopup = AuxiliaryPopupKind.None;
            UpdateButtonStates();
        }

        private AuxiliaryPopupVisual GetActivePopupVisual()
        {
            return _activePopup switch
            {
                AuxiliaryPopupKind.Item => _itemPopupVisual,
                AuxiliaryPopupKind.Wish => _wishPopupVisual,
                _ => null
            };
        }

        private Point GetAuxiliaryPopupPosition(AuxiliaryPopupVisual visual)
        {
            if (visual?.Frame == null)
            {
                return Position;
            }

            int x = Position.X + Math.Max(8, CurrentFrame.Width - visual.Frame.Width - 8);
            int y = Position.Y + 30;
            return new Point(x, y);
        }

        private Rectangle GetItemPopupRowBounds(int index)
        {
            Point popupPosition = GetAuxiliaryPopupPosition(_itemPopupVisual);
            return new Rectangle(popupPosition.X + 10, popupPosition.Y + 46 + (index * 24), 134, 22);
        }

        private Rectangle GetWishPopupRowBounds(int index)
        {
            Point popupPosition = GetAuxiliaryPopupPosition(_wishPopupVisual);
            return new Rectangle(popupPosition.X + 10, popupPosition.Y + 46 + (index * 20), 138, 18);
        }

        private static string GetItemPopupDescription(int index)
        {
            return index switch
            {
                0 => "Room shell",
                1 => "Sale bundles",
                2 => "Ledger",
                _ => string.Empty
            };
        }

        private Point GetExceptionPopupPosition()
        {
            if (_exceptionPopupVisual?.Frame == null)
            {
                return Position;
            }

            int x = Position.X + Math.Max(8, CurrentFrame.Width - _exceptionPopupVisual.Frame.Width - 8);
            int y = Position.Y + 30;
            return new Point(x, y);
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
