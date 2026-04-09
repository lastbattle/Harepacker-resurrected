using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        private enum LegacyExpandedPanel
        {
            None,
            Pet,
            Ride,
            Collection
        }

        private enum LegacyCollectionMode
        {
            Overview,
            Book
        }

        private readonly struct PageLayer
        {
            public PageLayer(string name, IDXObject texture, Point offset)
            {
                Name = name ?? string.Empty;
                Texture = texture;
                Offset = offset;
            }

            public PageLayer(IDXObject texture, Point offset)
                : this(string.Empty, texture, offset)
            {
            }

            public string Name { get; }
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

        private sealed class PersonalityTooltipVisual
        {
            public IDXObject BaseTop { get; set; }
            public IDXObject BaseMiddle { get; set; }
            public IDXObject BaseBottom { get; set; }
            public IDXObject Title { get; set; }
            public IDXObject CharmCollectionBody { get; set; }
            public Dictionary<string, IDXObject> BodyByTrait { get; } = new Dictionary<string, IDXObject>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<char, IDXObject> NumberGlyphs { get; } = new Dictionary<char, IDXObject>();
        }

        public readonly struct RankDeltaSnapshot
        {
            public RankDeltaSnapshot(int? previousWorldRank, int? previousJobRank)
            {
                PreviousWorldRank = previousWorldRank;
                PreviousJobRank = previousJobRank;
            }

            public int? PreviousWorldRank { get; }
            public int? PreviousJobRank { get; }
        }

        public sealed class UserInfoInspectionTarget
        {
            public CharacterBuild Build { get; init; }
            public int CharacterId { get; init; }
            public string Name { get; init; } = string.Empty;
            public string LocationSummary { get; init; } = string.Empty;
            public int Channel { get; init; } = 1;
        }

        public readonly struct UserInfoActionContext
        {
            public UserInfoActionContext(bool isRemoteTarget, int characterId, string characterName, CharacterBuild build, string locationSummary, int channel)
            {
                IsRemoteTarget = isRemoteTarget;
                CharacterId = characterId;
                CharacterName = characterName ?? string.Empty;
                Build = build;
                LocationSummary = locationSummary ?? string.Empty;
                Channel = channel;
            }

            public bool IsRemoteTarget { get; }
            public int CharacterId { get; }
            public string CharacterName { get; }
            public CharacterBuild Build { get; }
            public string LocationSummary { get; }
            public int Channel { get; }
        }

        public enum PopularityChangeDirection
        {
            Up,
            Down
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
        private readonly List<UIObject> _petTabButtons = new List<UIObject>();
        private readonly List<UIObject> _primaryButtons = new List<UIObject>();
        private readonly Dictionary<UserInfoPage, PageVisual> _pageVisuals = new Dictionary<UserInfoPage, PageVisual>();
        private readonly Dictionary<LegacyExpandedPanel, IDXObject> _legacyFrames = new Dictionary<LegacyExpandedPanel, IDXObject>();
        private readonly List<UIObject> _legacyPetButtons = new List<UIObject>();
        private readonly List<string> _petExceptionEntries = new List<string> { "Meso Bag", "Throwing Star" };
        private readonly string[] _itemPopupEntries = { "Mini Room", "Personal Shop", "Entrusted Shop" };
        private readonly List<string> _wishEntries = new List<string> { "White Scroll", "Brown Work Gloves", "Ilbi Throwing-Star" };
        private readonly Dictionary<ItemMakerRecipeFamily, IDXObject> _productSkillIcons = new Dictionary<ItemMakerRecipeFamily, IDXObject>();
        private readonly Dictionary<int, Texture2D> _itemIconCache = new Dictionary<int, Texture2D>();
        private readonly HashSet<string> _collectRewardClaims = new HashSet<string>(StringComparer.Ordinal);
        private IDXObject _productSkillRecipeIcon;
        private IDXObject _marriedIcon;

        private IDXObject _foreground;
        private Point _foregroundOffset;
        private IDXObject _nameBanner;
        private Point _nameBannerOffset;
        private SpriteFont _font;
        private ClientTextRasterizer _clientTextRasterizer;
        private CharacterBuild _characterBuild;
        private PetController _petController;
        private UserInfoInspectionTarget _inspectionTarget;
        private UserInfoPage _currentPage = UserInfoPage.Character;
        private string _statusMessage = "Character actions are available from this profile window.";
        private UIObject _partyButton;
        private UIObject _followButton;
        private UIObject _tradeButton;
        private UIObject _itemButton;
        private UIObject _wishButton;
        private UIObject _familyButton;
        private UIObject _petExceptionButton;
        private UIObject _collectSortButton;
        private UIObject _collectClaimButton;
        private UIObject _legacyPetShowButton;
        private UIObject _legacyPetHideButton;
        private UIObject _legacyRideShowButton;
        private UIObject _legacyRideHideButton;
        private UIObject _legacyBookShowButton;
        private UIObject _legacyBookHideButton;
        private UIObject _legacyCollectionShowButton;
        private UIObject _legacyCollectionHideButton;
        private UIObject _legacyExceptionShowButton;
        private UIObject _legacyExceptionHideButton;
        private UIObject _legacyPresentButton;
        private ExceptionPopupVisual _exceptionPopupVisual;
        private UIObject _exceptionRegisterButton;
        private UIObject _exceptionDeleteButton;
        private UIObject _exceptionMesoButton;
        private bool _exceptionPopupOpen;
        private AuxiliaryPopupVisual _itemPopupVisual;
        private AuxiliaryPopupVisual _wishPopupVisual;
        private UIObject _wishPresentButton;
        private UIObject _popupUpButton;
        private UIObject _popupDownButton;
        private PersonalityTooltipVisual _personalityTooltipVisual;
        private Func<CharacterBuild, ItemMakerProgressionSnapshot> _collectionSnapshotProvider;
        private Func<CharacterBuild, MonsterBookSnapshot> _monsterBookSnapshotProvider;
        private Func<CharacterBuild, RankDeltaSnapshot> _rankDeltaProvider;
        private AuxiliaryPopupKind _activePopup;
        private LegacyExpandedPanel _legacyExpandedPanel;
        private LegacyCollectionMode _legacyCollectionMode = LegacyCollectionMode.Overview;
        private int _selectedPetTabIndex;
        private int _selectedItemPopupIndex;
        private int _selectedWishIndex;
        private bool _petExceptionBlocksMeso = true;
        private int _selectedPetExceptionIndex = -1;
        private bool _collectSortByName;
        private MouseState _previousMouseState;
        private CharacterBuild _snapshotCacheBuild;
        private ItemMakerProgressionSnapshot _currentCollectionSnapshot = ItemMakerProgressionSnapshot.Default;
        private MonsterBookSnapshot _currentMonsterBookSnapshot = new MonsterBookSnapshot();
        private RankDeltaSnapshot _currentRankDeltaSnapshot;
        private bool _isMarriedProfile;

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
        public Func<UserInfoActionContext, string> PartyRequested { get; set; }
        public Func<UserInfoActionContext, string> FollowRequested { get; set; }
        public Func<UserInfoActionContext, string> TradingRoomRequested { get; set; }
        public Func<UserInfoActionContext, string> FamilyRequested { get; set; }
        public Func<UserInfoActionContext, PopularityChangeDirection, string> PopularityRequested { get; set; }
        public Action PersonalShopRequested { get; set; }
        public Action EntrustedShopRequested { get; set; }
        public Func<UserInfoActionContext, string> BookCollectionRequested { get; set; }
        public Func<UserInfoActionContext, string, string> WishPresentRequested { get; set; }
        public Func<UserInfoActionContext, bool> MarriedBadgeProvider { get; set; }
        public Func<string> LocalActionLocationSummaryProvider { get; set; }
        public Func<int> LocalActionChannelProvider { get; set; }
        public Func<UserInfoInspectionTarget, UserInfoInspectionTarget> InspectionTargetResolver { get; set; }

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

        public void AddPageLayer(string pageName, IDXObject layer, int offsetX, int offsetY, string layerName = null)
        {
            if (layer == null || !TryGetPage(pageName, out UserInfoPage page))
            {
                return;
            }

            GetOrCreateVisual(page).Layers.Add(new PageLayer(layerName, layer, new Point(offsetX, offsetY)));
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

        public void RegisterLegacyFrame(string panelName, IDXObject frame)
        {
            if (frame == null)
            {
                return;
            }

            if (!Enum.TryParse(panelName, true, out LegacyExpandedPanel panel) || panel == LegacyExpandedPanel.None)
            {
                return;
            }

            _legacyFrames[panel] = frame;
            if (!_isBigBang)
            {
                ApplyCurrentPageFrame();
            }
        }

        public void InitializePrimaryButtons(
            UIObject partyButton,
            UIObject tradeButton,
            UIObject itemButton,
            UIObject wishButton,
            UIObject familyButton,
            UIObject followButton = null)
        {
            _partyButton = partyButton;
            _followButton = followButton;
            _tradeButton = tradeButton;
            _itemButton = itemButton;
            _wishButton = wishButton;
            _familyButton = familyButton;

            if (_followButton != null)
            {
                UIObject anchorButton = _itemButton ?? _wishButton;
                if (anchorButton != null)
                {
                    _followButton.X = anchorButton.X + Math.Max(0, (anchorButton.CanvasSnapshotWidth - _followButton.CanvasSnapshotWidth) / 2);
                    _followButton.Y = anchorButton.Y + Math.Max(0, (anchorButton.CanvasSnapshotHeight - _followButton.CanvasSnapshotHeight) / 2);
                }
            }

            BindActionButton(partyButton, "Party list opened from the profile window.", RequestPartyAction, true);
            BindActionButton(followButton, "Follow request prepared from the remote profile seam.", RequestFollowAction, true);
            BindActionButton(tradeButton, "Trading-room shell opened.", RequestTradeAction, true);
            BindActionButton(itemButton, "Item list opened.", ToggleItemPopup, true);
            BindActionButton(wishButton, "Wish list opened.", ToggleWishPopup, true);
            BindActionButton(familyButton, "Family chart opened from the profile window.", RequestFamilyAction, true);
        }

        public void InitializePopupScrollButtons(UIObject popupUpButton, UIObject popupDownButton)
        {
            _popupUpButton = popupUpButton;
            _popupDownButton = popupDownButton;
            RegisterPrimaryButton(_popupUpButton, HandlePopupUpButton);
            RegisterPrimaryButton(_popupDownButton, HandlePopupDownButton);
            UpdateButtonStates();
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
            BindActionButton(_collectClaimButton, "Collection reward claimed.", ClaimCollectReward);
            UpdateButtonStates();
        }

        public void InitializeLegacyButtons(
            UIObject petShowButton,
            UIObject petHideButton,
            UIObject rideShowButton,
            UIObject rideHideButton,
            UIObject bookShowButton,
            UIObject bookHideButton,
            UIObject collectionShowButton,
            UIObject collectionHideButton,
            UIObject exceptionShowButton,
            UIObject exceptionHideButton,
            UIObject presentButton)
        {
            _legacyPetShowButton = petShowButton;
            _legacyPetHideButton = petHideButton;
            _legacyRideShowButton = rideShowButton;
            _legacyRideHideButton = rideHideButton;
            _legacyBookShowButton = bookShowButton;
            _legacyBookHideButton = bookHideButton;
            _legacyCollectionShowButton = collectionShowButton;
            _legacyCollectionHideButton = collectionHideButton;
            _legacyExceptionShowButton = exceptionShowButton;
            _legacyExceptionHideButton = exceptionHideButton;
            _legacyPresentButton = presentButton;

            BindActionButton(_legacyPetShowButton, "Pet add-on opened.", () => OpenLegacyPanel(LegacyExpandedPanel.Pet));
            BindActionButton(_legacyPetHideButton, "Pet add-on closed.", CloseLegacyPanel);
            BindActionButton(_legacyRideShowButton, "Ride add-on opened.", () => OpenLegacyPanel(LegacyExpandedPanel.Ride));
            BindActionButton(_legacyRideHideButton, "Ride add-on closed.", CloseLegacyPanel);
            BindActionButton(_legacyBookShowButton, "Monster Book add-on opened.", () => OpenLegacyCollectionMode(LegacyCollectionMode.Book));
            BindActionButton(_legacyBookHideButton, "Monster Book add-on closed.", CloseLegacyPanel);
            BindActionButton(_legacyCollectionShowButton, "Collection add-on opened.", () => OpenLegacyCollectionMode(LegacyCollectionMode.Overview));
            BindActionButton(_legacyCollectionHideButton, "Collection add-on closed.", CloseLegacyPanel);
            BindActionButton(_legacyExceptionShowButton, "Pet exception list opened.", ToggleExceptionPopup);
            BindActionButton(_legacyExceptionHideButton, "Pet exception list closed.", ToggleExceptionPopup);
            BindActionButton(_legacyPresentButton, "Wish present routing prepared.", PresentWishEntry);
            UpdateButtonStates();
        }

        public void InitializePetTabButtons(IEnumerable<UIObject> petTabButtons)
        {
            _petTabButtons.Clear();
            if (petTabButtons == null)
            {
                return;
            }

            int tabIndex = 0;
            foreach (UIObject button in petTabButtons)
            {
                if (button == null)
                {
                    tabIndex++;
                    continue;
                }

                int capturedIndex = tabIndex;
                _petTabButtons.Add(button);
                AddButton(button);
                button.ButtonClickReleased += _ =>
                {
                    if (capturedIndex >= GetAvailablePetSlotCount())
                    {
                        return;
                    }

                    _selectedPetTabIndex = capturedIndex;
                    _statusMessage = $"Pet {capturedIndex + 1} tab selected.";
                    UpdateButtonStates();
                };
                tabIndex++;
            }

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

        public void InitializePopularityButtons(UIObject popupUpButton, UIObject popupDownButton)
        {
            _popupUpButton = popupUpButton;
            _popupDownButton = popupDownButton;

            BindActionButton(_popupUpButton, "Popularity up request prepared.", () => RequestPopularityChange(PopularityChangeDirection.Up));
            BindActionButton(_popupDownButton, "Popularity down request prepared.", () => RequestPopularityChange(PopularityChangeDirection.Down));
            UpdateButtonStates();
        }

        public void SetCollectionSnapshotProvider(Func<CharacterBuild, ItemMakerProgressionSnapshot> snapshotProvider)
        {
            _collectionSnapshotProvider = snapshotProvider;
        }

        public void SetMonsterBookSnapshotProvider(Func<CharacterBuild, MonsterBookSnapshot> snapshotProvider)
        {
            _monsterBookSnapshotProvider = snapshotProvider;
        }

        public void SetRankDeltaProvider(Func<CharacterBuild, RankDeltaSnapshot> rankDeltaProvider)
        {
            _rankDeltaProvider = rankDeltaProvider;
        }

        public void SetProductSkillIcon(ItemMakerRecipeFamily family, IDXObject icon)
        {
            if (icon != null)
            {
                _productSkillIcons[family] = icon;
            }
        }

        public void SetProductSkillRecipeIcon(IDXObject icon)
        {
            _productSkillRecipeIcon = icon;
        }

        public void SetMarriedIcon(IDXObject icon)
        {
            _marriedIcon = icon;
        }

        public void InitializePersonalityTooltip(
            IDXObject baseTop,
            IDXObject baseMiddle,
            IDXObject baseBottom,
            IDXObject title,
            IDictionary<string, IDXObject> bodyByTrait,
            IDXObject charmCollectionBody = null,
            IDictionary<char, IDXObject> numberGlyphs = null)
        {
            _personalityTooltipVisual = new PersonalityTooltipVisual
            {
                BaseTop = baseTop,
                BaseMiddle = baseMiddle,
                BaseBottom = baseBottom,
                Title = title,
                CharmCollectionBody = charmCollectionBody
            };

            if (bodyByTrait == null)
            {
                return;
            }

            foreach (KeyValuePair<string, IDXObject> entry in bodyByTrait)
            {
                if (entry.Value != null && !string.IsNullOrWhiteSpace(entry.Key))
                {
                    _personalityTooltipVisual.BodyByTrait[entry.Key] = entry.Value;
                }
            }

            if (numberGlyphs == null)
            {
                return;
            }

            foreach (KeyValuePair<char, IDXObject> entry in numberGlyphs)
            {
                if (entry.Value != null)
                {
                    _personalityTooltipVisual.NumberGlyphs[entry.Key] = entry.Value;
                }
            }
        }

        public void SetPetController(PetController petController)
        {
            _petController = petController;
        }

        public void SetInspectionTarget(UserInfoInspectionTarget inspectionTarget)
        {
            _inspectionTarget = inspectionTarget;
            _statusMessage = inspectionTarget?.Build == null
                ? "Character actions are available from this profile window."
                : $"Inspecting {inspectionTarget.Name}. Remote follow, party, trade, family, and popularity requests now route through the simulator seams.";
            _exceptionPopupOpen = false;
            _activePopup = AuxiliaryPopupKind.None;
            if (!IsPageAvailable(_currentPage))
            {
                _currentPage = UserInfoPage.Character;
                ApplyCurrentPageFrame();
            }

            RefreshMarriageBadgeState();
            UpdateButtonStates();
        }

        public void ClearInspectionTarget()
        {
            _inspectionTarget = null;
            _statusMessage = "Character actions are available from this profile window.";
            RefreshMarriageBadgeState();
            UpdateButtonStates();
        }

        public override void Update(GameTime gameTime)
        {
            RefreshSnapshotCaches();
            ClampSelectedPetTabIndex();
            RefreshMarriageBadgeState();

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
            RefreshSnapshotCaches();
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
                    if (!_isBigBang)
                    {
                        DrawLegacyExpandedPanel(sprite);
                    }
                    break;
            }

            DrawExceptionPopup(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
            DrawAuxiliaryPopup(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
            DrawPersonalityTooltip(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
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

            Vector2 textSize = MeasureText(sprite, text, scale);
            Vector2 position = new Vector2(
                bounds.X + Math.Max(0f, (bounds.Width - textSize.X) * 0.5f),
                bounds.Y + Math.Max(0f, (bounds.Height - textSize.Y) * 0.5f) - 1f);
            DrawText(sprite, text, position, color, scale);
        }

        private void DrawPlainText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            DrawText(sprite, text, position, color, scale);
        }

        private void DrawCurrentPageVisuals(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            CharacterBuild displayBuild = GetDisplayedBuild();
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
                if (_currentPage == UserInfoPage.Personality &&
                    string.Equals(layer.Name, "before30level", StringComparison.OrdinalIgnoreCase) &&
                    (displayBuild?.Level ?? 0) >= 30)
                {
                    continue;
                }

                DrawLayer(layer.Texture, layer.Offset, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
            }

            DrawLayer(visual.Icon, visual.IconOffset, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
        }

        private void DrawCharacterPage(SpriteBatch sprite)
        {
            CharacterBuild displayBuild = GetDisplayedBuild();
            string inspectionBanner = BuildInspectionBanner();
            if (!string.IsNullOrWhiteSpace(inspectionBanner))
            {
                DrawPlainText(sprite, FitText(inspectionBanner, 150), new Vector2(Position.X + 18, Position.Y + 96), WarningColor, 0.48f);
            }

            string name = displayBuild?.Name;
            string job = displayBuild?.JobName;
            string level = displayBuild != null ? displayBuild.Level.ToString() : "-";
            string fame = displayBuild != null ? displayBuild.Fame.ToString() : "-";
            RankDeltaSnapshot rankSnapshot = GetRankDeltaSnapshot();
            string rank = FormatRank(displayBuild?.WorldRank ?? 0, rankSnapshot.PreviousWorldRank);
            string jobRank = FormatRank(displayBuild?.JobRank ?? 0, rankSnapshot.PreviousJobRank);
            string guild = displayBuild?.GuildDisplayText ?? "-";
            string alliance = displayBuild?.AllianceDisplayText ?? "-";
            string guildAlliance = guild == "-" && alliance == "-"
                ? "-"
                : alliance == "-"
                    ? guild
                    : $"{guild} / {alliance}";

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
                    jobRank,
                    fame,
                    guildAlliance);
                DrawMarriageBadge(sprite);
                DrawProductSkillSummary(sprite);
                DrawProfileProgressSummary(sprite);
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
                rank,
                jobRank,
                fame,
                guildAlliance);
            DrawProductSkillSummary(sprite);
            DrawProfileProgressSummary(sprite);
        }

        private void DrawRidePage(SpriteBatch sprite)
        {
            CharacterBuild displayBuild = GetDisplayedBuild();
            string mountName = GetEquippedItemName(EquipSlot.TamingMob, displayBuild);
            string saddleName = GetEquippedItemName(EquipSlot.Saddle, displayBuild);
            bool ridingReady = displayBuild?.HasMonsterRiding == true && !string.Equals(mountName, "-", StringComparison.Ordinal);

            DrawSectionHeader(sprite, "Ride");
            DrawLabeledRow(sprite, 42, "Status", ridingReady ? "Ride available" : "No active mount slot", ridingReady ? SuccessColor : WarningColor);
            DrawLabeledRow(sprite, 66, "Mount", mountName, ValueColor);
            DrawLabeledRow(sprite, 90, "Saddle", saddleName, ValueColor);
            DrawLabeledRow(sprite, 114, "Job", displayBuild?.JobName ?? "-", ValueColor);
            DrawLabeledRow(sprite, 138, "Notes", ridingReady
                ? "Taming-mob equipment is present in the simulator build."
                : "Equip a taming mob and saddle to mirror the client ride page.", MutedColor, 144);
        }

        private void DrawPetPage(SpriteBatch sprite)
        {
            DrawSectionHeader(sprite, "Pet");
            if (TryGetSelectedLocalPet(out PetRuntime pet, out int localPetCount))
            {
                DrawLayer(pet.Definition?.IconRaw ?? pet.Definition?.Icon,
                    new Point(20, 52),
                    sprite,
                    null,
                    null,
                    null);
                DrawLabeledRow(sprite, 50, "Pet", pet.Name, ValueColor, 132);
                DrawLabeledRow(sprite, 74, "Level", $"Command Lv. {pet.CommandLevel}", MutedColor, 132);
                DrawLabeledRow(sprite, 98, "Loot", pet.AutoLootEnabled ? "Auto-loot enabled" : "Auto-loot disabled",
                    pet.AutoLootEnabled ? SuccessColor : WarningColor, 132);
                DrawLabeledRow(sprite, 122, "Chat", $"Balloon {pet.ChatBalloonStyle}", MutedColor, 132);
                DrawLabeledRow(sprite, 146, "Active", $"{localPetCount} pet slot(s) populated", SecondaryColor, 132);
                DrawPlainText(
                    sprite,
                    $"Viewing pet {_selectedPetTabIndex + 1} of {localPetCount}.",
                    new Vector2(Position.X + 20, Position.Y + 174),
                    MutedColor,
                    0.56f);
                return;
            }

            if (TryGetSelectedRemotePetItemId(out int remotePetItemId, out int remotePetCount))
            {
                string petName = ResolveRemotePetDisplayName(remotePetItemId);
                Texture2D petIcon = TryResolveItemIcon(sprite, remotePetItemId);
                if (petIcon != null)
                {
                    sprite.Draw(
                        petIcon,
                        new Rectangle(Position.X + 20, Position.Y + 52, 32, 32),
                        Color.White);
                }

                DrawLabeledRow(sprite, 50, "Pet", petName, ValueColor, 132);
                DrawLabeledRow(sprite, 74, "Slot", $"Remote pet slot {_selectedPetTabIndex + 1}", MutedColor, 132);
                DrawLabeledRow(sprite, 98, "Source", "Remote AvatarLook / user packet", SecondaryColor, 132);
                DrawLabeledRow(sprite, 122, "State", "Live server-owned pet state unavailable", WarningColor, 132);
                DrawLabeledRow(sprite, 146, "Active", $"{remotePetCount} remote pet slot(s) authored", SecondaryColor, 132);
                DrawPlainText(
                    sprite,
                    $"Viewing remote pet {_selectedPetTabIndex + 1} of {remotePetCount}.",
                    new Vector2(Position.X + 20, Position.Y + 174),
                    MutedColor,
                    0.56f);
                return;
            }

            if (IsRemoteInspectionActive())
            {
                DrawPlainText(sprite,
                    "No remote pet slots were present on the inspected build.",
                    new Vector2(Position.X + 20, Position.Y + 54),
                    MutedColor,
                    0.62f);
                return;
            }

            DrawPlainText(sprite,
                "No active pets. Summon a simulator pet to populate this page.",
                new Vector2(Position.X + 20, Position.Y + 54),
                MutedColor,
                0.62f);
        }

        private void DrawCollectPage(SpriteBatch sprite)
        {
            ItemMakerProgressionSnapshot progression = GetCollectionSnapshot();
            List<(string Label, string Value)> entries = BuildCollectEntries();
            MonsterBookSnapshot snapshot = GetMonsterBookSnapshot();

            DrawSectionHeader(sprite, "Collect");
            for (int i = 0; i < entries.Count; i++)
            {
                (string label, string value) = entries[i];
                DrawLabeledRow(sprite, 40 + (i * 24), label, value, ValueColor, 144);
            }

            string rewardStatus = BuildCollectStatusText(progression, snapshot);
            DrawPlainText(
                sprite,
                FitText(rewardStatus, 220),
                new Vector2(Position.X + 20, Position.Y + 188),
                CanClaimCollectReward() ? SuccessColor : MutedColor,
                0.58f);
        }

        private void DrawPersonalityPage(SpriteBatch sprite)
        {
            CharacterBuild displayBuild = GetDisplayedBuild();
            DrawSectionHeader(sprite, "Personality");

            (string Label, int Value)[] traits =
            {
                ("Charisma", displayBuild?.TraitCharisma ?? 0),
                ("Insight", displayBuild?.TraitInsight ?? 0),
                ("Will", displayBuild?.TraitWill ?? 0),
                ("Craft", displayBuild?.TraitCraft ?? 0),
                ("Sense", displayBuild?.TraitSense ?? 0),
                ("Charm", displayBuild?.TraitCharm ?? 0)
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

        private void DrawProductSkillSummary(SpriteBatch sprite)
        {
            ItemMakerProgressionSnapshot snapshot = GetCollectionSnapshot();
            if (_font == null || snapshot == null)
            {
                return;
            }

            (ItemMakerRecipeFamily family, Point position)[] iconSlots =
            {
                (ItemMakerRecipeFamily.Generic, new Point(18, 130)),
                (ItemMakerRecipeFamily.Gloves, new Point(38, 130)),
                (ItemMakerRecipeFamily.Shoes, new Point(58, 130)),
                (ItemMakerRecipeFamily.Toys, new Point(78, 130))
            };

            foreach ((ItemMakerRecipeFamily family, Point position) in iconSlots)
            {
                if (!_productSkillIcons.TryGetValue(family, out IDXObject icon) || icon == null)
                {
                    continue;
                }

                bool active = family == ItemMakerRecipeFamily.Generic
                    ? snapshot.SuccessfulCrafts > 0
                    : snapshot.GetLevel(family) > 1;
                Color tint = active ? Color.White : new Color(255, 255, 255, 96);
                icon.DrawBackground(sprite, null, null, Position.X + position.X, Position.Y + position.Y, tint, false, null);
            }

            if (_productSkillRecipeIcon != null)
            {
                bool active = snapshot.DiscoveredRecipeCount > 0 || snapshot.UnlockedHiddenRecipeCount > 0;
                Color tint = active ? Color.White : new Color(255, 255, 255, 96);
                _productSkillRecipeIcon.DrawBackground(sprite, null, null, Position.X + 98, Position.Y + 130, tint, false, null);
            }

            string makerText = snapshot.SuccessfulCrafts > 0
                ? $"Maker Lv {snapshot.GenericLevel}  Crafts {snapshot.SuccessfulCrafts}"
                : "Maker profile has no crafted items yet.";
            DrawPlainText(sprite, FitText(makerText, 116), new Vector2(Position.X + 18, Position.Y + 112), MutedColor, 0.48f);
        }

        private void DrawProfileProgressSummary(SpriteBatch sprite)
        {
            string medalText = BuildMedalSummary();
            string pocketText = BuildPocketSummary();
            DrawEquipmentSummary(sprite, EquipSlot.Medal, medalText, new Point(18, 148), new Point(40, 152));
            DrawEquipmentSummary(sprite, EquipSlot.Pocket, pocketText, new Point(18, 164), new Point(40, 166));
        }

        private void DrawMarriageBadge(SpriteBatch sprite)
        {
            if (!_isBigBang || !_isMarriedProfile || _marriedIcon == null)
            {
                return;
            }

            // CUIUserInfo::Draw copies UI/UIWindow2.img/UserInfo/character/married at (15, 32).
            _marriedIcon.DrawBackground(sprite, null, null, Position.X + 15, Position.Y + 32, Color.White, false, null);
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

        private void DrawPersonalityTooltip(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            if (_currentPage != UserInfoPage.Personality || _personalityTooltipVisual == null)
            {
                return;
            }

            string traitKey = ResolveHoveredPersonalityTrait();
            if (string.IsNullOrWhiteSpace(traitKey) ||
                !_personalityTooltipVisual.BodyByTrait.TryGetValue(traitKey, out IDXObject body) ||
                body == null)
            {
                return;
            }

            Point tooltipPosition = new Point(Position.X + 104, Position.Y + 34);
            bool drawCharmCollection = string.Equals(traitKey, "charm", StringComparison.OrdinalIgnoreCase) &&
                                       ShouldDrawCharmCollectionTooltip();
            int contentHeight = body.Height + (drawCharmCollection ? _personalityTooltipVisual.CharmCollectionBody?.Height ?? 0 : 0);
            DrawLayer(_personalityTooltipVisual.BaseTop, new Point(tooltipPosition.X - Position.X, tooltipPosition.Y - Position.Y), sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);

            int middleHeight = Math.Max(0, contentHeight - 26);
            if (_personalityTooltipVisual.BaseMiddle != null && middleHeight > 0)
            {
                Texture2D middleTexture = (_personalityTooltipVisual.BaseMiddle as DXObject)?.Texture;
                if (middleTexture != null)
                {
                    sprite.Draw(
                        middleTexture,
                        new Rectangle(tooltipPosition.X, tooltipPosition.Y + 13, middleTexture.Width, middleHeight),
                        Color.White);
                }
            }

            DrawLayer(_personalityTooltipVisual.BaseBottom, new Point(tooltipPosition.X - Position.X, tooltipPosition.Y + 13 + middleHeight - Position.Y), sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
            DrawLayer(_personalityTooltipVisual.Title, new Point(tooltipPosition.X + 7 - Position.X, tooltipPosition.Y + 4 - Position.Y), sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
            DrawLayer(body, new Point(tooltipPosition.X + 7 - Position.X, tooltipPosition.Y + 34 - Position.Y), sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
            if (drawCharmCollection)
            {
                DrawLayer(
                    _personalityTooltipVisual.CharmCollectionBody,
                    new Point(tooltipPosition.X + 7 - Position.X, tooltipPosition.Y + 34 + body.Height - Position.Y),
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    drawReflectionInfo);
            }

            if (_font == null)
            {
                return;
            }

            DrawTooltipNumber(sprite, tooltipPosition, GetPersonalityTraitValue(traitKey));
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
                "Present routes into the Cash Shop wish-list seam.",
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

        private void RegisterPrimaryButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            _primaryButtons.Add(button);
            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
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
            if (!_isBigBang &&
                _legacyExpandedPanel != LegacyExpandedPanel.None &&
                _legacyFrames.TryGetValue(_legacyExpandedPanel, out IDXObject legacyFrame) &&
                legacyFrame != null)
            {
                Frame = legacyFrame;
                return;
            }

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
            bool remoteInspection = IsRemoteInspectionActive();
            foreach (UIObject button in _primaryButtons)
            {
                if (button == null)
                {
                    continue;
                }

                button.ButtonVisible = characterPage;
                button.SetEnabled(characterPage && !_exceptionPopupOpen);
            }

            if (_itemButton != null)
            {
                _itemButton.ButtonVisible = characterPage && !remoteInspection;
                _itemButton.SetEnabled(characterPage && !remoteInspection && !_exceptionPopupOpen);
            }

            if (_wishButton != null)
            {
                _wishButton.ButtonVisible = characterPage && !remoteInspection;
                _wishButton.SetEnabled(characterPage && !remoteInspection && !_exceptionPopupOpen);
            }

            if (_partyButton != null)
            {
                _partyButton.ButtonVisible = characterPage;
                _partyButton.SetEnabled(characterPage && !_exceptionPopupOpen);
            }

            if (_followButton != null)
            {
                _followButton.ButtonVisible = characterPage && remoteInspection;
                _followButton.SetEnabled(characterPage && remoteInspection && !_exceptionPopupOpen);
            }

            if (_tradeButton != null)
            {
                _tradeButton.ButtonVisible = characterPage;
                _tradeButton.SetEnabled(characterPage && !_exceptionPopupOpen);
            }

            if (_familyButton != null)
            {
                _familyButton.ButtonVisible = characterPage;
                _familyButton.SetEnabled(characterPage && !_exceptionPopupOpen);
            }

            if (!_isBigBang)
            {
                bool petOpen = _legacyExpandedPanel == LegacyExpandedPanel.Pet;
                bool rideOpen = _legacyExpandedPanel == LegacyExpandedPanel.Ride;
                bool collectionOpen = _legacyExpandedPanel == LegacyExpandedPanel.Collection;
                bool collectionBook = collectionOpen && _legacyCollectionMode == LegacyCollectionMode.Book;
                bool collectionOverview = collectionOpen && _legacyCollectionMode == LegacyCollectionMode.Overview;

                SetLegacyToggleButtonState(_legacyPetShowButton, !petOpen, !_exceptionPopupOpen);
                SetLegacyToggleButtonState(_legacyPetHideButton, petOpen, !_exceptionPopupOpen);
                SetLegacyToggleButtonState(_legacyRideShowButton, !rideOpen, !_exceptionPopupOpen);
                SetLegacyToggleButtonState(_legacyRideHideButton, rideOpen, !_exceptionPopupOpen);
                SetLegacyToggleButtonState(_legacyBookShowButton, !collectionBook, !_exceptionPopupOpen);
                SetLegacyToggleButtonState(_legacyBookHideButton, collectionBook, !_exceptionPopupOpen);
                SetLegacyToggleButtonState(_legacyCollectionShowButton, !collectionOverview, !_exceptionPopupOpen);
                SetLegacyToggleButtonState(_legacyCollectionHideButton, collectionOverview, !_exceptionPopupOpen);
                SetLegacyToggleButtonState(_legacyExceptionShowButton, petOpen && !_exceptionPopupOpen, HasActivePets());
                SetLegacyToggleButtonState(_legacyExceptionHideButton, petOpen && _exceptionPopupOpen, true);
                SetLegacyToggleButtonState(_legacyPresentButton, collectionBook && !_exceptionPopupOpen, _wishEntries.Count > 0);
            }

            if (_petExceptionButton != null)
            {
                bool showPetException = _currentPage == UserInfoPage.Pet;
                _petExceptionButton.ButtonVisible = showPetException;
                _petExceptionButton.SetEnabled(showPetException && !remoteInspection && HasActivePets() && !_exceptionPopupOpen);
            }

            int availablePetSlots = GetAvailablePetSlotCount();
            for (int i = 0; i < _petTabButtons.Count; i++)
            {
                UIObject button = _petTabButtons[i];
                if (button == null)
                {
                    continue;
                }

                bool visible = _currentPage == UserInfoPage.Pet && i < availablePetSlots;
                button.ButtonVisible = visible;
                button.SetEnabled(visible && !_exceptionPopupOpen);
                button.SetButtonState(_selectedPetTabIndex == i ? UIObjectState.Pressed : UIObjectState.Normal);
            }

            for (int i = 0; i < _legacyPetButtons.Count; i++)
            {
                UIObject button = _legacyPetButtons[i];
                if (button == null)
                {
                    continue;
                }

                bool visible = !_isBigBang && _legacyExpandedPanel == LegacyExpandedPanel.Pet;
                bool enabled = visible && i < availablePetSlots && !_exceptionPopupOpen;
                button.ButtonVisible = visible;
                button.SetEnabled(enabled);
                button.SetButtonState(_selectedPetTabIndex == i ? UIObjectState.Pressed : UIObjectState.Normal);
            }

            if (_collectSortButton != null)
            {
                bool showCollectButtons = _currentPage == UserInfoPage.Collect;
                _collectSortButton.ButtonVisible = showCollectButtons;
                _collectSortButton.SetEnabled(showCollectButtons && !_exceptionPopupOpen);
            }

            if (_collectClaimButton != null)
            {
                bool showCollectButtons = _currentPage == UserInfoPage.Collect && !remoteInspection;
                _collectClaimButton.ButtonVisible = showCollectButtons;
                _collectClaimButton.SetEnabled(showCollectButtons && !_exceptionPopupOpen && CanClaimCollectReward() && !HasClaimedCollectReward());
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

            bool popupSelectorMode = characterPage && !remoteInspection && _activePopup != AuxiliaryPopupKind.None;
            bool popularityMode = characterPage && remoteInspection;
            if (_popupUpButton != null)
            {
                _popupUpButton.ButtonVisible = popupSelectorMode || popularityMode;
                _popupUpButton.SetEnabled(!_exceptionPopupOpen && (popularityMode
                    ? CanRequestPopularity(PopularityChangeDirection.Up)
                    : popupSelectorMode && CanScrollPopupSelectionUp()));
            }

            if (_popupDownButton != null)
            {
                _popupDownButton.ButtonVisible = popupSelectorMode || popularityMode;
                _popupDownButton.SetEnabled(!_exceptionPopupOpen && (popularityMode
                    ? CanRequestPopularity(PopularityChangeDirection.Down)
                    : popupSelectorMode && CanScrollPopupSelectionDown()));
            }
        }

        private void HandlePopupUpButton()
        {
            if (IsRemoteInspectionActive())
            {
                RequestPopularityChange(PopularityChangeDirection.Up);
                return;
            }

            ScrollPopupSelectionUp();
        }

        private void HandlePopupDownButton()
        {
            if (IsRemoteInspectionActive())
            {
                RequestPopularityChange(PopularityChangeDirection.Down);
                return;
            }

            ScrollPopupSelectionDown();
        }

        private void ScrollPopupSelectionUp()
        {
            switch (_activePopup)
            {
                case AuxiliaryPopupKind.Item:
                    if (_selectedItemPopupIndex <= 0)
                    {
                        return;
                    }

                    _selectedItemPopupIndex = Math.Max(0, _selectedItemPopupIndex - 1);
                    _statusMessage = $"Selected {_itemPopupEntries[_selectedItemPopupIndex]} from the item list.";
                    break;
                case AuxiliaryPopupKind.Wish:
                    if (_selectedWishIndex <= 0)
                    {
                        return;
                    }

                    _selectedWishIndex = Math.Max(0, _selectedWishIndex - 1);
                    _statusMessage = $"Selected {_wishEntries[_selectedWishIndex]} from the wish list.";
                    break;
                default:
                    return;
            }

            UpdateButtonStates();
        }

        public void InitializeLegacyPetButtons(IEnumerable<UIObject> petButtons)
        {
            _legacyPetButtons.Clear();
            if (petButtons == null)
            {
                return;
            }

            int petIndex = 0;
            foreach (UIObject button in petButtons.Where(button => button != null))
            {
                int capturedIndex = petIndex;
                _legacyPetButtons.Add(button);
                AddButton(button);
                button.ButtonClickReleased += _ =>
                {
                    _selectedPetTabIndex = capturedIndex;
                    OpenLegacyPanel(LegacyExpandedPanel.Pet);
                    _statusMessage = $"Viewing pet slot {capturedIndex + 1}.";
                    UpdateButtonStates();
                };
                petIndex++;
            }

            UpdateButtonStates();
        }

        private void ScrollPopupSelectionDown()
        {
            switch (_activePopup)
            {
                case AuxiliaryPopupKind.Item:
                    if (_selectedItemPopupIndex >= _itemPopupEntries.Length - 1)
                    {
                        return;
                    }

                    _selectedItemPopupIndex = Math.Min(_itemPopupEntries.Length - 1, _selectedItemPopupIndex + 1);
                    _statusMessage = $"Selected {_itemPopupEntries[_selectedItemPopupIndex]} from the item list.";
                    break;
                case AuxiliaryPopupKind.Wish:
                    if (_selectedWishIndex >= _wishEntries.Count - 1)
                    {
                        return;
                    }

                    _selectedWishIndex = Math.Min(_wishEntries.Count - 1, _selectedWishIndex + 1);
                    _statusMessage = $"Selected {_wishEntries[_selectedWishIndex]} from the wish list.";
                    break;
                default:
                    return;
            }

            UpdateButtonStates();
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

        private string GetEquippedItemName(EquipSlot slot, CharacterBuild build = null)
        {
            build ??= GetDisplayedBuild();
            if (build?.Equipment != null &&
                build.Equipment.TryGetValue(slot, out CharacterPart part) &&
                !string.IsNullOrWhiteSpace(part?.Name))
            {
                return part.Name;
            }

            if (build?.HiddenEquipment != null &&
                build.HiddenEquipment.TryGetValue(slot, out CharacterPart hiddenPart) &&
                !string.IsNullOrWhiteSpace(hiddenPart?.Name))
            {
                return hiddenPart.Name;
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
            return GetAvailablePetSlotCount() > 0;
        }

        private void RefreshMarriageBadgeState()
        {
            _isMarriedProfile = MarriedBadgeProvider?.Invoke(BuildCurrentActionContext()) ?? false;
        }

        private void ClampSelectedPetTabIndex()
        {
            int petCount = GetAvailablePetSlotCount();
            if (petCount <= 0)
            {
                _selectedPetTabIndex = 0;
                return;
            }

            _selectedPetTabIndex = Math.Clamp(_selectedPetTabIndex, 0, petCount - 1);
        }

        private bool IsPageAvailable(UserInfoPage page)
        {
            CharacterBuild displayBuild = GetDisplayedBuild();
            return page switch
            {
                UserInfoPage.Ride => displayBuild?.HasMonsterRiding == true || !string.Equals(GetEquippedItemName(EquipSlot.TamingMob, displayBuild), "-", StringComparison.Ordinal),
                UserInfoPage.Pet => HasActivePets(),
                UserInfoPage.Collect => true,
                UserInfoPage.Personality => true,
                _ => true
            };
        }

        private void ToggleExceptionPopup()
        {
            if (IsRemoteInspectionActive())
            {
                _statusMessage = "Pet exception editing is local-only in the simulator.";
                UpdateButtonStates();
                return;
            }

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

        private void RequestPartyAction()
        {
            UserInfoActionContext context = BuildCurrentActionContext();
            string message = PartyRequested?.Invoke(context);
            if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message;
            }
        }

        private void RequestFollowAction()
        {
            UserInfoActionContext context = BuildCurrentActionContext();
            string message = FollowRequested?.Invoke(context);
            if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message;
            }
        }

        private void RequestTradeAction()
        {
            UserInfoActionContext context = BuildCurrentActionContext();
            string message = TradingRoomRequested?.Invoke(context);
            if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message;
            }
        }

        private void RequestFamilyAction()
        {
            UserInfoActionContext context = BuildCurrentActionContext();
            string message = FamilyRequested?.Invoke(context);
            if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message;
            }
        }

        private void RequestPopularityChange(PopularityChangeDirection direction)
        {
            UserInfoActionContext context = BuildCurrentActionContext();
            string message = PopularityRequested?.Invoke(context, direction);
            if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message;
            }
        }

        private void ToggleAuxiliaryPopup(AuxiliaryPopupKind popupKind)
        {
            _exceptionPopupOpen = false;
            _activePopup = _activePopup == popupKind ? AuxiliaryPopupKind.None : popupKind;
            if (_activePopup == AuxiliaryPopupKind.Item && _selectedItemPopupIndex < 0 && _itemPopupEntries.Length > 0)
            {
                _selectedItemPopupIndex = 0;
            }

            if (_activePopup == AuxiliaryPopupKind.Wish && _selectedWishIndex < 0 && _wishEntries.Count > 0)
            {
                _selectedWishIndex = 0;
            }

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

        private void OpenCollectionBook()
        {
            if (BookCollectionRequested == null)
            {
                _statusMessage = "The collection book owner is not available in this simulator session.";
                return;
            }

            string message = BookCollectionRequested.Invoke(BuildCurrentActionContext());
            _statusMessage = string.IsNullOrWhiteSpace(message)
                ? "Collection book opened."
                : message;
        }

        private void ClaimCollectReward()
        {
            if (!CanClaimCollectReward())
            {
                _statusMessage = "Collection reward is not ready yet.";
                return;
            }

            if (HasClaimedCollectReward())
            {
                _statusMessage = "Collection reward has already been claimed in this simulator session.";
                return;
            }

            string claimKey = BuildCollectRewardClaimKey();
            if (string.IsNullOrWhiteSpace(claimKey))
            {
                _statusMessage = "Collection reward claim could not resolve an active character owner.";
                return;
            }

            _collectRewardClaims.Add(claimKey);
            _statusMessage = "Collection reward claim acknowledged locally.";
            UpdateButtonStates();
        }

        private void PresentWishEntry()
        {
            if (_selectedWishIndex < 0 && _wishEntries.Count > 0)
            {
                _selectedWishIndex = 0;
            }

            if (_selectedWishIndex < 0 || _selectedWishIndex >= _wishEntries.Count)
            {
                _statusMessage = "Select a wish entry before previewing a present.";
                return;
            }

            string wishEntry = _wishEntries[_selectedWishIndex];
            string message = WishPresentRequested?.Invoke(BuildCurrentActionContext(), wishEntry);
            _statusMessage = string.IsNullOrWhiteSpace(message)
                ? $"Present routing prepared for {wishEntry}."
                : message;
        }

        private bool CanClaimCollectReward()
        {
            if (IsRemoteInspectionActive())
            {
                return false;
            }

            ItemMakerProgressionSnapshot progression = GetCollectionSnapshot();
            MonsterBookSnapshot snapshot = GetMonsterBookSnapshot();
            return progression.SuccessfulCrafts > 0
                || progression.DiscoveredRecipeCount > 0
                || progression.UnlockedHiddenRecipeCount > 0
                || snapshot.OwnedCardTypes > 0;
        }

        private bool HasClaimedCollectReward()
        {
            string claimKey = BuildCollectRewardClaimKey();
            return !string.IsNullOrWhiteSpace(claimKey) && _collectRewardClaims.Contains(claimKey);
        }

        private string BuildCollectRewardClaimKey()
        {
            if (IsRemoteInspectionActive())
            {
                return null;
            }

            CharacterBuild build = GetDisplayedBuild();
            if (build == null)
            {
                return null;
            }

            if (build.Id > 0)
            {
                return build.Id.ToString(CultureInfo.InvariantCulture);
            }

            return string.IsNullOrWhiteSpace(build.Name)
                ? null
                : build.Name.Trim();
        }

        private List<(string Label, string Value)> BuildCollectEntries()
        {
            ItemMakerProgressionSnapshot snapshot = GetCollectionSnapshot();
            List<(string Label, string Value)> entries = new List<(string Label, string Value)>
            {
                ("Maker", BuildCollectFamilySummary(snapshot, ItemMakerRecipeFamily.Generic)),
                ("Glove", BuildCollectFamilySummary(snapshot, ItemMakerRecipeFamily.Gloves)),
                ("Shoe", BuildCollectFamilySummary(snapshot, ItemMakerRecipeFamily.Shoes)),
                ("Toy", BuildCollectFamilySummary(snapshot, ItemMakerRecipeFamily.Toys)),
                ("Craft", snapshot.SuccessfulCrafts.ToString()),
                ("Recipes", BuildRecipeSummary(snapshot))
            };

            return _collectSortByName
                ? entries.OrderBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase).ToList()
                : entries;
        }

        private MonsterBookSnapshot GetMonsterBookSnapshot()
        {
            CharacterBuild activeBuild = _inspectionTarget?.Build ?? _characterBuild;
            if (!ReferenceEquals(_snapshotCacheBuild, activeBuild))
            {
                RefreshSnapshotCaches();
            }

            return _currentMonsterBookSnapshot;
        }

        private string BuildCollectFamilySummary(ItemMakerProgressionSnapshot snapshot, ItemMakerRecipeFamily family)
        {
            if (snapshot == null)
            {
                return "-";
            }

            int level = snapshot.GetLevel(family);
            int progress = snapshot.GetProgress(family);
            int target = snapshot.GetProgressTarget(family);
            return target > 0
                ? $"Lv {level} ({progress}/{target})"
                : $"Lv {level}";
        }

        private string BuildRecipeSummary(ItemMakerProgressionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "-";
            }

            int discovered = snapshot.DiscoveredRecipeCount;
            int hidden = snapshot.UnlockedHiddenRecipeCount;
            if (hidden <= 0)
            {
                return discovered.ToString();
            }

            return $"{discovered} + {hidden} hidden";
        }

        private string BuildCollectStatusText(ItemMakerProgressionSnapshot progression, MonsterBookSnapshot snapshot)
        {
            string bookText = snapshot.TotalCardTypes > 0
                ? $"Monster Book {snapshot.OwnedCardTypes}/{snapshot.TotalCardTypes}"
                : "Monster Book local only";
            string recipeText = progression.DiscoveredRecipeCount > 0 || progression.UnlockedHiddenRecipeCount > 0
                ? $"{progression.DiscoveredRecipeCount} recipes, {progression.UnlockedHiddenRecipeCount} hidden"
                : "No recipes discovered yet";
            return $"{bookText}. {recipeText}.";
        }

        private string BuildMedalSummary()
        {
            string medalName = GetEquippedItemName(EquipSlot.Medal);
            return string.Equals(medalName, "-", StringComparison.Ordinal)
                ? IsRemoteInspectionActive()
                    ? "Medal: none equipped on inspected build"
                    : "Medal: none equipped locally"
                : $"Medal: {medalName}";
        }

        private string BuildPocketSummary()
        {
            CharacterBuild displayBuild = GetDisplayedBuild();
            string pocketName = GetEquippedItemName(EquipSlot.Pocket);
            if (!string.Equals(pocketName, "-", StringComparison.Ordinal))
            {
                return $"Pocket: {pocketName}";
            }

            int charm = Math.Max(0, displayBuild?.TraitCharm ?? 0);
            return displayBuild?.IsPocketSlotAvailable == true
                ? IsRemoteInspectionActive()
                    ? $"Pocket unlocked on inspected build (Charm {charm})"
                    : $"Pocket unlocked at Charm {charm}"
                : IsRemoteInspectionActive()
                    ? $"Pocket locked on inspected build ({charm}/30 Charm)"
                    : $"Pocket locked ({charm}/30 Charm)";
        }

        private void DrawEquipmentSummary(SpriteBatch sprite, EquipSlot slot, string summaryText, Point iconPosition, Point textPosition)
        {
            if (sprite == null)
            {
                return;
            }

            CharacterPart equippedPart = TryResolveEquippedItem(slot);
            Texture2D icon = TryResolveItemIcon(sprite, equippedPart?.ItemId ?? 0);
            if (icon != null)
            {
                sprite.Draw(
                    icon,
                    new Rectangle(Position.X + iconPosition.X, Position.Y + iconPosition.Y, 16, 16),
                    Color.White);
            }

            DrawPlainText(
                sprite,
                FitText(summaryText, icon != null ? 128 : 150),
                new Vector2(Position.X + (icon != null ? textPosition.X : iconPosition.X), Position.Y + textPosition.Y),
                MutedColor,
                0.48f);
        }

        private CharacterPart TryResolveEquippedItem(EquipSlot slot)
        {
            CharacterBuild displayBuild = GetDisplayedBuild();
            if (displayBuild?.Equipment != null &&
                displayBuild.Equipment.TryGetValue(slot, out CharacterPart equippedPart) &&
                equippedPart != null)
            {
                return equippedPart;
            }

            if (displayBuild?.HiddenEquipment != null &&
                displayBuild.HiddenEquipment.TryGetValue(slot, out CharacterPart hiddenPart) &&
                hiddenPart != null)
            {
                return hiddenPart;
            }

            return null;
        }

        private Texture2D TryResolveItemIcon(SpriteBatch sprite, int itemId)
        {
            if (sprite?.GraphicsDevice == null || itemId <= 0 || HaCreator.Program.InfoManager?.ItemIconCache == null)
            {
                return null;
            }

            if (_itemIconCache.TryGetValue(itemId, out Texture2D cachedIcon))
            {
                return cachedIcon;
            }

            if (!HaCreator.Program.InfoManager.ItemIconCache.TryGetValue(itemId, out var canvas) || canvas == null)
            {
                _itemIconCache[itemId] = null;
                return null;
            }

            Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(sprite.GraphicsDevice);
            _itemIconCache[itemId] = texture;
            return texture;
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
            if (!leftReleased)
            {
                return;
            }

            bool petExceptionOwnerActive = _currentPage == UserInfoPage.Pet || (!_isBigBang && _legacyExpandedPanel == LegacyExpandedPanel.Pet);
            if (_exceptionPopupOpen && petExceptionOwnerActive)
            {
                HandleExceptionPopupClick(mouseState.Position);
                return;
            }

            if (_currentPage != UserInfoPage.Character)
            {
                if (_currentPage == UserInfoPage.Collect)
                {
                    HandleCollectPageClick(mouseState.Position);
                }

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

        private void HandleExceptionPopupClick(Point mousePosition)
        {
            for (int i = 0; i < Math.Min(3, _petExceptionEntries.Count); i++)
            {
                if (!GetExceptionPopupRowBounds(i).Contains(mousePosition))
                {
                    continue;
                }

                _selectedPetExceptionIndex = i;
                _statusMessage = $"Selected pet exception entry {_petExceptionEntries[i]}.";
                UpdateButtonStates();
                return;
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

        private void HandleCollectPageClick(Point mousePosition)
        {
            if (GetCollectBookLaunchBounds().Contains(mousePosition))
            {
                OpenCollectionBook();
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

        private void DrawLegacyExpandedPanel(SpriteBatch sprite)
        {
            switch (_legacyExpandedPanel)
            {
                case LegacyExpandedPanel.Pet:
                    DrawLegacyPetPanel(sprite);
                    break;
                case LegacyExpandedPanel.Ride:
                    DrawLegacyRidePanel(sprite);
                    break;
                case LegacyExpandedPanel.Collection:
                    DrawLegacyCollectionPanel(sprite);
                    break;
            }
        }

        private void DrawLegacyPetPanel(SpriteBatch sprite)
        {
            DrawPlainText(sprite, "Pet", new Vector2(Position.X + 18, Position.Y + 208), HeaderColor, 0.72f);
            if (TryGetSelectedLocalPet(out PetRuntime pet, out int localPetCount))
            {
                DrawLabeledRow(sprite, 230, "Pet", pet.Name, ValueColor, 138);
                DrawLabeledRow(sprite, 252, "Level", $"Command Lv. {pet.CommandLevel}", SecondaryColor, 138);
                DrawLabeledRow(sprite, 274, "Loot", pet.AutoLootEnabled ? "Auto-loot enabled" : "Auto-loot disabled", pet.AutoLootEnabled ? SuccessColor : WarningColor, 138);
                DrawLabeledRow(sprite, 296, "Chat", $"Balloon {pet.ChatBalloonStyle}", MutedColor, 138);
                DrawLabeledRow(sprite, 318, "Slots", $"{localPetCount} populated", SecondaryColor, 138);
                return;
            }

            if (TryGetSelectedRemotePetItemId(out int remotePetItemId, out int remotePetCount))
            {
                DrawLabeledRow(sprite, 230, "Pet", ResolveRemotePetDisplayName(remotePetItemId), ValueColor, 138);
                DrawLabeledRow(sprite, 252, "Slot", $"Remote pet slot {_selectedPetTabIndex + 1}", SecondaryColor, 138);
                DrawLabeledRow(sprite, 274, "State", "Packet-owned remote slot", MutedColor, 138);
                DrawLabeledRow(sprite, 296, "Notes", "Pet stats remain approximated locally", WarningColor, 138);
                DrawLabeledRow(sprite, 318, "Slots", $"{remotePetCount} authored", SecondaryColor, 138);
                return;
            }

            if (IsRemoteInspectionActive())
            {
                DrawPlainText(sprite, "No remote pet slots were present on the inspected build.", new Vector2(Position.X + 18, Position.Y + 232), MutedColor, 0.56f);
                return;
            }

            if (_petController?.ActivePets == null || _petController.ActivePets.Count == 0)
            {
                DrawPlainText(sprite, "No active pets are available for the legacy add-on.", new Vector2(Position.X + 18, Position.Y + 232), MutedColor, 0.56f);
                return;
            }
        }

        private int GetAvailablePetSlotCount()
        {
            if (IsRemoteInspectionActive())
            {
                return GetResolvedRemotePetItemIds().Count;
            }

            return Math.Min(3, _petController?.ActivePets?.Count ?? 0);
        }

        private bool TryGetSelectedLocalPet(out PetRuntime pet, out int petCount)
        {
            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
            petCount = Math.Min(3, pets?.Count ?? 0);
            if (IsRemoteInspectionActive() || petCount <= 0)
            {
                pet = null;
                return false;
            }

            pet = pets[Math.Clamp(_selectedPetTabIndex, 0, petCount - 1)];
            return pet != null;
        }

        private bool TryGetSelectedRemotePetItemId(out int petItemId, out int petCount)
        {
            IReadOnlyList<int> petItemIds = GetResolvedRemotePetItemIds();
            petCount = petItemIds.Count;
            if (!IsRemoteInspectionActive() || petCount <= 0)
            {
                petItemId = 0;
                return false;
            }

            petItemId = petItemIds[Math.Clamp(_selectedPetTabIndex, 0, petCount - 1)];
            return petItemId > 0;
        }

        private IReadOnlyList<int> GetResolvedRemotePetItemIds()
        {
            IReadOnlyList<int> remotePetItemIds = GetDisplayedBuild()?.RemotePetItemIds;
            if (remotePetItemIds == null || remotePetItemIds.Count == 0)
            {
                return Array.Empty<int>();
            }

            List<int> resolvedPetItemIds = new List<int>(3);
            foreach (int petItemId in remotePetItemIds)
            {
                if (petItemId <= 0)
                {
                    continue;
                }

                resolvedPetItemIds.Add(petItemId);
                if (resolvedPetItemIds.Count >= 3)
                {
                    break;
                }
            }

            return resolvedPetItemIds;
        }

        private static string ResolveRemotePetDisplayName(int petItemId)
        {
            return InventoryItemMetadataResolver.TryResolveItemName(petItemId, out string itemName) &&
                   !string.IsNullOrWhiteSpace(itemName)
                ? itemName
                : $"Pet item {petItemId}";
        }

        private void DrawLegacyRidePanel(SpriteBatch sprite)
        {
            CharacterBuild displayBuild = GetDisplayedBuild();
            string mountName = GetEquippedItemName(EquipSlot.TamingMob, displayBuild);
            string saddleName = GetEquippedItemName(EquipSlot.Saddle, displayBuild);
            bool ridingReady = displayBuild?.HasMonsterRiding == true && !string.Equals(mountName, "-", StringComparison.Ordinal);

            DrawPlainText(sprite, "Ride", new Vector2(Position.X + 18, Position.Y + 208), HeaderColor, 0.72f);
            DrawLabeledRow(sprite, 230, "Status", ridingReady ? "Ride available" : "No active mount slot", ridingReady ? SuccessColor : WarningColor, 138);
            DrawLabeledRow(sprite, 252, "Mount", mountName, ValueColor, 138);
            DrawLabeledRow(sprite, 274, "Saddle", saddleName, MutedColor, 138);
            DrawLabeledRow(sprite, 296, "Skill", displayBuild?.HasMonsterRiding == true ? "Monster Riding learned" : "Monster Riding not learned", SecondaryColor, 138);
        }

        private void DrawLegacyCollectionPanel(SpriteBatch sprite)
        {
            DrawPlainText(
                sprite,
                _legacyCollectionMode == LegacyCollectionMode.Book ? "Monster Book" : "Collection",
                new Vector2(Position.X + 18, Position.Y + 208),
                HeaderColor,
                0.72f);

            if (_legacyCollectionMode == LegacyCollectionMode.Book)
            {
                MonsterBookSnapshot snapshot = GetMonsterBookSnapshot();
                DrawLabeledRow(sprite, 230, "Cards", $"{snapshot.OwnedCardTypes}/{snapshot.TotalCardTypes}", ValueColor, 138);
                DrawLabeledRow(sprite, 252, "Complete", $"{snapshot.CompletedCardTypes}", SecondaryColor, 138);
                DrawLabeledRow(sprite, 274, "Cover", string.IsNullOrWhiteSpace(snapshot.RegisteredCardName) ? snapshot.Title : snapshot.RegisteredCardName, MutedColor, 138);
                DrawPlainText(sprite, "Open the dedicated book owner from the collection icon seam.", new Vector2(Position.X + 18, Position.Y + 300), MutedColor, 0.54f);
                return;
            }

            List<(string Label, string Value)> entries = BuildCollectEntries().Take(5).ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                (string label, string value) = entries[i];
                DrawLabeledRow(sprite, 230 + (i * 22), label, value, ValueColor, 138);
            }
        }

        private void OpenLegacyPanel(LegacyExpandedPanel panel)
        {
            if (panel == LegacyExpandedPanel.None)
            {
                CloseLegacyPanel();
                return;
            }

            _legacyExpandedPanel = _legacyExpandedPanel == panel ? LegacyExpandedPanel.None : panel;
            if (_legacyExpandedPanel != LegacyExpandedPanel.Pet)
            {
                _exceptionPopupOpen = false;
            }

            ApplyCurrentPageFrame();
            UpdateButtonStates();
        }

        private void OpenLegacyCollectionMode(LegacyCollectionMode mode)
        {
            bool sameModeAlreadyOpen = _legacyExpandedPanel == LegacyExpandedPanel.Collection && _legacyCollectionMode == mode;
            _legacyCollectionMode = mode;
            _legacyExpandedPanel = sameModeAlreadyOpen ? LegacyExpandedPanel.None : LegacyExpandedPanel.Collection;
            _exceptionPopupOpen = false;
            ApplyCurrentPageFrame();
            UpdateButtonStates();
        }

        private void CloseLegacyPanel()
        {
            _legacyExpandedPanel = LegacyExpandedPanel.None;
            _exceptionPopupOpen = false;
            ApplyCurrentPageFrame();
            UpdateButtonStates();
        }

        private void SetLegacyToggleButtonState(UIObject button, bool visible, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            button.ButtonVisible = visible;
            button.SetEnabled(visible && enabled);
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

        private Rectangle GetExceptionPopupRowBounds(int index)
        {
            Point popupPosition = GetExceptionPopupPosition();
            return new Rectangle(popupPosition.X + 10, popupPosition.Y + 48 + (index * 18), 136, 16);
        }

        private Rectangle GetCollectBookLaunchBounds()
        {
            if (_pageVisuals.TryGetValue(UserInfoPage.Collect, out PageVisual visual) && visual?.Icon != null)
            {
                return new Rectangle(
                    Position.X + visual.IconOffset.X,
                    Position.Y + visual.IconOffset.Y,
                    visual.Icon.Width,
                    visual.Icon.Height);
            }

            return new Rectangle(Position.X + 20, Position.Y + 24, 40, 40);
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

        private string FormatRank(int rank, int? previousRank)
        {
            if (rank <= 0)
            {
                return "-";
            }

            string baseText = $"#{rank:N0}";
            string delta = FormatRankDelta(rank, previousRank);
            return string.IsNullOrWhiteSpace(delta) ? baseText : $"{baseText} ({delta})";
        }

        private string FitText(string text, float maxWidth)
        {
            string safeText = string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
            if (_font == null || MeasureText(null, safeText, 1f).X <= maxWidth)
            {
                return safeText;
            }

            const string ellipsis = "...";
            for (int length = safeText.Length - 1; length > 0; length--)
            {
                string candidate = safeText.Substring(0, length) + ellipsis;
                if (MeasureText(null, candidate, 1f).X <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private ClientTextRasterizer EnsureClientTextRasterizer(GraphicsDevice graphicsDevice)
        {
            if (_clientTextRasterizer == null && graphicsDevice != null)
            {
                _clientTextRasterizer = new ClientTextRasterizer(graphicsDevice);
            }

            return _clientTextRasterizer;
        }

        private Vector2 MeasureText(SpriteBatch sprite, string text, float scale)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            ClientTextRasterizer rasterizer = EnsureClientTextRasterizer(sprite?.GraphicsDevice);
            return rasterizer != null
                ? rasterizer.MeasureString(text, scale)
                : _font.MeasureString(text) * scale;
        }

        private void DrawText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (_font == null || sprite == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            ClientTextRasterizer rasterizer = EnsureClientTextRasterizer(sprite.GraphicsDevice);
            if (rasterizer != null)
            {
                rasterizer.DrawString(sprite, text, position, color, scale);
                return;
            }

            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private ItemMakerProgressionSnapshot GetCollectionSnapshot()
        {
            CharacterBuild activeBuild = GetDisplayedBuild();
            if (!ReferenceEquals(_snapshotCacheBuild, activeBuild))
            {
                RefreshSnapshotCaches();
            }

            return _currentCollectionSnapshot;
        }

        private RankDeltaSnapshot GetRankDeltaSnapshot()
        {
            CharacterBuild activeBuild = GetDisplayedBuild();
            if (!ReferenceEquals(_snapshotCacheBuild, activeBuild))
            {
                RefreshSnapshotCaches();
            }

            return _currentRankDeltaSnapshot;
        }

        private void RefreshSnapshotCaches()
        {
            CharacterBuild activeBuild = GetDisplayedBuild();
            _snapshotCacheBuild = activeBuild;
            _currentCollectionSnapshot = _collectionSnapshotProvider?.Invoke(activeBuild) ?? ItemMakerProgressionSnapshot.Default;
            _currentMonsterBookSnapshot = _monsterBookSnapshotProvider?.Invoke(activeBuild) ?? new MonsterBookSnapshot();
            _currentRankDeltaSnapshot = _rankDeltaProvider?.Invoke(activeBuild) ?? default;
        }

        private bool CanRequestPopularity(PopularityChangeDirection direction)
        {
            if (!IsRemoteInspectionActive())
            {
                return false;
            }

            return direction != PopularityChangeDirection.Down || (GetDisplayedBuild()?.Fame ?? 0) > 0;
        }

        private bool CanScrollPopupSelectionUp()
        {
            return _activePopup switch
            {
                AuxiliaryPopupKind.Item => _selectedItemPopupIndex > 0,
                AuxiliaryPopupKind.Wish => _selectedWishIndex > 0,
                _ => false
            };
        }

        private bool CanScrollPopupSelectionDown()
        {
            return _activePopup switch
            {
                AuxiliaryPopupKind.Item => _selectedItemPopupIndex >= 0 && _selectedItemPopupIndex < _itemPopupEntries.Length - 1,
                AuxiliaryPopupKind.Wish => _selectedWishIndex >= 0 && _selectedWishIndex < _wishEntries.Count - 1,
                _ => false
            };
        }

        private string ResolveHoveredPersonalityTrait()
        {
            MouseState mouseState = Mouse.GetState();
            string[] traitKeys = { "charisma", "insight", "will", "craft", "sense", "charm" };
            for (int i = 0; i < traitKeys.Length; i++)
            {
                if (GetPersonalityRowBounds(i).Contains(mouseState.Position))
                {
                    return traitKeys[i];
                }
            }

            return null;
        }

        private Rectangle GetPersonalityRowBounds(int index)
        {
            return new Rectangle(Position.X + 18, Position.Y + 40 + (index * 24), 118, 22);
        }

        private int GetPersonalityTraitValue(string traitKey)
        {
            CharacterBuild displayBuild = GetDisplayedBuild();
            return traitKey?.ToLowerInvariant() switch
            {
                "charisma" => displayBuild?.TraitCharisma ?? 0,
                "insight" => displayBuild?.TraitInsight ?? 0,
                "will" => displayBuild?.TraitWill ?? 0,
                "craft" => displayBuild?.TraitCraft ?? 0,
                "sense" => displayBuild?.TraitSense ?? 0,
                "charm" => displayBuild?.TraitCharm ?? 0,
                _ => 0
            };
        }

        private bool ShouldDrawCharmCollectionTooltip()
        {
            return _personalityTooltipVisual?.CharmCollectionBody != null &&
                   (GetDisplayedBuild()?.IsPocketSlotAvailable ?? false);
        }

        private bool IsRemoteInspectionActive()
        {
            return GetResolvedInspectionTarget()?.Build != null;
        }

        private CharacterBuild GetDisplayedBuild()
        {
            return GetResolvedInspectionTarget()?.Build ?? _characterBuild;
        }

        private UserInfoActionContext BuildCurrentActionContext()
        {
            UserInfoInspectionTarget inspectionTarget = GetResolvedInspectionTarget();
            CharacterBuild build = inspectionTarget?.Build ?? _characterBuild;
            string locationSummary = IsRemoteInspectionActive()
                ? inspectionTarget?.LocationSummary ?? string.Empty
                : LocalActionLocationSummaryProvider?.Invoke() ?? string.Empty;
            int channel = IsRemoteInspectionActive()
                ? (inspectionTarget?.Channel ?? 0)
                : Math.Max(1, LocalActionChannelProvider?.Invoke() ?? 1);
            return new UserInfoActionContext(
                IsRemoteInspectionActive(),
                inspectionTarget?.CharacterId ?? build?.Id ?? 0,
                inspectionTarget?.Name ?? build?.Name ?? string.Empty,
                build,
                locationSummary,
                channel);
        }

        private string BuildInspectionBanner()
        {
            if (!IsRemoteInspectionActive())
            {
                return null;
            }

            UserInfoInspectionTarget inspectionTarget = GetResolvedInspectionTarget();
            string location = string.IsNullOrWhiteSpace(inspectionTarget?.LocationSummary)
                ? "Location unknown"
                : inspectionTarget.LocationSummary;
            string channelText = inspectionTarget?.Channel > 0
                ? $"CH {inspectionTarget.Channel}"
                : "CH ?";
            return $"{inspectionTarget?.Name}  {channelText}  {location}";
        }

        private UserInfoInspectionTarget GetResolvedInspectionTarget()
        {
            if (_inspectionTarget?.Build == null)
            {
                return null;
            }

            UserInfoInspectionTarget resolved = InspectionTargetResolver?.Invoke(_inspectionTarget);
            if (resolved == null)
            {
                return _inspectionTarget;
            }

            return new UserInfoInspectionTarget
            {
                Build = resolved.Build ?? _inspectionTarget.Build,
                CharacterId = resolved.CharacterId > 0 ? resolved.CharacterId : _inspectionTarget.CharacterId,
                Name = !string.IsNullOrWhiteSpace(resolved.Name) ? resolved.Name : _inspectionTarget.Name,
                LocationSummary = !string.IsNullOrWhiteSpace(resolved.LocationSummary) ? resolved.LocationSummary : _inspectionTarget.LocationSummary,
                Channel = resolved.Channel > 0 ? resolved.Channel : _inspectionTarget.Channel
            };
        }

        private static string FormatRankDelta(int currentRank, int? previousRank)
        {
            if (currentRank <= 0 || !previousRank.HasValue || previousRank.Value <= 0 || previousRank.Value == currentRank)
            {
                return null;
            }

            int delta = Math.Abs(previousRank.Value - currentRank);
            return previousRank.Value > currentRank ? $"up {delta}" : $"down {delta}";
        }

        private void DrawTooltipNumber(SpriteBatch sprite, Point tooltipPosition, int value)
        {
            if (_personalityTooltipVisual?.NumberGlyphs == null || _personalityTooltipVisual.NumberGlyphs.Count == 0)
            {
                DrawPlainText(
                    sprite,
                    value.ToString(),
                    new Vector2(tooltipPosition.X + 110, tooltipPosition.Y + 16),
                    WarningColor,
                    0.5f);
                return;
            }

            string text = Math.Max(0, value).ToString();
            int x = tooltipPosition.X + 106;
            int y = tooltipPosition.Y + 16;
            foreach (char ch in text)
            {
                if (!_personalityTooltipVisual.NumberGlyphs.TryGetValue(ch, out IDXObject glyph) || glyph == null)
                {
                    DrawPlainText(sprite, ch.ToString(), new Vector2(x, y - 1), WarningColor, 0.5f);
                    x += 6;
                    continue;
                }

                glyph.DrawBackground(sprite, null, null, x, y, Color.White, false, null);
                x += glyph.Width + 1;
            }
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
