using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Manages the conversation history for multi-turn AI interactions.
    /// Maintains both the display messages (ObservableCollection for UI) and
    /// converts to API message format (JArray for OpenRouter).
    /// </summary>
    public class ChatSession : INotifyPropertyChanged
    {
        private readonly ObservableCollection<ChatMessage> _messages;
        private string _systemPrompt;
        private string _currentMapContext;
        private ChatMessage _lastAssistantMessage;

        public ChatSession()
        {
            _messages = new ObservableCollection<ChatMessage>();
        }

        /// <summary>
        /// Observable collection of messages for UI binding
        /// </summary>
        public ObservableCollection<ChatMessage> Messages => _messages;

        /// <summary>
        /// The system prompt for this session
        /// </summary>
        public string SystemPrompt
        {
            get => _systemPrompt;
            set
            {
                if (_systemPrompt != value)
                {
                    _systemPrompt = value;
                    OnPropertyChanged(nameof(SystemPrompt));
                }
            }
        }

        /// <summary>
        /// The current map context (updated when map changes)
        /// </summary>
        public string CurrentMapContext
        {
            get => _currentMapContext;
            set
            {
                if (_currentMapContext != value)
                {
                    _currentMapContext = value;
                    OnPropertyChanged(nameof(CurrentMapContext));
                }
            }
        }

        /// <summary>
        /// Reference to the last assistant message (for accessing commands)
        /// </summary>
        public ChatMessage LastAssistantMessage
        {
            get => _lastAssistantMessage;
            private set
            {
                if (_lastAssistantMessage != value)
                {
                    _lastAssistantMessage = value;
                    OnPropertyChanged(nameof(LastAssistantMessage));
                    OnPropertyChanged(nameof(HasCommands));
                }
            }
        }

        /// <summary>
        /// Whether there are commands available to execute
        /// </summary>
        public bool HasCommands => LastAssistantMessage?.HasCommands == true;

        /// <summary>
        /// Check if there are any messages
        /// </summary>
        public bool HasMessages => _messages.Count > 0;

        /// <summary>
        /// Add a user message to the conversation
        /// </summary>
        public ChatMessage AddUserMessage(string content)
        {
            var message = new ChatMessage(ChatRole.User, content);
            _messages.Add(message);
            return message;
        }

        /// <summary>
        /// Add an assistant message to the conversation.
        /// Call with empty content when starting processing (shows "Thinking..." indicator).
        /// </summary>
        public ChatMessage AddAssistantMessage(string content = "")
        {
            var message = new ChatMessage(ChatRole.Assistant, content)
            {
                IsProcessing = string.IsNullOrEmpty(content)
            };
            _messages.Add(message);
            LastAssistantMessage = message;
            return message;
        }

        /// <summary>
        /// Convert the session to JArray format for OpenRouter API.
        /// Includes system prompt, map context in first user message, and all conversation history.
        /// </summary>
        public JArray ToApiMessages()
        {
            var apiMessages = new JArray();

            // Add system message if set
            if (!string.IsNullOrEmpty(_systemPrompt))
            {
                apiMessages.Add(new JObject
                {
                    ["role"] = "system",
                    ["content"] = _systemPrompt
                });
            }

            // Track whether we've added map context
            bool contextAdded = false;

            // Add all non-processing messages
            foreach (var msg in _messages.Where(m => m.Role != ChatRole.System && !m.IsProcessing))
            {
                var role = msg.Role == ChatRole.User ? "user" : "assistant";
                var content = msg.Content ?? string.Empty;

                // Prepend map context to first user message
                if (!contextAdded && msg.Role == ChatRole.User && !string.IsNullOrEmpty(_currentMapContext))
                {
                    content = $"## Current Map State\n{_currentMapContext}\n\n## User Request\n{content}";
                    contextAdded = true;
                }

                // For assistant messages, include commands in the content for context
                if (msg.Role == ChatRole.Assistant && !string.IsNullOrEmpty(msg.CommandsContent))
                {
                    content = $"{content}\n\n## Generated Commands\n{msg.CommandsContent}";
                }

                apiMessages.Add(new JObject
                {
                    ["role"] = role,
                    ["content"] = content
                });
            }

            return apiMessages;
        }

        /// <summary>
        /// Get just the conversation history without system prompt or map context.
        /// Useful for displaying or debugging.
        /// </summary>
        public JArray ToConversationHistory()
        {
            var history = new JArray();

            foreach (var msg in _messages.Where(m => !m.IsProcessing))
            {
                var role = msg.Role == ChatRole.User ? "user" : "assistant";
                var content = msg.Content ?? string.Empty;

                if (msg.Role == ChatRole.Assistant && !string.IsNullOrEmpty(msg.CommandsContent))
                {
                    content = $"{content}\n\n## Generated Commands\n{msg.CommandsContent}";
                }

                history.Add(new JObject
                {
                    ["role"] = role,
                    ["content"] = content
                });
            }

            return history;
        }

        /// <summary>
        /// Clear all messages and reset the session
        /// </summary>
        public void Clear()
        {
            _messages.Clear();
            LastAssistantMessage = null;
            OnPropertyChanged(nameof(Messages));
            OnPropertyChanged(nameof(HasMessages));
            OnPropertyChanged(nameof(HasCommands));
        }

        /// <summary>
        /// Get the latest commands from the last assistant message
        /// </summary>
        public string GetLatestCommands()
        {
            return LastAssistantMessage?.CommandsContent ?? string.Empty;
        }

        /// <summary>
        /// Get the number of user messages (turns) in the conversation
        /// </summary>
        public int TurnCount => _messages.Count(m => m.Role == ChatRole.User);

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
