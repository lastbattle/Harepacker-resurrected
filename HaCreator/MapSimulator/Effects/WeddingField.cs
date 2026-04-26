using HaSharedLibrary.Render.DX;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure.Data;
using Spine;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaCreator.Wz;
using HaSharedLibrary.Wz;

using HaSharedLibrary.Util;
using MapleLib.Converters;
using MapleLib.PacketLib;
using HaCreator.MapSimulator.Pools;


namespace HaCreator.MapSimulator.Effects
{
    /// <summary>
    /// Special Effect Field System - Manages specialized field types from MapleStory client.
    ///
    /// Handles:
    /// - CField_Wedding: Wedding ceremony effects (packets 379, 380)
    /// - CField_Witchtower: Witch tower score tracking (packet 358)
    /// - CField_GuildBoss: Guild boss healer/pulley mechanics (packets 344, 345)
    /// - CField_Dojang: Mu Lung Dojo timer and HUD gauges
    /// - CField_SpaceGAGA: Rescue Gaga timerboard clock
    /// - CField_Massacre: Kill counting and gauge system (packet 173)
    /// </summary>
    #region Wedding Field (CField_Wedding)
    public readonly struct WeddingPhotoScenePresentationPacketRecord
    {
        public WeddingPhotoScenePresentationPacketRecord(int packetType, string ownerName, int payloadLength, int tick)
        {
            PacketType = packetType;
            OwnerName = ownerName ?? string.Empty;
            PayloadLength = Math.Max(0, payloadLength);
            Tick = tick;
        }

        public int PacketType { get; }
        public string OwnerName { get; }
        public int PayloadLength { get; }
        public int Tick { get; }
    }

    /// <summary>
    /// Wedding Field - Wedding ceremony effects and dialogs.
    ///
    /// - Map 680000110: Cathedral wedding (NPC 9201011)
    /// - Map 680000210: Chapel wedding (NPC 9201002)
    /// - OnWeddingProgress (packet 379): Wedding step progress with dialogs
    /// - OnWeddingCeremonyEnd (packet 380): Ceremony completion with bless effect
    /// - SetBlessEffect: Sparkle animation between groom and bride positions
    /// </summary>
    public class WeddingField
    {
        private const int WhiteWeddingAltarMapId = 680000110;
        private const int SaintMapleAltarMapId = 680000210;
        private const int WhiteWeddingAltarNpcId = 9201011;
        private const int SaintMapleAltarNpcId = 9201002;
        private const int DialogDurationMs = 5000;
        private const int DialogFadeInDurationMs = 300;
        private const string BlessEffectImageName = "BasicEff.img";
        private const string BlessEffectPath = "Wedding";
        private const string WeddingUiImageName = "UIWindow.img";
        private const string CeremonyTextOverlayPath = "wedding/text/0";
        private const string CeremonyCardOverlayPath = "wedding/card/0";
        private const string WeddingMapHelperImageName = "MapHelper.img";
        private const string WeddingWeatherPath = "weather/wedding";
        private const string WeddingHeartWeatherPath = "weather/heartWedding";
        private const int DefaultCeremonyPetalCount = 35;
        private const int DefaultCeremonyHeartCount = 35;
        private const int DialogMaxWidth = 560;
        private const int DialogPadding = 20;
        private const int DialogLineSpacing = 4;
        private const int ParticipantLabelLineSpacing = 2;
        private const int WeddingPhotoCardRevealStep = 1;
        private const int WeddingPhotoCelebrationStep = 2;
        private const int PacketTypeUserEnterField = 179;
        private const int PacketTypeUserLeaveField = 180;
        private const int PacketTypeWeddingProgress = 379;
        private const int PacketTypeWeddingCeremonyEnd = 380;
        private const int PacketTypeUserMoveOfficial = 181;
        private const int PacketTypeUserMoveOrChairAlias = 210;
        private const int PacketTypeItemEffect = 215;
        private const int PacketTypeUserProfile = -1003;
        private const int PacketTypeSetActivePortableChairLegacy = 222;
        private const int PacketTypeAvatarModified = 223;
        private const int PacketTypeTemporaryStatSet = 225;
        private const int PacketTypeTemporaryStatReset = 226;
        private const int PacketTypeGuildNameChanged = 228;
        private const int PacketTypeGuildMarkChanged = 229;
        private const int PacketTypeCoupleRecordAdd = -1101;
        private const int PacketTypeCoupleRecordRemove = -1102;
        private const int PacketTypeFriendRecordAdd = -1103;
        private const int PacketTypeFriendRecordRemove = -1104;
        private const int PacketTypeMarriageRecordAdd = -1105;
        private const int PacketTypeMarriageRecordRemove = -1106;
        private const int PacketTypeNewYearCardRecordAdd = -1107;
        private const int PacketTypeNewYearCardRecordRemove = -1108;
        private const int RemoteDelayedLoadWindowMs = 100;
        private const int RemoteDelayedLoadCooltimeMs = 1000;
        private const int WeddingPhotoScenePresentationTrailLimit = 16;

        private enum WeddingPhotoScenePresentationStage
        {
            None,
            PacketTrailOnly,
            StepTextLayer,
            StepCardLayer,
            StepCelebrationLayer,
            BlessEffect
        }

        private enum PendingWeddingRemoteParticipantOperationType
        {
            TemporaryStatSet,
            TemporaryStatReset,
            GuildNameChanged,
            Profile,
            GuildMarkChanged,
            RelationshipRecordAdd,
            RelationshipRecordRemove
        }

        private readonly record struct PendingWeddingRemoteParticipantOperation(
            PendingWeddingRemoteParticipantOperationType Type,
            RemoteUserTemporaryStatSnapshot TemporaryStats,
            ushort TemporaryStatDelay,
            int[] ResetMaskWords,
            string GuildName,
            RemoteUserProfilePacket ProfilePacket,
            int MarkBackgroundId,
            int MarkBackgroundColor,
            int MarkId,
            int MarkColor,
            RemoteRelationshipOverlayType RelationshipType = RemoteRelationshipOverlayType.Generic,
            RemoteUserRelationshipRecord RelationshipRecord = default,
            int? RelationshipRemoveCharacterId = null,
            long? RelationshipRemoveItemSerial = null,
            RemoteRelationshipRecordDispatchKey RelationshipDispatchKey = default,
            long? RelationshipPairLookupSerial = null);


        private static readonly Dictionary<int, Dictionary<int, string>> WeddingDialogFallbacks = new()
        {
            [SaintMapleAltarNpcId] = new Dictionary<int, string>
            {
                [0] = "Dearly beloved, we are gathered here today to celebrate the marriage of these two fine, upstanding people. One can clearly see the love between you two, and it's a sight I'll never tire of. You have proved your love and received your Parent's Blessing. Do you wish to seal your love in the eternal embrace of marriage?",
                [1] = "Very well. Guests may now Bless the couple if they choose...",
                [3] = "By the power vested in me through the mighty Maple tree, I now pronounce you Husband and Wife. You may kiss the bride!",
                [4] = "With the Blessing of the Maple tree, I wish both of you a long and safe marriage."
            },
            [WhiteWeddingAltarNpcId] = new Dictionary<int, string>
            {
                [0] = "We are gathered here today to celebrate the union of these two love birds. I've never seen a better-looking couple in all my years of running this Chapel. So, do you want to travel the world and spend the rest of your life with your chosen spouse?",
                [1] = "Very well! I pronounce you Husband and Wife. You may kiss the bride!",
                [2] = "You two truly are a sight to behold...you may leave the Chapel now, and may the blessing of the Maple tree be with you."
            }
        };


        #region State
        private bool _isActive = false;
        private int _mapId;
        private int _npcId;
        private int? _localCharacterId;
        private Vector2? _localPlayerPosition;
        private CharacterBuild _localPlayerBuild;
        private Vector2? _groomPosition;
        private Vector2? _bridePosition;
        private readonly Dictionary<int, Vector2> _participantPositions = new();
        private readonly Dictionary<int, WeddingRemoteParticipant> _participantActors = new();
        private readonly Dictionary<string, WeddingRemoteParticipant> _audienceActors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _audienceActorNamesById = new();
        private readonly Dictionary<int, List<PendingWeddingRemoteParticipantOperation>> _pendingRemoteParticipantOperationsByCharacterId = new();
        private readonly Dictionary<RemoteRelationshipOverlayType, Dictionary<RemoteRelationshipRecordDispatchKey, int>> _relationshipRecordOwnerByDispatchKey = new();
        private readonly Queue<int> _pendingExternalRemoteActorLoads = new();
        private readonly HashSet<int> _pendingExternalRemoteActorLoadIds = new();
        private readonly HashSet<int> _loadedExternalRemoteActorIds = new();
        private readonly HashSet<int> _officialRemoteLifecycleActorIds = new();
        private int _externalRemoteActorLoadWindowEndTimeMs;
        private int _externalRemoteActorLoadCooltimeEndTimeMs;
        private CharacterLoader _characterLoader;


        // Wedding step (from OnWeddingProgress)
        private int _currentStep = 0;
        private int _groomId = 0;
        private int _brideId = 0;


        // Bless effect (sparkles between couple)
        private bool _blessEffectActive = false;
        private float _blessEffectAlpha = 0f;
        private int _blessEffectStartTime;
        private bool _ceremonyTextOverlayActive = false;
        private float _ceremonyTextOverlayAlpha = 0f;
        private bool _ceremonyCardOverlayActive = false;
        private float _ceremonyCardOverlayAlpha = 0f;
        private bool _ceremonyCelebrationActive = false;
        #endregion


        #region Visual Effects
        private List<WeddingSparkle> _sparkles = new();
        private List<IDXObject> _blessFrames;
        private Texture2D _ceremonyTextOverlayTexture;
        private Point _ceremonyTextOverlayOrigin;
        private Texture2D _ceremonyCardOverlayTexture;
        private Point _ceremonyCardOverlayOrigin;
        private readonly List<WeddingSceneFrame> _ceremonyPetalFrames = new();
        private readonly List<WeddingSceneFrame> _ceremonyHeartFrames = new();
        private readonly List<WeddingSceneParticle> _ceremonyPetals = new();
        private readonly List<WeddingSceneParticle> _ceremonyHearts = new();
        private WeddingSceneAssetConfig _ceremonyPetalConfig = new(DefaultCeremonyPetalCount, RandomFrames: true);
        private WeddingSceneAssetConfig _ceremonyHeartConfig = new(DefaultCeremonyHeartCount, RandomFrames: true);
        private Random _random = new();
        #endregion


        #region Dialog
        private WeddingDialog _currentDialog;
        private readonly Queue<WeddingDialog> _dialogQueue = new();
        private WeddingPacketResponse? _lastPacketResponse;
        private Action<string> _requestBgmOverride;
        private Action _clearBgmOverride;
        private Func<LoginAvatarLook, string, CharacterBuild> _remoteBuildFactory;
        private int? _lastPacketType;
        private readonly List<WeddingPhotoScenePresentationPacketRecord> _weddingPhotoScenePresentationPacketTrail = new();
        private int _weddingPhotoSceneUnhandledPacketCount;
        private int? _lastWeddingPhotoSceneUnhandledPacketType;
        private WeddingPhotoScenePresentationStage _weddingPhotoScenePresentationStage = WeddingPhotoScenePresentationStage.None;
        private int? _lastWeddingPhotoSceneStagePacketType;
        #endregion



        #region Public Properties
        internal bool HasPresentationOwner => _isActive || IsWeddingPhotoSceneOwnerActive;
        public bool IsActive => HasPresentationOwner;
        public bool HasWeddingPacketOwner => _isActive;
        public bool IsWeddingPhotoSceneOwnerActive { get; private set; }
        public string WeddingPhotoSceneOwnerDescription { get; private set; }
        public Rectangle? WeddingPhotoSceneViewport { get; private set; }
        public string WeddingPhotoSceneBackgroundMusicPath { get; private set; }
        public int CurrentStep => _currentStep;
        public bool IsBlessEffectActive => _blessEffectActive;
        public bool IsCeremonyTextOverlayActive => _ceremonyTextOverlayActive;
        public bool IsCeremonyCardOverlayActive => _ceremonyCardOverlayActive;
        public bool IsCeremonyCelebrationActive => _ceremonyCelebrationActive;
        public bool HasActiveScriptedDialog => _currentDialog != null || _dialogQueue.Count > 0;
        public WeddingParticipantRole LocalParticipantRole { get; private set; } = WeddingParticipantRole.Guest;
        public string CurrentDialogMessage => _currentDialog?.Message;
        public WeddingDialogMode CurrentDialogMode => _currentDialog?.Mode ?? WeddingDialogMode.Text;
        public bool IsGuestBlessPromptActive => _currentDialog?.Mode == WeddingDialogMode.YesNo;
        public Vector2? BlessEffectWorldCenter => TryGetBlessEffectWorldCenter();
        public int GroomId => _groomId;
        public int BrideId => _brideId;
        public Vector2? GroomPosition => _groomPosition;
        public Vector2? BridePosition => _bridePosition;
        public WeddingPacketResponse? LastPacketResponse => _lastPacketResponse;
        public int RemoteParticipantCount => _participantActors.Count + _audienceActors.Count;
        public int AudienceParticipantCount => _audienceActors.Count;
        public int CeremonyPetalParticleCount => _ceremonyPetals.Count;
        public int CeremonyHeartParticleCount => _ceremonyHearts.Count;
        public int? LastPacketType => _lastPacketType;
        public bool UseExternalRemoteActorRenderer { get; set; }
        public int WeddingPhotoScenePresentationPacketCount => _weddingPhotoScenePresentationPacketTrail.Count;
        public int? LastWeddingPhotoScenePresentationPacketType =>
            _weddingPhotoScenePresentationPacketTrail.Count == 0
                ? null
                : _weddingPhotoScenePresentationPacketTrail[^1].PacketType;
        public string LastWeddingPhotoScenePresentationPacketOwner =>
            _weddingPhotoScenePresentationPacketTrail.Count == 0
                ? null
                : _weddingPhotoScenePresentationPacketTrail[^1].OwnerName;
        public IReadOnlyList<WeddingPhotoScenePresentationPacketRecord> WeddingPhotoScenePresentationPacketTrail =>
            _weddingPhotoScenePresentationPacketTrail;
        public int WeddingPhotoSceneUnhandledPacketCount => _weddingPhotoSceneUnhandledPacketCount;
        public int? LastWeddingPhotoSceneUnhandledPacketType => _lastWeddingPhotoSceneUnhandledPacketType;
        #endregion



        #region Initialization
        public void Initialize(
            GraphicsDevice device,
            Action<string> requestBgmOverride = null,
            Action clearBgmOverride = null,
            Func<LoginAvatarLook, string, CharacterBuild> remoteBuildFactory = null,
            CharacterLoader characterLoader = null)
        {

            _requestBgmOverride = requestBgmOverride;

            _clearBgmOverride = clearBgmOverride;
            _remoteBuildFactory = remoteBuildFactory;
            _characterLoader = characterLoader;
            _blessFrames = LoadBlessFrames(device);
            LoadCeremonyOverlay(device);
            LoadCeremonyCelebrationAssets(device);
        }


        public void Enable(int mapId)
        {
            if (IsWeddingPhotoSceneOwnerActive)
            {
                _clearBgmOverride?.Invoke();
            }

            _isActive = true;
            IsWeddingPhotoSceneOwnerActive = false;
            WeddingPhotoSceneOwnerDescription = null;
            WeddingPhotoSceneViewport = null;
            WeddingPhotoSceneBackgroundMusicPath = null;
            _mapId = mapId;
            _currentStep = 0;


            // CField_Wedding::OnWeddingProgress only resolves ceremony NPC ids for the two ceremony maps.
            _npcId = ResolveCeremonyNpcId(mapId);
            _groomId = 0;
            _brideId = 0;
            _groomPosition = null;
            _bridePosition = null;
            _participantPositions.Clear();
            _participantActors.Clear();
            _audienceActors.Clear();
            _audienceActorNamesById.Clear();
            _pendingRemoteParticipantOperationsByCharacterId.Clear();
            ClearRelationshipRecordDispatchTables();
            _pendingExternalRemoteActorLoads.Clear();
            _pendingExternalRemoteActorLoadIds.Clear();
            _loadedExternalRemoteActorIds.Clear();
            _officialRemoteLifecycleActorIds.Clear();
            _externalRemoteActorLoadWindowEndTimeMs = 0;
            _externalRemoteActorLoadCooltimeEndTimeMs = 0;
            _lastPacketResponse = null;
            _lastPacketType = null;
            _ceremonyTextOverlayActive = false;
            _ceremonyTextOverlayAlpha = 0f;
            _ceremonyCardOverlayActive = false;
            _ceremonyCardOverlayAlpha = 0f;
            _ceremonyCelebrationActive = false;
            _ceremonyPetals.Clear();
            _ceremonyHearts.Clear();
            _ceremonyPetalConfig = new WeddingSceneAssetConfig(DefaultCeremonyPetalCount, RandomFrames: true);
            _ceremonyHeartConfig = new WeddingSceneAssetConfig(DefaultCeremonyHeartCount, RandomFrames: true);
            _currentDialog = null;
            _dialogQueue.Clear();
            LocalParticipantRole = WeddingParticipantRole.Guest;
            _weddingPhotoScenePresentationPacketTrail.Clear();
            _weddingPhotoSceneUnhandledPacketCount = 0;
            _lastWeddingPhotoSceneUnhandledPacketType = null;
            _weddingPhotoScenePresentationStage = WeddingPhotoScenePresentationStage.None;
            _lastWeddingPhotoSceneStagePacketType = null;


            System.Diagnostics.Debug.WriteLine($"[WeddingField] Enabled for map {mapId}, NPC {_npcId}");

        }


        public void BindWeddingPhotoSceneOwner(int mapId, string sourceDescription, Rectangle? viewport, string backgroundMusicPath = null)
        {
            bool ownerChanged =
                !IsWeddingPhotoSceneOwnerActive
                || _isActive
                || _mapId != mapId
                || WeddingPhotoSceneViewport != viewport
                || !string.Equals(WeddingPhotoSceneBackgroundMusicPath, backgroundMusicPath, StringComparison.Ordinal);

            if (!ownerChanged)
            {
                return;
            }

            _isActive = false;
            IsWeddingPhotoSceneOwnerActive = true;
            WeddingPhotoSceneOwnerDescription = sourceDescription ?? "CField_WeddingPhoto scene owner";
            WeddingPhotoSceneViewport = viewport;
            WeddingPhotoSceneBackgroundMusicPath = string.IsNullOrWhiteSpace(backgroundMusicPath) ? null : backgroundMusicPath;
            _mapId = mapId;
            _currentStep = 0;
            _blessEffectActive = false;
            _currentDialog = null;
            _dialogQueue.Clear();
            _ceremonyTextOverlayActive = false;
            _ceremonyCardOverlayActive = false;
            _ceremonyCelebrationActive = false;
            _ceremonyPetals.Clear();
            _ceremonyHearts.Clear();
            _lastPacketResponse = null;
            _lastPacketType = null;
            _groomId = 0;
            _brideId = 0;
            _groomPosition = null;
            _bridePosition = null;
            _participantPositions.Clear();
            _participantActors.Clear();
            _audienceActors.Clear();
            _audienceActorNamesById.Clear();
            _pendingRemoteParticipantOperationsByCharacterId.Clear();
            ClearRelationshipRecordDispatchTables();
            _pendingExternalRemoteActorLoads.Clear();
            _pendingExternalRemoteActorLoadIds.Clear();
            _loadedExternalRemoteActorIds.Clear();
            _officialRemoteLifecycleActorIds.Clear();
            _externalRemoteActorLoadWindowEndTimeMs = 0;
            _externalRemoteActorLoadCooltimeEndTimeMs = 0;
            LocalParticipantRole = WeddingParticipantRole.Guest;
            _weddingPhotoScenePresentationPacketTrail.Clear();
            _weddingPhotoSceneUnhandledPacketCount = 0;
            _lastWeddingPhotoSceneUnhandledPacketType = null;
            _weddingPhotoScenePresentationStage = WeddingPhotoScenePresentationStage.None;
            _lastWeddingPhotoSceneStagePacketType = null;

            if (!string.IsNullOrWhiteSpace(WeddingPhotoSceneBackgroundMusicPath))
            {
                _requestBgmOverride?.Invoke(WeddingPhotoSceneBackgroundMusicPath);
                return;
            }

            _clearBgmOverride?.Invoke();
        }


        public void ClearWeddingPhotoSceneOwner()
        {
            if (IsWeddingPhotoSceneOwnerActive && !_isActive)
            {
                _clearBgmOverride?.Invoke();
            }

            IsWeddingPhotoSceneOwnerActive = false;
            WeddingPhotoSceneOwnerDescription = null;
            WeddingPhotoSceneViewport = null;
            WeddingPhotoSceneBackgroundMusicPath = null;
            _weddingPhotoScenePresentationPacketTrail.Clear();
            _weddingPhotoSceneUnhandledPacketCount = 0;
            _lastWeddingPhotoSceneUnhandledPacketType = null;
            _weddingPhotoScenePresentationStage = WeddingPhotoScenePresentationStage.None;
            _lastWeddingPhotoSceneStagePacketType = null;
        }



        public void SetBlessFrames(List<IDXObject> frames)
        {
            _blessFrames = frames;
        }


        public void SetLocalPlayerState(int? localCharacterId, Vector2? localWorldPosition, CharacterBuild localPlayerBuild = null)
        {
            _localCharacterId = localCharacterId;
            _localPlayerPosition = localWorldPosition;
            _localPlayerBuild = localPlayerBuild?.Clone();
            UpdateParticipantState();
        }


        public bool TryApplyPacket(int packetType, byte[] payload, int currentTimeMs, out string errorMessage)

