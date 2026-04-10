using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator
{
    /// <summary>
    /// Chat message structure
    /// </summary>
    public struct ChatMessage
    {
        public string Text;
        public Color Color;
        public int Timestamp;
        public int ChatLogType;
        public int ChannelId;
        public string WhisperTargetCandidate;

        public ChatMessage(
            string text,
            Color color,
            int timestamp,
            int chatLogType = -1,
            string whisperTargetCandidate = null,
            int channelId = -1)
        {
            Text = text;
            Color = color;
            Timestamp = timestamp;
            ChatLogType = chatLogType;
            ChannelId = channelId;
            WhisperTargetCandidate = whisperTargetCandidate ?? string.Empty;
        }
    }

    public enum MapSimulatorChatTargetType
    {
        All = 0,
        Friend = 1,
        Party = 2,
        Guild = 3,
        Association = 4,
        Expedition = 5
    }

    public sealed class MapSimulatorChatRenderState
    {
        public IReadOnlyList<ChatMessage> Messages { get; init; } = Array.Empty<ChatMessage>();
        public IReadOnlyList<string> WhisperCandidates { get; init; } = Array.Empty<string>();
        public bool IsActive { get; init; }
        public bool IsWhisperTargetPickerActive { get; init; }
        public MapSimulatorChat.WhisperTargetPickerPresentation WhisperTargetPickerPresentation { get; init; } =
            MapSimulatorChat.WhisperTargetPickerPresentation.Inline;
        public string InputText { get; init; } = string.Empty;
        public int CursorPosition { get; init; }
        public MapSimulatorChatTargetType TargetType { get; init; }
        public string WhisperTarget { get; init; } = string.Empty;
        public int WhisperTargetPickerSelectionIndex { get; init; } = -1;
        public MapSimulatorChat.WhisperTargetPickerModalButtonFocus WhisperTargetPickerModalButtonFocus { get; init; } =
            MapSimulatorChat.WhisperTargetPickerModalButtonFocus.Confirm;
        public MapSimulatorChat.WhisperTargetPickerModalFocusTarget WhisperTargetPickerModalFocusTarget { get; init; } =
            MapSimulatorChat.WhisperTargetPickerModalFocusTarget.ComboBox;
        public string LocalPlayerName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Handles chat input, message history, and rendering for the MapSimulator
    /// </summary>
    public class MapSimulatorChat
    {
        #region Constants
        private static readonly Color WhisperMessageColor = new Color(255, 170, 255);
        private static readonly Color SystemMessageColor = new Color(255, 228, 151);
        private static readonly Color ErrorMessageColor = new Color(247, 75, 75);
        private static readonly Color DefaultMessageColor = Color.White;
        private static readonly Color NoticeMessageColor = new Color(151, 221, 255);
        private static readonly Color PartyMessageColor = new Color(124, 255, 172);
        private static readonly Color FriendMessageColor = new Color(255, 255, 120);
        private static readonly Color GuildMessageColor = new Color(176, 255, 120);
        private static readonly Color AllianceMessageColor = new Color(124, 236, 255);
        private static readonly Color ExpeditionMessageColor = new Color(255, 216, 128);
        private static readonly Color ClientType11Color = new Color(255, 255, 255, 176);
        private static readonly Color ClientType18Color = new Color(77, 26, 173, 44);
        private static readonly Color ClientType20Color = new Color(255, 92, 89, 128);
        private static readonly Color ClientType22Color = new Color(153, 204, 51);

        private const int CHAT_INPUT_X = 5;
        private const int CHAT_INPUT_Y_OFFSET = 55; // Offset from bottom of screen (just above status bar level indicator)
        private const int CHAT_INPUT_WIDTH = 478;
        private const int CHAT_INPUT_HEIGHT = 15;
        private const int CHAT_MESSAGE_DISPLAY_TIME = 10000; // Messages fade after 10 seconds
        private const int CHAT_MAX_INPUT_LENGTH = 100;
        // Client ChatLogAdd trims the chat log once it exceeds 0x40 entries.
        internal const int ClientChatLogEntryLimit = 64;
        internal const int WhisperTargetPickerVisibleRowCount = 6;
        private const int MAX_CHAT_MESSAGES = ClientChatLogEntryLimit;
        private const int CHAT_DISPLAY_LINES = 15;
        private const int CHAT_CURSOR_BLINK_RATE = 500; // Blink every 500ms

        // Key repeat settings
        private const int KEY_REPEAT_INITIAL_DELAY = 400; // ms before repeat starts
        private const int KEY_REPEAT_RATE = 35; // ms between repeats

        // Input history settings
        private const int MAX_INPUT_HISTORY = 50;
        private const int MAX_WHISPER_CANDIDATES = MAX_INPUT_HISTORY;
        #endregion

        #region Fields
        private bool _isActive = false;
        private readonly StringBuilder _inputText = new StringBuilder(128);
        private readonly List<ChatMessage> _messages = new List<ChatMessage>();
        internal event Action<string, int, int> ClientChatMessageAdded;
        private int _cursorBlinkTimer = 0;
        private int _lastTickCount = 0;
        private int _cursorPosition = 0; // Position within input text

        private SpriteFont _font;
        private Texture2D _backgroundTexture;
        private int _screenHeight;

        private readonly ChatCommandHandler _commandHandler = new ChatCommandHandler();
        private MapSimulatorChatTargetType _chatTarget = MapSimulatorChatTargetType.All;
        private string _whisperTarget = string.Empty;
        private string _replyTarget = string.Empty;
        private string _localPlayerName = string.Empty;
        private readonly List<string> _whisperCandidates = new List<string>();
        private bool _isWhisperTargetPickerActive;
        private WhisperTargetPickerPresentation _whisperTargetPickerPresentation = WhisperTargetPickerPresentation.Inline;
        private int _whisperTargetPickerSelectionIndex = -1;
        private WhisperTargetPickerModalButtonFocus _whisperTargetPickerModalButtonFocus =
            WhisperTargetPickerModalButtonFocus.Confirm;
        private WhisperTargetPickerModalFocusTarget _whisperTargetPickerModalFocusTarget =
            WhisperTargetPickerModalFocusTarget.ComboBox;
        private string _savedChatInputBeforeWhisperPicker = string.Empty;
        private int _savedChatCursorBeforeWhisperPicker;

        // Key repeat tracking
        private Keys _lastHeldKey = Keys.None;
        private int _keyHoldStartTime = 0;
        private int _lastKeyRepeatTime = 0;

        // Input history (stores what user typed, for Up/Down navigation)
        private readonly List<string> _inputHistory = new List<string>();
        private int _historyIndex = -1; // -1 means not browsing history
        private string _savedCurrentInput = ""; // Saves current input when browsing history
        #endregion

        #region Properties
        /// <summary>
        /// Whether chat input mode is currently active
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Command handler for registering and executing chat commands
        /// </summary>
        public ChatCommandHandler CommandHandler => _commandHandler;

        public Action<string, int> MessageSubmitted { get; set; }
        #endregion

        internal IReadOnlyList<string> WhisperCandidates => _whisperCandidates;
        internal bool IsWhisperTargetPickerActive => _isWhisperTargetPickerActive;
        internal int WhisperTargetPickerSelectionIndex => _whisperTargetPickerSelectionIndex;
        internal WhisperTargetPickerPresentation CurrentWhisperTargetPickerPresentation => _whisperTargetPickerPresentation;

        private enum ChatSubmitDisposition
        {
            NotHandled = 0,
            CloseChat = 1,
            KeepChatOpen = 2
        }

        public enum WhisperTargetPickerPresentation
        {
            Inline = 0,
            Modal = 1
        }

        public enum WhisperTargetPickerModalButtonFocus
        {
            Confirm = 0,
            Close = 1
        }

        public enum WhisperTargetPickerModalFocusTarget
        {
            ComboBox = 0,
            FooterButtons = 1
        }

        private enum WhisperTargetPickerNavigationMode
        {
            Step = 0,
            Page = 1,
            Absolute = 2
        }

        private enum ClientChatLogType
        {
            All = 0,
            Party = 2,
            Friend = 3,
            Guild = 4,
            Alliance = 5,
            Type11 = 11,
            System = 12,
            Notice = 13,
            OutgoingWhisper = 14,
            Error = 15,
            IncomingWhisper = 16,
            Type18 = 18,
            Type19 = 19,
            Type20 = 20,
            Type21 = 21,
            Type22 = 22,
            Type23 = 23,
            Expedition = 26
        }

        internal enum WhisperTargetValidationResult
        {
            Valid = 0,
            Empty = 1,
            Invalid = 2,
            Self = 3
        }

        internal static Color ResolveRenderedClientChatLogColor(int chatLogType, int channelId = -1)
        {
            return Enum.IsDefined(typeof(ClientChatLogType), chatLogType)
                ? ResolveClientChatLogColor((ClientChatLogType)chatLogType, channelId)
                : DefaultMessageColor;
        }

        #region Initialization
        /// <summary>
        /// Initialize the chat system with required resources
        /// </summary>
        /// <param name="font">Font to use for chat text</param>
        /// <param name="backgroundTexture">1x1 white texture for drawing backgrounds</param>
        /// <param name="screenHeight">Screen height for positioning</param>
        public void Initialize(SpriteFont font, Texture2D backgroundTexture, int screenHeight)
        {
            _font = font;
            _backgroundTexture = backgroundTexture;
            _screenHeight = screenHeight;
        }

        /// <summary>
        /// Update screen height (call when window is resized)
        /// </summary>
        public void UpdateScreenHeight(int screenHeight)
        {
            _screenHeight = screenHeight;
        }
        #endregion

        #region Input Handling
        /// <summary>
        /// Handles chat input processing
        /// </summary>
        /// <param name="newKeyboardState">Current keyboard state</param>
        /// <param name="oldKeyboardState">Previous keyboard state</param>
        /// <param name="tickCount">Current tick count for cursor blinking</param>
        /// <returns>True if chat consumed the input (skip other key handlers)</returns>
        public bool HandleInput(KeyboardState newKeyboardState, KeyboardState oldKeyboardState, int tickCount)
        {
            _lastTickCount = tickCount;

            if (!_isActive
                && newKeyboardState.IsKeyDown(Keys.OemQuestion)
                && oldKeyboardState.IsKeyUp(Keys.OemQuestion)
                && !(newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift)))
            {
                Activate(tickCount, "/");
                return true;
            }

            // Toggle chat with Enter key
            if (newKeyboardState.IsKeyDown(Keys.Enter) && oldKeyboardState.IsKeyUp(Keys.Enter))
            {
                if (_isActive)
                {
                    if (_isWhisperTargetPickerActive)
                    {
                        if (_whisperTargetPickerPresentation == WhisperTargetPickerPresentation.Modal
                            && _whisperTargetPickerModalFocusTarget == WhisperTargetPickerModalFocusTarget.FooterButtons
                            && _whisperTargetPickerModalButtonFocus == WhisperTargetPickerModalButtonFocus.Close)
                        {
                            CancelWhisperTargetPicker();
                            return true;
                        }

                        ConfirmWhisperTargetPicker(tickCount);
                        return true;
                    }

                    // Send message if there's text
                    if (_inputText.Length > 0)
                    {
                        string message = _inputText.ToString();

                        // Add to input history
                        AddToInputHistory(message);

                        _inputText.Clear();
                        _cursorPosition = 0;
                        ResetHistoryNavigation();

                        ChatSubmitDisposition slashDisposition = TryHandleSlashCommand(message, tickCount);
                        if (slashDisposition == ChatSubmitDisposition.KeepChatOpen)
                        {
                            _isActive = true;
                            _cursorBlinkTimer = tickCount;
                            return true;
                        }

                        if (slashDisposition == ChatSubmitDisposition.CloseChat)
                        {
                            _isActive = false;
                            return true;
                        }

                        // Check if it's a command
                        if (_commandHandler.IsCommand(message))
                        {
                            var result = _commandHandler.ExecuteCommand(message);
                            if (!string.IsNullOrEmpty(result.Message))
                            {
                                // Split multi-line messages
                                string[] lines = result.Message.Split('\n');
                                foreach (string line in lines)
                                {
                                    AddMessage(line, result.MessageColor, tickCount);
                                }
                            }
                        }
                        else
                        {
                            SendTargetedChatMessage(message, tickCount);
                        }
                    }
                    _isActive = false;
                }
                else
                {
                    Activate(tickCount);
                }
                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.Tab) && oldKeyboardState.IsKeyUp(Keys.Tab))
            {
                if (_isWhisperTargetPickerActive)
                {
                    if (_whisperTargetPickerPresentation == WhisperTargetPickerPresentation.Modal)
                    {
                        ToggleWhisperTargetPickerModalFocusTarget();
                        return true;
                    }

                    MoveWhisperTargetPickerSelection(
                        (newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift)) ? -1 : 1,
                        WhisperTargetPickerNavigationMode.Step);
                    return true;
                }

                CycleTarget(newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift) ? -1 : 1);
                return true;
            }

            // If chat is not active, don't consume input
            if (!_isActive)
                return false;

            // Handle Escape to cancel chat
            if (newKeyboardState.IsKeyDown(Keys.Escape))
            {
                if (_isWhisperTargetPickerActive)
                {
                    CancelWhisperTargetPicker();
                    return true;
                }

                Deactivate();
                return true;
            }

            // Handle Up arrow - browse history (older)
            if (newKeyboardState.IsKeyDown(Keys.Up) && oldKeyboardState.IsKeyUp(Keys.Up))
            {
                if (_isWhisperTargetPickerActive)
                {
                    ActivateWhisperTargetPickerModalComboFocus();
                    MoveWhisperTargetPickerSelection(-1, WhisperTargetPickerNavigationMode.Step);
                    return true;
                }

                if (_inputHistory.Count > 0)
                {
                    // Save current input when starting to browse history
                    if (_historyIndex == -1)
                    {
                        _savedCurrentInput = _inputText.ToString();
                    }

                    // Move to older entry
                    if (_historyIndex < _inputHistory.Count - 1)
                    {
                        _historyIndex++;
                        _inputText.Clear();
                        _inputText.Append(_inputHistory[_inputHistory.Count - 1 - _historyIndex]);
                        _cursorPosition = _inputText.Length; // Move cursor to end
                    }
                }
                return true;
            }

            // Handle Down arrow - browse history (newer)
            if (newKeyboardState.IsKeyDown(Keys.Down) && oldKeyboardState.IsKeyUp(Keys.Down))
            {
                if (_isWhisperTargetPickerActive)
                {
                    ActivateWhisperTargetPickerModalComboFocus();
                    MoveWhisperTargetPickerSelection(1, WhisperTargetPickerNavigationMode.Step);
                    return true;
                }

                if (_historyIndex > -1)
                {
                    _historyIndex--;
                    _inputText.Clear();

                    if (_historyIndex == -1)
                    {
                        // Restore the saved current input
                        _inputText.Append(_savedCurrentInput);
                    }
                    else
                    {
                        _inputText.Append(_inputHistory[_inputHistory.Count - 1 - _historyIndex]);
                    }
                    _cursorPosition = _inputText.Length; // Move cursor to end
                }
                return true;
            }

            // Handle Left arrow - move cursor left (with key repeat)
            if (newKeyboardState.IsKeyDown(Keys.Left))
            {
                if (_isWhisperTargetPickerActive
                    && _whisperTargetPickerPresentation == WhisperTargetPickerPresentation.Modal
                    && _whisperTargetPickerModalFocusTarget == WhisperTargetPickerModalFocusTarget.FooterButtons
                    && oldKeyboardState.IsKeyUp(Keys.Left))
                {
                    MoveWhisperTargetPickerModalButtonFocus(-1);
                    return true;
                }

                if (oldKeyboardState.IsKeyUp(Keys.Left))
                {
                    if (_cursorPosition > 0)
                        _cursorPosition--;
                    _lastHeldKey = Keys.Left;
                    _keyHoldStartTime = tickCount;
                    _lastKeyRepeatTime = tickCount;
                }
                else if (ShouldRepeatKey(Keys.Left, tickCount))
                {
                    if (_cursorPosition > 0)
                        _cursorPosition--;
                    _lastKeyRepeatTime = tickCount;
                }
                return true;
            }
            else if (_lastHeldKey == Keys.Left)
            {
                ResetKeyRepeat();
            }

            // Handle Right arrow - move cursor right (with key repeat)
            if (newKeyboardState.IsKeyDown(Keys.Right))
            {
                if (_isWhisperTargetPickerActive
                    && _whisperTargetPickerPresentation == WhisperTargetPickerPresentation.Modal
                    && _whisperTargetPickerModalFocusTarget == WhisperTargetPickerModalFocusTarget.FooterButtons
                    && oldKeyboardState.IsKeyUp(Keys.Right))
                {
                    MoveWhisperTargetPickerModalButtonFocus(1);
                    return true;
                }

                if (oldKeyboardState.IsKeyUp(Keys.Right))
                {
                    if (_cursorPosition < _inputText.Length)
                        _cursorPosition++;
                    _lastHeldKey = Keys.Right;
                    _keyHoldStartTime = tickCount;
                    _lastKeyRepeatTime = tickCount;
                }
                else if (ShouldRepeatKey(Keys.Right, tickCount))
                {
                    if (_cursorPosition < _inputText.Length)
                        _cursorPosition++;
                    _lastKeyRepeatTime = tickCount;
                }
                return true;
            }
            else if (_lastHeldKey == Keys.Right)
            {
                ResetKeyRepeat();
            }

            // Handle backspace with key repeat - delete at cursor position
            if (newKeyboardState.IsKeyDown(Keys.Back))
            {
                ActivateWhisperTargetPickerModalComboFocus();
                if (oldKeyboardState.IsKeyUp(Keys.Back))
                {
                    // First press - delete character before cursor
                    if (_cursorPosition > 0)
                    {
                        _inputText.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                        SyncWhisperTargetPickerSelectionFromInput();
                    }
                    _lastHeldKey = Keys.Back;
                    _keyHoldStartTime = tickCount;
                    _lastKeyRepeatTime = tickCount;
                }
                else if (ShouldRepeatKey(Keys.Back, tickCount))
                {
                    // Key repeat
                    if (_cursorPosition > 0)
                    {
                        _inputText.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                        SyncWhisperTargetPickerSelectionFromInput();
                    }
                    _lastKeyRepeatTime = tickCount;
                }
                return true;
            }
            else if (_lastHeldKey == Keys.Back)
            {
                ResetKeyRepeat();
            }

            // Handle Delete key - delete at cursor position (with key repeat)
            if (newKeyboardState.IsKeyDown(Keys.Delete))
            {
                ActivateWhisperTargetPickerModalComboFocus();
                if (oldKeyboardState.IsKeyUp(Keys.Delete))
                {
                    // First press - delete character at cursor
                    if (_cursorPosition < _inputText.Length)
                    {
                        _inputText.Remove(_cursorPosition, 1);
                        SyncWhisperTargetPickerSelectionFromInput();
                    }
                    _lastHeldKey = Keys.Delete;
                    _keyHoldStartTime = tickCount;
                    _lastKeyRepeatTime = tickCount;
                }
                else if (ShouldRepeatKey(Keys.Delete, tickCount))
                {
                    // Key repeat
                    if (_cursorPosition < _inputText.Length)
                    {
                        _inputText.Remove(_cursorPosition, 1);
                        SyncWhisperTargetPickerSelectionFromInput();
                    }
                    _lastKeyRepeatTime = tickCount;
                }
                return true;
            }
            else if (_lastHeldKey == Keys.Delete)
            {
                ResetKeyRepeat();
            }

            // Handle Home key - move cursor to start
            if (newKeyboardState.IsKeyDown(Keys.Home) && oldKeyboardState.IsKeyUp(Keys.Home))
            {
                if (_isWhisperTargetPickerActive)
                {
                    ActivateWhisperTargetPickerModalComboFocus();
                    MoveWhisperTargetPickerSelectionToBoundary(moveToLast: false);
                    return true;
                }

                _cursorPosition = 0;
                return true;
            }

            // Handle End key - move cursor to end
            if (newKeyboardState.IsKeyDown(Keys.End) && oldKeyboardState.IsKeyUp(Keys.End))
            {
                if (_isWhisperTargetPickerActive)
                {
                    ActivateWhisperTargetPickerModalComboFocus();
                    MoveWhisperTargetPickerSelectionToBoundary(moveToLast: true);
                    return true;
                }

                _cursorPosition = _inputText.Length;
                return true;
            }

            if (newKeyboardState.IsKeyDown(Keys.PageUp) && oldKeyboardState.IsKeyUp(Keys.PageUp))
            {
                if (_isWhisperTargetPickerActive)
                {
                    ActivateWhisperTargetPickerModalComboFocus();
                    PageWhisperTargetPickerSelection(-1);
                    return true;
                }
            }

            if (newKeyboardState.IsKeyDown(Keys.PageDown) && oldKeyboardState.IsKeyUp(Keys.PageDown))
            {
                if (_isWhisperTargetPickerActive)
                {
                    ActivateWhisperTargetPickerModalComboFocus();
                    PageWhisperTargetPickerSelection(1);
                    return true;
                }
            }

            // Handle character input
            bool shift = newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift);
            Keys[] pressedKeys = newKeyboardState.GetPressedKeys();

            // First, process ALL newly pressed keys immediately (for fast typing)
            foreach (Keys key in pressedKeys)
            {
                if (key == Keys.LeftShift || key == Keys.RightShift ||
                    key == Keys.LeftControl || key == Keys.RightControl ||
                    key == Keys.LeftAlt || key == Keys.RightAlt ||
                    key == Keys.Back || key == Keys.Enter || key == Keys.Escape || key == Keys.Tab ||
                    key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right ||
                    key == Keys.Home || key == Keys.End || key == Keys.Delete)
                    continue;

                // Process only newly pressed keys
                if (oldKeyboardState.IsKeyUp(key))
                {
                    char? c = KeyToChar(key, shift);
                    if (c.HasValue && _inputText.Length < CHAT_MAX_INPUT_LENGTH)
                    {
                        ActivateWhisperTargetPickerModalComboFocus();
                        _inputText.Insert(_cursorPosition, c.Value);
                        _cursorPosition++;
                        SyncWhisperTargetPickerSelectionFromInput();
                        // Track this key for potential repeat
                        _lastHeldKey = key;
                        _keyHoldStartTime = tickCount;
                        _lastKeyRepeatTime = tickCount;
                    }
                }
            }

            // Then handle key repeat for held keys
            if (_lastHeldKey != Keys.None && _lastHeldKey != Keys.Back &&
                newKeyboardState.IsKeyDown(_lastHeldKey) && ShouldRepeatKey(_lastHeldKey, tickCount))
            {
                char? c = KeyToChar(_lastHeldKey, shift);
                if (c.HasValue && _inputText.Length < CHAT_MAX_INPUT_LENGTH)
                {
                    _inputText.Insert(_cursorPosition, c.Value);
                    _cursorPosition++;
                    SyncWhisperTargetPickerSelectionFromInput();
                    _lastKeyRepeatTime = tickCount;
                }
            }
            else if (_lastHeldKey != Keys.None && _lastHeldKey != Keys.Back && !newKeyboardState.IsKeyDown(_lastHeldKey))
            {
                ResetKeyRepeat();
            }

            return true; // Consume input when chat is active
        }

        /// <summary>
        /// Check if a held key should repeat based on timing
        /// </summary>
        private bool ShouldRepeatKey(Keys key, int tickCount)
        {
            if (_lastHeldKey != key)
                return false;

            int holdDuration = tickCount - _keyHoldStartTime;
            if (holdDuration < KEY_REPEAT_INITIAL_DELAY)
                return false;

            int timeSinceLastRepeat = tickCount - _lastKeyRepeatTime;
            return timeSinceLastRepeat >= KEY_REPEAT_RATE;
        }

        /// <summary>
        /// Reset key repeat tracking
        /// </summary>
        private void ResetKeyRepeat()
        {
            _lastHeldKey = Keys.None;
            _keyHoldStartTime = 0;
            _lastKeyRepeatTime = 0;
        }

        /// <summary>
        /// Add a message to input history
        /// </summary>
        private void AddToInputHistory(string message)
        {
            // Don't add duplicates of the last entry
            if (_inputHistory.Count > 0 && _inputHistory[_inputHistory.Count - 1] == message)
                return;

            _inputHistory.Add(message);

            // Trim history if too large
            while (_inputHistory.Count > MAX_INPUT_HISTORY)
            {
                _inputHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Reset history navigation state
        /// </summary>
        private void ResetHistoryNavigation()
        {
            _historyIndex = -1;
            _savedCurrentInput = "";
        }

        /// <summary>
        /// Converts a keyboard key to a character
        /// </summary>
        private static char? KeyToChar(Keys key, bool shift)
        {
            // Letters
            if (key >= Keys.A && key <= Keys.Z)
            {
                char c = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpper(c) : c;
            }

            // Numbers
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                if (shift)
                {
                    char[] shiftNums = { ')', '!', '@', '#', '$', '%', '^', '&', '*', '(' };
                    return shiftNums[key - Keys.D0];
                }
                return (char)('0' + (key - Keys.D0));
            }

            // Numpad
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
                return (char)('0' + (key - Keys.NumPad0));

            // Special characters
            switch (key)
            {
                case Keys.Space: return ' ';
                case Keys.OemPeriod: return shift ? '>' : '.';
                case Keys.OemComma: return shift ? '<' : ',';
                case Keys.OemMinus: return shift ? '_' : '-';
                case Keys.OemPlus: return shift ? '+' : '=';
                case Keys.OemQuestion: return shift ? '?' : '/';
                case Keys.OemSemicolon: return shift ? ':' : ';';
                case Keys.OemQuotes: return shift ? '"' : '\'';
                case Keys.OemOpenBrackets: return shift ? '{' : '[';
                case Keys.OemCloseBrackets: return shift ? '}' : ']';
                case Keys.OemPipe: return shift ? '|' : '\\';
                case Keys.OemTilde: return shift ? '~' : '`';
            }

            return null;
        }
        #endregion

        #region Message Management
        /// <summary>
        /// Adds a message to the chat history
        /// </summary>
        /// <param name="text">Message text</param>
        /// <param name="color">Message color</param>
        /// <param name="tickCount">Current tick count for timestamp</param>
        public void AddMessage(string text, Color color, int tickCount, int chatLogType = -1)
        {
            AddMessage(text, color, tickCount, chatLogType, null);
        }

        public void AddClientChatMessage(string text, int tickCount, int chatLogType)
        {
            AddClientChatMessage(text, tickCount, chatLogType, null, -1);
        }

        public void AddClientChatMessage(string text, int tickCount, int chatLogType, string whisperTargetCandidate)
        {
            AddClientChatMessage(text, tickCount, chatLogType, whisperTargetCandidate, -1);
        }

        public void AddSystemMessage(string text, int tickCount)
        {
            AddClientMessage(text, tickCount, ClientChatLogType.System);
        }

        public void AddNoticeMessage(string text, int tickCount)
        {
            AddClientMessage(text, tickCount, ClientChatLogType.Notice);
        }

        public void AddErrorMessage(string text, int tickCount)
        {
            AddClientMessage(text, tickCount, ClientChatLogType.Error);
        }

        public void AddClientChatMessage(
            string text,
            int tickCount,
            int chatLogType,
            string whisperTargetCandidate,
            int channelId)
        {
            AddMessage(
                text,
                ResolveRenderedClientChatLogColor(chatLogType, channelId),
                tickCount,
                chatLogType,
                whisperTargetCandidate,
                channelId);
            ClientChatMessageAdded?.Invoke(text, chatLogType, tickCount);
        }

        public void AddMessage(string text, Color color, int tickCount, int chatLogType, string whisperTargetCandidate)
        {
            AddMessage(text, color, tickCount, chatLogType, whisperTargetCandidate, -1);
        }

        public void AddMessage(
            string text,
            Color color,
            int tickCount,
            int chatLogType,
            string whisperTargetCandidate,
            int channelId)
        {
            if (chatLogType < 0)
            {
                if (TryInferClientChatLogTypeFromPrefix(text, out ClientChatLogType prefixedType))
                {
                    chatLogType = (int)prefixedType;
                    color = ResolveClientChatLogColor(prefixedType, channelId);
                }
                else
                {
                    chatLogType = InferClientChatLogType(text, color);
                    if (Enum.IsDefined(typeof(ClientChatLogType), chatLogType))
                    {
                        color = ResolveClientChatLogColor((ClientChatLogType)chatLogType, channelId);
                    }
                }
            }

            RememberIncomingWhisperTarget(chatLogType, whisperTargetCandidate, text);

            _messages.Add(new ChatMessage(text, color, tickCount, chatLogType, whisperTargetCandidate, channelId));

            // Remove old messages if exceeding limit
            while (_messages.Count > MAX_CHAT_MESSAGES)
            {
                _messages.RemoveAt(0);
            }
        }

        /// <summary>
        /// Clears current input and deactivates chat (preserves message history)
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            CloseWhisperTargetPicker(restoreDraft: false);
            _inputText.Clear();
            _cursorPosition = 0;
            ResetKeyRepeat();
            ResetHistoryNavigation();
        }

        public void Activate(int tickCount, string initialText = null)
        {
            _isActive = true;
            _cursorBlinkTimer = tickCount;
            ResetHistoryNavigation();
            SetInputText(initialText ?? string.Empty);
        }

        public void ToggleActive(int tickCount)
        {
            if (_isActive)
            {
                Deactivate();
                return;
            }

            Activate(tickCount);
        }

        public void CycleTarget(int direction)
        {
            const int targetCount = 6;
            int nextIndex = ((int)_chatTarget + direction) % targetCount;
            if (nextIndex < 0)
            {
                nextIndex += targetCount;
            }

            _chatTarget = (MapSimulatorChatTargetType)nextIndex;
            _whisperTarget = string.Empty;
        }

        public void BeginWhisperTo(string whisperTarget, int tickCount)
        {
            if (!TryArmWhisperTarget(whisperTarget, tickCount))
            {
                return;
            }

            Activate(tickCount);
        }

        public void BeginWhisperFromChatLog(string whisperTarget, int tickCount)
        {
            WhisperTargetValidationResult validationResult = ValidateWhisperTargetCandidate(
                whisperTarget,
                _localPlayerName,
                out string normalizedTarget);
            if (validationResult != WhisperTargetValidationResult.Valid)
            {
                return;
            }

            AddWhisperCandidate(normalizedTarget);
            OpenWhisperTargetPicker(tickCount, normalizedTarget);
        }

        public void RememberWhisperTarget(string whisperTarget)
        {
            WhisperTargetValidationResult validationResult = ValidateWhisperTargetCandidate(
                whisperTarget,
                _localPlayerName,
                out string normalizedTarget);
            if (validationResult != WhisperTargetValidationResult.Valid)
            {
                return;
            }

            AddWhisperCandidate(normalizedTarget);
            _replyTarget = normalizedTarget;
        }

        public void AddIncomingTargetedMessage(
            MapSimulatorChatTargetType targetType,
            string speaker,
            string message,
            int tickCount)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string prefix = GetTargetPrefix(targetType);
            string trimmedSpeaker = speaker?.Trim();
            string trimmedMessage = message.Trim();
            string text = string.IsNullOrWhiteSpace(trimmedSpeaker)
                ? $"{prefix} {trimmedMessage}".Trim()
                : $"{prefix} {trimmedSpeaker}: {trimmedMessage}".Trim();
            ClientChatLogType chatLogType = GetTargetChatLogType(targetType);
            AddMessage(text, ResolveClientChatLogColor(chatLogType), tickCount, (int)chatLogType);
        }

        public MapSimulatorChatRenderState GetRenderState(string localPlayerName = null)
        {
            _localPlayerName = NormalizeChatSpeakerCandidate(localPlayerName);
            return new MapSimulatorChatRenderState
            {
                Messages = _messages,
                WhisperCandidates = _whisperCandidates,
                IsActive = _isActive,
                IsWhisperTargetPickerActive = _isWhisperTargetPickerActive,
                WhisperTargetPickerPresentation = _whisperTargetPickerPresentation,
                InputText = _inputText.ToString(),
                CursorPosition = _cursorPosition,
                TargetType = _chatTarget,
                WhisperTarget = _whisperTarget ?? string.Empty,
                WhisperTargetPickerSelectionIndex = _whisperTargetPickerSelectionIndex,
                WhisperTargetPickerModalButtonFocus = _whisperTargetPickerModalButtonFocus,
                WhisperTargetPickerModalFocusTarget = _whisperTargetPickerModalFocusTarget,
                LocalPlayerName = _localPlayerName
            };
        }

        /// <summary>
        /// Clears all chat messages
        /// </summary>
        public void ClearMessages()
        {
            _messages.Clear();
        }

        public void OpenWhisperTargetPicker(
            int tickCount,
            string initialTarget = null,
            WhisperTargetPickerPresentation presentation = WhisperTargetPickerPresentation.Inline)
        {
            string normalizedInitialTarget = NormalizeChatSpeakerCandidate(initialTarget);
            bool explicitInitialTarget = !string.IsNullOrWhiteSpace(normalizedInitialTarget);
            if (string.IsNullOrWhiteSpace(normalizedInitialTarget))
            {
                normalizedInitialTarget = NormalizeChatSpeakerCandidate(_whisperTarget);
                explicitInitialTarget = !string.IsNullOrWhiteSpace(normalizedInitialTarget);
            }

            if (string.IsNullOrWhiteSpace(normalizedInitialTarget))
            {
                normalizedInitialTarget = NormalizeChatSpeakerCandidate(_replyTarget);
                explicitInitialTarget = !string.IsNullOrWhiteSpace(normalizedInitialTarget);
            }

            if (!_isWhisperTargetPickerActive)
            {
                _savedChatInputBeforeWhisperPicker = _inputText.ToString();
                _savedChatCursorBeforeWhisperPicker = _cursorPosition;
            }

            Activate(tickCount);
            _isWhisperTargetPickerActive = true;
            _whisperTargetPickerPresentation = presentation;
            _whisperTargetPickerModalButtonFocus = WhisperTargetPickerModalButtonFocus.Confirm;
            _whisperTargetPickerModalFocusTarget = WhisperTargetPickerModalFocusTarget.ComboBox;
            ResetHistoryNavigation();

            SetInputText(explicitInitialTarget ? normalizedInitialTarget : string.Empty);
            SyncWhisperTargetPickerSelectionFromInput();
        }

        internal bool ConfirmWhisperTargetPicker(int tickCount)
        {
            if (!_isWhisperTargetPickerActive)
            {
                return false;
            }

            string whisperTarget = ResolveWhisperTargetPickerSelection();
            if (!TryArmWhisperTarget(whisperTarget, tickCount))
            {
                return false;
            }

            CloseWhisperTargetPicker(restoreDraft: false);
            _isActive = true;
            _cursorBlinkTimer = tickCount;
            return true;
        }

        internal void CancelActiveWhisperTargetPicker()
        {
            if (_isWhisperTargetPickerActive)
            {
                CancelWhisperTargetPicker();
            }
        }

        public void SelectWhisperTargetPickerCandidate(string whisperTarget, int tickCount)
        {
            OpenWhisperTargetPicker(tickCount, whisperTarget, _whisperTargetPickerPresentation);
            ConfirmWhisperTargetPicker(tickCount);
        }

        internal void OffsetWhisperTargetPickerSelection(int delta)
        {
            if (!_isWhisperTargetPickerActive || delta == 0)
            {
                return;
            }

            MoveWhisperTargetPickerSelection(delta, WhisperTargetPickerNavigationMode.Step);
        }

        internal void PageWhisperTargetPickerSelection(int deltaPages)
        {
            if (!_isWhisperTargetPickerActive || deltaPages == 0)
            {
                return;
            }

            MoveWhisperTargetPickerSelection(deltaPages, WhisperTargetPickerNavigationMode.Page);
        }

        internal void MoveWhisperTargetPickerSelectionToBoundary(bool moveToLast)
        {
            if (!_isWhisperTargetPickerActive || _whisperCandidates.Count == 0)
            {
                return;
            }

            MoveWhisperTargetPickerSelection(
                moveToLast ? _whisperCandidates.Count - 1 : 0,
                WhisperTargetPickerNavigationMode.Absolute);
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Draws the chat UI including messages and input box
        /// </summary>
        /// <param name="spriteBatch">SpriteBatch for drawing</param>
        /// <param name="tickCount">Current tick count for animations</param>
        public void Draw(SpriteBatch spriteBatch, int tickCount)
        {
            if (_font == null)
                return;

            int chatBoxY = _screenHeight - CHAT_INPUT_Y_OFFSET;

            // Draw chat messages (above input box)
            int messageY = chatBoxY - 16;
            int displayedMessages = 0;

            for (int i = _messages.Count - 1; i >= 0 && displayedMessages < CHAT_DISPLAY_LINES; i--)
            {
                ChatMessage msg = _messages[i];

                // Calculate fade based on time (fade out after CHAT_MESSAGE_DISPLAY_TIME)
                int age = tickCount - msg.Timestamp;
                float alpha = 1.0f;

                if (age > CHAT_MESSAGE_DISPLAY_TIME - 2000) // Start fading 2 seconds before disappear
                {
                    alpha = Math.Max(0, 1.0f - (age - (CHAT_MESSAGE_DISPLAY_TIME - 2000)) / 2000f);
                }

                if (alpha <= 0 && !_isActive)
                    continue;

                // When chat is active, show all messages without fading
                if (_isActive)
                    alpha = 1.0f;

                Color msgColor = msg.Color * alpha;

                // Draw message background for readability
                Vector2 textSize = _font.MeasureString(msg.Text);
                if (_backgroundTexture != null)
                {
                    spriteBatch.Draw(_backgroundTexture,
                        new Rectangle(CHAT_INPUT_X - 2, messageY - 1, (int)textSize.X + 4, (int)textSize.Y + 2),
                        new Color(0, 0, 0, (int)(150 * alpha)));
                }

                spriteBatch.DrawString(_font, msg.Text, new Vector2(CHAT_INPUT_X, messageY), msgColor);
                messageY -= 14; // Reduced line spacing for smaller font
                displayedMessages++;
            }

            // Draw chat input box when active
            if (_isActive)
            {
                // Draw input background
                if (_backgroundTexture != null)
                {
                    spriteBatch.Draw(_backgroundTexture,
                        new Rectangle(CHAT_INPUT_X - 2, chatBoxY - 2, CHAT_INPUT_WIDTH + 4, CHAT_INPUT_HEIGHT + 4),
                        new Color(0, 0, 0, 180));
                }

                // Draw input text
                string inputText = _inputText.ToString();
                spriteBatch.DrawString(_font, inputText, new Vector2(CHAT_INPUT_X, chatBoxY), Color.White);

                // Draw cursor as separate overlay (doesn't shift text)
                bool showCursor = ((tickCount - _cursorBlinkTimer) / CHAT_CURSOR_BLINK_RATE) % 2 == 0;
                if (showCursor && _backgroundTexture != null)
                {
                    // Calculate cursor X position based on text before cursor
                    string textBeforeCursor = inputText.Substring(0, _cursorPosition);
                    float cursorX = CHAT_INPUT_X + _font.MeasureString(textBeforeCursor).X;

                    // Draw cursor line
                    spriteBatch.Draw(_backgroundTexture,
                        new Rectangle((int)cursorX, chatBoxY, 1, CHAT_INPUT_HEIGHT),
                        Color.White);
                }
            }
        }
        #endregion

        private void SendTargetedChatMessage(string message, int tickCount)
        {
            if (!string.IsNullOrWhiteSpace(_whisperTarget))
            {
                SendWhisperMessage(_whisperTarget, message, tickCount);
                return;
            }

            string prefix = GetTargetPrefix(_chatTarget);
            ClientChatLogType chatLogType = GetTargetChatLogType(_chatTarget);
            Color color = ResolveClientChatLogColor(chatLogType);
            if (string.IsNullOrEmpty(prefix))
            {
                AddMessage(message, color, tickCount, (int)chatLogType);
                MessageSubmitted?.Invoke(message, tickCount);
                return;
            }

            AddMessage($"{prefix} {message}", color, tickCount, (int)chatLogType);
            MessageSubmitted?.Invoke(message, tickCount);
        }

        private ChatSubmitDisposition TryHandleSlashCommand(string message, int tickCount)
        {
            if (string.IsNullOrWhiteSpace(message) || message[0] != '/')
            {
                return ChatSubmitDisposition.NotHandled;
            }

            string trimmedMessage = message.Trim();
            if (TryHandleWhisperCommand(trimmedMessage, tickCount, out ChatSubmitDisposition whisperDisposition))
            {
                return whisperDisposition;
            }

            if (TryHandleTargetModeCommand(trimmedMessage, tickCount, out ChatSubmitDisposition targetDisposition))
            {
                return targetDisposition;
            }

            return ChatSubmitDisposition.NotHandled;
        }

        private bool TryHandleWhisperCommand(string trimmedMessage, int tickCount, out ChatSubmitDisposition disposition)
        {
            disposition = ChatSubmitDisposition.NotHandled;
            bool isWhisperCommand = string.Equals(trimmedMessage, "/w", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmedMessage, "/whisper", StringComparison.OrdinalIgnoreCase)
                || trimmedMessage.StartsWith("/w ", StringComparison.OrdinalIgnoreCase)
                || trimmedMessage.StartsWith("/whisper ", StringComparison.OrdinalIgnoreCase);
            bool isReplyCommand = string.Equals(trimmedMessage, "/r", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmedMessage, "/reply", StringComparison.OrdinalIgnoreCase)
                || trimmedMessage.StartsWith("/r ", StringComparison.OrdinalIgnoreCase)
                || trimmedMessage.StartsWith("/reply ", StringComparison.OrdinalIgnoreCase);

            if (!isWhisperCommand && !isReplyCommand)
            {
                return false;
            }

            if (isWhisperCommand)
            {
                string[] parts = trimmedMessage.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                {
                    if (string.IsNullOrWhiteSpace(_whisperTarget) && _whisperCandidates.Count == 0)
                    {
                        AddClientMessage("Usage: /w <name> [message]", tickCount, ClientChatLogType.Error);
                    }
                    else
                    {
                        OpenWhisperTargetPicker(
                            tickCount,
                            _whisperTarget,
                            WhisperTargetPickerPresentation.Modal);
                    }

                    disposition = ChatSubmitDisposition.KeepChatOpen;
                    return true;
                }

                if (!TryArmWhisperTarget(parts[1], tickCount))
                {
                    disposition = ChatSubmitDisposition.KeepChatOpen;
                    return true;
                }

                if (parts.Length >= 3)
                {
                    SendWhisperMessage(_whisperTarget, parts[2], tickCount);
                    disposition = ChatSubmitDisposition.CloseChat;
                    return true;
                }

                disposition = ChatSubmitDisposition.KeepChatOpen;
                return true;
            }

            string replyTarget = !string.IsNullOrWhiteSpace(_replyTarget) ? _replyTarget : _whisperTarget;
            if (string.IsNullOrWhiteSpace(replyTarget))
            {
                if (_whisperCandidates.Count == 0)
                {
                    AddClientMessage("No whisper target selected.", tickCount, ClientChatLogType.Error);
                }
                else
                {
                    OpenWhisperTargetPicker(
                        tickCount,
                        presentation: WhisperTargetPickerPresentation.Modal);
                }
                disposition = ChatSubmitDisposition.KeepChatOpen;
                return true;
            }

            if (!TryArmWhisperTarget(replyTarget, tickCount))
            {
                disposition = ChatSubmitDisposition.KeepChatOpen;
                return true;
            }
            string[] replyParts = trimmedMessage.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (replyParts.Length < 2)
            {
                disposition = ChatSubmitDisposition.KeepChatOpen;
                return true;
            }

            SendWhisperMessage(_whisperTarget, replyParts[1], tickCount);
            disposition = ChatSubmitDisposition.CloseChat;
            return true;
        }

        private bool TryHandleTargetModeCommand(string trimmedMessage, int tickCount, out ChatSubmitDisposition disposition)
        {
            disposition = ChatSubmitDisposition.NotHandled;
            if (!TryParseTargetModeCommand(trimmedMessage, out MapSimulatorChatTargetType targetType, out string payload))
            {
                return false;
            }

            _chatTarget = targetType;
            _whisperTarget = string.Empty;

            if (string.IsNullOrWhiteSpace(payload))
            {
                disposition = ChatSubmitDisposition.KeepChatOpen;
                return true;
            }

            string prefix = GetTargetPrefix(targetType);
            ClientChatLogType chatLogType = GetTargetChatLogType(targetType);
            Color color = ResolveClientChatLogColor(chatLogType);
            if (string.IsNullOrEmpty(prefix))
            {
                AddMessage(payload, color, tickCount, (int)chatLogType);
            }
            else
            {
                AddMessage($"{prefix} {payload}", color, tickCount, (int)chatLogType);
            }

            MessageSubmitted?.Invoke(payload, tickCount);
            disposition = ChatSubmitDisposition.CloseChat;

            return true;
        }

        private void SendWhisperMessage(string whisperTarget, string message, int tickCount)
        {
            if (!TryArmWhisperTarget(whisperTarget, tickCount))
            {
                return;
            }

            AddMessage(
                $"> {_whisperTarget}: {message}",
                WhisperMessageColor,
                tickCount,
                (int)ClientChatLogType.OutgoingWhisper,
                _whisperTarget);
            MessageSubmitted?.Invoke(message, tickCount);
        }

        private bool TryArmWhisperTarget(string whisperTarget, int tickCount)
        {
            WhisperTargetValidationResult validationResult = ValidateWhisperTargetCandidate(
                whisperTarget,
                _localPlayerName,
                out string normalizedTarget);
            if (validationResult == WhisperTargetValidationResult.Empty)
            {
                return false;
            }

            if (validationResult == WhisperTargetValidationResult.Invalid)
            {
                AddClientMessage(
                    MapleStoryStringPool.GetOrFallback(0x031F, "Please enter a valid character name."),
                    tickCount,
                    ClientChatLogType.System);
                return false;
            }

            if (validationResult == WhisperTargetValidationResult.Self)
            {
                AddClientMessage(
                    MapleStoryStringPool.GetOrFallback(0x0320, "You cannot whisper yourself."),
                    tickCount,
                    ClientChatLogType.System);
                return false;
            }

            _whisperTarget = normalizedTarget;
            _replyTarget = normalizedTarget;
            AddWhisperCandidate(normalizedTarget);
            if (_isWhisperTargetPickerActive)
            {
                SyncWhisperTargetPickerSelectionFromInput();
            }
            return true;
        }

        private void AddClientMessage(string text, int tickCount, ClientChatLogType chatLogType, Color? colorOverride = null)
        {
            Color color = colorOverride ?? ResolveClientChatLogColor(chatLogType);
            AddMessage(text, color, tickCount, (int)chatLogType);
        }

        private static Color ResolveClientChatLogColor(ClientChatLogType chatLogType, int channelId = -1)
        {
            return chatLogType switch
            {
                ClientChatLogType.All => DefaultMessageColor,
                ClientChatLogType.Party => PartyMessageColor,
                ClientChatLogType.Friend => FriendMessageColor,
                ClientChatLogType.Guild => GuildMessageColor,
                ClientChatLogType.Alliance => AllianceMessageColor,
                ClientChatLogType.Type11 => ClientType11Color,
                ClientChatLogType.Expedition => ExpeditionMessageColor,
                ClientChatLogType.Notice => NoticeMessageColor,
                ClientChatLogType.OutgoingWhisper => WhisperMessageColor,
                ClientChatLogType.Error => ErrorMessageColor,
                ClientChatLogType.System => SystemMessageColor,
                ClientChatLogType.IncomingWhisper => WhisperMessageColor,
                ClientChatLogType.Type18 => ClientType18Color,
                ClientChatLogType.Type19 => channelId != -1 ? ClientType22Color : ClientType20Color,
                ClientChatLogType.Type20 => ClientType20Color,
                ClientChatLogType.Type21 => new Color(255, 198, 0, 221),
                ClientChatLogType.Type22 => ClientType22Color,
                ClientChatLogType.Type23 => ClientType22Color,
                _ => DefaultMessageColor
            };
        }

        private static int InferClientChatLogType(string text, Color color)
        {
            if (TryInferClientChatLogTypeFromPrefix(text, out ClientChatLogType prefixType))
            {
                return (int)prefixType;
            }

            if (ColorsMatch(color, PartyMessageColor))
            {
                return (int)ClientChatLogType.Party;
            }

            if (ColorsMatch(color, FriendMessageColor))
            {
                return (int)ClientChatLogType.Friend;
            }

            if (ColorsMatch(color, GuildMessageColor))
            {
                return (int)ClientChatLogType.Guild;
            }

            if (ColorsMatch(color, AllianceMessageColor))
            {
                return (int)ClientChatLogType.Alliance;
            }

            if (ColorsMatch(color, ExpeditionMessageColor))
            {
                return (int)ClientChatLogType.Expedition;
            }

            if (ColorsMatch(color, ErrorMessageColor))
            {
                return (int)ClientChatLogType.Error;
            }

            if (ColorsMatch(color, NoticeMessageColor))
            {
                return (int)ClientChatLogType.Notice;
            }

            if (ColorsMatch(color, SystemMessageColor))
            {
                return (int)ClientChatLogType.System;
            }

            if (ColorsMatch(color, WhisperMessageColor))
            {
                return InferWhisperChatLogType(text);
            }

            if (ColorsMatch(color, ClientType11Color))
            {
                return (int)ClientChatLogType.Type11;
            }

            if (ColorsMatch(color, ClientType18Color))
            {
                return (int)ClientChatLogType.Type18;
            }

            if (ColorsMatch(color, ClientType20Color))
            {
                return (int)ClientChatLogType.Type20;
            }

            if (ColorsMatch(color, ClientType22Color))
            {
                return (int)ClientChatLogType.Type22;
            }

            return -1;
        }

        private static bool TryInferClientChatLogTypeFromPrefix(string text, out ClientChatLogType chatLogType)
        {
            chatLogType = ClientChatLogType.All;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (text.StartsWith("[Friend]", StringComparison.OrdinalIgnoreCase))
            {
                chatLogType = ClientChatLogType.Friend;
                return true;
            }

            if (text.StartsWith("[Party]", StringComparison.OrdinalIgnoreCase))
            {
                chatLogType = ClientChatLogType.Party;
                return true;
            }

            if (text.StartsWith("[Guild]", StringComparison.OrdinalIgnoreCase))
            {
                chatLogType = ClientChatLogType.Guild;
                return true;
            }

            if (text.StartsWith("[Alliance]", StringComparison.OrdinalIgnoreCase))
            {
                chatLogType = ClientChatLogType.Alliance;
                return true;
            }

            if (text.StartsWith("[Association]", StringComparison.OrdinalIgnoreCase))
            {
                chatLogType = ClientChatLogType.Alliance;
                return true;
            }

            if (text.StartsWith("[Expedition]", StringComparison.OrdinalIgnoreCase))
            {
                chatLogType = ClientChatLogType.Expedition;
                return true;
            }

            if (text.StartsWith("[System]", StringComparison.OrdinalIgnoreCase))
            {
                chatLogType = ClientChatLogType.System;
                return true;
            }

            if (text.StartsWith("[Notice]", StringComparison.OrdinalIgnoreCase))
            {
                chatLogType = ClientChatLogType.Notice;
                return true;
            }

            if (text.StartsWith("[Error]", StringComparison.OrdinalIgnoreCase))
            {
                chatLogType = ClientChatLogType.Error;
                return true;
            }

            if (HasWhisperPrefix(text)
                || text.StartsWith(">", StringComparison.Ordinal))
            {
                chatLogType = (ClientChatLogType)InferWhisperChatLogType(text);
                return true;
            }

            return false;
        }

        private static int InferWhisperChatLogType(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return (int)ClientChatLogType.IncomingWhisper;
            }

            if (text.StartsWith(">", StringComparison.Ordinal)
                || (HasWhisperPrefix(text)
                    && text.IndexOf("->", StringComparison.Ordinal) >= 0))
            {
                return (int)ClientChatLogType.OutgoingWhisper;
            }

            return (int)ClientChatLogType.IncomingWhisper;
        }

        private void RememberIncomingWhisperTarget(int chatLogType, string whisperTargetCandidate, string text)
        {
            if (chatLogType != (int)ClientChatLogType.IncomingWhisper)
            {
                return;
            }

            string resolvedTarget = whisperTargetCandidate;
            if (string.IsNullOrWhiteSpace(resolvedTarget))
            {
                resolvedTarget = ExtractIncomingWhisperTarget(text);
            }

            RememberWhisperTarget(resolvedTarget);
        }

        private static string ExtractIncomingWhisperTarget(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            int separatorIndex = text.IndexOf(':');
            if (separatorIndex < 0)
            {
                return string.Empty;
            }

            string prefix = text[..separatorIndex].Trim();
            if (!TryStripWhisperPrefix(prefix, out prefix))
            {
                return string.Empty;
            }

            return NormalizeChatSpeakerCandidate(prefix);
        }

        private static bool HasWhisperPrefix(string text)
        {
            return text.StartsWith("[Whisper]", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("[GM Whisper]", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryStripWhisperPrefix(string text, out string remainder)
        {
            remainder = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (text.StartsWith("[Whisper]", StringComparison.OrdinalIgnoreCase))
            {
                remainder = text["[Whisper]".Length..].TrimStart();
                return true;
            }

            if (text.StartsWith("[GM Whisper]", StringComparison.OrdinalIgnoreCase))
            {
                remainder = text["[GM Whisper]".Length..].TrimStart();
                return true;
            }

            return false;
        }

        internal static string NormalizeChatSpeakerCandidate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            int separatorIndex = trimmed.IndexOf(':');
            if (separatorIndex > 0)
            {
                trimmed = trimmed[..separatorIndex].TrimEnd();
            }

            if (TryStripWhisperPrefix(trimmed, out string whisperRemainder))
            {
                trimmed = whisperRemainder;
            }

            while (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                int closingBracketIndex = trimmed.IndexOf(']');
                if (closingBracketIndex <= 0 || closingBracketIndex >= trimmed.Length - 1)
                {
                    break;
                }

                trimmed = trimmed[(closingBracketIndex + 1)..].TrimStart();
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                trimmed = trimmed[1..].TrimStart();
            }

            int arrowIndex = trimmed.LastIndexOf("->", StringComparison.Ordinal);
            if (arrowIndex >= 0 && arrowIndex + 2 < trimmed.Length)
            {
                trimmed = trimmed[(arrowIndex + 2)..].TrimStart();
            }

            if (trimmed.StartsWith("GM ", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[3..].TrimStart();
            }

            if (TryTrimTrailingChannelSuffix(trimmed, out string withoutChannelSuffix))
            {
                trimmed = withoutChannelSuffix;
            }

            int lastSpaceIndex = trimmed.LastIndexOf(' ');
            if (lastSpaceIndex >= 0 && lastSpaceIndex + 1 < trimmed.Length)
            {
                trimmed = trimmed[(lastSpaceIndex + 1)..];
            }

            return trimmed.Trim();
        }

        internal static WhisperTargetValidationResult ValidateWhisperTargetCandidate(
            string whisperTarget,
            string localPlayerName,
            out string normalizedTarget)
        {
            normalizedTarget = NormalizeChatSpeakerCandidate(whisperTarget);
            if (string.IsNullOrWhiteSpace(normalizedTarget))
            {
                return WhisperTargetValidationResult.Empty;
            }

            if (!IsPlausibleCharacterName(normalizedTarget))
            {
                return WhisperTargetValidationResult.Invalid;
            }

            string normalizedLocalPlayerName = NormalizeChatSpeakerCandidate(localPlayerName);
            if (!string.IsNullOrWhiteSpace(normalizedLocalPlayerName)
                && string.Equals(normalizedTarget, normalizedLocalPlayerName, StringComparison.OrdinalIgnoreCase))
            {
                return WhisperTargetValidationResult.Self;
            }

            return WhisperTargetValidationResult.Valid;
        }

        private static bool IsPlausibleCharacterName(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            string trimmedName = characterName.Trim();
            if (trimmedName.Length < 4 || trimmedName.Length > 12)
            {
                return false;
            }

            int ambiguousCharacterCount = 0;
            foreach (char c in trimmedName)
            {
                bool isAsciiLetter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
                bool isAsciiDigit = c >= '0' && c <= '9';
                if (!isAsciiLetter && !isAsciiDigit)
                {
                    return false;
                }

                if (c == 'I' || c == 'l')
                {
                    ambiguousCharacterCount++;
                }
            }

            return ambiguousCharacterCount < 4;
        }

        private static bool TryTrimTrailingChannelSuffix(string text, out string trimmed)
        {
            trimmed = text;
            if (string.IsNullOrWhiteSpace(text) || !text.EndsWith(")", StringComparison.Ordinal))
            {
                return false;
            }

            int suffixStart = text.LastIndexOf(" (", StringComparison.Ordinal);
            if (suffixStart <= 0)
            {
                return false;
            }

            string suffix = text[(suffixStart + 2)..^1].Trim();
            if (!suffix.StartsWith("Ch", StringComparison.OrdinalIgnoreCase)
                && !suffix.StartsWith("Channel", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            trimmed = text[..suffixStart].TrimEnd();
            return true;
        }

        private static bool ColorsMatch(Color left, Color right)
        {
            return left.R == right.R
                && left.G == right.G
                && left.B == right.B
                && left.A == right.A;
        }

        private static ClientChatLogType GetTargetChatLogType(MapSimulatorChatTargetType targetType)
        {
            return targetType switch
            {
                MapSimulatorChatTargetType.All => ClientChatLogType.All,
                MapSimulatorChatTargetType.Friend => ClientChatLogType.Friend,
                MapSimulatorChatTargetType.Party => ClientChatLogType.Party,
                MapSimulatorChatTargetType.Guild => ClientChatLogType.Guild,
                MapSimulatorChatTargetType.Association => ClientChatLogType.Alliance,
                MapSimulatorChatTargetType.Expedition => ClientChatLogType.Expedition,
                _ => ClientChatLogType.All
            };
        }

        private void AddWhisperCandidate(string whisperTarget)
        {
            string normalizedTarget = NormalizeChatSpeakerCandidate(whisperTarget);
            if (string.IsNullOrWhiteSpace(normalizedTarget))
            {
                return;
            }

            _whisperCandidates.RemoveAll(candidate =>
                string.Equals(candidate, normalizedTarget, StringComparison.OrdinalIgnoreCase));
            _whisperCandidates.Insert(0, normalizedTarget);
            while (_whisperCandidates.Count > MAX_WHISPER_CANDIDATES)
            {
                _whisperCandidates.RemoveAt(_whisperCandidates.Count - 1);
            }
        }

        private void CancelWhisperTargetPicker()
        {
            CloseWhisperTargetPicker(restoreDraft: true);
        }

        private void CloseWhisperTargetPicker(bool restoreDraft)
        {
            _isWhisperTargetPickerActive = false;
            _whisperTargetPickerPresentation = WhisperTargetPickerPresentation.Inline;
            _whisperTargetPickerSelectionIndex = -1;
            _whisperTargetPickerModalButtonFocus = WhisperTargetPickerModalButtonFocus.Confirm;
            _whisperTargetPickerModalFocusTarget = WhisperTargetPickerModalFocusTarget.ComboBox;

            if (restoreDraft)
            {
                SetInputText(_savedChatInputBeforeWhisperPicker);
                _cursorPosition = Math.Clamp(_savedChatCursorBeforeWhisperPicker, 0, _inputText.Length);
            }
            else
            {
                SetInputText(string.Empty);
            }

            _savedChatInputBeforeWhisperPicker = string.Empty;
            _savedChatCursorBeforeWhisperPicker = 0;
        }

        private void MoveWhisperTargetPickerSelection(int delta, WhisperTargetPickerNavigationMode navigationMode)
        {
            if (_whisperCandidates.Count == 0)
            {
                _whisperTargetPickerSelectionIndex = -1;
                return;
            }

            int targetIndex;
            if (navigationMode == WhisperTargetPickerNavigationMode.Absolute)
            {
                targetIndex = Math.Clamp(delta, 0, _whisperCandidates.Count - 1);
            }
            else if (_whisperTargetPickerSelectionIndex < 0 || _whisperTargetPickerSelectionIndex >= _whisperCandidates.Count)
            {
                targetIndex = delta >= 0 ? 0 : _whisperCandidates.Count - 1;
            }
            else
            {
                int resolvedDelta = navigationMode == WhisperTargetPickerNavigationMode.Page
                    ? delta * WhisperTargetPickerVisibleRowCount
                    : delta;
                targetIndex = Math.Clamp(
                    _whisperTargetPickerSelectionIndex + resolvedDelta,
                    0,
                    _whisperCandidates.Count - 1);
            }

            _whisperTargetPickerSelectionIndex = targetIndex;
            SetInputText(_whisperCandidates[_whisperTargetPickerSelectionIndex]);
        }

        internal void MoveWhisperTargetPickerModalButtonFocus(int delta)
        {
            if (!_isWhisperTargetPickerActive
                || _whisperTargetPickerPresentation != WhisperTargetPickerPresentation.Modal
                || delta == 0)
            {
                return;
            }

            _whisperTargetPickerModalFocusTarget = WhisperTargetPickerModalFocusTarget.FooterButtons;
            _whisperTargetPickerModalButtonFocus = _whisperTargetPickerModalButtonFocus == WhisperTargetPickerModalButtonFocus.Confirm
                ? WhisperTargetPickerModalButtonFocus.Close
                : WhisperTargetPickerModalButtonFocus.Confirm;
        }

        internal void ActivateWhisperTargetPickerModalButtonFocus()
        {
            if (!_isWhisperTargetPickerActive
                || _whisperTargetPickerPresentation != WhisperTargetPickerPresentation.Modal)
            {
                return;
            }

            _whisperTargetPickerModalFocusTarget = WhisperTargetPickerModalFocusTarget.FooterButtons;
        }

        internal void ActivateWhisperTargetPickerModalComboFocus()
        {
            if (!_isWhisperTargetPickerActive
                || _whisperTargetPickerPresentation != WhisperTargetPickerPresentation.Modal)
            {
                return;
            }

            _whisperTargetPickerModalFocusTarget = WhisperTargetPickerModalFocusTarget.ComboBox;
        }

        internal void ToggleWhisperTargetPickerModalFocusTarget()
        {
            if (!_isWhisperTargetPickerActive
                || _whisperTargetPickerPresentation != WhisperTargetPickerPresentation.Modal)
            {
                return;
            }

            _whisperTargetPickerModalFocusTarget = _whisperTargetPickerModalFocusTarget == WhisperTargetPickerModalFocusTarget.ComboBox
                ? WhisperTargetPickerModalFocusTarget.FooterButtons
                : WhisperTargetPickerModalFocusTarget.ComboBox;
        }

        internal static int ResolveWhisperTargetPickerFirstVisibleIndex(
            int selectionIndex,
            int candidateCount,
            int visibleRowCount = WhisperTargetPickerVisibleRowCount)
        {
            if (candidateCount <= 0)
            {
                return 0;
            }

            int clampedVisibleRowCount = Math.Max(1, visibleRowCount);
            if (candidateCount <= clampedVisibleRowCount || selectionIndex < 0)
            {
                return 0;
            }

            int maxStartIndex = Math.Max(0, candidateCount - clampedVisibleRowCount);
            int preferredStartIndex = selectionIndex - clampedVisibleRowCount + 1;
            return Math.Clamp(preferredStartIndex, 0, maxStartIndex);
        }

        private string ResolveWhisperTargetPickerSelection()
        {
            string typedValue = _inputText.ToString();
            if (!string.IsNullOrWhiteSpace(typedValue))
            {
                return typedValue;
            }

            return _whisperTargetPickerSelectionIndex >= 0 && _whisperTargetPickerSelectionIndex < _whisperCandidates.Count
                ? _whisperCandidates[_whisperTargetPickerSelectionIndex]
                : string.Empty;
        }

        private void SyncWhisperTargetPickerSelectionFromInput()
        {
            if (!_isWhisperTargetPickerActive)
            {
                return;
            }

            string normalizedInput = NormalizeChatSpeakerCandidate(_inputText.ToString());
            if (string.IsNullOrWhiteSpace(normalizedInput))
            {
                _whisperTargetPickerSelectionIndex = -1;
                return;
            }

            for (int i = 0; i < _whisperCandidates.Count; i++)
            {
                if (string.Equals(_whisperCandidates[i], normalizedInput, StringComparison.OrdinalIgnoreCase))
                {
                    _whisperTargetPickerSelectionIndex = i;
                    return;
                }
            }

            _whisperTargetPickerSelectionIndex = -1;
        }

        private void SetInputText(string text)
        {
            _inputText.Clear();
            if (!string.IsNullOrEmpty(text))
            {
                _inputText.Append(text);
            }

            _cursorPosition = _inputText.Length;
        }

        private static bool TryParseTargetModeCommand(
            string trimmedMessage,
            out MapSimulatorChatTargetType targetType,
            out string payload)
        {
            targetType = MapSimulatorChatTargetType.All;
            payload = string.Empty;

            string[] parts = trimmedMessage.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            if (!TryMapTargetCommand(parts[0], out targetType))
            {
                return false;
            }

            if (parts.Length >= 2)
            {
                payload = parts[1].Trim();
            }

            return true;
        }

        private static bool TryMapTargetCommand(string command, out MapSimulatorChatTargetType targetType)
        {
            targetType = MapSimulatorChatTargetType.All;

            switch (command.ToLowerInvariant())
            {
                case "/all":
                case "/say":
                    targetType = MapSimulatorChatTargetType.All;
                    return true;
                case "/f":
                case "/friend":
                    targetType = MapSimulatorChatTargetType.Friend;
                    return true;
                case "/p":
                case "/party":
                    targetType = MapSimulatorChatTargetType.Party;
                    return true;
                case "/g":
                case "/guild":
                    targetType = MapSimulatorChatTargetType.Guild;
                    return true;
                case "/a":
                case "/alliance":
                case "/association":
                    targetType = MapSimulatorChatTargetType.Association;
                    return true;
                case "/e":
                case "/expedition":
                    targetType = MapSimulatorChatTargetType.Expedition;
                    return true;
                default:
                    return false;
            }
        }

        private static string GetTargetPrefix(MapSimulatorChatTargetType targetType)
        {
            return targetType switch
            {
                MapSimulatorChatTargetType.All => string.Empty,
                MapSimulatorChatTargetType.Friend => "[Friend]",
                MapSimulatorChatTargetType.Party => "[Party]",
                MapSimulatorChatTargetType.Guild => "[Guild]",
                MapSimulatorChatTargetType.Association => "[Alliance]",
                MapSimulatorChatTargetType.Expedition => "[Expedition]",
                _ => string.Empty
            };
        }

    }
}
