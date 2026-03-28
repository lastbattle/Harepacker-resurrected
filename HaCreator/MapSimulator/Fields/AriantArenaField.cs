using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Character;
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
        private const int PacketTypeSetActivePortableChair = 222;
        private const int PacketTypeAvatarModified = 223;
        private const int PacketTypeShowResult = 171;
        private const int PacketTypeUserScore = 354;
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
                    case PacketTypeSetActivePortableChair:
                        return TryApplyRemoteChairPacket(payload, out errorMessage);
                    case PacketTypeAvatarModified:
                        return TryApplyRemoteAvatarModifiedPacket(payload, out errorMessage);
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
                if (update.Score < 0 || ShouldSuppressLocalRankEntry(normalizedName))
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
                    _entries.Add(new AriantArenaScoreEntry(normalizedName, clampedScore, GetNextIconIndex()));
                    changed = true;
                }
            }
            if (!changed)
            {
                return;
            }
            _entries.Sort(static (left, right) =>
            {
                int scoreCompare = right.Score.CompareTo(left.Score);
                return scoreCompare != 0
                    ? scoreCompare
                    : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });
            _scoreRefreshSerial++;
            _showScoreboard = _entries.Count > 0;
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
            _lastResultMessage = _entries.Count > 0
                ? $"{_entries[0].Name} wins Ariant Arena with {_entries[0].Score} point{(_entries[0].Score == 1 ? string.Empty : "s")}."
                : "Ariant Arena result shown.";
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
                        IconX + icon.X,
                        iconY + icon.Y,
                        Color.White,
                        false,
                        null);
                }
                DrawOutlinedText(spriteBatch, font, entry.Name, new Vector2(NameX, textY), new Color(20, 20, 20), new Color(204, 236, 255));
                DrawOutlinedText(spriteBatch, font, entry.Score.ToString(), new Vector2(ScoreX, textY), new Color(20, 20, 20), new Color(255, 222, 112));
            }
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
            if (font != null && !string.IsNullOrWhiteSpace(_lastResultMessage))
            {
                Vector2 textSize = font.MeasureString(_lastResultMessage);
                float textX = (viewport.Width - textSize.X) * 0.5f;
                float textY = Math.Max(anchorY + 236, 220);
                if (pixelTexture != null)
                {
                    Rectangle backdrop = new Rectangle((int)textX - 10, (int)textY - 6, (int)textSize.X + 20, (int)textSize.Y + 12);
                    spriteBatch.Draw(pixelTexture, backdrop, new Color(0, 0, 0, 120));
                }
                DrawOutlinedText(spriteBatch, font, _lastResultMessage, new Vector2(textX, textY), Color.Black, Color.White);
            }
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
            WzImage uiWindow = global::HaCreator.Program.FindImage("UI", "UIWindow.img")
                ?? global::HaCreator.Program.FindImage("UI", "UIWindow2.img");
            WzImageProperty ariantMatch = uiWindow?["AriantMatch"];
            LoadAnimatedFrames(ariantMatch?["Result"], _resultFrames);
            EnsureResultSoundRegistered();
            WzImageProperty iconRoot = ariantMatch?["characterIcon"];
            for (int i = 0; i < MaxRankEntries; i++)
            {
                if (WzInfoTools.GetRealProperty(iconRoot?[i.ToString()]) is WzCanvasProperty canvas
                    && TryCreateDxObject(canvas, out IDXObject icon))
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
            WzBinaryProperty sound =
                WzInfoTools.GetRealProperty(global::HaCreator.Program.FindImage("Sound", "MiniGame.img")?["Show"]) as WzBinaryProperty
                ?? WzInfoTools.GetRealProperty(global::HaCreator.Program.FindImage("Sound", "MiniGame.img")?["Win"]) as WzBinaryProperty
                ?? FindBestAriantResultSound(
                    global::HaCreator.Program.FindImage("Sound", "MiniGame.img"),
                    global::HaCreator.Program.FindImage("Sound", "Game.img"));
            if (sound == null)
            {
                return;
            }
            _resultSoundKey = "AriantArena:Result";
            _soundManager.RegisterSound(_resultSoundKey, sound);
        }
        private static WzBinaryProperty FindBestAriantResultSound(params WzImage[] sources)
        {
            WzBinaryProperty best = null;
            int bestScore = 0;
            foreach (WzImage source in sources)
            {
                if (source?.WzProperties == null)
                {
                    continue;
                }
                foreach (WzImageProperty child in source.WzProperties)
                {
                    FindBestAriantResultSoundRecursive(child, child?.Name ?? string.Empty, ref best, ref bestScore);
                }
            }
            return best;
        }
        private static void FindBestAriantResultSoundRecursive(
            WzImageProperty property,
            string path,
            ref WzBinaryProperty best,
            ref int bestScore)
        {
            if (property == null)
            {
                return;
            }
            WzImageProperty resolved = WzInfoTools.GetRealProperty(property);
            string currentPath = string.IsNullOrWhiteSpace(path)
                ? property.Name ?? string.Empty
                : path;
            string lowerPath = currentPath.ToLowerInvariant();
            if (resolved is WzBinaryProperty binary)
            {
                int score = 0;
                if (lowerPath.Contains("ariant"))
                {
                    score += 8;
                }
                if (lowerPath.Contains("result") || lowerPath.Contains("clear"))
                {
                    score += 4;
                }
                if (lowerPath.Contains("win"))
                {
                    score += 2;
                }
                if (score > bestScore)
                {
                    best = binary;
                    bestScore = score;
                }
                return;
            }
            if (resolved?.WzProperties == null)
            {
                return;
            }
            foreach (WzImageProperty child in resolved.WzProperties)
            {
                string childPath = string.IsNullOrWhiteSpace(currentPath)
                    ? child?.Name ?? string.Empty
                    : $"{currentPath}/{child?.Name}";
                FindBestAriantResultSoundRecursive(child, childPath, ref best, ref bestScore);
            }
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
            if (spawn.PortableChairItemId.HasValue)
            {
                _remoteUserPool?.TrySetPortableChair(spawn.CharacterId, spawn.PortableChairItemId, out _);
            }

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

        private bool TryApplyRemoteAvatarModifiedPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!TryDecodeRemoteAvatarModifiedPacket(payload, out AriantArenaRemoteAvatarModifiedPacket avatarUpdate, out errorMessage))
            {
                return false;
            }

            if (avatarUpdate.AvatarLook == null)
            {
                return true;
            }

            if (!TryGetRemoteActor(avatarUpdate.CharacterId, out RemoteUserActor participant))
            {
                errorMessage = $"Remote Ariant actor id {avatarUpdate.CharacterId} does not exist.";
                return false;
            }

            CharacterBuild build = CreateRemoteBuildFromAvatarLook(
                participant.Name,
                avatarUpdate.AvatarLook,
                out errorMessage);
            if (build == null)
            {
                return false;
            }

            return _remoteUserPool.TryAddOrUpdate(
                avatarUpdate.CharacterId,
                build,
                participant.Position,
                out errorMessage,
                participant.FacingRight,
                participant.ActionName,
                AriantRemoteSourceTag,
                isVisibleInWorld: true);
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

        private static bool TryDecodeRemoteAvatarModifiedPacket(byte[] payload, out AriantArenaRemoteAvatarModifiedPacket packet, out string errorMessage)
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
                        errorMessage = avatarError ?? "Remote Ariant avatar-modified packet could not decode AvatarLook.";
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

                packet = new AriantArenaRemoteAvatarModifiedPacket(characterId, avatarLook);
                return true;
            }
            catch (EndOfStreamException)
            {
                errorMessage = "Ariant remote avatar-modified packet ended before decoding completed.";
                return false;
            }
        }

        private static bool TryCreatePacketReader(byte[] payload, int packetType, out PacketReader reader, out string errorMessage)
        {
            reader = null;
            errorMessage = null;
            if (payload == null || payload.Length == 0)
            {
                errorMessage = $"Ariant packet {packetType} payload is missing.";
                return false;
            }

            reader = new PacketReader(payload);
            return true;
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

        private int GetNextIconIndex()
        {
            if (MaxRankEntries <= 0)
            {
                return -1;
            }
            return Math.Clamp(_entries.Count, 0, MaxRankEntries - 1);
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
    internal readonly record struct AriantArenaRemoteAvatarModifiedPacket(int CharacterId, LoginAvatarLook AvatarLook);
    #endregion
}
