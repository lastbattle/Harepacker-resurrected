using MapleLib.PacketLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HaSharedLibrary.Util;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketFieldStateRuntime
    {
        private const int HelpMessageDisplayDurationMs = 6000;
        private const int HelpFadeDurationMs = 220;
        private const int QuestTimerSpacing = 8;
        private const int QuestTimerDefaultTop = 12;
        private const int QuestTimerActionOffsetX = 52;
        private const int QuestTimerActionCenterOffsetY = 6;
        private const int QuestTimerActionTopOffsetY = 5;
        private const int QuestTimerTooltipRefreshIntervalMs = 500;
        private const int QuestTimerTooltipWidth = 150;
        private const int QuestTimerLargeScreenWidth = 1024;
        private const int QuestTimerLargeModeShiftX = 224;
        private const int QuestTimerGaugeUnitCount = 48;
        private const int QuestTimerLargeModeMinimumX = 400;
        private const int QuestTimerSmallModeMinimumX = 512;

        private readonly List<string> _mapHelpMessages = new();
        private readonly Dictionary<int, PacketQuestTimerEntry> _questTimers = new();
        private readonly Dictionary<int, PacketQuestTimerOwnerState> _questTimerOwners = new();
        private readonly Dictionary<string, PacketQuestTimerVisualStyle> _questTimerStyles = new(StringComparer.OrdinalIgnoreCase);
        private Texture2D _pixelTexture;
        private Texture2D _helpDialogTexture;
        private Texture2D _questTimeBarBackgroundTexture;
        private Texture2D _questTimeGaugeLeftTexture;
        private Texture2D _questTimeGaugeMiddleTexture;
        private Texture2D _questTimeGaugeRightTexture;
        private bool _visualAssetsLoaded;
        private int _boundMapId = int.MinValue;
        private string _boundMapName = string.Empty;
        private PacketHelpMessageState _activeHelpMessage;
        private string _lastFieldSpecificDataSummary = "No field-specific data payload received.";
        private string _statusMessage = "Packet-owned field state idle.";
        private bool _questTimerLargeMode;
        private int _questTimerLastRenderWidth;
        private int _questTimerHoveredQuestId;
        private Point _questTimerMousePosition;
        private int _questTimerDraggedQuestId = -1;

        internal void Initialize(GraphicsDevice graphicsDevice, MapInfo mapInfo)
        {
            if (_pixelTexture == null && graphicsDevice != null)
            {
                _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }

            EnsureVisualAssetsLoaded(graphicsDevice);
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
            _questTimerOwners.Clear();
            _activeHelpMessage = null;
            _lastFieldSpecificDataSummary = "No field-specific data payload received.";
            _statusMessage = _mapHelpMessages.Count > 0
                ? $"Loaded {_mapHelpMessages.Count} help message entr{(_mapHelpMessages.Count == 1 ? "y" : "ies")} for map {mapId}."
                : $"Packet-owned field state bound to map {mapId}.";
        }

        internal void Clear()
        {
            _questTimers.Clear();
            _questTimerOwners.Clear();
            _activeHelpMessage = null;
            _mapHelpMessages.Clear();
            _lastFieldSpecificDataSummary = "No field-specific data payload received.";
            _statusMessage = "Packet-owned field state cleared.";
            _boundMapId = int.MinValue;
            _boundMapName = string.Empty;
            _questTimerHoveredQuestId = 0;
            _questTimerDraggedQuestId = -1;
        }

        internal void Update(int currentTick)
        {
            if (_activeHelpMessage != null && currentTick >= _activeHelpMessage.ExpiresAtTick)
            {
                _activeHelpMessage = null;
            }

            if (_questTimers.Count == 0)
            {
                _questTimerHoveredQuestId = 0;
                UpdateQuestTimerTooltipState(currentTick);
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
                UpdateQuestTimerTooltipState(currentTick);
                return;
            }

            foreach (int questId in expiredQuestIds)
            {
                _questTimers.Remove(questId);
                _questTimerOwners.Remove(questId);
                if (_questTimerHoveredQuestId == questId)
                {
                    _questTimerHoveredQuestId = 0;
                }

                if (_questTimerDraggedQuestId == questId)
                {
                    _questTimerDraggedQuestId = -1;
                }
            }

            _statusMessage = expiredQuestIds.Count == 1
                ? $"Quest timer expired for {ResolveQuestName(expiredQuestIds[0])}."
                : $"{expiredQuestIds.Count} packet-authored quest timers expired.";

            UpdateQuestTimerTooltipState(currentTick);
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
                    _questTimerOwners.Clear();
                    _questTimerHoveredQuestId = 0;
                    _questTimerDraggedQuestId = -1;
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

            timerText = $"Time left: {FormatRemainingTime(Math.Max(0, timer.ExpireTick - currentTick))}";
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
            int visibleOwners = _questTimerOwners.Values.Count(static owner => !owner.IsDismissed);
            int dismissedOwners = _questTimerOwners.Count - visibleOwners;
            return $"{_statusMessage} {helpStatus}; {timerStatus}; timerOwners=visible:{visibleOwners},dismissed:{dismissedOwners}; fieldspecific={_lastFieldSpecificDataSummary}";
        }

        internal void DrawHelpOverlay(SpriteBatch spriteBatch, SpriteFont font, int renderWidth, int renderHeight, int currentTick)
        {
            if (spriteBatch == null || font == null || _pixelTexture == null)
            {
                return;
            }

            DrawHelpMessage(spriteBatch, font, renderWidth, renderHeight, currentTick);
        }

        internal void DrawQuestTimerOwnerLayer(SpriteBatch spriteBatch, int renderWidth, int renderHeight, int currentTick)
        {
            if (spriteBatch == null || _pixelTexture == null)
            {
                return;
            }

            SyncQuestTimerOwners(renderWidth);
            RefreshQuestTimerOwnerBounds(currentTick);
            DrawQuestTimerBars(spriteBatch, renderHeight, currentTick);
        }

        internal void DrawQuestTimerActionLayer(SpriteBatch spriteBatch, SpriteFont font, int renderWidth, int renderHeight, int currentTick)
        {
            if (spriteBatch == null || font == null || _pixelTexture == null)
            {
                return;
            }

            SyncQuestTimerOwners(renderWidth);
            RefreshQuestTimerOwnerBounds(currentTick);
            DrawQuestTimerActions(spriteBatch, font, renderWidth, renderHeight, currentTick);
        }

        internal IReadOnlyList<Rectangle> GetQuestTimerInteractiveBounds(int renderWidth, int renderHeight, int currentTick)
        {
            if (_questTimers.Count == 0)
            {
                return Array.Empty<Rectangle>();
            }

            SyncQuestTimerOwners(renderWidth);
            RefreshQuestTimerOwnerBounds(currentTick);

            List<Rectangle> bounds = new();
            foreach (PacketQuestTimerEntry timer in _questTimers.Values.OrderBy(static entry => entry.ExpireTick))
            {
                if (!_questTimerOwners.TryGetValue(timer.QuestId, out PacketQuestTimerOwnerState owner) ||
                    owner.IsDismissed ||
                    !owner.IsVisible)
                {
                    continue;
                }

                bounds.Add(Rectangle.Union(owner.BarBounds, owner.ActionBounds));
                if (owner.TooltipVisible && owner.TooltipBounds != Rectangle.Empty)
                {
                    bounds.Add(owner.TooltipBounds);
                }
            }

            return bounds;
        }

        internal bool HandleQuestTimerMouse(MouseState mouseState, MouseState previousMouseState, int renderWidth, int renderHeight, int currentTick)
        {
            SyncQuestTimerOwners(renderWidth);
            RefreshQuestTimerOwnerBounds(currentTick);
            _questTimerMousePosition = mouseState.Position;

            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && previousMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

            if (_questTimerDraggedQuestId >= 0 &&
                _questTimerOwners.TryGetValue(_questTimerDraggedQuestId, out PacketQuestTimerOwnerState draggedOwner))
            {
                if (leftPressed)
                {
                    draggedOwner.Position = new Point(
                        mouseState.X - draggedOwner.DragOffset.X,
                        Math.Max(0, mouseState.Y - draggedOwner.DragOffset.Y));
                    return true;
                }

                if (leftJustReleased)
                {
                    draggedOwner.IsDragging = false;
                    _questTimerDraggedQuestId = -1;
                    return true;
                }
            }

            int hoveredQuestId = HitTestQuestTimer(mouseState.Position, out bool overAction);
            if (_questTimerHoveredQuestId != hoveredQuestId &&
                hoveredQuestId > 0 &&
                _questTimerOwners.TryGetValue(hoveredQuestId, out PacketQuestTimerOwnerState hoveredTimerOwner))
            {
                hoveredTimerOwner.LastTooltipRefreshTick = int.MinValue;
            }

            _questTimerHoveredQuestId = hoveredQuestId;

            if (hoveredQuestId <= 0)
            {
                return false;
            }

            if (leftJustPressed && _questTimerOwners.TryGetValue(hoveredQuestId, out PacketQuestTimerOwnerState hoveredOwner))
            {
                if (overAction)
                {
                    hoveredOwner.IsDismissed = true;
                    hoveredOwner.IsDragging = false;
                    _questTimerHoveredQuestId = 0;
                    _statusMessage = $"Dismissed quest timer owner for {ResolveQuestName(hoveredQuestId)}.";
                    return true;
                }

                hoveredOwner.IsDragging = true;
                hoveredOwner.IsDismissed = false;
                hoveredOwner.IsPinned = true;
                hoveredOwner.DragOffset = new Point(
                    mouseState.X - hoveredOwner.Position.X,
                    mouseState.Y - hoveredOwner.Position.Y);
                _questTimerDraggedQuestId = hoveredQuestId;
                return true;
            }

            if (leftJustReleased)
            {
                _questTimerDraggedQuestId = -1;
            }

            return false;
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
            _activeHelpMessage = new PacketHelpMessageState(text, currentTick, currentTick + HelpMessageDisplayDurationMs, index);
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
                    PacketQuestTimerOwnerState owner = GetOrCreateQuestTimerOwner(questId);
                    owner.IsDismissed = false;
                    owner.IsDragging = false;
                    appliedCount++;
                }

                if (stream.Position != stream.Length)
                {
                    message = $"Quest timer packet has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                _statusMessage = appliedCount == 1 ? "Applied 1 packet-authored quest timer." : $"Applied {appliedCount} packet-authored quest timers.";
                message = _statusMessage;
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentOutOfRangeException || ex is ArgumentException)
            {
                message = $"Quest timer packet could not be decoded: {ex.Message}";
                return false;
            }
        }

        internal string ApplyQuestTimer(int questId, int remainingMs, bool timeKeepQuestTimer, int currentTick)
        {
            if (questId <= 0)
            {
                _statusMessage = "Ignored a packet-authored quest timer with an invalid quest id.";
                return _statusMessage;
            }

            int boundedRemainingMs = Math.Max(0, remainingMs);
            DateTime startUtc = DateTime.UtcNow;
            DateTime endUtc = startUtc.AddMilliseconds(boundedRemainingMs);
            (string questName, string timerUiKey) = ResolveQuestMetadata(questId);
            _questTimers[questId] = new PacketQuestTimerEntry(
                questId,
                questName,
                timerUiKey,
                startUtc,
                endUtc,
                currentTick,
                unchecked(currentTick + boundedRemainingMs),
                boundedRemainingMs);

            PacketQuestTimerOwnerState owner = GetOrCreateQuestTimerOwner(questId);
            owner.IsDismissed = false;
            owner.IsDragging = false;
            _statusMessage = timeKeepQuestTimer
                ? $"Applied packet-authored keep-alive quest timer for {questName}."
                : $"Applied packet-authored quest timer for {questName}.";
            return _statusMessage;
        }

        internal string RemoveQuestTimer(int questId, bool timeKeepQuestTimer)
        {
            if (questId <= 0)
            {
                _statusMessage = "Ignored a packet-authored quest-timer removal with an invalid quest id.";
                return _statusMessage;
            }

            string questName = ResolveQuestName(questId);
            bool removed = _questTimers.Remove(questId);
            _questTimerOwners.Remove(questId);
            if (_questTimerHoveredQuestId == questId)
            {
                _questTimerHoveredQuestId = 0;
            }

            if (_questTimerDraggedQuestId == questId)
            {
                _questTimerDraggedQuestId = -1;
            }

            _statusMessage = removed
                ? (timeKeepQuestTimer
                    ? $"Removed packet-authored keep-alive quest timer for {questName}."
                    : $"Removed packet-authored quest timer for {questName}.")
                : (timeKeepQuestTimer
                    ? $"Packet-authored keep-alive quest timer for {questName} was already absent."
                    : $"Packet-authored quest timer for {questName} was already absent.");
            return _statusMessage;
        }

        internal string ResetQuestTimer(int questId, bool timeKeepQuestTimer, int currentTick)
        {
            if (questId <= 0)
            {
                _statusMessage = "Ignored a packet-authored quest-timer reset with an invalid quest id.";
                return _statusMessage;
            }

            if (!_questTimers.TryGetValue(questId, out PacketQuestTimerEntry timer))
            {
                _statusMessage = timeKeepQuestTimer
                    ? $"Packet-authored keep-alive quest timer for {ResolveQuestName(questId)} is not active."
                    : $"Packet-authored quest timer for {ResolveQuestName(questId)} is not active.";
                return _statusMessage;
            }

            int durationMs = Math.Max(0, timer.DurationMs);
            DateTime startUtc = DateTime.UtcNow;
            DateTime endUtc = startUtc.AddMilliseconds(durationMs);
            _questTimers[questId] = timer with
            {
                StartUtc = startUtc,
                EndUtc = endUtc,
                ReceivedAtTick = currentTick,
                ExpireTick = unchecked(currentTick + durationMs)
            };

            PacketQuestTimerOwnerState owner = GetOrCreateQuestTimerOwner(questId);
            owner.IsDismissed = false;
            owner.IsDragging = false;
            _statusMessage = timeKeepQuestTimer
                ? $"Reset packet-authored keep-alive quest timer for {timer.QuestName}."
                : $"Reset packet-authored quest timer for {timer.QuestName}.";
            return _statusMessage;
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
            _lastFieldSpecificDataSummary = string.IsNullOrWhiteSpace(handoffSummary) ? payloadSummary : $"{payloadSummary}, {handoffSummary}";
            _statusMessage = "Captured packet-authored field-specific data.";
            message = _lastFieldSpecificDataSummary;
            return true;
        }

        private PacketQuestTimerOwnerState GetOrCreateQuestTimerOwner(int questId)
        {
            if (!_questTimerOwners.TryGetValue(questId, out PacketQuestTimerOwnerState owner))
            {
                owner = new PacketQuestTimerOwnerState(questId);
                _questTimerOwners[questId] = owner;
            }

            return owner;
        }

        private void SyncQuestTimerOwners(int renderWidth)
        {
            if (_questTimers.Count == 0)
            {
                _questTimerLastRenderWidth = renderWidth;
                return;
            }

            _questTimerLargeMode = renderWidth >= QuestTimerLargeScreenWidth;
            _questTimerLastRenderWidth = renderWidth;

            int barWidth = Math.Max(88, _questTimeBarBackgroundTexture?.Width ?? 110);
            int barHeight = Math.Max(10, _questTimeBarBackgroundTexture?.Height ?? 14);
            int ownerWidth = QuestTimerActionOffsetX + barWidth;
            int ownerHeight = Math.Max(barHeight, ResolveQuestTimerActionSize()) + QuestTimerSpacing;
            int anchorX = Math.Max(12, renderWidth - ownerWidth - (_questTimerLargeMode ? QuestTimerLargeModeShiftX : 20));
            int layoutIndex = 0;

            foreach (PacketQuestTimerEntry timer in _questTimers.Values.OrderBy(static entry => entry.ExpireTick))
            {
                PacketQuestTimerOwnerState owner = GetOrCreateQuestTimerOwner(timer.QuestId);
                owner.SetVisible(!owner.IsDismissed);
                owner.ApplyScreenMode(_questTimerLargeMode);
                if (owner.IsDismissed || owner.IsDragging || owner.IsPinned)
                {
                    continue;
                }

                owner.Position = new Point(anchorX, QuestTimerDefaultTop + (layoutIndex * ownerHeight));
                layoutIndex++;
            }
        }

        private int HitTestQuestTimer(Point mousePosition, out bool overAction)
        {
            overAction = false;

            foreach (PacketQuestTimerEntry timer in _questTimers.Values.OrderByDescending(static entry => entry.ExpireTick))
            {
                if (!_questTimerOwners.TryGetValue(timer.QuestId, out PacketQuestTimerOwnerState owner) || owner.IsDismissed)
                {
                    continue;
                }

                if (owner.ActionBounds.Contains(mousePosition))
                {
                    overAction = true;
                    return timer.QuestId;
                }

                Rectangle combinedBounds = Rectangle.Union(owner.BarBounds, owner.ActionBounds);
                if (combinedBounds.Contains(mousePosition))
                {
                    return timer.QuestId;
                }
            }

            return 0;
        }

        private void RefreshQuestTimerOwnerBounds(int currentTick)
        {
            foreach (PacketQuestTimerEntry timer in _questTimers.Values.OrderBy(static entry => entry.ExpireTick))
            {
                PacketQuestTimerOwnerState owner = GetOrCreateQuestTimerOwner(timer.QuestId);
                Rectangle barBounds = ResolveQuestTimerBarBounds(owner.Position);
                Rectangle actionBounds = ResolveQuestTimerActionBounds(timer, owner.Position, currentTick);
                owner.SetBounds(barBounds, actionBounds);
            }
        }

        private Rectangle ResolveQuestTimerBarBounds(Point ownerPosition)
        {
            int width = Math.Max(88, _questTimeBarBackgroundTexture?.Width ?? 110);
            int height = Math.Max(10, _questTimeBarBackgroundTexture?.Height ?? 14);
            return new Rectangle(ownerPosition.X + QuestTimerActionOffsetX, ownerPosition.Y, width, height);
        }

        private Rectangle ResolveQuestTimerActionBounds(PacketQuestTimerEntry timer, Point ownerPosition, int currentTick)
        {
            PacketQuestTimerVisualStyle style = ResolveQuestTimerStyle(timer.TimerUiKey);
            PacketQuestTimerFrame frame = ResolveQuestTimerFrame(style, currentTick);
            Texture2D iconTexture = frame?.Texture;
            Point origin = frame?.Origin ?? Point.Zero;
            int width = Math.Max(16, iconTexture?.Width ?? 18);
            int height = Math.Max(16, iconTexture?.Height ?? 18);
            int x = ownerPosition.X + Math.Max(0, QuestTimerActionOffsetX - width);
            int y = ownerPosition.Y + QuestTimerActionTopOffsetY - origin.Y + QuestTimerActionCenterOffsetY - (height / 2);
            return new Rectangle(x, y, width, height);
        }

        private void DrawQuestTimerAction(SpriteBatch spriteBatch, PacketQuestTimerEntry timer, Rectangle actionBounds, int currentTick)
        {
            PacketQuestTimerVisualStyle style = ResolveQuestTimerStyle(timer.TimerUiKey);
            PacketQuestTimerFrame frame = ResolveQuestTimerFrame(style, currentTick);
            if (frame?.Texture != null)
            {
                spriteBatch.Draw(frame.Texture, new Vector2(actionBounds.X, actionBounds.Y), Color.White);
                return;
            }

            spriteBatch.Draw(_pixelTexture, actionBounds, new Color(245, 197, 90));
            Rectangle innerBounds = new(actionBounds.X + 3, actionBounds.Y + 3, Math.Max(2, actionBounds.Width - 6), Math.Max(2, actionBounds.Height - 6));
            spriteBatch.Draw(_pixelTexture, innerBounds, new Color(123, 84, 24));
        }

        private void DrawQuestTimerTooltip(SpriteBatch spriteBatch, SpriteFont font, int renderWidth, int renderHeight, int currentTick)
        {
            if (_questTimerHoveredQuestId <= 0 ||
                !_questTimers.TryGetValue(_questTimerHoveredQuestId, out PacketQuestTimerEntry timer) ||
                !_questTimerOwners.TryGetValue(_questTimerHoveredQuestId, out PacketQuestTimerOwnerState owner) ||
                owner.IsDismissed ||
                !owner.IsVisible ||
                !owner.TooltipVisible ||
                string.IsNullOrWhiteSpace(owner.TooltipText))
            {
                return;
            }

            Vector2 measured = font.MeasureString(timer.QuestName);
            Vector2 measuredTime = font.MeasureString("00:00:00");
            int width = Math.Max(QuestTimerTooltipWidth, (int)Math.Ceiling(Math.Max(measured.X, measuredTime.X)) + 18);
            int height = Math.Max(42, (int)Math.Ceiling((font.LineSpacing * 2f) + 14f));
            int x = Math.Clamp(_questTimerMousePosition.X + 18, 8, Math.Max(8, renderWidth - width - 8));
            int y = Math.Clamp(_questTimerMousePosition.Y + 18, 8, Math.Max(8, renderHeight - height - 8));
            Rectangle tooltipBounds = new(x, y, width, height);
            owner.TooltipBounds = tooltipBounds;
            DrawPanel(spriteBatch, tooltipBounds, new Color(18, 22, 30, 236), new Color(236, 208, 124));
            DrawWrappedText(spriteBatch, font, owner.TooltipText, new Rectangle(x + 8, y + 7, width - 16, height - 12), 0.52f, Color.White);
        }

        private int ResolveQuestTimerActionSize()
        {
            if (_questTimerStyles.TryGetValue("default", out PacketQuestTimerVisualStyle style))
            {
                PacketQuestTimerFrame frame = style.Frames.FirstOrDefault();
                if (frame?.Texture != null)
                {
                    return Math.Max(frame.Texture.Width, frame.Texture.Height);
                }
            }

            return 18;
        }

        private void DrawQuestTimerBars(SpriteBatch spriteBatch, int renderHeight, int currentTick)
        {
            if (_questTimers.Count == 0)
            {
                return;
            }

            List<PacketQuestTimerEntry> timers = _questTimers.Values.OrderBy(static timer => timer.ExpireTick).ToList();
            for (int i = 0; i < timers.Count; i++)
            {
                PacketQuestTimerEntry timer = timers[i];
                PacketQuestTimerOwnerState owner = GetOrCreateQuestTimerOwner(timer.QuestId);
                if (owner.IsDismissed)
                {
                    continue;
                }

                DrawQuestTimerBar(spriteBatch, timer, owner, renderHeight, currentTick);
            }
        }

        private void DrawQuestTimerActions(SpriteBatch spriteBatch, SpriteFont font, int renderWidth, int renderHeight, int currentTick)
        {
            if (_questTimers.Count == 0)
            {
                return;
            }

            List<PacketQuestTimerEntry> timers = _questTimers.Values.OrderBy(static timer => timer.ExpireTick).ToList();
            for (int i = 0; i < timers.Count; i++)
            {
                PacketQuestTimerEntry timer = timers[i];
                PacketQuestTimerOwnerState owner = GetOrCreateQuestTimerOwner(timer.QuestId);
                if (owner.IsDismissed)
                {
                    continue;
                }

                DrawQuestTimerActionEntry(spriteBatch, timer, owner, currentTick);
            }

            DrawQuestTimerTooltip(spriteBatch, font, renderWidth, renderHeight, currentTick);
        }

        private void DrawQuestTimerBar(
            SpriteBatch spriteBatch,
            PacketQuestTimerEntry timer,
            PacketQuestTimerOwnerState owner,
            int renderHeight,
            int currentTick)
        {
            DrawQuestGauge(spriteBatch, owner.BarBounds);
            DrawQuestGaugeFill(spriteBatch, owner.BarBounds, ResolveQuestTimerUnitCount(timer, currentTick));
        }

        private void DrawQuestTimerActionEntry(SpriteBatch spriteBatch, PacketQuestTimerEntry timer, PacketQuestTimerOwnerState owner, int currentTick)
        {
            DrawQuestTimerAction(spriteBatch, timer, owner.ActionBounds, currentTick);
        }

        private void DrawQuestGauge(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (_questTimeBarBackgroundTexture != null)
            {
                spriteBatch.Draw(_questTimeBarBackgroundTexture, new Vector2(bounds.X, bounds.Y), Color.White);
            }
            else
            {
                spriteBatch.Draw(_pixelTexture, bounds, new Color(52, 55, 71, 220));
            }

        }

        private void DrawQuestGaugeFill(SpriteBatch spriteBatch, Rectangle bounds, int unitCount)
        {
            if (unitCount <= 0)
            {
                return;
            }

            if (_questTimeGaugeLeftTexture == null || _questTimeGaugeMiddleTexture == null || _questTimeGaugeRightTexture == null)
            {
                int fallbackWidth = (int)Math.Round(bounds.Width * Math.Clamp(unitCount / (float)QuestTimerGaugeUnitCount, 0f, 1f));
                spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y + 2, fallbackWidth, Math.Max(2, bounds.Height - 4)), new Color(255, 202, 94));
                return;
            }

            int fillStartX = bounds.X + 2;
            int fillY = bounds.Y + 2;
            int rightCapX = fillStartX + QuestTimerGaugeUnitCount - _questTimeGaugeRightTexture.Width;
            spriteBatch.Draw(_questTimeGaugeRightTexture, new Vector2(rightCapX, fillY), Color.White);

            if (unitCount > _questTimeGaugeRightTexture.Width)
            {
                int middleUnits = Math.Min(
                    unitCount - _questTimeGaugeRightTexture.Width,
                    QuestTimerGaugeUnitCount - _questTimeGaugeRightTexture.Width - 1);
                int middleCursorX = rightCapX - _questTimeGaugeMiddleTexture.Width;
                for (int i = 0; i < middleUnits; i++)
                {
                    spriteBatch.Draw(_questTimeGaugeMiddleTexture, new Vector2(middleCursorX - i, fillY), Color.White);
                }
            }

            if (unitCount > (QuestTimerGaugeUnitCount - 1))
            {
                spriteBatch.Draw(_questTimeGaugeLeftTexture, new Vector2(fillStartX, fillY), Color.White);
            }
        }

        private void DrawHelpMessage(SpriteBatch spriteBatch, SpriteFont font, int renderWidth, int renderHeight, int currentTick)
        {
            if (_activeHelpMessage == null || string.IsNullOrWhiteSpace(_activeHelpMessage.Text))
            {
                return;
            }

            float alpha = ComputeHelpAlpha(_activeHelpMessage, currentTick);
            if (_helpDialogTexture != null)
            {
                int boxX = (renderWidth - _helpDialogTexture.Width) / 2;
                int boxY = Math.Max(26, (renderHeight / 2) - (_helpDialogTexture.Height / 2));
                spriteBatch.Draw(_helpDialogTexture, new Vector2(boxX, boxY), Color.White * alpha);

                if (!string.IsNullOrWhiteSpace(_boundMapName))
                {
                    spriteBatch.DrawString(font, _boundMapName, new Vector2(boxX + 18, boxY + 14), new Color(246, 225, 160) * alpha, 0f, Vector2.Zero, 0.46f, SpriteEffects.None, 0f);
                }

                DrawWrappedText(spriteBatch, font, _activeHelpMessage.Text, new Rectangle(boxX + 22, boxY + 38, _helpDialogTexture.Width - 44, _helpDialogTexture.Height - 54), 0.56f, new Color(247, 242, 232) * alpha);
                return;
            }

            Rectangle bounds = new((renderWidth - 360) / 2, Math.Max(26, (renderHeight / 2) - 60), 360, 120);
            DrawPanel(spriteBatch, bounds, new Color(22, 25, 34, 232) * alpha, new Color(234, 205, 118) * alpha);
            DrawWrappedText(spriteBatch, font, _activeHelpMessage.Text, new Rectangle(bounds.X + 14, bounds.Y + 16, bounds.Width - 28, bounds.Height - 28), 0.58f, Color.White * alpha);
        }

        private void DrawWrappedText(SpriteBatch spriteBatch, SpriteFont font, string text, Rectangle bounds, float scale, Color color)
        {
            Vector2 textPos = new(bounds.X, bounds.Y);
            foreach (string line in WrapText(font, text, bounds.Width, scale))
            {
                spriteBatch.DrawString(font, line, textPos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                textPos.Y += font.LineSpacing * scale;
                if (textPos.Y > bounds.Bottom)
                {
                    break;
                }
            }
        }

        private void DrawPanel(SpriteBatch spriteBatch, Rectangle bounds, Color fill, Color border)
        {
            spriteBatch.Draw(_pixelTexture, bounds, fill);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), border);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), border);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), border);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), border);
        }

        private void EnsureVisualAssetsLoaded(GraphicsDevice graphicsDevice)
        {
            if (_visualAssetsLoaded || graphicsDevice == null)
            {
                return;
            }

            _visualAssetsLoaded = true;
            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img");
            WzImage uiWindow1Image = Program.FindImage("UI", "UIWindow.img");
            EnsureParsed(uiWindow2Image);
            EnsureParsed(uiWindow1Image);

            _helpDialogTexture = LoadCanvasTexture(ResolveProperty(uiWindow2Image, "UtilDlgEx/notice") as WzCanvasProperty, graphicsDevice)
                ?? LoadCanvasTexture(ResolveProperty(uiWindow1Image, "UtilDlgEx/notice") as WzCanvasProperty, graphicsDevice);
            _questTimeBarBackgroundTexture = LoadCanvasTexture(ResolveProperty(uiWindow2Image, "Quest/TimeQuest/TimeBar/backgrnd") as WzCanvasProperty, graphicsDevice)
                ?? LoadCanvasTexture(ResolveProperty(uiWindow1Image, "Quest/TimeQuest/TimeBar/backgrnd") as WzCanvasProperty, graphicsDevice);
            _questTimeGaugeLeftTexture = LoadCanvasTexture(ResolveProperty(uiWindow2Image, "Quest/TimeQuest/TimeBar/TimeGage/0") as WzCanvasProperty, graphicsDevice)
                ?? LoadCanvasTexture(ResolveProperty(uiWindow1Image, "Quest/TimeQuest/TimeBar/TimeGage/0") as WzCanvasProperty, graphicsDevice);
            _questTimeGaugeMiddleTexture = LoadCanvasTexture(ResolveProperty(uiWindow2Image, "Quest/TimeQuest/TimeBar/TimeGage/1") as WzCanvasProperty, graphicsDevice)
                ?? LoadCanvasTexture(ResolveProperty(uiWindow1Image, "Quest/TimeQuest/TimeBar/TimeGage/1") as WzCanvasProperty, graphicsDevice);
            _questTimeGaugeRightTexture = LoadCanvasTexture(ResolveProperty(uiWindow2Image, "Quest/TimeQuest/TimeBar/TimeGage/2") as WzCanvasProperty, graphicsDevice)
                ?? LoadCanvasTexture(ResolveProperty(uiWindow1Image, "Quest/TimeQuest/TimeBar/TimeGage/2") as WzCanvasProperty, graphicsDevice);

            LoadQuestTimerStyle("default", uiWindow2Image, uiWindow1Image, graphicsDevice);
            LoadQuestTimerStyle("SelectMob", uiWindow2Image, uiWindow1Image, graphicsDevice);
        }

        private void LoadQuestTimerStyle(string styleKey, WzImage uiWindow2Image, WzImage uiWindow1Image, GraphicsDevice graphicsDevice)
        {
            PacketQuestTimerVisualStyle style = TryLoadQuestTimerStyle(uiWindow2Image, styleKey, graphicsDevice)
                ?? TryLoadQuestTimerStyle(uiWindow1Image, styleKey, graphicsDevice);
            if (style != null)
            {
                _questTimerStyles[styleKey] = style;
            }
        }

        private static PacketQuestTimerVisualStyle TryLoadQuestTimerStyle(WzImage image, string styleKey, GraphicsDevice graphicsDevice)
        {
            if (image == null)
            {
                return null;
            }

            WzSubProperty styleProperty = ResolveProperty(image, $"Quest/TimeQuest/AlarmClock/{styleKey}") as WzSubProperty;
            if (styleProperty == null)
            {
                return null;
            }

            List<PacketQuestTimerFrame> frames = new();
            foreach (WzImageProperty child in styleProperty.WzProperties.OrderBy(static prop => prop.Name, StringComparer.Ordinal))
            {
                if (child is not WzSubProperty frameGroup)
                {
                    continue;
                }

                foreach (WzImageProperty frameProperty in frameGroup.WzProperties.OrderBy(static prop => prop.Name, StringComparer.Ordinal))
                {
                    if (frameProperty is not WzCanvasProperty canvas)
                    {
                        continue;
                    }

                    Texture2D texture = LoadCanvasTexture(canvas, graphicsDevice);
                    if (texture == null)
                    {
                        continue;
                    }

                    int delay = Math.Max(120, (canvas["delay"] as WzIntProperty)?.Value ?? 120);
                    frames.Add(new PacketQuestTimerFrame(texture, delay, ResolveCanvasOrigin(canvas)));
                }
            }

            return frames.Count == 0 ? null : new PacketQuestTimerVisualStyle(styleKey, frames);
        }

        private static void EnsureParsed(WzImage image)
        {
            if (image != null && !image.Parsed)
            {
                image.ParseImage();
            }
        }

        private static WzImageProperty ResolveProperty(WzObject root, string propertyPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(propertyPath))
            {
                return root as WzImageProperty;
            }

            WzObject current = root;
            foreach (string segment in propertyPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = current switch
                {
                    WzImage image => image[segment],
                    WzImageProperty property => property[segment],
                    _ => null
                };
                if (current == null)
                {
                    break;
                }
            }

            return current as WzImageProperty;
        }

        private static Texture2D LoadCanvasTexture(WzCanvasProperty canvas, GraphicsDevice graphicsDevice)
        {
            try
            {
                return canvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(graphicsDevice);
            }
            catch
            {
                return null;
            }
        }

        private static Point ResolveCanvasOrigin(WzCanvasProperty canvas)
        {
            System.Drawing.PointF? origin = canvas?.GetCanvasOriginPosition();
            return origin.HasValue ? new Point((int)origin.Value.X, (int)origin.Value.Y) : Point.Zero;
        }

        private PacketQuestTimerVisualStyle ResolveQuestTimerStyle(string timerUiKey)
        {
            if (!string.IsNullOrWhiteSpace(timerUiKey) && _questTimerStyles.TryGetValue(timerUiKey, out PacketQuestTimerVisualStyle style))
            {
                return style;
            }

            return _questTimerStyles.TryGetValue("default", out PacketQuestTimerVisualStyle fallback) ? fallback : null;
        }

        private static Texture2D ResolveQuestTimerIcon(PacketQuestTimerVisualStyle style, int currentTick)
        {
            PacketQuestTimerFrame frame = ResolveQuestTimerFrame(style, currentTick);
            return frame?.Texture;
        }

        private static PacketQuestTimerFrame ResolveQuestTimerFrame(PacketQuestTimerVisualStyle style, int currentTick)
        {
            if (style?.Frames == null || style.Frames.Count == 0)
            {
                return null;
            }

            int elapsed = Math.Max(0, currentTick % style.TotalDuration);
            int accumulated = 0;
            for (int i = 0; i < style.Frames.Count; i++)
            {
                accumulated += style.Frames[i].Delay;
                if (elapsed < accumulated)
                {
                    return style.Frames[i];
                }
            }

            return style.Frames[style.Frames.Count - 1];
        }

        private static float ComputeHelpAlpha(PacketHelpMessageState helpMessage, int currentTick)
        {
            if (helpMessage == null)
            {
                return 0f;
            }

            float fadeIn = Math.Clamp((currentTick - helpMessage.StartedAtTick) / (float)HelpFadeDurationMs, 0f, 1f);
            float fadeOut = Math.Clamp((helpMessage.ExpiresAtTick - currentTick) / (float)HelpFadeDurationMs, 0f, 1f);
            return Math.Min(fadeIn, fadeOut);
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

            string normalized = rawText.Replace("\\n", "\n", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal);
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
            return span.TotalHours >= 1 ? $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}" : $"{span.Minutes:00}:{span.Seconds:00}";
        }

        private void UpdateQuestTimerTooltipState(int currentTick)
        {
            foreach (PacketQuestTimerOwnerState timerOwner in _questTimerOwners.Values)
            {
                timerOwner.TooltipVisible = false;
                timerOwner.TooltipBounds = Rectangle.Empty;
            }

            if (_questTimerHoveredQuestId <= 0 ||
                !_questTimers.TryGetValue(_questTimerHoveredQuestId, out PacketQuestTimerEntry timer) ||
                !_questTimerOwners.TryGetValue(_questTimerHoveredQuestId, out PacketQuestTimerOwnerState owner) ||
                owner.IsDismissed ||
                !owner.IsVisible)
            {
                return;
            }

            owner.TooltipVisible = true;
            if (owner.LastTooltipRefreshTick == int.MinValue ||
                currentTick - owner.LastTooltipRefreshTick >= QuestTimerTooltipRefreshIntervalMs)
            {
                owner.TooltipText = BuildQuestTimerTooltipText(timer, currentTick);
                owner.LastTooltipRefreshTick = currentTick;
            }
        }

        private static string BuildQuestTimerTooltipText(PacketQuestTimerEntry timer, int currentTick)
        {
            string remaining = FormatQuestTimerTooltipRemainingTime(Math.Max(0, timer.ExpireTick - currentTick));
            return $"{timer.QuestName}\n{remaining}";
        }

        private static string FormatQuestTimerTooltipRemainingTime(int remainingMs)
        {
            int remainingSeconds = Math.Max(0, (int)Math.Ceiling(remainingMs / 1000f));
            TimeSpan span = TimeSpan.FromSeconds(remainingSeconds);
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }

        private static int ResolveQuestTimerUnitCount(PacketQuestTimerEntry timer, int currentTick)
        {
            int remainingMs = Math.Max(0, timer.ExpireTick - currentTick);
            if (remainingMs <= 0)
            {
                return 0;
            }

            int unitDuration = Math.Max(1, timer.DurationMs / QuestTimerGaugeUnitCount);
            return Math.Clamp((remainingMs / unitDuration) + 1, 1, QuestTimerGaugeUnitCount);
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

        private static string ResolveQuestName(int questId) => ResolveQuestMetadata(questId).questName;

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

        private sealed record PacketHelpMessageState(string Text, int StartedAtTick, int ExpiresAtTick, int Index);
        private sealed record PacketQuestTimerEntry(int QuestId, string QuestName, string TimerUiKey, DateTime StartUtc, DateTime EndUtc, int ReceivedAtTick, int ExpireTick, int DurationMs);
        private sealed record PacketQuestTimerFrame(Texture2D Texture, int Delay, Point Origin);
        private sealed class PacketQuestTimerOwnerState
        {
            public PacketQuestTimerOwnerState(int questId)
            {
                QuestId = questId;
                ActionOwner = new PacketQuestTimerActionOwner(questId);
            }

            public int QuestId { get; }
            public PacketQuestTimerActionOwner ActionOwner { get; }
            public Point Position { get; set; } = new(QuestTimerDefaultTop, QuestTimerDefaultTop);
            public Rectangle BarBounds { get; private set; }
            public Rectangle ActionBounds => ActionOwner.Bounds;
            public Rectangle TooltipBounds { get; set; }
            public Point DragOffset { get; set; }
            public bool IsDismissed { get; set; }
            public bool IsDragging { get; set; }
            public bool IsPinned { get; set; }
            public bool IsVisible { get; private set; } = true;
            public bool IsLargeMode { get; private set; }
            public bool TooltipVisible { get; set; }
            public string TooltipText { get; set; } = string.Empty;
            public int LastTooltipRefreshTick { get; set; } = int.MinValue;

            public void SetBounds(Rectangle barBounds, Rectangle actionBounds)
            {
                BarBounds = barBounds;
                ActionOwner.Bounds = actionBounds;
            }

            public void SetVisible(bool visible)
            {
                IsVisible = visible;
                ActionOwner.IsVisible = visible;
                if (!visible)
                {
                    TooltipVisible = false;
                    TooltipBounds = Rectangle.Empty;
                }
            }

            public void ApplyScreenMode(bool largeMode)
            {
                if (IsLargeMode == largeMode)
                {
                    return;
                }

                int shiftedX = Position.X;
                if (largeMode)
                {
                    if (shiftedX >= QuestTimerLargeModeMinimumX)
                    {
                        shiftedX += QuestTimerLargeModeShiftX;
                    }
                }
                else if (shiftedX >= QuestTimerSmallModeMinimumX)
                {
                    shiftedX -= QuestTimerLargeModeShiftX;
                }

                Position = new Point(shiftedX, Position.Y);
                IsLargeMode = largeMode;
            }
        }

        private sealed class PacketQuestTimerActionOwner
        {
            public PacketQuestTimerActionOwner(int questId)
            {
                QuestId = questId;
            }

            public int QuestId { get; }
            public Rectangle Bounds { get; set; }
            public bool IsVisible { get; set; }
        }

        private sealed record PacketQuestTimerVisualStyle(string StyleKey, IReadOnlyList<PacketQuestTimerFrame> Frames)
        {
            public int TotalDuration { get; } = Math.Max(1, Frames.Sum(static frame => Math.Max(1, frame.Delay)));
        }
    }
}
