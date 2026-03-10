using System.Collections.Generic;
using HaCreator.MapSimulator.Entities;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class NpcDialogueResolver
    {
        public static IReadOnlyList<string> ResolveInitialPages(NpcItem npc)
        {
            string npcName = npc?.NpcInstance?.NpcInfo?.StringName;
            string npcDescription = npc?.NpcInstance?.NpcInfo?.StringFunc;

            var pages = new List<string>();

            if (!string.IsNullOrWhiteSpace(npcDescription))
            {
                pages.Add(npcDescription.Trim());
            }

            if (pages.Count == 0)
            {
                pages.Add(string.IsNullOrWhiteSpace(npcName)
                    ? "The NPC does not have dialogue text in the loaded data."
                    : $"{npcName} is ready to talk.");
            }

            return pages;
        }
    }
}
