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
using HaCreator.MapSimulator.Managers;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using HaSharedLibrary.Util;
using MapleLib.Converters;
using MapleLib.PacketLib;using HaCreator.MapSimulator.Pools;

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
        private const int CathedralWeddingMapId = 680000110;
        private const int ChapelWeddingMapId = 680000210;
        private const int CathedralNpcId = 9201011;
        private const int ChapelNpcId = 9201002;
        private const int DialogDurationMs = 5000;
        private const string ChapelGuestBlessPromptText = "Would you like to give your blessing to the couple?";
        private const string BlessEffectImageName = "BasicEff.img";
        private const string BlessEffectPath = "Wedding";
        private const string WeddingBgmPath = "BgmEvent/wedding";
        private const string WeddingUiImageName = "UIWindow.img";
        private const string CeremonyTextOverlayPath = "wedding/text/0";
        private const string CeremonyCardOverlayPath = "wedding/card/0";
        private const string WeddingMapHelperImageName = "MapHelper.img";
        private const string WeddingWeatherPath = "weather/wedding";
        private const string WeddingHeartWeatherPath = "weather/heartWedding";
        private const int CeremonyPetalCount = 28;
        private const int CeremonyHeartCount = 14;
        private const int PacketTypeUserEnterField = 179;
        private const int PacketTypeUserLeaveField = 180;
        private const int PacketTypeUserMove = 210;
        private const int PacketTypeSetActivePortableChair = 222;
        private const int PacketTypeAvatarModified = 223;

        private static readonly Dictionary<int, Dictionary<int, string>> WeddingDialogFallbacks = new()
        {
            [ChapelNpcId] = new Dictionary<int, string>
            {
                [0] = "Dearly beloved, we are gathered here today to celebrate the marriage of these two fine, upstanding people. One can clearly see the love between you two, and it's a sight I'll never tire of. You have proved your love and received your Parent's Blessing. Do you wish to seal your love in the eternal embrace of marriage?",
                [1] = "Very well. Guests may now Bless the couple if they choose...",
                [3] = "By the power vested in me through the mighty Maple tree, I now pronounce you Husband and Wife. You may kiss the bride!",
                [4] = "With the Blessing of the Maple tree, I wish both of you a long and safe marriage."
            },
            [CathedralNpcId] = new Dictionary<int, string>
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
        #endregion

        #region Public Properties
        public bool IsActive => _isActive;
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
        public int? LastPacketType => _lastPacketType;
        public bool UseExternalRemoteActorRenderer { get; set; }
        #endregion

        #region Initialization
        public void Initialize(
            GraphicsDevice device,
            Action<string> requestBgmOverride = null,
            Action clearBgmOverride = null,

            Func<LoginAvatarLook, string, CharacterBuild> remoteBuildFactory = null)
        {
            _requestBgmOverride = requestBgmOverride;
            _clearBgmOverride = clearBgmOverride;
            _remoteBuildFactory = remoteBuildFactory;
            _blessFrames = LoadBlessFrames(device);
            LoadCeremonyOverlay(device);
            LoadCeremonyCelebrationAssets(device);
        }

        public void Enable(int mapId)
        {
            _isActive = true;
            _mapId = mapId;
            _currentStep = 0;

            // Set NPC based on map (from client: 680000110 = 9201011, 680000210 = 9201002)
            _npcId = mapId == CathedralWeddingMapId ? CathedralNpcId : ChapelNpcId;
            _groomId = 0;
            _brideId = 0;
            _groomPosition = null;
            _bridePosition = null;
            _participantPositions.Clear();
            _participantActors.Clear();
            _audienceActors.Clear();
            _audienceActorNamesById.Clear();
            _lastPacketResponse = null;
            _lastPacketType = null;
            _ceremonyTextOverlayActive = false;
            _ceremonyTextOverlayAlpha = 0f;
            _ceremonyCardOverlayActive = false;
            _ceremonyCardOverlayAlpha = 0f;
            _ceremonyCelebrationActive = false;
            _ceremonyPetals.Clear();
            _ceremonyHearts.Clear();
            _currentDialog = null;
            _dialogQueue.Clear();
            LocalParticipantRole = WeddingParticipantRole.Guest;

            System.Diagnostics.Debug.WriteLine($"[WeddingField] Enabled for map {mapId}, NPC {_npcId}");
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

            if (!_isActive)

            {

                errorMessage = "Wedding runtime inactive.";

                return false;

            }

            try

            {

                switch (packetType)

                {

                    case PacketTypeUserEnterField:

                        return TryApplyRemoteSpawnPacket(payload, out errorMessage);

                    case PacketTypeUserLeaveField:

                        return TryApplyRemoteLeavePacket(payload, out errorMessage);

                    case PacketTypeUserMove:

                        return TryApplyRemoteMovePacket(payload, currentTimeMs, out errorMessage);

                    case PacketTypeSetActivePortableChair:

                        return TryApplyRemoteChairPacket(payload, out errorMessage);

                    case PacketTypeAvatarModified:

                        return TryApplyRemoteAvatarModifiedPacket(payload, out errorMessage);

                    default:

                        errorMessage = $"Unsupported wedding packet type: {packetType}";

                        return false;

                }

            }

            catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException || ex is IOException)

            {

                errorMessage = ex.Message;

                return false;

            }

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
                    participant.Build?.Clone());
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
                    participant.Build?.Clone()));
            }

            return snapshots;
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
            if (characterId.HasValue
                && _audienceActorNamesById.TryGetValue(characterId.Value, out string previousName)
                && !string.Equals(previousName, actorName, StringComparison.OrdinalIgnoreCase))
            {
                _audienceActors.Remove(previousName);
            }

            if (!_audienceActors.TryGetValue(actorName, out WeddingRemoteParticipant participant))
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
            }

            return _audienceActors.Remove(normalized);
        }

        public void ClearAudienceParticipants()
        {
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
                participant.Build?.Clone());
            return true;
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
                CharacterBuild actorBuild = build.Clone();
                if (string.IsNullOrWhiteSpace(actorBuild.Name))
                {
                    actorBuild.Name = participant.Name;
                }

                participant.Name = actorBuild.Name;
                participant.Build = actorBuild;
                participant.Assembler = new CharacterAssembler(actorBuild);
                participant.HasExplicitBuild = true;
            }

            if (facingRight.HasValue)
            {
                participant.FacingRight = facingRight.Value;
                participant.HasExplicitFacing = true;
            }

            if (!string.IsNullOrWhiteSpace(actionName))
            {
                participant.ActionName = actionName.Trim();
                participant.HasExplicitAction = true;
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
            TryConfigureParticipantActor(
                characterId,
                fallbackPosition ?? participant.Position,
                participant.Build,
                participant.FacingRight,
                participant.ActionName,
                out _);
        }

        private bool TryApplyRemoteSpawnPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!TryDecodeRemoteSpawnPacket(payload, out WeddingRemoteSpawnPacket spawn, out errorMessage))
            {
                return false;
            }

            CharacterBuild build = CreateRemoteBuildFromAvatarLook(spawn.Name, spawn.AvatarLook, out errorMessage);
            if (build == null)
            {
                return false;
            }

            string actionName = ResolveRemoteActionName(spawn.MoveAction, spawn.PortableChairItemId);
            bool facingRight = (spawn.MoveAction & 1) == 0;
            if (spawn.CharacterId == _groomId || spawn.CharacterId == _brideId)
            {
                return TryConfigureParticipantActor(spawn.CharacterId, spawn.Position, build, facingRight, actionName, out errorMessage);
            }

            UpsertAudienceParticipant(build, spawn.Position, facingRight, actionName, spawn.CharacterId);
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
                return TryConfigureParticipantActor(move.CharacterId, move.Position, build: null, facingRight, actionName, out errorMessage);
            }

            return TryMoveAudienceParticipantById(move.CharacterId, move.Position, facingRight, actionName, out errorMessage);
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
            if (!TryDecodeRemoteAvatarModifiedPacket(payload, out WeddingRemoteAvatarModifiedPacket packet, out errorMessage))
            {
                return false;
            }

            if (packet.AvatarLook == null)
            {
                return true;
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

                return TryConfigureParticipantActor(packet.CharacterId, position, build, facingRight, actionName, out errorMessage);
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

            UpsertAudienceParticipant(audienceBuild, audienceSnapshot.Position, audienceSnapshot.FacingRight, audienceSnapshot.ActionName, packet.CharacterId);
            return true;
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

            return build;
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
                spawn.PortableChairItemId ?? 0);
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
                move.MoveAction);
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

            packet = new WeddingRemoteChairPacket(chair.CharacterId, chair.ChairItemId ?? 0);
            return true;
        }

        private static bool TryDecodeRemoteAvatarModifiedPacket(byte[] payload, out WeddingRemoteAvatarModifiedPacket packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;
            if (!TryCreatePacketReader(payload, PacketTypeAvatarModified, out PacketReader reader, out errorMessage))
            {
                return false;
            }

            try
            {
                int characterId = reader.ReadInt();
                byte flags = reader.ReadByte();
                LoginAvatarLook avatarLook = null;

                if ((flags & 0x01) != 0)
                {
                    if (!LoginAvatarLookCodec.TryDecode(reader, out avatarLook, out string avatarError))
                    {
                        errorMessage = avatarError ?? "Wedding remote avatar-modified packet could not decode AvatarLook.";
                        return false;
                    }
                }

                if ((flags & 0x02) != 0)
                {
                    reader.ReadByte();
                }

                if ((flags & 0x04) != 0)
                {
                    reader.ReadByte();
                }

                if (reader.ReadByte() != 0)
                {
                    reader.ReadLong();
                    reader.ReadLong();
                    reader.ReadInt();
                }

                if (reader.ReadByte() != 0)
                {
                    reader.ReadLong();
                    reader.ReadLong();
                    reader.ReadInt();
                }

                if (reader.ReadByte() != 0)
                {
                    reader.ReadInt();
                    reader.ReadInt();
                    reader.ReadInt();
                }

                reader.ReadInt();
                packet = new WeddingRemoteAvatarModifiedPacket(characterId, avatarLook);
                return true;
            }
            catch (EndOfStreamException)
            {
                errorMessage = "Wedding remote avatar-modified packet ended before decoding completed.";
                return false;
            }
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
                PacketTypeUserMove => "usermove (210)",
                PacketTypeSetActivePortableChair => "chair (222)",
                PacketTypeAvatarModified => "avatarmodified (223)",
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
            System.Diagnostics.Debug.WriteLine($"[WeddingField] OnWeddingProgress: step={step}, groom={groomId}, bride={brideId}");

            _currentStep = step;
            _groomId = groomId;
            _brideId = brideId;
            UpdateParticipantState();

            // Step 0: Start ceremony - play wedding BGM
            if (step == 0)
            {
                SetBlessEffect(false, currentTimeMs);
                SetCeremonyTextOverlay(true);
                SetCeremonyCardOverlay(false);
                SetCeremonyCelebration(active: false);
                _requestBgmOverride?.Invoke(WeddingBgmPath);
            }
            else
            {
                SetCeremonyTextOverlay(false);
                bool pronounced = IsPronouncementOrBlessingStep(step);
                SetCeremonyCardOverlay(pronounced);
                SetCeremonyCelebration(pronounced);
            }

            // Show dialog for current step
            ShowWeddingDialog(step, currentTimeMs);
        }

        /// <summary>
        /// OnWeddingCeremonyEnd - Packet 380
        /// Triggers the bless effect (sparkles)
        /// </summary>
        public void OnWeddingCeremonyEnd(int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine("[WeddingField] OnWeddingCeremonyEnd - Starting bless effect");
            SetBlessEffect(true, currentTimeMs);
            SetCeremonyCardOverlay(active: true);
            SetCeremonyCelebration(active: true);
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
                        true,
                        currentTimeMs);
                }
            }
            else if (ShouldSendParticipantAdvancePacket())
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
            _blessEffectActive = active;
            if (active)
            {
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
                LoadSceneFrames(mapHelperImage?.GetFromPath(WeddingWeatherPath) as WzImageProperty, _ceremonyPetalFrames, device);
                LoadSceneFrames(mapHelperImage?.GetFromPath(WeddingHeartWeatherPath) as WzImageProperty, _ceremonyHeartFrames, device);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WeddingField] Failed to load ceremony celebration assets: {ex.Message}");
            }
        }

        private static void LoadSceneFrames(WzImageProperty sourceProperty, List<WeddingSceneFrame> destination, GraphicsDevice device)
        {
            destination.Clear();
            if (sourceProperty == null)
            {
                return;
            }

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
        }

        private void ShowWeddingDialog(int step, int currentTimeMs)
        {
            _currentDialog = CreateDialogForStep(step, currentTimeMs);
        }

        private WeddingDialog CreateDialogForStep(int step, int currentTimeMs)
        {
            if (_mapId == ChapelWeddingMapId && step == 2)
            {
                if (LocalParticipantRole == WeddingParticipantRole.Guest)
                {
                    return new WeddingDialog
                    {
                        Message = ChapelGuestBlessPromptText,
                        NpcId = _npcId,
                        StartTime = currentTimeMs,
                        Duration = DialogDurationMs,
                        Mode = WeddingDialogMode.YesNo
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
                Mode = WeddingDialogMode.Text
            };
        }

        private string ResolveWeddingDialogText(int npcId, int step)
        {
            string propertyName = $"wedding{step}";
            try
            {
                WzImage npcImage = global::HaCreator.Program.FindImage("String", "Npc.img");
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

            if (_localPlayerPosition.HasValue)
            {
                if (LocalParticipantRole == WeddingParticipantRole.Groom)
                {
                    _groomPosition = _localPlayerPosition;
                }
                else if (LocalParticipantRole == WeddingParticipantRole.Bride)
                {
                    _bridePosition = _localPlayerPosition;
                }
            }

            if (_groomId > 0
                && _participantPositions.TryGetValue(_groomId, out Vector2 groomPosition)
                && LocalParticipantRole != WeddingParticipantRole.Groom)
            {
                _groomPosition = groomPosition;
            }

            if (_brideId > 0
                && _participantPositions.TryGetValue(_brideId, out Vector2 bridePosition)
                && LocalParticipantRole != WeddingParticipantRole.Bride)
            {
                _bridePosition = bridePosition;
            }

            SyncParticipantActor(_groomId, WeddingParticipantRole.Groom, _groomPosition);
            SyncParticipantActor(_brideId, WeddingParticipantRole.Bride, _bridePosition);
            PromoteAudienceActorToParticipant(_groomId, WeddingParticipantRole.Groom, _groomPosition);
            PromoteAudienceActorToParticipant(_brideId, WeddingParticipantRole.Bride, _bridePosition);
        }

        private bool ShouldSendParticipantAdvancePacket()
        {
            if (LocalParticipantRole != WeddingParticipantRole.Groom
                && LocalParticipantRole != WeddingParticipantRole.Bride)
            {
                return false;
            }

            return !(_mapId == ChapelWeddingMapId && _currentStep == 2);
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
            return $"Wedding map {_mapId}: step {_currentStep}, role {role}, dialog {dialog}, scene {scene}, coupleActors={_participantActors.Count}, audienceActors={_audienceActors.Count}, groom {groomPosition}, bride {bridePosition}, last packet {lastPacket}, last remote packet {lastRemotePacket}.";
        }

        private Vector2? TryGetBlessEffectWorldCenter()
        {
            Vector2? groom = _groomPosition;
            Vector2? bride = _bridePosition;

            if (!groom.HasValue && !bride.HasValue)
            {
                return null;
            }

            if (!groom.HasValue && bride.HasValue)
            {
                groom = new Vector2(bride.Value.X - 40f, bride.Value.Y);
            }

            if (!bride.HasValue && groom.HasValue)
            {
                bride = new Vector2(groom.Value.X + 40f, groom.Value.Y);
            }

            return new Vector2(
                (groom!.Value.X + bride!.Value.X) * 0.5f,
                ((groom.Value.Y + bride.Value.Y) * 0.5f) - 20f);
        }

        private Vector2 GetBlessEffectScreenCenter(int mapShiftX, int mapShiftY, int centerX, int centerY, int screenWidth, int screenHeight)
        {
            Vector2? worldCenter = TryGetBlessEffectWorldCenter();
            if (worldCenter.HasValue)
            {
                return new Vector2(
                    worldCenter.Value.X - mapShiftX + centerX,
                    worldCenter.Value.Y - mapShiftY + centerY);
            }

            return new Vector2(screenWidth * 0.5f, screenHeight * 0.5f - 20f);
        }
        #endregion

        #region Update
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive) return;

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
                if (currentTimeMs - _currentDialog.StartTime > _currentDialog.Duration)
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
            if (!_isActive) return;

            DrawCeremonyOverlay(spriteBatch);
            DrawCeremonyCardOverlay(spriteBatch);
            DrawCeremonyCelebration(spriteBatch, mapShiftX, mapShiftY, centerX, centerY, tickCount);
            DrawRemoteParticipants(spriteBatch, skeletonMeshRenderer, mapShiftX, mapShiftY, centerX, centerY, tickCount, font);

            // Draw bless effect sparkles
            if (_blessEffectActive && _blessEffectAlpha > 0)
            {
                int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
                int screenHeight = spriteBatch.GraphicsDevice.Viewport.Height;
                Vector2 blessCenter = GetBlessEffectScreenCenter(mapShiftX, mapShiftY, centerX, centerY, screenWidth, screenHeight);

                if (_blessFrames != null && _blessFrames.Count > 0)
                {
                    IDXObject currentBlessFrame = GetCurrentBlessFrame(tickCount);
                    if (currentBlessFrame != null)
                    {
                        currentBlessFrame.DrawBackground(
                            spriteBatch,
                            skeletonMeshRenderer,
                            gameTime,
                            (int)blessCenter.X + currentBlessFrame.X,
                            (int)blessCenter.Y + currentBlessFrame.Y,
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

                        int x = (int)(blessCenter.X + sparkle.X);
                        int y = (int)(blessCenter.Y + sparkle.Y);
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
            _ceremonyTextOverlayActive = active;
            if (!_ceremonyTextOverlayActive && _ceremonyTextOverlayAlpha < 0.001f)
            {
                _ceremonyTextOverlayAlpha = 0f;
            }
        }

        private void SetCeremonyCardOverlay(bool active)
        {
            _ceremonyCardOverlayActive = active;
            if (!_ceremonyCardOverlayActive && _ceremonyCardOverlayAlpha < 0.001f)
            {
                _ceremonyCardOverlayAlpha = 0f;
            }
        }

        private void SetCeremonyCelebration(bool active)
        {
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

            for (int i = 0; i < CeremonyPetalCount; i++)
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
                    FrameIndex = _random.Next(_ceremonyPetalFrames.Count),
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

            for (int i = 0; i < CeremonyHeartCount; i++)
            {
                float angle = MathHelper.TwoPi * (i / (float)CeremonyHeartCount);
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
                    FrameIndex = _random.Next(_ceremonyHeartFrames.Count),
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
                    petal.FrameIndex = _random.Next(_ceremonyPetalFrames.Count);
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
                    heart.FrameIndex = _random.Next(_ceremonyHeartFrames.Count);
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

            Vector2 coupleCenter = GetBlessEffectScreenCenter(
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                viewport.Width,
                viewport.Height);
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
                AssembledFrame frame = participant.Assembler.GetFrameAtTime(participant.ActionName, tickCount)
                    ?? participant.Assembler.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), tickCount);
                if (frame == null)
                {
                    continue;
                }

                int screenX = (int)MathF.Round(participant.Position.X) - mapShiftX + centerX;
                int screenY = (int)MathF.Round(participant.Position.Y) - mapShiftY + centerY;
                frame.Draw(spriteBatch, skeletonMeshRenderer, screenX, screenY, participant.FacingRight, Color.White);

                if (font == null)
                {
                    continue;
                }

                Vector2 textSize = font.MeasureString(participant.Name);
                float topY = screenY - frame.FeetOffset + frame.Bounds.Top;
                Vector2 textPosition = new Vector2(
                    screenX - (textSize.X * 0.5f),
                    topY - textSize.Y - 6f);
                DrawOutlinedText(spriteBatch, font, participant.Name, textPosition, Color.Black, new Color(255, 242, 178));
            }
        }

        private static void DrawOutlinedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color shadowColor, Color textColor)
        {
            spriteBatch.DrawString(font, text, position + Vector2.One, shadowColor);
            spriteBatch.DrawString(font, text, position, textColor);
        }

        private void DrawDialog(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int currentTimeMs)
        {
            if (_currentDialog == null) return;

            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
            Vector2 textSize = font.MeasureString(_currentDialog.Message);
            int boxWidth = (int)textSize.X + 40;
            int boxHeight = (int)textSize.Y + 20;
            int boxX = (screenWidth - boxWidth) / 2;
            int boxY = 100;

            // Calculate alpha based on time
            int elapsed = currentTimeMs - _currentDialog.StartTime;
            float alpha = 1f;
            if (elapsed < 300)
                alpha = elapsed / 300f;
            else if (elapsed > _currentDialog.Duration - 300)
                alpha = (_currentDialog.Duration - elapsed) / 300f;

            Color bgColor = new Color(50, 50, 50, (int)(200 * alpha));
            Color textColor = new Color(255, 255, 255, (int)(255 * alpha));

            // Draw box
            spriteBatch.Draw(pixelTexture, new Rectangle(boxX, boxY, boxWidth, boxHeight), bgColor);

            // Draw text
            spriteBatch.DrawString(font, _currentDialog.Message, new Vector2(boxX + 20, boxY + 10), textColor);

            if (_currentDialog.Mode == WeddingDialogMode.YesNo)
            {
                Vector2 promptPosition = new Vector2(boxX + 20, boxY + boxHeight - 28);
                spriteBatch.DrawString(font, "[Yes]     [No]", promptPosition, new Color(255, 235, 180, (int)(255 * alpha)));
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
            return _mapId == ChapelWeddingMapId
                ? step >= 3
                : step >= 1;
        }

        private void SyncParticipantActor(int characterId, WeddingParticipantRole role, Vector2? position)
        {
            if (characterId <= 0 || !position.HasValue)
            {
                _participantActors.Remove(characterId);
                return;
            }

            if (_localCharacterId.HasValue && _localCharacterId.Value == characterId)
            {
                _participantActors.Remove(characterId);
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
                participant.ActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
            }
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
            _lastPacketResponse = null;
            _lastPacketType = null;
            _localCharacterId = null;
            _localPlayerPosition = null;
            _localPlayerBuild = null;
            LocalParticipantRole = WeddingParticipantRole.Guest;
        }
        #endregion
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
    }

    public readonly record struct WeddingRemoteParticipantSnapshot(
        int CharacterId,
        string Name,
        WeddingParticipantRole Role,
        Vector2 Position,
        bool FacingRight,
        string ActionName,
        CharacterBuild Build);

    internal readonly record struct WeddingRemoteSpawnPacket(
        int CharacterId,
        string Name,
        LoginAvatarLook AvatarLook,
        Vector2 Position,
        byte MoveAction,
        int PortableChairItemId);

    internal readonly record struct WeddingRemoteMovePacket(int CharacterId, Vector2 Position, byte MoveAction);

    internal readonly record struct WeddingRemoteChairPacket(int CharacterId, int PortableChairItemId);

    internal readonly record struct WeddingRemoteAvatarModifiedPacket(int CharacterId, LoginAvatarLook AvatarLook);

    public sealed class WeddingRemoteParticipant
    {
        public WeddingRemoteParticipant(int characterId, WeddingParticipantRole role, string name, CharacterBuild build, CharacterAssembler assembler)
        {
            CharacterId = characterId;
            Role = role;
            Name = name;
            Build = build;
            Assembler = assembler;
            ActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
        }

        public int CharacterId { get; set; }
        public WeddingParticipantRole Role { get; }
        public string Name { get; set; }
        public CharacterBuild Build { get; set; }
        public CharacterAssembler Assembler { get; set; }
        public Vector2 Position { get; set; }
        public bool FacingRight { get; set; } = true;
        public string ActionName { get; set; }

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
