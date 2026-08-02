using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator
{
    internal static class PacketOwnedSkillAliasCatalog
    {
        internal static HashSet<int> BuildSkillIdCatalog(
            IEnumerable<KeyValuePair<int, string>> skillNames,
            IEnumerable<KeyValuePair<int, string>> skillDescriptions,
            int preferredCurrentSkillId,
            int preferredLegacySkillId,
            string canonicalSkillName = null,
            string canonicalDescriptionFragment = null)
        {
            var ids = new HashSet<int>();
            AddCandidate(ids, preferredCurrentSkillId);
            AddCandidate(ids, preferredLegacySkillId);

            if (!string.IsNullOrWhiteSpace(canonicalSkillName) && skillNames != null)
            {
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
            }

            if (!string.IsNullOrWhiteSpace(canonicalDescriptionFragment) && skillDescriptions != null)
            {
                string normalizedDescriptionFragment = canonicalDescriptionFragment.Trim();
                foreach (KeyValuePair<int, string> entry in skillDescriptions)
                {
                    if (entry.Key <= 0 || string.IsNullOrWhiteSpace(entry.Value))
                    {
                        continue;
                    }

                    if (entry.Value.IndexOf(normalizedDescriptionFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ids.Add(entry.Key);
                    }
                }
            }

            return ids;
        }

        internal static HashSet<int> BuildVengeanceSkillIdCatalog(
            IEnumerable<KeyValuePair<int, string>> skillNames,
            int preferredCurrentSkillId,
            int preferredLegacySkillId,
            string canonicalSkillName)
        {
            return BuildSkillIdCatalog(
                skillNames: skillNames,
                skillDescriptions: null,
                preferredCurrentSkillId: preferredCurrentSkillId,
                preferredLegacySkillId: preferredLegacySkillId,
                canonicalSkillName: canonicalSkillName);
        }

        internal static int[] BuildPreferredAliasCandidates(
            IEnumerable<int> candidateSkillIds,
            int preferredCurrentSkillId,
            int preferredLegacySkillId)
        {
            var ordered = new List<int>();
            var yielded = new HashSet<int>();

            AddOrdered(preferredCurrentSkillId);
            AddOrdered(preferredLegacySkillId);

            if (candidateSkillIds != null)
            {
                foreach (int skillId in candidateSkillIds.OrderBy(static id => id))
                {
                    AddOrdered(skillId);
                }
            }

            return ordered.ToArray();

            void AddOrdered(int skillId)
            {
                AddOrderedCandidate(ordered, yielded, skillId);
            }
        }

        private static void AddCandidate(ICollection<int> values, int skillId)
        {
            if (skillId > 0)
            {
                values.Add(skillId);
            }
        }

        private static void AddOrderedCandidate(ICollection<int> ordered, ISet<int> yielded, int skillId)
        {
            if (skillId > 0 && yielded.Add(skillId))
            {
                ordered.Add(skillId);
            }
        }
    }
}
