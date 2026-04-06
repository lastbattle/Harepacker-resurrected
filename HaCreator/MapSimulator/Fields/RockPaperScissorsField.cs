using HaSharedLibrary.Util;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
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

    public sealed class RockPaperScissorsField
    {
        public const int OwnerOpcode = 371;
        public const string ClientDialogOwnerName = "CRPSGameDlg";
        public const int OpenNoticeStringPoolId = 0xE83;
        public const int WinNoticeStringPoolId = 3724;
        public const int LoseNoticeStringPoolId = 3723;
        public const int DialogWidth = 310;
        public const int DialogHeight = 358;
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
        private Rectangle _mainButtonRect;
        private Rectangle _exitButtonRect;
        private uint _entryDialogValue;
        private bool _isVisible;
        private bool _choiceButtonsEnabled;
        private bool _mainButtonEnabled;
        private bool _exitButtonEnabled;
        private bool _requestSent;
        private bool _receiveCompensation;
        private int _currentNpcDisplayIndex;
        private int _lastSwitchTick;
        private int _switchCadenceMs;
        private int _roundDeadlineTick;
        private int _resultExpireTick;
        private RockPaperScissorsChoice _playerChoice = RockPaperScissorsChoice.None;
        private RockPaperScissorsChoice _npcChoice = RockPaperScissorsChoice.None;
        private RockPaperScissorsMainButtonType _mainButtonType = RockPaperScissorsMainButtonType.Start;
        private RockPaperScissorsResultType _resultType = RockPaperScissorsResultType.None;

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

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
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
                _choiceButtonsEnabled = false;
                _requestSent = false;
                _npcChoice = RockPaperScissorsChoice.None;
                StraightVictoryCount = -1;
                _roundDeadlineTick = 0;
                _switchCadenceMs = 0;
                ShowResult(currentTick);
                CurrentStatusMessage = "RPS round reached the 30000 ms limit and fell into the time-over result branch.";
                LastPacketSummary = "local timeout -> ShowResult(timeover)";
            }

            if (_resultExpireTick > 0 && currentTick >= _resultExpireTick && _resultType != RockPaperScissorsResultType.None)
            {
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
                CurrentStatusMessage = $"{DescribeResultType(_resultType)} result expired and restored the client-owned main/exit button state.";
                _resultExpireTick = 0;
            }
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

            Texture2D resultTexture = ResolveResultTexture();
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

                Reset();
                message = "Closed the local Rock-Paper-Scissors dialog preview.";
                return true;
            }

            if (Translate(_mainButtonRect, panelX, panelY).Contains(mousePosition))
            {
                if (!_mainButtonEnabled)
                {
                    message = "RPS main button is disabled until the next client-owned packet transition.";
                    return true;
                }

                int nextSubtype = _mainButtonType == RockPaperScissorsMainButtonType.Continue ? 12 : 9;
                if (!TryApplyRawPacket(nextSubtype, Array.Empty<byte>(), Environment.TickCount, out string error))
                {
                    message = error;
                    return true;
                }

                message = $"Applied local main-button preview via RPS subtype {nextSubtype}.";
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

                    _playerChoice = (RockPaperScissorsChoice)i;
                    _requestSent = true;
                    _choiceButtonsEnabled = false;
                    CurrentStatusMessage = $"Queued local {DescribeChoice(_playerChoice)} selection and disabled the three RPS buttons until the next packet-owned result.";
                    LastPacketSummary = $"local selection -> {DescribeChoice(_playerChoice)}";
                    message = CurrentStatusMessage;
                    return true;
                }
            }

            return false;
        }

        public string DescribeStatus()
        {
            if (!_isVisible)
            {
                return $"RPS: hidden | opcode={OwnerOpcode} | owner={LastDialogOwner} | last={LastPacketSummary}";
            }

            return $"RPS: visible | opcode={OwnerOpcode} | owner={ClientDialogOwnerName} | entry={_entryDialogValue} | main={_mainButtonType} | buttons={(_choiceButtonsEnabled ? "enabled" : "disabled")} | requestSent={_requestSent} | player={DescribeChoice(_playerChoice)} | npc={DescribeChoice(_npcChoice)} | streak={StraightVictoryCount} | result={DescribeResultType(_resultType)} | compensation={_receiveCompensation} | summary={LastPacketSummary}";
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
            _resultExpireTick = 0;
            _playerChoice = RockPaperScissorsChoice.None;
            _npcChoice = RockPaperScissorsChoice.None;
            _resultType = RockPaperScissorsResultType.None;
            _mainButtonType = RockPaperScissorsMainButtonType.Start;
            StraightVictoryCount = 0;
            LastDialogOwner = "none";
            CurrentStatusMessage = "Rock-Paper-Scissors dialog inactive.";
            LastPacketSummary = "No Rock-Paper-Scissors packet applied yet.";
            LastPacketType = 0;
        }

        private bool TryApplyOpenPacket(byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            if (_isVisible)
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
            _roundDeadlineTick = 0;
            _switchCadenceMs = 0;
            _resultExpireTick = 0;
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
                    _choiceButtonsEnabled = false;
                    _mainButtonEnabled = true;
                    _exitButtonEnabled = true;
                    _requestSent = false;
                    _npcChoice = RockPaperScissorsChoice.None;
                    _playerChoice = RockPaperScissorsChoice.None;
                    StraightVictoryCount = 0;
                    _roundDeadlineTick = 0;
                    _switchCadenceMs = 0;
                    _resultType = RockPaperScissorsResultType.None;
                    _mainButtonType = RockPaperScissorsMainButtonType.Start;
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
                    break;

                case 9:
                case 12:
                    BeginRound(currentTimeMs);
                    CurrentStatusMessage = $"RPS subtype {packetType} started a live round, cleared the NPC choice, armed the 30000 ms limit, and enabled the three RPS buttons.";
                    LastPacketSummary = $"round-start ({packetType}) -> switching=120 limit=30000";
                    break;

                case 10:
                    _choiceButtonsEnabled = false;
                    _roundDeadlineTick = 0;
                    _switchCadenceMs = 0;
                    _npcChoice = RockPaperScissorsChoice.None;
                    StraightVictoryCount = -1;
                    ShowResult(currentTimeMs);
                    CurrentStatusMessage = "RPS subtype 10 forced immediate result presentation by clearing switching and routing into ShowResult.";
                    LastPacketSummary = "force-result (10) -> streak=-1 + ShowResult";
                    break;

                case 11:
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
            _resultExpireTick = currentTimeMs + ResultExpireDelayMs;
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
            _choiceButtonsEnabled = true;
            _mainButtonEnabled = false;
            _exitButtonEnabled = false;
            _switchCadenceMs = ChoiceSwitchCadenceMs;
            _lastSwitchTick = currentTimeMs;
            _roundDeadlineTick = currentTimeMs + RoundLimitMs;
            _resultExpireTick = 0;
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
    }
}
