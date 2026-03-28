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
        private string _resultSoundKey;
        private string _lastResultMessage;
        private string _localPlayerName;
        private int? _lastPacketType;
        public bool IsActive => _isActive;
        public IReadOnlyList<AriantArenaScoreEntry> Entries => _entries;
        public int ScoreRefreshSerial => _scoreRefreshSerial;
        public int RemoteParticipantCount => _remoteParticipants.Count;
        public void Initialize(GraphicsDevice graphicsDevice, SoundManager soundManager = null)
        {
            _graphicsDevice = graphicsDevice;
            _soundManager = soundManager;
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
            if (characterId.HasValue
                && _remoteParticipantNamesById.TryGetValue(characterId.Value, out string previousName)
                && !string.Equals(previousName, normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                _remoteParticipants.Remove(previousName);
            }
            _remoteParticipants[normalizedName] = new RemoteParticipant
            {
                Name = normalizedName,
                Build = build,
                Assembler = new CharacterAssembler(build),
                Position = position,
                FacingRight = facingRight,
                ActionName = NormalizeRemoteActionName(actionName)
            };
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
        public bool RemoveRemoteParticipant(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && _remoteParticipants.Remove(name.Trim());
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
    #endregion
}
