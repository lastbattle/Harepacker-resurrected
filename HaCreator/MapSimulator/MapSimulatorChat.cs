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

        public ChatMessage(string text, Color color, int timestamp)
        {
            Text = text;
            Color = color;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Handles chat input, message history, and rendering for the MapSimulator
    /// </summary>
    public class MapSimulatorChat
    {
        #region Constants
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

        private SpriteFont _font;
        private Texture2D _backgroundTexture;
        private int _screenHeight;

        private readonly ChatCommandHandler _commandHandler = new ChatCommandHandler();

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
        #endregion

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
                        ResetHistoryNavigation();

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
                            // Regular chat message
                            AddMessage(message, Color.White, tickCount);
                        }
                    }
                    _isActive = false;
                }
                else
                {
                    _isActive = true;
                    _cursorBlinkTimer = tickCount;
                    ResetHistoryNavigation();
                }
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
                }
                return true;
            }

            // Handle backspace with key repeat
            if (newKeyboardState.IsKeyDown(Keys.Back))
            {
                if (oldKeyboardState.IsKeyUp(Keys.Back))
                {
                    // First press
                    if (_inputText.Length > 0)
                        _inputText.Remove(_inputText.Length - 1, 1);
                    _lastHeldKey = Keys.Back;
                    _keyHoldStartTime = tickCount;
                    _lastKeyRepeatTime = tickCount;
                }
                else if (ShouldRepeatKey(Keys.Back, tickCount))
                {
                    // Key repeat
                    if (_inputText.Length > 0)
                        _inputText.Remove(_inputText.Length - 1, 1);
                    _lastKeyRepeatTime = tickCount;
                }
                return true;
            }
            else if (_lastHeldKey == Keys.Back)
            {
                ResetKeyRepeat();
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
                    key == Keys.Up || key == Keys.Down)
                    continue;

                // Process only newly pressed keys
                if (oldKeyboardState.IsKeyUp(key))
                {
                    char? c = KeyToChar(key, shift);
                    if (c.HasValue && _inputText.Length < CHAT_MAX_INPUT_LENGTH)
                    {
                        _inputText.Append(c.Value);
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
                    _inputText.Append(c.Value);
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
        public void AddMessage(string text, Color color, int tickCount)
        {
            _messages.Add(new ChatMessage(text, color, tickCount));

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

                // Draw input text with cursor
                string inputText = _inputText.ToString();
                bool showCursor = ((tickCount - _cursorBlinkTimer) / CHAT_CURSOR_BLINK_RATE) % 2 == 0;
                string displayText = inputText + (showCursor ? "|" : "");

                spriteBatch.DrawString(_font, displayText, new Vector2(CHAT_INPUT_X, chatBoxY), Color.White);
            }
        }
        #endregion
    }
}
