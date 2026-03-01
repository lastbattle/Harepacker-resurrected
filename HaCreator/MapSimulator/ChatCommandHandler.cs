using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    /// <summary>
    /// Handles chat commands starting with "/"
    /// </summary>
    public class ChatCommandHandler
    {
        /// <summary>
        /// Result of command execution
        /// </summary>
        public struct CommandResult
        {
            public bool Success;
            public string Message;
            public Color MessageColor;

            public static CommandResult Ok(string message = null)
            {
                return new CommandResult
                {
                    Success = true,
                    Message = message,
                    MessageColor = Color.LightGreen
                };
            }

            public static CommandResult Error(string message)
            {
                return new CommandResult
                {
                    Success = false,
                    Message = message,
                    MessageColor = Color.Red
                };
            }

            public static CommandResult Info(string message)
            {
                return new CommandResult
                {
                    Success = true,
                    Message = message,
                    MessageColor = Color.Yellow
                };
            }
        }

        /// <summary>
        /// Delegate for command execution
        /// </summary>
        /// <param name="args">Command arguments (excluding the command name)</param>
        /// <returns>Result of command execution</returns>
        public delegate CommandResult CommandExecutor(string[] args);

        /// <summary>
        /// Command definition
        /// </summary>
        private class Command
        {
            public string Name;
            public string Description;
            public string Usage;
            public CommandExecutor Execute;
        }

        private readonly Dictionary<string, Command> _commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Register a new command
        /// </summary>
        /// <param name="name">Command name (without /)</param>
        /// <param name="description">Description for help</param>
        /// <param name="usage">Usage example</param>
        /// <param name="executor">Function to execute the command</param>
        public void RegisterCommand(string name, string description, string usage, CommandExecutor executor)
        {
            _commands[name.ToLower()] = new Command
            {
                Name = name.ToLower(),
                Description = description,
                Usage = usage,
                Execute = executor
            };
        }

        /// <summary>
        /// Check if a message is a command (starts with /)
        /// </summary>
        public bool IsCommand(string message)
        {
            return !string.IsNullOrEmpty(message) && message.StartsWith("/");
        }

        /// <summary>
        /// Execute a command
        /// </summary>
        /// <param name="message">Full message including /</param>
        /// <returns>Result of command execution</returns>
        public CommandResult ExecuteCommand(string message)
        {
            if (!IsCommand(message))
            {
                return CommandResult.Error("Not a command");
            }

            // Remove leading / and split by spaces
            string commandLine = message.Substring(1).Trim();
            if (string.IsNullOrEmpty(commandLine))
            {
                return CommandResult.Error("Empty command");
            }

            string[] parts = commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string commandName = parts[0].ToLower();

            // Handle built-in help command
            if (commandName == "help" || commandName == "?")
            {
                return ExecuteHelp(parts.Length > 1 ? parts[1] : null);
            }

            // Look up command
            if (!_commands.TryGetValue(commandName, out Command command))
            {
                return CommandResult.Error($"Unknown command: /{commandName}. Type /help for list of commands.");
            }

            // Extract arguments (everything after command name)
            string[] args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);

            try
            {
                return command.Execute(args);
            }
            catch (Exception ex)
            {
                return CommandResult.Error($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute the help command
        /// </summary>
        private CommandResult ExecuteHelp(string specificCommand)
        {
            if (!string.IsNullOrEmpty(specificCommand))
            {
                // Help for specific command
                if (_commands.TryGetValue(specificCommand.ToLower(), out Command cmd))
                {
                    return CommandResult.Info($"/{cmd.Name}: {cmd.Description}\nUsage: {cmd.Usage}");
                }
                return CommandResult.Error($"Unknown command: /{specificCommand}");
            }

            // List all commands
            var helpText = new System.Text.StringBuilder();
            helpText.AppendLine("Available commands:");
            foreach (var cmd in _commands.Values)
            {
                helpText.AppendLine($"  /{cmd.Name} - {cmd.Description}");
            }
            helpText.Append("Type /help <command> for more info");
            return CommandResult.Info(helpText.ToString());
        }
    }
}
