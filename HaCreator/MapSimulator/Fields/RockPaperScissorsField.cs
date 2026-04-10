using HaSharedLibrary.Util;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace HaCreator.MapSimulator.Fields
{
    public enum RockPaperScissorsChoice
    {
        None = -1,
        Rock = 0,
        Paper = 1,
        Scissor = 2
    }

    public enum RockPaperScissorsMainButtonType
    {
        Start = 3000,
        Continue = 3001,
        Retry = 3002
    }

    public enum RockPaperScissorsResultType
    {
        None,
        Win,
        Lose,
        Draw,
        TimeOver
    }

    public enum RockPaperScissorsClientRequestType
    {
        Start = 0,
        Select = 1,
        Timeout = 2,
        Continue = 3,
        Exit = 4,
        Retry = 5
    }

    public sealed class RockPaperScissorsClientPacket
    {
        public RockPaperScissorsClientPacket(int opcode, RockPaperScissorsClientRequestType requestType, RockPaperScissorsChoice choice, byte[] payload, string summary)
        {
            Opcode = opcode;
            RequestType = requestType;
            Choice = choice;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Summary = summary ?? string.Empty;
        }

        public int Opcode { get; }
        public RockPaperScissorsClientRequestType RequestType { get; }
        public RockPaperScissorsChoice Choice { get; }
        public byte[] Payload { get; }
        public string Summary { get; }
    }

    public sealed class RockPaperScissorsField
    {
        public const int OwnerOpcode = 371;
        public const int ClientOpcode = 160;
        public const string ClientDialogOwnerName = "CRPSGameDlg";
        public const int OpenNoticeStringPoolId = 0xE83;
        public const int WinNoticeStringPoolId = 3724;
        public const int LoseNoticeStringPoolId = 3723;
        public const int DrawSoundStringPoolId = 0x645;
        public const int WinSoundStringPoolId = 0x646;
        public const int LoseSoundStringPoolId = 0x647;
        public const int TimerSoundStringPoolId = 0x648;
        public const int SwitchSoundStringPoolId = 0x64D;
        public const int StartTipStringPoolId = 0xE84;
        public const int TimeLeftTipStringPoolId = 0xE85;
        public const int ContinueTipStringPoolId = 0xE86;
        public const int ClearTipStringPoolId = 0xE87;
        public const int RetryTipStringPoolId = 0xE88;
        public const int CompensationRetryTipStringPoolId = 0xE89;
        public const int LoseRetryTipStringPoolId = 0xE8A;
        public const int DialogWidth = 310;
        public const int DialogHeight = 358;
        internal const int TipViewportWidth = 270;
        public const int MainButtonX = 108;
        public const int MainButtonY = 202;
        public const int ExitButtonX = 242;
        public const int ExitButtonY = 335;
        public const int ChoiceButtonBaseX = 19;
        public const int ChoiceButtonSpacing = 41;
        public const int ChoiceButtonY = 104;
        private const int ChoiceCount = 3;
        private const int ChoiceSwitchCadenceMs = 120;
        private const int RoundLimitMs = 30000;
        private const int ResultFadeDelayMs = 1000;
        private const int ResultExpireDelayMs = 3000;

        private readonly Texture2D[] _choiceTextures = new Texture2D[ChoiceCount];
        private readonly Texture2D[] _choiceFlashTextures = new Texture2D[ChoiceCount];
        private readonly Rectangle[] _choiceButtonRects = new Rectangle[ChoiceCount];
        private readonly Queue<RockPaperScissorsClientPacket> _pendingClientPackets = new();
        private GraphicsDevice _graphicsDevice;
        private bool _assetsLoaded;
        private Texture2D _backgroundTexture;
        private Texture2D _winTexture;
        private Texture2D _loseTexture;
        private Texture2D _drawTexture;
        private Texture2D _timeOverTexture;
        private Texture2D _charWinTexture;
        private Texture2D _charLoseTexture;
        private Texture2D _mainStartTexture;
        private Texture2D _mainContinueTexture;
        private Texture2D _mainRetryTexture;
        private Texture2D _exitTexture;
        private Func<bool> _hasUniqueModelessOwnerConflict;
        private Rectangle _mainButtonRect;
        private Rectangle _exitButtonRect;
        private uint _entryDialogValue;
        private bool _isVisible;
        private bool _choiceButtonsEnabled;
        private bool _mainButtonEnabled;
        private bool _exitButtonEnabled;
        private bool _requestSent;
        private bool _receiveCompensation;
        private int? _pendingNoticeStringPoolId;
        private int _currentNpcDisplayIndex;
        private int _lastSwitchTick;
        private int _switchCadenceMs;
        private int _roundDeadlineTick;
        private int _resultRevealTick;
        private int _resultExpireTick;
        private bool _resultLayerVisible;
        private RockPaperScissorsChoice _playerChoice = RockPaperScissorsChoice.None;
        private RockPaperScissorsChoice _npcChoice = RockPaperScissorsChoice.None;
        private RockPaperScissorsMainButtonType _mainButtonType = RockPaperScissorsMainButtonType.Start;
        private RockPaperScissorsResultType _resultType = RockPaperScissorsResultType.None;
        private string _pendingNoticeMessage;
        private string _tipText = string.Empty;
        private int _tipTextLength;
        private int _tipPosition;
        private uint _lastTipOption = uint.MaxValue;
        private string _lastMinigameSound = string.Empty;
        private bool _tipLayoutDirty = true;
        private int _lastTimerSoundSecond = int.MinValue;

        public bool IsVisible => _isVisible;
        public bool ChoiceButtonsEnabled => _choiceButtonsEnabled;
        public bool MainButtonEnabled => _mainButtonEnabled;
        public bool ExitButtonEnabled => _exitButtonEnabled;
        public bool RequestSent => _requestSent;
        public bool ReceiveCompensation => _receiveCompensation;
        public uint EntryDialogValue => _entryDialogValue;
        public int StraightVictoryCount { get; private set; }
        public int LastPacketType { get; private set; }
        public string LastDialogOwner { get; private set; } = "none";
        public string LastPacketSummary { get; private set; } = "No Rock-Paper-Scissors packet applied yet.";
        public string CurrentStatusMessage { get; private set; } = "Rock-Paper-Scissors dialog inactive.";
        public RockPaperScissorsChoice PlayerChoice => _playerChoice;
        public RockPaperScissorsChoice NpcChoice => _npcChoice;
        public RockPaperScissorsResultType ResultType => _resultType;
        public RockPaperScissorsMainButtonType MainButtonType => _mainButtonType;
        public string CurrentTipText => _tipText;
        public int CurrentTipPosition => _tipPosition;
        public string LastMinigameSound => _lastMinigameSound;
        internal bool IsResultLayerVisible => _resultLayerVisible;

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        public void SetUniqueModelessOwnerConflictEvaluator(Func<bool> evaluator)
        {
            _hasUniqueModelessOwnerConflict = evaluator;
        }

        public void Update(int currentTick)
        {
            if (!_isVisible)
            {
                return;
            }

            if (_choiceButtonsEnabled && _npcChoice == RockPaperScissorsChoice.None && currentTick >= _lastSwitchTick + ChoiceSwitchCadenceMs)
            {
                _currentNpcDisplayIndex = (_currentNpcDisplayIndex + 1) % ChoiceCount;
                _lastSwitchTick = currentTick;
                PlayMinigameSound(SwitchSoundStringPoolId);
            }

            if (_switchCadenceMs > 0 && _npcChoice != RockPaperScissorsChoice.None && currentTick >= _lastSwitchTick + _switchCadenceMs)
            {
                _currentNpcDisplayIndex = (_currentNpcDisplayIndex + 1) % ChoiceCount;
                _lastSwitchTick = currentTick;
                _switchCadenceMs *= 2;
                if (_switchCadenceMs >= 720 && _currentNpcDisplayIndex == (int)_npcChoice)
                {
                    _switchCadenceMs = 0;
                    ShowResult(currentTick);
                    CurrentStatusMessage = $"RPS switching settled on {DescribeChoice(_npcChoice)} and entered ShowResult.";
                    LastPacketSummary = $"switch-settle -> npc={DescribeChoice(_npcChoice)} + ShowResult";
                }
            }

            if (_choiceButtonsEnabled && _roundDeadlineTick > 0 && currentTick >= _roundDeadlineTick)
            {
                QueueClientPacket(RockPaperScissorsClientRequestType.Timeout, RockPaperScissorsChoice.None);
                _choiceButtonsEnabled = false;
                _roundDeadlineTick = 0;
                _switchCadenceMs = 0;
                _requestSent = true;
                CurrentStatusMessage = "RPS round reached the 30000 ms limit, sent client opcode 160 subtype 2, and is waiting for a server-owned follow-up.";
                LastPacketSummary = "client timeout -> opcode=160 subtype=2";
            }
            else if (_choiceButtonsEnabled && _roundDeadlineTick > currentTick)
            {
                int remainingMs = _roundDeadlineTick - currentTick;
                int remainingSeconds = (remainingMs + 999) / 1000;
                if (remainingMs < 10000 && remainingSeconds != _lastTimerSoundSecond)
                {
                    _lastTimerSoundSecond = remainingSeconds;
                    PlayMinigameSound(TimerSoundStringPoolId);
                }
            }

            if (_resultRevealTick > 0 && currentTick >= _resultRevealTick && _resultType != RockPaperScissorsResultType.None)
            {
                _resultRevealTick = 0;
                _resultLayerVisible = true;
                PlayMinigameSound(ResolveResultSoundStringPoolId());
                CurrentStatusMessage = $"RPS result layer became visible after the client-owned {ResultFadeDelayMs} ms reveal delay.";
                LastPacketSummary = $"result-reveal -> {DescribeResultType(_resultType)}";
            }

            if (_resultExpireTick > 0 && currentTick >= _resultExpireTick && _resultType != RockPaperScissorsResultType.None)
            {
                _resultRevealTick = 0;
                _resultLayerVisible = false;
                _resultExpireTick = 0;
                if (_resultType == RockPaperScissorsResultType.Draw)
                {
                    BeginRound(currentTick);
                    CurrentStatusMessage = "RPS draw result expired and immediately re-armed the next live round.";
                    LastPacketSummary = "draw-expire -> round-start switching=120 limit=30000";
                    return;
                }

                _mainButtonType = ResolvePostResultMainButtonType();
                _mainButtonEnabled = true;
                _exitButtonEnabled = true;
                UpdateTipText(currentTick);
                CurrentStatusMessage = $"{DescribeResultType(_resultType)} result expired and restored the client-owned main/exit button state.";
                _resultExpireTick = 0;
            }

            UpdateTipText(currentTick);
            AdvanceTipScroll();
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isVisible || spriteBatch == null || pixelTexture == null || font == null)
            {
                return;
            }

            EnsureAssetsLoaded();
            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            int panelX = Math.Max(0, (viewport.Width - DialogWidth) / 2);
            int panelY = Math.Max(0, (viewport.Height - DialogHeight) / 2);
            Rectangle panelRect = new Rectangle(panelX, panelY, DialogWidth, DialogHeight);

            if (_backgroundTexture != null)
            {
                spriteBatch.Draw(_backgroundTexture, panelRect, Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, panelRect, new Color(30, 26, 22, 228));
            }

            for (int i = 0; i < ChoiceCount; i++)
            {
                Rectangle rect = Translate(_choiceButtonRects[i], panelX, panelY);
                bool isSelected = _playerChoice == (RockPaperScissorsChoice)i;
                Color tint = _choiceButtonsEnabled ? Color.White : new Color(180, 180, 180, 255);
                if (isSelected)
                {
                    spriteBatch.Draw(pixelTexture, Inflate(rect, 2), new Color(238, 208, 97, 160));
                }

                if (_choiceTextures[i] != null)
                {
                    spriteBatch.Draw(_choiceTextures[i], rect, tint);
                }
                else
                {
                    spriteBatch.Draw(pixelTexture, rect, tint);
                }
            }

            Rectangle mainRect = Translate(_mainButtonRect, panelX, panelY);
            Rectangle exitRect = Translate(_exitButtonRect, panelX, panelY);
            DrawButton(spriteBatch, pixelTexture, ResolveMainButtonTexture(), mainRect, _mainButtonEnabled);
            DrawButton(spriteBatch, pixelTexture, _exitTexture, exitRect, _exitButtonEnabled);

            Texture2D npcTexture = ResolveNpcDisplayTexture();
            if (npcTexture != null)
            {
                Rectangle npcRect = new Rectangle(panelX + 203, panelY + 103, npcTexture.Width, npcTexture.Height);
                spriteBatch.Draw(npcTexture, npcRect, Color.White);
            }

            Texture2D resultTexture = _resultLayerVisible ? ResolveResultTexture() : null;
            if (resultTexture != null)
            {
                Rectangle resultRect = new Rectangle(
                    panelX + (DialogWidth - resultTexture.Width) / 2,
                    panelY + 225,
                    resultTexture.Width,
                    resultTexture.Height);
                spriteBatch.Draw(resultTexture, resultRect, Color.White);

                Texture2D badgeTexture = ResolveBadgeTexture();
                if (badgeTexture != null)
                {
                    Rectangle badgeRect = new Rectangle(resultRect.Right - badgeTexture.Width, resultRect.Top - 8, badgeTexture.Width, badgeTexture.Height);
                    spriteBatch.Draw(badgeTexture, badgeRect, Color.White);
                }
            }

            DrawShadowedText(spriteBatch, font, "Rock-Paper-Scissors", new Vector2(panelX + 12, panelY + 10), Color.White);
            DrawShadowedText(
                spriteBatch,
                font,
                $"owner={ClientDialogOwnerName} | opcode={OwnerOpcode} | last subtype={LastPacketType}",
                new Vector2(panelX + 12, panelY + 28),
                Color.Gainsboro,
                0.82f);
            DrawShadowedText(
                spriteBatch,
                font,
                TrimForDisplay(LastPacketSummary, 50),
                new Vector2(panelX + 12, panelY + 46),
                Color.LightGoldenrodYellow,
                0.8f);
            DrawShadowedText(
                spriteBatch,
                font,
                TrimForDisplay(CurrentStatusMessage, 52),
                new Vector2(panelX + 12, panelY + 64),
                Color.White,
                0.8f);

            string stateText = $"main={_mainButtonType} | streak={StraightVictoryCount} | choice={DescribeChoice(_playerChoice)} | npc={DescribeChoice(_npcChoice)}";
            DrawShadowedText(spriteBatch, font, stateText, new Vector2(panelX + 12, panelY + 82), Color.Silver, 0.78f);
            RefreshTipLayout(font);
            DrawShadowedText(spriteBatch, font, _tipText, new Vector2(panelX + 20 + _tipPosition, panelY + 294), Color.Black, 0.76f);
        }

        public static bool TryParsePacketType(string token, out int packetType)
        {
            packetType = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetType))
            {
                return IsSupportedPacketType(packetType);
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "open":
                    packetType = 8;
                    return true;
                case "destroy":
                case "close":
                    packetType = 13;
                    return true;
                case "win":
                    packetType = 6;
                    return true;
                case "lose":
                    packetType = 7;
                    return true;
                case "start":
                    packetType = 9;
                    return true;
                case "forceresult":
                case "showresult":
                    packetType = 10;
                    return true;
                case "result":
                case "npcpick":
                    packetType = 11;
                    return true;
                case "continue":
                    packetType = 12;
                    return true;
                case "reset":
                    packetType = 14;
                    return true;
                default:
                    return false;
            }
        }

        public bool TryApplyRawPacket(int packetType, byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            payload ??= Array.Empty<byte>();
            LastPacketType = packetType;

            try
            {
                switch (packetType)
                {
                    case 8:
                        return TryApplyOpenPacket(payload, currentTimeMs, out errorMessage);
                    case 13:
                        return TryApplyDestroyPacket(currentTimeMs, out errorMessage);
                    case 6:
                    case 7:
                    case 9:
                    case 10:
                    case 11:
                    case 12:
                    case 14:
                        if (!_isVisible)
                        {
                            errorMessage = $"{ClientDialogOwnerName} is not the active unique-modeless owner.";
                            return false;
                        }

                        ProcessPacket(packetType, payload, currentTimeMs);
                        return true;
                    default:
                        errorMessage = $"Unsupported RPS packet subtype: {packetType}.";
                        return false;
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is InvalidDataException || ex is IOException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public bool HandleMouse(
            Point mousePosition,
            int viewportWidth,
            int viewportHeight,
            bool leftClickReleased,
            out string message)
        {
            message = null;
            if (!_isVisible || !leftClickReleased)
            {
                return false;
            }

            int panelX = Math.Max(0, (viewportWidth - DialogWidth) / 2);
            int panelY = Math.Max(0, (viewportHeight - DialogHeight) / 2);

            if (Translate(_exitButtonRect, panelX, panelY).Contains(mousePosition))
            {
                if (!_exitButtonEnabled)
                {
                    message = "RPS exit is disabled until the current client-owned transition completes.";
                    return true;
                }

                SendMainClientRequest(RockPaperScissorsClientRequestType.Exit);
                message = "Sent client RPS exit request opcode 160 subtype 4 and disabled main/exit until the server follow-up.";
                return true;
            }

            if (Translate(_mainButtonRect, panelX, panelY).Contains(mousePosition))
            {
                if (!_mainButtonEnabled)
                {
                    message = "RPS main button is disabled until the next client-owned packet transition.";
                    return true;
                }

                RockPaperScissorsClientRequestType requestType = ResolveMainButtonClientRequestType();
                SendMainClientRequest(requestType);
                message = $"Sent client RPS {DescribeClientRequestType(requestType)} request opcode 160 subtype {(int)requestType} and disabled main/exit until the server follow-up.";
                return true;
            }

            if (_choiceButtonsEnabled)
            {
                for (int i = 0; i < ChoiceCount; i++)
                {
                    if (!Translate(_choiceButtonRects[i], panelX, panelY).Contains(mousePosition))
                    {
                        continue;
                    }

                    SendSelection((RockPaperScissorsChoice)i);
                    message = CurrentStatusMessage;
                    return true;
                }
            }

            return false;
        }

        public bool TrySelectChoice(RockPaperScissorsChoice choice, out string message)
        {
            message = null;
            if (!_isVisible)
            {
                message = $"{ClientDialogOwnerName} is not the active unique-modeless owner.";
                return false;
            }

            if (!_choiceButtonsEnabled)
            {
                message = "RPS choice buttons are disabled until the next client-owned round start.";
                return false;
            }

            if (choice is < RockPaperScissorsChoice.Rock or > RockPaperScissorsChoice.Scissor)
            {
                message = "RPS choice must be rock, paper, or scissor.";
                return false;
            }

            SendSelection(choice);
            message = CurrentStatusMessage;
            return true;
        }

        public bool TryActivateMainButton(int currentTimeMs, out string message)
        {
            message = null;
            if (!_isVisible)
            {
                message = $"{ClientDialogOwnerName} is not the active unique-modeless owner.";
                return false;
            }

            if (!_mainButtonEnabled)
            {
                message = "RPS main button is disabled until the next client-owned packet transition.";
                return false;
            }

            RockPaperScissorsClientRequestType requestType = ResolveMainButtonClientRequestType();
            SendMainClientRequest(requestType);
            message = $"Sent client RPS {DescribeClientRequestType(requestType)} request opcode 160 subtype {(int)requestType} and disabled main/exit until the server follow-up.";
            return true;
        }

        public string DescribeStatus()
        {
            if (!_isVisible)
            {
                return $"RPS: hidden | opcode={OwnerOpcode} | owner={LastDialogOwner} | last={LastPacketSummary}";
            }

            return $"RPS: visible | opcode={OwnerOpcode} | clientOpcode={ClientOpcode} | owner={ClientDialogOwnerName} | entry={_entryDialogValue} | main={_mainButtonType} | buttons={(_choiceButtonsEnabled ? "enabled" : "disabled")} | requestSent={_requestSent} | player={DescribeChoice(_playerChoice)} | npc={DescribeChoice(_npcChoice)} | streak={StraightVictoryCount} | result={DescribeResultType(_resultType)} | compensation={_receiveCompensation} | sound={_lastMinigameSound} | tip={TrimForDisplay(_tipText, 40)} | summary={LastPacketSummary}";
        }

        public bool TryConsumePendingClientPacket(out RockPaperScissorsClientPacket packet)
        {
            if (_pendingClientPackets.Count > 0)
            {
                packet = _pendingClientPackets.Dequeue();
                return true;
            }

            packet = null;
            return false;
        }

        public bool TryConsumePendingNotice(out int stringPoolId, out string message)
        {
            if (_pendingNoticeStringPoolId is not int pendingStringPoolId
                || string.IsNullOrWhiteSpace(_pendingNoticeMessage))
            {
                stringPoolId = 0;
                message = null;
                return false;
            }

            stringPoolId = pendingStringPoolId;
            message = _pendingNoticeMessage;
            _pendingNoticeStringPoolId = null;
            _pendingNoticeMessage = null;
            return true;
        }

        public void Reset()
        {
            _isVisible = false;
            _choiceButtonsEnabled = false;
            _mainButtonEnabled = true;
            _exitButtonEnabled = true;
            _requestSent = false;
            _receiveCompensation = false;
            _entryDialogValue = 0;
            _currentNpcDisplayIndex = 0;
            _lastSwitchTick = 0;
            _switchCadenceMs = 0;
            _roundDeadlineTick = 0;
            _resultRevealTick = 0;
            _resultExpireTick = 0;
            _resultLayerVisible = false;
            _playerChoice = RockPaperScissorsChoice.None;
            _npcChoice = RockPaperScissorsChoice.None;
            _resultType = RockPaperScissorsResultType.None;
            _mainButtonType = RockPaperScissorsMainButtonType.Start;
            StraightVictoryCount = 0;
            LastDialogOwner = "none";
            CurrentStatusMessage = "Rock-Paper-Scissors dialog inactive.";
            LastPacketSummary = "No Rock-Paper-Scissors packet applied yet.";
            LastPacketType = 0;
            ClearPendingNotice();
            _pendingClientPackets.Clear();
            _tipText = string.Empty;
            _tipTextLength = 0;
            _tipPosition = 0;
            _lastTipOption = uint.MaxValue;
            _lastMinigameSound = string.Empty;
            _tipLayoutDirty = true;
            _lastTimerSoundSecond = int.MinValue;
        }

        private bool TryApplyOpenPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (_isVisible || _hasUniqueModelessOwnerConflict?.Invoke() == true)
            {
                errorMessage = "Rock-Paper-Scissors dialog cannot open while another unique-modeless owner is already active.";
                return false;
            }

            if (payload.Length < sizeof(uint))
            {
                errorMessage = "RPS subtype 8 payload must contain a uint32 dialog value.";
                return false;
            }

            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream);
            _entryDialogValue = reader.ReadUInt32();
            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException("Unexpected trailing bytes in RPS subtype 8 payload.");
            }

            _isVisible = true;
            _playerChoice = RockPaperScissorsChoice.None;
            _npcChoice = RockPaperScissorsChoice.None;
            _resultType = RockPaperScissorsResultType.None;
            _choiceButtonsEnabled = false;
            _mainButtonEnabled = true;
            _exitButtonEnabled = true;
            _requestSent = false;
            _receiveCompensation = false;
            _mainButtonType = RockPaperScissorsMainButtonType.Start;
            StraightVictoryCount = 0;
            LastDialogOwner = ClientDialogOwnerName;
            CurrentStatusMessage = $"{ResolveOpenNoticeText()} [StringPool 0x{OpenNoticeStringPoolId:X}]";
            LastPacketSummary = $"open (8) notice -> {ClientDialogOwnerName} entry={_entryDialogValue}";
            QueuePacketOwnedNotice(OpenNoticeStringPoolId, ResolveOpenNoticeText());
            _roundDeadlineTick = 0;
            _resultRevealTick = 0;
            _switchCadenceMs = 0;
            _resultExpireTick = 0;
            _resultLayerVisible = false;
            UpdateTipText(currentTimeMs);
            return true;
        }

        private bool TryApplyDestroyPacket(int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (!_isVisible)
            {
                errorMessage = $"{ClientDialogOwnerName} is not active.";
                return false;
            }

            LastPacketSummary = "destroy (13) -> CWnd::Destroy(unique-modeless owner)";
            CurrentStatusMessage = "RPS owner destroyed through subtype 13.";
            Reset();
            LastPacketSummary = "destroy (13) -> CWnd::Destroy(unique-modeless owner)";
            LastPacketType = 13;
            return true;
        }

        private void ProcessPacket(int packetType, byte[] payload, int currentTimeMs)
        {
            switch (packetType)
            {
                case 6:
                case 7:
                case 14:
                    ClearPendingNotice();
                    _choiceButtonsEnabled = false;
                    _mainButtonEnabled = true;
                    _exitButtonEnabled = true;
                    _requestSent = false;
                    _receiveCompensation = false;
                    _npcChoice = RockPaperScissorsChoice.None;
                    _playerChoice = RockPaperScissorsChoice.None;
                    StraightVictoryCount = 0;
                    _roundDeadlineTick = 0;
                    _resultRevealTick = 0;
                    _switchCadenceMs = 0;
                    _resultExpireTick = 0;
                    _resultLayerVisible = false;
                    _resultType = RockPaperScissorsResultType.None;
                    _mainButtonType = RockPaperScissorsMainButtonType.Start;
                    UpdateTipText(currentTimeMs);
                    CurrentStatusMessage = packetType switch
                    {
                        6 => $"{ResolveWinNoticeText()} [StringPool 0x{WinNoticeStringPoolId:X}]",
                        7 => $"{ResolveLoseNoticeText()} [StringPool 0x{LoseNoticeStringPoolId:X}]",
                        _ => "RPS round reset without a client notice."
                    };
                    LastPacketSummary = packetType switch
                    {
                        6 => "reset (6) -> main button restored + notice 3724",
                        7 => "reset (7) -> main button restored + notice 3723",
                        _ => "reset (14) -> main button restored without a notice"
                    };
                    if (packetType == 6)
                    {
                        QueuePacketOwnedNotice(WinNoticeStringPoolId, ResolveWinNoticeText());
                    }
                    else if (packetType == 7)
                    {
                        QueuePacketOwnedNotice(LoseNoticeStringPoolId, ResolveLoseNoticeText());
                    }
                    break;

                case 9:
                case 12:
                    ClearPendingNotice();
                    BeginRound(currentTimeMs);
                    UpdateTipText(currentTimeMs);
                    CurrentStatusMessage = $"RPS subtype {packetType} started a live round, cleared the NPC choice, armed the 30000 ms limit, and enabled the three RPS buttons.";
                    LastPacketSummary = $"round-start ({packetType}) -> switching=120 limit=30000";
                    break;

                case 10:
                    ClearPendingNotice();
                    _choiceButtonsEnabled = false;
                    _roundDeadlineTick = 0;
                    _switchCadenceMs = 0;
                    _receiveCompensation = false;
                    _npcChoice = RockPaperScissorsChoice.None;
                    StraightVictoryCount = -1;
                    ShowResult(currentTimeMs);
                    UpdateTipText(currentTimeMs);
                    CurrentStatusMessage = "RPS subtype 10 forced immediate result presentation by clearing switching and routing into ShowResult.";
                    LastPacketSummary = "force-result (10) -> streak=-1 + ShowResult";
                    break;

                case 11:
                    ClearPendingNotice();
                    using (var stream = new MemoryStream(payload, writable: false))
                    using (var reader = new BinaryReader(stream))
                    {
                        _npcChoice = (RockPaperScissorsChoice)reader.ReadByte();
                        sbyte decodedStraightVictories = reader.ReadSByte();
                        _receiveCompensation = decodedStraightVictories < 0 && StraightVictoryCount == 0;
                        StraightVictoryCount = decodedStraightVictories;
                        if (stream.Position != stream.Length)
                        {
                            throw new InvalidDataException("Unexpected trailing bytes in RPS subtype 11 payload.");
                        }
                    }

                    CurrentStatusMessage = $"RPS subtype 11 decoded NPC pick {DescribeChoice(_npcChoice)} and streak {StraightVictoryCount}; compensation={_receiveCompensation}.";
                    LastPacketSummary = $"result-payload (11) -> npc={DescribeChoice(_npcChoice)} streak={StraightVictoryCount}";
                    break;
            }
        }

        private void ShowResult(int currentTimeMs)
        {
            if (StraightVictoryCount < 0)
            {
                _resultType = _npcChoice == RockPaperScissorsChoice.None
                    ? RockPaperScissorsResultType.TimeOver
                    : RockPaperScissorsResultType.Lose;
                _mainButtonType = RockPaperScissorsMainButtonType.Retry;
            }
            else if (_playerChoice != RockPaperScissorsChoice.None && _playerChoice == _npcChoice)
            {
                _resultType = RockPaperScissorsResultType.Draw;
                _mainButtonType = RockPaperScissorsMainButtonType.Continue;
            }
            else
            {
                _resultType = EvaluateRoundResult(_playerChoice, _npcChoice)
                    ? RockPaperScissorsResultType.Win
                    : RockPaperScissorsResultType.Lose;
                _mainButtonType = _resultType == RockPaperScissorsResultType.Win
                    ? RockPaperScissorsMainButtonType.Continue
                    : RockPaperScissorsMainButtonType.Retry;
            }

            _choiceButtonsEnabled = false;
            _requestSent = false;
            _roundDeadlineTick = 0;
            _mainButtonEnabled = false;
            _exitButtonEnabled = false;
            _resultLayerVisible = false;
            _resultRevealTick = currentTimeMs + ResultFadeDelayMs;
            _resultExpireTick = currentTimeMs + ResultExpireDelayMs;
            UpdateTipText(currentTimeMs);
        }

        private static bool IsSupportedPacketType(int packetType)
        {
            return packetType is 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14;
        }

        private void BeginRound(int currentTimeMs)
        {
            _npcChoice = RockPaperScissorsChoice.None;
            _playerChoice = RockPaperScissorsChoice.None;
            _resultType = RockPaperScissorsResultType.None;
            _requestSent = false;
            _receiveCompensation = false;
            _choiceButtonsEnabled = true;
            _mainButtonEnabled = false;
            _exitButtonEnabled = false;
            _switchCadenceMs = ChoiceSwitchCadenceMs;
            _lastSwitchTick = currentTimeMs;
            _roundDeadlineTick = currentTimeMs + RoundLimitMs;
            _resultRevealTick = 0;
            _resultExpireTick = 0;
            _resultLayerVisible = false;
            _lastTimerSoundSecond = int.MinValue;
            UpdateTipText(currentTimeMs);
        }

        private RockPaperScissorsMainButtonType ResolvePostResultMainButtonType()
        {
            if (StraightVictoryCount <= 0 || StraightVictoryCount >= 10)
            {
                return RockPaperScissorsMainButtonType.Retry;
            }

            return RockPaperScissorsMainButtonType.Continue;
        }

        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _graphicsDevice == null)
            {
                return;
            }

            WzImage uiWindow2Image = global::HaCreator.Program.FindImage("UI", "UIWindow2.img");
            WzImage uiWindow1Image = global::HaCreator.Program.FindImage("UI", "UIWindow.img");
            WzSubProperty rpsProperty = uiWindow2Image?["RpsGame"] as WzSubProperty
                ?? uiWindow1Image?["RpsGame"] as WzSubProperty;
            if (rpsProperty == null)
            {
                return;
            }

            _backgroundTexture = LoadCanvasTexture(rpsProperty["backgrnd"] as WzCanvasProperty);
            _winTexture = LoadCanvasTexture(rpsProperty["win"] as WzCanvasProperty);
            _loseTexture = LoadCanvasTexture(rpsProperty["lose"] as WzCanvasProperty);
            _drawTexture = LoadCanvasTexture(rpsProperty["draw"] as WzCanvasProperty);
            _timeOverTexture = LoadCanvasTexture(rpsProperty["timeover"] as WzCanvasProperty);
            _charWinTexture = LoadCanvasTexture(rpsProperty["charWin"] as WzCanvasProperty);
            _charLoseTexture = LoadCanvasTexture(rpsProperty["charLose"] as WzCanvasProperty);
            _mainStartTexture = LoadButtonTexture(rpsProperty, "BtStart");
            _mainContinueTexture = LoadButtonTexture(rpsProperty, "BtContinue");
            _mainRetryTexture = LoadButtonTexture(rpsProperty, "BtRetry");
            _exitTexture = LoadButtonTexture(rpsProperty, "BtExit");
            _choiceTextures[0] = LoadCanvasTexture(rpsProperty["rock"] as WzCanvasProperty);
            _choiceTextures[1] = LoadCanvasTexture(rpsProperty["paper"] as WzCanvasProperty);
            _choiceTextures[2] = LoadCanvasTexture(rpsProperty["scissor"] as WzCanvasProperty);
            _choiceFlashTextures[0] = LoadCanvasTexture(rpsProperty["Frock"] as WzCanvasProperty);
            _choiceFlashTextures[1] = LoadCanvasTexture(rpsProperty["Fpaper"] as WzCanvasProperty);
            _choiceFlashTextures[2] = LoadCanvasTexture(rpsProperty["Fscissor"] as WzCanvasProperty);

            for (int i = 0; i < ChoiceCount; i++)
            {
                Texture2D texture = _choiceTextures[i];
                int width = texture?.Width ?? 87;
                int height = texture?.Height ?? 77;
                _choiceButtonRects[i] = new Rectangle(ChoiceButtonBaseX + (ChoiceButtonSpacing * i), ChoiceButtonY, width, height);
            }

            Texture2D mainTexture = _mainStartTexture ?? _mainContinueTexture ?? _mainRetryTexture;
            _mainButtonRect = new Rectangle(MainButtonX, MainButtonY, mainTexture?.Width ?? 101, mainTexture?.Height ?? 35);
            _exitButtonRect = new Rectangle(ExitButtonX, ExitButtonY, _exitTexture?.Width ?? 59, _exitTexture?.Height ?? 17);
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

        private Texture2D LoadButtonTexture(WzSubProperty root, string buttonName)
        {
            return LoadCanvasTexture(root?[buttonName]?["normal"]?["0"] as WzCanvasProperty);
        }

        private Texture2D ResolveMainButtonTexture()
        {
            return _mainButtonType switch
            {
                RockPaperScissorsMainButtonType.Continue => _mainContinueTexture ?? _mainStartTexture,
                RockPaperScissorsMainButtonType.Retry => _mainRetryTexture ?? _mainStartTexture,
                _ => _mainStartTexture
            };
        }

        private Texture2D ResolveNpcDisplayTexture()
        {
            if (_npcChoice != RockPaperScissorsChoice.None)
            {
                return _choiceTextures[(int)_npcChoice];
            }

            return _choiceButtonsEnabled
                ? _choiceFlashTextures[_currentNpcDisplayIndex] ?? _choiceTextures[_currentNpcDisplayIndex]
                : null;
        }

        private Texture2D ResolveResultTexture()
        {
            return _resultType switch
            {
                RockPaperScissorsResultType.Win => _winTexture,
                RockPaperScissorsResultType.Lose => _loseTexture,
                RockPaperScissorsResultType.Draw => _drawTexture,
                RockPaperScissorsResultType.TimeOver => _timeOverTexture,
                _ => null
            };
        }

        private Texture2D ResolveBadgeTexture()
        {
            return _resultType switch
            {
                RockPaperScissorsResultType.Win => _charWinTexture,
                RockPaperScissorsResultType.Lose => _charLoseTexture,
                _ => null
            };
        }

        private static bool EvaluateRoundResult(RockPaperScissorsChoice playerChoice, RockPaperScissorsChoice npcChoice)
        {
            return (playerChoice, npcChoice) switch
            {
                (RockPaperScissorsChoice.Rock, RockPaperScissorsChoice.Scissor) => true,
                (RockPaperScissorsChoice.Paper, RockPaperScissorsChoice.Rock) => true,
                (RockPaperScissorsChoice.Scissor, RockPaperScissorsChoice.Paper) => true,
                _ => false
            };
        }

        private static Rectangle Translate(Rectangle rect, int offsetX, int offsetY)
        {
            return new Rectangle(rect.X + offsetX, rect.Y + offsetY, rect.Width, rect.Height);
        }

        private static Rectangle Inflate(Rectangle rect, int value)
        {
            return new Rectangle(rect.X - value, rect.Y - value, rect.Width + (value * 2), rect.Height + (value * 2));
        }

        private static void DrawButton(SpriteBatch spriteBatch, Texture2D pixelTexture, Texture2D texture, Rectangle rect, bool enabled)
        {
            Color tint = enabled ? Color.White : new Color(170, 170, 170, 255);
            if (texture != null)
            {
                spriteBatch.Draw(texture, rect, tint);
                return;
            }

            spriteBatch.Draw(pixelTexture, rect, enabled ? new Color(78, 92, 120, 220) : new Color(60, 60, 60, 220));
        }

        private static void DrawShadowedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color, float scale = 1f)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            spriteBatch.DrawString(font, text, position + new Vector2(1f, 1f), new Color(0, 0, 0, 180), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
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

        private static string ResolveOpenNoticeText() => MapleStoryStringPool.GetOrFallback(OpenNoticeStringPoolId, "Rock-Paper-Scissors challenge opened.");

        private static string ResolveWinNoticeText() => MapleStoryStringPool.GetOrFallback(WinNoticeStringPoolId, "Rock-Paper-Scissors round complete: win notice.");

        private static string ResolveLoseNoticeText() => MapleStoryStringPool.GetOrFallback(LoseNoticeStringPoolId, "Rock-Paper-Scissors round complete: lose notice.");

        private void QueuePacketOwnedNotice(int stringPoolId, string message)
        {
            if (stringPoolId <= 0 || string.IsNullOrWhiteSpace(message))
            {
                ClearPendingNotice();
                return;
            }

            _pendingNoticeStringPoolId = stringPoolId;
            _pendingNoticeMessage = message;
        }

        private void ClearPendingNotice()
        {
            _pendingNoticeStringPoolId = null;
            _pendingNoticeMessage = null;
        }

        private static string DescribeChoice(RockPaperScissorsChoice choice)
        {
            return choice switch
            {
                RockPaperScissorsChoice.Rock => "rock",
                RockPaperScissorsChoice.Paper => "paper",
                RockPaperScissorsChoice.Scissor => "scissor",
                _ => "none"
            };
        }

        private static string DescribeResultType(RockPaperScissorsResultType resultType)
        {
            return resultType switch
            {
                RockPaperScissorsResultType.Win => "win",
                RockPaperScissorsResultType.Lose => "lose",
                RockPaperScissorsResultType.Draw => "draw",
                RockPaperScissorsResultType.TimeOver => "time-over",
                _ => "none"
            };
        }

        private static int ResolveResultSoundStringPoolId(RockPaperScissorsResultType resultType)
        {
            return resultType switch
            {
                RockPaperScissorsResultType.Win => WinSoundStringPoolId,
                RockPaperScissorsResultType.Lose => LoseSoundStringPoolId,
                RockPaperScissorsResultType.Draw => DrawSoundStringPoolId,
                RockPaperScissorsResultType.TimeOver => TimerSoundStringPoolId,
                _ => 0
            };
        }

        private int ResolveResultSoundStringPoolId()
        {
            return ResolveResultSoundStringPoolId(_resultType);
        }

        private void PlayMinigameSound(int stringPoolId)
        {
            _lastMinigameSound = stringPoolId > 0
                ? MapleStoryStringPool.GetOrFallback(stringPoolId, $"StringPool 0x{stringPoolId:X}")
                : string.Empty;
        }

        private void QueueClientPacket(RockPaperScissorsClientRequestType requestType, RockPaperScissorsChoice choice)
        {
            byte[] payload = requestType == RockPaperScissorsClientRequestType.Select
                ? new[] { (byte)choice }
                : Array.Empty<byte>();
            string summary = requestType == RockPaperScissorsClientRequestType.Select
                ? $"opcode={ClientOpcode} subtype={(int)requestType} choice={DescribeChoice(choice)}"
                : $"opcode={ClientOpcode} subtype={(int)requestType}";
            _pendingClientPackets.Enqueue(new RockPaperScissorsClientPacket(ClientOpcode, requestType, choice, payload, summary));
        }

        private void SendMainClientRequest(RockPaperScissorsClientRequestType requestType)
        {
            QueueClientPacket(requestType, RockPaperScissorsChoice.None);
            _requestSent = true;
            _choiceButtonsEnabled = false;
            _mainButtonEnabled = false;
            _exitButtonEnabled = false;
            _roundDeadlineTick = 0;
            UpdateTipText(Environment.TickCount);
            CurrentStatusMessage = $"Queued client-owned RPS request {DescribeClientRequestType(requestType)}.";
            LastPacketSummary = $"client-request -> subtype={(int)requestType}";
        }

        private void SendSelection(RockPaperScissorsChoice choice)
        {
            _playerChoice = choice;
            _choiceButtonsEnabled = false;
            _mainButtonEnabled = false;
            _exitButtonEnabled = false;
            _requestSent = true;
            QueueClientPacket(RockPaperScissorsClientRequestType.Select, choice);
            PlayMinigameSound(SwitchSoundStringPoolId);
            UpdateTipText(Environment.TickCount);
            CurrentStatusMessage = $"Queued client-owned RPS selection {DescribeChoice(choice)} and disabled the owner buttons until the server follow-up.";
            LastPacketSummary = $"client-select -> {DescribeChoice(choice)}";
        }

        private RockPaperScissorsClientRequestType ResolveMainButtonClientRequestType()
        {
            return _mainButtonType switch
            {
                RockPaperScissorsMainButtonType.Continue => RockPaperScissorsClientRequestType.Continue,
                RockPaperScissorsMainButtonType.Retry => RockPaperScissorsClientRequestType.Retry,
                _ => RockPaperScissorsClientRequestType.Start
            };
        }

        private static string DescribeClientRequestType(RockPaperScissorsClientRequestType requestType)
        {
            return requestType switch
            {
                RockPaperScissorsClientRequestType.Start => "start",
                RockPaperScissorsClientRequestType.Select => "select",
                RockPaperScissorsClientRequestType.Timeout => "timeout",
                RockPaperScissorsClientRequestType.Continue => "continue",
                RockPaperScissorsClientRequestType.Exit => "exit",
                RockPaperScissorsClientRequestType.Retry => "retry",
                _ => requestType.ToString()
            };
        }

        private void UpdateTipText(int currentTick)
        {
            string nextTip;
            uint optionKey;

            if (!_isVisible)
            {
                nextTip = string.Empty;
                optionKey = uint.MaxValue;
            }
            else if (_choiceButtonsEnabled && _roundDeadlineTick > currentTick)
            {
                int remainingSeconds = Math.Max(0, (_roundDeadlineTick - currentTick + 999) / 1000);
                nextTip = FormatTipStringPoolText(TimeLeftTipStringPoolId, "Time left: {0}s", remainingSeconds);
                optionKey = 1;
            }
            else if (_resultType == RockPaperScissorsResultType.Win)
            {
                nextTip = MapleStoryStringPool.GetOrFallback(ClearTipStringPoolId, "Round cleared.");
                optionKey = 2;
            }
            else if (_resultType == RockPaperScissorsResultType.Lose || _resultType == RockPaperScissorsResultType.TimeOver)
            {
                int stringPoolId = _receiveCompensation ? CompensationRetryTipStringPoolId : LoseRetryTipStringPoolId;
                nextTip = MapleStoryStringPool.GetOrFallback(stringPoolId, "Try again.");
                optionKey = _receiveCompensation ? 3u : 4u;
            }
            else if (_mainButtonType == RockPaperScissorsMainButtonType.Continue)
            {
                nextTip = FormatTipStringPoolText(ContinueTipStringPoolId, "Continue to round {0}.", StraightVictoryCount + 1);
                optionKey = 5;
            }
            else if (_mainButtonType == RockPaperScissorsMainButtonType.Retry)
            {
                nextTip = MapleStoryStringPool.GetOrFallback(RetryTipStringPoolId, "Retry.");
                optionKey = 6;
            }
            else
            {
                nextTip = MapleStoryStringPool.GetOrFallback(StartTipStringPoolId, "Start.");
                optionKey = 0;
            }

            if (_lastTipOption != optionKey)
            {
                _lastTipOption = optionKey;
                _tipLayoutDirty = true;
            }

            if (!string.Equals(_tipText, nextTip ?? string.Empty, StringComparison.Ordinal))
            {
                _tipLayoutDirty = true;
            }

            _tipText = nextTip ?? string.Empty;
            if (_tipLayoutDirty && string.IsNullOrWhiteSpace(_tipText))
            {
                _tipTextLength = 0;
                _tipPosition = 0;
            }
        }

        internal static int ResolveTipStartPosition(int tipWidth)
        {
            return tipWidth <= TipViewportWidth
                ? (TipViewportWidth - Math.Max(0, tipWidth)) / 2
                : TipViewportWidth / 2;
        }

        internal static int AdvanceTipScrollPosition(int tipWidth, int currentPosition)
        {
            if (tipWidth <= TipViewportWidth)
            {
                return ResolveTipStartPosition(tipWidth);
            }

            int nextPosition = currentPosition - 1;
            return tipWidth + nextPosition < 0
                ? TipViewportWidth
                : nextPosition;
        }

        internal static string FormatTipStringPoolText(int stringPoolId, string fallbackFormat, params object[] args)
        {
            string template = MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackFormat) ?? fallbackFormat ?? string.Empty;
            string compositeFormat = template
                .Replace("%d", "{0}", StringComparison.Ordinal)
                .Replace("%ld", "{0}", StringComparison.Ordinal)
                .Replace("%s", "{0}", StringComparison.Ordinal);

            if (args == null || args.Length == 0 || !compositeFormat.Contains("{0}", StringComparison.Ordinal))
            {
                return compositeFormat;
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, compositeFormat, args);
            }
            catch (FormatException)
            {
                return fallbackFormat != null
                    ? string.Format(CultureInfo.InvariantCulture, fallbackFormat, args)
                    : compositeFormat;
            }
        }

        private void AdvanceTipScroll()
        {
            if (_tipLayoutDirty)
            {
                return;
            }

            _tipPosition = AdvanceTipScrollPosition(_tipTextLength, _tipPosition);
        }

        private void RefreshTipLayout(SpriteFont font)
        {
            if (!_tipLayoutDirty || font == null)
            {
                return;
            }

            _tipTextLength = string.IsNullOrWhiteSpace(_tipText)
                ? 0
                : (int)Math.Ceiling(font.MeasureString(_tipText).X);
            _tipPosition = ResolveTipStartPosition(_tipTextLength);
            _tipLayoutDirty = false;
        }
    }
}
