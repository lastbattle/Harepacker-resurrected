using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator
{
    internal static class PacketOwnedSkillAliasCatalog
    {
        internal static HashSet<int> BuildVengeanceSkillIdCatalog(
            IEnumerable<KeyValuePair<int, string>> skillNames,
            int preferredCurrentSkillId,
            int preferredLegacySkillId,
            string canonicalSkillName)
        {
            var ids = new HashSet<int>();
            if (preferredCurrentSkillId > 0)
            {
                ids.Add(preferredCurrentSkillId);
            }

            if (preferredLegacySkillId > 0)
            {
                ids.Add(preferredLegacySkillId);
            }

            if (skillNames == null || string.IsNullOrWhiteSpace(canonicalSkillName))
            {
                return ids;
            }

            string normalizedCanonicalName = canonicalSkillName.Trim();
            foreach (KeyValuePair<int, string> entry in skillNames)
            {
                if (entry.Key <= 0 || string.IsNullOrWhiteSpace(entry.Value))
                {
                    continue;
                }

                if (string.Equals(entry.Value.Trim(), normalizedCanonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    ids.Add(entry.Key);
                }
            }

            return ids;
        }

        internal static int[] BuildPreferredAliasCandidates(
            IEnumerable<int> candidateSkillIds,
            int preferredCurrentSkillId,
            int preferredLegacySkillId)
        {
            var ordered = new List<int>();
            var yielded = new HashSet<int>();

            Add(preferredCurrentSkillId);
            Add(preferredLegacySkillId);

            if (candidateSkillIds != null)
            {
                foreach (int skillId in candidateSkillIds.OrderBy(static id => id))
                {
                    Add(skillId);
                }
            }

            return ordered.ToArray();

            void Add(int skillId)
            {
                if (skillId > 0 && yielded.Add(skillId))
                {
                    ordered.Add(skillId);
                }
            }
        }
    }
}
