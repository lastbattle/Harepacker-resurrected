using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
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
        private sealed class RemoteParticipant
        {
            public int? CharacterId { get; init; }
            public string Name { get; init; }
            public CharacterBuild Build { get; init; }
            public CharacterAssembler Assembler { get; init; }
            public Vector2 Position { get; set; }
            public bool FacingRight { get; set; } = true;
            public string ActionName { get; set; } = CharacterPart.GetActionString(CharacterAction.Stand1);
        }
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
        private readonly Dictionary<string, RemoteParticipant> _remoteParticipants = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _remoteParticipantNamesById = new();
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
        private SoundManager _soundManager;
        private Func<LoginAvatarLook, string, CharacterBuild> _remoteBuildFactory;
        private string _resultSoundKey;
        private string _lastResultMessage;
        private string _localPlayerName;
        private int? _lastPacketType;
        public bool IsActive => _isActive;
        public IReadOnlyList<AriantArenaScoreEntry> Entries => _entries;
        public int ScoreRefreshSerial => _scoreRefreshSerial;
        public int RemoteParticipantCount => _remoteParticipants.Count;
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
            _remoteParticipants.Clear();
            _remoteParticipantNamesById.Clear();
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
                        return TryApplyRemoteMovePacket(payload, out errorMessage);
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

            string normalizedName = build.Name.Trim();
            if (_remoteParticipants.TryGetValue(normalizedName, out RemoteParticipant existingParticipant)
                && existingParticipant.CharacterId.HasValue
                && (!characterId.HasValue || existingParticipant.CharacterId.Value != characterId.Value))
            {
                _remoteParticipantNamesById.Remove(existingParticipant.CharacterId.Value);
            }

            if (characterId.HasValue
                && _remoteParticipantNamesById.TryGetValue(characterId.Value, out string previousName)
                && !string.Equals(previousName, normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                _remoteParticipants.Remove(previousName);
            }

            _remoteParticipants[normalizedName] = new RemoteParticipant
            {
                CharacterId = characterId,
                Name = normalizedName,
                Build = build,
                Assembler = new CharacterAssembler(build),
                Position = position,
                FacingRight = facingRight,
                ActionName = NormalizeRemoteActionName(actionName)
            };

            if (characterId.HasValue)
            {
                _remoteParticipantNamesById[characterId.Value] = normalizedName;
            }
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
            if (!_remoteParticipants.TryGetValue(normalizedName, out RemoteParticipant participant))
            {
                message = $"Remote Ariant actor '{normalizedName}' does not exist.";
                return false;
            }
            participant.Position = position;
            if (facingRight.HasValue)
            {
                participant.FacingRight = facingRight.Value;
            }
            if (!string.IsNullOrWhiteSpace(actionName))
            {
                participant.ActionName = NormalizeRemoteActionName(actionName);
            }
            return true;
        }

        public bool TryMoveRemoteParticipant(int characterId, Vector2 position, bool? facingRight, string actionName, out string message)
        {
            message = null;
            if (!_remoteParticipantNamesById.TryGetValue(characterId, out string participantName))
            {
                message = $"Remote Ariant actor id {characterId} does not exist.";
                return false;
            }

            return TryMoveRemoteParticipant(participantName, position, facingRight, actionName, out message);
        }

        public bool RemoveRemoteParticipant(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string normalizedName = name.Trim();
            if (!_remoteParticipants.TryGetValue(normalizedName, out RemoteParticipant participant))
            {
                return false;
            }

            if (participant.CharacterId.HasValue)
            {
                _remoteParticipantNamesById.Remove(participant.CharacterId.Value);
            }

            return _remoteParticipants.Remove(normalizedName);
        }

        public bool RemoveRemoteParticipant(int characterId)
        {
            return _remoteParticipantNamesById.TryGetValue(characterId, out string participantName)
                && RemoveRemoteParticipant(participantName);
        }

        public void ClearRemoteParticipants()
        {
            _remoteParticipants.Clear();
            _remoteParticipantNamesById.Clear();
        }

        public bool TryGetRemoteParticipant(string name, out AriantArenaRemoteParticipantSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(name)
                && _remoteParticipants.TryGetValue(name.Trim(), out RemoteParticipant participant))
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
            if (_remoteParticipantNamesById.TryGetValue(characterId, out string participantName))
            {
                return TryGetRemoteParticipant(participantName, out snapshot);
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
            DrawRemoteParticipants(spriteBatch, skeletonMeshRenderer, mapShiftX, mapShiftY, centerX, centerY, tickCount, font);
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
            _remoteParticipants.Clear();
            _remoteParticipantNamesById.Clear();
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
            return $"Ariant Arena active, {_entries.Count} score row(s), remoteActors={_remoteParticipants.Count}, result={(_showResult ? "showing" : "idle")}, refresh={_scoreRefreshSerial}, lastPacket={(_lastPacketType?.ToString() ?? "None")}, {leaderText}";
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
            if (_remoteParticipants.Count == 0)
            {
                return;
            }
            foreach (RemoteParticipant participant in _remoteParticipants.Values
                .OrderBy(entry => entry.Position.Y)
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
            if (!TryDecodeRemoteSpawnPacket(payload, out AriantArenaRemoteSpawnPacket spawn, out errorMessage))
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
                spawn.Position,
                facingRight: true,
                ResolveRemoteActionName(spawn.MoveAction, spawn.PortableChairItemId),
                spawn.CharacterId);
            return true;
        }

        private bool TryApplyRemoteLeavePacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!TryDecodeRemoteCharacterIdPacket(payload, PacketTypeUserLeaveField, out int characterId, out errorMessage))
            {
                return false;
            }

            if (!RemoveRemoteParticipant(characterId))
            {
                errorMessage = $"Remote Ariant actor id {characterId} does not exist.";
                return false;
            }

            return true;
        }

        private bool TryApplyRemoteMovePacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!TryDecodeRemoteMovePacket(payload, out AriantArenaRemoteMovePacket move, out errorMessage))
            {
                return false;
            }

            return TryMoveRemoteParticipant(
                move.CharacterId,
                move.Position,
                facingRight: null,
                ResolveRemoteActionName(move.MoveAction, portableChairItemId: 0),
                out errorMessage);
        }

        private bool TryApplyRemoteChairPacket(byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!TryDecodeRemoteChairPacket(payload, out AriantArenaRemoteChairPacket chair, out errorMessage))
            {
                return false;
            }

            if (!TryGetRemoteParticipant(chair.CharacterId, out AriantArenaRemoteParticipantSnapshot snapshot))
            {
                errorMessage = $"Remote Ariant actor id {chair.CharacterId} does not exist.";
                return false;
            }

            string actionName = chair.PortableChairItemId > 0
                ? CharacterPart.GetActionString(CharacterAction.Sit)
                : CharacterPart.GetActionString(CharacterAction.Stand1);
            return TryMoveRemoteParticipant(
                chair.CharacterId,
                snapshot.Position,
                facingRight: null,
                actionName,
                out errorMessage);
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

            if (!_remoteParticipantNamesById.TryGetValue(avatarUpdate.CharacterId, out string participantName)
                || !_remoteParticipants.TryGetValue(participantName, out RemoteParticipant participant))
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

            UpsertRemoteParticipant(
                build,
                participant.Position,
                participant.FacingRight,
                participant.ActionName,
                avatarUpdate.CharacterId);
            return true;
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

        private static bool TryDecodeRemoteSpawnPacket(byte[] payload, out AriantArenaRemoteSpawnPacket packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;
            if (!TryCreatePacketReader(payload, PacketTypeUserEnterField, out PacketReader reader, out errorMessage))
            {
                return false;
            }

            try
            {
                int characterId = reader.ReadInt();
                reader.ReadByte();
                string name = reader.ReadMapleString();
                reader.ReadMapleString();
                reader.Skip(6);
                if (!TrySkipEmptyRemoteSecondaryStat(reader, out errorMessage))
                {
                    return false;
                }

                reader.ReadShort();
                if (!LoginAvatarLookCodec.TryDecode(reader, out LoginAvatarLook avatarLook, out string avatarError))
                {
                    errorMessage = avatarError ?? "Remote Ariant AvatarLook payload could not be decoded.";
                    return false;
                }

                reader.ReadInt();
                reader.ReadInt();
                reader.ReadInt();
                reader.ReadInt();
                reader.ReadInt();
                int portableChairItemId = reader.ReadInt();
                short x = reader.ReadShort();
                short y = reader.ReadShort();
                byte moveAction = reader.ReadByte();

                packet = new AriantArenaRemoteSpawnPacket(
                    characterId,
                    string.IsNullOrWhiteSpace(name) ? $"Remote{characterId}" : name.Trim(),
                    avatarLook,
                    new Vector2(x, y),
                    moveAction,
                    portableChairItemId);
                return true;
            }
            catch (EndOfStreamException)
            {
                errorMessage = "Ariant remote spawn packet ended before decoding completed.";
                return false;
            }
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
                errorMessage = "Ariant remote actor packet ended before the character id was fully read.";
                return false;
            }
        }

        private static bool TryDecodeRemoteMovePacket(byte[] payload, out AriantArenaRemoteMovePacket packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;
            if (!TryCreatePacketReader(payload, PacketTypeUserMove, out PacketReader reader, out errorMessage))
            {
                return false;
            }

            try
            {
                int characterId = reader.ReadInt();
                short x = reader.ReadShort();
                short y = reader.ReadShort();
                reader.ReadShort();
                reader.ReadShort();
                int movementCount = reader.ReadByte();
                byte moveAction = 0;
                Vector2 position = new Vector2(x, y);

                for (int i = 0; i < movementCount; i++)
                {
                    byte movementType = reader.ReadByte();
                    short nextX = x;
                    short nextY = y;

                    switch (movementType)
                    {
                        case 0:
                        case 5:
                        case 12:
                        case 14:
                        case 35:
                        case 36:
                            nextX = reader.ReadShort();
                            nextY = reader.ReadShort();
                            reader.Skip(6);
                            if (movementType == 12)
                            {
                                reader.ReadShort();
                            }
                            reader.Skip(4);
                            break;

                        case 1:
                        case 2:
                        case 13:
                        case 16:
                        case 18:
                        case 31:
                        case 32:
                        case 33:
                        case 34:
                            reader.Skip(4);
                            break;

                        case 3:
                        case 4:
                        case 6:
                        case 7:
                        case 8:
                        case 10:
                            nextX = reader.ReadShort();
                            nextY = reader.ReadShort();
                            reader.Skip(2);
                            break;

                        case 9:
                            reader.ReadByte();
                            break;

                        case 11:
                            reader.Skip(6);
                            break;

                        case 17:
                            nextX = reader.ReadShort();
                            nextY = reader.ReadShort();
                            reader.Skip(4);
                            break;

                        case 20:
                        case 21:
                        case 22:
                        case 23:
                        case 24:
                        case 25:
                        case 26:
                        case 27:
                        case 28:
                        case 29:
                        case 30:
                            break;

                        default:
                            errorMessage = $"Unsupported Ariant remote move fragment type: {movementType}";
                            return false;
                    }

                    moveAction = reader.ReadByte();
                    reader.ReadShort();
                    x = nextX;
                    y = nextY;
                    position = new Vector2(nextX, nextY);
                }

                packet = new AriantArenaRemoteMovePacket(characterId, position, moveAction);
                return true;
            }
            catch (EndOfStreamException)
            {
                errorMessage = "Ariant remote move packet ended before decoding completed.";
                return false;
            }
        }

        private static bool TryDecodeRemoteChairPacket(byte[] payload, out AriantArenaRemoteChairPacket packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;
            if (!TryCreatePacketReader(payload, PacketTypeSetActivePortableChair, out PacketReader reader, out errorMessage))
            {
                return false;
            }

            try
            {
                int characterId = reader.ReadInt();
                int portableChairItemId = reader.ReadInt();
                packet = new AriantArenaRemoteChairPacket(characterId, portableChairItemId);
                return true;
            }
            catch (EndOfStreamException)
            {
                errorMessage = "Ariant remote chair packet ended before decoding completed.";
                return false;
            }
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

        private static bool TrySkipEmptyRemoteSecondaryStat(PacketReader reader, out string errorMessage)
        {
            errorMessage = null;
            byte[] remoteSecondaryStatMask = reader.ReadBytes(16);
            if (remoteSecondaryStatMask.Length != 16)
            {
                errorMessage = "Ariant remote spawn packet ended before the 16-byte remote secondary-stat mask was fully read.";
                return false;
            }

            if (remoteSecondaryStatMask.Any(value => value != 0))
            {
                errorMessage = "Ariant remote spawn packets with non-empty remote secondary-stat masks are not decoded yet.";
                return false;
            }

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
    internal readonly record struct AriantArenaRemoteSpawnPacket(int CharacterId, string Name, LoginAvatarLook AvatarLook, Vector2 Position, byte MoveAction, int PortableChairItemId);
    internal readonly record struct AriantArenaRemoteMovePacket(int CharacterId, Vector2 Position, byte MoveAction);
    internal readonly record struct AriantArenaRemoteChairPacket(int CharacterId, int PortableChairItemId);
    internal readonly record struct AriantArenaRemoteAvatarModifiedPacket(int CharacterId, LoginAvatarLook AvatarLook);
    #endregion
}
