using System.Collections.Generic;
using HaCreator.MapSimulator.Entities;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class NpcDialogueResolver
    {
        public static IReadOnlyList<NpcInteractionPage> ResolveInitialPages(NpcItem npc)
        {
            string npcName = npc?.NpcInstance?.NpcInfo?.StringName;
            string npcDescription = npc?.NpcInstance?.NpcInfo?.StringFunc;

            var pages = new List<NpcInteractionPage>();

            if (!string.IsNullOrWhiteSpace(npcDescription))
            {
                pages.Add(new NpcInteractionPage
                {
                    Text = NpcDialogueTextFormatter.Format(npcDescription)
                });
            }

            IReadOnlyList<string> idleSpeechLines = npc?.GetIdleSpeechLines();
            if (idleSpeechLines != null)
            {
                for (int i = 0; i < idleSpeechLines.Count; i++)
                {
                    string line = NpcDialogueTextFormatter.Format(idleSpeechLines[i]);
                    if (string.IsNullOrWhiteSpace(line) ||
                        (!string.IsNullOrWhiteSpace(npcDescription) && string.Equals(line, NpcDialogueTextFormatter.Format(npcDescription), System.StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    pages.Add(new NpcInteractionPage
                    {
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
