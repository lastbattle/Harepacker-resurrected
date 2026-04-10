using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class ClientSkillCancelResolver
    {
        private static readonly int[] SupportedAffectedSkillCancelTypes = { 1, 10, 16, 33, 50, 51 };
        private static readonly IReadOnlyDictionary<int, int> ClientCancelRequestSkillAliases =
            new Dictionary<int, int>
            {
                [32120000] = 32001003,
                [32110000] = 32101002,
                [32120001] = 32101003
            };

        internal static int NormalizeClientCancelRequestSkillId(int skillId)
        {
            return ClientCancelRequestSkillAliases.TryGetValue(skillId, out int normalizedSkillId)
                ? normalizedSkillId
                : skillId;
        }

        public static bool DoesClientCancelMatchSkillId(
            int activeSkillId,
            int requestedSkillId,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            if (activeSkillId <= 0)
            {
                return false;
            }

            if (requestedSkillId <= 0)
            {
                return true;
            }

            return activeSkillId == requestedSkillId
                   || ResolveCancelRequestSkillIds(activeSkillId, getSkillData, skillCatalog).Contains(requestedSkillId);
        }

        public static int ResolveCancelRequestSkillId(
            int skillId,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            return ResolveCancelRequestSkillIds(skillId, getSkillData, skillCatalog).FirstOrDefault();
        }

        public static IReadOnlyList<int> ResolveCancelRequestSkillIds(
            int skillId,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            if (skillId <= 0)
            {
                return Array.Empty<int>();
            }

            HashSet<int> resolvedSkillIds = ResolveConnectedCancelFamily(skillId, getSkillData, skillCatalog);
            if (resolvedSkillIds.Count > 1)
            {
                resolvedSkillIds.Remove(skillId);
            }

            return resolvedSkillIds.Count > 0
                ? resolvedSkillIds.ToArray()
                : new[] { skillId };
        }

        internal static IReadOnlyList<int> ResolveConnectedCancelFamilySkillIds(
            int skillId,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            if (skillId <= 0)
            {
                return Array.Empty<int>();
            }

            return ResolveConnectedCancelFamily(skillId, getSkillData, skillCatalog).ToArray();
        }

        private static bool UsesAffectedSkillCancelFamily(SkillData skill)
        {
            return skill != null
                   && skill.GetAffectedSkillIds().Length > 0
                   && Array.IndexOf(SupportedAffectedSkillCancelTypes, skill.ClientInfoType) >= 0;
        }

        private static HashSet<int> ResolveConnectedCancelFamily(
            int rootSkillId,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            HashSet<int> resolvedSkillIds = new();
            if (rootSkillId <= 0)
            {
                return resolvedSkillIds;
            }

            Queue<int> pendingSkillIds = new();
            pendingSkillIds.Enqueue(rootSkillId);
            EnqueueSkillId(NormalizeClientCancelRequestSkillId(rootSkillId), pendingSkillIds, resolvedSkillIds);

            while (pendingSkillIds.Count > 0)
            {
                int currentSkillId = pendingSkillIds.Dequeue();
                if (!resolvedSkillIds.Add(currentSkillId))
                {
                    continue;
                }

                SkillData currentSkill = getSkillData?.Invoke(currentSkillId);
                EnqueueLinkedSkillIds(currentSkill, pendingSkillIds, resolvedSkillIds);

                if (skillCatalog == null || skillCatalog.Count == 0)
                {
                    continue;
                }

                foreach (SkillData candidate in skillCatalog)
                {
                    if (candidate?.SkillId <= 0
                        || resolvedSkillIds.Contains(candidate.SkillId)
                        || candidate.SkillId == currentSkillId)
                    {
                        continue;
                    }

                    if (candidate.LinksDummySkill(currentSkillId)
                        || (UsesAffectedSkillCancelFamily(candidate) && candidate.LinksAffectedSkill(currentSkillId)))
                    {
                        pendingSkillIds.Enqueue(candidate.SkillId);
                    }
                }
            }

            return resolvedSkillIds;
        }

        private static void EnqueueLinkedSkillIds(SkillData skill, Queue<int> pendingSkillIds, HashSet<int> resolvedSkillIds)
        {
            if (skill == null)
            {
                return;
            }

            if (UsesAffectedSkillCancelFamily(skill))
            {
                foreach (int affectedSkillId in skill.GetAffectedSkillIds())
                {
                    EnqueueSkillId(affectedSkillId, pendingSkillIds, resolvedSkillIds);
                }
            }

            int[] dummySkillIds = skill.DummySkillParents ?? Array.Empty<int>();
            for (int i = 0; i < dummySkillIds.Length; i++)
            {
                EnqueueSkillId(dummySkillIds[i], pendingSkillIds, resolvedSkillIds);
            }
        }

        private static void EnqueueSkillId(int skillId, Queue<int> pendingSkillIds, HashSet<int> resolvedSkillIds)
        {
            if (skillId > 0 && !resolvedSkillIds.Contains(skillId))
            {
                pendingSkillIds.Enqueue(skillId);
            }
        }
    }
}
