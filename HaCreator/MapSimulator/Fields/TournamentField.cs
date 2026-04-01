using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Fields
{
    public enum TournamentPacketType
    {
        Tournament = 374,
        MatchTable = 375,
        SetPrize = 376,
        Uew = 377,
        NoOp = 378
    }

    internal readonly record struct TournamentClientMessage(int StringPoolId, string FallbackText);

    public sealed class TournamentField
    {
        private const int StatusDurationMs = 9000;

        private int _mapId;
        private bool _isActive;
        private int _lastPacketType;
        private string _statusMessage;
        private int _statusMessageUntil;
        private string _lastPacketSummary;
        private string _lastPayloadHex;
        private int[] _lastStringPoolIds = Array.Empty<int>();

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public int LastPacketType => _lastPacketType;
        public string CurrentStatusMessage => _statusMessage;
        public string LastPacketSummary => _lastPacketSummary;
        public IReadOnlyList<int> LastStringPoolIds => _lastStringPoolIds;

        public void Configure(MapInfo mapInfo)
        {
            Reset();
            _mapId = mapInfo?.id ?? 0;
            _isActive = mapInfo?.fieldType == FieldType.FIELDTYPE_TOURNAMENT;
            if (_isActive)
            {
                _statusMessage = $"Tournament wrapper ready for map {_mapId} (String/Map.img/victoria/109070000/help0 describes the tournament bracket flow).";
                _statusMessageUntil = Environment.TickCount + StatusDurationMs;
            }
        }

        public void Update(int tickCount)
        {
            if (_statusMessage != null && tickCount >= _statusMessageUntil)
            {
                _statusMessage = null;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || spriteBatch == null || pixelTexture == null || font == null)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            const int panelWidth = 388;
            const int panelHeight = 142;
            int panelX = viewport.Width - panelWidth - 18;
            int panelY = 18;

            spriteBatch.Draw(pixelTexture, new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(18, 23, 33, 225));
            spriteBatch.Draw(pixelTexture, new Rectangle(panelX, panelY, panelWidth, 30), new Color(82, 59, 44, 255));
            DrawShadowedText(spriteBatch, font, "Tournament", new Vector2(panelX + 12, panelY + 7), Color.White);

            DrawShadowedText(
                spriteBatch,
                font,
                $"map={_mapId} | packets=374-378 | owner=CField_Tournament",
                new Vector2(panelX + 12, panelY + 38),
                Color.Gainsboro,
                0.85f);

            DrawShadowedText(
                spriteBatch,
                font,
                string.IsNullOrWhiteSpace(_lastPacketSummary)
                    ? "No tournament packet has been applied yet."
                    : _lastPacketSummary,
                new Vector2(panelX + 12, panelY + 60),
                Color.White,
                0.85f);

            string stringPoolText = _lastStringPoolIds.Length == 0
                ? "StringPool ids: none"
                : $"StringPool ids: {string.Join(", ", _lastStringPoolIds.Select(id => $"0x{id:X}"))}";
            DrawShadowedText(spriteBatch, font, stringPoolText, new Vector2(panelX + 12, panelY + 80), Color.Silver, 0.85f);

            string payloadText = string.IsNullOrWhiteSpace(_lastPayloadHex)
                ? "payload: none"
                : $"payload: {TrimForDisplay(_lastPayloadHex, 52)}";
            DrawShadowedText(spriteBatch, font, payloadText, new Vector2(panelX + 12, panelY + 100), Color.Silver, 0.85f);

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                DrawShadowedText(spriteBatch, font, TrimForDisplay(_statusMessage, 72), new Vector2(panelX + 12, panelY + 120), Color.LightGoldenrodYellow, 0.82f);
            }
        }

        public bool TryApplyPacket(TournamentPacketType packetType, byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;

            if (!_isActive)
            {
                errorMessage = "Tournament runtime inactive.";
                return false;
            }

            payload ??= Array.Empty<byte>();
            _lastPacketType = (int)packetType;
            _lastPayloadHex = Convert.ToHexString(payload);

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);

                switch (packetType)
                {
                    case TournamentPacketType.Tournament:
                        ApplyTournamentNotice(reader, currentTimeMs);
                        EnsurePacketConsumed(stream, "tournament");
                        return true;

                    case TournamentPacketType.MatchTable:
                        ApplyMatchTable(payload, currentTimeMs);
                        return true;

                    case TournamentPacketType.SetPrize:
                        ApplyPrize(reader, currentTimeMs);
                        EnsurePacketConsumed(stream, "set-prize");
                        return true;

                    case TournamentPacketType.Uew:
                        ApplyUew(reader, currentTimeMs);
                        EnsurePacketConsumed(stream, "uew");
                        return true;

                    case TournamentPacketType.NoOp:
                        SetStatus(
                            "Tournament packet 378 reached the dedicated client wrapper and intentionally performed no local UI action.",
                            currentTimeMs,
                            Array.Empty<int>(),
                            "noop (378)");
                        EnsurePacketConsumed(stream, "noop");
                        return true;

                    default:
                        errorMessage = $"Unsupported Tournament packet type: {(int)packetType}";
                        return false;
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is InvalidDataException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public bool TryApplyRawPacket(int packetType, byte[] payload, int currentTimeMs, out string errorMessage)
        {
            if (!Enum.IsDefined(typeof(TournamentPacketType), packetType))
            {
                errorMessage = $"Unsupported Tournament raw packet type: {packetType}";
                return false;
            }

            return TryApplyPacket((TournamentPacketType)packetType, payload, currentTimeMs, out errorMessage);
        }

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Tournament runtime is inactive on this map.";
            }

            string packetText = _lastPacketType > 0 ? DescribePacketType(_lastPacketType) : "none";
            string stringPoolText = _lastStringPoolIds.Length == 0
                ? "none"
                : string.Join("/", _lastStringPoolIds.Select(id => $"0x{id:X}"));
            string summary = string.IsNullOrWhiteSpace(_lastPacketSummary) ? "No packet applied yet." : _lastPacketSummary;
            return $"Tournament: active | map={_mapId} | last={packetText} | stringPool={stringPoolText} | summary={summary}";
        }

        public void Reset()
        {
            _mapId = 0;
            _isActive = false;
            _lastPacketType = 0;
            _statusMessage = null;
            _statusMessageUntil = 0;
            _lastPacketSummary = null;
            _lastPayloadHex = null;
            _lastStringPoolIds = Array.Empty<int>();
        }

        private void ApplyTournamentNotice(BinaryReader reader, int currentTimeMs)
        {
            byte branch = reader.ReadByte();
            byte noticeCode = reader.ReadByte();

            TournamentClientMessage message = branch == 0
                ? noticeCode switch
                {
                    0 => new TournamentClientMessage(0x3A4, "Tournament entry notice branch 0 reported that the current session cannot proceed."),
                    1 => new TournamentClientMessage(0x3A3, "Tournament entry notice branch 1 reported that the current session cannot proceed."),
                    _ => new TournamentClientMessage(0x3A3, $"Tournament blocked notice branch received unknown code {noticeCode}.")
                }
                : noticeCode switch
                {
                    1 => new TournamentClientMessage(0x3A7, "Tournament round-result branch selected notice code 1."),
                    2 => new TournamentClientMessage(0x3A6, "Tournament round-result branch selected notice code 2."),
                    _ => new TournamentClientMessage(0x3A5, string.Format(CultureInfo.InvariantCulture, "Tournament round-result branch selected code {0}.", noticeCode))
                };

            string summary = branch == 0
                ? $"notice (374) blocked-branch code={noticeCode}"
                : $"notice (374) round-branch code={noticeCode}";
            SetStatus(FormatStringPoolMessage(message), currentTimeMs, new[] { message.StringPoolId }, summary);
        }

        private void ApplyMatchTable(byte[] payload, int currentTimeMs)
        {
            SetStatus(
                $"Tournament match table payload received ({payload.Length} byte(s)); the client opens CMatchTableDlg directly from opcode 375.",
                currentTimeMs,
                Array.Empty<int>(),
                $"match-table (375) bytes={payload.Length}");
        }

        private void ApplyPrize(BinaryReader reader, int currentTimeMs)
        {
            byte prizeCode = reader.ReadByte();
            bool hasItems = reader.ReadByte() != 0;
            if (hasItems)
            {
                int firstItemId = reader.ReadInt32();
                int secondItemId = reader.ReadInt32();
                TournamentClientMessage message = new(0x3AA, $"Tournament prize notice awarded item ids {firstItemId} and {secondItemId}.");
                SetStatus(
                    FormatStringPoolMessage(message),
                    currentTimeMs,
                    new[] { message.StringPoolId },
                    $"set-prize (376) code={prizeCode} items={firstItemId},{secondItemId}");
                return;
            }

            TournamentClientMessage fallbackMessage = prizeCode != 0
                ? new TournamentClientMessage(0x3A8, $"Tournament prize notice without items selected branch code {prizeCode}.")
                : new TournamentClientMessage(0x3A9, "Tournament prize notice reported no item rewards for the local branch.");

            SetStatus(
                FormatStringPoolMessage(fallbackMessage),
                currentTimeMs,
                new[] { fallbackMessage.StringPoolId },
                $"set-prize (376) code={prizeCode} items=none");
        }

        private void ApplyUew(BinaryReader reader, int currentTimeMs)
        {
            byte uewCode = reader.ReadByte();
            TournamentClientMessage? message = uewCode switch
            {
                2 => new TournamentClientMessage(0x9F8, "Tournament UEW branch reported code 2."),
                4 => new TournamentClientMessage(0x9F7, "Tournament UEW branch reported code 4."),
                8 or 16 => new TournamentClientMessage(0x9F6, string.Format(CultureInfo.InvariantCulture, "Tournament UEW branch reported code {0}.", uewCode)),
                _ => null
            };

            string fallback = message.HasValue
                ? FormatStringPoolMessage(message.Value)
                : $"Tournament UEW packet reported code {uewCode}.";
            int[] ids = message.HasValue ? new[] { message.Value.StringPoolId } : Array.Empty<int>();
            SetStatus(fallback, currentTimeMs, ids, $"uew (377) code={uewCode}");
        }

        private void SetStatus(string text, int currentTimeMs, IReadOnlyList<int> stringPoolIds, string packetSummary)
        {
            _statusMessage = text;
            _statusMessageUntil = currentTimeMs + StatusDurationMs;
            _lastPacketSummary = packetSummary;
            _lastStringPoolIds = stringPoolIds?.Where(id => id > 0).Distinct().ToArray() ?? Array.Empty<int>();
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                374 => "tournament (374)",
                375 => "matchtable (375)",
                376 => "setprize (376)",
                377 => "uew (377)",
                378 => "noop (378)",
                _ => packetType.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static string FormatStringPoolMessage(TournamentClientMessage definition)
        {
            return $"{definition.FallbackText} [StringPool 0x{definition.StringPoolId:X}]";
        }

        private static void EnsurePacketConsumed(Stream stream, string packetLabel)
        {
            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException($"Unexpected trailing bytes in Tournament {packetLabel} payload.");
            }
        }

        private static void DrawShadowedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color, float scale = 1f)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 shadowOffset = new Vector2(1f, 1f);
            spriteBatch.DrawString(font, text, position + shadowOffset, Color.Black * 0.75f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private static string TrimForDisplay(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }

            return text[..Math.Max(0, maxLength - 3)] + "...";
        }
    }
}
