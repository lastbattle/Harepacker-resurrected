using System;
using System.ComponentModel;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Role enum matching OpenRouter API message roles
    /// </summary>
    public enum ChatRole
    {
        User,
        Assistant,
        System
    }

    /// <summary>
    /// Represents a single message in the AI chat conversation.
    /// Implements INotifyPropertyChanged for WPF data binding.
    /// </summary>
    public class ChatMessage : INotifyPropertyChanged
    {
        private ChatRole _role;
        private string _content;
        private string _commandsContent;
        private DateTime _timestamp;
        private bool _isProcessing;
        private bool _hasError;
        private string _errorMessage;

        public ChatMessage(ChatRole role, string content)
        {
            _role = role;
            _content = content ?? string.Empty;
            _timestamp = DateTime.Now;
            _isProcessing = false;
            _hasError = false;
        }

        /// <summary>
        /// The role of this message (User, Assistant, System)
        /// </summary>
        public ChatRole Role
        {
            get => _role;
            set
            {
                if (_role != value)
                {
                    _role = value;
                    OnPropertyChanged(nameof(Role));
                    OnPropertyChanged(nameof(IsUser));
                    OnPropertyChanged(nameof(IsAssistant));
                }
            }
        }

        /// <summary>
        /// The main text content of the message
        /// </summary>
        public string Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged(nameof(Content));
                }
            }
        }

        /// <summary>
        /// Extracted commands from AI response (for Assistant messages)
        /// </summary>
        public string CommandsContent
        {
            get => _commandsContent;
            set
            {
                if (_commandsContent != value)
                {
                    _commandsContent = value;
                    OnPropertyChanged(nameof(CommandsContent));
                    OnPropertyChanged(nameof(HasCommands));
                }
            }
        }

        /// <summary>
        /// When the message was created
        /// </summary>
        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                if (_timestamp != value)
                {
                    _timestamp = value;
                    OnPropertyChanged(nameof(Timestamp));
                    OnPropertyChanged(nameof(TimestampDisplay));
                }
            }
        }

        /// <summary>
        /// Whether this message is still being processed (for loading indicator)
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    OnPropertyChanged(nameof(IsProcessing));
                }
            }
        }

        /// <summary>
        /// Whether this message resulted in an error
        /// </summary>
        public bool HasError
        {
            get => _hasError;
            set
            {
                if (_hasError != value)
                {
                    _hasError = value;
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        /// <summary>
        /// Error message if HasError is true
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged(nameof(ErrorMessage));
                }
            }
        }

        // Convenience properties for XAML binding
        public bool IsUser => _role == ChatRole.User;
        public bool IsAssistant => _role == ChatRole.Assistant;
        public bool HasCommands => !string.IsNullOrEmpty(_commandsContent);
        public string TimestampDisplay => _timestamp.ToString("HH:mm");

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