        {

            errorMessage = null;
            _lastPacketType = packetType;
            if (!CanApplyPacket(packetType, out bool photoScenePresentationPacket))
            {
                errorMessage = "Wedding runtime inactive.";
                return false;
            }
            if (IsWeddingPhotoSceneOwnerActive
                && !_isActive
                && packetType != PacketTypeWeddingProgress
                && packetType != PacketTypeWeddingCeremonyEnd)
            {
                RecordWeddingPhotoSceneUnhandledPacket(packetType, payload?.Length ?? 0, currentTimeMs);
                errorMessage = $"CField_WeddingPhoto delegated packet {packetType} to base CField owner surface; simulator keeps packet trail only.";
                return true;
            }

            if (photoScenePresentationPacket)
            {
                RecordWeddingPhotoScenePresentationPacket(packetType, payload?.Length ?? 0, currentTimeMs);
            }

            try
            {
                switch (packetType)
                {
                    case PacketTypeWeddingProgress:
                        if (!_isActive && !IsWeddingPhotoSceneOwnerActive)
                        {
                            errorMessage = "Wedding owner inactive.";
                            return false;
                        }

                        return TryApplyWeddingProgressPacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeWeddingCeremonyEnd:
                        if (!_isActive && !IsWeddingPhotoSceneOwnerActive)
                        {
                            errorMessage = "Wedding owner inactive.";
                            return false;
                        }

                        return TryApplyWeddingCeremonyEndPacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeUserEnterField:
                        return TryApplyRemoteSpawnPacket(payload, out errorMessage);
                    case PacketTypeUserLeaveField:
                        return TryApplyRemoteLeavePacket(payload, out errorMessage);
                    case PacketTypeUserMoveOfficial:
                        return TryApplyRemoteMovePacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeUserMoveOrChairAlias:
                        return TryApplyMoveOrChairAliasPacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeItemEffect:
                        return TryApplyRemoteItemEffectPacket(payload, out errorMessage);
                    case PacketTypeUserProfile:
                        return TryApplyRemoteProfilePacket(payload, out errorMessage);
                    case PacketTypeSetActivePortableChairLegacy:
                        return TryApplyRemoteChairPacket(payload, out errorMessage);
                    case PacketTypeAvatarModified:
                        return TryApplyRemoteAvatarModifiedPacket(payload, out errorMessage);
                    case PacketTypeTemporaryStatSet:
                        return TryApplyRemoteTemporaryStatSetPacket(payload, out errorMessage);
                    case PacketTypeTemporaryStatReset:
                        return TryApplyRemoteTemporaryStatResetPacket(payload, out errorMessage);
                    case PacketTypeGuildNameChanged:
                        return TryApplyRemoteGuildNameChangedPacket(payload, out errorMessage);
                    case PacketTypeGuildMarkChanged:
                        return TryApplyRemoteGuildMarkChangedPacket(payload, out errorMessage);
                    case PacketTypeCoupleRecordAdd:
                    case PacketTypeFriendRecordAdd:
                    case PacketTypeMarriageRecordAdd:
                    case PacketTypeNewYearCardRecordAdd:
                        return TryApplyRemoteRelationshipRecordAddPacket(packetType, payload, out errorMessage);
                    case PacketTypeCoupleRecordRemove:
                    case PacketTypeFriendRecordRemove:
                    case PacketTypeMarriageRecordRemove:
                    case PacketTypeNewYearCardRecordRemove:
                        return TryApplyRemoteRelationshipRecordRemovePacket(packetType, payload, out errorMessage);
                    default:
                        if (IsWeddingPhotoSceneOwnerActive && !_isActive)
                        {
                            RecordWeddingPhotoSceneUnhandledPacket(packetType, payload?.Length ?? 0, currentTimeMs);
                            errorMessage = $"CField_WeddingPhoto delegated packet {packetType} to base CField owner surface; simulator keeps packet trail only.";
                            return true;
                        }

                        errorMessage = photoScenePresentationPacket
                            ? $"Unsupported wedding-photo scene presentation packet type: {packetType}"
                            : $"Unsupported wedding packet type: {packetType}";
                        return false;
                }
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException || ex is IOException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private bool CanApplyPacket(int packetType, out bool photoScenePresentationPacket)
        {
            photoScenePresentationPacket = false;
            if (_isActive)
            {
                return true;
            }

            if (!IsWeddingPhotoSceneOwnerActive)
            {
                return false;
            }

            photoScenePresentationPacket = IsWeddingPhotoScenePresentationPacket(packetType);
            // CField_WeddingPhoto keeps running inside CField::OnPacket even for families
            // we do not decode yet; keep those packets on the owner trail as passthrough.
            return true;
        }

        private static bool IsWeddingPhotoScenePresentationPacket(int packetType)
        {
            switch (packetType)
            {
                case PacketTypeWeddingProgress:
                case PacketTypeWeddingCeremonyEnd:
                case PacketTypeUserEnterField:
                case PacketTypeUserLeaveField:
                case PacketTypeUserMoveOfficial:
                case PacketTypeUserMoveOrChairAlias:
                case PacketTypeItemEffect:
                case PacketTypeUserProfile:
                case PacketTypeSetActivePortableChairLegacy:
                case PacketTypeAvatarModified:
                case PacketTypeTemporaryStatSet:
                case PacketTypeTemporaryStatReset:
                case PacketTypeGuildNameChanged:
                case PacketTypeGuildMarkChanged:
                case PacketTypeCoupleRecordAdd:
                case PacketTypeCoupleRecordRemove:
                case PacketTypeFriendRecordAdd:
                case PacketTypeFriendRecordRemove:
                case PacketTypeMarriageRecordAdd:
                case PacketTypeMarriageRecordRemove:
                case PacketTypeNewYearCardRecordAdd:
                case PacketTypeNewYearCardRecordRemove:
                    return true;
                default:
                    return false;
            }
        }

        private void RecordWeddingPhotoScenePresentationPacket(int packetType, int payloadLength, int currentTimeMs)
        {
            if (!IsWeddingPhotoSceneOwnerActive)
            {
                return;
            }

            if (_weddingPhotoScenePresentationPacketTrail.Count >= WeddingPhotoScenePresentationTrailLimit)
            {
                _weddingPhotoScenePresentationPacketTrail.RemoveAt(0);
            }

            _weddingPhotoScenePresentationPacketTrail.Add(new WeddingPhotoScenePresentationPacketRecord(
                packetType,
                ResolveWeddingPhotoScenePresentationOwnerName(packetType),
                payloadLength,
                currentTimeMs));
            SyncWeddingPhotoScenePresentationStage(packetType);
        }

        private void RecordWeddingPhotoSceneUnhandledPacket(int packetType, int payloadLength, int currentTimeMs)
        {
            _weddingPhotoSceneUnhandledPacketCount++;
            _lastWeddingPhotoSceneUnhandledPacketType = packetType;
            RecordWeddingPhotoScenePresentationPacket(packetType, payloadLength, currentTimeMs);
        }

        private static string ResolveWeddingPhotoScenePresentationOwnerName(int packetType)
        {
            return packetType switch
            {
                PacketTypeWeddingProgress => "CField_WeddingPhoto wedding-progress choreography",
                PacketTypeWeddingCeremonyEnd => "CField_WeddingPhoto wedding-ceremony-end choreography",
                PacketTypeUserEnterField => "CField_WeddingPhoto remote user enter presentation",
                PacketTypeUserLeaveField => "CField_WeddingPhoto remote user leave presentation",
                PacketTypeUserMoveOfficial => "CField_WeddingPhoto remote user move presentation",
                PacketTypeUserMoveOrChairAlias => "CField_WeddingPhoto remote move/chair presentation",
                PacketTypeItemEffect => "CField_WeddingPhoto remote item-effect presentation",
                PacketTypeUserProfile => "CField_WeddingPhoto remote profile presentation",
                PacketTypeSetActivePortableChairLegacy => "CField_WeddingPhoto remote chair presentation",
                PacketTypeAvatarModified => "CField_WeddingPhoto remote avatar-modified presentation",
                PacketTypeTemporaryStatSet => "CField_WeddingPhoto remote temporary-stat set presentation",
                PacketTypeTemporaryStatReset => "CField_WeddingPhoto remote temporary-stat reset presentation",
                PacketTypeGuildNameChanged => "CField_WeddingPhoto remote guild-name presentation",
                PacketTypeGuildMarkChanged => "CField_WeddingPhoto remote guild-mark presentation",
                PacketTypeCoupleRecordAdd => "CField_WeddingPhoto remote couple-record add presentation",
                PacketTypeCoupleRecordRemove => "CField_WeddingPhoto remote couple-record remove presentation",
                PacketTypeFriendRecordAdd => "CField_WeddingPhoto remote friend-record add presentation",
                PacketTypeFriendRecordRemove => "CField_WeddingPhoto remote friend-record remove presentation",
                PacketTypeMarriageRecordAdd => "CField_WeddingPhoto remote marriage-record add presentation",
                PacketTypeMarriageRecordRemove => "CField_WeddingPhoto remote marriage-record remove presentation",
                PacketTypeNewYearCardRecordAdd => "CField_WeddingPhoto remote new-year-card add presentation",
                PacketTypeNewYearCardRecordRemove => "CField_WeddingPhoto remote new-year-card remove presentation",
                _ => "CField_WeddingPhoto remote presentation"
            };
        }

        internal string DescribeWeddingPhotoScenePresentationState()
        {
            if (!IsWeddingPhotoSceneOwnerActive)
            {
                return string.Empty;
            }

            SyncWeddingPhotoScenePresentationStage();

            string layerState = _ceremonyTextOverlayActive
                ? "text overlay"
                : _ceremonyCardOverlayActive
                    ? "card overlay"
                    : _ceremonyCelebrationActive
                        ? "celebration particles"
                        : "no ceremony layer";
            string stageSummary = $"stage {_weddingPhotoScenePresentationStage} (last stage packet {_lastWeddingPhotoSceneStagePacketType?.ToString(CultureInfo.InvariantCulture) ?? "none"})";

            if (_weddingPhotoScenePresentationPacketTrail.Count == 0)
            {
                return $"wedding-photo presentation packet trail is waiting for remote-user, chair, avatar, temporary-stat, guild, and relationship packets; active step {_currentStep}, layer state {layerState}, {stageSummary}, passthrough packets {_weddingPhotoSceneUnhandledPacketCount}.";
            }

            WeddingPhotoScenePresentationPacketRecord last = _weddingPhotoScenePresentationPacketTrail[^1];
            string passthroughSummary = _weddingPhotoSceneUnhandledPacketCount > 0
                ? $", passthrough packets {_weddingPhotoSceneUnhandledPacketCount} (last type {_lastWeddingPhotoSceneUnhandledPacketType?.ToString(CultureInfo.InvariantCulture) ?? "none"})"
                : ", passthrough packets 0";
            return $"wedding-photo presentation packet trail has {_weddingPhotoScenePresentationPacketTrail.Count} packet(s); last packet {last.PacketType} owned by {last.OwnerName}, payload {last.PayloadLength} byte(s), tick {last.Tick}; active step {_currentStep}, layer state {layerState}, {stageSummary}{passthroughSummary}.";
        }
        public void SetParticipantPosition(int characterId, Vector2 worldPosition)
        {
            if (characterId <= 0)
            {
                return;
            }


            _participantPositions[characterId] = worldPosition;

            UpdateParticipantState();

        }



        public bool TryGetRemoteParticipant(int characterId, out WeddingRemoteParticipantSnapshot snapshot)
        {

            if (_participantActors.TryGetValue(characterId, out WeddingRemoteParticipant participant))

            {

                snapshot = new WeddingRemoteParticipantSnapshot(
                    participant.CharacterId,
                    participant.Name,
                    participant.Role,
                    participant.Position,
                    participant.FacingRight,
                    participant.ActionName,
                    participant.Build?.Clone(),
                    participant.MovementSnapshot,
                    participant.PortableChairItemId,
                    participant.PortableChairPairCharacterId,
                    participant.TemporaryStats,
                    participant.TemporaryStatDelay,
                    participant.TemporaryStatRevision,
                    participant.PacketOwnedItemEffectItemId,
                    participant.PacketOwnedItemEffectRevision,
                    participant.AvatarModifiedState,
                    participant.AvatarModifiedRevision,
                    participant.NameTagRevision,
                    participant.ProfileMetadataRevision,
                    participant.GuildMarkRevision);
                return true;
            }



            snapshot = default;
            return false;
        }

        public IReadOnlyList<WeddingRemoteParticipantSnapshot> GetRemoteParticipantSnapshots()
        {
            List<WeddingRemoteParticipantSnapshot> snapshots = new(_participantActors.Count + _audienceActors.Count);
            foreach (WeddingRemoteParticipant participant in _participantActors.Values.Concat(_audienceActors.Values))
            {
                snapshots.Add(new WeddingRemoteParticipantSnapshot(
                    participant.CharacterId,
                    participant.Name,
                    participant.Role,
                    participant.Position,
                    participant.FacingRight,
                    participant.ActionName,
                    participant.Build?.Clone(),
                    participant.MovementSnapshot,
                    participant.PortableChairItemId,
                    participant.PortableChairPairCharacterId,
                    participant.TemporaryStats,
                    participant.TemporaryStatDelay,
                    participant.TemporaryStatRevision,
                    participant.PacketOwnedItemEffectItemId,
                    participant.PacketOwnedItemEffectRevision,
                    participant.AvatarModifiedState,
                    participant.AvatarModifiedRevision,
                    participant.NameTagRevision,
                    participant.ProfileMetadataRevision,
                    participant.GuildMarkRevision));
            }

            return snapshots;
        }

        public IReadOnlyList<WeddingRemoteParticipantSnapshot> GetExternalRendererParticipantSnapshots()
        {
            List<WeddingRemoteParticipantSnapshot> snapshots = new(_participantActors.Count + _audienceActors.Count);
            foreach (WeddingRemoteParticipant participant in _participantActors.Values.Concat(_audienceActors.Values))
            {
                if (!ShouldMirrorParticipantToExternalRenderer(participant))
                {
                    continue;
                }

                snapshots.Add(new WeddingRemoteParticipantSnapshot(
                    participant.CharacterId,
                    participant.Name,
                    participant.Role,
                    participant.Position,
                    participant.FacingRight,
                    participant.ActionName,
                    participant.Build?.Clone(),
                    participant.MovementSnapshot,
                    participant.PortableChairItemId,
                    participant.PortableChairPairCharacterId,
                    participant.TemporaryStats,
                    participant.TemporaryStatDelay,
                    participant.TemporaryStatRevision,
                    participant.PacketOwnedItemEffectItemId,
                    participant.PacketOwnedItemEffectRevision,
                    participant.AvatarModifiedState,
                    participant.AvatarModifiedRevision,
                    participant.NameTagRevision,
                    participant.ProfileMetadataRevision,
                    participant.GuildMarkRevision));
            }

            return snapshots;
        }

        public int PendingExternalRemoteActorLoadCount => _pendingExternalRemoteActorLoadIds.Count;

        public int LoadedExternalRemoteActorCount => _loadedExternalRemoteActorIds.Count;

        private bool UsesOfficialRemoteLifecycle(WeddingRemoteParticipant participant)
        {
            return participant != null
                && participant.CharacterId > 0
                && _officialRemoteLifecycleActorIds.Contains(participant.CharacterId);
        }

        private bool ShouldMirrorParticipantToExternalRenderer(WeddingRemoteParticipant participant)
        {
            if (participant == null)
            {
                return false;
            }

            return !UsesOfficialRemoteLifecycle(participant)
                || _loadedExternalRemoteActorIds.Contains(participant.CharacterId);
        }

        private bool ShouldDrawParticipantInternally(WeddingRemoteParticipant participant)
        {
            if (!UseExternalRemoteActorRenderer)
            {
                return true;
            }

            return !ShouldMirrorParticipantToExternalRenderer(participant);
        }

        private void QueueExternalRemoteActorLoad(int characterId)
        {
            if (characterId <= 0
                || !_officialRemoteLifecycleActorIds.Contains(characterId)
                || _loadedExternalRemoteActorIds.Contains(characterId))
            {
                return;
            }

            if (_pendingExternalRemoteActorLoadIds.Add(characterId))
            {
                _pendingExternalRemoteActorLoads.Enqueue(characterId);
            }
        }

        private void RemoveExternalRemoteActorTracking(int characterId)
        {
            if (characterId <= 0)
            {
                return;
            }

            _pendingExternalRemoteActorLoadIds.Remove(characterId);
            _loadedExternalRemoteActorIds.Remove(characterId);
            _officialRemoteLifecycleActorIds.Remove(characterId);
            if (_pendingExternalRemoteActorLoadIds.Count == 0)
            {
                _externalRemoteActorLoadWindowEndTimeMs = 0;
                _externalRemoteActorLoadCooltimeEndTimeMs = 0;
            }
        }

        private void AdvanceExternalRemoteActorLoadLifecycle(int currentTimeMs)
        {
            if (_pendingExternalRemoteActorLoadIds.Count == 0)
            {
                _externalRemoteActorLoadWindowEndTimeMs = 0;
                _externalRemoteActorLoadCooltimeEndTimeMs = 0;
                return;
            }

            if (_externalRemoteActorLoadCooltimeEndTimeMs != 0)
            {
                if (currentTimeMs < _externalRemoteActorLoadCooltimeEndTimeMs)
                {
                    return;
                }

                _externalRemoteActorLoadCooltimeEndTimeMs = 0;
            }

            if (_externalRemoteActorLoadWindowEndTimeMs == 0)
            {
                _externalRemoteActorLoadWindowEndTimeMs = currentTimeMs + RemoteDelayedLoadWindowMs;
                if (_externalRemoteActorLoadWindowEndTimeMs == 0)
                {
                    _externalRemoteActorLoadWindowEndTimeMs = 1;
                }
            }

            if (currentTimeMs >= _externalRemoteActorLoadWindowEndTimeMs)
            {
                _externalRemoteActorLoadWindowEndTimeMs = 0;
                if (_pendingExternalRemoteActorLoadIds.Count > 0)
                {
                    _externalRemoteActorLoadCooltimeEndTimeMs = currentTimeMs + RemoteDelayedLoadCooltimeMs;
                    if (_externalRemoteActorLoadCooltimeEndTimeMs == 0)
                    {
                        _externalRemoteActorLoadCooltimeEndTimeMs = 1;
                    }
                }

                return;
            }

            while (_pendingExternalRemoteActorLoads.Count > 0)
            {
                int characterId = _pendingExternalRemoteActorLoads.Dequeue();
                if (!_pendingExternalRemoteActorLoadIds.Remove(characterId))
                {
                    continue;
                }

                if (_participantActors.ContainsKey(characterId) || TryGetAudienceActorById(characterId, out _))
                {
                    _loadedExternalRemoteActorIds.Add(characterId);
                }

                break;
            }

            if (_pendingExternalRemoteActorLoadIds.Count == 0)
            {
                _externalRemoteActorLoadWindowEndTimeMs = 0;
                _externalRemoteActorLoadCooltimeEndTimeMs = 0;
            }
        }

        public bool TryConfigureParticipantActor(
            int characterId,
            Vector2? worldPosition,
            CharacterBuild build,
            bool? facingRight,
            string actionName,
            out string message)
        {
            message = null;
            if (characterId <= 0)
            {
                message = "Wedding participant ID is missing.";
                return false;
            }

            if (characterId != _groomId && characterId != _brideId)
            {
                message = $"Wedding participant {characterId} is not the active groom or bride.";
                return false;
            }

            if (worldPosition.HasValue)
            {
                _participantPositions[characterId] = worldPosition.Value;
            }

            UpdateParticipantState();
            if (!_participantActors.TryGetValue(characterId, out WeddingRemoteParticipant participant))
            {
                message = $"Wedding participant {characterId} does not have a resolved overlay position yet.";
                return false;
            }

            ApplyParticipantPresentation(participant, build, facingRight, actionName);
            if (worldPosition.HasValue)
            {
                participant.Position = worldPosition.Value;
            }

            participant.MovementSnapshot = null;
            participant.MovementDrivenActionSelection = false;
            TryApplyPendingRemoteParticipantOperations(participant);

            return true;
        }

        public void UpsertAudienceParticipant(CharacterBuild build, Vector2 worldPosition, bool facingRight, string actionName = null)
        {
            UpsertAudienceParticipant(build, worldPosition, facingRight, actionName, build?.Id);
        }

        public void UpsertAudienceParticipant(CharacterBuild build, Vector2 worldPosition, bool facingRight, string actionName, int? characterId)
        {
            if (build == null)
            {
                return;
            }

            string actorName = string.IsNullOrWhiteSpace(build.Name) ? "Guest" : build.Name.Trim();
            WeddingRemoteParticipant participant = null;
            if (characterId.HasValue
                && _audienceActorNamesById.TryGetValue(characterId.Value, out string previousName)
                && !string.Equals(previousName, actorName, StringComparison.OrdinalIgnoreCase)
                && _audienceActors.TryGetValue(previousName, out participant))
            {
                _audienceActors.Remove(previousName);
                if (_audienceActors.TryGetValue(actorName, out WeddingRemoteParticipant replacedParticipant)
                    && replacedParticipant.CharacterId > 0
                    && replacedParticipant.CharacterId != characterId.Value)
                {
                    _audienceActorNamesById.Remove(replacedParticipant.CharacterId);
                }

                _audienceActors[actorName] = participant;
            }

            if (participant == null && !_audienceActors.TryGetValue(actorName, out participant))
            {
                CharacterBuild actorBuild = build.Clone();
                actorBuild.Name = actorName;
                participant = new WeddingRemoteParticipant(
                    characterId ?? actorBuild.Id,
                    WeddingParticipantRole.Guest,
                    actorBuild.Name,
                    actorBuild,
                    new CharacterAssembler(actorBuild));
                _audienceActors[actorName] = participant;
            }

            participant.CharacterId = characterId ?? participant.CharacterId;
            ApplyParticipantPresentation(participant, build, facingRight, actionName);
            participant.Position = worldPosition;
            participant.MovementSnapshot = null;
            participant.MovementDrivenActionSelection = false;
            TryApplyPendingRemoteParticipantOperations(participant);
            if (participant.CharacterId > 0)
            {
                _audienceActorNamesById[participant.CharacterId] = actorName;
            }
        }

        public bool TryMoveAudienceParticipant(string name, Vector2 worldPosition, bool? facingRight, string actionName, out string message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                message = "Wedding guest name is missing.";
                return false;
            }

            if (!_audienceActors.TryGetValue(name.Trim(), out WeddingRemoteParticipant participant))
            {
                message = $"Wedding guest '{name}' does not exist.";
                return false;
            }

            participant.Position = worldPosition;
            ApplyParticipantPresentation(participant, build: null, facingRight, actionName);
            participant.MovementSnapshot = null;
            participant.MovementDrivenActionSelection = false;
            return true;
        }

        public bool RemoveAudienceParticipant(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string normalized = name.Trim();
            if (!_audienceActors.TryGetValue(normalized, out WeddingRemoteParticipant participant))
            {
                return false;
            }

            if (participant.CharacterId > 0)
            {
                _audienceActorNamesById.Remove(participant.CharacterId);
                RemoveExternalRemoteActorTracking(participant.CharacterId);
                _pendingRemoteParticipantOperationsByCharacterId.Remove(participant.CharacterId);
                RemoveRelationshipRecordDispatchKeysForOwnerAcrossTypes(participant.CharacterId);
            }

            return _audienceActors.Remove(normalized);
        }

        public void ClearAudienceParticipants()
        {
            foreach (int characterId in _audienceActorNamesById.Keys.ToArray())
            {
                RemoveExternalRemoteActorTracking(characterId);
                _pendingRemoteParticipantOperationsByCharacterId.Remove(characterId);
                RemoveRelationshipRecordDispatchKeysForOwnerAcrossTypes(characterId);
            }

            _audienceActors.Clear();
            _audienceActorNamesById.Clear();
        }

        public bool TryMoveAudienceParticipantById(int characterId, Vector2 worldPosition, bool? facingRight, string actionName, out string message)
        {
            message = null;
            if (!_audienceActorNamesById.TryGetValue(characterId, out string actorName))
            {
                message = $"Wedding guest id {characterId} does not exist.";
                return false;
            }

            return TryMoveAudienceParticipant(actorName, worldPosition, facingRight, actionName, out message);
        }

        public bool RemoveAudienceParticipantById(int characterId)
        {
            return _audienceActorNamesById.TryGetValue(characterId, out string actorName)
                && RemoveAudienceParticipant(actorName);
        }

        public bool TryGetAudienceParticipantById(int characterId, out WeddingRemoteParticipantSnapshot snapshot)
        {
            snapshot = default;
            if (!_audienceActorNamesById.TryGetValue(characterId, out string actorName)
                || !_audienceActors.TryGetValue(actorName, out WeddingRemoteParticipant participant))
            {
                return false;
            }

            snapshot = new WeddingRemoteParticipantSnapshot(
                participant.CharacterId,
                participant.Name,
                participant.Role,
                participant.Position,
                participant.FacingRight,
                participant.ActionName,
                participant.Build?.Clone(),
                participant.MovementSnapshot,
                participant.PortableChairItemId,
                participant.PortableChairPairCharacterId,
                participant.TemporaryStats,
                participant.TemporaryStatDelay,
                participant.TemporaryStatRevision,
                participant.PacketOwnedItemEffectItemId,
                participant.PacketOwnedItemEffectRevision,
                participant.AvatarModifiedState,
                participant.AvatarModifiedRevision,
                participant.NameTagRevision,
                participant.ProfileMetadataRevision,
                participant.GuildMarkRevision);
            return true;
        }

        private bool TryApplyWeddingProgressPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (payload == null || payload.Length < 9)
            {
                errorMessage = "Wedding progress packet expects 9 bytes: <step:1><groomId:4><brideId:4>.";
                return false;
            }

            int step = payload[0];
            int groomId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1, sizeof(int)));
            int brideId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(5, sizeof(int)));
            OnWeddingProgress(step, groomId, brideId, currentTimeMs);
            return true;
        }

        private bool TryApplyWeddingCeremonyEndPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            OnWeddingCeremonyEnd(currentTimeMs);
            return true;
        }

        private bool TryApplyMoveOrChairAliasPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            if (payload != null
                && (payload.Length == sizeof(int) * 2 || payload.Length == sizeof(int) * 3))
            {
                return TryApplyRemoteChairPacket(payload, out errorMessage);
            }

            return TryApplyRemoteMovePacket(payload, currentTimeMs, out errorMessage);
        }

        private bool TryGetAudienceActorById(int characterId, out WeddingRemoteParticipant participant)
        {
            participant = null;
            return _audienceActorNamesById.TryGetValue(characterId, out string actorName)
                && _audienceActors.TryGetValue(actorName, out participant);
        }

        private static void ApplyParticipantPresentation(
            WeddingRemoteParticipant participant,
            CharacterBuild build,
            bool? facingRight,
            string actionName)
        {
            if (participant == null)
            {
                return;
            }

            if (build != null)
            {
                WeddingParticipantNameTagSignature previousNameTag = CreateNameTagSignature(participant);
                CharacterBuild actorBuild = build.Clone();
                if (string.IsNullOrWhiteSpace(actorBuild.Name))
                {
                    actorBuild.Name = participant.Name;
                }

                participant.Name = actorBuild.Name;
                participant.Build = actorBuild;
                participant.Assembler = new CharacterAssembler(actorBuild);
                participant.HasExplicitBuild = true;
                if (!previousNameTag.Equals(CreateNameTagSignature(participant))
                    || participant.NameTagRevision == 0)
                {
                    RefreshParticipantNameTag(participant);
                }
            }

            if (facingRight.HasValue)
            {
                participant.FacingRight = facingRight.Value;
                participant.HasExplicitFacing = true;
            }

            if (!string.IsNullOrWhiteSpace(actionName))
            {
                participant.BaseActionName = actionName.Trim();
                participant.ActionName = ResolveVisibleParticipantActionName(participant, participant.BaseActionName);
                participant.HasExplicitAction = true;
            }
            else if (string.IsNullOrWhiteSpace(participant.ActionName))
            {
                participant.ActionName = ResolveVisibleParticipantActionName(participant, participant.BaseActionName);
            }
        }

        private static void ApplyParticipantMovementSnapshot(
            WeddingRemoteParticipant participant,
            PlayerMovementSyncSnapshot movementSnapshot,
            int currentTimeMs)
        {
            if (participant == null || movementSnapshot == null)
            {
                return;
            }

            participant.MovementSnapshot = movementSnapshot;
            participant.MovementDrivenActionSelection = true;
            UpdateParticipantMovementSnapshot(participant, currentTimeMs);
        }

        private static void UpdateParticipantMovementSnapshot(
            WeddingRemoteParticipant participant,
            int currentTimeMs)
        {
            if (participant?.MovementSnapshot == null)
            {
                return;
            }

            Physics.PassivePositionSnapshot sampled = participant.MovementSnapshot.SampleAtTime(currentTimeMs);
            participant.Position = new Vector2(sampled.X, sampled.Y);
            participant.FacingRight = sampled.FacingRight;
            if (participant.MovementDrivenActionSelection)
            {
                participant.BaseActionName = ResolveRemoteActionName(sampled.Action, participant.Build?.ActivePortableChair?.ItemId ?? 0);
                participant.ActionName = ResolveVisibleParticipantActionName(participant, participant.BaseActionName);
            }
        }

        private void UpdateRemoteParticipantMovementSnapshots(int currentTimeMs)
        {
            foreach (WeddingRemoteParticipant participant in _participantActors.Values.Concat(_audienceActors.Values))
            {
                if (participant.MovementSnapshot == null)
                {
                    continue;
                }

                UpdateParticipantMovementSnapshot(participant, currentTimeMs);
            }
        }

        private void PromoteAudienceActorToParticipant(int characterId, WeddingParticipantRole role, Vector2? fallbackPosition)
        {
            if (characterId <= 0
                || !_audienceActorNamesById.TryGetValue(characterId, out string actorName)
                || !_audienceActors.TryGetValue(actorName, out WeddingRemoteParticipant participant))
            {
                return;
            }

            _audienceActors.Remove(actorName);
            _audienceActorNamesById.Remove(characterId);
            if (TryConfigureParticipantActor(
                characterId,
                fallbackPosition ?? participant.Position,
                participant.Build,
                participant.FacingRight,
                participant.ActionName,
                out _)
                && _participantActors.TryGetValue(characterId, out WeddingRemoteParticipant promotedParticipant))
            {
                CopyPromotedAudienceParticipantState(participant, promotedParticipant);
            }
        }

        private static void CopyPromotedAudienceParticipantState(WeddingRemoteParticipant source, WeddingRemoteParticipant destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.PortableChairItemId = source.PortableChairItemId;
            destination.PortableChairPairCharacterId = source.PortableChairPairCharacterId;
            destination.TemporaryStats = source.TemporaryStats;
            destination.TemporaryStatDelay = source.TemporaryStatDelay;
            destination.TemporaryStatRevision = Math.Max(destination.TemporaryStatRevision, source.TemporaryStatRevision);
            destination.MovementSnapshot = source.MovementSnapshot;
            destination.MovementDrivenActionSelection = source.MovementDrivenActionSelection;
            destination.PacketOwnedItemEffect = source.PacketOwnedItemEffect;
            destination.PacketOwnedItemEffectItemId = source.PacketOwnedItemEffectItemId;
            destination.PacketOwnedItemEffectRevision = source.PacketOwnedItemEffectRevision;
            destination.AvatarModifiedState = source.AvatarModifiedState;
            destination.AvatarModifiedRevision = source.AvatarModifiedRevision;
            destination.HasExplicitFacing = source.HasExplicitFacing;
            destination.HasExplicitAction = source.HasExplicitAction;
            destination.HasExplicitBuild = source.HasExplicitBuild;
            destination.NameTagRevision = Math.Max(destination.NameTagRevision, source.NameTagRevision);
            destination.ProfileMetadataRevision = Math.Max(destination.ProfileMetadataRevision, source.ProfileMetadataRevision);
            destination.GuildMarkRevision = Math.Max(destination.GuildMarkRevision, source.GuildMarkRevision);
            ApplyParticipantTemporaryStatPresentation(destination);
            if (!string.IsNullOrWhiteSpace(source.BaseActionName))
            {
                destination.BaseActionName = source.BaseActionName;
            }
        }

        private bool TryApplyRemoteSpawnPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!TryDecodeRemoteSpawnPacket(payload, out WeddingRemoteSpawnPacket spawn, out errorMessage))
            {
                return false;
            }

            return TryApplyRemoteSpawnPacket(spawn, out errorMessage);
        }

        internal bool TryApplyRemoteSpawnPacket(WeddingRemoteSpawnPacket spawn, out string errorMessage)
        {
            errorMessage = null;
            CharacterBuild build = CreateRemoteBuildFromAvatarLook(spawn.Name, spawn.AvatarLook, out errorMessage);
            if (build == null)
            {
                return false;
            }

            ApplyRemoteSpawnMetadata(build, spawn);
            string actionName = ResolveRemoteActionName(spawn.MoveAction, spawn.PortableChairItemId);
            bool facingRight = (spawn.MoveAction & 1) == 0;
            _officialRemoteLifecycleActorIds.Add(spawn.CharacterId);
            if (spawn.CharacterId == _groomId || spawn.CharacterId == _brideId)
            {
                bool configured = TryConfigureParticipantActor(spawn.CharacterId, spawn.Position, build, facingRight, actionName, out errorMessage);
                if (configured)
                {
                    if (_participantActors.TryGetValue(spawn.CharacterId, out WeddingRemoteParticipant participant))
                    {
                        SetParticipantTemporaryStats(participant, spawn.TemporaryStats);
                    }

                    ApplyParticipantPortableChairState(spawn.CharacterId, spawn.PortableChairItemId, pairCharacterId: null);
                    QueueExternalRemoteActorLoad(spawn.CharacterId);
                }

                return configured;
            }

            UpsertAudienceParticipant(build, spawn.Position, facingRight, actionName, spawn.CharacterId);
            if (TryGetAudienceActorById(spawn.CharacterId, out WeddingRemoteParticipant audienceParticipant))
            {
                SetParticipantTemporaryStats(audienceParticipant, spawn.TemporaryStats);
            }

            ApplyParticipantPortableChairState(spawn.CharacterId, spawn.PortableChairItemId, pairCharacterId: null);
            QueueExternalRemoteActorLoad(spawn.CharacterId);
            return true;
        }

        private bool TryApplyRemoteLeavePacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!TryDecodeRemoteCharacterIdPacket(payload, PacketTypeUserLeaveField, out int characterId, out errorMessage))
            {
                return false;
            }

            bool removed = RemoveAudienceParticipantById(characterId);
            if (_participantActors.Remove(characterId))
            {
                removed = true;
            }

            if (_participantPositions.Remove(characterId))
            {
                removed = true;
            }

            if (_pendingRemoteParticipantOperationsByCharacterId.Remove(characterId))
            {
                removed = true;
            }

            if (characterId == _groomId && LocalParticipantRole != WeddingParticipantRole.Groom)
            {
                _groomPosition = null;
                removed = true;
            }

            if (characterId == _brideId && LocalParticipantRole != WeddingParticipantRole.Bride)
            {
                _bridePosition = null;
                removed = true;
            }

            if (!removed)
            {
                errorMessage = $"Wedding remote actor id {characterId} does not exist.";
                return false;
            }

            RemoveExternalRemoteActorTracking(characterId);
            UpdateParticipantState();
            return true;
        }

        private bool TryApplyRemoteMovePacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!TryDecodeRemoteMovePacket(payload, currentTimeMs, out WeddingRemoteMovePacket move, out errorMessage))
            {
                return false;
            }

            string actionName = ResolveRemoteActionName(move.MoveAction, portableChairItemId: 0);
            bool facingRight = (move.MoveAction & 1) == 0;
            if (move.CharacterId == _groomId || move.CharacterId == _brideId)
            {
                if (!TryConfigureParticipantActor(move.CharacterId, move.Position, build: null, facingRight, actionName, out errorMessage))
                {
                    return false;
                }

                if (_participantActors.TryGetValue(move.CharacterId, out WeddingRemoteParticipant participant))
                {
                    ApplyParticipantMovementSnapshot(participant, move.MovementSnapshot, currentTimeMs);
                }

                return true;
            }

            if (!TryMoveAudienceParticipantById(move.CharacterId, move.Position, facingRight, actionName, out errorMessage))
            {
                return false;
            }

            if (TryGetAudienceActorById(move.CharacterId, out WeddingRemoteParticipant audienceParticipant))
            {
                ApplyParticipantMovementSnapshot(audienceParticipant, move.MovementSnapshot, currentTimeMs);
            }

            return true;
        }

        private bool TryApplyRemoteChairPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!TryDecodeRemoteChairPacket(payload, out WeddingRemoteChairPacket chair, out errorMessage))
            {
                return false;
            }

            string actionName = chair.PortableChairItemId > 0
                ? CharacterPart.GetActionString(CharacterAction.Sit)
                : CharacterPart.GetActionString(CharacterAction.Stand1);
            if (!ApplyParticipantPortableChairState(chair.CharacterId, chair.PortableChairItemId, chair.PairCharacterId))
            {
                errorMessage = $"Wedding remote actor id {chair.CharacterId} does not exist.";
                return false;
            }

            if (chair.CharacterId == _groomId || chair.CharacterId == _brideId)
            {
                if (!TryGetRemoteParticipant(chair.CharacterId, out WeddingRemoteParticipantSnapshot snapshot))
                {
                    errorMessage = $"Wedding remote participant id {chair.CharacterId} does not exist.";
                    return false;
                }

                return TryConfigureParticipantActor(chair.CharacterId, snapshot.Position, build: null, facingRight: null, actionName, out errorMessage);
            }

            if (!TryGetAudienceParticipantById(chair.CharacterId, out WeddingRemoteParticipantSnapshot audienceSnapshot))
            {
                errorMessage = $"Wedding guest id {chair.CharacterId} does not exist.";
                return false;
            }

            return TryMoveAudienceParticipantById(chair.CharacterId, audienceSnapshot.Position, facingRight: null, actionName, out errorMessage);
        }

        private bool TryApplyRemoteAvatarModifiedPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!TryDecodeRemoteAvatarModifiedPacket(payload, out RemoteUserAvatarModifiedPacket packet, out errorMessage))
            {
                return false;
            }

            if (packet.AvatarLook == null)
            {
                if (_participantActors.TryGetValue(packet.CharacterId, out WeddingRemoteParticipant participantWithoutLook))
                {
                    StoreAvatarModifiedState(participantWithoutLook, packet);
                    ApplyAvatarModifiedStateToBuild(participantWithoutLook.Build, packet);
                    return true;
                }

                if (TryGetAudienceActorById(packet.CharacterId, out WeddingRemoteParticipant audienceWithoutLook))
                {
                    StoreAvatarModifiedState(audienceWithoutLook, packet);
                    ApplyAvatarModifiedStateToBuild(audienceWithoutLook.Build, packet);
                    return true;
                }

                errorMessage = $"Wedding remote actor id {packet.CharacterId} does not exist.";
                return false;
            }

            if (packet.CharacterId == _groomId || packet.CharacterId == _brideId)
            {
                string actorName = packet.CharacterId == _groomId ? "Groom" : "Bride";
                Vector2? position = packet.CharacterId == _groomId ? _groomPosition : _bridePosition;
                bool? facingRight = null;
                string actionName = null;
                if (TryGetRemoteParticipant(packet.CharacterId, out WeddingRemoteParticipantSnapshot participantSnapshot))
                {
                    actorName = participantSnapshot.Name;
                    position = participantSnapshot.Position;
                    facingRight = participantSnapshot.FacingRight;
                    actionName = participantSnapshot.ActionName;
                }

                CharacterBuild build = CreateRemoteBuildFromAvatarLook(actorName, packet.AvatarLook, out errorMessage);
                if (build == null)
                {
                    return false;
                }

                CopyPersistentBuildMetadata(build, participantSnapshot.Build);
                ApplyAvatarModifiedStateToBuild(build, packet);
                bool configured = TryConfigureParticipantActor(packet.CharacterId, position, build, facingRight, actionName, out errorMessage);
                if (configured && _participantActors.TryGetValue(packet.CharacterId, out WeddingRemoteParticipant participant))
                {
                    StoreAvatarModifiedState(participant, packet);
                }

                return configured;
            }

            if (!TryGetAudienceParticipantById(packet.CharacterId, out WeddingRemoteParticipantSnapshot audienceSnapshot))
            {
                errorMessage = $"Wedding guest id {packet.CharacterId} does not exist.";
                return false;
            }

            CharacterBuild audienceBuild = CreateRemoteBuildFromAvatarLook(audienceSnapshot.Name, packet.AvatarLook, out errorMessage);
            if (audienceBuild == null)
            {
                return false;
            }

            CopyPersistentBuildMetadata(audienceBuild, audienceSnapshot.Build);
            ApplyAvatarModifiedStateToBuild(audienceBuild, packet);
            UpsertAudienceParticipant(audienceBuild, audienceSnapshot.Position, audienceSnapshot.FacingRight, audienceSnapshot.ActionName, packet.CharacterId);
            if (TryGetAudienceActorById(packet.CharacterId, out WeddingRemoteParticipant audienceParticipant))
            {
                StoreAvatarModifiedState(audienceParticipant, packet);
            }

            ApplyParticipantPortableChairState(packet.CharacterId, audienceSnapshot.PortableChairItemId ?? 0, audienceSnapshot.PortableChairPairCharacterId);
            return true;
        }

        private bool TryApplyRemoteTemporaryStatSetPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseTemporaryStatSet(payload, out RemoteUserTemporaryStatSetPacket packet, out errorMessage))
            {
                return false;
            }

            return TryApplyRemoteTemporaryStatSetPacket(packet, out errorMessage);
        }

        internal bool TryApplyRemoteTemporaryStatSetPacket(RemoteUserTemporaryStatSetPacket packet, out string errorMessage)
        {
            errorMessage = null;
            if (!TryResolveParticipantForTemporaryStats(packet.CharacterId, out WeddingRemoteParticipant participant, out _))
            {
                QueuePendingRemoteParticipantOperation(
                    packet.CharacterId,
                    new PendingWeddingRemoteParticipantOperation(
                        PendingWeddingRemoteParticipantOperationType.TemporaryStatSet,
                        packet.TemporaryStats,
                        packet.Delay,
                        Array.Empty<int>(),
                        null,
                        default,
                        0,
                        0,
                        0,
                        0));
                return true;
            }

            SetParticipantTemporaryStats(participant, packet.TemporaryStats, packet.Delay);
            return true;
        }

        private bool TryApplyRemoteTemporaryStatResetPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseTemporaryStatReset(payload, out RemoteUserTemporaryStatResetPacket packet, out errorMessage))
            {
                return false;
            }

            return TryApplyRemoteTemporaryStatResetPacket(packet, out errorMessage);
        }

        internal bool TryApplyRemoteTemporaryStatResetPacket(RemoteUserTemporaryStatResetPacket packet, out string errorMessage)
        {
            errorMessage = null;
            if (!TryResolveParticipantForTemporaryStats(packet.CharacterId, out WeddingRemoteParticipant participant, out _))
            {
                QueuePendingRemoteParticipantOperation(
                    packet.CharacterId,
                    new PendingWeddingRemoteParticipantOperation(
                        PendingWeddingRemoteParticipantOperationType.TemporaryStatReset,
                        default,
                        0,
                        packet.MaskWords ?? Array.Empty<int>(),
                        null,
                        default,
                        0,
                        0,
                        0,
                        0));
                return true;
            }

            ApplyParticipantTemporaryStatReset(participant, packet.MaskWords ?? Array.Empty<int>());
            return true;
        }

        private bool TryApplyRemoteGuildNameChangedPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseGuildNameChanged(payload, out RemoteUserGuildNameChangedPacket packet, out errorMessage))
            {
                return false;
            }

            return TryApplyRemoteGuildNameChangedPacket(packet, out errorMessage);
        }

        internal bool TryApplyRemoteGuildNameChangedPacket(RemoteUserGuildNameChangedPacket packet, out string errorMessage)
        {
            errorMessage = null;
            if (!TryResolveParticipantForTemporaryStats(packet.CharacterId, out WeddingRemoteParticipant participant, out _))
            {
                QueuePendingRemoteParticipantOperation(
                    packet.CharacterId,
                    new PendingWeddingRemoteParticipantOperation(
                        PendingWeddingRemoteParticipantOperationType.GuildNameChanged,
                        default,
                        0,
                        Array.Empty<int>(),
                        packet.GuildName,
                        default,
                        0,
                        0,
                        0,
                        0));
                return true;
            }

            ApplyParticipantGuildNameChanged(participant, packet.GuildName);
            return true;
        }

        private bool TryApplyRemoteProfilePacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseProfile(payload, out RemoteUserProfilePacket packet, out errorMessage))
            {
                return false;
            }

            return TryApplyRemoteProfilePacket(packet, out errorMessage);
        }

        internal bool TryApplyRemoteProfilePacket(RemoteUserProfilePacket packet, out string errorMessage)
        {
            errorMessage = null;
            if (!TryResolveParticipantForTemporaryStats(packet.CharacterId, out WeddingRemoteParticipant participant, out _))
            {
                QueuePendingRemoteParticipantOperation(
                    packet.CharacterId,
                    new PendingWeddingRemoteParticipantOperation(
                        PendingWeddingRemoteParticipantOperationType.Profile,
                        default,
                        0,
                        Array.Empty<int>(),
                        null,
                        packet,
                        0,
                        0,
                        0,
                        0));
                return true;
            }

            ApplyParticipantProfileMetadata(participant, packet);
            return true;
        }

        private bool TryApplyRemoteGuildMarkChangedPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseGuildMarkChanged(payload, out RemoteUserGuildMarkChangedPacket packet, out errorMessage))
            {
                return false;
            }

            return TryApplyRemoteGuildMarkChangedPacket(packet, out errorMessage);
        }

        internal bool TryApplyRemoteGuildMarkChangedPacket(RemoteUserGuildMarkChangedPacket packet, out string errorMessage)
        {
            errorMessage = null;
            if (!TryResolveParticipantForTemporaryStats(packet.CharacterId, out WeddingRemoteParticipant participant, out _))
            {
                QueuePendingRemoteParticipantOperation(
                    packet.CharacterId,
                    new PendingWeddingRemoteParticipantOperation(
                        PendingWeddingRemoteParticipantOperationType.GuildMarkChanged,
                        default,
                        0,
                        Array.Empty<int>(),
                        null,
                        default,
                        packet.MarkBackgroundId,
                        packet.MarkBackgroundColor,
                        packet.MarkId,
                        packet.MarkColor));
                return true;
            }

            ApplyParticipantGuildMarkChanged(
                participant,
                packet.MarkBackgroundId,
                packet.MarkBackgroundColor,
                packet.MarkId,
                packet.MarkColor);
            return true;
        }

        private bool TryApplyRemoteItemEffectPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseItemEffect(payload, out RemoteUserItemEffectPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryResolveParticipantForTemporaryStats(packet.CharacterId, out WeddingRemoteParticipant participant, out errorMessage))
            {
                return false;
            }

            if (packet.RelationshipType == RemoteRelationshipOverlayType.Generic)
            {
                return TryApplyParticipantGenericItemEffect(
                    participant,
                    packet.ItemId,
                    Environment.TickCount,
                    out errorMessage,
                    _characterLoader);
            }

            RemoteUserAvatarModifiedPacket state = participant.AvatarModifiedState
                ?? CreateDefaultAvatarModifiedState(packet.CharacterId);
            RemoteUserRelationshipRecord relationshipRecord = new(
                IsActive: packet.ItemId.HasValue && packet.ItemId.Value > 0,
                ItemId: packet.ItemId ?? 0,
                ItemSerial: null,
                PairItemSerial: null,
                CharacterId: packet.CharacterId,
                PairCharacterId: packet.PairCharacterId);
            state = ApplyRelationshipRecordToAvatarModifiedState(state, packet.RelationshipType, relationshipRecord);

            StoreAvatarModifiedState(participant, state);
            ApplyAvatarModifiedStateToBuild(participant.Build, state);
            return true;
        }

        private bool TryApplyRemoteRelationshipRecordAddPacket(int packetType, byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseRelationshipRecordAdd(packetType, payload, out RemoteUserRelationshipRecordPacket packet, out errorMessage))
            {
                return false;
            }

            int characterId = packet.RelationshipRecord.CharacterId ?? 0;
            if (characterId <= 0)
            {
                errorMessage = $"{packet.RelationshipType} relationship add packet does not contain a valid owner character ID.";
                return false;
            }

            RemoteUserRelationshipRecord relationshipRecord = NormalizeWeddingRelationshipRecordForApply(
                packet.RelationshipType,
                packet.RelationshipRecord,
                packet.DispatchKey,
                packet.PairLookupSerial);

            RegisterRelationshipRecordDispatchKeys(
                packet.RelationshipType,
                packet.DispatchKey,
                relationshipRecord,
                characterId);

            if (!TryResolveParticipantForTemporaryStats(characterId, out WeddingRemoteParticipant participant, out _))
            {
                QueuePendingRemoteParticipantOperation(
                    characterId,
                    new PendingWeddingRemoteParticipantOperation(
                        PendingWeddingRemoteParticipantOperationType.RelationshipRecordAdd,
                        default,
                        0,
                        Array.Empty<int>(),
                        null,
                        default,
                        0,
                        0,
                        0,
                        0,
                        packet.RelationshipType,
                        relationshipRecord,
                        null,
                        null,
                        packet.DispatchKey,
                        packet.PairLookupSerial));
                return true;
            }

            ApplyRelationshipRecordAdd(participant, packet.RelationshipType, relationshipRecord, packet.DispatchKey, packet.PairLookupSerial);
            return true;
        }

        private bool TryApplyRemoteRelationshipRecordRemovePacket(int packetType, byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseRelationshipRecordRemove(packetType, payload, out RemoteUserRelationshipRecordRemovePacket packet, out errorMessage))
            {
                return false;
            }

            if (TryResolveRelationshipRecordOwnerFromDispatchKey(packet.RelationshipType, packet.DispatchKey, out int mappedOwnerCharacterId))
            {
                if (!TryResolveParticipantForTemporaryStats(mappedOwnerCharacterId, out WeddingRemoteParticipant mappedParticipant, out _))
                {
                    QueuePendingRemoteParticipantOperation(
                        mappedOwnerCharacterId,
                        new PendingWeddingRemoteParticipantOperation(
                            PendingWeddingRemoteParticipantOperationType.RelationshipRecordRemove,
                            default,
                            0,
                            Array.Empty<int>(),
                            null,
                            default,
                            0,
                            0,
                            0,
                            0,
                            packet.RelationshipType,
                            default,
                            mappedOwnerCharacterId,
                            packet.ItemSerial));
                    return true;
                }

                if (ApplyRelationshipRecordRemove(mappedParticipant, packet.RelationshipType, mappedOwnerCharacterId, packet.ItemSerial))
                {
                    RemoveRelationshipRecordDispatchKeysForOwner(packet.RelationshipType, mappedOwnerCharacterId);
                    return true;
                }
            }

            if (packet.CharacterId.HasValue
                && packet.CharacterId.Value > 0
                && !TryResolveParticipantForTemporaryStats(packet.CharacterId.Value, out _, out _))
            {
                QueuePendingRemoteParticipantOperation(
                    packet.CharacterId.Value,
                    new PendingWeddingRemoteParticipantOperation(
                        PendingWeddingRemoteParticipantOperationType.RelationshipRecordRemove,
                        default,
                        0,
                        Array.Empty<int>(),
                        null,
                        default,
                        0,
                        0,
                        0,
                        0,
                        packet.RelationshipType,
                        default,
                        packet.CharacterId,
                        packet.ItemSerial));
                return true;
            }

            int affectedCount = 0;
            foreach (WeddingRemoteParticipant participant in _participantActors.Values.Concat(_audienceActors.Values))
            {
                if (ApplyRelationshipRecordRemove(participant, packet.RelationshipType, packet.CharacterId, packet.ItemSerial))
                {
                    RemoveRelationshipRecordDispatchKeysForOwner(packet.RelationshipType, participant.CharacterId);
                    affectedCount++;
                }
            }

            if (affectedCount == 0)
            {
                errorMessage = packet.CharacterId.HasValue
                    ? $"No wedding remote actor matched {packet.RelationshipType} remove character {packet.CharacterId.Value}."
                    : packet.ItemSerial.HasValue
                        ? $"No wedding remote actor matched {packet.RelationshipType} remove serial {packet.ItemSerial.Value}."
                        : $"No wedding remote actor matched {packet.RelationshipType} remove request.";
                return false;
            }

            return true;
        }

        private void ApplyRelationshipRecordAdd(
            WeddingRemoteParticipant participant,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord,
            RemoteRelationshipRecordDispatchKey dispatchKey = default,
            long? pairLookupSerial = null)
        {
            if (participant == null)
            {
                return;
            }

            relationshipRecord = NormalizeWeddingRelationshipRecordForApply(
                relationshipType,
                relationshipRecord,
                dispatchKey,
                pairLookupSerial);

            RemoteUserAvatarModifiedPacket state = participant.AvatarModifiedState
                ?? CreateDefaultAvatarModifiedState(participant.CharacterId);
            state = ApplyRelationshipRecordToAvatarModifiedState(state, relationshipType, relationshipRecord);
            StoreAvatarModifiedState(participant, state);
        }

        private RemoteUserRelationshipRecord NormalizeWeddingRelationshipRecordForApply(
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord,
            RemoteRelationshipRecordDispatchKey dispatchKey,
            long? pairLookupSerial)
        {
            if (relationshipType is not (RemoteRelationshipOverlayType.Couple or RemoteRelationshipOverlayType.Friendship)
                || !relationshipRecord.IsActive
                || !pairLookupSerial.HasValue)
            {
                return relationshipRecord;
            }

            int ownerCharacterId = relationshipRecord.CharacterId ?? 0;
            if (ownerCharacterId <= 0)
            {
                return relationshipRecord;
            }

            if (!TryFindWeddingRelationshipRecord(
                    relationshipType,
                    ownerCharacterId,
                    pairLookupSerial.Value,
                    out int matchedOwnerCharacterId,
                    out RemoteUserRelationshipRecord matchedRecord))
            {
                return relationshipRecord;
            }

            long? ownerItemSerial = relationshipRecord.ItemSerial;
            if (!ownerItemSerial.HasValue
                && dispatchKey.Kind == RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial)
            {
                ownerItemSerial = dispatchKey.Serial;
            }

            long? matchedItemSerial = matchedRecord.ItemSerial;
            if (!ownerItemSerial.HasValue || !matchedItemSerial.HasValue)
            {
                return relationshipRecord;
            }

            bool ownerIsLowerCharacterId = ownerCharacterId <= matchedOwnerCharacterId;
            return relationshipRecord with
            {
                ItemId = relationshipRecord.ItemId > 0 ? relationshipRecord.ItemId : matchedRecord.ItemId,
                ItemSerial = ownerIsLowerCharacterId ? ownerItemSerial.Value : matchedItemSerial.Value,
                PairItemSerial = ownerIsLowerCharacterId ? matchedItemSerial.Value : ownerItemSerial.Value,
                CharacterId = ownerIsLowerCharacterId ? ownerCharacterId : matchedOwnerCharacterId,
                PairCharacterId = ownerIsLowerCharacterId ? matchedOwnerCharacterId : ownerCharacterId
            };
        }

        private bool TryFindWeddingRelationshipRecord(
            RemoteRelationshipOverlayType relationshipType,
            int excludedOwnerCharacterId,
            long pairLookupSerial,
            out int ownerCharacterId,
            out RemoteUserRelationshipRecord relationshipRecord)
        {
            ownerCharacterId = 0;
            relationshipRecord = default;
            foreach (WeddingRemoteParticipant participant in _participantActors.Values.Concat(_audienceActors.Values))
            {
                if (participant == null
                    || participant.CharacterId == excludedOwnerCharacterId
                    || !participant.AvatarModifiedState.HasValue)
                {
                    continue;
                }

                RemoteUserRelationshipRecord candidate = GetRelationshipRecord(
                    participant.AvatarModifiedState.Value,
                    relationshipType);
                if (!candidate.IsActive)
                {
                    continue;
                }

                if (!candidate.PairItemSerial.HasValue || candidate.PairItemSerial.Value != pairLookupSerial)
                {
                    continue;
                }

                ownerCharacterId = participant.CharacterId;
                relationshipRecord = candidate;
                return true;
            }

            if (!TryResolveRelationshipRecordOwnerFromDispatchKey(
                    relationshipType,
                    new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                        pairLookupSerial,
                        CharacterId: null),
                    out ownerCharacterId)
                || ownerCharacterId == excludedOwnerCharacterId
                || !TryResolveParticipantForTemporaryStats(ownerCharacterId, out WeddingRemoteParticipant mappedParticipant, out _)
                || !mappedParticipant.AvatarModifiedState.HasValue)
            {
                ownerCharacterId = 0;
                return false;
            }

            relationshipRecord = GetRelationshipRecord(mappedParticipant.AvatarModifiedState.Value, relationshipType);
            return relationshipRecord.IsActive;
        }

        private static bool ApplyRelationshipRecordRemove(
            WeddingRemoteParticipant participant,
            RemoteRelationshipOverlayType relationshipType,
            int? characterId,
            long? itemSerial)
        {
            if (participant == null || !participant.AvatarModifiedState.HasValue)
            {
                return false;
            }

            RemoteUserRelationshipRecord currentRecord = GetRelationshipRecord(
                participant.AvatarModifiedState.Value,
                relationshipType);
            if (!currentRecord.IsActive)
            {
                return false;
            }

            if (!ShouldClearRelationshipRecord(characterId, itemSerial, participant, currentRecord))
            {
                return false;
            }

            RemoteUserAvatarModifiedPacket updatedState = ApplyRelationshipRecordToAvatarModifiedState(
                participant.AvatarModifiedState.Value,
                relationshipType,
                currentRecord with { IsActive = false, ItemId = 0 });
            StoreAvatarModifiedState(participant, updatedState);
            return true;
        }

        private static bool ShouldClearRelationshipRecord(
            RemoteUserRelationshipRecordRemovePacket removePacket,
            WeddingRemoteParticipant participant,
            out RemoteUserRelationshipRecord currentRecord)
        {
            currentRecord = GetRelationshipRecord(
                participant.AvatarModifiedState.Value,
                removePacket.RelationshipType);
            return ShouldClearRelationshipRecord(
                removePacket.CharacterId,
                removePacket.ItemSerial,
                participant,
                currentRecord);
        }

        private static bool ShouldClearRelationshipRecord(
            int? characterId,
            long? itemSerial,
            WeddingRemoteParticipant participant,
            RemoteUserRelationshipRecord currentRecord)
        {
            if (!currentRecord.IsActive || participant == null)
            {
                return false;
            }

            if (characterId.HasValue && characterId.Value > 0)
            {
                if (characterId.Value == participant.CharacterId)
                {
                    return true;
                }

                return currentRecord.PairCharacterId.HasValue
                    && currentRecord.PairCharacterId.Value == characterId.Value;
            }

            if (itemSerial.HasValue)
            {
                long serial = itemSerial.Value;
                return (currentRecord.ItemSerial.HasValue && currentRecord.ItemSerial.Value == serial)
                    || (currentRecord.PairItemSerial.HasValue && currentRecord.PairItemSerial.Value == serial);
            }

            return true;
        }

        private static RemoteUserAvatarModifiedPacket CreateDefaultAvatarModifiedState(int characterId)
        {
            return new RemoteUserAvatarModifiedPacket(
                CharacterId: characterId,
                AvatarLook: null,
                Speed: null,
                CarryItemEffect: null,
                CoupleRecord: default,
                FriendshipRecord: default,
                MarriageRecord: default,
                NewYearCardRecord: default,
                CompletedSetItemId: 0);
        }

        private static RemoteUserAvatarModifiedPacket ApplyRelationshipRecordToAvatarModifiedState(
            RemoteUserAvatarModifiedPacket state,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord)
        {
            return relationshipType switch
            {
                RemoteRelationshipOverlayType.Couple => state with { CoupleRecord = relationshipRecord },
                RemoteRelationshipOverlayType.Friendship => state with { FriendshipRecord = relationshipRecord },
                RemoteRelationshipOverlayType.Marriage => state with { MarriageRecord = relationshipRecord },
                RemoteRelationshipOverlayType.NewYearCard => state with { NewYearCardRecord = relationshipRecord },
                _ => state
            };
        }

        private static RemoteUserRelationshipRecord GetRelationshipRecord(
            RemoteUserAvatarModifiedPacket state,
            RemoteRelationshipOverlayType relationshipType)
        {
            return relationshipType switch
            {
                RemoteRelationshipOverlayType.Couple => state.CoupleRecord,
                RemoteRelationshipOverlayType.Friendship => state.FriendshipRecord,
                RemoteRelationshipOverlayType.Marriage => state.MarriageRecord,
                RemoteRelationshipOverlayType.NewYearCard => state.NewYearCardRecord,
                _ => default
            };
        }

        private void RegisterRelationshipRecordDispatchKeys(
            RemoteRelationshipOverlayType relationshipType,
            RemoteRelationshipRecordDispatchKey primaryDispatchKey,
            RemoteUserRelationshipRecord relationshipRecord,
            int ownerCharacterId)
        {
            if (ownerCharacterId <= 0)
            {
                return;
            }

            RemoveRelationshipRecordDispatchKeysForOwner(relationshipType, ownerCharacterId);
            RegisterRelationshipRecordDispatchKey(relationshipType, primaryDispatchKey, ownerCharacterId);
            if (relationshipType is RemoteRelationshipOverlayType.Couple or RemoteRelationshipOverlayType.Friendship)
            {
                RegisterRelationshipRecordDispatchKey(
                    relationshipType,
                    new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                        relationshipRecord.ItemSerial,
                        CharacterId: null),
                    ownerCharacterId);
                RegisterRelationshipRecordDispatchKey(
                    relationshipType,
                    new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                        relationshipRecord.PairItemSerial,
                        CharacterId: null),
                    ownerCharacterId);
                return;
            }

            if (relationshipType == RemoteRelationshipOverlayType.NewYearCard)
            {
                RegisterRelationshipRecordDispatchKey(
                    relationshipType,
                    new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.NewYearCardSerial,
                        relationshipRecord.ItemSerial,
                        CharacterId: null),
                    ownerCharacterId);
                return;
            }

            if (relationshipType == RemoteRelationshipOverlayType.Marriage)
            {
                RegisterRelationshipRecordDispatchKey(
                    relationshipType,
                    new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.CharacterId,
                        Serial: null,
                        ownerCharacterId),
                    ownerCharacterId);
            }
        }

        private void RegisterRelationshipRecordDispatchKey(
            RemoteRelationshipOverlayType relationshipType,
            RemoteRelationshipRecordDispatchKey dispatchKey,
            int ownerCharacterId)
        {
            if (!dispatchKey.HasValue || ownerCharacterId <= 0)
            {
                return;
            }

            GetRelationshipRecordDispatchOwnerTable(relationshipType)[dispatchKey] = ownerCharacterId;
        }

        private bool TryResolveRelationshipRecordOwnerFromDispatchKey(
            RemoteRelationshipOverlayType relationshipType,
            RemoteRelationshipRecordDispatchKey dispatchKey,
            out int ownerCharacterId)
        {
            ownerCharacterId = 0;
            if (!dispatchKey.HasValue)
            {
                return false;
            }

            return GetRelationshipRecordDispatchOwnerTable(relationshipType).TryGetValue(dispatchKey, out ownerCharacterId)
                && ownerCharacterId > 0;
        }

        private Dictionary<RemoteRelationshipRecordDispatchKey, int> GetRelationshipRecordDispatchOwnerTable(
            RemoteRelationshipOverlayType relationshipType)
        {
            if (!_relationshipRecordOwnerByDispatchKey.TryGetValue(relationshipType, out Dictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable))
            {
                dispatchTable = new Dictionary<RemoteRelationshipRecordDispatchKey, int>();
                _relationshipRecordOwnerByDispatchKey[relationshipType] = dispatchTable;
            }

            return dispatchTable;
        }

        private void RemoveRelationshipRecordDispatchKeysForOwner(
            RemoteRelationshipOverlayType relationshipType,
            int ownerCharacterId)
        {
            if (ownerCharacterId <= 0
                || !_relationshipRecordOwnerByDispatchKey.TryGetValue(relationshipType, out Dictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable))
            {
                return;
            }

            foreach (RemoteRelationshipRecordDispatchKey dispatchKey in dispatchTable
                .Where(entry => entry.Value == ownerCharacterId)
                .Select(entry => entry.Key)
                .ToArray())
            {
                dispatchTable.Remove(dispatchKey);
            }
        }

        private void RemoveRelationshipRecordDispatchKeysForOwnerAcrossTypes(int ownerCharacterId)
        {
            if (ownerCharacterId <= 0)
            {
                return;
            }

            foreach (RemoteRelationshipOverlayType relationshipType in _relationshipRecordOwnerByDispatchKey.Keys.ToArray())
            {
                RemoveRelationshipRecordDispatchKeysForOwner(relationshipType, ownerCharacterId);
            }
        }

        private void ClearRelationshipRecordDispatchTables()
        {
            foreach (Dictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable in _relationshipRecordOwnerByDispatchKey.Values)
            {
                dispatchTable.Clear();
            }
        }

        private static void ApplyParticipantTemporaryStatPresentation(WeddingRemoteParticipant participant)
        {
            if (participant == null)
            {
                return;
            }

            participant.ActionName = ResolveVisibleParticipantActionName(participant, participant.BaseActionName);
        }

        private static void SetParticipantTemporaryStats(
            WeddingRemoteParticipant participant,
            RemoteUserTemporaryStatSnapshot temporaryStats,
            ushort delay = 0)
        {
            if (participant == null)
            {
                return;
            }

            WeddingParticipantNameTagSignature previousNameTagSignature = CreateNameTagSignature(participant);
            participant.TemporaryStats = temporaryStats;
            participant.TemporaryStatDelay = delay;
            participant.TemporaryStatRevision++;
            ApplyParticipantTemporaryStatPresentation(participant);
            if (participant.NameTagRevision == 0
                || !previousNameTagSignature.Equals(CreateNameTagSignature(participant)))
            {
                RefreshParticipantNameTag(participant);
            }
        }

        private static void ApplyParticipantTemporaryStatReset(
            WeddingRemoteParticipant participant,
            IReadOnlyList<int> resetMaskWords)
        {
            if (participant == null)
            {
                return;
            }

            int[] currentMaskWords = participant.TemporaryStats.MaskWords ?? Array.Empty<int>();
            int resetWordCount = resetMaskWords?.Count ?? 0;
            int maskWordCount = Math.Max(currentMaskWords.Length, resetWordCount);
            if (maskWordCount == 0)
            {
                participant.TemporaryStats = default;
                participant.TemporaryStatDelay = 0;
                ApplyParticipantTemporaryStatPresentation(participant);
                participant.TemporaryStatRevision++;
                return;
            }

            int[] remainingMaskWords = new int[maskWordCount];
            for (int i = 0; i < maskWordCount; i++)
            {
                int currentWord = i < currentMaskWords.Length ? currentMaskWords[i] : 0;
                int resetWord = i < resetWordCount ? resetMaskWords[i] : 0;
                int remainingWord = currentWord & ~resetWord;
                remainingMaskWords[i] = remainingWord;
            }

            SetParticipantTemporaryStats(
                participant,
                RemoteUserPacketCodec.ApplyResetMask(participant.TemporaryStats, remainingMaskWords),
                delay: 0);
        }

        private static void ApplyParticipantGuildNameChanged(WeddingRemoteParticipant participant, string guildName)
        {
            if (participant?.Build == null)
            {
                return;
            }

            participant.Build.GuildName = string.IsNullOrWhiteSpace(guildName) ? string.Empty : guildName.Trim();
            participant.Build.HasAuthoritativeProfileGuild = true;
            participant.ProfileMetadataRevision++;
            RefreshParticipantNameTag(participant);
        }

        private static void ApplyParticipantProfileMetadata(WeddingRemoteParticipant participant, RemoteUserProfilePacket packet)
        {
            CharacterBuild build = participant?.Build;
            if (build == null)
            {
                return;
            }

            bool metadataUpdated = false;
            if (packet.Level.HasValue && packet.Level.Value > 0)
            {
                build.Level = Math.Max(1, packet.Level.Value);
                build.HasAuthoritativeProfileLevel = true;
                metadataUpdated = true;
            }

            if (packet.JobId.HasValue && packet.JobId.Value >= 0)
            {
                build.Job = packet.JobId.Value;
                build.JobName = SkillDataLoader.GetJobName(packet.JobId.Value);
                build.HasAuthoritativeProfileJob = true;
                metadataUpdated = true;
            }

            if (packet.GuildName != null)
            {
                build.GuildName = string.IsNullOrWhiteSpace(packet.GuildName) ? string.Empty : packet.GuildName.Trim();
                build.HasAuthoritativeProfileGuild = true;
                metadataUpdated = true;
                RefreshParticipantNameTag(participant);
            }

            if (packet.AllianceName != null)
            {
                build.AllianceName = string.IsNullOrWhiteSpace(packet.AllianceName) ? string.Empty : packet.AllianceName.Trim();
                build.HasAuthoritativeProfileAlliance = true;
                metadataUpdated = true;
            }

            if (packet.Fame.HasValue)
            {
                build.Fame = packet.Fame.Value;
                build.HasAuthoritativeProfileFame = true;
                metadataUpdated = true;
            }

            if (packet.WorldRank.HasValue)
            {
                build.WorldRank = Math.Max(0, packet.WorldRank.Value);
                build.HasAuthoritativeProfileWorldRank = true;
                metadataUpdated = true;
            }

            if (packet.JobRank.HasValue)
            {
                build.JobRank = Math.Max(0, packet.JobRank.Value);
                build.HasAuthoritativeProfileJobRank = true;
                metadataUpdated = true;
            }

            if (packet.HasRide.HasValue)
            {
                build.HasMonsterRiding = packet.HasRide.Value;
                build.HasAuthoritativeProfileRide = true;
                metadataUpdated = true;
            }

            if (packet.HasPendantSlot.HasValue)
            {
                build.HasPendantSlotExtension = packet.HasPendantSlot.Value;
                build.HasAuthoritativeProfilePendantSlot = true;
                metadataUpdated = true;
            }

            if (packet.HasPocketSlot.HasValue)
            {
                build.HasPocketSlot = packet.HasPocketSlot.Value;
                build.HasAuthoritativeProfilePocketSlot = true;
                metadataUpdated = true;
            }

            bool hasTraitUpdate = false;
            if (packet.TraitCharisma.HasValue)
            {
                build.TraitCharisma = Math.Max(0, packet.TraitCharisma.Value);
                hasTraitUpdate = true;
                metadataUpdated = true;
            }

            if (packet.TraitInsight.HasValue)
            {
                build.TraitInsight = Math.Max(0, packet.TraitInsight.Value);
                hasTraitUpdate = true;
                metadataUpdated = true;
            }

            if (packet.TraitWill.HasValue)
            {
                build.TraitWill = Math.Max(0, packet.TraitWill.Value);
                hasTraitUpdate = true;
                metadataUpdated = true;
            }

            if (packet.TraitCraft.HasValue)
            {
                build.TraitCraft = Math.Max(0, packet.TraitCraft.Value);
                hasTraitUpdate = true;
                metadataUpdated = true;
            }

            if (packet.TraitSense.HasValue)
            {
                build.TraitSense = Math.Max(0, packet.TraitSense.Value);
                hasTraitUpdate = true;
                metadataUpdated = true;
            }

            if (packet.TraitCharm.HasValue)
            {
                build.TraitCharm = Math.Max(0, packet.TraitCharm.Value);
                hasTraitUpdate = true;
                metadataUpdated = true;
            }

            if (hasTraitUpdate)
            {
                build.HasAuthoritativeProfileTraits = true;
            }

            if (packet.HasMedal.HasValue)
            {
                build.HasAuthoritativeProfileMedal = true;
                metadataUpdated = true;
            }

            if (packet.HasCollection.HasValue)
            {
                build.HasAuthoritativeProfileCollection = true;
                metadataUpdated = true;
            }

            if (metadataUpdated)
            {
                participant.ProfileMetadataRevision++;
            }
        }

        private static void ApplyParticipantGuildMarkChanged(
            WeddingRemoteParticipant participant,
            int markBackgroundId,
            int markBackgroundColor,
            int markId,
            int markColor)
        {
            if (participant?.Build == null)
            {
                return;
            }

            participant.Build.GuildMarkBackgroundId = markBackgroundId > 0 ? markBackgroundId : null;
            participant.Build.GuildMarkBackgroundColor = markBackgroundColor;
            participant.Build.GuildMarkId = markId > 0 ? markId : null;
            participant.Build.GuildMarkColor = markColor;
            participant.GuildMarkRevision++;
            RefreshParticipantNameTag(participant);
        }

        private static void RefreshParticipantNameTag(WeddingRemoteParticipant participant)
        {
            if (participant == null)
            {
                return;
            }

            participant.NameTagRevision++;
        }

        private static WeddingParticipantNameTagSignature CreateNameTagSignature(WeddingRemoteParticipant participant)
        {
            CharacterBuild build = participant?.Build;
            RemoteUserAvatarModifiedPacket? avatarModifiedState = participant?.AvatarModifiedState;
            RemoteUserRelationshipRecord coupleRecord = avatarModifiedState?.CoupleRecord ?? default;
            RemoteUserRelationshipRecord friendshipRecord = avatarModifiedState?.FriendshipRecord ?? default;
            RemoteUserRelationshipRecord marriageRecord = avatarModifiedState?.MarriageRecord ?? default;
            RemoteUserRelationshipRecord newYearCardRecord = avatarModifiedState?.NewYearCardRecord ?? default;
            return new WeddingParticipantNameTagSignature(
                NormalizeNameTagText(participant?.Name),
                NormalizeNameTagText(build?.GuildName),
                build?.GuildMarkBackgroundId,
                build?.GuildMarkBackgroundColor,
                build?.GuildMarkId,
                build?.GuildMarkColor,
                ResolveRelationshipNameTagItemId(coupleRecord),
                ResolveRelationshipNameTagItemId(friendshipRecord),
                ResolveRelationshipNameTagItemId(marriageRecord),
                ResolveRelationshipNameTagItemId(newYearCardRecord),
                ShouldDrawParticipantLikeClient(participant));
        }

        private static int? ResolveRelationshipNameTagItemId(RemoteUserRelationshipRecord relationshipRecord)
        {
            return relationshipRecord.IsActive && relationshipRecord.ItemId > 0
                ? relationshipRecord.ItemId
                : null;
        }

        private static string NormalizeNameTagText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private readonly record struct WeddingParticipantNameTagSignature(
            string Name,
            string GuildName,
            int? GuildMarkBackgroundId,
            int? GuildMarkBackgroundColor,
            int? GuildMarkId,
            int? GuildMarkColor,
            int? CoupleItemId,
            int? FriendshipItemId,
            int? MarriageItemId,
            int? NewYearCardItemId,
            bool IsVisibleLikeClient);

        private bool TryResolveParticipantForTemporaryStats(int characterId, out WeddingRemoteParticipant participant, out string errorMessage)
        {
            participant = null;
            errorMessage = null;
            if (_participantActors.TryGetValue(characterId, out participant) || TryGetAudienceActorById(characterId, out participant))
            {
                return true;
            }

            errorMessage = $"Wedding remote actor id {characterId} does not exist.";
            return false;
        }

        private void QueuePendingRemoteParticipantOperation(
            int characterId,
            PendingWeddingRemoteParticipantOperation operation)
        {
            if (characterId <= 0)
            {
                return;
            }

            if (!_pendingRemoteParticipantOperationsByCharacterId.TryGetValue(characterId, out List<PendingWeddingRemoteParticipantOperation> operations))
            {
                operations = new List<PendingWeddingRemoteParticipantOperation>();
                _pendingRemoteParticipantOperationsByCharacterId[characterId] = operations;
            }

            operations.Add(operation);
        }

        private void TryApplyPendingRemoteParticipantOperations(WeddingRemoteParticipant participant)
        {
            if (participant == null || participant.CharacterId <= 0)
            {
                return;
            }

            if (!_pendingRemoteParticipantOperationsByCharacterId.TryGetValue(participant.CharacterId, out List<PendingWeddingRemoteParticipantOperation> operations)
                || operations == null
                || operations.Count == 0)
            {
                return;
            }

            foreach (PendingWeddingRemoteParticipantOperation operation in operations)
            {
                switch (operation.Type)
                {
                    case PendingWeddingRemoteParticipantOperationType.TemporaryStatSet:
                        SetParticipantTemporaryStats(participant, operation.TemporaryStats, operation.TemporaryStatDelay);
                        break;
                    case PendingWeddingRemoteParticipantOperationType.TemporaryStatReset:
                        ApplyParticipantTemporaryStatReset(participant, operation.ResetMaskWords ?? Array.Empty<int>());
                        break;
                    case PendingWeddingRemoteParticipantOperationType.GuildNameChanged:
                        ApplyParticipantGuildNameChanged(participant, operation.GuildName);
                        break;
                    case PendingWeddingRemoteParticipantOperationType.Profile:
                        ApplyParticipantProfileMetadata(participant, operation.ProfilePacket);
                        break;
                    case PendingWeddingRemoteParticipantOperationType.GuildMarkChanged:
                        ApplyParticipantGuildMarkChanged(
                            participant,
                            operation.MarkBackgroundId,
                            operation.MarkBackgroundColor,
                            operation.MarkId,
                            operation.MarkColor);
                        break;
                    case PendingWeddingRemoteParticipantOperationType.RelationshipRecordAdd:
                        ApplyRelationshipRecordAdd(
                            participant,
                            operation.RelationshipType,
                            operation.RelationshipRecord,
                            operation.RelationshipDispatchKey,
                            operation.RelationshipPairLookupSerial);
                        break;
                    case PendingWeddingRemoteParticipantOperationType.RelationshipRecordRemove:
                        if (ApplyRelationshipRecordRemove(
                            participant,
                            operation.RelationshipType,
                            operation.RelationshipRemoveCharacterId,
                            operation.RelationshipRemoveItemSerial))
                        {
                            RemoveRelationshipRecordDispatchKeysForOwner(operation.RelationshipType, participant.CharacterId);
                        }
                        break;
                }
            }

            _pendingRemoteParticipantOperationsByCharacterId.Remove(participant.CharacterId);
        }

        private CharacterBuild CreateRemoteBuildFromAvatarLook(string actorName, LoginAvatarLook avatarLook, out string errorMessage)
        {
            errorMessage = null;
            if (avatarLook == null)
            {
                errorMessage = "Wedding AvatarLook payload is missing.";
                return null;
            }

            CharacterBuild build = _remoteBuildFactory?.Invoke(avatarLook, actorName);
            if (build == null)
            {
                errorMessage = "Wedding AvatarLook payload could not be converted into a character build.";
                return null;
            }

            if (!string.IsNullOrWhiteSpace(actorName))
            {
                build.Name = actorName.Trim();
            }

            build.HasAuthoritativeProfileLevel = false;
            build.HasAuthoritativeProfileJob = false;
            build.HasAuthoritativeProfileGuild = false;

            return build;
        }

        internal static void ApplyRemoteSpawnMetadata(CharacterBuild build, WeddingRemoteSpawnPacket spawn)
        {
            if (build == null)
            {
                return;
            }

            if (spawn.Level.HasValue && spawn.Level.Value > 0)
            {
                build.Level = spawn.Level.Value;
                build.HasAuthoritativeProfileLevel = true;
            }

            if (spawn.JobId.HasValue && spawn.JobId.Value >= 0)
            {
                build.Job = spawn.JobId.Value;
                build.JobName = SkillDataLoader.GetJobName(spawn.JobId.Value);
                build.HasAuthoritativeProfileJob = true;
            }

            if (spawn.GuildName != null)
            {
                build.GuildName = string.IsNullOrWhiteSpace(spawn.GuildName) ? string.Empty : spawn.GuildName.Trim();
                build.HasAuthoritativeProfileGuild = true;
            }
        }

        private static void CopyPersistentBuildMetadata(CharacterBuild destination, CharacterBuild source)
        {
            if (destination == null || source == null)
            {
                return;
            }

            if (source.HasAuthoritativeProfileLevel)
            {
                destination.Level = Math.Max(1, source.Level);
                destination.HasAuthoritativeProfileLevel = true;
            }

            if (source.HasAuthoritativeProfileJob)
            {
                destination.Job = Math.Max(0, source.Job);
                destination.HasAuthoritativeProfileJob = true;
            }

            if (!string.IsNullOrWhiteSpace(source.JobName)
                && (source.HasAuthoritativeProfileJob || string.IsNullOrWhiteSpace(destination.JobName)))
            {
                destination.JobName = source.JobName;
            }

            if (source.HasAuthoritativeProfileGuild)
            {
                destination.GuildName = source.GuildName ?? string.Empty;
                destination.HasAuthoritativeProfileGuild = true;
            }

            if (source.HasAuthoritativeProfileAlliance)
            {
                destination.AllianceName = source.AllianceName ?? string.Empty;
                destination.HasAuthoritativeProfileAlliance = true;
            }

            if (source.HasAuthoritativeProfileFame)
            {
                destination.Fame = source.Fame;
                destination.HasAuthoritativeProfileFame = true;
            }

            if (source.HasAuthoritativeProfileWorldRank)
            {
                destination.WorldRank = Math.Max(0, source.WorldRank);
                destination.HasAuthoritativeProfileWorldRank = true;
            }

            if (source.HasAuthoritativeProfileJobRank)
            {
                destination.JobRank = Math.Max(0, source.JobRank);
                destination.HasAuthoritativeProfileJobRank = true;
            }

            if (source.HasAuthoritativeProfileRide)
            {
                destination.HasMonsterRiding = source.HasMonsterRiding;
                destination.HasAuthoritativeProfileRide = true;
            }

            if (source.HasAuthoritativeProfilePendantSlot)
            {
                destination.HasPendantSlotExtension = source.HasPendantSlotExtension;
                destination.HasAuthoritativeProfilePendantSlot = true;
            }

            if (source.HasAuthoritativeProfilePocketSlot)
            {
                destination.HasPocketSlot = source.HasPocketSlot;
                destination.HasAuthoritativeProfilePocketSlot = true;
            }

            if (source.HasAuthoritativeProfileTraits)
            {
                destination.TraitCharisma = Math.Max(0, source.TraitCharisma);
                destination.TraitInsight = Math.Max(0, source.TraitInsight);
                destination.TraitWill = Math.Max(0, source.TraitWill);
                destination.TraitCraft = Math.Max(0, source.TraitCraft);
                destination.TraitSense = Math.Max(0, source.TraitSense);
                destination.TraitCharm = Math.Max(0, source.TraitCharm);
                destination.HasAuthoritativeProfileTraits = true;
            }

            if (source.HasAuthoritativeProfileMedal)
            {
                destination.HasAuthoritativeProfileMedal = true;
            }

            if (source.HasAuthoritativeProfileCollection)
            {
                destination.HasAuthoritativeProfileCollection = true;
            }

            destination.GuildMarkBackgroundId = source.GuildMarkBackgroundId;
            destination.GuildMarkBackgroundColor = source.GuildMarkBackgroundColor;
            destination.GuildMarkId = source.GuildMarkId;
            destination.GuildMarkColor = source.GuildMarkColor;
        }

        private static bool TryDecodeRemoteSpawnPacket(byte[] payload, out WeddingRemoteSpawnPacket packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseEnterField(payload, out RemoteUserEnterFieldPacket spawn, out errorMessage))
            {
                return false;
            }

            packet = new WeddingRemoteSpawnPacket(
                spawn.CharacterId,
                string.IsNullOrWhiteSpace(spawn.Name) ? $"WeddingGuest{spawn.CharacterId}" : spawn.Name.Trim(),
                spawn.AvatarLook,
                new Vector2(spawn.X, spawn.Y),
                EncodeMoveAction(spawn.FacingRight, spawn.ActionName, spawn.PortableChairItemId ?? 0),
                spawn.PortableChairItemId ?? 0,
                spawn.TemporaryStats,
                spawn.Level,
                spawn.GuildName,
                spawn.JobId);
            return true;
        }

        private static bool TryDecodeRemoteCharacterIdPacket(byte[] payload, int packetType, out int characterId, out string errorMessage)
        {
            characterId = 0;
            errorMessage = null;
            if (!TryCreatePacketReader(payload, packetType, out PacketReader reader, out errorMessage))
            {
                return false;
            }

            try
            {
                characterId = reader.ReadInt();
                return true;
            }
            catch (EndOfStreamException)
            {
                errorMessage = "Wedding remote actor packet ended before the character id was fully read.";
                return false;
            }
        }

        private static bool TryDecodeRemoteMovePacket(byte[] payload, int currentTimeMs, out WeddingRemoteMovePacket packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseMove(payload, currentTimeMs, out RemoteUserMovePacket move, out errorMessage))
            {
                return false;
            }

            packet = new WeddingRemoteMovePacket(
                move.CharacterId,
                new Vector2(move.Snapshot.PassivePosition.X, move.Snapshot.PassivePosition.Y),
                move.MoveAction,
                move.Snapshot);
            return true;
        }

        private static bool TryDecodeRemoteChairPacket(byte[] payload, out WeddingRemoteChairPacket packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParsePortableChair(payload, out RemoteUserPortableChairPacket chair, out errorMessage))
            {
                return false;
            }

            packet = new WeddingRemoteChairPacket(chair.CharacterId, chair.ChairItemId ?? 0, chair.PairCharacterId);
            return true;
        }

        private bool ApplyParticipantPortableChairState(int characterId, int portableChairItemId, int? pairCharacterId)
        {
            if (_participantActors.TryGetValue(characterId, out WeddingRemoteParticipant participant))
            {
                participant.PortableChairItemId = portableChairItemId > 0 ? portableChairItemId : null;
                participant.PortableChairPairCharacterId = portableChairItemId > 0 ? pairCharacterId : null;
                return true;
            }

            if (TryGetAudienceActorById(characterId, out WeddingRemoteParticipant audienceParticipant))
            {
                audienceParticipant.PortableChairItemId = portableChairItemId > 0 ? portableChairItemId : null;
                audienceParticipant.PortableChairPairCharacterId = portableChairItemId > 0 ? pairCharacterId : null;
                return true;
            }

            return false;
        }

        private static bool TryDecodeRemoteAvatarModifiedPacket(byte[] payload, out RemoteUserAvatarModifiedPacket packet, out string errorMessage)
        {
            return RemoteUserPacketCodec.TryParseAvatarModified(payload, out packet, out errorMessage);
        }

        private static void ApplyAvatarModifiedStateToBuild(CharacterBuild build, RemoteUserAvatarModifiedPacket packet)
        {
            if (build == null || !packet.Speed.HasValue)
            {
                return;
            }

            build.Speed = packet.Speed.Value;
        }

        private static int? ResolveParticipantCarryItemEffectCount(WeddingRemoteParticipant participant)
        {
            if (participant?.AvatarModifiedState is not RemoteUserAvatarModifiedPacket avatarModifiedState
                || !avatarModifiedState.CarryItemEffect.HasValue
                || avatarModifiedState.CarryItemEffect.Value <= 0)
            {
                return null;
            }

            return avatarModifiedState.CarryItemEffect.Value;
        }

        private static int ResolveParticipantCompletedSetItemId(WeddingRemoteParticipant participant)
        {
            return participant?.AvatarModifiedState is RemoteUserAvatarModifiedPacket avatarModifiedState
                && avatarModifiedState.CompletedSetItemId > 0
                    ? avatarModifiedState.CompletedSetItemId
                    : 0;
        }

        private static void StoreAvatarModifiedState(WeddingRemoteParticipant participant, RemoteUserAvatarModifiedPacket packet)
        {
            if (participant == null)
            {
                return;
            }

            WeddingParticipantNameTagSignature previousNameTagSignature = CreateNameTagSignature(participant);
            participant.AvatarModifiedState = packet;
            participant.AvatarModifiedRevision++;
            if (participant.NameTagRevision == 0
                || !previousNameTagSignature.Equals(CreateNameTagSignature(participant)))
            {
                RefreshParticipantNameTag(participant);
            }
        }

        internal static bool TryApplyParticipantGenericItemEffect(
            WeddingRemoteParticipant participant,
            int? itemId,
            int currentTime,
            out string message,
            CharacterLoader characterLoader = null)
        {
            message = null;
            if (participant == null)
            {
                message = "Wedding participant is required.";
                return false;
            }

            if (!itemId.HasValue || itemId.Value <= 0)
            {
                participant.PacketOwnedItemEffect = null;
                participant.PacketOwnedItemEffectItemId = null;
                participant.PacketOwnedItemEffectRevision++;
                message = $"Wedding remote actor {participant.CharacterId} generic item effect cleared.";
                return true;
            }

            ItemEffectAnimationSet effect = characterLoader?.LoadItemEffectAnimationSet(itemId.Value);
            if (characterLoader != null && effect == null)
            {
                message = $"Wedding remote actor {participant.CharacterId} item effect {itemId.Value} could not be loaded from Effect/ItemEff.img.";
                return false;
            }

            participant.PacketOwnedItemEffect = new WeddingPacketOwnedItemEffectState
            {
                ItemId = itemId.Value,
                Effect = effect,
                StartTime = currentTime
            };
            participant.PacketOwnedItemEffectItemId = itemId.Value;
            participant.PacketOwnedItemEffectRevision++;
            message = characterLoader == null
                ? $"Wedding remote actor {participant.CharacterId} generic item effect {itemId.Value} tracked for shared remote rendering."
                : $"Wedding remote actor {participant.CharacterId} generic item effect {itemId.Value} applied.";
            return true;
        }

        private static bool TryCreatePacketReader(byte[] payload, int packetType, out PacketReader reader, out string errorMessage)
        {
            reader = null;
            errorMessage = null;
            if (payload == null || payload.Length == 0)
            {
                errorMessage = $"Wedding packet {packetType} payload is missing.";
                return false;
            }

            reader = new PacketReader(payload);
            return true;
        }

        private static string ResolveRemoteActionName(byte moveAction, int portableChairItemId)
        {
            if (portableChairItemId > 0)
            {
                return CharacterPart.GetActionString(CharacterAction.Sit);
            }

            return (moveAction >> 1) switch
            {
                1 => CharacterPart.GetActionString(CharacterAction.Walk1),
                4 => CharacterPart.GetActionString(CharacterAction.Alert),
                5 => CharacterPart.GetActionString(CharacterAction.Jump),
                6 => CharacterPart.GetActionString(CharacterAction.Sit),
                17 => CharacterPart.GetActionString(CharacterAction.Ladder),
                18 => CharacterPart.GetActionString(CharacterAction.Rope),
                _ => CharacterPart.GetActionString(CharacterAction.Stand1)
            };
        }

        private static string ResolveVisibleParticipantActionName(WeddingRemoteParticipant participant, string actionName)
        {
            return RemoteUserActorPool.ResolveClientVisibleActionName(
                actionName,
                participant?.TemporaryStats.KnownState ?? default);
        }

        private static string ResolveRemoteActionName(Physics.MoveAction moveAction, int portableChairItemId)
        {
            if (portableChairItemId > 0)
            {
                return CharacterPart.GetActionString(CharacterAction.Sit);
            }

            return moveAction switch
            {
                Physics.MoveAction.Walk => CharacterPart.GetActionString(CharacterAction.Walk1),
                Physics.MoveAction.Jump or Physics.MoveAction.Fall => CharacterPart.GetActionString(CharacterAction.Jump),
                Physics.MoveAction.Hit => CharacterPart.GetActionString(CharacterAction.Alert),
                Physics.MoveAction.Ladder => CharacterPart.GetActionString(CharacterAction.Ladder),
                Physics.MoveAction.Rope => CharacterPart.GetActionString(CharacterAction.Rope),
                Physics.MoveAction.Swim => CharacterPart.GetActionString(CharacterAction.Swim),
                Physics.MoveAction.Fly => CharacterPart.GetActionString(CharacterAction.Fly),
                _ => CharacterPart.GetActionString(CharacterAction.Stand1)
            };
        }

        private static byte EncodeMoveAction(bool facingRight, string actionName, int portableChairItemId)
        {
            int actionCode = portableChairItemId > 0
                ? 6
                : NormalizeActionCode(actionName);
            return (byte)((actionCode << 1) | (facingRight ? 0 : 1));
        }

        private static int NormalizeActionCode(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return 0;
            }

            return actionName.Trim().ToLowerInvariant() switch
            {
                "walk" or "walk1" => 1,
                "alert" => 4,
                "jump" => 5,
                "sit" => 6,
                "ladder" => 17,
                "rope" => 18,
                _ => 0
            };
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                PacketTypeUserEnterField => "userenter (179)",
                PacketTypeUserLeaveField => "userleave (180)",
                PacketTypeUserMoveOrChairAlias => "usermove (210)",
                PacketTypeItemEffect => "itemeffect (215)",
                PacketTypeUserProfile => "profile (-1003)",
                PacketTypeSetActivePortableChairLegacy => "chair (222)",
                PacketTypeAvatarModified => "avatarmodified (223)",
                PacketTypeTemporaryStatSet => "tempset (225)",
                PacketTypeTemporaryStatReset => "tempreset (226)",
                PacketTypeGuildNameChanged => "guildnamechanged (228)",
                PacketTypeGuildMarkChanged => "guildmarkchanged (229)",
                PacketTypeCoupleRecordAdd => "couplerecordadd (-1101)",
                PacketTypeCoupleRecordRemove => "couplerecordremove (-1102)",
                PacketTypeFriendRecordAdd => "friendrecordadd (-1103)",
                PacketTypeFriendRecordRemove => "friendrecordremove (-1104)",
                PacketTypeMarriageRecordAdd => "marriagerecordadd (-1105)",
                PacketTypeMarriageRecordRemove => "marriagerecordremove (-1106)",
                PacketTypeNewYearCardRecordAdd => "newyearcardrecordadd (-1107)",
                PacketTypeNewYearCardRecordRemove => "newyearcardrecordremove (-1108)",
                _ => packetType.ToString(CultureInfo.InvariantCulture)
            };
        }

        #endregion


        #region Packet Handling (matching CField_Wedding)



        /// <summary>
        /// OnWeddingProgress - Packet 379
        /// Updates wedding step and shows dialog
        /// </summary>
        public void OnWeddingProgress(int step, int groomId, int brideId, int currentTimeMs)
        {
            if (!_isActive && !IsWeddingPhotoSceneOwnerActive)
            {
                return;
            }

            int npcId = ResolveCeremonyNpcId(_mapId);

            System.Diagnostics.Debug.WriteLine($"[WeddingField] OnWeddingProgress: step={step}, groom={groomId}, bride={brideId}");

            DismissCurrentDialog();

            _currentStep = step;
            _npcId = npcId;
            _groomId = groomId;
            _brideId = brideId;
            UpdateParticipantState();

            bool hasCeremonyNpc = npcId > 0;


            ApplyWeddingProgressLayerState(step, currentTimeMs, hasCeremonyNpc);
            SyncWeddingPhotoScenePresentationStage(PacketTypeWeddingProgress);


            if (hasCeremonyNpc)
            {
                ShowWeddingDialog(step, currentTimeMs);
            }

        }



        /// <summary>
        /// OnWeddingCeremonyEnd - Packet 380
        /// Triggers the bless effect (sparkles)
        /// </summary>
        public void OnWeddingCeremonyEnd(int currentTimeMs)
        {
            if (!_isActive && !IsWeddingPhotoSceneOwnerActive)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine("[WeddingField] OnWeddingCeremonyEnd - Starting bless effect");
            DismissCurrentDialog();
            SetCeremonyTextOverlay(active: false);

            SetBlessEffect(true, currentTimeMs);
            SyncWeddingPhotoScenePresentationStage(PacketTypeWeddingCeremonyEnd);
        }

        private void ApplyWeddingProgressLayerState(int step, int currentTimeMs, bool hasCeremonyNpc)
        {
            // Step 0 always resets into the declaration text layer.
            if (step == 0)
            {
                SetBlessEffect(false, currentTimeMs);
                SetCeremonyTextOverlay(true);
                SetCeremonyCardOverlay(false);
                SetCeremonyCelebration(active: false);
                if (_isActive)
                {
                    _requestBgmOverride?.Invoke(WeddingCeremonyClientText.ResolveOpeningBgmPath(_mapId));
                }

                return;
            }

            SetCeremonyTextOverlay(false);

            // CField_WeddingPhoto owns packet-driven scene progression but has no altar NPC dialog contract.
            if (IsWeddingPhotoSceneOwnerActive && !_isActive && !hasCeremonyNpc)
            {
                SetCeremonyCardOverlay(step >= WeddingPhotoCardRevealStep);
                SetCeremonyCelebration(step >= WeddingPhotoCelebrationStep);
                return;
            }

            bool pronounced = IsPronouncementOrBlessingStep(step);
            SetCeremonyCardOverlay(pronounced);
            SetCeremonyCelebration(pronounced);
        }


        public WeddingPacketResponse? RespondToCurrentDialog(bool accepted, int currentTimeMs)
        {
            if (_currentDialog == null)
            {
                return null;
            }


            WeddingPacketResponse? packetResponse = null;
            if (_currentDialog.Mode == WeddingDialogMode.YesNo)
            {
                if (accepted)
                {
                    packetResponse = new WeddingPacketResponse(
                        WeddingPacketOpcode.GuestBless,
                        _currentStep,
                        Accepted: true,
                        currentTimeMs);
                }
            }
            else if (accepted && ShouldSendParticipantAdvancePacket())
            {
                packetResponse = new WeddingPacketResponse(
                    WeddingPacketOpcode.AdvanceStep,
                    _currentStep,
                    Accepted: true,
                    currentTimeMs);
            }


            _lastPacketResponse = packetResponse;
            _currentDialog = null;
            if (_dialogQueue.Count > 0)
            {
                _currentDialog = _dialogQueue.Dequeue();
                _currentDialog.StartTime = currentTimeMs;
            }


            return packetResponse;

        }



        /// <summary>
        /// SetBlessEffect - Start/stop sparkle effect between couple
        /// Based on client's CField_Wedding::SetBlessEffect
        /// </summary>
        public void SetBlessEffect(bool active, int currentTimeMs)
        {
            if (active)
            {
                if (!TryGetBlessEffectWorldCenter().HasValue)
                {
                    _blessEffectActive = false;
                    _blessEffectAlpha = 0f;
                    _sparkles.Clear();
                    return;
                }

                _blessEffectActive = true;
                _blessEffectStartTime = currentTimeMs;
                _blessEffectAlpha = 1f;


                if (_blessFrames != null && _blessFrames.Count > 0)
                {
                    _sparkles.Clear();
                    return;
                }


                // Create sparkles
                _sparkles.Clear();
                for (int i = 0; i < 50; i++)
                {
                    _sparkles.Add(new WeddingSparkle
                    {
                        X = (float)(_random.NextDouble() - 0.5f) * 80f,
                        Y = (float)(_random.NextDouble() - 0.5f) * 40f,
                        Scale = 0.5f + (float)_random.NextDouble() * 0.5f,
                        Alpha = 0f,
                        Velocity = new Vector2((float)(_random.NextDouble() - 0.5f) * 12f, (float)(_random.NextDouble() - 0.25f) * 10f),
                        LifeTime = 2000 + _random.Next(3000),
                        SpawnDelay = i * 100
                    });
                }
            }
            else
            {
                _blessEffectActive = false;
                _blessEffectAlpha = 0f;
                _sparkles.Clear();
            }
        }


        private void LoadCeremonyOverlay(GraphicsDevice device)
        {
            if (device == null)
            {
                return;
            }


            try
            {
                WzImage uiWindowImage = global::HaCreator.Program.FindImage("UI", WeddingUiImageName);
                uiWindowImage?.ParseImage();
                WzCanvasProperty overlayCanvas = WzInfoTools.GetRealProperty(uiWindowImage?.GetFromPath(CeremonyTextOverlayPath)) as WzCanvasProperty;
                if (overlayCanvas == null)
                {
                    return;
                }


                using var bitmap = overlayCanvas.GetLinkedWzCanvasBitmap();
                if (bitmap == null)
                {
                    return;
                }


                _ceremonyTextOverlayTexture = bitmap.ToTexture2D(device);

                System.Drawing.PointF origin = overlayCanvas.GetCanvasOriginPosition();

                _ceremonyTextOverlayOrigin = new Point((int)origin.X, (int)origin.Y);



                WzCanvasProperty cardCanvas = WzInfoTools.GetRealProperty(uiWindowImage?.GetFromPath(CeremonyCardOverlayPath)) as WzCanvasProperty;
                using var cardBitmap = cardCanvas?.GetLinkedWzCanvasBitmap();
                if (cardBitmap != null)
                {
                    _ceremonyCardOverlayTexture = cardBitmap.ToTexture2D(device);
                    System.Drawing.PointF cardOrigin = cardCanvas.GetCanvasOriginPosition();
                    _ceremonyCardOverlayOrigin = new Point((int)cardOrigin.X, (int)cardOrigin.Y);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WeddingField] Failed to load ceremony overlay: {ex.Message}");
            }
        }


        private List<IDXObject> LoadBlessFrames(GraphicsDevice device)
        {
            if (device == null)
            {
                return null;
            }


            try
            {
                WzImage basicEffImage = global::HaCreator.Program.FindImage("Effect", BlessEffectImageName);
                basicEffImage?.ParseImage();
                WzImageProperty blessProperty = basicEffImage?.GetFromPath(BlessEffectPath) as WzImageProperty;
                if (blessProperty == null)
                {
                    return null;
                }


                var frames = new List<IDXObject>();
                int frameIndex = 0;
                while (true)
                {
                    WzImageProperty frameProperty = WzInfoTools.GetRealProperty(blessProperty[frameIndex.ToString()]);
                    if (frameProperty == null)
                    {
                        break;
                    }


                    WzCanvasProperty frameCanvas = frameProperty as WzCanvasProperty;
                    if (frameCanvas == null && frameProperty is WzUOLProperty frameUol)
                    {
                        frameCanvas = frameUol.LinkValue as WzCanvasProperty;
                    }


                    if (frameCanvas != null)
                    {
                        using var bitmap = frameCanvas.GetLinkedWzCanvasBitmap();
                        if (bitmap != null)
                        {
                            Texture2D texture = bitmap.ToTexture2D(device);
                            System.Drawing.PointF origin = frameCanvas.GetCanvasOriginPosition();
                            int delay = InfoTool.GetOptionalInt(frameCanvas["delay"], 100) ?? 100;
                            frames.Add(new DXObject(-(int)origin.X, -(int)origin.Y, texture, delay));
                        }
                    }


                    frameIndex++;

                }



                return frames.Count > 0 ? frames : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WeddingField] Failed to load bless effect frames: {ex.Message}");
                return null;
            }
        }


        private void LoadCeremonyCelebrationAssets(GraphicsDevice device)
        {
            if (device == null)
            {
                return;
            }


            try
            {
                WzImage mapHelperImage = global::HaCreator.Program.FindImage("Map", WeddingMapHelperImageName);
                mapHelperImage?.ParseImage();
                _ceremonyPetalConfig = LoadSceneFrames(
                    mapHelperImage?.GetFromPath(WeddingWeatherPath) as WzImageProperty,
                    _ceremonyPetalFrames,
                    device,
                    DefaultCeremonyPetalCount);
                _ceremonyHeartConfig = LoadSceneFrames(
                    mapHelperImage?.GetFromPath(WeddingHeartWeatherPath) as WzImageProperty,
                    _ceremonyHeartFrames,
                    device,
                    DefaultCeremonyHeartCount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WeddingField] Failed to load ceremony celebration assets: {ex.Message}");
            }
        }


        private static WeddingSceneAssetConfig LoadSceneFrames(
            WzImageProperty sourceProperty,
            List<WeddingSceneFrame> destination,
            GraphicsDevice device,
            int defaultCount)
        {
            destination.Clear();
            WeddingSceneAssetConfig config = new(defaultCount, RandomFrames: true);
            if (sourceProperty == null)
            {
                return config;
            }

            int configuredCount = InfoTool.GetOptionalInt(sourceProperty["count"], defaultCount) ?? defaultCount;
            bool randomFrames = (InfoTool.GetOptionalInt(sourceProperty["random"], 1) ?? 1) != 0;
            config = new WeddingSceneAssetConfig(Math.Max(1, configuredCount), randomFrames);


            int frameIndex = 0;
            while (true)
            {
                WzImageProperty frameProperty = WzInfoTools.GetRealProperty(sourceProperty[frameIndex.ToString()]);
                if (frameProperty == null)
                {
                    break;
                }


                if (frameProperty is not WzCanvasProperty frameCanvas)
                {
                    frameIndex++;
                    continue;
                }


                using var bitmap = frameCanvas.GetLinkedWzCanvasBitmap();
                if (bitmap != null)
                {
                    Texture2D texture = bitmap.ToTexture2D(device);
                    System.Drawing.PointF origin = frameCanvas.GetCanvasOriginPosition();
                    int delay = InfoTool.GetOptionalInt(frameCanvas["delay"], 100) ?? 100;
                    destination.Add(new WeddingSceneFrame(texture, new Vector2(origin.X, origin.Y), delay));
                }


                frameIndex++;

            }

            return config;

        }



        private void ShowWeddingDialog(int step, int currentTimeMs)
        {
            _currentDialog = CreateDialogForStep(step, currentTimeMs);
        }

        private void DismissCurrentDialog()
        {
            _currentDialog = null;
            _dialogQueue.Clear();
        }


        private WeddingDialog CreateDialogForStep(int step, int currentTimeMs)
        {
            if (_mapId == SaintMapleAltarMapId && step == 2)
            {
                if (LocalParticipantRole == WeddingParticipantRole.Guest)
                {
                    return new WeddingDialog
                    {
                        Message = WeddingCeremonyClientText.GetGuestBlessPromptText(),
                        NpcId = _npcId,
                        StartTime = currentTimeMs,
                        Duration = DialogDurationMs,
                        Mode = WeddingDialogMode.YesNo,
                        AllowTimeout = false
                    };
                }


                return null;

            }



            string dialogText = ResolveWeddingDialogText(_npcId, step);
            if (string.IsNullOrWhiteSpace(dialogText))
            {
                return null;
            }


            return new WeddingDialog
            {
                Message = dialogText,
                NpcId = _npcId,
                StartTime = currentTimeMs,
                Duration = DialogDurationMs,
                Mode = WeddingDialogMode.Text,
                AllowTimeout = false
            };
        }


        private string ResolveWeddingDialogText(int npcId, int step)
        {
            string propertyName = $"wedding{step}";
            try
            {
                WzImage npcImage = global::HaCreator.Program.FindImage("String", "Npc.img");
                npcImage?.ParseImage();
                string liveValue = npcImage?[npcId.ToString()]?[propertyName]?.GetString();
                if (!string.IsNullOrWhiteSpace(liveValue))
                {
                    return liveValue.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WeddingField] Failed to load {propertyName} for NPC {npcId}: {ex.Message}");
            }


            if (WeddingDialogFallbacks.TryGetValue(npcId, out Dictionary<int, string> dialogSet)
                && dialogSet.TryGetValue(step, out string fallback))
            {
                return fallback;
            }


            return null;

        }



        private void UpdateParticipantState()
        {
            LocalParticipantRole = WeddingParticipantRole.Guest;
            if (_localCharacterId.HasValue)
            {
                if (_localCharacterId.Value == _groomId)
                {
                    LocalParticipantRole = WeddingParticipantRole.Groom;
                }
                else if (_localCharacterId.Value == _brideId)
                {
                    LocalParticipantRole = WeddingParticipantRole.Bride;
                }
            }

            PruneInactiveParticipantActors();

            Vector2? groomPosition = null;
            Vector2? bridePosition = null;

            if (_localPlayerPosition.HasValue)
            {
                if (LocalParticipantRole == WeddingParticipantRole.Groom)
                {
                    groomPosition = _localPlayerPosition;
                }
                else if (LocalParticipantRole == WeddingParticipantRole.Bride)
                {
                    bridePosition = _localPlayerPosition;
                }
            }


            if (_groomId > 0
                && _participantPositions.TryGetValue(_groomId, out Vector2 resolvedGroomPosition)
                && LocalParticipantRole != WeddingParticipantRole.Groom)
            {
                groomPosition = resolvedGroomPosition;
            }


            if (_brideId > 0
                && _participantPositions.TryGetValue(_brideId, out Vector2 resolvedBridePosition)
                && LocalParticipantRole != WeddingParticipantRole.Bride)
            {
                bridePosition = resolvedBridePosition;
            }

            _groomPosition = groomPosition;
            _bridePosition = bridePosition;

            SyncParticipantActor(_groomId, WeddingParticipantRole.Groom, _groomPosition);

            SyncParticipantActor(_brideId, WeddingParticipantRole.Bride, _bridePosition);
            PromoteAudienceActorToParticipant(_groomId, WeddingParticipantRole.Groom, _groomPosition);
            PromoteAudienceActorToParticipant(_brideId, WeddingParticipantRole.Bride, _bridePosition);
        }

        private void PruneInactiveParticipantActors()
        {
            if (_participantActors.Count == 0)
            {
                return;
            }

            foreach (int characterId in _participantActors.Keys.ToArray())
            {
                if (characterId == _groomId || characterId == _brideId)
                {
                    continue;
                }

                _participantActors.Remove(characterId);
            }
        }



        private bool ShouldSendParticipantAdvancePacket()
        {
            if (LocalParticipantRole != WeddingParticipantRole.Groom
                && LocalParticipantRole != WeddingParticipantRole.Bride)
            {
                return false;
            }


            return !(_mapId == SaintMapleAltarMapId && _currentStep == 2);

        }



        public string DescribeStatus()
        {
            string role = LocalParticipantRole.ToString();
            string dialog = _currentDialog?.Mode == WeddingDialogMode.YesNo
                ? "guest bless prompt"
                : _currentDialog != null
                    ? $"dialog step {_currentStep}"
                    : "no dialog";
            string groomPosition = _groomPosition.HasValue
                ? $"({(int)_groomPosition.Value.X}, {(int)_groomPosition.Value.Y})"
                : "unknown";
            string bridePosition = _bridePosition.HasValue
                ? $"({(int)_bridePosition.Value.X}, {(int)_bridePosition.Value.Y})"
                : "unknown";
            string lastPacket = _lastPacketResponse.HasValue
                ? _lastPacketResponse.Value.ToString()
                : "none";
            string lastRemotePacket = _lastPacketType.HasValue
                ? DescribePacketType(_lastPacketType.Value)
                : "none";
            string scene = _ceremonyTextOverlayActive
                ? "declaration overlay active"
                : _ceremonyCardOverlayActive
                    ? "ceremony card overlay active"
                : _ceremonyCelebrationActive
                    ? "celebration particles active"
                    : "no scene overlay";
            if (IsWeddingPhotoSceneOwnerActive)
            {
                string viewport = WeddingPhotoSceneViewport.HasValue
                    ? $" viewport={WeddingPhotoSceneViewport.Value.Left},{WeddingPhotoSceneViewport.Value.Top},{WeddingPhotoSceneViewport.Value.Right},{WeddingPhotoSceneViewport.Value.Bottom}"
                    : " viewport=<none>";
                string lastPhotoPacket = _lastPacketType.HasValue
                    ? DescribePacketType(_lastPacketType.Value)
                    : "none";
                return $"Wedding photo scene owner map {_mapId}: {WeddingPhotoSceneOwnerDescription}.{viewport} coupleActors={_participantActors.Count}, audienceActors={_audienceActors.Count}, remoteLoaded={_loadedExternalRemoteActorIds.Count}, remotePending={_pendingExternalRemoteActorLoadIds.Count}, last packet {lastPhotoPacket}, scene={scene}. Packet owner remains CField_WeddingPhoto, not CField_Wedding::OnPacket.";
            }

            return $"Wedding map {_mapId}: step {_currentStep}, role {role}, dialog {dialog}, scene {scene}, coupleActors={_participantActors.Count}, audienceActors={_audienceActors.Count}, remoteLoaded={_loadedExternalRemoteActorIds.Count}, remotePending={_pendingExternalRemoteActorLoadIds.Count}, groom {groomPosition}, bride {bridePosition}, last packet {lastPacket}, last remote packet {lastRemotePacket}.";
        }


        private Vector2? TryGetBlessEffectWorldCenter()
        {
            if (!_groomPosition.HasValue || !_bridePosition.HasValue)
            {
                return null;
            }


            return new Vector2(
                (_groomPosition.Value.X + _bridePosition.Value.X) * 0.5f,
                ((_groomPosition.Value.Y + _bridePosition.Value.Y) * 0.5f) - 20f);
        }


        private Vector2? TryGetBlessEffectScreenCenter(int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            Vector2? worldCenter = TryGetBlessEffectWorldCenter();
            if (worldCenter.HasValue)
            {
                return new Vector2(
                    worldCenter.Value.X - mapShiftX + centerX,
                    worldCenter.Value.Y - mapShiftY + centerY);
            }

            return null;
        }

        #endregion



        #region Update

        public void Update(int currentTimeMs, float deltaSeconds)

        {

            if (!HasPresentationOwner) return;
            UpdateRemoteParticipantMovementSnapshots(currentTimeMs);
            AdvanceExternalRemoteActorLoadLifecycle(currentTimeMs);

            if (_blessEffectActive && !TryGetBlessEffectWorldCenter().HasValue)
            {
                SetBlessEffect(false, currentTimeMs);
            }


            float overlayTargetAlpha = _ceremonyTextOverlayActive ? 1f : 0f;
            _ceremonyTextOverlayAlpha = MathHelper.Clamp(
                _ceremonyTextOverlayAlpha + ((overlayTargetAlpha - _ceremonyTextOverlayAlpha) * Math.Min(deltaSeconds * 10f, 1f)),
                0f,
                1f);
            float cardOverlayTargetAlpha = _ceremonyCardOverlayActive ? 1f : 0f;
            _ceremonyCardOverlayAlpha = MathHelper.Clamp(
                _ceremonyCardOverlayAlpha + ((cardOverlayTargetAlpha - _ceremonyCardOverlayAlpha) * Math.Min(deltaSeconds * 8f, 1f)),
                0f,
                1f);
            UpdateCeremonyCelebration(deltaSeconds);


            // Update bless effect
            if (_blessEffectActive && (_blessFrames == null || _blessFrames.Count == 0))
            {
                int elapsed = currentTimeMs - _blessEffectStartTime;


                // Update sparkles
                foreach (var sparkle in _sparkles)
                {
                    if (elapsed < sparkle.SpawnDelay) continue;


                    int sparkleElapsed = elapsed - sparkle.SpawnDelay;
                    int loopedElapsed = sparkle.LifeTime > 0
                        ? sparkleElapsed % sparkle.LifeTime
                        : sparkleElapsed;
                    float lifeProgress = sparkle.LifeTime > 0
                        ? (float)loopedElapsed / sparkle.LifeTime
                        : 0f;


                    // Fade in/out
                    if (lifeProgress < 0.2f)
                        sparkle.Alpha = lifeProgress / 0.2f;
                    else if (lifeProgress > 0.8f)
                        sparkle.Alpha = (1f - lifeProgress) / 0.2f;
                    else
                        sparkle.Alpha = 1f;


                    // Move
                    sparkle.X += sparkle.Velocity.X * deltaSeconds;
                    sparkle.Y += sparkle.Velocity.Y * deltaSeconds;
                }
            }


            // Update dialog
            if (_currentDialog != null)
            {
                if (_currentDialog.AllowTimeout
                    && currentTimeMs - _currentDialog.StartTime > _currentDialog.Duration)
                {
                    RespondToCurrentDialog(accepted: false, currentTimeMs);
                }
            }
        }
        #endregion


        #region Draw
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount,
            Texture2D pixelTexture, SpriteFont font)
        {
            if (!HasPresentationOwner) return;


            DrawCeremonyOverlay(spriteBatch);
            DrawCeremonyCardOverlay(spriteBatch);
            DrawCeremonyCelebration(spriteBatch, mapShiftX, mapShiftY, centerX, centerY, tickCount);
            DrawRemoteParticipants(spriteBatch, skeletonMeshRenderer, mapShiftX, mapShiftY, centerX, centerY, tickCount, font);


            // Draw bless effect sparkles
            if (_blessEffectActive && _blessEffectAlpha > 0)
            {
                Vector2? blessCenter = TryGetBlessEffectScreenCenter(mapShiftX, mapShiftY, centerX, centerY);
                if (!blessCenter.HasValue)
                {
                    return;
                }


                if (_blessFrames != null && _blessFrames.Count > 0)
                {
                    IDXObject currentBlessFrame = GetCurrentBlessFrame(tickCount);
                    if (currentBlessFrame != null)
                    {
                        currentBlessFrame.DrawBackground(
                            spriteBatch,
                            skeletonMeshRenderer,
                            gameTime,
                            (int)blessCenter.Value.X + currentBlessFrame.X,
                            (int)blessCenter.Value.Y + currentBlessFrame.Y,
                            Color.White,
                            false,
                            null);
                    }
                }
                else
                {
                    foreach (var sparkle in _sparkles)
                    {
                        if (sparkle.Alpha <= 0) continue;


                        int x = (int)(blessCenter.Value.X + sparkle.X);
                        int y = (int)(blessCenter.Value.Y + sparkle.Y);
                        int size = (int)(8 * sparkle.Scale);
                        byte alpha = (byte)(sparkle.Alpha * _blessEffectAlpha * 255);


                        // Draw sparkle (cross pattern)
                        Color sparkleColor = new Color((byte)255, (byte)255, (byte)200, alpha);
                        spriteBatch.Draw(pixelTexture, new Rectangle(x - size / 2, y - 1, size, 2), sparkleColor);
                        spriteBatch.Draw(pixelTexture, new Rectangle(x - 1, y - size / 2, 2, size), sparkleColor);
                    }
                }
            }


            // Draw current dialog
            if (_currentDialog != null && font != null)
            {
                DrawDialog(spriteBatch, pixelTexture, font, tickCount);
            }


            // Debug info
            if (font != null)
            {
                string info = $"Wedding: Step {_currentStep} | Role: {LocalParticipantRole} | Bless: {_blessEffectActive}";
                spriteBatch.DrawString(font, info, new Vector2(10, 10), Color.Pink);
            }
        }


        private void DrawCeremonyOverlay(SpriteBatch spriteBatch)
        {
            if (_ceremonyTextOverlayTexture == null || _ceremonyTextOverlayAlpha <= 0f)
            {
                return;
            }


            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            Vector2 center = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f - 54f);
            Color tint = Color.White * _ceremonyTextOverlayAlpha;
            spriteBatch.Draw(
                _ceremonyTextOverlayTexture,
                center,
                null,
                tint,
                0f,
                new Vector2(_ceremonyTextOverlayOrigin.X, _ceremonyTextOverlayOrigin.Y),
                1f,
                SpriteEffects.None,
                0f);
        }


        private void DrawCeremonyCardOverlay(SpriteBatch spriteBatch)
        {
            if (_ceremonyCardOverlayTexture == null || _ceremonyCardOverlayAlpha <= 0f)
            {
                return;
            }


            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            Vector2 center = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f - 28f);
            Color tint = Color.White * _ceremonyCardOverlayAlpha;
            spriteBatch.Draw(
                _ceremonyCardOverlayTexture,
                center,
                null,
                tint,
                0f,
                new Vector2(_ceremonyCardOverlayOrigin.X, _ceremonyCardOverlayOrigin.Y),
                1f,
                SpriteEffects.None,
                0f);
        }


        private void SetCeremonyTextOverlay(bool active)
        {
            if (_ceremonyTextOverlayActive == active)
            {
                return;
            }

            _ceremonyTextOverlayActive = active;
            if (!_ceremonyTextOverlayActive && _ceremonyTextOverlayAlpha < 0.001f)
            {
                _ceremonyTextOverlayAlpha = 0f;
            }
        }


        private void SetCeremonyCardOverlay(bool active)
        {
            if (_ceremonyCardOverlayActive == active)
            {
                return;
            }

            _ceremonyCardOverlayActive = active;
            if (!_ceremonyCardOverlayActive && _ceremonyCardOverlayAlpha < 0.001f)
            {
                _ceremonyCardOverlayAlpha = 0f;
            }
        }


        private void SetCeremonyCelebration(bool active)
        {
            if (_ceremonyCelebrationActive == active)
            {
                return;
            }

            _ceremonyCelebrationActive = active;
            _ceremonyPetals.Clear();
            _ceremonyHearts.Clear();
            if (!_ceremonyCelebrationActive)
            {
                return;
            }


            SpawnCeremonyPetals();

            SpawnCeremonyHearts();

        }



        private void SpawnCeremonyPetals()
        {
            if (_ceremonyPetalFrames.Count == 0)
            {
                return;
            }


            for (int i = 0; i < _ceremonyPetalConfig.Count; i++)
            {
                _ceremonyPetals.Add(new WeddingSceneParticle
                {
                    ScreenNormalizedPosition = new Vector2(
                        (float)_random.NextDouble(),
                        (float)(_random.NextDouble() * 1.15f)),
                    Velocity = new Vector2(
                        -0.015f + (float)_random.NextDouble() * 0.03f,
                        0.05f + (float)_random.NextDouble() * 0.10f),
                    Scale = 0.7f + (float)_random.NextDouble() * 0.65f,
                    Rotation = (float)(_random.NextDouble() * Math.PI * 2.0),
                    RotationSpeed = -0.7f + (float)_random.NextDouble() * 1.4f,
                    Alpha = 0.45f + (float)_random.NextDouble() * 0.35f,
                    FrameIndex = ResolveSceneFrameIndex(i, _ceremonyPetalFrames.Count, _ceremonyPetalConfig.RandomFrames),
                    FrameTimeMs = _random.Next(0, 300),
                    DrawAroundCouple = false
                });
            }
        }


        private void SpawnCeremonyHearts()
        {
            if (_ceremonyHeartFrames.Count == 0)
            {
                return;
            }


            for (int i = 0; i < _ceremonyHeartConfig.Count; i++)
            {
                float angle = MathHelper.TwoPi * (i / (float)_ceremonyHeartConfig.Count);
                float radius = 18f + (float)_random.NextDouble() * 54f;
                _ceremonyHearts.Add(new WeddingSceneParticle
                {
                    WorldOffset = new Vector2(
                        (float)Math.Cos(angle) * radius,
                        -12f + (float)Math.Sin(angle * 1.6f) * 14f),
                    Velocity = new Vector2(
                        -8f + (float)_random.NextDouble() * 16f,
                        -18f - (float)_random.NextDouble() * 24f),
                    Scale = 0.8f + (float)_random.NextDouble() * 0.55f,
                    Rotation = -0.15f + (float)_random.NextDouble() * 0.30f,
                    RotationSpeed = -0.5f + (float)_random.NextDouble() * 1.0f,
                    Alpha = 0.68f + (float)_random.NextDouble() * 0.22f,
                    FrameIndex = ResolveSceneFrameIndex(i, _ceremonyHeartFrames.Count, _ceremonyHeartConfig.RandomFrames),
                    FrameTimeMs = _random.Next(0, 900),
                    DrawAroundCouple = true
                });
            }
        }


        private void UpdateCeremonyCelebration(float deltaSeconds)
        {
            if (!_ceremonyCelebrationActive)
            {
                return;
            }


            for (int i = 0; i < _ceremonyPetals.Count; i++)
            {
                WeddingSceneParticle petal = _ceremonyPetals[i];
                petal.ScreenNormalizedPosition += petal.Velocity * deltaSeconds;
                petal.Rotation += petal.RotationSpeed * deltaSeconds;
                petal.FrameTimeMs += (int)(deltaSeconds * 1000f);


                if (petal.ScreenNormalizedPosition.Y > 1.12f)
                {
                    petal.ScreenNormalizedPosition = new Vector2(
                        (float)_random.NextDouble(),
                        -0.08f - (float)_random.NextDouble() * 0.12f);
                    petal.Velocity = new Vector2(
                        -0.015f + (float)_random.NextDouble() * 0.03f,
                        0.05f + (float)_random.NextDouble() * 0.10f);
                    petal.Scale = 0.7f + (float)_random.NextDouble() * 0.65f;
                    petal.Alpha = 0.45f + (float)_random.NextDouble() * 0.35f;
                    petal.FrameIndex = ResolveSceneFrameIndex(i, _ceremonyPetalFrames.Count, _ceremonyPetalConfig.RandomFrames);
                    petal.FrameTimeMs = 0;
                }


                if (petal.ScreenNormalizedPosition.X < -0.15f)
                {
                    petal.ScreenNormalizedPosition = new Vector2(1.05f, petal.ScreenNormalizedPosition.Y);
                }
                else if (petal.ScreenNormalizedPosition.X > 1.15f)
                {
                    petal.ScreenNormalizedPosition = new Vector2(-0.05f, petal.ScreenNormalizedPosition.Y);
                }


                _ceremonyPetals[i] = petal;

            }



            for (int i = 0; i < _ceremonyHearts.Count; i++)
            {
                WeddingSceneParticle heart = _ceremonyHearts[i];
                heart.WorldOffset += heart.Velocity * deltaSeconds;
                heart.Rotation += heart.RotationSpeed * deltaSeconds;
                heart.FrameTimeMs += (int)(deltaSeconds * 1000f);
                heart.Alpha = MathHelper.Clamp(heart.Alpha - (deltaSeconds * 0.16f), 0.2f, 1f);


                if (heart.WorldOffset.Y < -130f || heart.Alpha <= 0.21f)
                {
                    float angle = (float)(_random.NextDouble() * Math.PI * 2.0);
                    float radius = 12f + (float)_random.NextDouble() * 46f;
                    heart.WorldOffset = new Vector2((float)Math.Cos(angle) * radius, -8f + (float)Math.Sin(angle) * 10f);
                    heart.Velocity = new Vector2(
                        -8f + (float)_random.NextDouble() * 16f,
                        -18f - (float)_random.NextDouble() * 24f);
                    heart.Scale = 0.8f + (float)_random.NextDouble() * 0.55f;
                    heart.Alpha = 0.68f + (float)_random.NextDouble() * 0.22f;
                    heart.FrameIndex = ResolveSceneFrameIndex(i, _ceremonyHeartFrames.Count, _ceremonyHeartConfig.RandomFrames);
                    heart.FrameTimeMs = 0;
                }


                _ceremonyHearts[i] = heart;

            }

        }



        private void DrawCeremonyCelebration(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTimeMs)
        {
            if (!_ceremonyCelebrationActive)
            {
                return;
            }


            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;

            DrawSceneParticles(spriteBatch, _ceremonyPetals, _ceremonyPetalFrames, null, viewport, currentTimeMs);



            Vector2? coupleCenter = TryGetBlessEffectScreenCenter(
                mapShiftX,
                mapShiftY,
                centerX,
                centerY);
            DrawSceneParticles(spriteBatch, _ceremonyHearts, _ceremonyHeartFrames, coupleCenter, viewport, currentTimeMs);
        }


        private static void DrawSceneParticles(
            SpriteBatch spriteBatch,
            List<WeddingSceneParticle> particles,
            List<WeddingSceneFrame> frames,
            Vector2? coupleCenter,
            Viewport viewport,
            int currentTimeMs)
        {
            if (particles.Count == 0 || frames.Count == 0)
            {
                return;
            }


            foreach (WeddingSceneParticle particle in particles)
            {
                WeddingSceneFrame frame = ResolveSceneFrame(frames, particle.FrameIndex, particle.FrameTimeMs + currentTimeMs);
                if (frame?.Texture == null)
                {
                    continue;
                }


                Vector2 position = particle.DrawAroundCouple && coupleCenter.HasValue
                    ? coupleCenter.Value + particle.WorldOffset
                    : new Vector2(
                        particle.ScreenNormalizedPosition.X * viewport.Width,
                        particle.ScreenNormalizedPosition.Y * viewport.Height);


                spriteBatch.Draw(
                    frame.Texture,
                    position,
                    null,
                    Color.White * particle.Alpha,
                    particle.Rotation,
                    frame.Origin,
                    particle.Scale,
                    SpriteEffects.None,
                    0f);
            }
        }


        private static WeddingSceneFrame ResolveSceneFrame(List<WeddingSceneFrame> frames, int baseFrameIndex, int animationTimeMs)
        {
            if (frames.Count == 0)
            {
                return null;
            }


            int totalDuration = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalDuration += Math.Max(frames[i].Delay, 1);
            }


            if (totalDuration <= 0)
            {
                return frames[Math.Clamp(baseFrameIndex, 0, frames.Count - 1)];
            }


            int loopTime = Math.Abs(animationTimeMs) % totalDuration;
            int accumulated = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                accumulated += Math.Max(frames[i].Delay, 1);
                if (loopTime < accumulated)
                {
                    return frames[i];
                }
            }


            return frames[Math.Clamp(baseFrameIndex, 0, frames.Count - 1)];

        }



        private int ResolveSceneFrameIndex(int particleIndex, int frameCount, bool randomFrames)
        {
            if (frameCount <= 0)
            {
                return 0;
            }

            return randomFrames
                ? _random.Next(frameCount)
                : particleIndex % frameCount;
        }


        private void DrawRemoteParticipants(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            SpriteFont font)
        {
            if (UseExternalRemoteActorRenderer)
            {
                return;
            }

            if (_participantActors.Count == 0 && _audienceActors.Count == 0)
            {
                return;
            }


            foreach (WeddingRemoteParticipant participant in _participantActors.Values
                .Concat(_audienceActors.Values)
                .OrderBy(entry => entry.Position.Y)
                .ThenBy(entry => entry.Role)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!ShouldDrawParticipantInternally(participant))
                {
                    continue;
                }

                if (!ShouldDrawParticipantLikeClient(participant))
                {
                    continue;
                }

                AssembledFrame frame = participant.Assembler.GetFrameAtTime(participant.ActionName, tickCount)
                    ?? participant.Assembler.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), tickCount);
                if (frame == null)
                {
                    continue;
                }

                int screenX = (int)MathF.Round(participant.Position.X) - mapShiftX + centerX;
                int screenY = (int)MathF.Round(participant.Position.Y) - mapShiftY + centerY;

                DrawParticipantPacketOwnedItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    participant,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                DrawParticipantCarryItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    participant,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                DrawParticipantCompletedSetItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    participant,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                frame.Draw(spriteBatch, skeletonMeshRenderer, screenX, screenY, participant.FacingRight, Color.White);
                DrawParticipantPacketOwnedItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    participant,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawParticipantCarryItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    participant,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawParticipantCompletedSetItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    participant,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);



                if (font == null)
                {
                    continue;
                }


                float topY = screenY - frame.FeetOffset + frame.Bounds.Top;
                DrawParticipantLabels(spriteBatch, font, participant, screenX, topY);
            }
        }

        internal static bool ShouldDrawParticipantLikeClient(WeddingRemoteParticipant participant)
        {
            return participant?.TemporaryStats.KnownState.IsHiddenLikeClient != true;
        }

        internal static IReadOnlyList<string> BuildParticipantLabelLines(WeddingRemoteParticipant participant)
        {
            List<string> lines = new();
            if (participant == null || !ShouldDrawParticipantLikeClient(participant))
            {
                return lines;
            }

            string guildName = participant.Build?.GuildName?.Trim();
            if (!string.IsNullOrWhiteSpace(guildName))
            {
                lines.Add(guildName);
            }

            string name = participant.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                lines.Add(name);
            }

            return lines;
        }

        private static void DrawParticipantLabels(
            SpriteBatch spriteBatch,
            SpriteFont font,
            WeddingRemoteParticipant participant,
            int screenX,
            float topY)
        {
            IReadOnlyList<string> labelLines = BuildParticipantLabelLines(participant);
            if (labelLines.Count == 0)
            {
                return;
            }

            float labelTopY = topY - ((labelLines.Count * font.LineSpacing) + ((labelLines.Count - 1) * ParticipantLabelLineSpacing)) - 6f;
            for (int i = 0; i < labelLines.Count; i++)
            {
                string line = labelLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                Vector2 textSize = font.MeasureString(line);
                Texture2D guildMarkBackground = null;
                Texture2D guildMark = null;
                bool drawGuildMark = i == 0
                    && !string.IsNullOrWhiteSpace(participant.Build?.GuildName)
                    && TryResolveParticipantGuildMarkTextures(spriteBatch.GraphicsDevice, participant, out guildMarkBackground, out guildMark);
                float guildMarkWidth = drawGuildMark
                    ? Math.Max(guildMarkBackground?.Width ?? 0, guildMark?.Width ?? 0)
                    : 0f;
                float totalWidth = textSize.X + (drawGuildMark ? guildMarkWidth + 4f : 0f);
                Vector2 textPosition = new(
                    screenX - (totalWidth * 0.5f) + (drawGuildMark ? guildMarkWidth + 4f : 0f),
                    labelTopY + (i * (font.LineSpacing + ParticipantLabelLineSpacing)));
                Color textColor = i == labelLines.Count - 1
                    ? new Color(255, 242, 178)
                    : new Color(176, 226, 255);
                if (drawGuildMark)
                {
                    float iconLeft = textPosition.X - guildMarkWidth - 4f;
                    DrawGuildMarkIcon(
                        spriteBatch,
                        guildMarkBackground,
                        guildMark,
                        new Vector2(iconLeft, textPosition.Y + ((font.LineSpacing - Math.Max(guildMarkBackground?.Height ?? 0, guildMark?.Height ?? 0)) * 0.5f)));
                }

                DrawOutlinedText(spriteBatch, font, line, textPosition, Color.Black, textColor);
            }
        }

        private static void DrawGuildMarkIcon(
            SpriteBatch spriteBatch,
            Texture2D backgroundTexture,
            Texture2D markTexture,
            Vector2 position)
        {
            if (backgroundTexture != null)
            {
                spriteBatch.Draw(backgroundTexture, position, Color.White);
            }

            if (markTexture == null)
            {
                return;
            }

            if (backgroundTexture == null)
            {
                spriteBatch.Draw(markTexture, position, Color.White);
                return;
            }

            Vector2 markPosition = new(
                position.X + ((backgroundTexture.Width - markTexture.Width) * 0.5f),
                position.Y + ((backgroundTexture.Height - markTexture.Height) * 0.5f));
            spriteBatch.Draw(markTexture, markPosition, Color.White);
        }

        private static bool TryResolveParticipantGuildMarkTextures(
            GraphicsDevice device,
            WeddingRemoteParticipant participant,
            out Texture2D backgroundTexture,
            out Texture2D markTexture)
        {
            backgroundTexture = null;
            markTexture = null;
            CharacterBuild build = participant?.Build;
            if (device == null
                || build?.GuildMarkBackgroundId is not int backgroundId || backgroundId <= 0
                || build.GuildMarkId is not int markId || markId <= 0)
            {
                return false;
            }

            int backgroundColor = build.GuildMarkBackgroundColor ?? 1;
            int markColor = build.GuildMarkColor ?? 1;
            backgroundTexture = GuildMarkTextureCache.GetBackgroundTexture(device, backgroundId, backgroundColor);
            markTexture = GuildMarkTextureCache.GetMarkTexture(device, markId, markColor);
            return backgroundTexture != null || markTexture != null;
        }

        private static void DrawParticipantPacketOwnedItemEffect(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            WeddingRemoteParticipant participant,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            ItemEffectAnimationSet effect = participant?.PacketOwnedItemEffect?.Effect;
            if (effect?.OwnerLayers == null || effect.OwnerLayers.Count == 0)
            {
                return;
            }

            int elapsedTime = Math.Max(0, currentTime - participant.PacketOwnedItemEffect.StartTime);
            foreach (PortableChairLayer layer in effect.OwnerLayers)
            {
                if ((layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, elapsedTime);
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    frame,
                    screenX,
                    screenY,
                    participant.FacingRight);
            }
        }

        private void DrawParticipantCarryItemEffect(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            WeddingRemoteParticipant participant,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (_characterLoader == null
                || ResolveParticipantCarryItemEffectCount(participant) is not int carryCount
                || carryCount <= 0)
            {
                return;
            }

            CarryItemEffectDefinition effect = _characterLoader.LoadCarryItemEffectDefinition();
            if (effect?.IsReady != true)
            {
                return;
            }

            (int totalTokenCount, int tensTokenCount) = RemoteUserActorPool.ResolveCarryItemEffectTokenCounts(carryCount);
            for (int index = 0; index < totalTokenCount; index++)
            {
                PortableChairLayer layer = RemoteUserActorPool.ResolveCarryItemEffectLayer(effect, index, tensTokenCount);
                if (layer?.Animation == null)
                {
                    continue;
                }

                Point offset = RemoteUserActorPool.ResolveCarryItemEffectOffset(
                    index,
                    totalTokenCount,
                    tensTokenCount,
                    participant.FacingRight,
                    out bool isFrontLayer);
                if (isFrontLayer != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(
                    layer,
                    RemoteUserActorPool.ResolveCarryItemEffectAnimationTime(currentTime, index));
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    frame,
                    screenX + offset.X,
                    screenY + offset.Y,
                    participant.FacingRight);
            }
        }

        private void DrawParticipantCompletedSetItemEffect(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            WeddingRemoteParticipant participant,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (_characterLoader == null)
            {
                return;
            }

            int setItemId = ResolveParticipantCompletedSetItemId(participant);
            if (setItemId <= 0)
            {
                return;
            }

            ItemEffectAnimationSet effect = _characterLoader.LoadCompletedSetItemEffectAnimationSet(setItemId);
            if (effect?.OwnerLayers == null || effect.OwnerLayers.Count == 0)
            {
                return;
            }

            foreach (PortableChairLayer layer in effect.OwnerLayers)
            {
                if ((layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, currentTime);
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    frame,
                    screenX,
                    screenY,
                    participant.FacingRight);
            }
        }

        private static void DrawOutlinedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color shadowColor, Color textColor)
        {
            Vector2[] offsets =
            {
                new Vector2(-1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, -1f),
                new Vector2(0f, 1f)
            };
            foreach (Vector2 offset in offsets)
            {
                spriteBatch.DrawString(font, text, position + offset, shadowColor);
            }

            spriteBatch.DrawString(font, text, position, textColor);
        }


        private void DrawDialog(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int currentTimeMs)

        {

            if (_currentDialog == null) return;



            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
            int maxTextWidth = Math.Min(DialogMaxWidth, Math.Max(180, screenWidth - 80)) - (DialogPadding * 2);
            IReadOnlyList<string> dialogLines = WrapDialogMessage(font, _currentDialog.Message, maxTextWidth);
            if (dialogLines.Count == 0)
            {
                return;
            }

            float maxLineWidth = 0f;
            foreach (string line in dialogLines)
            {
                maxLineWidth = Math.Max(maxLineWidth, font.MeasureString(line).X);
            }

            int lineHeight = font.LineSpacing;
            int textHeight = (dialogLines.Count * lineHeight) + ((dialogLines.Count - 1) * DialogLineSpacing);
            int promptHeight = _currentDialog.Mode == WeddingDialogMode.YesNo ? lineHeight + 10 : 0;
            int boxWidth = (int)Math.Ceiling(Math.Min(maxLineWidth + (DialogPadding * 2), DialogMaxWidth));
            int boxHeight = textHeight + promptHeight + (DialogPadding * 2);
            int boxX = (screenWidth - boxWidth) / 2;
            int boxY = 100;


            // Calculate alpha based on time
            int elapsed = currentTimeMs - _currentDialog.StartTime;
            float alpha = 1f;
            if (elapsed < DialogFadeInDurationMs)
            {
                alpha = elapsed / (float)DialogFadeInDurationMs;
            }
            else if (_currentDialog.AllowTimeout
                && elapsed > _currentDialog.Duration - DialogFadeInDurationMs)
            {
                alpha = (_currentDialog.Duration - elapsed) / (float)DialogFadeInDurationMs;
            }
            alpha = MathHelper.Clamp(alpha, 0f, 1f);


            Color bgColor = new Color(50, 50, 50, (int)(200 * alpha));

            Color textColor = new Color(255, 255, 255, (int)(255 * alpha));



            // Draw box

            spriteBatch.Draw(pixelTexture, new Rectangle(boxX, boxY, boxWidth, boxHeight), bgColor);



            // Draw text

            Vector2 textPosition = new Vector2(boxX + DialogPadding, boxY + DialogPadding);
            for (int i = 0; i < dialogLines.Count; i++)
            {
                spriteBatch.DrawString(
                    font,
                    dialogLines[i],
                    textPosition + new Vector2(0f, i * (lineHeight + DialogLineSpacing)),
                    textColor);
            }



            if (_currentDialog.Mode == WeddingDialogMode.YesNo)
            {
                Vector2 promptPosition = new Vector2(boxX + DialogPadding, boxY + boxHeight - DialogPadding - lineHeight);
                spriteBatch.DrawString(font, "[Yes]     [No]", promptPosition, new Color(255, 235, 180, (int)(255 * alpha)));
            }
        }

        internal static IReadOnlyList<string> WrapDialogMessageForMeasurement(Func<string, float> measureTextWidth, string text, float maxWidth)
        {
            List<string> lines = new();
            if (measureTextWidth == null || string.IsNullOrWhiteSpace(text) || maxWidth <= 0f)
            {
                return lines;
            }

            foreach (string paragraph in text.Replace("\r", string.Empty).Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    lines.Add(string.Empty);
                    continue;
                }

                string currentLine = string.Empty;
                foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    foreach (string chunk in SplitMeasuredWord(word, maxWidth, measureTextWidth))
                    {
                        string candidate = string.IsNullOrEmpty(currentLine)
                            ? chunk
                            : $"{currentLine} {chunk}";
                        if (!string.IsNullOrEmpty(currentLine) && measureTextWidth(candidate) > maxWidth)
                        {
                            lines.Add(currentLine);
                            currentLine = chunk;
                            continue;
                        }

                        currentLine = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                }
            }

            return lines;
        }

        private static IReadOnlyList<string> WrapDialogMessage(SpriteFont font, string text, float maxWidth)
        {
            if (font == null)
            {
                return Array.Empty<string>();
            }

            return WrapDialogMessageForMeasurement(
                value => font.MeasureString(value).X,
                text,
                maxWidth);
        }

        private static IEnumerable<string> SplitMeasuredWord(string word, float maxWidth, Func<string, float> measureTextWidth)
        {
            if (string.IsNullOrEmpty(word))
            {
                yield break;
            }

            if (measureTextWidth(word) <= maxWidth)
            {
                yield return word;
                yield break;
            }

            int start = 0;
            while (start < word.Length)
            {
                int length = 1;
                int bestLength = 1;
                while (start + length <= word.Length)
                {
                    string candidate = word.Substring(start, length);
                    if (measureTextWidth(candidate) <= maxWidth)
                    {
                        bestLength = length;
                        length++;
                        continue;
                    }

                    break;
                }

                yield return word.Substring(start, bestLength);
                start += bestLength;
            }
        }


        private IDXObject GetCurrentBlessFrame(int currentTimeMs)
        {
            if (_blessFrames == null || _blessFrames.Count == 0)
            {
                return null;
            }


            int totalDuration = 0;
            foreach (IDXObject frame in _blessFrames)
            {
                totalDuration += Math.Max(frame.Delay, 1);
            }


            if (totalDuration <= 0)
            {
                return _blessFrames[0];
            }


            int elapsed = Math.Max(0, currentTimeMs - _blessEffectStartTime);
            int loopTime = elapsed % totalDuration;
            int accumulated = 0;
            foreach (IDXObject frame in _blessFrames)
            {
                accumulated += Math.Max(frame.Delay, 1);
                if (loopTime < accumulated)
                {
                    return frame;
                }
            }


            return _blessFrames[_blessFrames.Count - 1];

        }



        private bool IsPronouncementOrBlessingStep(int step)
        {
            return _mapId == SaintMapleAltarMapId
                ? step >= 3
                : step >= 1;
        }

        private static int ResolveCeremonyNpcId(int mapId)
        {
            return mapId switch
            {
                WhiteWeddingAltarMapId => WhiteWeddingAltarNpcId,
                SaintMapleAltarMapId => SaintMapleAltarNpcId,
                _ => 0
            };
        }


        private void SyncParticipantActor(int characterId, WeddingParticipantRole role, Vector2? position)
        {
            if (characterId <= 0 || !position.HasValue)
            {
                _participantActors.Remove(characterId);
                _pendingRemoteParticipantOperationsByCharacterId.Remove(characterId);
                RemoveRelationshipRecordDispatchKeysForOwnerAcrossTypes(characterId);
                return;
            }


            if (_localCharacterId.HasValue && _localCharacterId.Value == characterId)
            {
                _participantActors.Remove(characterId);
                _pendingRemoteParticipantOperationsByCharacterId.Remove(characterId);
                RemoveRelationshipRecordDispatchKeysForOwnerAcrossTypes(characterId);
                return;
            }


            if (!_participantActors.TryGetValue(characterId, out WeddingRemoteParticipant participant))
            {
                CharacterBuild build = CreateParticipantBuild(role, characterId);
                if (build == null)
                {
                    return;
                }


                participant = new WeddingRemoteParticipant(
                    characterId,
                    role,
                    build.Name,
                    build,
                    new CharacterAssembler(build));
                _participantActors[characterId] = participant;
            }


            participant.Position = position.Value;

            if (!participant.HasExplicitFacing)
            {
                participant.FacingRight = role == WeddingParticipantRole.Groom;
            }
            if (!participant.HasExplicitAction || string.IsNullOrWhiteSpace(participant.ActionName))
            {
                participant.BaseActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
                participant.ActionName = ResolveVisibleParticipantActionName(participant, participant.BaseActionName);
            }

            TryApplyPendingRemoteParticipantOperations(participant);
        }



        private CharacterBuild CreateParticipantBuild(WeddingParticipantRole role, int characterId)
        {
            CharacterBuild build = _localPlayerBuild?.Clone();
            if (build == null)
            {
                return null;
            }


            build.Id = characterId;
            build.Name = role == WeddingParticipantRole.Groom ? "Groom" : "Bride";
            return build;
        }
        #endregion


        #region Reset
        public void Reset()
        {
            _clearBgmOverride?.Invoke();
            _isActive = false;
            IsWeddingPhotoSceneOwnerActive = false;
            WeddingPhotoSceneOwnerDescription = null;
            WeddingPhotoSceneViewport = null;
            WeddingPhotoSceneBackgroundMusicPath = null;
            _currentStep = 0;
            _blessEffectActive = false;
            _ceremonyTextOverlayActive = false;
            _ceremonyTextOverlayAlpha = 0f;
            _ceremonyCardOverlayActive = false;
            _ceremonyCardOverlayAlpha = 0f;
            _ceremonyCelebrationActive = false;
            _sparkles.Clear();
            _ceremonyPetals.Clear();
            _ceremonyHearts.Clear();
            _currentDialog = null;
            _dialogQueue.Clear();
            _groomId = 0;
            _brideId = 0;
            _groomPosition = null;
            _bridePosition = null;
            _participantPositions.Clear();
            _participantActors.Clear();
            _audienceActors.Clear();
            _audienceActorNamesById.Clear();
            _pendingRemoteParticipantOperationsByCharacterId.Clear();
            ClearRelationshipRecordDispatchTables();
            _pendingExternalRemoteActorLoads.Clear();
            _pendingExternalRemoteActorLoadIds.Clear();
            _loadedExternalRemoteActorIds.Clear();
            _officialRemoteLifecycleActorIds.Clear();
            _externalRemoteActorLoadWindowEndTimeMs = 0;
            _externalRemoteActorLoadCooltimeEndTimeMs = 0;
            _lastPacketResponse = null;
            _lastPacketType = null;
            _localCharacterId = null;
            _localPlayerPosition = null;
            _localPlayerBuild = null;
            LocalParticipantRole = WeddingParticipantRole.Guest;
            _weddingPhotoScenePresentationStage = WeddingPhotoScenePresentationStage.None;
            _lastWeddingPhotoSceneStagePacketType = null;
        }
        #endregion

        private void SyncWeddingPhotoScenePresentationStage(int? stagePacketType = null)
        {
            if (stagePacketType.HasValue)
            {
                _lastWeddingPhotoSceneStagePacketType = stagePacketType.Value;
            }

            if (!IsWeddingPhotoSceneOwnerActive || _isActive)
            {
                _weddingPhotoScenePresentationStage = WeddingPhotoScenePresentationStage.None;
                return;
            }

            if (_blessEffectActive)
            {
                _weddingPhotoScenePresentationStage = WeddingPhotoScenePresentationStage.BlessEffect;
                return;
            }

            if (_ceremonyCelebrationActive)
            {
                _weddingPhotoScenePresentationStage = WeddingPhotoScenePresentationStage.StepCelebrationLayer;
                return;
            }

            if (_ceremonyCardOverlayActive)
            {
                _weddingPhotoScenePresentationStage = WeddingPhotoScenePresentationStage.StepCardLayer;
                return;
            }

            if (_ceremonyTextOverlayActive)
            {
                _weddingPhotoScenePresentationStage = WeddingPhotoScenePresentationStage.StepTextLayer;
                return;
            }

            _weddingPhotoScenePresentationStage = _weddingPhotoScenePresentationPacketTrail.Count > 0
                ? WeddingPhotoScenePresentationStage.PacketTrailOnly
                : WeddingPhotoScenePresentationStage.None;
        }
    }


    public enum WeddingParticipantRole
    {
        Guest,
        Groom,
        Bride
    }


    public enum WeddingDialogMode
    {
        Text,
        YesNo
    }


    public class WeddingSparkle
    {
        public float X, Y;
        public float Scale;
        public float Alpha;
        public Vector2 Velocity;
        public int LifeTime;
        public int SpawnDelay;
    }


    public class WeddingDialog
    {
        public string Message;
        public int NpcId;
        public int StartTime;
        public int Duration;
        public WeddingDialogMode Mode;
        public bool AllowTimeout;
    }


    public readonly record struct WeddingRemoteParticipantSnapshot(
        int CharacterId,
        string Name,
        WeddingParticipantRole Role,
        Vector2 Position,
        bool FacingRight,
        string ActionName,
        CharacterBuild Build,
        PlayerMovementSyncSnapshot MovementSnapshot,
        int? PortableChairItemId,
        int? PortableChairPairCharacterId,
        RemoteUserTemporaryStatSnapshot TemporaryStats,
        ushort TemporaryStatDelay,
        int TemporaryStatRevision,
        int? PacketOwnedItemEffectItemId,
        int PacketOwnedItemEffectRevision,
        RemoteUserAvatarModifiedPacket? AvatarModifiedState,
        int AvatarModifiedRevision,
        int NameTagRevision,
        int ProfileMetadataRevision,
        int GuildMarkRevision);

    internal readonly record struct WeddingRemoteSpawnPacket(
        int CharacterId,
        string Name,
        LoginAvatarLook AvatarLook,
        Vector2 Position,
        byte MoveAction,
        int PortableChairItemId,
        RemoteUserTemporaryStatSnapshot TemporaryStats,
        int? Level,
        string GuildName,
        int? JobId);

    internal readonly record struct WeddingRemoteMovePacket(
        int CharacterId,
        Vector2 Position,
        byte MoveAction,
        PlayerMovementSyncSnapshot MovementSnapshot);

    internal readonly record struct WeddingRemoteChairPacket(int CharacterId, int PortableChairItemId, int? PairCharacterId);

    public sealed class WeddingPacketOwnedItemEffectState
    {
        public int ItemId { get; init; }
        public ItemEffectAnimationSet Effect { get; init; }
        public int StartTime { get; init; }
    }

    public sealed class WeddingRemoteParticipant
    {
        public WeddingRemoteParticipant(int characterId, WeddingParticipantRole role, string name, CharacterBuild build, CharacterAssembler assembler)
        {
            CharacterId = characterId;
            Role = role;
            Name = name;
            Build = build;
            Assembler = assembler;
            BaseActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
            ActionName = BaseActionName;
        }


        public int CharacterId { get; set; }
        public WeddingParticipantRole Role { get; }

        public string Name { get; set; }
        public CharacterBuild Build { get; set; }
        public CharacterAssembler Assembler { get; set; }
        public Vector2 Position { get; set; }
        public bool FacingRight { get; set; } = true;
        public string BaseActionName { get; set; }
        public string ActionName { get; set; }
        public PlayerMovementSyncSnapshot MovementSnapshot { get; set; }
        public int? PortableChairItemId { get; set; }
        public int? PortableChairPairCharacterId { get; set; }
        public RemoteUserTemporaryStatSnapshot TemporaryStats { get; set; }
        public ushort TemporaryStatDelay { get; set; }
        public int TemporaryStatRevision { get; set; }
        public WeddingPacketOwnedItemEffectState PacketOwnedItemEffect { get; set; }
        public int? PacketOwnedItemEffectItemId { get; set; }
        public int PacketOwnedItemEffectRevision { get; set; }
        public RemoteUserAvatarModifiedPacket? AvatarModifiedState { get; set; }
        public int AvatarModifiedRevision { get; set; }
        public int NameTagRevision { get; set; }
        public int ProfileMetadataRevision { get; set; }
        public int GuildMarkRevision { get; set; }
        public bool MovementDrivenActionSelection { get; set; }
        public bool HasExplicitFacing { get; set; }
        public bool HasExplicitAction { get; set; }
        public bool HasExplicitBuild { get; set; }
    }



    public sealed class WeddingSceneFrame
    {
        public WeddingSceneFrame(Texture2D texture, Vector2 origin, int delay)
        {
            Texture = texture;
            Origin = origin;
            Delay = Math.Max(delay, 1);
        }


        public Texture2D Texture { get; }
        public Vector2 Origin { get; }
        public int Delay { get; }
    }


    internal readonly record struct WeddingSceneAssetConfig(int Count, bool RandomFrames);


    public struct WeddingSceneParticle
    {
        public Vector2 ScreenNormalizedPosition;
        public Vector2 WorldOffset;
        public Vector2 Velocity;
        public float Scale;
        public float Rotation;
        public float RotationSpeed;
        public float Alpha;
        public int FrameIndex;
        public int FrameTimeMs;
        public bool DrawAroundCouple;
    }


    public enum WeddingPacketOpcode
    {
        AdvanceStep = 163,
        GuestBless = 164
    }


    public readonly record struct WeddingPacketResponse(
        WeddingPacketOpcode Opcode,
        int Step,
        bool Accepted,
        int TimeMs)
    {
        public override string ToString()
        {
            return Opcode == WeddingPacketOpcode.GuestBless
                ? $"{(int)Opcode} (guest bless {(Accepted ? "yes" : "no")})"
                : $"{(int)Opcode} (step {Step})";
        }
    }
    #endregion
}
