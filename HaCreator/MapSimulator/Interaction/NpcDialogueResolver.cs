using System.Collections.Generic;
using HaCreator.MapSimulator.Entities;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class NpcDialogueResolver
    {
        public static IReadOnlyList<NpcInteractionPage> ResolveInitialPages(
            NpcItem npc,
            NpcDialogueFormattingContext formattingContext = null)
        {
            string npcName = npc?.NpcInstance?.NpcInfo?.StringName;
            string npcDescription = npc?.NpcInstance?.NpcInfo?.StringFunc;

            var pages = new List<NpcInteractionPage>();
            string formattedDescription = NpcDialogueTextFormatter.Format(npcDescription, formattingContext);

            if (!string.IsNullOrWhiteSpace(npcDescription))
            {
                pages.Add(new NpcInteractionPage
                {
                    RawText = npcDescription,
                    Text = formattedDescription
                });
            }

            IReadOnlyList<string> idleSpeechLines = npc?.GetIdleSpeechLines();
            if (idleSpeechLines != null)
            {
                for (int i = 0; i < idleSpeechLines.Count; i++)
                {
                    string rawLine = idleSpeechLines[i];
                    string line = NpcDialogueTextFormatter.Format(rawLine, formattingContext);
                    if (string.IsNullOrWhiteSpace(line) ||
                        (!string.IsNullOrWhiteSpace(formattedDescription) && string.Equals(line, formattedDescription, System.StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    pages.Add(new NpcInteractionPage
                    {
                        RawText = rawLine ?? string.Empty,
                        Text = line
                    });
                }
            }

            if (pages.Count == 0)
            {
                pages.Add(new NpcInteractionPage
                {
                    Text = string.IsNullOrWhiteSpace(npcName)
                        ? "The NPC does not have dialogue text in the loaded data."
                        : $"{npcName} is ready to talk."
                });
            }

            return pages;
        }
    }
}
