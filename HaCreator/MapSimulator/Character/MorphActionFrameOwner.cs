using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character
{
    internal sealed class MorphActionFrameOwner
    {
        private readonly Dictionary<string, string> _resolvedActionCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Func<string, CharacterAnimation> _actionLoader;

        internal MorphActionFrameOwner(int morphTemplateId, Func<string, CharacterAnimation> actionLoader)
        {
            MorphTemplateId = morphTemplateId;
            _actionLoader = actionLoader ?? throw new ArgumentNullException(nameof(actionLoader));
        }

        internal int MorphTemplateId { get; }

        internal CharacterAnimation GetAnimation(CharacterPart part, string actionName)
        {
            if (part?.Type != CharacterPartType.Morph || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            if (_resolvedActionCache.TryGetValue(actionName, out string resolvedActionName))
            {
                return TryLoadNamedAnimation(part, resolvedActionName, out CharacterAnimation cachedAnimation)
                    ? cachedAnimation
                    : null;
            }

            foreach (string candidate in EnumerateActionCandidates(part, actionName))
            {
                if (TryLoadNamedAnimation(part, candidate, out CharacterAnimation animation))
                {
                    _resolvedActionCache[actionName] = candidate;
                    return animation;
                }
            }

            _resolvedActionCache[actionName] = string.Empty;
            return null;
        }

        private static IEnumerable<string> EnumerateActionCandidates(CharacterPart part, string actionName)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string candidate in MorphClientActionResolver.EnumerateClientActionAliases(part, actionName))
            {
                if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private bool TryLoadNamedAnimation(CharacterPart part, string actionName, out CharacterAnimation animation)
        {
            animation = null;
            if (part == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (part.Animations.TryGetValue(actionName, out animation)
                && animation?.Frames?.Count > 0)
            {
                return true;
            }

            if (part.AvailableAnimations?.Count > 0
                && !part.AvailableAnimations.Contains(actionName))
            {
                return false;
            }

            animation = _actionLoader(actionName);
            if (animation?.Frames?.Count > 0)
            {
                part.Animations[actionName] = animation;
                return true;
            }

            animation = null;
            return false;
        }
    }
}
