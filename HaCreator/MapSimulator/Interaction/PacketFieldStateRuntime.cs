using MapleLib.PacketLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketFieldStateRuntime
    {
        private const int HelpMessageDisplayDurationMs = 6000;
        private const int QuestTimerWidth = 220;
        private const int QuestTimerHeight = 42;
        private const int QuestTimerSpacing = 6;

        private readonly List<string> _mapHelpMessages = new();
        private readonly Dictionary<int, PacketQuestTimerEntry> _questTimers = new();
        private Texture2D _pixelTexture;
        private int _boundMapId = int.MinValue;
        private string _boundMapName = string.Empty;
        private PacketHelpMessageState _activeHelpMessage;
        private string _lastFieldSpecificDataSummary = "No field-specific data payload received.";
        private string _statusMessage = "Packet-owned field state idle.";

        internal void Initialize(GraphicsDevice graphicsDevice, MapInfo mapInfo)
        {
            if (_pixelTexture == null && graphicsDevice != null)
            {
                _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }

            BindMap(mapInfo);
        }

        internal void BindMap(MapInfo mapInfo)
        {
            int mapId = mapInfo?.id ?? 0;
            if (_boundMapId == mapId)
            {
                return;
            }

            _boundMapId = mapId;
            _boundMapName = mapInfo?.strMapName ?? mapInfo?.streetName ?? string.Empty;
            _mapHelpMessages.Clear();
            _mapHelpMessages.AddRange(BuildHelpMessages(mapInfo));
            _questTimers.Clear();
            _activeHelpMessage = null;
            _lastFieldSpecificDataSummary = "No field-specific data payload received.";
            _statusMessage = _mapHelpMessages.Count > 0
                ? $"Loaded {_mapHelpMessages.Count} help message entr{(_mapHelpMessages.Count == 1 ? "y" : "ies")} for map {mapId}."
                : $"Packet-owned field state bound to map {mapId}.";
        }

        internal void Clear()
        {
            _questTimers.Clear();
            _activeHelpMessage = null;
            _mapHelpMessages.Clear();
            _lastFieldSpecificDataSummary = "No field-specific data payload received.";
            _statusMessage = "Packet-owned field state cleared.";
            _boundMapId = int.MinValue;
            _boundMapName = string.Empty;
        }

        internal void Update(int currentTick)
        {
            if (_activeHelpMessage != null && currentTick >= _activeHelpMessage.ExpiresAtTick)
            {
                _activeHelpMessage = null;
            }

            if (_questTimers.Count == 0)
            {
                return;
            }

            List<int> expiredQuestIds = null;
            foreach ((int questId, PacketQuestTimerEntry timer) in _questTimers)
            {
                if (currentTick < timer.ExpireTick)
                {
                    continue;
                }

                expiredQuestIds ??= new List<int>();
                expiredQuestIds.Add(questId);
            }

            if (expiredQuestIds == null)
            {
                return;
            }

            foreach (int questId in expiredQuestIds)
            {
                _questTimers.Remove(questId);
            }

            _statusMessage = expiredQuestIds.Count == 1
                ? $"Quest timer expired for {ResolveQuestName(expiredQuestIds[0])}."
                : $"{expiredQuestIds.Count} packet-authored quest timers expired.";
        }

        internal bool TryApplyPacket(
            int packetType,
            byte[] payload,
            int currentTick,
            Func<string, bool?, int, int?, bool> setDynamicObjectTagState,
            Func<byte[], int, string> fieldSpecificDataHandler,
            out string message)
        {
            payload ??= Array.Empty<byte>();
            switch (packetType)
            {
                case 149:
                    return TryApplyFieldSpecificData(payload, currentTick, fieldSpecificDataHandler, out message);

                case 162:
                    return TryApplyDesc(payload, currentTick, out message);

                case 166:
                    _questTimers.Clear();
                    _statusMessage = "Cleared all packet-authored quest timers.";
                    message = _statusMessage;
                    return true;

                case 167:
                    return TryApplyQuestTime(payload, currentTick, out message);

                case 169:
                    return TryApplyObjectState(payload, currentTick, setDynamicObjectTagState, out message);

                default:
                    message = $"Unsupported field packet type {packetType}.";
                    return false;
            }
        }

        internal bool TryGetQuestTimerText(int questId, int currentTick, out string timerText)
        {
            timerText = string.Empty;
            if (!_questTimers.TryGetValue(questId, out PacketQuestTimerEntry timer))
            {
                return false;
            }

            int remainingMs = Math.Max(0, timer.ExpireTick - currentTick);
            timerText = $"Time left: {FormatRemainingTime(remainingMs)}";
            return true;
        }

        internal string DescribeStatus(int currentTick)
        {
            string helpStatus = _activeHelpMessage == null
                ? "help=idle"
                : $"help=\"{TrimForStatus(_activeHelpMessage.Text)}\"";

            string timerStatus = _questTimers.Count == 0
                ? "timers=none"
                : $"timers={string.Join(", ", _questTimers.Values.OrderBy(static entry => entry.ExpireTick).Take(3).Select(timer => $"{timer.QuestName} {FormatRemainingTime(Math.Max(0, timer.ExpireTick - currentTick))}"))}";

            return $"{_statusMessage} {helpStatus}; {timerStatus}; fieldspecific={_lastFieldSpecificDataSummary}";
        }

        internal void Draw(SpriteBatch spriteBatch, SpriteFont font, int renderWidth, int renderHeight, int currentTick)
        {
            if (spriteBatch == null || font == null || _pixelTexture == null)
            {
                return;
            }

            DrawQuestTimers(spriteBatch, font, renderWidth, currentTick);
            DrawHelpMessage(spriteBatch, font, renderWidth, renderHeight);
        }

        private bool TryApplyDesc(byte[] payload, int currentTick, out string message)
        {
            if (payload == null || payload.Length < 1)
            {
                message = "Field help packet payload is empty.";
                return false;
            }

            byte index = payload[0];
            if (index >= _mapHelpMessages.Count)
            {
                message = $"Field help message index {index} is not available for map {_boundMapId}.";
                return false;
            }

            string text = _mapHelpMessages[index];
            _activeHelpMessage = new PacketHelpMessageState(text, currentTick + HelpMessageDisplayDurationMs, index);
            _statusMessage = $"Displayed field help message #{index} on map {_boundMapId}.";
            message = $"{_statusMessage} {TrimForStatus(text)}";
            return true;
        }

        private bool TryApplyQuestTime(byte[] payload, int currentTick, out string message)
        {
            if (payload == null || payload.Length < 1)
            {
                message = "Quest timer packet payload is empty.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream);
                byte count = reader.ReadByte();
                DateTime utcNow = DateTime.UtcNow;
                int appliedCount = 0;

                for (int i = 0; i < count; i++)
                {
                    int questId = reader.ReadInt32();
                    long startFileTime = reader.ReadInt64();
                    long endFileTime = reader.ReadInt64();

                    DateTime startUtc = DateTime.FromFileTimeUtc(startFileTime);
                    DateTime endUtc = DateTime.FromFileTimeUtc(endFileTime);
                    int remainingMs = Math.Max(0, (int)Math.Min(int.MaxValue, (endUtc - utcNow).TotalMilliseconds));
                    int durationMs = Math.Max(remainingMs, (int)Math.Min(int.MaxValue, Math.Max(0d, (endUtc - startUtc).TotalMilliseconds)));

                    (string questName, string timerUiKey) = ResolveQuestMetadata(questId);
                    _questTimers[questId] = new PacketQuestTimerEntry(
                        questId,
                        questName,
                        timerUiKey,
                        startUtc,
                        endUtc,
                        currentTick,
                        unchecked(currentTick + remainingMs),
                        durationMs);
                    appliedCount++;
                }

                if (stream.Position != stream.Length)
                {
                    message = $"Quest timer packet has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                _statusMessage = appliedCount == 1
                    ? $"Applied 1 packet-authored quest timer."
                    : $"Applied {appliedCount} packet-authored quest timers.";
                message = _statusMessage;
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentOutOfRangeException || ex is ArgumentException)
            {
                message = $"Quest timer packet could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyObjectState(
            byte[] payload,
            int currentTick,
            Func<string, bool?, int, int?, bool> setDynamicObjectTagState,
            out string message)
        {
            if (payload == null || payload.Length == 0)
            {
                message = "Object-state packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                string tag = reader.ReadMapleString();
                int stateValue = reader.ReadInt();
                bool isEnabled = stateValue != 0;

                if (string.IsNullOrWhiteSpace(tag))
                {
                    message = "Object-state packet did not contain a tag name.";
                    return false;
                }

                bool applied = setDynamicObjectTagState?.Invoke(tag, isEnabled, 0, currentTick) == true;
                message = applied
                    ? $"Applied object-state packet for '{tag}' => {(isEnabled ? "on" : "off")}."
                    : $"Object-state packet for '{tag}' was ignored because no matching tagged object state is available.";
                _statusMessage = message;
                return applied;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException)
            {
                message = $"Object-state packet could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyFieldSpecificData(
            byte[] payload,
            int currentTick,
            Func<byte[], int, string> fieldSpecificDataHandler,
            out string message)
        {
            string payloadSummary = payload == null || payload.Length == 0
                ? "empty"
                : $"{payload.Length} byte(s), hex={BitConverter.ToString(payload, 0, Math.Min(payload.Length, 24)).Replace("-", string.Empty, StringComparison.Ordinal)}";

            string handoffSummary = fieldSpecificDataHandler?.Invoke(payload ?? Array.Empty<byte>(), currentTick);
            _lastFieldSpecificDataSummary = string.IsNullOrWhiteSpace(handoffSummary)
                ? payloadSummary
                : $"{payloadSummary}, {handoffSummary}";
            _statusMessage = "Captured packet-authored field-specific data.";
            message = _lastFieldSpecificDataSummary;
            return true;
        }

        private void DrawQuestTimers(SpriteBatch spriteBatch, SpriteFont font, int renderWidth, int currentTick)
        {
            if (_questTimers.Count == 0)
            {
                return;
            }

            List<PacketQuestTimerEntry> timers = _questTimers.Values
                .OrderBy(static timer => timer.ExpireTick)
                .Take(3)
                .ToList();

            int totalHeight = (timers.Count * QuestTimerHeight) + ((timers.Count - 1) * QuestTimerSpacing);
            int y = 10;
            int x = (renderWidth - QuestTimerWidth) / 2;

            for (int i = 0; i < timers.Count; i++)
            {
                PacketQuestTimerEntry timer = timers[i];
                Rectangle bounds = new(x, y + (i * (QuestTimerHeight + QuestTimerSpacing)), QuestTimerWidth, QuestTimerHeight);
                Color frameColor = ResolveTimerFrameColor(timer.TimerUiKey);
                spriteBatch.Draw(_pixelTexture, bounds, new Color(17, 23, 35, 220));
                DrawBorder(spriteBatch, bounds, frameColor);

                string title = timer.QuestName;
                string remain = FormatRemainingTime(Math.Max(0, timer.ExpireTick - currentTick));
                spriteBatch.DrawString(font, title, new Vector2(bounds.X + 8, bounds.Y + 6), Color.White, 0f, Vector2.Zero, 0.48f, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, remain, new Vector2(bounds.Right - 66, bounds.Y + 18), frameColor, 0f, Vector2.Zero, 0.54f, SpriteEffects.None, 0f);
            }
        }

        private void DrawHelpMessage(SpriteBatch spriteBatch, SpriteFont font, int renderWidth, int renderHeight)
        {
            if (_activeHelpMessage == null || string.IsNullOrWhiteSpace(_activeHelpMessage.Text))
            {
                return;
            }

            const float scale = 0.58f;
            const int padding = 14;
            string[] lines = WrapText(font, _activeHelpMessage.Text, 340f, scale).ToArray();
            if (lines.Length == 0)
            {
                return;
            }

            float maxWidth = lines.Max(line => font.MeasureString(line).X * scale);
            int width = (int)Math.Ceiling(maxWidth) + (padding * 2);
            int height = (int)Math.Ceiling((lines.Length * font.LineSpacing * scale)) + (padding * 2) + 10;
            Rectangle bounds = new((renderWidth - width) / 2, Math.Max(26, (renderHeight / 2) - (height / 2)), width, height);

            spriteBatch.Draw(_pixelTexture, bounds, new Color(22, 25, 34, 232));
            DrawBorder(spriteBatch, bounds, new Color(234, 205, 118));

            Vector2 textPos = new(bounds.X + padding, bounds.Y + padding + 2);
            for (int i = 0; i < lines.Length; i++)
            {
                spriteBatch.DrawString(font, lines[i], textPos, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                textPos.Y += font.LineSpacing * scale;
            }
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        private static IEnumerable<string> BuildHelpMessages(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return Array.Empty<string>();
            }

            List<string> messages = new();
            AppendSplitMessages(messages, mapInfo.help);
            AppendSplitMessages(messages, mapInfo.mapDesc);
            return messages;
        }

        private static void AppendSplitMessages(List<string> messages, string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return;
            }

            string normalized = rawText
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\r", string.Empty, StringComparison.Ordinal);
            string[] parts = normalized.Split(new[] { '\n', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part) && !messages.Contains(part, StringComparer.Ordinal))
                {
                    messages.Add(part);
                }
            }
        }

        private static string FormatRemainingTime(int remainingMs)
        {
            int remainingSeconds = Math.Max(0, (int)Math.Ceiling(remainingMs / 1000f));
            TimeSpan span = TimeSpan.FromSeconds(remainingSeconds);
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}"
                : $"{span.Minutes:00}:{span.Seconds:00}";
        }

        private static string TrimForStatus(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string compact = text.Replace('\n', ' ').Trim();
            return compact.Length <= 72 ? compact : $"{compact[..69]}...";
        }

        private static IEnumerable<string> WrapText(SpriteFont font, string text, float maxWidth, float scale)
        {
            foreach (string paragraph in (text ?? string.Empty).Split('\n'))
            {
                string remaining = paragraph.Trim();
                if (remaining.Length == 0)
                {
                    yield return string.Empty;
                    continue;
                }

                while (remaining.Length > 0)
                {
                    int length = remaining.Length;
                    while (length > 1 && (font.MeasureString(remaining[..length]).X * scale) > maxWidth)
                    {
                        int previousSpace = remaining.LastIndexOf(' ', length - 1, length - 1);
                        length = previousSpace > 0 ? previousSpace : length - 1;
                    }

                    string line = remaining[..Math.Max(1, length)].Trim();
                    yield return line;
                    remaining = remaining[line.Length..].TrimStart();
                }
            }
        }

        private static Color ResolveTimerFrameColor(string timerUiKey)
        {
            return timerUiKey?.Trim() switch
            {
                "SelectMob" => new Color(106, 186, 255),
                "default" => new Color(234, 205, 118),
                _ => new Color(214, 214, 214)
            };
        }

        private static string ResolveQuestName(int questId)
        {
            return ResolveQuestMetadata(questId).questName;
        }

        private static (string questName, string timerUiKey) ResolveQuestMetadata(int questId)
        {
            string key = questId.ToString(CultureInfo.InvariantCulture);
            if (Program.InfoManager?.QuestInfos != null &&
                Program.InfoManager.QuestInfos.TryGetValue(key, out WzSubProperty questInfo) &&
                questInfo != null)
            {
                string questName = (questInfo["name"] as WzStringProperty)?.Value;
                string timerUiKey = (questInfo["timerUI"] as WzStringProperty)?.Value;
                return (
                    string.IsNullOrWhiteSpace(questName) ? $"Quest #{questId}" : questName,
                    string.IsNullOrWhiteSpace(timerUiKey) ? "default" : timerUiKey);
            }

            return ($"Quest #{questId}", "default");
        }

        private sealed record PacketHelpMessageState(string Text, int ExpiresAtTick, int Index);

        private sealed record PacketQuestTimerEntry(
            int QuestId,
            string QuestName,
            string TimerUiKey,
            DateTime StartUtc,
            DateTime EndUtc,
            int ReceivedAtTick,
            int ExpireTick,
            int DurationMs);
    }
}
