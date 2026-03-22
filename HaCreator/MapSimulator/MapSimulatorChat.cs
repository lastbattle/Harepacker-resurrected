using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;

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

        public ChatMessage(string text, Color color, int timestamp, int chatLogType = -1)
        {
            Text = text;
            Color = color;
            Timestamp = timestamp;
            ChatLogType = chatLogType;
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
        public bool IsActive { get; init; }
        public string InputText { get; init; } = string.Empty;
        public int CursorPosition { get; init; }
        public MapSimulatorChatTargetType TargetType { get; init; }
        public string WhisperTarget { get; init; } = string.Empty;
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

        private const int CHAT_INPUT_X = 5;
        private const int CHAT_INPUT_Y_OFFSET = 55; // Offset from bottom of screen (just above status bar level indicator)
        private const int CHAT_INPUT_WIDTH = 478;
        private const int CHAT_INPUT_HEIGHT = 15;
        private const int CHAT_MESSAGE_DISPLAY_TIME = 10000; // Messages fade after 10 seconds
        private const int CHAT_MAX_INPUT_LENGTH = 100;
        private const int MAX_CHAT_MESSAGES = 50;
        private const int CHAT_DISPLAY_LINES = 15;
        private const int CHAT_CURSOR_BLINK_RATE = 500; // Blink every 500ms

        // Key repeat settings
        private const int KEY_REPEAT_INITIAL_DELAY = 400; // ms before repeat starts
        private const int KEY_REPEAT_RATE = 35; // ms between repeats

        // Input history settings
        private const int MAX_INPUT_HISTORY = 50;
        #endregion

        #region Fields
        private bool _isActive = false;
        private readonly StringBuilder _inputText = new StringBuilder(128);
        private readonly List<ChatMessage> _messages = new List<ChatMessage>();
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

        private enum ChatSubmitDisposition
        {
            NotHandled = 0,
            CloseChat = 1,
            KeepChatOpen = 2
        }

        private enum ClientChatLogType
        {
            All = 0,
            Party = 2,
            Friend = 3,
            Guild = 4,
            Alliance = 5,
            System = 12,
            Error = 15,
            Whisper = 16,
            Expedition = 26
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
                CycleTarget(newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift) ? -1 : 1);
                return true;
            }

            // If chat is not active, don't consume input
            if (!_isActive)
                return false;

            // Handle Escape to cancel chat
            if (newKeyboardState.IsKeyDown(Keys.Escape))
            {
                _isActive = false;
                _inputText.Clear();
                _cursorPosition = 0;
                ResetKeyRepeat();
                ResetHistoryNavigation();
                return true;
            }

            // Handle Up arrow - browse history (older)
            if (newKeyboardState.IsKeyDown(Keys.Up) && oldKeyboardState.IsKeyUp(Keys.Up))
            {
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
                if (oldKeyboardState.IsKeyUp(Keys.Back))
                {
                    // First press - delete character before cursor
                    if (_cursorPosition > 0)
                    {
                        _inputText.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
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
                if (oldKeyboardState.IsKeyUp(Keys.Delete))
                {
                    // First press - delete character at cursor
                    if (_cursorPosition < _inputText.Length)
                    {
                        _inputText.Remove(_cursorPosition, 1);
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
                _cursorPosition = 0;
                return true;
            }

            // Handle End key - move cursor to end
            if (newKeyboardState.IsKeyDown(Keys.End) && oldKeyboardState.IsKeyUp(Keys.End))
            {
                _cursorPosition = _inputText.Length;
                return true;
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
                    key == Keys.Back || key == Keys.Enter || key == Keys.Escape ||
                    key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right ||
                    key == Keys.Home || key == Keys.End || key == Keys.Delete)
                    continue;

                // Process only newly pressed keys
                if (oldKeyboardState.IsKeyUp(key))
                {
                    char? c = KeyToChar(key, shift);
                    if (c.HasValue && _inputText.Length < CHAT_MAX_INPUT_LENGTH)
                    {
                        _inputText.Insert(_cursorPosition, c.Value);
                        _cursorPosition++;
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
            _messages.Add(new ChatMessage(text, color, tickCount, chatLogType));

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
            _inputText.Clear();
            _cursorPosition = 0;
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

        public MapSimulatorChatRenderState GetRenderState()
        {
            return new MapSimulatorChatRenderState
            {
                Messages = _messages,
                IsActive = _isActive,
                InputText = _inputText.ToString(),
                CursorPosition = _cursorPosition,
                TargetType = _chatTarget,
                WhisperTarget = _whisperTarget ?? string.Empty
            };
        }

        /// <summary>
        /// Clears all chat messages
        /// </summary>
        public void ClearMessages()
        {
            _messages.Clear();
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
            Color color = GetTargetColor(_chatTarget);
            int chatLogType = (int)GetTargetChatLogType(_chatTarget);
            if (string.IsNullOrEmpty(prefix))
            {
                AddMessage(message, color, tickCount, chatLogType);
                MessageSubmitted?.Invoke(message, tickCount);
                return;
            }

            AddMessage($"{prefix} {message}", color, tickCount, chatLogType);
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
                    if (string.IsNullOrWhiteSpace(_whisperTarget))
                    {
                        AddClientMessage("Usage: /w <name> [message]", tickCount, ClientChatLogType.Error);
                    }
                    else
                    {
                        AddClientMessage($"Whisper target set to {_whisperTarget}.", tickCount, ClientChatLogType.System);
                    }

                    disposition = ChatSubmitDisposition.KeepChatOpen;
                    return true;
                }

                _whisperTarget = parts[1];
                _replyTarget = parts[1];
                if (parts.Length >= 3)
                {
                    SendWhisperMessage(_whisperTarget, parts[2], tickCount);
                    disposition = ChatSubmitDisposition.CloseChat;
                    return true;
                }

                AddClientMessage($"Whisper target set to {_whisperTarget}.", tickCount, ClientChatLogType.System);
                disposition = ChatSubmitDisposition.KeepChatOpen;
                return true;
            }

            string replyTarget = !string.IsNullOrWhiteSpace(_replyTarget) ? _replyTarget : _whisperTarget;
            if (string.IsNullOrWhiteSpace(replyTarget))
            {
                AddClientMessage("No whisper target selected.", tickCount, ClientChatLogType.Error);
                disposition = ChatSubmitDisposition.KeepChatOpen;
                return true;
            }

            _whisperTarget = replyTarget;
            string[] replyParts = trimmedMessage.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (replyParts.Length < 2)
            {
                AddClientMessage($"Reply target set to {_whisperTarget}.", tickCount, ClientChatLogType.System);
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

            bool targetChanged = _chatTarget != targetType;
            _chatTarget = targetType;
            _whisperTarget = string.Empty;

            if (string.IsNullOrWhiteSpace(payload))
            {
                if (targetChanged)
                {
                    AddClientMessage($"{GetTargetModeDisplayName(targetType)} chat selected.", tickCount, ClientChatLogType.System);
                }

                disposition = ChatSubmitDisposition.KeepChatOpen;
                return true;
            }

            string prefix = GetTargetPrefix(targetType);
            Color color = GetTargetColor(targetType);
            if (string.IsNullOrEmpty(prefix))
            {
                AddMessage(payload, color, tickCount, (int)GetTargetChatLogType(targetType));
            }
            else
            {
                AddMessage($"{prefix} {payload}", color, tickCount, (int)GetTargetChatLogType(targetType));
            }

            MessageSubmitted?.Invoke(payload, tickCount);
            disposition = ChatSubmitDisposition.CloseChat;

            return true;
        }

        private void SendWhisperMessage(string whisperTarget, string message, int tickCount)
        {
            if (string.IsNullOrWhiteSpace(whisperTarget))
            {
                return;
            }

            _whisperTarget = whisperTarget;
            _replyTarget = whisperTarget;
            AddMessage($"> {whisperTarget}: {message}", WhisperMessageColor, tickCount, (int)ClientChatLogType.Whisper);
            MessageSubmitted?.Invoke(message, tickCount);
        }

        private void AddClientMessage(string text, int tickCount, ClientChatLogType chatLogType, Color? colorOverride = null)
        {
            Color color = colorOverride ?? ResolveClientChatLogColor(chatLogType) ?? Color.White;
            AddMessage(text, color, tickCount, (int)chatLogType);
        }

        private static Color? ResolveClientChatLogColor(ClientChatLogType chatLogType)
        {
            return chatLogType switch
            {
                ClientChatLogType.Error => ErrorMessageColor,
                ClientChatLogType.System => SystemMessageColor,
                _ => null
            };
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

        private static string GetTargetModeDisplayName(MapSimulatorChatTargetType targetType)
        {
            return targetType switch
            {
                MapSimulatorChatTargetType.All => "All",
                MapSimulatorChatTargetType.Friend => "Friend",
                MapSimulatorChatTargetType.Party => "Party",
                MapSimulatorChatTargetType.Guild => "Guild",
                MapSimulatorChatTargetType.Association => "Alliance",
                MapSimulatorChatTargetType.Expedition => "Expedition",
                _ => "All"
            };
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

        private static Color GetTargetColor(MapSimulatorChatTargetType targetType)
        {
            return targetType switch
            {
                MapSimulatorChatTargetType.All => Color.White,
                MapSimulatorChatTargetType.Friend => new Color(255, 255, 120),
                MapSimulatorChatTargetType.Party => new Color(124, 255, 172),
                MapSimulatorChatTargetType.Guild => new Color(176, 255, 120),
                MapSimulatorChatTargetType.Association => new Color(124, 236, 255),
                MapSimulatorChatTargetType.Expedition => new Color(255, 216, 128),
                _ => Color.White
            };
        }
    }
}
