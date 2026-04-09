using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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

    public enum TournamentLifecyclePhase
    {
        Lobby = 0,
        EntryGate = 1,
        MatchTable = 2,
        RestPeriod = 3,
        PrizePodium = 4,
        SessionNotice = 5
    }

    internal readonly record struct TournamentClientMessage(int StringPoolId, string FallbackText);

    public sealed class TournamentField
    {
        private const int StatusDurationMs = 9000;
        private const int PostFinalExitGraceMs = 5 * 60 * 1000;
        private const string MatchTableDialogOwner = "CMatchTableDlg";
        private const string TournamentContractSummary = "32-player bracket, resting period between rounds, prize podium after finals, five-minute exit grace.";

        private int _mapId;
        private bool _isActive;
        private int _lastPacketType;
        private string _statusMessage;
        private int _statusMessageUntil;
        private string _lastPacketSummary;
        private string _lastPayloadHex;
        private string _lastDialogOwner;
        private int[] _lastStringPoolIds = Array.Empty<int>();
        private int[] _lastPrizeItemIds = Array.Empty<int>();
        private TournamentLifecyclePhase _lifecyclePhase;
        private string _lifecycleSummary;
        private int? _podiumExitDeadlineTick;
        private readonly TournamentMatchTableDialogState _matchTableDialog = new();

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public int LastPacketType => _lastPacketType;
        public string CurrentStatusMessage => _statusMessage;
        public string LastPacketSummary => _lastPacketSummary;
        public string LastDialogOwner => _lastDialogOwner;
        public IReadOnlyList<int> LastStringPoolIds => _lastStringPoolIds;
        public IReadOnlyList<int> LastPrizeItemIds => _lastPrizeItemIds;
        public TournamentLifecyclePhase LifecyclePhase => _lifecyclePhase;
        public TournamentMatchTableDialogState MatchTableDialog => _matchTableDialog;

        public void Configure(MapInfo mapInfo)
        {
            Reset();
            _mapId = mapInfo?.id ?? 0;
            _isActive = mapInfo?.fieldType == FieldType.FIELDTYPE_TOURNAMENT;
            if (_isActive)
            {
                SetLifecyclePhase(
                    TournamentLifecyclePhase.Lobby,
                    "String/Map.img/victoria/109070000/help0 keeps the Tournament wrapper anchored to the bracket lobby, resting period, prize podium, and five-minute post-finals exit flow.");
                _statusMessage = $"Tournament wrapper ready for map {_mapId} ({TournamentContractSummary})";
                _statusMessageUntil = Environment.TickCount + StatusDurationMs;
            }
        }

        public void Update(int tickCount)
        {
            if (_statusMessage != null && tickCount >= _statusMessageUntil)
            {
                _statusMessage = null;
            }

            if (_podiumExitDeadlineTick.HasValue && tickCount >= _podiumExitDeadlineTick.Value)
            {
                _podiumExitDeadlineTick = null;
            }

            _matchTableDialog.Update(tickCount);
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || spriteBatch == null || pixelTexture == null || font == null)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            const int panelWidth = 388;
            const int panelHeight = 164;
            int panelX = viewport.Width - panelWidth - 18;
            int panelY = 18;

            spriteBatch.Draw(pixelTexture, new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(18, 23, 33, 225));
            spriteBatch.Draw(pixelTexture, new Rectangle(panelX, panelY, panelWidth, 30), new Color(82, 59, 44, 255));
            DrawShadowedText(spriteBatch, font, "Tournament", new Vector2(panelX + 12, panelY + 7), Color.White);

            DrawShadowedText(
                spriteBatch,
                font,
                $"map={_mapId} | packets=374-378 | owner=CField_Tournament | phase={GetLifecyclePhaseLabel()}",
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

            string dialogText = string.IsNullOrWhiteSpace(_lastDialogOwner)
                ? "dialog: none"
                : $"dialog: {_lastDialogOwner}";
            DrawShadowedText(spriteBatch, font, TrimForDisplay(dialogText, 52), new Vector2(panelX + 12, panelY + 100), Color.Silver, 0.85f);

            DrawShadowedText(
                spriteBatch,
                font,
                TrimForDisplay(BuildLifecycleStatusText(Environment.TickCount), 72),
                new Vector2(panelX + 12, panelY + 120),
                Color.LightSteelBlue,
                0.82f);

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                DrawShadowedText(spriteBatch, font, TrimForDisplay(_statusMessage, 72), new Vector2(panelX + 12, panelY + 140), Color.LightGoldenrodYellow, 0.82f);
            }

            _matchTableDialog.Draw(spriteBatch, pixelTexture, font);
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
            string dialogText = string.IsNullOrWhiteSpace(_lastDialogOwner) ? "none" : _lastDialogOwner;
            string summary = string.IsNullOrWhiteSpace(_lastPacketSummary) ? "No packet applied yet." : _lastPacketSummary;
            string matchTableText = _matchTableDialog.DescribeStatus();
            string phaseText = BuildLifecycleStatusText(Environment.TickCount);
            return $"Tournament: active | map={_mapId} | phase={GetLifecyclePhaseLabel()} | contract={TournamentContractSummary} | last={packetText} | dialog={dialogText} | stringPool={stringPoolText} | summary={summary} | lifecycle={phaseText}{Environment.NewLine}{matchTableText}";
        }

        public string DescribeMatchTableDialog()
        {
            return _matchTableDialog.DescribeStatus();
        }

        public bool TryScrollMatchTableDialog(int delta, out string message)
        {
            return _matchTableDialog.TryScroll(delta, out message);
        }

        public bool HandleMatchTableDialogMouse(
            Point mousePosition,
            int viewportWidth,
            int viewportHeight,
            int scrollWheelDelta,
            bool leftClickReleased,
            out string message)
        {
            return _matchTableDialog.HandleMouse(mousePosition, viewportWidth, viewportHeight, scrollWheelDelta, leftClickReleased, out message);
        }

        public void CloseMatchTableDialog()
        {
            _matchTableDialog.Close("Tournament match-table dialog closed locally.");
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
            _lastDialogOwner = null;
            _lastStringPoolIds = Array.Empty<int>();
            _lastPrizeItemIds = Array.Empty<int>();
            _lifecyclePhase = TournamentLifecyclePhase.Lobby;
            _lifecycleSummary = null;
            _podiumExitDeadlineTick = null;
            _matchTableDialog.Reset();
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
                ? $"notice (374) blocked-entry code={noticeCode}"
                : $"notice (374) round-result code={noticeCode}";
            SetLifecyclePhase(
                branch == 0 ? TournamentLifecyclePhase.EntryGate : TournamentLifecyclePhase.RestPeriod,
                branch == 0
                    ? $"Blocked entry branch {noticeCode} stayed inside the Tournament lobby gate."
                    : "Round-result notice entered the resting-period seam described by String/Map.img/victoria/109070000/help0.");
            SetStatus(
                branch == 0
                    ? FormatStringPoolMessage(message)
                    : noticeCode switch
                    {
                        1 => FormatStringPoolMessage(message),
                        2 => FormatStringPoolMessage(message),
                        _ => FormatStringPoolMessage(message, noticeCode)
                    },
                currentTimeMs,
                new[] { message.StringPoolId },
                summary,
                "CUtilDlg::Notice");
        }

        private void ApplyMatchTable(byte[] payload, int currentTimeMs)
        {
            if (!_matchTableDialog.TryOpen(payload, out string dialogSummary, out string errorMessage))
            {
                throw new InvalidDataException(errorMessage);
            }

            SetLifecyclePhase(
                TournamentLifecyclePhase.MatchTable,
                $"CField_Tournament::OnTournamentMatchTable opened {MatchTableDialogOwner} for the live bracket preview.");
            SetStatus(
                dialogSummary,
                currentTimeMs,
                Array.Empty<int>(),
                $"match-table (375) bytes={payload.Length}",
                $"{MatchTableDialogOwner}::DoModal");
        }

        private void ApplyPrize(BinaryReader reader, int currentTimeMs)
        {
            byte prizeCode = reader.ReadByte();
            bool hasItems = reader.ReadByte() != 0;
            _lastPrizeItemIds = Array.Empty<int>();
            _podiumExitDeadlineTick = unchecked(currentTimeMs + PostFinalExitGraceMs);
            SetLifecyclePhase(
                TournamentLifecyclePhase.PrizePodium,
                "Tournament prize flow moved the finalists to the podium and armed the five-minute exit grace from String/Map.img/victoria/109070000/help0.");
            if (hasItems)
            {
                int firstItemId = reader.ReadInt32();
                int secondItemId = reader.ReadInt32();
                _lastPrizeItemIds = new[] { firstItemId, secondItemId };
                string firstItemName = ResolveItemName(firstItemId);
                string secondItemName = ResolveItemName(secondItemId);
                TournamentClientMessage message = new(
                    0x3AA,
                    $"Tournament prize notice awarded {FormatItemLabel(firstItemId, firstItemName)} and {FormatItemLabel(secondItemId, secondItemName)}.");
                SetStatus(
                    FormatStringPoolMessage(message),
                    currentTimeMs,
                    new[] { message.StringPoolId },
                    $"set-prize (376) code={prizeCode} items={FormatSummaryItemList(_lastPrizeItemIds)}",
                    "CUtilDlg::Notice");
                return;
            }

            TournamentClientMessage fallbackMessage = prizeCode != 0
                ? new TournamentClientMessage(0x3A8, $"Tournament prize notice without items selected branch code {prizeCode}.")
                : new TournamentClientMessage(0x3A9, "Tournament prize notice reported no item rewards for the local branch.");

            SetStatus(
                FormatStringPoolMessage(fallbackMessage),
                currentTimeMs,
                new[] { fallbackMessage.StringPoolId },
                $"set-prize (376) code={prizeCode} items=none",
                "CUtilDlg::Notice");
        }

        private void ApplyUew(BinaryReader reader, int currentTimeMs)
        {
            byte uewCode = reader.ReadByte();
            SetLifecyclePhase(
                TournamentLifecyclePhase.SessionNotice,
                $"Tournament UEW branch reported client notice code {uewCode}.");
            TournamentClientMessage? message = uewCode switch
            {
                2 => new TournamentClientMessage(0x9F8, "Tournament UEW branch reported code 2."),
                4 => new TournamentClientMessage(0x9F7, "Tournament UEW branch reported code 4."),
                8 or 16 => new TournamentClientMessage(0x9F6, string.Format(CultureInfo.InvariantCulture, "Tournament UEW branch reported code {0}.", uewCode)),
                _ => null
            };

            string fallback = message.HasValue
                ? (uewCode is 8 or 16
                    ? FormatStringPoolMessage(message.Value, uewCode)
                    : FormatStringPoolMessage(message.Value))
                : $"Tournament UEW packet reported code {uewCode}.";
            int[] ids = message.HasValue ? new[] { message.Value.StringPoolId } : Array.Empty<int>();
            SetStatus(fallback, currentTimeMs, ids, $"uew (377) code={uewCode}", "CUtilDlg::Notice");
        }

        private void SetStatus(string text, int currentTimeMs, IReadOnlyList<int> stringPoolIds, string packetSummary, string dialogOwner = null)
        {
            _statusMessage = text;
            _statusMessageUntil = currentTimeMs + StatusDurationMs;
            _lastPacketSummary = packetSummary;
            _lastDialogOwner = dialogOwner;
            _lastStringPoolIds = stringPoolIds?.Where(id => id > 0).Distinct().ToArray() ?? Array.Empty<int>();
        }

        private void SetLifecyclePhase(TournamentLifecyclePhase phase, string summary)
        {
            _lifecyclePhase = phase;
            _lifecycleSummary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        }

        private string BuildLifecycleStatusText(int currentTimeMs)
        {
            string phaseText = string.IsNullOrWhiteSpace(_lifecycleSummary)
                ? GetLifecyclePhaseLabel()
                : _lifecycleSummary;

            if (_lifecyclePhase != TournamentLifecyclePhase.PrizePodium || !_podiumExitDeadlineTick.HasValue)
            {
                return phaseText;
            }

            int remainingMs = _podiumExitDeadlineTick.Value - currentTimeMs;
            if (remainingMs <= 0)
            {
                return $"{phaseText} Exit grace elapsed locally.";
            }

            int totalSeconds = (int)Math.Ceiling(remainingMs / 1000d);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{phaseText} Exit grace {minutes}:{seconds:D2} remaining.";
        }

        private string GetLifecyclePhaseLabel()
        {
            return _lifecyclePhase switch
            {
                TournamentLifecyclePhase.EntryGate => "entry-gate",
                TournamentLifecyclePhase.MatchTable => "match-table",
                TournamentLifecyclePhase.RestPeriod => "rest-period",
                TournamentLifecyclePhase.PrizePodium => "prize-podium",
                TournamentLifecyclePhase.SessionNotice => "session-notice",
                _ => "lobby"
            };
        }

        private static string FormatSummaryItemList(IEnumerable<int> itemIds)
        {
            int[] items = itemIds?.Where(id => id > 0).ToArray() ?? Array.Empty<int>();
            return items.Length == 0
                ? "none"
                : string.Join(",", items);
        }

        private static string FormatItemLabel(int itemId, string itemName)
        {
            return string.IsNullOrWhiteSpace(itemName)
                ? $"item {itemId}"
                : $"{itemName} ({itemId})";
        }

        private static string ResolveItemName(int itemId)
        {
            if (itemId <= 0
                || Program.InfoManager?.ItemNameCache == null
                || !Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo))
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(itemInfo.Item2) ? null : itemInfo.Item2.Trim();
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

        private static string FormatStringPoolMessage(TournamentClientMessage definition, params object[] args)
        {
            string format = GetTournamentCompositeFormat(definition.StringPoolId, definition.FallbackText);
            string text = args == null || args.Length == 0
                ? format
                : string.Format(CultureInfo.InvariantCulture, format, args);
            return $"{text} [StringPool 0x{definition.StringPoolId:X}]";
        }

        private static string GetTournamentCompositeFormat(int stringPoolId, string fallbackText)
        {
            if (!MapleStoryStringPool.TryGet(stringPoolId, out string text))
            {
                return fallbackText;
            }

            return text.Replace("%n", "{0}", StringComparison.Ordinal);
        }

        private static void EnsurePacketConsumed(Stream stream, string packetLabel)
        {
            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException($"Unexpected trailing bytes in Tournament {packetLabel} payload.");
            }
        }

        internal static void DrawShadowedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color, float scale = 1f)
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

    public sealed class TournamentMatchTableDialogState
    {
        private const int RawTableByteCount = 0x300;
        private const int EntrantCount = 8;
        private const int EntryValueCount = 6;
        private const int MinPayloadLength = RawTableByteCount + 1;
        private const int DialogWidth = 758;
        private const int DialogHeight = 470;
        private const int MatchTableWidth = 714;
        private const int MatchTableHeight = 406;
        private const int DrawOffsetX = 22;
        private const int DrawOffsetY = 38;
        private const int AvatarLineOffsetY = 28;
        private const int HeaderHeight = 28;
        private const int CloseButtonWidth = 36;
        private const int CloseButtonHeight = 18;
        private const int ScrollButtonWidth = 24;
        private const int ScrollButtonHeight = 18;
        private const int MatchTableStatusMin = 2;
        private const int MatchTableStatusMax = 5;
        private const int MaxScroll = 2;

        private static readonly Point[] RoundOneNamePoints =
        {
            new(42, 393),
            new(125, 393),
            new(216, 393),
            new(299, 393),
            new(390, 393),
            new(473, 393),
            new(564, 393),
            new(647, 393)
        };

        private static readonly Point[] RoundTwoNamePoints =
        {
            new(81, 289),
            new(256, 289),
            new(431, 289),
            new(606, 289)
        };

        private static readonly Point[] SemiFinalNamePoints =
        {
            new(277, 185),
            new(411, 185)
        };

        private static readonly Point ChampionNamePoint = new(341, 81);
        private static readonly Point[] RoundOneDebugPoints =
        {
            new(42, 410),
            new(125, 410),
            new(216, 410),
            new(299, 410),
            new(390, 410),
            new(473, 410),
            new(564, 410),
            new(647, 410)
        };

        private readonly int[,] _matchValues = new int[EntrantCount, EntryValueCount];
        private readonly string[] _slotNames = new string[EntrantCount];
        private byte[] _rawTable = Array.Empty<byte>();

        public bool IsVisible { get; private set; }
        public byte Stage { get; private set; }
        public int Scroll { get; private set; } = MaxScroll;
        public int PayloadLength { get; private set; }
        public string Summary { get; private set; }
        public IReadOnlyList<string> SlotNames => _slotNames;

        public void Reset()
        {
            IsVisible = false;
            Stage = 0;
            Scroll = MaxScroll;
            PayloadLength = 0;
            Summary = null;
            _rawTable = Array.Empty<byte>();
            Array.Clear(_matchValues, 0, _matchValues.Length);
            Array.Fill(_slotNames, string.Empty);
        }

        public void Update(int tickCount)
        {
        }

        public bool TryOpen(byte[] payload, out string summary, out string errorMessage)
        {
            summary = null;
            errorMessage = null;

            if (payload == null || payload.Length < MinPayloadLength)
            {
                errorMessage = $"Tournament match-table payload must be at least {MinPayloadLength} byte(s); received {payload?.Length ?? 0}.";
                return false;
            }

            PayloadLength = payload.Length;
            _rawTable = payload.Take(RawTableByteCount).ToArray();
            Stage = payload[RawTableByteCount];
            Scroll = MaxScroll;
            IsVisible = true;

            DecodeMatchValues();
            DecodeSlotNames();

            string firstNames = string.Join(", ", _slotNames.Where(name => !string.IsNullOrWhiteSpace(name)).Take(4));
            if (string.IsNullOrWhiteSpace(firstNames))
            {
                firstNames = "no printable entrant names recovered";
            }

            Summary = $"Tournament match table opened in a dedicated {MatchTableDialogOwnerText} dialog ({PayloadLength} byte(s), stage={Stage}, scroll={Scroll}, preview={firstNames}).";
            summary = Summary;
            return true;
        }

        public void Close(string reason = null)
        {
            IsVisible = false;
            Summary = string.IsNullOrWhiteSpace(reason) ? "Tournament match-table dialog is closed." : reason;
        }

        public bool TryScroll(int delta, out string message)
        {
            if (!IsVisible)
            {
                message = "Tournament match-table dialog is not open.";
                return false;
            }

            int nextScroll = Math.Clamp(Scroll + delta, 0, MaxScroll);
            Scroll = nextScroll;
            message = $"Tournament match-table dialog scroll set to {Scroll}.";
            return true;
        }

        public string DescribeStatus()
        {
            if (!IsVisible)
            {
                return "Tournament match-table dialog: closed.";
            }

            string names = string.Join(", ", _slotNames.Where(name => !string.IsNullOrWhiteSpace(name)).Take(4));
            if (string.IsNullOrWhiteSpace(names))
            {
                names = "none recovered";
            }

            return $"Tournament match-table dialog: open | payload={PayloadLength} bytes | stage={Stage} | scroll={Scroll} | entrants={names}";
        }

        public bool HandleMouse(
            Point mousePosition,
            int viewportWidth,
            int viewportHeight,
            int scrollWheelDelta,
            bool leftClickReleased,
            out string message)
        {
            message = null;
            if (!IsVisible)
            {
                return false;
            }

            GetLayout(viewportWidth, viewportHeight, out Rectangle panelRect, out Rectangle closeButtonRect, out Rectangle upButtonRect, out Rectangle downButtonRect);
            if (!panelRect.Contains(mousePosition))
            {
                return false;
            }

            if (scrollWheelDelta != 0)
            {
                int step = Math.Sign(scrollWheelDelta);
                return TryScroll(-step, out message);
            }

            if (!leftClickReleased)
            {
                message = null;
                return true;
            }

            if (closeButtonRect.Contains(mousePosition))
            {
                Close("Tournament match-table dialog closed via CMatchTableDlg::BtClose.");
                message = Summary;
                return true;
            }

            if (upButtonRect.Contains(mousePosition))
            {
                return TryScroll(-1, out message);
            }

            if (downButtonRect.Contains(mousePosition))
            {
                return TryScroll(1, out message);
            }

            message = "Tournament match-table dialog captured the click.";
            return true;
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!IsVisible || spriteBatch == null || pixelTexture == null || font == null)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            GetLayout(viewport.Width, viewport.Height, out Rectangle panelRect, out Rectangle closeButtonRect, out Rectangle upButtonRect, out Rectangle downButtonRect, out float scale);
            int panelX = panelRect.X;
            int panelY = panelRect.Y;
            int scaledWidth = panelRect.Width;
            int scaledHeight = panelRect.Height;

            DrawFilled(spriteBatch, pixelTexture, new Rectangle(panelX, panelY, scaledWidth, scaledHeight), new Color(14, 18, 27, 238));
            DrawBorder(spriteBatch, pixelTexture, new Rectangle(panelX, panelY, scaledWidth, scaledHeight), new Color(205, 182, 119, 255));
            DrawFilled(spriteBatch, pixelTexture, new Rectangle(panelX, panelY, scaledWidth, Scale(HeaderHeight, scale)), new Color(79, 57, 39, 255));

            float titleScale = Math.Max(0.82f, 0.92f * scale);
            TournamentField.DrawShadowedText(spriteBatch, font, "Tournament Match Table", new Vector2(panelX + Scale(12, scale), panelY + Scale(6, scale)), Color.White, titleScale);

            DrawButton(spriteBatch, pixelTexture, font, closeButtonRect, "X", true, scale);
            DrawButton(spriteBatch, pixelTexture, font, upButtonRect, "Up", Scroll > 0, Math.Max(0.68f, 0.78f * scale));
            DrawButton(spriteBatch, pixelTexture, font, downButtonRect, "Dn", Scroll < MaxScroll, Math.Max(0.68f, 0.78f * scale));

            Rectangle tableRect = new(
                panelX + Scale(DrawOffsetX, scale),
                panelY + Scale(DrawOffsetY, scale),
                Scale(MatchTableWidth, scale),
                Scale(MatchTableHeight, scale));
            DrawFilled(spriteBatch, pixelTexture, tableRect, new Color(35, 41, 56, 240));
            DrawBorder(spriteBatch, pixelTexture, tableRect, new Color(139, 168, 201, 210));

            Rectangle stageRect = new(tableRect.X, tableRect.Y, tableRect.Width, Scale(28, scale));
            DrawFilled(spriteBatch, pixelTexture, stageRect, ResolveStageColor());
            TournamentField.DrawShadowedText(
                spriteBatch,
                font,
                $"stage={Stage} | scroll={Scroll} | client owner={MatchTableDialogOwnerText}",
                new Vector2(tableRect.X + Scale(10, scale), tableRect.Y + Scale(6, scale)),
                Color.White,
                Math.Max(0.7f, 0.82f * scale));

            DrawBracketSkeleton(spriteBatch, pixelTexture, tableRect, scale);
            DrawRoundOne(spriteBatch, font, tableRect, scale);
            DrawRoundSummary(spriteBatch, font, tableRect, scale);
        }

        private void GetLayout(
            int viewportWidth,
            int viewportHeight,
            out Rectangle panelRect,
            out Rectangle closeButtonRect,
            out Rectangle upButtonRect,
            out Rectangle downButtonRect)
        {
            GetLayout(viewportWidth, viewportHeight, out panelRect, out closeButtonRect, out upButtonRect, out downButtonRect, out _);
        }

        private void GetLayout(
            int viewportWidth,
            int viewportHeight,
            out Rectangle panelRect,
            out Rectangle closeButtonRect,
            out Rectangle upButtonRect,
            out Rectangle downButtonRect,
            out float scale)
        {
            scale = Math.Min(1f, Math.Min((viewportWidth - 48f) / DialogWidth, (viewportHeight - 64f) / DialogHeight));
            scale = Math.Max(0.72f, scale);

            int scaledWidth = Scale(DialogWidth, scale);
            int scaledHeight = Scale(DialogHeight, scale);
            int panelX = Math.Max(18, (viewportWidth - scaledWidth) / 2);
            int panelY = Math.Max(18, (viewportHeight - scaledHeight) / 2);
            panelRect = new Rectangle(panelX, panelY, scaledWidth, scaledHeight);
            closeButtonRect = new Rectangle(panelX + Scale(710, scale), panelY + Scale(9, scale), Scale(CloseButtonWidth, scale), Scale(CloseButtonHeight, scale));
            upButtonRect = new Rectangle(panelX + Scale(729, scale), panelY + Scale(36, scale), Scale(ScrollButtonWidth, scale), Scale(ScrollButtonHeight, scale));
            downButtonRect = new Rectangle(panelX + Scale(729, scale), panelY + Scale(433, scale), Scale(ScrollButtonWidth, scale), Scale(ScrollButtonHeight, scale));
        }

        private void DecodeMatchValues()
        {
            Array.Clear(_matchValues, 0, _matchValues.Length);

            int bytesToDecode = Math.Min(_rawTable.Length, EntrantCount * EntryValueCount * sizeof(int));
            for (int slotIndex = 0; slotIndex < EntrantCount; slotIndex++)
            {
                for (int valueIndex = 0; valueIndex < EntryValueCount; valueIndex++)
                {
                    int byteOffset = (slotIndex * EntryValueCount + valueIndex) * sizeof(int);
                    if (byteOffset + sizeof(int) > bytesToDecode)
                    {
                        return;
                    }

                    _matchValues[slotIndex, valueIndex] = BitConverter.ToInt32(_rawTable, byteOffset);
                }
            }
        }

        private void DecodeSlotNames()
        {
            Array.Fill(_slotNames, string.Empty);

            List<string> candidates = ExtractLikelyNames(_rawTable)
                .Distinct(StringComparer.Ordinal)
                .Take(EntrantCount)
                .ToList();

            for (int index = 0; index < EntrantCount; index++)
            {
                _slotNames[index] = index < candidates.Count
                    ? candidates[index]
                    : $"Slot {index + 1}";
            }
        }

        private static IEnumerable<string> ExtractLikelyNames(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                yield break;
            }

            foreach (string ascii in ExtractPrintableAscii(data))
            {
                yield return ascii;
            }

            foreach (string unicode in ExtractPrintableUnicode(data))
            {
                yield return unicode;
            }
        }

        private static IEnumerable<string> ExtractPrintableAscii(byte[] data)
        {
            StringBuilder builder = new();
            foreach (byte value in data)
            {
                if (value is >= 32 and <= 126)
                {
                    builder.Append((char)value);
                    continue;
                }

                if (builder.Length >= 3)
                {
                    string candidate = builder.ToString().Trim();
                    if (LooksLikeName(candidate))
                    {
                        yield return candidate;
                    }
                }

                builder.Clear();
            }

            if (builder.Length >= 3)
            {
                string candidate = builder.ToString().Trim();
                if (LooksLikeName(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> ExtractPrintableUnicode(byte[] data)
        {
            StringBuilder builder = new();
            for (int index = 0; index + 1 < data.Length; index += 2)
            {
                char value = BitConverter.ToChar(data, index);
                if (value is >= ' ' and <= '~')
                {
                    builder.Append(value);
                    continue;
                }

                if (builder.Length >= 3)
                {
                    string candidate = builder.ToString().Trim();
                    if (LooksLikeName(candidate))
                    {
                        yield return candidate;
                    }
                }

                builder.Clear();
            }

            if (builder.Length >= 3)
            {
                string candidate = builder.ToString().Trim();
                if (LooksLikeName(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static bool LooksLikeName(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 18)
            {
                return false;
            }

            int alphaNumericCount = candidate.Count(char.IsLetterOrDigit);
            return alphaNumericCount >= 3 && candidate.Any(char.IsLetter);
        }

        private void DrawBracketSkeleton(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle tableRect, float scale)
        {
            Color activeLine = new(204, 214, 239, 220);
            Color inactiveLine = new(88, 97, 122, 180);

            Point[] roundOne = RoundOneNamePoints.Select(point => ScaleAndOffset(point, tableRect, scale)).ToArray();
            Point[] roundTwo = RoundTwoNamePoints.Select(point => ScaleAndOffset(point, tableRect, scale)).ToArray();
            Point[] semiFinal = SemiFinalNamePoints.Select(point => ScaleAndOffset(point, tableRect, scale)).ToArray();
            Point champion = ScaleAndOffset(ChampionNamePoint, tableRect, scale);

            for (int pairIndex = 0; pairIndex < 4; pairIndex++)
            {
                Color lineColor = Stage > 2 ? activeLine : inactiveLine;
                DrawBracketPair(spriteBatch, pixelTexture, roundOne[pairIndex * 2], roundOne[pairIndex * 2 + 1], roundTwo[pairIndex], lineColor, scale);
            }

            for (int pairIndex = 0; pairIndex < 2; pairIndex++)
            {
                Color lineColor = Stage > 3 ? activeLine : inactiveLine;
                DrawBracketPair(spriteBatch, pixelTexture, roundTwo[pairIndex * 2], roundTwo[pairIndex * 2 + 1], semiFinal[pairIndex], lineColor, scale);
            }

            DrawBracketPair(spriteBatch, pixelTexture, semiFinal[0], semiFinal[1], champion, Stage > 4 ? activeLine : inactiveLine, scale);
        }

        private void DrawRoundOne(SpriteBatch spriteBatch, SpriteFont font, Rectangle tableRect, float scale)
        {
            float nameScale = Math.Max(0.56f, 0.72f * scale);
            float debugScale = Math.Max(0.48f, 0.58f * scale);
            Color currentColor = new(255, 247, 212);
            Color normalColor = Color.Gainsboro;
            Color debugColor = new(182, 191, 209);

            for (int slotIndex = 0; slotIndex < EntrantCount; slotIndex++)
            {
                Vector2 namePosition = ToVector2(ScaleAndOffset(RoundOneNamePoints[slotIndex], tableRect, scale));
                DrawCenteredText(spriteBatch, font, _slotNames[slotIndex], namePosition, normalColor, nameScale);

                string dataSummary = $"[{_matchValues[slotIndex, 0]},{_matchValues[slotIndex, 1]},{_matchValues[slotIndex, 4]}]";
                Vector2 debugPosition = ToVector2(ScaleAndOffset(RoundOneDebugPoints[slotIndex], tableRect, scale));
                DrawCenteredText(spriteBatch, font, dataSummary, debugPosition, debugColor, debugScale);
            }

            DrawCenteredText(spriteBatch, font, ResolveRoundLabel(1), ToVector2(ScaleAndOffset(new Point(83, 362), tableRect, scale)), currentColor, nameScale);
            DrawCenteredText(spriteBatch, font, ResolveRoundLabel(2), ToVector2(ScaleAndOffset(new Point(255, 258), tableRect, scale)), currentColor, nameScale);
            DrawCenteredText(spriteBatch, font, ResolveRoundLabel(3), ToVector2(ScaleAndOffset(new Point(341, 154), tableRect, scale)), currentColor, nameScale);
            DrawCenteredText(spriteBatch, font, ResolveRoundLabel(4), ToVector2(ScaleAndOffset(new Point(341, 50), tableRect, scale)), currentColor, nameScale);
        }

        private void DrawRoundSummary(SpriteBatch spriteBatch, SpriteFont font, Rectangle tableRect, float scale)
        {
            float summaryScale = Math.Max(0.56f, 0.66f * scale);
            Color summaryColor = Color.WhiteSmoke;

            string[] roundTwo = BuildRoundSummaries(0, 4, 2);
            string[] semiFinal = BuildRoundSummaries(4, 2, 1);
            string champion = BuildChampionSummary();

            for (int index = 0; index < roundTwo.Length; index++)
            {
                DrawCenteredText(
                    spriteBatch,
                    font,
                    roundTwo[index],
                    ToVector2(ScaleAndOffset(RoundTwoNamePoints[index], tableRect, scale)),
                    Stage > 2 ? summaryColor : Color.SlateGray,
                    summaryScale);
            }

            for (int index = 0; index < semiFinal.Length; index++)
            {
                DrawCenteredText(
                    spriteBatch,
                    font,
                    semiFinal[index],
                    ToVector2(ScaleAndOffset(SemiFinalNamePoints[index], tableRect, scale)),
                    Stage > 3 ? summaryColor : Color.SlateGray,
                    summaryScale);
            }

            DrawCenteredText(
                spriteBatch,
                font,
                champion,
                ToVector2(ScaleAndOffset(ChampionNamePoint, tableRect, scale)),
                Stage > 4 ? Color.White : Color.SlateGray,
                summaryScale);
        }

        private string[] BuildRoundSummaries(int keyIndex, int pairCount, int nextIndex)
        {
            string[] result = new string[pairCount];
            for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
            {
                int leftSlot = pairIndex * 2;
                int rightSlot = leftSlot + 1;
                string leftName = leftSlot < _slotNames.Length ? _slotNames[leftSlot] : string.Empty;
                string rightName = rightSlot < _slotNames.Length ? _slotNames[rightSlot] : string.Empty;
                int leftKey = keyIndex < EntryValueCount ? _matchValues[leftSlot, keyIndex] : 0;
                int rightKey = keyIndex < EntryValueCount ? _matchValues[rightSlot, keyIndex] : 0;
                int nextKey = nextIndex < EntryValueCount && pairIndex < EntrantCount ? _matchValues[Math.Min(pairIndex, EntrantCount - 1), nextIndex] : 0;
                result[pairIndex] = ResolvePairSummary(leftName, rightName, leftKey, rightKey, nextKey);
            }

            return result;
        }

        private string BuildChampionSummary()
        {
            string left = SemiFinalNamePoints.Length > 0 ? BuildRoundSummaries(4, 2, 5)[0] : "Pending";
            string right = SemiFinalNamePoints.Length > 1 ? BuildRoundSummaries(4, 2, 5)[1] : "Pending";
            int leftKey = _matchValues[0, 5];
            int rightKey = _matchValues[1, 5];
            return ResolvePairSummary(left, right, leftKey, rightKey, 0);
        }

        private static string ResolvePairSummary(string leftName, string rightName, int leftKey, int rightKey, int nextKey)
        {
            if (nextKey > 0)
            {
                if (leftKey == nextKey && !string.IsNullOrWhiteSpace(leftName))
                {
                    return leftName;
                }

                if (rightKey == nextKey && !string.IsNullOrWhiteSpace(rightName))
                {
                    return rightName;
                }
            }

            if (leftKey > 0 && rightKey <= 0 && !string.IsNullOrWhiteSpace(leftName))
            {
                return leftName;
            }

            if (rightKey > 0 && leftKey <= 0 && !string.IsNullOrWhiteSpace(rightName))
            {
                return rightName;
            }

            if (!string.IsNullOrWhiteSpace(leftName) && !string.IsNullOrWhiteSpace(rightName))
            {
                return $"{TrimLabel(leftName, 8)}/{TrimLabel(rightName, 8)}";
            }

            return "Pending";
        }

        private Color ResolveStageColor()
        {
            return Stage switch
            {
                <= 1 => new Color(98, 84, 126, 220),
                2 => new Color(69, 111, 159, 220),
                3 => new Color(75, 131, 106, 220),
                4 => new Color(160, 119, 56, 220),
                _ => new Color(168, 78, 63, 220)
            };
        }

        private string ResolveRoundLabel(int round)
        {
            return round switch
            {
                1 => "Round of 8",
                2 => "Final 4",
                3 => "Semi Final",
                _ => "Champion"
            };
        }

        private static string TrimLabel(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            {
                return text;
            }

            return text[..Math.Max(1, maxLength - 1)] + "…";
        }

        private static void DrawBracketPair(SpriteBatch spriteBatch, Texture2D pixelTexture, Point left, Point right, Point target, Color color, float scale)
        {
            int thickness = Math.Max(1, Scale(2, scale));
            int middleX = (left.X + target.X) / 2;
            DrawFilled(spriteBatch, pixelTexture, new Rectangle(left.X, left.Y, Math.Max(1, middleX - left.X), thickness), color);
            DrawFilled(spriteBatch, pixelTexture, new Rectangle(right.X, right.Y, Math.Max(1, middleX - right.X), thickness), color);
            DrawFilled(spriteBatch, pixelTexture, new Rectangle(middleX, Math.Min(left.Y, right.Y), thickness, Math.Abs(right.Y - left.Y) + thickness), color);
            DrawFilled(spriteBatch, pixelTexture, new Rectangle(middleX, target.Y, Math.Max(1, target.X - middleX), thickness), color);
        }

        private static void DrawButton(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Rectangle bounds, string label, bool enabled, float scale)
        {
            Color fill = enabled ? new Color(81, 96, 126, 220) : new Color(52, 58, 72, 220);
            Color border = enabled ? new Color(210, 216, 229, 220) : new Color(116, 123, 137, 180);
            DrawFilled(spriteBatch, pixelTexture, bounds, fill);
            DrawBorder(spriteBatch, pixelTexture, bounds, border);

            Vector2 size = font.MeasureString(label) * scale;
            Vector2 position = new(
                bounds.X + (bounds.Width - size.X) / 2f,
                bounds.Y + (bounds.Height - size.Y) / 2f);
            TournamentField.DrawShadowedText(spriteBatch, font, label, position, enabled ? Color.White : Color.Gainsboro, scale);
        }

        private static void DrawCenteredText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 center, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = font.MeasureString(text) * scale;
            Vector2 position = new(center.X - size.X / 2f, center.Y - size.Y / 2f);
            TournamentField.DrawShadowedText(spriteBatch, font, text, position, color, scale);
        }

        private static Point ScaleAndOffset(Point point, Rectangle tableRect, float scale)
        {
            return new Point(tableRect.X + Scale(point.X, scale), tableRect.Y + Scale(point.Y, scale));
        }

        private static Vector2 ToVector2(Point point)
        {
            return new Vector2(point.X, point.Y);
        }

        private static int Scale(int value, float scale)
        {
            return (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);
        }

        private static void DrawFilled(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle bounds, Color color)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            spriteBatch.Draw(pixelTexture, bounds, color);
        }

        private static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle bounds, Color color)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            DrawFilled(spriteBatch, pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), color);
            DrawFilled(spriteBatch, pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), color);
            DrawFilled(spriteBatch, pixelTexture, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), color);
            DrawFilled(spriteBatch, pixelTexture, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), color);
        }

        private const string MatchTableDialogOwnerText = "CMatchTableDlg";
    }
}
