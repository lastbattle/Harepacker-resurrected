using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed record ClientClaimChatLogResult(
        string ChatLog,
        int IncludedLineCount,
        bool ReachedCharacterBudget);

    internal static class ClientClaimChatLogParity
    {
        internal const int ClientClaimChatLogCharacterBudget = 1600;

        internal static ClientClaimChatLogResult BuildChatLogOfTwoCharacters(
            IReadOnlyList<string> storedChatLines,
            string targetCharacterName,
            string sendCharacterName)
        {
            if (storedChatLines == null || storedChatLines.Count == 0)
            {
                return new ClientClaimChatLogResult(string.Empty, 0, false);
            }

            string target = targetCharacterName ?? string.Empty;
            string sender = sendCharacterName ?? string.Empty;
            string chatLog = string.Empty;
            int countedCharacters = 0;
            int includedLineCount = 0;
            bool reachedBudget = false;

            for (int i = storedChatLines.Count - 1; i >= 0; i--)
            {
                string line = storedChatLines[i] ?? string.Empty;
                string characterName = ExtractCharacterName(line);
                if (!string.Equals(characterName, target, StringComparison.Ordinal)
                    && !string.Equals(characterName, sender, StringComparison.Ordinal))
                {
                    continue;
                }

                countedCharacters += line.Length + 1;
                if (countedCharacters > ClientClaimChatLogCharacterBudget)
                {
                    reachedBudget = true;
                    break;
                }

                string outputLine = line;
                if (string.Equals(characterName, sender, StringComparison.Ordinal)
                    && ClientCurseProcessParity.TryProcessString(
                        outputLine,
                        ignoreNewLine: true,
                        out string processedLine,
                        out _,
                        out _))
                {
                    outputLine = processedLine;
                }

                chatLog = outputLine + "\n" + chatLog;
                includedLineCount++;
            }

            if (chatLog.EndsWith("\n", StringComparison.Ordinal))
            {
                chatLog = chatLog[..^1];
            }

            return new ClientClaimChatLogResult(chatLog, includedLineCount, reachedBudget);
        }

        internal static string ExtractCharacterName(string chatLine)
        {
            if (string.IsNullOrEmpty(chatLine))
            {
                return string.Empty;
            }

            int separatorIndex = chatLine.IndexOf(':');
            if (separatorIndex < 0)
            {
                return string.Empty;
            }

            if (chatLine.Length >= 2
                && chatLine[0] == ' '
                && chatLine[1] == ' ')
            {
                return string.Empty;
            }

            int titleAndNameLength = Math.Max(0, separatorIndex - 1);
            if (titleAndNameLength == 0)
            {
                return string.Empty;
            }

            string titleAndName = chatLine[..titleAndNameLength];
            int nameStartIndex = 0;
            for (int i = titleAndName.IndexOf(' '); i != -1; i = titleAndName.IndexOf(' ', i + 1))
            {
                nameStartIndex = i + 1;
            }

            return nameStartIndex >= titleAndName.Length
                ? string.Empty
                : titleAndName[nameStartIndex..];
        }
    }
}
