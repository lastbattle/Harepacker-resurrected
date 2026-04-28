using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using MapleLib.Converters;
using MapleLib.PacketLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using BinaryReader = MapleLib.PacketLib.PacketReader;
namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Aggregates minigame field runtimes behind a single simulator surface.
    /// This gives parity work a stable ownership seam before each minigame is
    /// expanded into client-like packet, timerboard, and result handling.
    /// </summary>
    #region Ariant Arena Field (CField_AriantArena)
    /// <summary>
    /// Ariant Arena ranking and result flow.
    ///
    /// Client evidence:
    /// - CField_AriantArena::OnUserScore (0x5492b0): updates or removes score rows, clamps score to 9999, and re-sorts rank order
    ///   while suppressing the local player's entry for job branches 8xx and 9xx
    /// - CField_AriantArena::UpdateScoreAndRank (0x547c90): draws a top-left score surface with icon at (5, y), name at (21, y),
    ///   score at (106, y), 17px row spacing, and redraws user name tags after score refreshes
    /// - CField_AriantArena::OnShowResult (0x547630): loads the AriantMatch result animation at the center-top origin with a +100 Y offset
    /// - WZ evidence: UI/UIWindow.img/AriantMatch and UI/UIWindow2.img/AriantMatch expose the result frames and rank icons
    /// </summary>
    public class AriantArenaField
    {
        private const string AriantRemoteSourceTag = "ariantarena";
        private const int MaxRankEntries = 6;
        private const int MaxScore = 9999;
        private const int PacketTypeUserEnterField = 179;
        private const int PacketTypeUserLeaveField = 180;
        private const int PacketTypeUserMove = 210;
        private const int PacketTypeMeleeAttack1 = 211;
        private const int PacketTypeMeleeAttack2 = 212;
        private const int PacketTypeMeleeAttack3 = 213;
        private const int PacketTypeMeleeAttack4 = 214;
        private const int PacketTypeSkillPrepare = 215;
        private const int PacketTypeMovingShootAttackPrepare = 216;
        private const int PacketTypeSkillCancel = 217;
        private const int PacketTypeHit = 218;
        private const int PacketTypeEmotion = 219;
        private const int PacketTypeSetActiveEffectItem = 220;
        private const int PacketTypeUpgradeTombEffect = 221;
        private const int PacketTypeSetActivePortableChair = 222;
        private const int PacketTypeAvatarModified = 223;
        private const int PacketTypeEffect = 224;
        private const int PacketTypeTemporaryStatSet = 225;
        private const int PacketTypeTemporaryStatReset = 226;
        private const int PacketTypeReceiveHp = 227;
        private const int PacketTypeGuildNameChanged = 228;
        private const int PacketTypeGuildMarkChanged = 229;
        private const int PacketTypeThrowGrenade = 230;
        private const int PacketTypeShowResult = 171;
        private const int PacketTypeUserScore = 354;
        private const int RankIconStringPoolId = 0x1123;
        private const int ResultLayerStringPoolId = 0x1124;
        private const int ResultSoundStringPoolId = 0x1125;
        private const int ScoreTextStringPoolId = 0x112A;
        private const string RankIconFallbackPathFormat = "UI/UIWindow.img/AriantMatch/characterIcon/{0}";
        private const string ResultLayerFallbackPath = "UI/UIWindow.img/AriantMatch/Result";
        private const string ResultSoundFallbackPath = "Sound/MiniGame.img/Show";
        private const string ScoreTextFallbackFormat = "{0} Point";
        private const int ScoreLayerScreenX = 0;
        private const int ScoreLayerScreenY = 30;
        private const int ScoreLayerWidth = 300;
        private const int ScoreLayerHeight = 300;
        private const int IconX = 5;
        private const int NameX = 21;
        private const int ScoreX = 106;
        private const int FirstIconY = 0;
        private const int FirstTextY = 2;
        private const int RowSpacing = 17;
        private const int ResultOffsetY = 100;
        private const int ResultHoldDurationMs = 1200;
        private readonly List<AriantArenaScoreEntry> _entries = new();
        private readonly List<IDXObject> _resultFrames = new();
        private readonly List<IDXObject> _rankIcons = new();
        private GraphicsDevice _graphicsDevice;
        private bool _assetsLoaded;
        private bool _isActive;
        private bool _showScoreboard;
        private bool _showResult;
        private int _resultFrameIndex;
        private int _resultFrameStartedAt;
        private int _resultVisibleUntil;
        private int _scoreRefreshSerial;
        private int _localPlayerJob;
        private RemoteUserActorPool _remoteUserPool;
        private SoundManager _soundManager;
        private Func<LoginAvatarLook, string, CharacterBuild> _remoteBuildFactory;
        private string _resultSoundKey;
        private string _lastResultMessage;
        private string _localPlayerName;
        private int? _lastPacketType;
        public bool IsActive => _isActive;
        public IReadOnlyList<AriantArenaScoreEntry> Entries => _entries;
        public int ScoreRefreshSerial => _scoreRefreshSerial;
        public int RemoteParticipantCount => CountAriantRemoteParticipants();
        internal static Rectangle ScoreLayerBoundsForTesting => new(ScoreLayerScreenX, ScoreLayerScreenY, ScoreLayerWidth, ScoreLayerHeight);
        internal bool IsScoreLayerVisibleForTesting => _showScoreboard;
        internal bool IsResultLayerVisibleForTesting => _showResult;
        public void Initialize(
            GraphicsDevice graphicsDevice,
            SoundManager soundManager = null,
            Func<LoginAvatarLook, string, CharacterBuild> remoteBuildFactory = null)
        {
            _graphicsDevice = graphicsDevice;
            _soundManager = soundManager;
            _remoteBuildFactory = remoteBuildFactory;
            EnsureAssetsLoaded();
        }
        public void SetRemoteUserPool(RemoteUserActorPool remoteUserPool)
        {
            _remoteUserPool = remoteUserPool;
        }
        public void Enable()
        {
            _isActive = true;
            _showScoreboard = true;
            _showResult = false;
            _resultFrameIndex = 0;
            _resultFrameStartedAt = 0;
            _resultVisibleUntil = 0;
            _scoreRefreshSerial = 0;
            _lastResultMessage = null;
            _lastPacketType = null;
            ClearRemoteParticipants();
            EnsureAssetsLoaded();
        }
        public void SetLocalPlayerState(string playerName, int jobId)
        {
            _localPlayerName = string.IsNullOrWhiteSpace(playerName) ? null : playerName.Trim();
            _localPlayerJob = Math.Max(0, jobId);
        }
        public void OnUserScore(string userName, int score)
        {
            ApplyUserScoreBatch(new[] { new AriantArenaScoreUpdate(userName, score) });
        }
        public bool TryApplyPacket(int packetType, byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            _lastPacketType = packetType;
            if (!_isActive)
            {
                errorMessage = "Ariant Arena runtime inactive.";
                return false;
            }
            try
            {
                switch (packetType)
                {
                    case PacketTypeShowResult:
                        OnShowResult(currentTimeMs);
                        return true;
                    case PacketTypeUserEnterField:
                        return TryApplyRemoteSpawnPacket(payload, out errorMessage);
                    case PacketTypeUserLeaveField:
                        return TryApplyRemoteLeavePacket(payload, out errorMessage);
                    case PacketTypeUserMove:
                        return TryApplyRemoteMovePacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeMeleeAttack1:
                    case PacketTypeMeleeAttack2:
                    case PacketTypeMeleeAttack3:
                    case PacketTypeMeleeAttack4:
                        return TryApplyRemoteMeleeAttackPacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeSkillPrepare:
                        return TryApplyRemotePreparedSkillPacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeMovingShootAttackPrepare:
                        return TryApplyRemoteMovingShootAttackPreparePacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeSkillCancel:
                        return TryApplyRemotePreparedSkillClearPacket(payload, out errorMessage);
                    case PacketTypeEmotion:
                        return TryApplyRemoteEmotionPacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeSetActiveEffectItem:
                        return TryApplyRemoteActiveEffectItemPacket(payload, out errorMessage);
                    case PacketTypeUpgradeTombEffect:
                        return TryApplyRemoteUpgradeTombPacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeSetActivePortableChair:
                        return TryApplyRemoteChairPacket(payload, out errorMessage);
                    case PacketTypeAvatarModified:
                        return TryApplyRemoteAvatarModifiedPacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeTemporaryStatSet:
                        return TryApplyRemoteTemporaryStatSetPacket(payload, out errorMessage);
                    case PacketTypeTemporaryStatReset:
                        return TryApplyRemoteTemporaryStatResetPacket(payload, out errorMessage);
                    case PacketTypeReceiveHp:
                        return TryApplyRemoteReceiveHpPacket(payload, out errorMessage);
                    case PacketTypeGuildNameChanged:
                        return TryApplyRemoteGuildNameChangedPacket(payload, out errorMessage);
                    case PacketTypeGuildMarkChanged:
                        return TryApplyRemoteGuildMarkChangedPacket(payload, out errorMessage);
                    case PacketTypeHit:
                        return TryApplyRemoteHitPacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeEffect:
                        return TryApplyRemoteEffectPacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeThrowGrenade:
                        return TryApplyRemoteThrowGrenadePacket(payload, currentTimeMs, out errorMessage);
                    case PacketTypeUserScore:
                        ApplyUserScoreBatch(DecodeUserScorePacket(payload));
                        return true;
                    default:
                        errorMessage = $"Unsupported Ariant packet type: {packetType}";
                        return false;
                }
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException || ex is IOException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
        public void ApplyUserScoreBatch(IEnumerable<AriantArenaScoreUpdate> updates)
        {
            if (!_isActive)
            {
                return;
            }
            if (updates == null)
            {
                return;
            }

            bool changed = false;
            foreach (AriantArenaScoreUpdate update in updates)
            {
                string normalizedName = update.UserName?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    continue;
                }
                int existingIndex = _entries.FindIndex(entry => string.Equals(entry.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
                if (update.Score < 0)
                {
                    if (existingIndex >= 0)
                    {
                        _entries.RemoveAt(existingIndex);
                        changed = true;
                    }
                    continue;
                }
                if (ShouldSuppressLocalRankEntry(normalizedName))
                {
                    if (existingIndex >= 0)
                    {
                        _entries.RemoveAt(existingIndex);
                        changed = true;
                    }
                    continue;
                }
                int clampedScore = Math.Clamp(update.Score, 0, MaxScore);
                if (existingIndex >= 0)
                {
                    if (_entries[existingIndex].Score != clampedScore)
                    {
                        _entries[existingIndex] = _entries[existingIndex] with { Score = clampedScore };
                        changed = true;
                    }
                }
                else
                {
                    _entries.Add(new AriantArenaScoreEntry(normalizedName, clampedScore));
                    changed = true;
                }
            }

            if (changed)
            {
                _entries.Sort(static (left, right) =>
                {
                    int scoreCompare = right.Score.CompareTo(left.Score);
                    return scoreCompare != 0
                        ? scoreCompare
                        : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                });
                ReassignRankIconIndexes();
            }

            _scoreRefreshSerial++;
            _showScoreboard = true;
            _showResult = false;
        }
        public void OnShowResult(int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }
            EnsureAssetsLoaded();
            _showScoreboard = false;
            _showResult = _resultFrames.Count > 0;
            _resultFrameIndex = 0;
            _resultFrameStartedAt = currentTimeMs;
            _resultVisibleUntil = currentTimeMs + GetResultDuration() + ResultHoldDurationMs;
            _lastResultMessage = null;
            if (!string.IsNullOrWhiteSpace(_resultSoundKey))
            {
                _soundManager?.PlaySound(_resultSoundKey);
            }
        }
        public void ClearScores()
        {
            _entries.Clear();
            _showScoreboard = false;
            _showResult = false;
            _resultFrameIndex = 0;
            _resultFrameStartedAt = 0;
            _resultVisibleUntil = 0;
            _scoreRefreshSerial = 0;
            _lastResultMessage = null;
            _lastPacketType = null;
        }
        public void UpsertRemoteParticipant(CharacterBuild build, Vector2 position, bool facingRight = true, string actionName = null, int? characterId = null)
        {
            if (!_isActive || build == null || string.IsNullOrWhiteSpace(build.Name))
            {
                return;
            }

            int resolvedCharacterId = characterId ?? ResolveSyntheticRemoteParticipantId(build.Name);
            CharacterBuild remoteBuild = build.Clone();
            remoteBuild.Name = string.IsNullOrWhiteSpace(remoteBuild.Name) ? build.Name.Trim() : remoteBuild.Name.Trim();
            _remoteUserPool?.TryAddOrUpdate(
                resolvedCharacterId,
                remoteBuild,
                position,
                out _,
                facingRight,
                NormalizeRemoteActionName(actionName),
                AriantRemoteSourceTag,
                isVisibleInWorld: true);
        }

        public bool TryMoveRemoteParticipant(string name, Vector2 position, bool? facingRight, string actionName, out string message)
        {
            message = null;
            if (!_isActive)
            {
                message = "Ariant Arena runtime inactive.";
                return false;
            }
            string normalizedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                message = "Remote Ariant actor name is required.";
                return false;
            }
            if (!TryGetRemoteActorByName(normalizedName, out RemoteUserActor participant))
            {
                message = $"Remote Ariant actor '{normalizedName}' does not exist.";
                return false;
            }

            return _remoteUserPool.TryMove(
                participant.CharacterId,
                position,
                facingRight,
                string.IsNullOrWhiteSpace(actionName) ? null : NormalizeRemoteActionName(actionName),
                out message);
        }

        public bool TryMoveRemoteParticipant(int characterId, Vector2 position, bool? facingRight, string actionName, out string message)
        {
            message = null;
            if (!TryGetRemoteActor(characterId, out _))
            {
                message = $"Remote Ariant actor id {characterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryMove(
                characterId,
                position,
                facingRight,
                string.IsNullOrWhiteSpace(actionName) ? null : NormalizeRemoteActionName(actionName),
                out message);
        }

        public bool RemoveRemoteParticipant(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string normalizedName = name.Trim();
            if (!TryGetRemoteActorByName(normalizedName, out RemoteUserActor participant))
            {
                return false;
            }

            return _remoteUserPool.TryRemove(participant.CharacterId, out _);
        }

        public bool RemoveRemoteParticipant(int characterId)
        {
            return TryGetRemoteActor(characterId, out _)
                && _remoteUserPool.TryRemove(characterId, out _);
        }

        public void ClearRemoteParticipants()
        {
            _remoteUserPool?.RemoveBySourceTag(AriantRemoteSourceTag);
        }

        public bool TryGetRemoteParticipant(string name, out AriantArenaRemoteParticipantSnapshot snapshot)
        {
            if (TryGetRemoteActorByName(name, out RemoteUserActor participant))
            {
                snapshot = new AriantArenaRemoteParticipantSnapshot(
                    participant.Name,
                    participant.Position,
                    participant.FacingRight,
                    participant.ActionName);
                return true;
            }
            snapshot = default;
            return false;
        }

        public bool TryGetRemoteParticipant(int characterId, out AriantArenaRemoteParticipantSnapshot snapshot)
        {
            if (TryGetRemoteActor(characterId, out RemoteUserActor participant))
            {
                snapshot = new AriantArenaRemoteParticipantSnapshot(
                    participant.Name,
                    participant.Position,
                    participant.FacingRight,
                    participant.ActionName);
                return true;
            }

            snapshot = default;
            return false;
        }
        public void Update(int currentTimeMs)
        {
            if (!_isActive || !_showResult || _resultFrames.Count == 0)
            {
                return;
            }
            if (currentTimeMs >= _resultVisibleUntil)
            {
                _showResult = false;
                return;
            }
            while (_resultFrameIndex < _resultFrames.Count - 1)
            {
                IDXObject frame = _resultFrames[_resultFrameIndex];
                int delay = frame.Delay > 0 ? frame.Delay : 100;
                if (currentTimeMs - _resultFrameStartedAt < delay)
                {
                    break;
                }
                _resultFrameStartedAt += delay;
                _resultFrameIndex++;
            }
        }
        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            Texture2D pixelTexture,
            SpriteFont font = null)
        {
            if (!_isActive)
            {
                return;
            }
            if (_showScoreboard && font != null)
            {
                DrawScoreboard(spriteBatch, skeletonMeshRenderer, gameTime, font);
            }
            if (_showResult)
            {
                DrawResult(spriteBatch, skeletonMeshRenderer, gameTime, pixelTexture, font);
            }
        }
        public void Reset()
        {
            _isActive = false;
            _showScoreboard = false;
            _showResult = false;
            _entries.Clear();
            _resultFrameIndex = 0;
            _resultFrameStartedAt = 0;
            _resultVisibleUntil = 0;
            _scoreRefreshSerial = 0;
            _localPlayerJob = 0;
            _lastResultMessage = null;
            _localPlayerName = null;
            _lastPacketType = null;
            ClearRemoteParticipants();
        }
        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Ariant Arena runtime inactive";
            }
            string leaderText = _entries.Count == 0
                ? "no scores"
                : string.Join(", ", _entries.Take(MaxRankEntries).Select((entry, index) => $"{index + 1}.{entry.Name}={entry.Score}"));
            return $"Ariant Arena active, {_entries.Count} score row(s), remoteActors={CountAriantRemoteParticipants()}, result={(_showResult ? "showing" : "idle")}, refresh={_scoreRefreshSerial}, lastPacket={(_lastPacketType?.ToString() ?? "None")}, {leaderText}";
        }
        private bool ShouldSuppressLocalRankEntry(string normalizedName)
        {
            return !string.IsNullOrWhiteSpace(_localPlayerName)
                && string.Equals(normalizedName, _localPlayerName, StringComparison.OrdinalIgnoreCase)
                && IsHiddenAriantArenaJob(_localPlayerJob);
        }
        private static bool IsHiddenAriantArenaJob(int jobId)
        {
            int branch = Math.Abs(jobId) % 1000 / 100;
            return branch == 8 || branch == 9;
        }
        private void DrawScoreboard(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, SpriteFont font)
        {
            int rowCount = Math.Min(_entries.Count, MaxRankEntries);
            for (int i = 0; i < rowCount; i++)
            {
                int iconY = FirstIconY + (i * RowSpacing);
                int textY = FirstTextY + (i * RowSpacing);
                AriantArenaScoreEntry entry = _entries[i];
                if (entry.IconIndex >= 0 && entry.IconIndex < _rankIcons.Count)
                {
                    IDXObject icon = _rankIcons[entry.IconIndex];
                    icon.DrawBackground(
                        spriteBatch,
                        skeletonMeshRenderer,
                        gameTime,
                        ScoreLayerScreenX + IconX + icon.X,
                        ScoreLayerScreenY + iconY + icon.Y,
                        Color.White,
                        false,
                        null);
                }
                DrawOutlinedText(spriteBatch, font, entry.Name, new Vector2(ScoreLayerScreenX + NameX, ScoreLayerScreenY + textY), new Color(20, 20, 20), new Color(204, 236, 255));
                DrawOutlinedText(spriteBatch, font, FormatScoreText(entry.Score), new Vector2(ScoreLayerScreenX + ScoreX, ScoreLayerScreenY + textY), new Color(20, 20, 20), new Color(255, 222, 112));
            }
        }
        internal static string FormatScoreTextForTesting(int score)
        {
            return FormatScoreText(score);
        }
        internal static string ResolveResultLayerPathForTesting()
        {
            return ResolveResultLayerPath();
        }
        internal static string ResolveRankIconPathForTesting(int iconIndex)
        {
            return ResolveRankIconPath(iconIndex);
        }
        internal static string ResolveResultSoundPathForTesting()
        {
            return ResolveResultSoundPath();
        }
        internal string LastResultMessageForTesting => _lastResultMessage;
        private static string FormatScoreText(int score)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                ScoreTextStringPoolId,
                ScoreTextFallbackFormat,
                maxPlaceholderCount: 1,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, Math.Clamp(score, 0, MaxScore));
        }
        private static string ResolveRankIconPath(int iconIndex)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                RankIconStringPoolId,
                RankIconFallbackPathFormat,
                maxPlaceholderCount: 1,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, Math.Clamp(iconIndex, 0, MaxRankEntries - 1));
        }
        private void DrawResult(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, Texture2D pixelTexture, SpriteFont font)
        {
            if (_resultFrames.Count == 0)
            {
                return;
            }
            IDXObject frame = _resultFrames[Math.Clamp(_resultFrameIndex, 0, _resultFrames.Count - 1)];
            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            int anchorX = viewport.Width / 2;
            int anchorY = ResultOffsetY;
            frame.DrawBackground(
                spriteBatch,
                skeletonMeshRenderer,
                gameTime,
                anchorX + frame.X,
                anchorY + frame.Y,
                Color.White,
                false,
                null);
        }
        private int GetResultDuration()
        {
            int total = 0;
            for (int i = 0; i < _resultFrames.Count; i++)
            {
                total += _resultFrames[i].Delay > 0 ? _resultFrames[i].Delay : 100;
            }
            return total;
        }
        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _graphicsDevice == null)
            {
                return;
            }
            WzImageProperty resultRoot = ResolveWzPath(ResolveResultLayerPath());
            LoadAnimatedFrames(resultRoot, _resultFrames);
            EnsureResultSoundRegistered();
            for (int i = 0; i < MaxRankEntries; i++)
            {
                WzCanvasProperty canvas =
                    WzInfoTools.GetRealProperty(ResolveWzPath(ResolveRankIconPath(i))) as WzCanvasProperty
                    ?? WzInfoTools.GetRealProperty(ResolveWzPath(string.Format(
                        CultureInfo.InvariantCulture,
                        "UI/UIWindow2.img/AriantMatch/characterIcon/{0}",
                        i))) as WzCanvasProperty;
                if (canvas != null && TryCreateDxObject(canvas, out IDXObject icon))
                {
                    _rankIcons.Add(icon);
                }
            }
            _assetsLoaded = true;
        }
        private void EnsureResultSoundRegistered()
        {
            if (_soundManager == null || !string.IsNullOrWhiteSpace(_resultSoundKey))
            {
                return;
            }
            WzBinaryProperty sound = WzInfoTools.GetRealProperty(ResolveWzPath(ResolveResultSoundPath())) as WzBinaryProperty
                ?? WzInfoTools.GetRealProperty(global::HaCreator.Program.FindImage("Sound", "MiniGame.img")?["Show"]) as WzBinaryProperty;
            if (sound == null)
            {
                return;
            }
            _resultSoundKey = "AriantArena:Result";
            _soundManager.RegisterSound(_resultSoundKey, sound);
        }
        private static string ResolveResultLayerPath()
        {
            return MapleStoryStringPool.GetOrFallback(
                ResultLayerStringPoolId,
                ResultLayerFallbackPath);
        }
        private static string ResolveResultSoundPath()
        {
            return MapleStoryStringPool.GetOrFallback(
                ResultSoundStringPoolId,
                ResultSoundFallbackPath);
        }
        private static WzImageProperty ResolveWzPath(string wzPath)
        {
            if (string.IsNullOrWhiteSpace(wzPath))
            {
                return null;
            }

            string[] parts = wzPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return null;
            }

            WzImage image = global::HaCreator.Program.FindImage(parts[0], parts[1]);
            if (image == null || parts.Length == 2)
            {
                return null;
            }

            return image.GetFromPath(string.Join("/", parts, 2, parts.Length - 2));
        }
        private static IEnumerable<AriantArenaScoreUpdate> DecodeUserScorePacket(byte[] payload)
        {
            if (payload == null)
            {
                throw new InvalidDataException("Ariant score packet payload is missing.");
            }
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
            int count = reader.ReadByte();
            var updates = new List<AriantArenaScoreUpdate>(count);
            for (int i = 0; i < count; i++)
            {
                string userName = ReadMapleString(reader);
                int score = reader.ReadInt32();
                updates.Add(new AriantArenaScoreUpdate(userName, score));
            }
            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException($"Ariant score packet has {stream.Length - stream.Position} trailing byte(s).");
            }
            return updates;
        }
        private static string ReadMapleString(BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Ariant score packet ended before the player name was fully read.");
            }
            return Encoding.Default.GetString(bytes);
        }
        private bool TryApplyRemoteSpawnPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseEnterField(payload, out RemoteUserEnterFieldPacket spawn, out errorMessage))
            {
                return false;
            }

            CharacterBuild build = CreateRemoteBuildFromAvatarLook(
                spawn.Name,
                spawn.AvatarLook,
                out errorMessage);
            if (build == null)
            {
                return false;
            }

            UpsertRemoteParticipant(
                build,
                new Vector2(spawn.X, spawn.Y),
                spawn.FacingRight,
                spawn.ActionName,
                spawn.CharacterId);
            RemoteUserEnterFieldStateApplicator.TryApply(
                _remoteUserPool,
                spawn,
                Environment.TickCount,
                syncAnimationDisplayerRemoteUserState: null,
                out _);

            return true;
        }

        private bool TryApplyRemoteLeavePacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseLeaveField(payload, out RemoteUserLeaveFieldPacket leavePacket, out errorMessage))
            {
                return false;
            }

            if (!RemoveRemoteParticipant(leavePacket.CharacterId))
            {
                errorMessage = $"Remote Ariant actor id {leavePacket.CharacterId} does not exist.";
                return false;
            }

            return true;
        }

        private bool TryApplyRemoteMovePacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseMove(payload, currentTimeMs, out RemoteUserMovePacket move, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(move.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {move.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryApplyMoveSnapshot(
                move.CharacterId,
                move.Snapshot,
                move.MoveAction,
                currentTimeMs,
                out errorMessage);
        }

        private bool TryApplyRemoteChairPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParsePortableChair(payload, out RemoteUserPortableChairPacket chair, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(chair.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {chair.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TrySetPortableChair(chair.CharacterId, chair.ChairItemId, out errorMessage);
        }

        private bool TryApplyRemoteMeleeAttackPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseMeleeAttack(payload, out RemoteUserMeleeAttackPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryRegisterMeleeAfterImage(
                packet.CharacterId,
                packet.SkillId,
                packet.ActionName,
                packet.ActionCode,
                packet.MasteryPercent,
                packet.ChargeSkillId,
                packet.ActionSpeed,
                packet.PreparedSkillReleaseFollowUpValue,
                packet.MobHits,
                packet.FacingRight,
                currentTimeMs,
                out errorMessage);
        }

        private bool TryApplyRemotePreparedSkillPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParsePreparedSkill(payload, out RemoteUserPreparedSkillPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            PreparedSkillHudRules.PreparedSkillHudProfile hudProfile = PreparedSkillHudRules.ResolveProfile(packet.SkillId);
            bool resolvedIsKeydownSkill = PreparedSkillHudRules.ResolveKeyDownSkillState(
                packet.SkillId,
                packet.IsKeydownSkill);
            PreparedSkillHudRules.ResolveRemotePreparedSkillPhases(
                packet.SkillId,
                resolvedIsKeydownSkill,
                packet.IsHolding,
                packet.DurationMs,
                packet.MaxHoldDurationMs,
                packet.AutoEnterHold,
                out int activeDurationMs,
                out int prepareDurationMs,
                out bool autoEnterHold);
            return _remoteUserPool.TrySetPreparedSkill(
                packet.CharacterId,
                packet.SkillId,
                packet.SkillName,
                activeDurationMs,
                string.IsNullOrWhiteSpace(packet.SkinKey) ? hudProfile.SkinKey : packet.SkinKey,
                resolvedIsKeydownSkill,
                packet.IsHolding,
                PreparedSkillHudRules.ResolvePreparedGaugeDuration(
                    packet.SkillId,
                    packet.GaugeDurationMs,
                    packet.DurationMs,
                    resolvedIsKeydownSkill),
                Math.Max(0, packet.MaxHoldDurationMs),
                PreparedSkillHudRules.ResolveTextVariant(packet.SkillId),
                packet.ShowText && hudProfile.ShowText,
                currentTimeMs,
                out errorMessage,
                prepareDurationMs: prepareDurationMs,
                autoEnterHold: autoEnterHold);
        }

        private bool TryApplyRemotePreparedSkillClearPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParsePreparedSkillClear(payload, out RemoteUserPreparedSkillClearPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryClearPreparedSkill(packet.CharacterId, Environment.TickCount, out errorMessage);
        }

        private bool TryApplyRemoteMovingShootAttackPreparePacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseMovingShootAttackPrepare(payload, out RemoteUserMovingShootAttackPreparePacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryApplyMovingShootAttackPrepare(packet, currentTimeMs, out errorMessage);
        }

        private bool TryApplyRemoteActiveEffectItemPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseActiveEffectItem(payload, out RemoteUserActiveEffectItemPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryApplyActiveEffectItem(packet, Environment.TickCount, out errorMessage);
        }

        private bool TryApplyRemoteUpgradeTombPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseUpgradeTombEffect(payload, out RemoteUserUpgradeTombPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryApplyUpgradeTombEffect(packet, currentTimeMs, out errorMessage);
        }

        private bool TryApplyRemoteHitPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseHit(payload, out RemoteUserHitPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryApplyHit(packet, currentTimeMs, out errorMessage);
        }

        private bool TryApplyRemoteEmotionPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseEmotion(payload, out RemoteUserEmotionPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryApplyEmotion(packet, currentTimeMs, out errorMessage);
        }

        private bool TryApplyRemoteEffectPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseEffect(payload, out RemoteUserEffectPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryApplyEffect(packet, currentTimeMs, out errorMessage);
        }

        private bool TryApplyRemoteAvatarModifiedPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseAvatarModified(payload, out RemoteUserAvatarModifiedPacket avatarUpdate, out errorMessage))
            {
                return false;
            }

            return _remoteUserPool.TryApplyAvatarModified(
                avatarUpdate,
                currentTimeMs,
                out errorMessage);
        }

        private bool TryApplyRemoteTemporaryStatSetPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseTemporaryStatSet(payload, out RemoteUserTemporaryStatSetPacket packet, out errorMessage))
            {
                return false;
            }

            return _remoteUserPool.TryApplyTemporaryStatSet(packet, out errorMessage);
        }

        private bool TryApplyRemoteTemporaryStatResetPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseTemporaryStatReset(payload, out RemoteUserTemporaryStatResetPacket packet, out errorMessage))
            {
                return false;
            }

            return _remoteUserPool.TryApplyTemporaryStatReset(packet, out errorMessage);
        }

        private bool TryApplyRemoteReceiveHpPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseReceiveHp(payload, out RemoteUserReceiveHpPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryApplyReceiveHp(packet, out errorMessage);
        }

        private bool TryApplyRemoteGuildNameChangedPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseGuildNameChanged(payload, out RemoteUserGuildNameChangedPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryApplyProfileMetadata(
                packet.CharacterId,
                level: null,
                packet.GuildName,
                jobId: null,
                out errorMessage);
        }

        private bool TryApplyRemoteGuildMarkChangedPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseGuildMarkChanged(payload, out RemoteUserGuildMarkChangedPacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryApplyGuildMark(
                packet.CharacterId,
                packet.MarkBackgroundId,
                packet.MarkBackgroundColor,
                packet.MarkId,
                packet.MarkColor,
                out errorMessage);
        }

        private bool TryApplyRemoteThrowGrenadePacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!RemoteUserPacketCodec.TryParseThrowGrenade(payload, out RemoteUserThrowGrenadePacket packet, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteActor(packet.CharacterId, out _))
            {
                errorMessage = $"Remote Ariant actor id {packet.CharacterId} does not exist.";
                return false;
            }

            return _remoteUserPool.TryApplyThrowGrenade(packet, currentTimeMs, out errorMessage);
        }

        private CharacterBuild CreateRemoteBuildFromAvatarLook(string actorName, LoginAvatarLook avatarLook, out string errorMessage)
        {
            errorMessage = null;
            if (avatarLook == null)
            {
                errorMessage = "Remote Ariant AvatarLook payload is missing.";
                return null;
            }

            CharacterBuild build = _remoteBuildFactory?.Invoke(avatarLook, actorName);
            if (build == null)
            {
                errorMessage = "Remote Ariant AvatarLook payload could not be converted into a character build.";
                return null;
            }

            build.Name = string.IsNullOrWhiteSpace(actorName) ? build.Name : actorName.Trim();
            return build;
        }

        private static bool DecodeFacingRight(byte moveAction)
        {
            return (moveAction & 1) == 0;
        }

        private static int ResolveSyntheticRemoteParticipantId(string actorName)
        {
            string normalized = actorName?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return 0x40000001;
            }

            int hash = StringComparer.OrdinalIgnoreCase.GetHashCode($"{AriantRemoteSourceTag}:{normalized}");
            if (hash == int.MinValue)
            {
                hash = int.MaxValue;
            }

            return 0x40000000 | (Math.Abs(hash) & 0x3FFFFFFF);
        }

        private int CountAriantRemoteParticipants()
        {
            return _remoteUserPool?.Actors.Count(IsAriantRemoteActor) ?? 0;
        }

        private bool TryGetRemoteActorByName(string name, out RemoteUserActor actor)
        {
            actor = null;
            return !string.IsNullOrWhiteSpace(name)
                && _remoteUserPool != null
                && _remoteUserPool.TryGetActorByName(name.Trim(), out actor)
                && IsAriantRemoteActor(actor);
        }

        private bool TryGetRemoteActor(int characterId, out RemoteUserActor actor)
        {
            actor = null;
            return _remoteUserPool != null
                && _remoteUserPool.TryGetActor(characterId, out actor)
                && IsAriantRemoteActor(actor);
        }

        private static bool IsAriantRemoteActor(RemoteUserActor actor)
        {
            return actor != null
                && string.Equals(actor.SourceTag, AriantRemoteSourceTag, StringComparison.OrdinalIgnoreCase);
        }

        private void ReassignRankIconIndexes()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                int iconIndex = i < MaxRankEntries ? i : -1;
                if (_entries[i].IconIndex != iconIndex)
                {
                    _entries[i] = _entries[i] with { IconIndex = iconIndex };
                }
            }
        }
        private static string NormalizeRemoteActionName(string actionName)
        {
            string normalized = string.IsNullOrWhiteSpace(actionName)
                ? CharacterPart.GetActionString(CharacterAction.Stand1)
                : actionName.Trim();
            return normalized;
        }
        private void LoadAnimatedFrames(WzImageProperty source, List<IDXObject> target)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }
            WzImageProperty resolvedSource = WzInfoTools.GetRealProperty(source);
            if (resolvedSource is WzCanvasProperty canvas)
            {
                if (TryCreateDxObject(canvas, out IDXObject singleFrame))
                {
                    target.Add(singleFrame);
                }
                return;
            }
            if (resolvedSource is not WzSubProperty)
            {
                return;
            }
            for (int i = 0; ; i++)
            {
                if (WzInfoTools.GetRealProperty(resolvedSource[i.ToString()]) is not WzCanvasProperty frameCanvas)
                {
                    break;
                }
                if (TryCreateDxObject(frameCanvas, out IDXObject frame))
                {
                    target.Add(frame);
                }
            }
        }
        private bool TryCreateDxObject(WzCanvasProperty canvas, out IDXObject dxObject)
        {
            dxObject = null;
            if (_graphicsDevice == null || canvas == null)
            {
                return false;
            }
            using var bitmap = canvas.GetLinkedWzCanvasBitmap();
            if (bitmap == null)
            {
                return false;
            }
            Texture2D texture = bitmap.ToTexture2D(_graphicsDevice);
            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            int delay = canvas["delay"]?.GetInt() ?? 100;
            dxObject = new DXObject(-(int)origin.X, -(int)origin.Y, texture, delay);
            return true;
        }
        private static void DrawOutlinedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color shadowColor, Color textColor)
        {
            spriteBatch.DrawString(font, text, position + Vector2.One, shadowColor);
            spriteBatch.DrawString(font, text, position, textColor);
        }
    }
    public readonly record struct AriantArenaScoreEntry(string Name, int Score, int IconIndex = -1);
    public readonly record struct AriantArenaScoreUpdate(string UserName, int Score);
    public readonly record struct AriantArenaRemoteParticipantSnapshot(string Name, Vector2 Position, bool FacingRight, string ActionName);
    #endregion
}
