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
    #region Memory Game / Match Cards (CMemoryGameDlg)
    public enum MemoryGamePacketType
    {
        OpenRoom,
        SetReady,
        StartGame,
        RevealCard,
        BanUser,
        ClaimTie,
        GiveUp,
        EndRoom,
        SelectMatchCardsMode
    }


    /// <summary>
    /// MiniRoom Match Cards runtime. This mirrors the client-owned dialog shape
    /// by keeping a dedicated room shell, board state, ready/start button flow,
    /// turn ownership, and delayed mismatch hide handling.
    /// </summary>
    public class MemoryGameField
    {
        private const int DefaultRows = 4;
        private const int DefaultColumns = 4;
        private const int DefaultLocalPlayerIndex = 0;
        private const int DefaultMismatchHideDelayMs = 900;
        private const int DefaultTurnSeconds = 15;
        private const int DefaultResultSeconds = 5;
        private const int DefaultRemoteActionDelayMs = 600;
        private const int ClientDialogWidth = 734;
        private const int ClientDialogHeight = 429;
        private const int ClientBoardLeft = 48;
        private const int ClientBoardTop = 36;
        private const int ClientBoardWidth = 292;
        private const int ClientBoardHeight = 330;
        private const int ClientTurnIndicatorY = 55;
        private const int ClientTurnIndicatorLeftX = 402;
        private const int ClientTurnIndicatorRightX = 488;
        private const int ClientNameBarY = 149;
        private const int ClientNameBarLeftX = 404;
        private const int ClientNameBarRightX = 490;
        private const int ClientRecordPanelX = 404;
        private const int ClientRecordPanelY = 167;
        private const int ClientMasterPanelX = 460;
        private const int ClientMasterPanelY = 63;
        private const int ClientTimerTextX = 295;
        private const int ClientTimerTextY = 404;
        private const int ClientReadyButtonX = 625;
        private const int ClientReadyButtonY = 243;
        private const int ClientTieButtonX = 458;
        private const int ClientTieButtonY = 403;
        private const int ClientGiveUpButtonX = 410;
        private const int ClientGiveUpButtonY = 403;
        private const int ClientEndButtonX = 679;
        private const int ClientEndButtonY = 403;
        private const int ClientBanButtonX = 551;
        private const int ClientBanButtonY = 63;
        private const int ClientScoreLeftX = 418;
        private const int ClientScoreRightX = 582;
        private const int ClientScoreY = 176;
        private const int ClientReadyIndicatorLeftX = 408;
        private const int ClientReadyIndicatorRightX = 494;
        private const int ClientReadyIndicatorY = 184;
        private const int CardFaceTextureCount = 15;
        private const int CardBackTextureCount = 3;
        private const int DigitTextureCount = 10;
        private const byte MiniRoomBaseEnterPacketType = 4;
        private const byte MiniRoomBaseGameplayPacketType = 6;
        private const byte MiniRoomBaseChatPacketType = 7;
        private const byte MiniRoomBaseChatRepeatPacketType = 8;
        private const byte MiniRoomBaseAvatarPacketType = 9;
        private const byte MiniRoomBaseLeavePacketType = 10;
        private const byte MiniRoomChatGameMessageType = 7;
        private const byte MemoryGameTieRequestPacketType = 50;
        private const byte MemoryGameTieResultPacketType = 51;
        private const byte MemoryGameReadyPacketType = 58;
        private const byte MemoryGameCancelReadyPacketType = 59;
        private const byte MemoryGameClientBanOrTurnUpCardPacketType = 60;
        private const byte MemoryGameStartPacketType = 61;
        private const byte MemoryGameClientGiveUpPacketType = 52;
        private const byte MemoryGameClientBookLeavePacketType = 56;
        private const byte MemoryGameClientCancelLeavePacketType = 57;
        private const byte MemoryGameGameResultPacketType = 62;
        private const byte MemoryGameTimeOverPacketType = 63;
        private const byte MemoryGameTurnUpCardPacketType = 68;
        private const int MemoryGameGiveUpPromptStringPoolId = 0x1D7;
        private const int MemoryGameIncomingTiePromptStringPoolId = 0x1D9;
        private const int MemoryGameOutgoingTiePromptStringPoolId = 0x1DA;
        private const int MemoryGameTieResultNoticeStringPoolId = 0x1DB;
        private const int MemoryGameBookLeavePromptStringPoolId = 0x1E0;
        private const int MemoryGameCancelLeavePromptStringPoolId = 0x1E1;
        private const int MemoryGameCloseRoomPromptStringPoolId = 0x1E4;
        private const int ClientPromptBoxWidth = 250;
        private const int ClientPromptBoxHeight = 98;
        private const int ClientPromptButtonWidth = 64;
        private const int ClientPromptButtonHeight = 22;
        private static readonly IReadOnlyDictionary<int, MiniRoomGameMessageDefinition> MiniRoomGameMessages = new Dictionary<int, MiniRoomGameMessageDefinition>
        {
            [0] = new MiniRoomGameMessageDefinition(0x1C8, "[%s] have been expelled."),
            [1] = new MiniRoomGameMessageDefinition(0x1CD, "[%s]'s turn."),
            [2] = new MiniRoomGameMessageDefinition(0x1CA, "[%s] have forfeited."),
            [3] = new MiniRoomGameMessageDefinition(0x1CB, "[%s] have requested a handicap."),
            [4] = new MiniRoomGameMessageDefinition(0x1C5, "[%s] have left."),
            [5] = new MiniRoomGameMessageDefinition(0x1C6, "[%s] have called to leave after this game."),
            [6] = new MiniRoomGameMessageDefinition(0x1C7, "[%s] have cancelled the request to leave after this game."),
            [7] = new MiniRoomGameMessageDefinition(0x1C4, "[%s] have entered."),
            [8] = new MiniRoomGameMessageDefinition(0x1CF, "[%s] can't start the game due to lack of mesos."),
            [9] = new MiniRoomGameMessageDefinition(0x1CE, "[%s] has matched cards. Please continue."),
            [101] = new MiniRoomGameMessageDefinition(0x1D2, "10 sec. left."),
            [102] = new MiniRoomGameMessageDefinition(0x1D0, "The game has started."),
            [103] = new MiniRoomGameMessageDefinition(0x1D1, "The game has ended.\r\nThe room will automatically close in 10 sec.")
        };
        private static readonly IReadOnlyDictionary<int, MiniRoomNoticeMessageDefinition> MiniRoomLeaveNotices = new Dictionary<int, MiniRoomNoticeMessageDefinition>
        {
            [0x1CC] = new MiniRoomNoticeMessageDefinition(0x1CC, "You have left the room."),
            [0x1D3] = new MiniRoomNoticeMessageDefinition(0x1D3, "The room is closed.")
        };


        private readonly List<Card> _cards = new();
        private readonly List<int> _revealedCardIndices = new(2);
        private readonly Queue<PendingRemoteAction> _pendingRemoteActions = new();
        private readonly Dictionary<int, MiniRoomParticipantState> _miniRoomParticipants = new();
        private readonly int[] _scores = new int[2];
        private readonly bool[] _readyStates = new bool[2];
        private readonly bool[] _leaveBookingStates = new bool[2];
        private readonly string[] _playerNames = new string[2];
        private readonly int[] _wins = new int[2];
        private readonly int[] _losses = new int[2];
        private readonly int[] _draws = new int[2];
        private readonly Dictionary<MemoryGamePacketType, int> _packetCounts = new();
        private readonly Texture2D[] _cardFaceTextures = new Texture2D[CardFaceTextureCount];
        private readonly Texture2D[] _cardBackTextures = new Texture2D[CardBackTextureCount];
        private readonly Texture2D[] _digitTextures = new Texture2D[DigitTextureCount];


        private RoomStage _stage = RoomStage.Hidden;
        private SocialRoomRuntime _miniRoomRuntime;
        private GraphicsDevice _graphicsDevice;
        private bool _assetsLoaded;
        private Texture2D _backgroundTexture;
        private Texture2D _masterPanelTexture;
        private Texture2D _turnTexture;
        private Texture2D _readyOnTexture;
        private Texture2D _readyOffTexture;
        private Texture2D _winTexture;
        private Texture2D _loseTexture;
        private Texture2D _drawTexture;
        private Texture2D _readyButtonTexture;
        private Texture2D _startButtonTexture;
        private Texture2D _tieButtonTexture;
        private Texture2D _giveUpButtonTexture;
        private Texture2D _endButtonTexture;
        private Texture2D _banButtonTexture;
        private int _rows;
        private int _columns;
        private int _localPlayerIndex;
        private int _currentTurnIndex;
        private int _pendingHideTick;
        private int _turnDeadlineTick;
        private int _resultExpireTick;
        private int _lastWinnerIndex = -1;
        private string _title = "Match Cards";
        private string _statusMessage = "Open a MiniRoom to begin.";
        private MemoryGamePacketType? _lastPacketType;
        private string _lastPacketSummary = "No Match Cards packet dispatched.";
        private Func<LoginAvatarLook, CharacterBuild> _miniRoomAvatarBuildFactory;
        private CharacterBuild _localMiniRoomAvatarBuild;
        private MemoryGamePromptState _pendingPrompt;
        private bool _localTieRequestSent;
        private bool _localGiveUpRequestSent;


        public enum RoomStage
        {
            Hidden,
            Lobby,
            Playing,
            Result
        }


        public sealed class Card
        {
            public int FaceId { get; init; }
            public bool IsFaceUp { get; set; }
            public bool IsMatched { get; set; }
        }


        private sealed class MiniRoomParticipantState
        {
            public MiniRoomParticipantState(int slot)
            {
                Slot = slot;
            }


            public int Slot { get; }
            public string Name { get; set; }
            public short JobCode { get; set; }
            public LoginAvatarLook AvatarLook { get; set; }
            public CharacterBuild AvatarBuild { get; set; }
        }


        private readonly struct MiniRoomGameMessageDefinition
        {
            public MiniRoomGameMessageDefinition(int stringPoolId, string fallbackText)
            {
                StringPoolId = stringPoolId;
                FallbackText = fallbackText ?? string.Empty;
            }


            public int StringPoolId { get; }

            public string FallbackText { get; }

        }


        private readonly struct MiniRoomNoticeMessageDefinition
        {
            public MiniRoomNoticeMessageDefinition(int stringPoolId, string text)
            {
                StringPoolId = stringPoolId;
                Text = text ?? string.Empty;
            }


            public int StringPoolId { get; }

            public string Text { get; }
        }



        private readonly struct PendingRemoteAction
        {
            public PendingRemoteAction(RemoteActionType actionType, int executeTick, int playerIndex, int cardIndex, bool readyState)
            {
                ActionType = actionType;
                ExecuteTick = executeTick;
                PlayerIndex = playerIndex;
                CardIndex = cardIndex;
                ReadyState = readyState;
            }


            public RemoteActionType ActionType { get; }
            public int ExecuteTick { get; }
            public int PlayerIndex { get; }
            public int CardIndex { get; }
            public bool ReadyState { get; }
        }


        private enum RemoteActionType
        {
            Ready,
            Start,
            Reveal,
            Tie,
            GiveUp,
            End
        }

        private enum MemoryGamePromptType
        {
            None,
            OutgoingTieRequest,
            IncomingTieRequest,
            GiveUp,
            BookLeave,
            CancelBookedLeave,
            CloseRoom
        }

        private readonly struct MemoryGamePromptState
        {
            public MemoryGamePromptState(MemoryGamePromptType type, int stringPoolId, int playerIndex, string text)
            {
                Type = type;
                StringPoolId = stringPoolId;
                PlayerIndex = playerIndex;
                Text = text ?? string.Empty;
            }

            public MemoryGamePromptType Type { get; }
            public int StringPoolId { get; }
            public int PlayerIndex { get; }
            public string Text { get; }
            public bool IsActive => Type != MemoryGamePromptType.None;
        }


        public RoomStage Stage => _stage;
        public bool IsVisible => _stage != RoomStage.Hidden;
        public bool IsPlaying => _stage == RoomStage.Playing;
        public bool HasPendingMismatch => _pendingHideTick > 0;
        public IReadOnlyList<Card> Cards => _cards;
        public int CurrentTurnIndex => _currentTurnIndex;
        public int LocalPlayerIndex => _localPlayerIndex;
        public int CurrentTurnTimeRemainingSeconds => _turnDeadlineTick <= 0 ? 0 : Math.Max(0, (_turnDeadlineTick - Environment.TickCount + 999) / 1000);
        public int LastWinnerIndex => _lastWinnerIndex;
        public IReadOnlyList<int> Scores => _scores;
        public IReadOnlyList<bool> ReadyStates => _readyStates;
        public IReadOnlyList<bool> LeaveBookingStates => _leaveBookingStates;
        public IReadOnlyList<string> PlayerNames => _playerNames;
        public string Title => _title;
        public MemoryGamePacketType? LastPacketType => _lastPacketType;
        public string LastPacketSummary => _lastPacketSummary;
        public bool HasPendingPrompt => _pendingPrompt.IsActive;
        public string PendingPromptText => _pendingPrompt.Text;


        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }


        public void AttachMiniRoomRuntime(SocialRoomRuntime runtime)
        {
            _miniRoomRuntime = runtime;
            _miniRoomRuntime?.BindMiniRoomHandlers(HandleMiniRoomReadyRequested, HandleMiniRoomStartRequested, HandleMiniRoomModeRequested);
            SyncMiniRoomRuntime();
        }


        public void SetMiniRoomAvatarBuildFactory(Func<LoginAvatarLook, CharacterBuild> avatarBuildFactory)
        {
            _miniRoomAvatarBuildFactory = avatarBuildFactory;
            foreach (MiniRoomParticipantState participant in _miniRoomParticipants.Values)
            {
                participant.AvatarBuild = CreateMiniRoomAvatarBuild(participant.AvatarLook);
            }


            SyncMiniRoomRuntime();

        }



        public void SetLocalMiniRoomAvatar(CharacterBuild build)
        {
            _localMiniRoomAvatarBuild = build?.Clone();
            SyncMiniRoomRuntime();
        }


        public void OpenRoom(
            string title = "Match Cards",
            string playerOneName = "Player",
            string playerTwoName = "Opponent",
            int rows = DefaultRows,
            int columns = DefaultColumns,
            int localPlayerIndex = DefaultLocalPlayerIndex)
        {
            rows = Math.Max(2, rows);
            columns = Math.Max(2, columns);
            if ((rows * columns) % 2 != 0)
            {
                columns++;
            }


            _rows = rows;
            _columns = columns;
            _localPlayerIndex = Math.Clamp(localPlayerIndex, 0, 1);
            _playerNames[0] = string.IsNullOrWhiteSpace(playerOneName) ? "Player" : playerOneName.Trim();
            _playerNames[1] = string.IsNullOrWhiteSpace(playerTwoName) ? "Opponent" : playerTwoName.Trim();
            _title = string.IsNullOrWhiteSpace(title) ? "Match Cards" : title.Trim();


            ClearRoundState();
            _stage = RoomStage.Lobby;
            _statusMessage = "Ready the room, then start the board.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Match Cards room opened.");
            SyncMiniRoomRuntime();
        }


        public bool TrySetReady(int playerIndex, bool isReady, out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Open a Memory Game room first.";
                return false;
            }


            if (_stage == RoomStage.Playing)
            {
                message = "Ready state is locked while a round is in progress.";
                return false;
            }


            if (!IsValidPlayerIndex(playerIndex))
            {
                message = $"Invalid player index: {playerIndex}.";
                return false;
            }


            _readyStates[playerIndex] = isReady;
            _statusMessage = $"{_playerNames[playerIndex]} is {(isReady ? "ready" : "not ready")}.";
            message = _statusMessage;
            _miniRoomRuntime?.AddMiniRoomSystemMessage(_statusMessage);
            SyncMiniRoomRuntime();
            return true;
        }


        public bool TryStartGame(int tickCount, out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Open a Memory Game room first.";
                return false;
            }


            if (!_readyStates[0] || !_readyStates[1])
            {
                message = "Both players must be ready before the round can start.";
                return false;
            }


            InitializeBoard();
            _stage = RoomStage.Playing;
            _currentTurnIndex = 0;
            _pendingHideTick = 0;
            _lastWinnerIndex = -1;
            _turnDeadlineTick = tickCount + DefaultTurnSeconds * 1000;
            _statusMessage = $"{_playerNames[_currentTurnIndex]}'s turn.";
            message = _statusMessage;
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Match Cards round started.");
            SyncMiniRoomRuntime();
            return true;
        }


        public bool TryRevealCard(int cardIndex, int tickCount, out string message)
        {
            return TryRevealCard(cardIndex, tickCount, _localPlayerIndex, out message);
        }


        public bool TryRevealCard(int cardIndex, int tickCount, int playerIndex, out string message)
        {
            if (_stage != RoomStage.Playing)
            {
                message = "The board is not active.";
                return false;
            }


            if (_currentTurnIndex != playerIndex)
            {
                message = $"It is {_playerNames[_currentTurnIndex]}'s turn.";
                return false;
            }


            if (_pendingHideTick > 0)
            {
                message = "Wait for the previous mismatch to resolve.";
                return false;
            }


            if (cardIndex < 0 || cardIndex >= _cards.Count)
            {
                message = $"Invalid card index: {cardIndex}.";
                return false;
            }


            Card card = _cards[cardIndex];
            if (card.IsMatched || card.IsFaceUp)
            {
                message = "That card is already revealed.";
                return false;
            }


            card.IsFaceUp = true;

            _revealedCardIndices.Add(cardIndex);



            if (_revealedCardIndices.Count == 1)
            {
                _statusMessage = $"{_playerNames[_currentTurnIndex]} revealed card {cardIndex}.";
                message = _statusMessage;
                _miniRoomRuntime?.AddMiniRoomSpeakerMessage(_playerNames[_currentTurnIndex], $"turned up card {cardIndex}.", _currentTurnIndex == _localPlayerIndex);
                SyncMiniRoomRuntime();
                return true;
            }


            Card firstCard = _cards[_revealedCardIndices[0]];
            if (firstCard.FaceId == card.FaceId)
            {
                firstCard.IsMatched = true;
                card.IsMatched = true;
                _scores[_currentTurnIndex]++;
                _revealedCardIndices.Clear();
                _turnDeadlineTick = tickCount + DefaultTurnSeconds * 1000;


                if (AreAllCardsMatched())
                {
                    FinishRound(tickCount);
                }
                else
                {
                    _statusMessage = $"{_playerNames[_currentTurnIndex]} found a pair.";
                    _miniRoomRuntime?.AddMiniRoomSystemMessage(_statusMessage);
                    SyncMiniRoomRuntime();
                }


                message = _statusMessage;

                return true;

            }



            _pendingHideTick = tickCount + DefaultMismatchHideDelayMs;
            _statusMessage = "Mismatch. Cards will flip back.";
            message = _statusMessage;
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Mismatch. Waiting for cards to flip back.");
            SyncMiniRoomRuntime();
            return true;
        }


        public bool TryClaimTie(out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Open a Memory Game room first.";
                return false;
            }


            _stage = RoomStage.Result;
            _lastWinnerIndex = -1;
            _draws[0]++;
            _draws[1]++;
            _resultExpireTick = Environment.TickCount + DefaultResultSeconds * 1000;
            _statusMessage = "The room settled as a draw.";
            message = _statusMessage;
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : The round ended in a draw.");
            SyncMiniRoomRuntime();
            return true;
        }

        public bool TryPromptTieRequest(out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Open a Memory Game room first.";
                return false;
            }

            if (_localTieRequestSent)
            {
                message = "A Match Cards tie request is already pending.";
                return false;
            }

            return TryOpenPrompt(
                MemoryGamePromptType.OutgoingTieRequest,
                MemoryGameOutgoingTiePromptStringPoolId,
                _localPlayerIndex,
                ResolveMemoryGamePromptText(MemoryGameOutgoingTiePromptStringPoolId),
                out message);
        }


        public bool TryBanParticipant(int requesterIndex, out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Open a Memory Game room first.";
                return false;
            }

            if (!IsValidPlayerIndex(requesterIndex))
            {
                message = $"Invalid player index: {requesterIndex}.";
                return false;
            }

            int targetIndex = requesterIndex == 0 ? 1 : 0;
            string targetName = ResolveParticipantName(targetIndex);
            if (string.IsNullOrWhiteSpace(targetName)
                || (targetIndex == 1 && string.Equals(targetName, "Opponent", StringComparison.Ordinal))
                || (targetIndex == 0 && string.Equals(targetName, "Player", StringComparison.Ordinal)))
            {
                message = "No participant is available to ban.";
                return false;
            }

            _statusMessage = $"Ban request sent for {targetName}.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {ResolveMiniRoomGameMessage(0, targetName)}");
            SyncMiniRoomRuntime();
            message = _statusMessage;
            return true;
        }


        public bool TryGiveUp(int playerIndex, out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Open a Memory Game room first.";
                return false;
            }


            if (!IsValidPlayerIndex(playerIndex))
            {
                message = $"Invalid player index: {playerIndex}.";
                return false;
            }


            int winnerIndex = playerIndex == 0 ? 1 : 0;
            _wins[winnerIndex]++;
            _losses[playerIndex]++;
            _stage = RoomStage.Result;
            _lastWinnerIndex = winnerIndex;
            _resultExpireTick = Environment.TickCount + DefaultResultSeconds * 1000;
            _statusMessage = $"{_playerNames[playerIndex]} gave up. {_playerNames[winnerIndex]} wins.";
            message = _statusMessage;
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {_playerNames[playerIndex]} gave up.");
            SyncMiniRoomRuntime();
            return true;
        }

        public bool TryPromptGiveUp(int playerIndex, out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Open a Memory Game room first.";
                return false;
            }

            if (!IsValidPlayerIndex(playerIndex))
            {
                message = $"Invalid player index: {playerIndex}.";
                return false;
            }

            if (playerIndex == _localPlayerIndex && _localGiveUpRequestSent)
            {
                message = "A Match Cards give-up request is already pending.";
                return false;
            }

            return TryOpenPrompt(
                MemoryGamePromptType.GiveUp,
                MemoryGameGiveUpPromptStringPoolId,
                playerIndex,
                ResolveMemoryGamePromptText(MemoryGameGiveUpPromptStringPoolId),
                out message);
        }


        public bool TryEndRoom(out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Memory Game room is already closed.";
                return false;
            }


            Reset();
            message = "Memory Game room closed.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Match Cards room closed.");
            return true;
        }


        public bool TryRequestRoomExit(int playerIndex, out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Memory Game room is already closed.";
                return false;
            }


            if (!IsValidPlayerIndex(playerIndex))
            {
                message = $"Invalid player index: {playerIndex}.";
                return false;
            }


            if (_stage == RoomStage.Lobby)
            {
                if (playerIndex == 0 || playerIndex == _localPlayerIndex)
                {
                    return TryOpenPrompt(
                        MemoryGamePromptType.CloseRoom,
                        MemoryGameCloseRoomPromptStringPoolId,
                        playerIndex,
                        ResolveMemoryGamePromptText(MemoryGameCloseRoomPromptStringPoolId),
                        out message);
                }

                return TryResolveLobbyExit(playerIndex, out message);
            }

            bool booked = !_leaveBookingStates[playerIndex];
            return TryOpenPrompt(
                booked ? MemoryGamePromptType.BookLeave : MemoryGamePromptType.CancelBookedLeave,
                booked ? MemoryGameBookLeavePromptStringPoolId : MemoryGameCancelLeavePromptStringPoolId,
                playerIndex,
                ResolveMemoryGamePromptText(booked ? MemoryGameBookLeavePromptStringPoolId : MemoryGameCancelLeavePromptStringPoolId),
                out message);
        }

        public bool TryConfirmPrompt(int tickCount, out string message)
        {
            if (!_pendingPrompt.IsActive)
            {
                message = "No Match Cards confirmation prompt is active.";
                return false;
            }

            MemoryGamePromptState prompt = _pendingPrompt;
            ClearPendingPrompt();
            return prompt.Type switch
            {
                MemoryGamePromptType.OutgoingTieRequest => ConfirmOutgoingTieRequest(tickCount, out message),
                MemoryGamePromptType.IncomingTieRequest => ConfirmIncomingTieRequest(tickCount, out message),
                MemoryGamePromptType.GiveUp => ConfirmGiveUp(prompt.PlayerIndex, out message),
                MemoryGamePromptType.BookLeave => TryApplyLeaveBookingStatus(prompt.PlayerIndex, booked: true, out message),
                MemoryGamePromptType.CancelBookedLeave => TryApplyLeaveBookingStatus(prompt.PlayerIndex, booked: false, out message),
                MemoryGamePromptType.CloseRoom => TryResolveLobbyExit(prompt.PlayerIndex, out message),
                _ => AssignPromptMissing(out message)
            };
        }

        public bool TryCancelPrompt(out string message)
        {
            if (!_pendingPrompt.IsActive)
            {
                message = "No Match Cards confirmation prompt is active.";
                return false;
            }

            MemoryGamePromptState prompt = _pendingPrompt;
            ClearPendingPrompt();
            string promptText = string.IsNullOrWhiteSpace(prompt.Text) ? "Match Cards prompt" : prompt.Text;
            _statusMessage = $"Canceled: {promptText}";
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {_statusMessage}");
            SyncMiniRoomRuntime();
            message = _statusMessage;
            return true;
        }


        public bool TryDispatchPacket(
            MemoryGamePacketType packetType,
            int tickCount,
            out string message,
            int playerIndex = DefaultLocalPlayerIndex,
            int cardIndex = -1,
            bool readyState = true,
            string playerOneName = null,
            string playerTwoName = null,
            int rows = DefaultRows,
            int columns = DefaultColumns,
            string title = "Match Cards")
        {
            _lastPacketType = packetType;
            _packetCounts.TryGetValue(packetType, out int count);
            _packetCounts[packetType] = count + 1;


            bool handled = packetType switch
            {
                MemoryGamePacketType.OpenRoom => TryDispatchOpenPacket(title, playerOneName, playerTwoName, rows, columns, playerIndex, out message),
                MemoryGamePacketType.SetReady => TrySetReady(playerIndex, readyState, out message),
                MemoryGamePacketType.StartGame => TryStartGame(tickCount, out message),
                MemoryGamePacketType.RevealCard => TryRevealCard(cardIndex, tickCount, playerIndex, out message),
                MemoryGamePacketType.BanUser => TryBanParticipant(playerIndex, out message),
                MemoryGamePacketType.ClaimTie => TryClaimTie(out message),
                MemoryGamePacketType.GiveUp => TryGiveUp(playerIndex, out message),
                MemoryGamePacketType.EndRoom => TryEndRoom(out message),
                MemoryGamePacketType.SelectMatchCardsMode => TrySelectMatchCardsMode(out message),
                _ => AssignUnsupportedPacket(packetType, out message)
            };


            _lastPacketSummary = $"{packetType}: {message}";

            return handled;

        }



        public bool TryDispatchMiniRoomPacket(byte[] packetBytes, int tickCount, out string message)
        {
            if (packetBytes == null || packetBytes.Length == 0)
            {
                message = "MiniRoom packet payload is empty.";
                return false;
            }


            EnsureRoomOpenFromMiniRoomRuntime();



            try
            {
                byte[] payload = NormalizeMiniRoomPacketPayload(packetBytes);
                PacketReader reader = new(payload);
                byte basePacketType = reader.ReadByte();
                return basePacketType switch
                {
                    MiniRoomBaseEnterPacketType => TryDispatchMiniRoomEnterPacket(reader, out message),
                    MiniRoomBaseGameplayPacketType => TryDispatchMiniRoomGameplayPacket(reader, tickCount, out message),
                    MiniRoomBaseChatPacketType => TryDispatchMiniRoomChatPacket(reader, out message),
                    MiniRoomBaseChatRepeatPacketType => TryDispatchMiniRoomChatPacket(reader, out message),
                    MiniRoomBaseAvatarPacketType => TryDispatchMiniRoomAvatarPacket(reader, out message),
                    MiniRoomBaseLeavePacketType => TryDispatchMiniRoomLeavePacket(reader, out message),
                    _ => FailMiniRoomPacket(basePacketType, out message)
                };
            }
            catch (EndOfStreamException)
            {
                message = $"MiniRoom packet ended unexpectedly: {BitConverter.ToString(packetBytes)}";
                return false;
            }
        }


        public bool TryDispatchOfficialClientPacket(byte[] packetBytes, int tickCount, out string message)
        {
            if (packetBytes == null || packetBytes.Length == 0)
            {
                message = "Memory Game client payload is empty.";
                return false;
            }


            EnsureRoomOpenFromMiniRoomRuntime();

            byte packetType = packetBytes[0];
            bool handled = packetType switch
            {
                MiniRoomBaseLeavePacketType => TryDispatchPacket(MemoryGamePacketType.EndRoom, tickCount, out message),
                MemoryGameTieRequestPacketType => TryPromptTieRequest(out message),
                MemoryGameTieResultPacketType => TryApplyOutgoingTieResponse(packetBytes, tickCount, out message),
                MemoryGameClientGiveUpPacketType => TryDispatchPacket(MemoryGamePacketType.GiveUp, tickCount, out message, _localPlayerIndex),
                MemoryGameClientBookLeavePacketType => TryApplyLeaveBookingStatus(_localPlayerIndex, booked: true, out message),
                MemoryGameClientCancelLeavePacketType => TryApplyLeaveBookingStatus(_localPlayerIndex, booked: false, out message),
                MemoryGameReadyPacketType => TryDispatchPacket(MemoryGamePacketType.SetReady, tickCount, out message, _localPlayerIndex, readyState: true),
                MemoryGameCancelReadyPacketType => TryDispatchPacket(MemoryGamePacketType.SetReady, tickCount, out message, _localPlayerIndex, readyState: false),
                MemoryGameClientBanOrTurnUpCardPacketType => TryApplyClientBanOrTurnUpCardPacket(packetBytes, tickCount, out message),
                MemoryGameStartPacketType => TryDispatchPacket(MemoryGamePacketType.StartGame, tickCount, out message),
                _ => FailOfficialClientPacket(packetType, out message)
            };

            _lastPacketSummary = $"official client {packetType}: {message}";
            return handled;
        }


        public static byte[] NormalizeMiniRoomPacketPayload(byte[] packetBytes)
        {
            if (packetBytes == null || packetBytes.Length == 0)
            {
                return Array.Empty<byte>();
            }


            if (IsMiniRoomBasePacketType(packetBytes[0]))
            {
                return (byte[])packetBytes.Clone();
            }


            if (packetBytes.Length > sizeof(ushort) && IsMiniRoomBasePacketType(packetBytes[sizeof(ushort)]))
            {
                byte[] trimmed = new byte[packetBytes.Length - sizeof(ushort)];
                Buffer.BlockCopy(packetBytes, sizeof(ushort), trimmed, 0, trimmed.Length);
                return trimmed;
            }


            return (byte[])packetBytes.Clone();

        }



        public static bool TryParseMiniRoomPacketHex(string text, out byte[] packetBytes, out string error)
        {
            packetBytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Provide at least one hex byte.";
                return false;
            }


            string[] tokens = text
                .Replace(",", " ", StringComparison.Ordinal)
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<byte> bytes = new(tokens.Length);
            foreach (string token in tokens)
            {
                string normalized = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? token[2..]
                    : token;
                if (!byte.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, null, out byte value))
                {
                    error = $"Invalid hex byte: {token}.";
                    return false;
                }


                bytes.Add(value);

            }



            packetBytes = bytes.ToArray();
            error = string.Empty;
            return packetBytes.Length > 0;
        }


        public int GetPacketCount(MemoryGamePacketType packetType)
        {
            return _packetCounts.TryGetValue(packetType, out int count) ? count : 0;
        }


        public bool TryQueueRemoteAction(string action, int tickCount, out string message, int cardIndex = -1, int delayMs = DefaultRemoteActionDelayMs)
        {
            int remotePlayerIndex = _localPlayerIndex == 0 ? 1 : 0;
            int executeTick = tickCount + Math.Max(0, delayMs);
            switch (action)
            {
                case "ready":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.Ready, executeTick, remotePlayerIndex, -1, true));
                    message = $"{_playerNames[remotePlayerIndex]} will ready in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "unready":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.Ready, executeTick, remotePlayerIndex, -1, false));
                    message = $"{_playerNames[remotePlayerIndex]} will clear ready in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "start":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.Start, executeTick, remotePlayerIndex, -1, false));
                    message = $"{_playerNames[remotePlayerIndex]} will request start in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "flip":
                    if (cardIndex < 0)
                    {
                        message = "Remote flip requires a card index.";
                        return false;
                    }


                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.Reveal, executeTick, remotePlayerIndex, cardIndex, false));
                    message = $"{_playerNames[remotePlayerIndex]} will reveal card {cardIndex} in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "tie":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.Tie, executeTick, remotePlayerIndex, -1, false));
                    message = $"{_playerNames[remotePlayerIndex]} will request a tie in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "giveup":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.GiveUp, executeTick, remotePlayerIndex, -1, false));
                    message = $"{_playerNames[remotePlayerIndex]} will give up in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "end":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.End, executeTick, remotePlayerIndex, -1, false));
                    message = $"{_playerNames[remotePlayerIndex]} will close the room in {Math.Max(0, delayMs)} ms.";
                    return true;
                default:
                    message = "Usage: /memorygame remote <ready|unready|start|flip|tie|giveup|end> [...]";
                    return false;
            }
        }


        public void Update(int tickCount)
        {
            if (_stage == RoomStage.Playing)
            {
                if (_pendingHideTick > 0 && tickCount >= _pendingHideTick)
                {
                    ResolveMismatch();
                }


                if (_turnDeadlineTick > 0 && tickCount >= _turnDeadlineTick && _pendingHideTick <= 0)
                {
                    AdvanceTurn(tickCount);
                    _statusMessage = $"{_playerNames[_currentTurnIndex]}'s turn.";
                    SyncMiniRoomRuntime();
                }
            }
            else if (_stage == RoomStage.Result && _resultExpireTick > 0 && tickCount >= _resultExpireTick)
            {
                ReturnToLobby();
            }


            ProcessRemoteActions(tickCount);

        }



        public bool HandleMouseClick(Point mousePosition, int viewportWidth, int viewportHeight, int tickCount, out string message)
        {
            message = null;
            if (_stage == RoomStage.Hidden)
            {
                return false;
            }


            GetLayout(viewportWidth, viewportHeight, out Rectangle outer, out Rectangle boardArea, out _, out Rectangle[] buttonRects);
            if (!outer.Contains(mousePosition))
            {
                return false;
            }


            for (int i = 0; i < buttonRects.Length; i++)
            {
                if (!buttonRects[i].Contains(mousePosition))
                {
                    continue;
                }


                switch (i)
                {
                case 0:
                        return HandlePrimarySidebarAction(tickCount, out message);
                    case 1:
                        TryPromptTieRequest(out message);
                        return true;
                    case 2:
                        TryPromptGiveUp(_localPlayerIndex, out message);
                        return true;
                    case 3:
                        TryRequestRoomExit(_localPlayerIndex, out message);
                        return true;
                    case 4:
                        message = "Ban is not modeled for the simulator MiniRoom.";
                        return true;
                }
            }


            int cardIndex = GetCardIndexAt(mousePosition, boardArea);
            if (cardIndex >= 0)
            {
                TryRevealCard(cardIndex, tickCount, out message);
                return true;
            }


            return true;

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
            if (_stage == RoomStage.Hidden || pixelTexture == null || font == null)
            {
                return;
            }


            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;

            EnsureAssetsLoaded();



            int dialogWidth = _backgroundTexture?.Width ?? ClientDialogWidth;
            int dialogHeight = _backgroundTexture?.Height ?? ClientDialogHeight;
            int dialogX = viewport.Width / 2 - dialogWidth / 2;
            int dialogY = Math.Max(24, viewport.Height / 2 - dialogHeight / 2);


            Rectangle outer = new Rectangle(dialogX, dialogY, dialogWidth, dialogHeight);

            Rectangle boardArea = new Rectangle(dialogX + ClientBoardLeft, dialogY + ClientBoardTop, ClientBoardWidth, ClientBoardHeight);



            if (_backgroundTexture != null)
            {
                spriteBatch.Draw(_backgroundTexture, new Vector2(dialogX, dialogY), Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, outer, new Color(20, 27, 41, 235));
            }


            if (_masterPanelTexture != null)
            {
                spriteBatch.Draw(_masterPanelTexture, new Vector2(dialogX + ClientMasterPanelX, dialogY + ClientMasterPanelY), Color.White);
            }


            DrawOutlinedText(spriteBatch, font, _title, new Vector2(dialogX + 407, dialogY + 19), Color.Black, Color.Black);
            DrawClientNamePanel(spriteBatch, pixelTexture, font, _playerNames[0], _scores[0], dialogX + ClientNameBarLeftX, dialogY + ClientNameBarY, _readyStates[0], _currentTurnIndex == 0, isLeftPanel: true);
            DrawClientNamePanel(spriteBatch, pixelTexture, font, _playerNames[1], _scores[1], dialogX + ClientNameBarRightX, dialogY + ClientNameBarY, _readyStates[1], _currentTurnIndex == 1, isLeftPanel: false);
            DrawBoard(spriteBatch, pixelTexture, font, boardArea);
            DrawClientTurnIndicator(spriteBatch, dialogX, dialogY);
            DrawClientButtons(spriteBatch, pixelTexture, font, dialogX, dialogY);
            DrawClientRecordSummary(spriteBatch, font, dialogX, dialogY);
            DrawOutlinedText(spriteBatch, font, $"{CurrentTurnTimeRemainingSeconds}s", new Vector2(dialogX + ClientTimerTextX, dialogY + ClientTimerTextY), Color.Black, new Color(48, 48, 48));
            DrawOutlinedText(spriteBatch, font, _statusMessage, new Vector2(dialogX + 407, dialogY + 320), Color.Black, new Color(72, 52, 24));
            DrawPromptOverlay(spriteBatch, pixelTexture, font, outer);
        }


        public string DescribeStatus()
        {
            string playerOneName = string.IsNullOrWhiteSpace(_playerNames[0]) ? "Player" : _playerNames[0];
            string playerTwoName = string.IsNullOrWhiteSpace(_playerNames[1]) ? "Opponent" : _playerNames[1];
            return $"{_title}: stage={_stage}, turn={_currentTurnIndex}, ready=[{_readyStates[0]},{_readyStates[1]}], leave=[{_leaveBookingStates[0]},{_leaveBookingStates[1]}], score={_scores[0]}-{_scores[1]}, players={playerOneName}/{playerTwoName}, cards={_cards.Count}, pendingHide={_pendingHideTick > 0}, lastPacket={_lastPacketType?.ToString() ?? "None"}";
        }


        public void Reset()
        {
            _cards.Clear();
            _revealedCardIndices.Clear();
            _scores[0] = 0;
            _scores[1] = 0;
            _readyStates[0] = false;
            _readyStates[1] = false;
            _leaveBookingStates[0] = false;
            _leaveBookingStates[1] = false;
            _playerNames[0] = "Player";
            _playerNames[1] = "Opponent";
            _rows = 0;
            _columns = 0;
            _localPlayerIndex = DefaultLocalPlayerIndex;
            _currentTurnIndex = 0;
            _pendingHideTick = 0;
            _turnDeadlineTick = 0;
            _resultExpireTick = 0;
            _lastWinnerIndex = -1;
            _title = "Match Cards";
            _statusMessage = "Open a MiniRoom to begin.";
            _stage = RoomStage.Hidden;
            _pendingRemoteActions.Clear();
            _miniRoomParticipants.Clear();
            _lastPacketType = null;
            _lastPacketSummary = "Memory Game room reset.";
            _packetCounts.Clear();
            ClearPendingPrompt();
            _localTieRequestSent = false;
            _localGiveUpRequestSent = false;
            SyncMiniRoomRuntime();
        }


        private void InitializeBoard()
        {
            _cards.Clear();
            _revealedCardIndices.Clear();
            _scores[0] = 0;
            _scores[1] = 0;
            _pendingHideTick = 0;
            _resultExpireTick = 0;
            _pendingRemoteActions.Clear();
            ClearPendingPrompt();
            _localTieRequestSent = false;
            _localGiveUpRequestSent = false;


            int pairCount = (_rows * _columns) / 2;
            List<int> faceIds = new(pairCount * 2);
            for (int i = 0; i < pairCount; i++)
            {
                faceIds.Add(i);
                faceIds.Add(i);
            }


            Random random = new((_rows * 397) ^ (_columns * 211) ^ _title.GetHashCode(StringComparison.Ordinal));
            for (int i = faceIds.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (faceIds[i], faceIds[swapIndex]) = (faceIds[swapIndex], faceIds[i]);
            }


            for (int i = 0; i < faceIds.Count; i++)
            {
                _cards.Add(new Card
                {
                    FaceId = faceIds[i],
                    IsFaceUp = false,
                    IsMatched = false
                });
            }
        }


        private void ResolveMismatch()
        {
            for (int i = 0; i < _revealedCardIndices.Count; i++)
            {
                _cards[_revealedCardIndices[i]].IsFaceUp = false;
            }


            _revealedCardIndices.Clear();
            _pendingHideTick = 0;
            AdvanceTurn(Environment.TickCount);
            _statusMessage = $"{_playerNames[_currentTurnIndex]}'s turn.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Turn passed after the mismatch.");
            SyncMiniRoomRuntime();
        }


        private void AdvanceTurn(int tickCount)
        {
            _currentTurnIndex = _currentTurnIndex == 0 ? 1 : 0;
            _turnDeadlineTick = tickCount + DefaultTurnSeconds * 1000;
        }


        private bool AreAllCardsMatched()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (!_cards[i].IsMatched)
                {
                    return false;
                }
            }


            return _cards.Count > 0;

        }



        private void FinishRound(int tickCount)
        {
            _stage = RoomStage.Result;
            _resultExpireTick = tickCount + DefaultResultSeconds * 1000;
            _pendingHideTick = 0;
            _turnDeadlineTick = 0;


            if (_scores[0] == _scores[1])
            {
                _lastWinnerIndex = -1;
                _draws[0]++;
                _draws[1]++;
                _statusMessage = "Round complete. Draw.";
                _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Match Cards round complete. Draw.");
                SyncMiniRoomRuntime();
                return;
            }


            int winnerIndex = _scores[0] > _scores[1] ? 0 : 1;
            int loserIndex = winnerIndex == 0 ? 1 : 0;
            _lastWinnerIndex = winnerIndex;
            _wins[winnerIndex]++;
            _losses[loserIndex]++;
            _statusMessage = $"Round complete. {_playerNames[winnerIndex]} wins.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {_playerNames[winnerIndex]} won the round.");
            SyncMiniRoomRuntime();
        }


        private void ReturnToLobby()
        {
            if (TryResolveBookedLeaveAfterRound(out string leaveResolutionMessage))
            {
                _statusMessage = leaveResolutionMessage;
            }

            if (_stage == RoomStage.Hidden)
            {
                return;
            }

            _cards.Clear();
            _revealedCardIndices.Clear();
            _scores[0] = 0;
            _scores[1] = 0;
            _pendingHideTick = 0;
            _turnDeadlineTick = 0;
            _resultExpireTick = 0;
            _lastWinnerIndex = -1;
            _stage = RoomStage.Lobby;
            if (string.IsNullOrWhiteSpace(_statusMessage))
            {
                _statusMessage = "Ready the room, then start the board.";
            }

            SyncMiniRoomRuntime();
        }


        private void ClearRoundState()
        {
            _cards.Clear();
            _revealedCardIndices.Clear();
            _scores[0] = 0;
            _scores[1] = 0;
            _readyStates[0] = false;
            _readyStates[1] = false;
            _leaveBookingStates[0] = false;
            _leaveBookingStates[1] = false;
            _currentTurnIndex = 0;
            _pendingHideTick = 0;
            _turnDeadlineTick = 0;
            _resultExpireTick = 0;
            _lastWinnerIndex = -1;
            _pendingRemoteActions.Clear();
        }


        private bool TryDispatchMiniRoomEnterPacket(PacketReader reader, out string message)
        {
            int slot = reader.ReadByte();
            if (!TryDecodeMiniRoomParticipant(reader, slot, out MiniRoomParticipantState participant, out message))
            {
                return false;
            }


            string seatDescription = slot < 2 ? ResolveSeatLabel(slot) : $"Visitor seat {slot}";
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {participant.Name} entered the Match Cards room ({seatDescription}).");
            SyncMiniRoomRuntime();
            message = $"{participant.Name} entered MiniRoom slot {slot} with job {participant.JobCode}.";
            return true;
        }


        private bool TryDispatchMiniRoomChatPacket(PacketReader reader, out string message)
        {
            byte chatType = reader.ReadByte();
            if (chatType == MiniRoomChatGameMessageType)
            {
                int gameMessageCode = reader.ReadByte();
                string characterName = reader.ReadMapleString();
                string gameMessage = ResolveMiniRoomGameMessage(gameMessageCode, characterName);
                _statusMessage = gameMessage;
                _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {gameMessage}");
                SyncMiniRoomRuntime();
                message = gameMessage;
                return true;
            }


            int speakerSlot = chatType;
            string rawText = reader.ReadMapleString();
            string speakerName = ResolveParticipantName(speakerSlot);
            string normalizedText = NormalizeMiniRoomChatText(rawText, ref speakerName);
            bool isLocalSpeaker = speakerSlot == _localPlayerIndex;
            _miniRoomRuntime?.AddMiniRoomSpeakerMessage(speakerName, normalizedText, isLocalSpeaker);
            _statusMessage = $"{speakerName} said: {normalizedText}";
            SyncMiniRoomRuntime();
            message = $"MiniRoom chat from slot {speakerSlot}: {speakerName} : {normalizedText}";
            return true;
        }


        private bool TryDispatchMiniRoomAvatarPacket(PacketReader reader, out string message)
        {
            int slot = reader.ReadByte();
            if (!TryDecodeMiniRoomAvatar(reader, slot, out MiniRoomParticipantState participant, out message))
            {
                return false;
            }


            string participantName = ResolveParticipantName(slot);
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {participantName} updated their MiniRoom avatar.");
            SyncMiniRoomRuntime();
            message = $"Updated MiniRoom avatar for slot {slot}: {participantName}.";
            return true;
        }


        private bool TryDispatchMiniRoomGameplayPacket(PacketReader reader, int tickCount, out string message)
        {
            byte packetType = reader.ReadByte();
            switch (packetType)
            {
                case MemoryGameReadyPacketType:
                    return TryApplyRemoteReadyPacket(isReady: true, out message);
                case MemoryGameCancelReadyPacketType:
                    return TryApplyRemoteReadyPacket(isReady: false, out message);
                case MemoryGameStartPacketType:
                    return TryApplyStartPacket(reader, tickCount, out message);
                case MemoryGameTurnUpCardPacketType:
                    return TryApplyTurnUpCardPacket(reader, tickCount, out message);
                case MemoryGameTimeOverPacketType:
                    return TryApplyTimeOverPacket(reader, tickCount, out message);
                case MemoryGameTieRequestPacketType:
                    return TryApplyTieRequestStatus(_localPlayerIndex, out message);
                case MemoryGameTieResultPacketType:
                    return TryApplyTieResultStatus(out message);
                case MemoryGameGameResultPacketType:
                    return TryApplyGameResultPacket(reader, tickCount, out message);
                default:
                    message = $"MiniRoom gameplay packet {packetType} is not modeled for Match Cards.";
                    return false;
            }
        }


        private bool TryDispatchMiniRoomLeavePacket(PacketReader reader, out string message)
        {
            int playerIndex = reader.ReadByte();
            if (playerIndex < 0)
            {
                message = $"MiniRoom leave packet used invalid player index {playerIndex}.";
                return false;
            }


            string playerName = ResolveParticipantName(playerIndex);
            int? leaveReason = null;
            try
            {
                leaveReason = reader.ReadByte();
            }
            catch (EndOfStreamException)
            {
            }
            string leaveStatusMessage = leaveReason.HasValue
                ? ResolveMiniRoomGameMessage(TranslateLeaveReasonToGameMessageCode(leaveReason.Value), playerName)
                : $"{playerName} left the Match Cards room.";
            _miniRoomParticipants.Remove(playerIndex);
            if (IsValidPlayerIndex(playerIndex) && (_stage == RoomStage.Playing || _stage == RoomStage.Result || _stage == RoomStage.Lobby))
            {
                Reset();
                _statusMessage = leaveStatusMessage;
            }
            else
            {
                _statusMessage = leaveStatusMessage;
                SyncMiniRoomRuntime();
            }


            message = leaveReason.HasValue
                ? $"{playerName} left the Match Cards room (reason {leaveReason.Value})."
                : $"{playerName} left the Match Cards room.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {_statusMessage}", isWarning: true);
            SyncMiniRoomRuntime();
            return true;
        }


        private bool TryApplyRemoteReadyPacket(bool isReady, out string message)
        {
            int remotePlayerIndex = _localPlayerIndex == 0 ? 1 : 0;
            bool handled = TrySetReady(remotePlayerIndex, isReady, out message);
            if (handled)
            {
                _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {ResolveRemotePlayerName()} {(isReady ? "is ready." : "canceled ready.")}");
            }


            return handled;

        }


        private bool TryApplyTieRequestStatus(int playerIndex, out string message)
        {
            if (!IsValidPlayerIndex(playerIndex))
            {
                message = $"Invalid player index: {playerIndex}.";
                return false;
            }


            string playerName = ResolveParticipantName(playerIndex);
            return TryOpenPrompt(
                MemoryGamePromptType.IncomingTieRequest,
                MemoryGameIncomingTiePromptStringPoolId,
                playerIndex,
                ResolveMemoryGamePromptText(MemoryGameIncomingTiePromptStringPoolId, playerName),
                out message);
        }

        private bool TryApplyTieResultStatus(out string message)
        {
            _localTieRequestSent = false;
            _statusMessage = ResolveMemoryGamePromptText(MemoryGameTieResultNoticeStringPoolId);
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {_statusMessage}", isWarning: true);
            SyncMiniRoomRuntime();
            message = _statusMessage;
            return true;
        }


        private bool TryApplyLeaveBookingStatus(int playerIndex, bool booked, out string message)
        {
            if (!IsValidPlayerIndex(playerIndex))
            {
                message = $"Invalid player index: {playerIndex}.";
                return false;
            }


            _leaveBookingStates[playerIndex] = booked;
            string playerName = ResolveParticipantName(playerIndex);
            _statusMessage = booked
                ? $"{playerName} booked a leave request for the next round."
                : $"{playerName} canceled the pending leave request.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {_statusMessage}");
            SyncMiniRoomRuntime();
            message = _statusMessage;
            return true;
        }

        private bool TryApplyOutgoingTieResponse(byte[] packetBytes, int tickCount, out string message)
        {
            bool accepted = packetBytes.Length <= 1 || packetBytes[1] != 0;
            _localTieRequestSent = false;
            if (!accepted)
            {
                return TryApplyTieResultStatus(out message);
            }

            return TryClaimTie(out message);
        }


        private bool TryApplyClientBanOrTurnUpCardPacket(byte[] packetBytes, int tickCount, out string message)
        {
            if (packetBytes.Length <= 1)
            {
                return TryDispatchPacket(MemoryGamePacketType.BanUser, tickCount, out message, _localPlayerIndex);
            }


            int cardIndex = packetBytes[1];
            return TryDispatchPacket(MemoryGamePacketType.RevealCard, tickCount, out message, _localPlayerIndex, cardIndex);
        }



        private bool TryApplyStartPacket(PacketReader reader, int tickCount, out string message)
        {
            int currentTurnIndex = reader.ReadByte();
            int cardCount = reader.ReadByte();
            if (cardCount <= 0)
            {
                message = "Memory Game start packet did not include a card count.";
                return false;
            }


            List<int> shuffle = new(cardCount);
            for (int i = 0; i < cardCount; i++)
            {
                shuffle.Add(reader.ReadInt());
            }


            ResolveBoardDimensions(cardCount, out _rows, out _columns);
            InitializeBoardFromPacket(shuffle);
            _stage = RoomStage.Playing;
            _currentTurnIndex = Math.Clamp(currentTurnIndex, 0, _playerNames.Length - 1);
            _turnDeadlineTick = tickCount + (200 * cardCount) + 11500;
            _resultExpireTick = 0;
            _readyStates[0] = false;
            _readyStates[1] = false;
            _statusMessage = $"{_playerNames[_currentTurnIndex]}'s turn.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Start packet applied from MiniRoom payload.");
            SyncMiniRoomRuntime();
            message = $"Applied start packet for {cardCount} cards. {_statusMessage}";
            return true;
        }


        private bool TryApplyTurnUpCardPacket(PacketReader reader, int tickCount, out string message)
        {
            int firstRevealFlag = reader.ReadByte();
            int cardIndex = reader.ReadByte();
            if (!TryEnsureCardIndex(cardIndex, out message))
            {
                return false;
            }


            if (firstRevealFlag != 0)
            {
                _cards[cardIndex].IsFaceUp = true;
                _revealedCardIndices.Clear();
                _revealedCardIndices.Add(cardIndex);
                _statusMessage = $"{_playerNames[_currentTurnIndex]} revealed card {cardIndex}.";
                _miniRoomRuntime?.AddMiniRoomSpeakerMessage(_playerNames[_currentTurnIndex], $"turned up card {cardIndex}.", _currentTurnIndex == _localPlayerIndex);
                SyncMiniRoomRuntime();
                message = _statusMessage;
                return true;
            }


            int pairedCardIndex = reader.ReadByte();
            int resultOwner = reader.ReadByte();
            if (!TryEnsureCardIndex(pairedCardIndex, out message))
            {
                return false;
            }


            _cards[cardIndex].IsFaceUp = true;
            _cards[pairedCardIndex].IsFaceUp = true;
            _revealedCardIndices.Clear();
            _revealedCardIndices.Add(pairedCardIndex);
            _revealedCardIndices.Add(cardIndex);


            if (resultOwner < _playerNames.Length)
            {
                _currentTurnIndex = resultOwner;
                _turnDeadlineTick = tickCount + 11600;
                _statusMessage = $"Mismatch pending. {_playerNames[_currentTurnIndex]} takes the next turn after flip-back.";
                _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Packet mismatch received. Waiting for time-over flip-back.");
            }
            else
            {
                int scoringPlayerIndex = resultOwner - _playerNames.Length;
                if (!IsValidPlayerIndex(scoringPlayerIndex))
                {
                    message = $"Turn-up packet used invalid scoring owner {resultOwner}.";
                    return false;
                }


                _cards[cardIndex].IsMatched = true;
                _cards[pairedCardIndex].IsMatched = true;
                _scores[scoringPlayerIndex]++;
                _currentTurnIndex = scoringPlayerIndex;
                _turnDeadlineTick = tickCount + 10000;
                _revealedCardIndices.Clear();
                _statusMessage = $"{_playerNames[scoringPlayerIndex]} found a pair.";
                _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {_playerNames[scoringPlayerIndex]} matched cards {pairedCardIndex} and {cardIndex}.");
            }


            SyncMiniRoomRuntime();
            message = _statusMessage;
            return true;
        }


        private bool TryApplyTimeOverPacket(PacketReader reader, int tickCount, out string message)
        {
            int currentTurnIndex = reader.ReadByte();
            if (_revealedCardIndices.Count > 0)
            {
                foreach (int revealedIndex in _revealedCardIndices)
                {
                    if (revealedIndex >= 0 && revealedIndex < _cards.Count && !_cards[revealedIndex].IsMatched)
                    {
                        _cards[revealedIndex].IsFaceUp = false;
                    }
                }


                _revealedCardIndices.Clear();

            }



            _currentTurnIndex = Math.Clamp(currentTurnIndex, 0, _playerNames.Length - 1);
            _turnDeadlineTick = tickCount + 10000;
            _statusMessage = $"{_playerNames[_currentTurnIndex]}'s turn.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Time-over packet returned the board to the next turn.");
            SyncMiniRoomRuntime();
            message = _statusMessage;
            return true;
        }


        private bool TryApplyGameResultPacket(PacketReader reader, int tickCount, out string message)
        {
            int resultType = reader.ReadByte();
            _stage = RoomStage.Result;
            _pendingHideTick = 0;
            _turnDeadlineTick = 0;
            _resultExpireTick = tickCount + DefaultResultSeconds * 1000;


            if (resultType == 1)
            {
                _lastWinnerIndex = -1;
                _draws[0]++;
                _draws[1]++;
                _statusMessage = "Round complete. Draw.";
                _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Game-result packet ended the round in a draw.");
                SyncMiniRoomRuntime();
                message = _statusMessage;
                return true;
            }


            int winnerIndex = reader.ReadByte();
            if (!IsValidPlayerIndex(winnerIndex))
            {
                message = $"Game-result packet used invalid winner index {winnerIndex}.";
                return false;
            }


            int loserIndex = winnerIndex == 0 ? 1 : 0;
            _lastWinnerIndex = winnerIndex;
            _wins[winnerIndex]++;
            _losses[loserIndex]++;
            _statusMessage = $"Round complete. {_playerNames[winnerIndex]} wins.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : Game-result packet declared {_playerNames[winnerIndex]} the winner.");
            SyncMiniRoomRuntime();
            message = _statusMessage;
            return true;
        }


        private void InitializeBoardFromPacket(IReadOnlyList<int> shuffle)
        {
            _cards.Clear();
            _revealedCardIndices.Clear();
            _scores[0] = 0;
            _scores[1] = 0;
            _pendingHideTick = 0;
            _resultExpireTick = 0;
            _pendingRemoteActions.Clear();


            for (int i = 0; i < shuffle.Count; i++)
            {
                _cards.Add(new Card
                {
                    FaceId = Math.Max(0, shuffle[i]),
                    IsFaceUp = false,
                    IsMatched = false
                });
            }
        }


        private static void ResolveBoardDimensions(int cardCount, out int rows, out int columns)
        {
            rows = 2;
            columns = Math.Max(2, cardCount / 2);
            int bestDifference = int.MaxValue;
            for (int candidateRows = 2; candidateRows <= cardCount; candidateRows++)
            {
                if (cardCount % candidateRows != 0)
                {
                    continue;
                }


                int candidateColumns = cardCount / candidateRows;
                int difference = Math.Abs(candidateColumns - candidateRows);
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    rows = Math.Min(candidateRows, candidateColumns);
                    columns = Math.Max(candidateRows, candidateColumns);
                }
            }
        }


        private bool TryEnsureCardIndex(int cardIndex, out string message)
        {
            if (cardIndex < 0 || cardIndex >= _cards.Count)
            {
                message = $"Invalid card index: {cardIndex}.";
                return false;
            }


            message = string.Empty;

            return true;

        }



        private string ResolveRemotePlayerName()
        {
            int remotePlayerIndex = _localPlayerIndex == 0 ? 1 : 0;
            return string.IsNullOrWhiteSpace(_playerNames[remotePlayerIndex]) ? "Opponent" : _playerNames[remotePlayerIndex];
        }


        private static bool FailMiniRoomPacket(byte basePacketType, out string message)
        {
            message = $"MiniRoom base packet {basePacketType} is not modeled for Match Cards.";
            return false;
        }


        private static bool FailOfficialClientPacket(byte packetType, out string message)
        {
            message = $"Memory Game client packet {packetType} is not modeled for Match Cards.";
            return false;
        }


        private static bool IsMiniRoomBasePacketType(byte packetType)
        {
            return packetType == MiniRoomBaseEnterPacketType
                || packetType == MiniRoomBaseGameplayPacketType
                || packetType == MiniRoomBaseChatPacketType
                || packetType == MiniRoomBaseChatRepeatPacketType
                || packetType == MiniRoomBaseAvatarPacketType
                || packetType == MiniRoomBaseLeavePacketType;
        }


        private void DrawBoard(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, Rectangle area)
        {
            if (_cards.Count == 0)
            {
                DrawOutlinedText(spriteBatch, font, "No board yet", new Vector2(area.X + 96, area.Y + 124), Color.Black, Color.Black);
                return;
            }


            int gapX = _columns <= 0 ? 0 : Math.Max(6, (area.Width - (_columns * 49)) / (_columns + 1));

            int gapY = _rows <= 0 ? 0 : Math.Max(8, (area.Height - (_rows * 62)) / (_rows + 1));



            for (int index = 0; index < _cards.Count; index++)
            {
                int row = index / _columns;
                int column = index % _columns;
                Rectangle cardRect = new Rectangle(
                    area.X + gapX + column * (49 + gapX),
                    area.Y + gapY + row * (62 + gapY),
                    49,
                    62);


                Card card = _cards[index];
                Texture2D cardTexture = ResolveCardTexture(card);
                if (cardTexture != null)
                {
                    spriteBatch.Draw(cardTexture, new Vector2(cardRect.X, cardRect.Y), card.IsMatched ? Color.White * 0.82f : Color.White);
                    continue;
                }


                Color cardColor = card.IsMatched
                    ? new Color(111, 162, 85)
                    : card.IsFaceUp
                        ? new Color(246, 224, 167)
                        : new Color(145, 82, 42);


                spriteBatch.Draw(pixel, cardRect, cardColor);

            }



            if (_stage == RoomStage.Result)
            {
                Texture2D resultTexture = _lastWinnerIndex switch
                {
                    0 when _localPlayerIndex == 0 => _winTexture,
                    1 when _localPlayerIndex == 1 => _winTexture,
                    0 or 1 => _loseTexture,
                    _ => _drawTexture
                };


                if (resultTexture != null)
                {
                    Vector2 resultPosition = new(
                        area.Center.X - (resultTexture.Width / 2f),
                        area.Center.Y - (resultTexture.Height / 2f));
                    spriteBatch.Draw(resultTexture, resultPosition, Color.White);
                }
            }
        }

        private void DrawPromptOverlay(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, Rectangle outer)
        {
            if (!_pendingPrompt.IsActive)
            {
                return;
            }

            GetPromptLayout(outer, out Rectangle promptBox, out Rectangle yesRect, out Rectangle noRect);
            spriteBatch.Draw(pixel, promptBox, new Color(38, 24, 12, 230));
            spriteBatch.Draw(pixel, new Rectangle(promptBox.X + 1, promptBox.Y + 1, promptBox.Width - 2, promptBox.Height - 2), new Color(247, 232, 194, 240));
            DrawOutlinedText(spriteBatch, font, "Confirm", new Vector2(promptBox.X + 10, promptBox.Y + 8), Color.Black, new Color(96, 60, 20));

            float textY = promptBox.Y + 30;
            foreach (string line in WrapPromptText(font, _pendingPrompt.Text, promptBox.Width - 20))
            {
                DrawOutlinedText(spriteBatch, font, line, new Vector2(promptBox.X + 10, textY), Color.Black, new Color(72, 52, 24));
                textY += font.LineSpacing;
            }

            DrawButton(spriteBatch, pixel, font, yesRect.X, yesRect.Y, null, "Yes");
            DrawButton(spriteBatch, pixel, font, noRect.X, noRect.Y, null, "No");
        }


        private void HandleMiniRoomReadyRequested()
        {
            EnsureRoomOpenFromMiniRoomRuntime();
            int remotePlayerIndex = _localPlayerIndex == 0 ? 1 : 0;
            TryDispatchPacket(MemoryGamePacketType.SetReady, Environment.TickCount, out _, remotePlayerIndex, readyState: !_readyStates[remotePlayerIndex]);
        }


        private void HandleMiniRoomStartRequested()
        {
            EnsureRoomOpenFromMiniRoomRuntime();
            TryDispatchPacket(MemoryGamePacketType.SetReady, Environment.TickCount, out _, _localPlayerIndex, readyState: true);
            int remotePlayerIndex = _localPlayerIndex == 0 ? 1 : 0;
            TryDispatchPacket(MemoryGamePacketType.SetReady, Environment.TickCount, out _, remotePlayerIndex, readyState: true);
            TryDispatchPacket(MemoryGamePacketType.StartGame, Environment.TickCount, out _);
        }


        private void HandleMiniRoomModeRequested()
        {
            EnsureRoomOpenFromMiniRoomRuntime();
            TryDispatchPacket(MemoryGamePacketType.SelectMatchCardsMode, Environment.TickCount, out _);
        }


        private void EnsureRoomOpenFromMiniRoomRuntime()
        {
            if (_stage != RoomStage.Hidden)
            {
                return;
            }


            string ownerName = _miniRoomRuntime?.Occupants.Count > 0 ? _miniRoomRuntime.Occupants[0].Name : "Player";
            string guestName = _miniRoomRuntime?.Occupants.Count > 1 ? _miniRoomRuntime.Occupants[1].Name : "Opponent";
            string title = _miniRoomRuntime?.RoomTitle ?? "Match Cards";
            OpenRoom(title, ownerName, guestName, DefaultRows, DefaultColumns, DefaultLocalPlayerIndex);
        }


        private void ProcessRemoteActions(int tickCount)
        {
            while (_pendingRemoteActions.Count > 0 && tickCount >= _pendingRemoteActions.Peek().ExecuteTick)
            {
                PendingRemoteAction action = _pendingRemoteActions.Dequeue();
                MemoryGamePacketType packetType = action.ActionType switch
                {
                    RemoteActionType.Ready => MemoryGamePacketType.SetReady,
                    RemoteActionType.Start => MemoryGamePacketType.StartGame,
                    RemoteActionType.Reveal => MemoryGamePacketType.RevealCard,
                    RemoteActionType.Tie => MemoryGamePacketType.ClaimTie,
                    RemoteActionType.GiveUp => MemoryGamePacketType.GiveUp,
                    RemoteActionType.End => MemoryGamePacketType.SelectMatchCardsMode,
                    _ => MemoryGamePacketType.SelectMatchCardsMode
                };

                if (action.ActionType == RemoteActionType.End)
                {
                    TryRequestRoomExit(action.PlayerIndex, out _);
                    continue;
                }


                TryDispatchPacket(packetType, tickCount, out _, action.PlayerIndex, action.CardIndex, action.ReadyState);

            }

        }



        private void SyncMiniRoomRuntime()
        {
            if (_miniRoomRuntime == null)
            {
                return;
            }


            string roomState = _stage switch
            {
                RoomStage.Hidden => "Board closed",
                RoomStage.Lobby => "Waiting for ready check",
                RoomStage.Playing => $"{_playerNames[_currentTurnIndex]}'s turn ({CurrentTurnTimeRemainingSeconds}s)",
                RoomStage.Result => _lastWinnerIndex >= 0 ? $"{_playerNames[_lastWinnerIndex]} won the round" : "Round ended in a draw",
                _ => string.Empty
            };


            List<SocialRoomOccupant> extraOccupants = BuildMiniRoomExtraOccupants();

            CharacterBuild ownerBuild = ResolveParticipantAvatarBuild(0);

            CharacterBuild guestBuild = ResolveParticipantAvatarBuild(1);

            string roomStatus = BuildRoomStatusMessage();



            _miniRoomRuntime.SyncMiniRoomMatchCards(
                _title,
                ResolvePrimaryParticipantName(0),
                ResolvePrimaryParticipantName(1),
                _readyStates[0],
                _readyStates[1],
                _scores[0],
                _scores[1],
                _currentTurnIndex,
                roomStatus,
                roomState,
                BuildParticipantDetail(0, includeScore: true),
                BuildParticipantDetail(1, includeScore: true),
                ownerBuild,
                guestBuild,
                extraOccupants);
        }


        private List<SocialRoomOccupant> BuildMiniRoomExtraOccupants()
        {
            List<SocialRoomOccupant> occupants = new();
            foreach (KeyValuePair<int, MiniRoomParticipantState> entry in _miniRoomParticipants.OrderBy(entry => entry.Key))
            {
                int slot = entry.Key;
                if (slot < 2)
                {
                    continue;
                }


                occupants.Add(new SocialRoomOccupant(
                    ResolveParticipantName(slot),
                    SocialRoomOccupantRole.Visitor,
                    BuildParticipantDetail(slot, includeScore: false),
                    isReady: false,
                    avatarBuild: entry.Value.AvatarBuild?.Clone()));
            }


            return occupants;

        }



        private bool TryDecodeMiniRoomParticipant(PacketReader reader, int slot, out MiniRoomParticipantState participant, out string message)
        {
            participant = null;
            if (slot < 0)
            {
                message = $"MiniRoom enter packet used invalid slot {slot}.";
                return false;
            }


            if (!LoginAvatarLookCodec.TryDecode(reader, out LoginAvatarLook avatarLook, out string decodeError))
            {
                message = decodeError;
                return false;
            }


            string name = reader.ReadMapleString();
            short jobCode = reader.ReadShort();
            participant = UpsertMiniRoomParticipant(slot, name, jobCode, avatarLook);
            message = string.Empty;
            return true;
        }


        private bool TryDecodeMiniRoomAvatar(PacketReader reader, int slot, out MiniRoomParticipantState participant, out string message)
        {
            participant = null;
            if (slot < 0)
            {
                message = $"MiniRoom avatar packet used invalid slot {slot}.";
                return false;
            }


            if (!LoginAvatarLookCodec.TryDecode(reader, out LoginAvatarLook avatarLook, out string decodeError))
            {
                message = decodeError;
                return false;
            }


            participant = UpsertMiniRoomParticipant(slot, null, null, avatarLook);
            message = string.Empty;
            return true;
        }


        private MiniRoomParticipantState UpsertMiniRoomParticipant(int slot, string name, short? jobCode, LoginAvatarLook avatarLook)
        {
            if (!_miniRoomParticipants.TryGetValue(slot, out MiniRoomParticipantState participant))
            {
                participant = new MiniRoomParticipantState(slot);
                _miniRoomParticipants[slot] = participant;
            }


            if (!string.IsNullOrWhiteSpace(name))
            {
                participant.Name = name.Trim();
                if (slot < _playerNames.Length)
                {
                    _playerNames[slot] = participant.Name;
                }
            }


            if (jobCode.HasValue)
            {
                participant.JobCode = jobCode.Value;
            }


            if (avatarLook != null)
            {
                participant.AvatarLook = avatarLook;
                participant.AvatarBuild = CreateMiniRoomAvatarBuild(avatarLook);
            }


            return participant;

        }



        private string ResolvePrimaryParticipantName(int slot)
        {
            if (_miniRoomParticipants.TryGetValue(slot, out MiniRoomParticipantState participant)
                && !string.IsNullOrWhiteSpace(participant.Name))
            {
                return participant.Name;
            }


            return _playerNames[slot];

        }



        private string ResolveParticipantName(int slot)
        {
            if (_miniRoomParticipants.TryGetValue(slot, out MiniRoomParticipantState participant)
                && !string.IsNullOrWhiteSpace(participant.Name))
            {
                return participant.Name;
            }


            if (slot >= 0 && slot < _playerNames.Length && !string.IsNullOrWhiteSpace(_playerNames[slot]))
            {
                return _playerNames[slot];
            }


            return $"Seat {slot}";

        }



        private string BuildParticipantDetail(int slot, bool includeScore)
        {
            List<string> detailParts = new() { ResolveSeatLabel(slot) };
            if (includeScore && slot >= 0 && slot < _scores.Length)
            {
                detailParts.Add($"Score {_scores[slot]}");
                if (_currentTurnIndex == slot && _stage == RoomStage.Playing)
                {
                    detailParts.Add("Current turn");
                }
            }


            if (slot >= 0 && slot < _leaveBookingStates.Length && _leaveBookingStates[slot])
            {
                detailParts.Add("Leaving after round");
            }


            if (_miniRoomParticipants.TryGetValue(slot, out MiniRoomParticipantState participant))
            {
                if (participant.JobCode > 0)
                {
                    detailParts.Add($"Job {participant.JobCode}");
                }


                if (participant.AvatarLook != null)
                {
                    detailParts.Add($"Face {participant.AvatarLook.FaceId}");
                    detailParts.Add($"Hair {participant.AvatarLook.HairId}");
                }
            }


            return string.Join(" | ", detailParts);

        }



        private CharacterBuild ResolveParticipantAvatarBuild(int slot)
        {
            if (slot == _localPlayerIndex && _localMiniRoomAvatarBuild != null)
            {
                return _localMiniRoomAvatarBuild.Clone();
            }


            if (_miniRoomParticipants.TryGetValue(slot, out MiniRoomParticipantState participant))
            {
                return participant.AvatarBuild?.Clone();
            }


            return null;

        }



        private CharacterBuild CreateMiniRoomAvatarBuild(LoginAvatarLook avatarLook)
        {
            if (avatarLook == null || _miniRoomAvatarBuildFactory == null)
            {
                return null;
            }


            try
            {
                return _miniRoomAvatarBuildFactory(avatarLook)?.Clone();
            }
            catch
            {
                return null;
            }
        }


        private static string ResolveSeatLabel(int slot)
        {
            return slot switch
            {
                0 => "Host seat",
                1 => "Guest seat",
                _ => $"Visitor seat {slot}"
            };
        }


        private static string NormalizeMiniRoomChatText(string rawText, ref string speakerName)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return string.Empty;
            }


            int separatorIndex = rawText.IndexOf(" : ", StringComparison.Ordinal);
            if (separatorIndex > 0)
            {
                string parsedSpeaker = rawText[..separatorIndex].Trim();
                string parsedMessage = rawText[(separatorIndex + 3)..].Trim();
                if (!string.IsNullOrWhiteSpace(parsedSpeaker))
                {
                    speakerName = parsedSpeaker;
                }


                if (!string.IsNullOrWhiteSpace(parsedMessage))
                {
                    return parsedMessage;
                }
            }


            return rawText.Trim();

        }



        private static string ResolveMiniRoomGameMessage(int gameMessageCode, string characterName)
        {
            string resolvedName = string.IsNullOrWhiteSpace(characterName) ? "The other player" : characterName.Trim();
            if (!MiniRoomGameMessages.TryGetValue(gameMessageCode, out MiniRoomGameMessageDefinition definition))
            {
                return $"MiniRoom game message {gameMessageCode} for {resolvedName}.";
            }


            string text = definition.FallbackText.Contains("{0}", StringComparison.Ordinal)
                ? string.Format(CultureInfo.InvariantCulture, definition.FallbackText, resolvedName)
                : definition.FallbackText;
            return $"{text} [StringPool 0x{definition.StringPoolId:X}]";
        }


        private static int TranslateLeaveReasonToGameMessageCode(int leaveReason)
        {
            return leaveReason switch
            {
                2 => 103,
                3 => 102,
                _ => 4
            };
        }


        private bool HandlePrimarySidebarAction(int tickCount, out string message)
        {
            if (_stage == RoomStage.Lobby && !_readyStates[_localPlayerIndex])
            {
                TryDispatchPacket(MemoryGamePacketType.SetReady, tickCount, out message, _localPlayerIndex, readyState: true);
                return true;
            }


            if (_stage == RoomStage.Lobby)
            {
                TryDispatchPacket(MemoryGamePacketType.StartGame, tickCount, out message);
                return true;
            }


            message = "The primary Memory Game button is only available from the lobby.";

            return true;

        }



        private string GetPrimaryButtonLabel()
        {
            if (_stage == RoomStage.Lobby && !_readyStates[_localPlayerIndex])
            {
                return "Ready";
            }


            return "Start";

        }

        private bool TryOpenPrompt(MemoryGamePromptType type, int stringPoolId, int playerIndex, string text, out string message)
        {
            if (_pendingPrompt.IsActive)
            {
                message = "Finish the current Match Cards confirmation prompt first.";
                return false;
            }

            _pendingPrompt = new MemoryGamePromptState(type, stringPoolId, playerIndex, text);
            _statusMessage = text;
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {text}");
            SyncMiniRoomRuntime();
            message = text;
            return true;
        }

        private bool ConfirmOutgoingTieRequest(int tickCount, out string message)
        {
            _localTieRequestSent = true;
            return TryClaimTie(out message);
        }

        private bool ConfirmIncomingTieRequest(int tickCount, out string message)
        {
            return TryClaimTie(out message);
        }

        private bool ConfirmGiveUp(int playerIndex, out string message)
        {
            if (playerIndex == _localPlayerIndex)
            {
                _localGiveUpRequestSent = true;
            }

            return TryGiveUp(playerIndex, out message);
        }

        private void ClearPendingPrompt()
        {
            _pendingPrompt = default;
        }

        private static bool AssignPromptMissing(out string message)
        {
            message = "The Match Cards prompt could not be resolved.";
            return false;
        }


        private string BuildRoomStatusMessage()
        {
            string leaveStatus = BuildLeaveBookingSummary();
            string promptStatus = _pendingPrompt.IsActive
                ? $"Prompt: {_pendingPrompt.Text}"
                : string.Empty;
            string combinedStatus = string.Join(
                " ",
                new[] { _statusMessage, leaveStatus, promptStatus }.Where(part => !string.IsNullOrWhiteSpace(part)));
            return string.IsNullOrWhiteSpace(combinedStatus) ? _statusMessage : combinedStatus;
        }


        private string BuildLeaveBookingSummary()
        {
            List<string> pendingSeats = new();
            for (int i = 0; i < _leaveBookingStates.Length; i++)
            {
                if (_leaveBookingStates[i])
                {
                    pendingSeats.Add(ResolveParticipantName(i));
                }
            }


            return pendingSeats.Count == 0
                ? string.Empty
                : $"Pending leave: {string.Join(", ", pendingSeats)}.";
        }


        private string GetExitButtonLabel()
        {
            if (_stage == RoomStage.Lobby)
            {
                return "End";
            }


            return _leaveBookingStates[_localPlayerIndex] ? "Stay" : "Leave";
        }

        private void GetPromptLayout(Rectangle outer, out Rectangle promptBox, out Rectangle yesRect, out Rectangle noRect)
        {
            int promptX = outer.Center.X - (ClientPromptBoxWidth / 2);
            int promptY = outer.Center.Y - (ClientPromptBoxHeight / 2);
            promptBox = new Rectangle(promptX, promptY, ClientPromptBoxWidth, ClientPromptBoxHeight);
            yesRect = new Rectangle(promptBox.X + 26, promptBox.Bottom - 30, ClientPromptButtonWidth, ClientPromptButtonHeight);
            noRect = new Rectangle(promptBox.Right - 26 - ClientPromptButtonWidth, promptBox.Bottom - 30, ClientPromptButtonWidth, ClientPromptButtonHeight);
        }

        private IEnumerable<string> WrapPromptText(SpriteFont font, string text, int maxWidth)
        {
            if (font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder line = new();
            foreach (string word in words)
            {
                string candidate = line.Length == 0 ? word : $"{line} {word}";
                if (font.MeasureString(candidate).X <= maxWidth || line.Length == 0)
                {
                    line.Clear();
                    line.Append(candidate);
                    continue;
                }

                yield return line.ToString();
                line.Clear();
                line.Append(word);
            }

            if (line.Length > 0)
            {
                yield return line.ToString();
            }
        }

        private static string ResolveMemoryGamePromptText(int stringPoolId, string name = null)
        {
            string resolvedName = string.IsNullOrWhiteSpace(name) ? "The other player" : name.Trim();
            string text = stringPoolId switch
            {
                MemoryGameGiveUpPromptStringPoolId => "Would you like to give up this Match Cards round?",
                MemoryGameIncomingTiePromptStringPoolId => string.Format(CultureInfo.InvariantCulture, "{0} requested a tie. Accept it?", resolvedName),
                MemoryGameOutgoingTiePromptStringPoolId => "Would you like to request a tie?",
                MemoryGameTieResultNoticeStringPoolId => "The other player declined the tie request.",
                MemoryGameBookLeavePromptStringPoolId => "Would you like to leave the room after this round?",
                MemoryGameCancelLeavePromptStringPoolId => "Cancel the pending leave request after this round?",
                MemoryGameCloseRoomPromptStringPoolId => "Would you like to close the Match Cards room?",
                _ => $"Match Cards prompt 0x{stringPoolId:X}."
            };

            return $"{text} [StringPool 0x{stringPoolId:X}]";
        }


        private bool TryResolveLobbyExit(int playerIndex, out string message)
        {
            if (playerIndex == 0 || playerIndex == _localPlayerIndex)
            {
                return TryEndRoom(out message);
            }


            _leaveBookingStates[playerIndex] = false;
            _readyStates[playerIndex] = false;
            string playerName = ResolveParticipantName(playerIndex);
            _miniRoomParticipants.Remove(playerIndex);
            _playerNames[playerIndex] = playerIndex == 1 ? "Opponent" : $"Seat {playerIndex}";
            _statusMessage = ResolveMiniRoomGameMessage(4, playerName);
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {_statusMessage}");
            SyncMiniRoomRuntime();
            message = $"{playerName} left the Match Cards room from the lobby.";
            return true;
        }


        private bool TryResolveBookedLeaveAfterRound(out string message)
        {
            message = null;
            for (int playerIndex = 0; playerIndex < _leaveBookingStates.Length; playerIndex++)
            {
                if (!_leaveBookingStates[playerIndex])
                {
                    continue;
                }


                _leaveBookingStates[playerIndex] = false;
                string playerName = ResolveParticipantName(playerIndex);
                if (playerIndex == 0 || playerIndex == _localPlayerIndex)
                {
                    Reset();
                    message = $"{playerName} left the Match Cards room after the round.";
                    _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {message}");
                    return true;
                }


                _readyStates[playerIndex] = false;
                _miniRoomParticipants.Remove(playerIndex);
                _playerNames[playerIndex] = playerIndex == 1 ? "Opponent" : $"Seat {playerIndex}";
                message = $"{playerName} left the Match Cards room after the round.";
                _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {message}");
            }


            return !string.IsNullOrWhiteSpace(message);
        }



        private void GetLayout(int viewportWidth, int viewportHeight, out Rectangle outer, out Rectangle boardArea, out Rectangle sidebar, out Rectangle[] buttonRects)
        {
            int dialogWidth = _backgroundTexture?.Width ?? ClientDialogWidth;
            int dialogHeight = _backgroundTexture?.Height ?? ClientDialogHeight;
            int dialogX = viewportWidth / 2 - dialogWidth / 2;
            int dialogY = Math.Max(24, viewportHeight / 2 - dialogHeight / 2);
            outer = new Rectangle(dialogX, dialogY, dialogWidth, dialogHeight);
            boardArea = new Rectangle(dialogX + ClientBoardLeft, dialogY + ClientBoardTop, ClientBoardWidth, ClientBoardHeight);
            sidebar = new Rectangle(dialogX + ClientRecordPanelX, dialogY + ClientRecordPanelY, 300, 132);
            buttonRects = new Rectangle[5];
            buttonRects[0] = CreateButtonRect(dialogX + ClientReadyButtonX, dialogY + ClientReadyButtonY, _readyStates[_localPlayerIndex] ? _startButtonTexture : _readyButtonTexture, 96, 29);
            buttonRects[1] = CreateButtonRect(dialogX + ClientTieButtonX, dialogY + ClientTieButtonY, _tieButtonTexture, 43, 18);
            buttonRects[2] = CreateButtonRect(dialogX + ClientGiveUpButtonX, dialogY + ClientGiveUpButtonY, _giveUpButtonTexture, 43, 18);
            buttonRects[3] = CreateButtonRect(dialogX + ClientEndButtonX, dialogY + ClientEndButtonY, _endButtonTexture, 43, 18);
            buttonRects[4] = CreateButtonRect(dialogX + ClientBanButtonX, dialogY + ClientBanButtonY, _banButtonTexture, 11, 11);
        }


        private int GetCardIndexAt(Point mousePosition, Rectangle area)
        {
            if (_cards.Count == 0 || !area.Contains(mousePosition))
            {
                return -1;
            }


            int gapX = _columns <= 0 ? 0 : Math.Max(6, (area.Width - (_columns * 49)) / (_columns + 1));

            int gapY = _rows <= 0 ? 0 : Math.Max(8, (area.Height - (_rows * 62)) / (_rows + 1));



            for (int index = 0; index < _cards.Count; index++)
            {
                int row = index / _columns;
                int column = index % _columns;
                Rectangle cardRect = new Rectangle(
                    area.X + gapX + column * (49 + gapX),
                    area.Y + gapY + row * (62 + gapY),
                    49,
                    62);
                if (cardRect.Contains(mousePosition))
                {
                    return index;
                }
            }


            return -1;

        }



        private void DrawNameBar(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, string name, int score, int x, int y, bool isActiveTurn)
        {
            Rectangle rect = new Rectangle(x, y, 174, 28);
            spriteBatch.Draw(pixel, rect, isActiveTurn ? new Color(223, 196, 120) : new Color(132, 103, 73));
            DrawOutlinedText(spriteBatch, font, name, new Vector2(x + 8, y + 5), Color.Black, Color.White);
            DrawOutlinedText(spriteBatch, font, score.ToString(), new Vector2(x + 146, y + 5), Color.Black, Color.White);
        }


        private void DrawClientNamePanel(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, string name, int score, int x, int y, bool isReady, bool isActiveTurn, bool isLeftPanel)

        {

            DrawNameBar(spriteBatch, pixel, font, name, score, x, y, isActiveTurn);



            Texture2D readyTexture = isReady ? _readyOnTexture : _readyOffTexture;
            if (readyTexture != null)
            {
                int readyX = isLeftPanel ? ClientReadyIndicatorLeftX : ClientReadyIndicatorRightX;
                spriteBatch.Draw(readyTexture, new Vector2(x - ClientNameBarLeftX + readyX, y - ClientNameBarY + ClientReadyIndicatorY), Color.White);
            }
        }


        private void DrawClientButtons(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, int dialogX, int dialogY)
        {
            DrawButton(spriteBatch, pixel, font, dialogX + ClientReadyButtonX, dialogY + ClientReadyButtonY, _readyStates[_localPlayerIndex] ? _startButtonTexture : _readyButtonTexture, GetPrimaryButtonLabel());
            DrawButton(spriteBatch, pixel, font, dialogX + ClientTieButtonX, dialogY + ClientTieButtonY, _tieButtonTexture, "Tie");
            DrawButton(spriteBatch, pixel, font, dialogX + ClientGiveUpButtonX, dialogY + ClientGiveUpButtonY, _giveUpButtonTexture, "Give Up");
            DrawButton(spriteBatch, pixel, font, dialogX + ClientEndButtonX, dialogY + ClientEndButtonY, _endButtonTexture, GetExitButtonLabel());
            DrawButton(spriteBatch, pixel, font, dialogX + ClientBanButtonX, dialogY + ClientBanButtonY, _banButtonTexture, string.Empty);
        }


        private void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, int x, int y, Texture2D texture, string label)
        {
            if (texture != null)
            {
                spriteBatch.Draw(texture, new Vector2(x, y), Color.White);
            }
            else
            {
                spriteBatch.Draw(pixel, new Rectangle(x, y, 64, 22), new Color(119, 84, 48));
            }


            if (!string.IsNullOrWhiteSpace(label))
            {
                DrawOutlinedText(spriteBatch, font, label, new Vector2(x + 8, y + 6), Color.Black, Color.White);
            }
        }


        private void DrawClientRecordSummary(SpriteBatch spriteBatch, SpriteFont font, int dialogX, int dialogY)
        {
            int localIndex = Math.Clamp(_localPlayerIndex, 0, _wins.Length - 1);
            DrawBitmapNumber(spriteBatch, _scores[0], dialogX + ClientScoreLeftX, dialogY + ClientScoreY);
            DrawBitmapNumber(spriteBatch, _scores[1], dialogX + ClientScoreRightX, dialogY + ClientScoreY);
            DrawOutlinedText(spriteBatch, font, $"W {_wins[localIndex]}  L {_losses[localIndex]}  D {_draws[localIndex]}", new Vector2(dialogX + 409, dialogY + 210), Color.Black, new Color(48, 48, 48));
            DrawOutlinedText(spriteBatch, font, $"Packet: {_lastPacketType?.ToString() ?? "None"}", new Vector2(dialogX + 409, dialogY + 228), Color.Black, new Color(48, 48, 48));
            DrawOutlinedText(spriteBatch, font, $"Room: {_stage}", new Vector2(dialogX + 409, dialogY + 246), Color.Black, new Color(48, 48, 48));
        }


        private void DrawBitmapNumber(SpriteBatch spriteBatch, int value, int x, int y)
        {
            string scoreText = Math.Clamp(value, 0, 99).ToString("00");
            foreach (char digit in scoreText)
            {
                int index = digit - '0';
                Texture2D texture = index >= 0 && index < _digitTextures.Length ? _digitTextures[index] : null;
                if (texture == null)
                {
                    return;
                }


                spriteBatch.Draw(texture, new Vector2(x, y), Color.White);
                x += texture.Width - 1;
            }
        }


        private void DrawClientTurnIndicator(SpriteBatch spriteBatch, int dialogX, int dialogY)
        {
            if (_turnTexture == null || _stage != RoomStage.Playing)
            {
                return;
            }


            spriteBatch.Draw(_turnTexture, new Vector2(dialogX + ResolveTurnIndicatorX(), dialogY + ClientTurnIndicatorY), Color.White);

        }



        public static bool TryParsePacketType(string text, out MemoryGamePacketType packetType)
        {
            packetType = MemoryGamePacketType.OpenRoom;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }


            string normalized = text.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
            return normalized.ToLowerInvariant() switch
            {
                "open" => AssignPacket(MemoryGamePacketType.OpenRoom, out packetType),
                "ready" or "unready" => AssignPacket(MemoryGamePacketType.SetReady, out packetType),
                "start" => AssignPacket(MemoryGamePacketType.StartGame, out packetType),
                "flip" or "reveal" => AssignPacket(MemoryGamePacketType.RevealCard, out packetType),
                "ban" or "expel" => AssignPacket(MemoryGamePacketType.BanUser, out packetType),
                "tie" => AssignPacket(MemoryGamePacketType.ClaimTie, out packetType),
                "giveup" => AssignPacket(MemoryGamePacketType.GiveUp, out packetType),
                "end" or "close" => AssignPacket(MemoryGamePacketType.EndRoom, out packetType),
                "mode" or "matchcards" => AssignPacket(MemoryGamePacketType.SelectMatchCardsMode, out packetType),
                _ => Enum.TryParse(normalized, true, out packetType)
            };
        }


        private bool TryDispatchOpenPacket(string title, string playerOneName, string playerTwoName, int rows, int columns, int localPlayerIndex, out string message)
        {
            OpenRoom(title, playerOneName ?? "Player", playerTwoName ?? "Opponent", rows, columns, localPlayerIndex);
            message = DescribeStatus();
            return true;
        }


        private bool TrySelectMatchCardsMode(out string message)
        {
            EnsureRoomOpenFromMiniRoomRuntime();
            _statusMessage = "Match Cards room selected.";
            message = _statusMessage;
            SyncMiniRoomRuntime();
            return true;
        }


        private static bool AssignUnsupportedPacket(MemoryGamePacketType packetType, out string message)
        {
            message = $"Unsupported Memory Game packet: {packetType}.";
            return false;
        }


        private static bool AssignPacket(MemoryGamePacketType value, out MemoryGamePacketType packetType)
        {
            packetType = value;
            return true;
        }


        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _graphicsDevice == null)
            {
                return;
            }


            WzImage uiWindow2Image = global::HaCreator.Program.FindImage("UI", "UIWindow2.img");
            WzImage uiWindow1Image = global::HaCreator.Program.FindImage("UI", "UIWindow.img");
            WzSubProperty minigameRoot = uiWindow2Image?["Minigame"] as WzSubProperty
                ?? uiWindow1Image?["Minigame"] as WzSubProperty;
            WzSubProperty memoryGameProperty = minigameRoot?["MemoryGame"] as WzSubProperty;
            WzSubProperty commonProperty = minigameRoot?["Common"] as WzSubProperty;


            _backgroundTexture = LoadCanvasTexture(memoryGameProperty?["backgrnd"] as WzCanvasProperty);
            _masterPanelTexture = LoadCanvasTexture(memoryGameProperty?["backgrnd2"] as WzCanvasProperty);
            _turnTexture = LoadCanvasTexture(commonProperty?["turn"] as WzCanvasProperty);
            _readyOnTexture = LoadCanvasTexture(commonProperty?["readyOn"] as WzCanvasProperty);
            _readyOffTexture = LoadCanvasTexture(commonProperty?["readyOff"] as WzCanvasProperty);
            _winTexture = LoadCanvasTexture(commonProperty?["win"] as WzCanvasProperty);
            _loseTexture = LoadCanvasTexture(commonProperty?["lose"] as WzCanvasProperty);
            _drawTexture = LoadCanvasTexture(commonProperty?["draw"] as WzCanvasProperty);
            _readyButtonTexture = LoadButtonTexture(commonProperty, "btReady");
            _startButtonTexture = LoadButtonTexture(commonProperty, "btStart");
            _tieButtonTexture = LoadButtonTexture(commonProperty, "btDraw");
            _giveUpButtonTexture = LoadButtonTexture(commonProperty, "btAbsten");
            _endButtonTexture = LoadButtonTexture(commonProperty, "btExit");
            _banButtonTexture = LoadButtonTexture(commonProperty, "btBan");


            WzImageProperty numberProperty = memoryGameProperty?["number"];
            for (int i = 0; i < _digitTextures.Length; i++)
            {
                _digitTextures[i] = LoadCanvasTexture(numberProperty?[i.ToString()] as WzCanvasProperty);
            }


            WzImageProperty cardProperty = memoryGameProperty?["card"];
            for (int i = 0; i < _cardFaceTextures.Length; i++)
            {
                _cardFaceTextures[i] = LoadCanvasTexture(cardProperty?[i.ToString()] as WzCanvasProperty);
            }


            for (int i = 0; i < _cardBackTextures.Length; i++)
            {
                _cardBackTextures[i] = LoadCanvasTexture(cardProperty?[$"back{i}"] as WzCanvasProperty);
            }


            _assetsLoaded = true;

        }



        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            if (_graphicsDevice == null || canvas == null)
            {
                return null;
            }


            using var bitmap = canvas.GetLinkedWzCanvasBitmap();

            return bitmap?.ToTexture2DAndDispose(_graphicsDevice);

        }



        private Texture2D LoadButtonTexture(WzSubProperty commonProperty, string buttonName)
        {
            return LoadCanvasTexture(commonProperty?[buttonName]?["normal"]?["0"] as WzCanvasProperty);
        }


        private Texture2D ResolveCardTexture(Card card)
        {
            if (card == null)
            {
                return null;
            }


            if (!card.IsFaceUp && !card.IsMatched)
            {
                return _cardBackTextures[0];
            }


            if (card.FaceId < 0 || card.FaceId >= _cardFaceTextures.Length)
            {
                return null;
            }


            return _cardFaceTextures[card.FaceId];

        }



        private int ResolveTurnIndicatorX()
        {
            if (_currentTurnIndex == 0)
            {
                return _localPlayerIndex != 0 ? ClientTurnIndicatorLeftX : ClientTurnIndicatorRightX;
            }


            return _localPlayerIndex != 0 ? ClientTurnIndicatorRightX : ClientTurnIndicatorLeftX;

        }



        private static Rectangle CreateButtonRect(int x, int y, Texture2D texture, int fallbackWidth, int fallbackHeight)
        {
            return new Rectangle(x, y, texture?.Width ?? fallbackWidth, texture?.Height ?? fallbackHeight);
        }


        private static void DrawOutlinedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color shadowColor, Color textColor)
        {
            spriteBatch.DrawString(font, text, position + Vector2.One, shadowColor);
            spriteBatch.DrawString(font, text, position, textColor);
        }


        private bool IsValidPlayerIndex(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < _playerNames.Length;
        }
    }
    #endregion
}
