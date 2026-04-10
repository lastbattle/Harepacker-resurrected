using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character
{
    internal static class ShadowPartnerClientActionResolver
    {
        private static readonly string[] SwingHeuristicFragments =
        {
            "swing",
            "doubleswing",
            "tripleswing",
            "smash",
            "panic",
            "chop",
            "tempest",
            "strike",
            "wave",
            "upper",
            "spin",
            "demolition",
            "snatch",
            "shockwave",
            "dragonstrike",
            "backspin",
            "doubleupper",
            "screw",
            "straight",
            "somersault",
            "fist"
        };

        private static readonly string[] StabHeuristicFragments =
        {
            "stab",
            "pierce",
            "thrust",
            "assaulter",
            "assassination",
            "savage",
            "showdown"
        };

        private static readonly string[] RangedHeuristicFragments =
        {
            "shoot",
            "shot",
            "arrow",
            "rain",
            "orb",
            "fire",
            "burst",
            "drain",
            "spear",
            "windshot",
            "windspear",
            "stormbreak",
            "arrowrain",
            "eburster",
            "edrain",
            "eorb",
            "ninjastorm",
            "vampire"
        };

        private static readonly IReadOnlyDictionary<string, string[]> SharedAliasMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["move"] = new[] { "walk1", "walk2" },
                ["walk"] = new[] { "walk1", "walk2" },
                ["stand"] = new[] { "stand1", "stand2" },
                ["ghostwalk"] = new[] { "walk1", "walk2", "stand1", "stand2" },
                ["ghoststand"] = new[] { "stand1", "stand2" },
                ["ghostjump"] = new[] { "jump", "fly", "stand1", "stand2" },
                ["ghostladder"] = new[] { "ladder", "rope", "stand1", "stand2" },
                ["ghostrope"] = new[] { "rope", "ladder", "stand1", "stand2" },
                ["ghostprone"] = new[] { "prone", "proneStab", "stand1", "stand2" },
                ["ghostpronestab"] = new[] { "proneStab", "prone", "stand1", "stand2" },
                ["ghost"] = new[] { "stand1", "stand2", "dead" },
                // action_mapping_for_ghost@0x406500 remaps the ghost heal raw action
                // onto raw action 48 before LoadShadowPartnerAction falls back to the
                // plain action-name lookup, so keep `heal` ahead of idle fallback here too.
                ["ghostheal"] = new[] { "heal", "stand1", "stand2" },
                // `special/*` does not publish ghost or fly2/swim-specific branches for the
                // confirmed Shadow Partner skills, so keep the client-shaped stand/fly/jump
                // collapse ahead of broader fallback when those raw-action families surface.
                ["ghostfly"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["ghostsit"] = new[] { "sit", "stand1", "stand2" },
                ["hit"] = new[] { "alert", "stand1", "stand2" },
                ["dead"] = new[] { "dead", "stand1" },
                ["swim"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2Move"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2Skill"] = new[] { "stand1", "stand2", "fly", "jump" },
                // The Shadow Partner jobs still publish non-generic raw action names in WZ:
                // `Skill/411.img/skill/4111005/action/0 = avenger`,
                // `Skill/1411.img/skill/14111002/action/0 = avenger`,
                // `Skill/421.img/skill/4211002/action/0 = assaulter`,
                // and `Skill/421.img/skill/4211006/action/0 = prone2`.
                // `special/*` only authors generic shoot/stab/prone families, so mirror the
                // client-owned alias seam here before falling back to the plain raw action name.
                ["avenger"] = new[] { "shoot1", "shoot2", "shootF" },
                ["assaulter"] = new[] { "stabO1", "stabO2", "stabOF" },
                // `get_action_name_from_code(182) = flyingAssaulter` is client-owned rather
                // than WZ-authored in the mounted helper `action/0` rows, but it still resolves
                // to the same helper branch family as `assaulter`.
                ["flyingAssaulter"] = new[] { "assaulter", "stabO1", "stabO2", "stabOF" },
                ["prone2"] = new[] { "prone", "proneStab", "stand1", "stand2" },
                // Additional thief/night-walker helper raw action names recovered from WZ:
                // `Skill/420.img/skill/4201005/action/0 = savage`,
                // `Skill/412.img/skill/4121003/action/0 = showdown`,
                // `Skill/422.img/skill/4221001/action/0 = assassination`,
                // `Skill/422.img/skill/4221006/action/0 = smokeshell`,
                // `Skill/412.img/skill/4121008/action/0 = ninjastorm`,
                // and `Skill/1410.img/skill/14101006/action/0 = vampire`.
                // Shadow Partner still only publishes generic `special/*` families, so keep these
                // on client-owned helper rows before the plain raw-action fallback.
                ["savage"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["showdown"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["assassination"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["assassinations"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["ninjastorm"] = new[] { "shoot1", "shoot2", "shootF" },
                ["vampire"] = new[] { "shoot1", "shoot2", "shootF" },
                ["smokeshell"] = new[] { "alert", "stand1", "stand2" },
                // Client raw actions still include broader attack families such as the
                // dual-blade, polearm, and crossbow-specific aliases below. Shadow
                // Partner only authors the generic `special/*` families, so keep
                // collapsing these raw names onto those authored branches before
                // broader fallback.
                ["stabD1"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["swingD1"] = new[] { "swingO1", "swingO2", "swingO3", "swingOF" },
                ["swingD2"] = new[] { "swingO1", "swingO2", "swingO3", "swingOF" },
                ["doubleSwing"] = new[] { "swingP1", "swingP2", "swingPF" },
                ["tripleSwing"] = new[] { "swingP1", "swingP2", "swingPF" },
                ["shotC1"] = new[] { "shoot1", "shoot2", "shootF" }
            };

        private static readonly string[] PiecedShadowPartnerActionNames =
        {
            "avenger",
            "assaulter",
            "flyingAssaulter",
            "savage",
            "showdown",
            "assassination",
            "assassinations",
            "ninjastorm",
            "vampire",
            "stabD1",
            "swingD1",
            "swingD2",
            "doubleSwing",
            "tripleSwing",
            "shotC1"
        };

        public static IEnumerable<string> EnumerateClientMappedCandidates(
            string playerActionName,
            PlayerState state,
            string fallbackActionName,
            string weaponType = null,
            int? rawActionCode = null)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string rawActionName = null;

            // `LoadShadowPartnerAction` walks action-specific remap rows before it falls back
            // to the plain `get_action_name_from_code(raw)` lookup. Mirror that client order
            // for the alias-backed helper families we have recovered so far instead of only
            // special-casing ghost-family actions.
            if (ShouldPreferActionSpecificAliasCandidates(playerActionName))
            {
                foreach (string candidate in EnumerateAliasCandidates(playerActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (rawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out rawActionName)
                && !string.IsNullOrWhiteSpace(rawActionName))
            {
                foreach (string candidate in EnumerateActionSpecificAliasCandidates(rawActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                if (yielded.Add(rawActionName))
                {
                    yield return rawActionName;
                }
            }

            if (!string.IsNullOrWhiteSpace(playerActionName))
            {
                if (yielded.Add(playerActionName))
                {
                    yield return playerActionName;
                }

                foreach (string candidate in EnumerateAliasCandidates(playerActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                foreach (string candidate in EnumerateHeuristicAttackAliases(playerActionName, state, weaponType))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (state is PlayerState.Swimming or PlayerState.Flying)
            {
                foreach (string candidate in new[] { "stand1", "stand2", "fly", "jump" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state is PlayerState.Jumping or PlayerState.Falling)
            {
                foreach (string candidate in new[] { "jump", "fly", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Ladder)
            {
                foreach (string candidate in new[] { "ladder", "rope", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Rope)
            {
                foreach (string candidate in new[] { "rope", "ladder", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Prone)
            {
                foreach (string candidate in new[] { "prone", "proneStab", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Sitting)
            {
                foreach (string candidate in new[] { "sit", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Hit)
            {
                foreach (string candidate in new[] { "alert", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Dead)
            {
                foreach (string candidate in new[] { "dead", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Walking)
            {
                foreach (string candidate in new[] { "walk1", "walk2", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Standing)
            {
                foreach (string candidate in new[] { "stand1", "stand2", "alert" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackActionName) && yielded.Add(fallbackActionName))
            {
                yield return fallbackActionName;
            }
        }

        public static IEnumerable<string> EnumerateClientActionAliases(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                yield break;
            }

            foreach (string candidate in EnumerateAliasCandidates(playerActionName))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateAliasCandidates(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                yield break;
            }

            if (playerActionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase))
            {
                yield return "alert";
                yield break;
            }

            if (string.Equals(playerActionName, "ladder2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "ladder";
                yield break;
            }

            if (string.Equals(playerActionName, "rope2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "rope";
                yield break;
            }

            if (SharedAliasMap.TryGetValue(playerActionName, out string[] aliases))
            {
                foreach (string alias in aliases)
                {
                    yield return alias;
                }
            }
        }

        public static IReadOnlyList<int> ResolveClientFrameRemap(
            string playerActionName,
            string resolvedActionName,
            int availableFrameCount)
        {
            return ResolveClientFrameRemap(playerActionName, null, resolvedActionName, availableFrameCount);
        }

        public static IReadOnlyList<int> ResolveClientFrameRemap(
            string playerActionName,
            string rawActionName,
            string resolvedActionName,
            int availableFrameCount)
        {
            if (availableFrameCount <= 0
                || string.IsNullOrWhiteSpace(resolvedActionName))
            {
                return Array.Empty<int>();
            }

            IReadOnlyList<int> rawActionFrameRemap = ResolveSingleActionFrameRemap(rawActionName, resolvedActionName, availableFrameCount);
            if (rawActionFrameRemap.Count > 0)
            {
                return rawActionFrameRemap;
            }

            IReadOnlyList<int> playerActionFrameRemap = ResolveSingleActionFrameRemap(playerActionName, resolvedActionName, availableFrameCount);
            if (playerActionFrameRemap.Count > 0)
            {
                return playerActionFrameRemap;
            }

            return Array.Empty<int>();
        }

        public static SkillAnimation ResolvePlaybackAnimation(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string resolvedActionName,
            string playerActionName,
            string rawActionName = null)
        {
            if (actionAnimations == null
                || string.IsNullOrWhiteSpace(resolvedActionName)
                || !actionAnimations.TryGetValue(resolvedActionName, out SkillAnimation baseAnimation)
                || baseAnimation?.Frames == null
                || baseAnimation.Frames.Count == 0)
            {
                return null;
            }

            IReadOnlyList<int> frameRemap = ResolveClientFrameRemap(
                playerActionName,
                rawActionName,
                resolvedActionName,
                baseAnimation.Frames.Count);
            if (frameRemap == null || frameRemap.Count == 0)
            {
                return baseAnimation;
            }

            var remappedFrames = new List<SkillFrame>(frameRemap.Count);
            foreach (int requestedIndex in frameRemap)
            {
                int frameIndex = Math.Clamp(requestedIndex, 0, baseAnimation.Frames.Count - 1);
                remappedFrames.Add(baseAnimation.Frames[frameIndex]);
            }

            if (remappedFrames.Count == 0)
            {
                return baseAnimation;
            }

            var remappedAnimation = new SkillAnimation
            {
                Name = baseAnimation.Name,
                Frames = remappedFrames,
                Loop = baseAnimation.Loop,
                Origin = baseAnimation.Origin,
                ZOrder = baseAnimation.ZOrder
            };
            remappedAnimation.CalculateDuration();
            return remappedAnimation;
        }

        internal static IEnumerable<string> EnumeratePiecedShadowPartnerActionNames()
        {
            foreach (string actionName in PiecedShadowPartnerActionNames)
            {
                yield return actionName;
            }
        }

        internal static SkillAnimation TryBuildPiecedShadowPartnerActionAnimation(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string actionName)
        {
            if (actionAnimations == null
                || actionAnimations.Count == 0
                || string.IsNullOrWhiteSpace(actionName)
                || !SharedAliasMap.TryGetValue(actionName, out string[] pieceActionNames)
                || pieceActionNames == null
                || pieceActionNames.Length == 0)
            {
                return null;
            }

            var frames = new List<SkillFrame>();
            SkillAnimation firstPieceAnimation = null;
            foreach (string pieceActionName in pieceActionNames)
            {
                if (string.IsNullOrWhiteSpace(pieceActionName)
                    || !actionAnimations.TryGetValue(pieceActionName, out SkillAnimation pieceAnimation)
                    || pieceAnimation?.Frames == null
                    || pieceAnimation.Frames.Count == 0)
                {
                    continue;
                }

                firstPieceAnimation ??= pieceAnimation;
                foreach (SkillFrame frame in pieceAnimation.Frames)
                {
                    if (frame != null)
                    {
                        frames.Add(frame);
                    }
                }
            }

            if (frames.Count == 0)
            {
                return null;
            }

            var piecedAnimation = new SkillAnimation
            {
                Name = actionName,
                Frames = frames,
                Loop = false,
                Origin = firstPieceAnimation?.Origin ?? Point.Zero,
                ZOrder = firstPieceAnimation?.ZOrder ?? 0
            };
            piecedAnimation.CalculateDuration();
            return piecedAnimation;
        }

        public static Point ResolveClientTargetOffset(
            string observedPlayerActionName,
            PlayerState state,
            bool facingRight,
            int sideOffsetPx,
            int backActionOffsetYPx)
        {
            if (IsClientBackAction(observedPlayerActionName, state))
            {
                return new Point(0, backActionOffsetYPx);
            }

            return new Point(facingRight ? -sideOffsetPx : sideOffsetPx, 0);
        }

        public static Point InterpolateClientOffset(
            Point startOffset,
            Point targetOffset,
            int transitionStartTime,
            int currentTime,
            int transitionDurationMs)
        {
            if (transitionDurationMs <= 0)
            {
                return targetOffset;
            }

            int elapsedTime = Math.Max(0, currentTime - transitionStartTime);
            float progress = MathHelper.Clamp(elapsedTime / (float)transitionDurationMs, 0f, 1f);
            return new Point(
                (int)Math.Round(MathHelper.Lerp(startOffset.X, targetOffset.X, progress)),
                (int)Math.Round(MathHelper.Lerp(startOffset.Y, targetOffset.Y, progress)));
        }

        public static int ResolveAttackDelayMs(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string actionName,
            int defaultDelayMs)
        {
            if (actionAnimations != null
                && !string.IsNullOrWhiteSpace(actionName)
                && actionAnimations.TryGetValue(actionName, out SkillAnimation animation)
                && animation?.Frames != null
                && animation.Frames.Count > 0)
            {
                int frameDelay = animation.Frames[0]?.Delay ?? 0;
                if (frameDelay > 0)
                {
                    return frameDelay;
                }
            }

            return defaultDelayMs;
        }

        internal static string ResolveCreateActionName(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            PlayerState state)
        {
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return null;
            }

            foreach (string candidate in EnumerateCreateActionCandidates(state))
            {
                if (actionAnimations.ContainsKey(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        internal static IEnumerable<string> EnumerateCreateActionCandidates(PlayerState state)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool airborne = state is PlayerState.Jumping or PlayerState.Falling or PlayerState.Swimming or PlayerState.Flying;
            bool stationary = state is PlayerState.Standing or PlayerState.Walking or PlayerState.Sitting or PlayerState.Prone;

            foreach (string candidate in new[] { "create2", "create3", "create4" })
            {
                string stateVariant = airborne
                    ? candidate + "_f"
                    : stationary
                        ? candidate + "_s"
                        : null;

                if (!string.IsNullOrWhiteSpace(stateVariant) && yielded.Add(stateVariant))
                {
                    yield return stateVariant;
                }

                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }

                string alternateVariant = airborne ? candidate + "_s" : candidate + "_f";
                if (yielded.Add(alternateVariant))
                {
                    yield return alternateVariant;
                }
            }
        }

        public static bool IsBlockingAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && (IsAttackAction(actionName)
                       || actionName.StartsWith("create", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsAttackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.StartsWith("swing", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("stab", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsClientBackAction(string observedPlayerActionName, PlayerState state)
        {
            if (state is PlayerState.Ladder or PlayerState.Rope)
            {
                return true;
            }

            return observedPlayerActionName?.ToLowerInvariant() switch
            {
                "ladder" or "ladder2" or "ghostladder" => true,
                "rope" or "rope2" or "ghostrope" => true,
                _ => false
            };
        }

        private static bool ShouldPreferActionSpecificAliasCandidates(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                return false;
            }

            if (playerActionName.StartsWith("ghost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (SharedAliasMap.ContainsKey(playerActionName))
            {
                return true;
            }

            return string.Equals(playerActionName, "ladder2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(playerActionName, "rope2", StringComparison.OrdinalIgnoreCase)
                   || playerActionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseIndexedAlertFrame(string playerActionName, int availableFrameCount, out int frameIndex)
        {
            frameIndex = 0;
            if (!playerActionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase)
                || playerActionName.Length <= "alert".Length)
            {
                return false;
            }

            string suffix = playerActionName["alert".Length..];
            if (!int.TryParse(suffix, out int indexedAlert)
                || indexedAlert <= 1)
            {
                return false;
            }

            frameIndex = Math.Clamp(indexedAlert - 1, 0, availableFrameCount - 1);
            return true;
        }

        private static IEnumerable<string> EnumerateActionSpecificAliasCandidates(string actionName)
        {
            if (!ShouldPreferActionSpecificAliasCandidates(actionName))
            {
                yield break;
            }

            foreach (string candidate in EnumerateAliasCandidates(actionName))
            {
                yield return candidate;
            }
        }

        public static IEnumerable<string> EnumerateClientIdentityCandidates(
            string playerActionName,
            PlayerState state,
            string weaponType = null,
            int? rawActionCode = null)
        {
            foreach (string candidate in EnumerateHelperIdentityCandidates(
                         playerActionName,
                         state,
                         weaponType,
                         rawActionCode))
            {
                yield return candidate;
            }
        }

        public static IEnumerable<string> EnumerateHelperIdentityCandidates(
            string playerActionName,
            PlayerState state,
            string weaponType = null,
            int? rawActionCode = null)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string rawActionName = null;

            if (ShouldPreferActionSpecificAliasCandidates(playerActionName))
            {
                foreach (string candidate in EnumerateAliasCandidates(playerActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (rawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out rawActionName)
                && !string.IsNullOrWhiteSpace(rawActionName))
            {
                foreach (string candidate in EnumerateActionSpecificAliasCandidates(rawActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                if (yielded.Add(rawActionName))
                {
                    yield return rawActionName;
                }

                foreach (string candidate in EnumerateHeuristicAttackAliases(rawActionName, state, weaponType))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                yield break;
            }

            if (!ShouldSuppressRawBackedGenericAttackIdentityCandidate(playerActionName, rawActionCode)
                && yielded.Add(playerActionName))
            {
                yield return playerActionName;
            }

            foreach (string candidate in EnumerateAliasCandidates(playerActionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (string candidate in EnumerateHeuristicAttackAliases(playerActionName, state, weaponType))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        internal static bool ShouldSuppressRawBackedGenericAttackIdentityCandidate(
            string playerActionName,
            int? rawActionCode)
        {
            if (string.IsNullOrWhiteSpace(playerActionName)
                || !rawActionCode.HasValue
                || !playerActionName.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                || !CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out string rawActionName)
                || string.IsNullOrWhiteSpace(rawActionName))
            {
                return false;
            }

            return !string.Equals(playerActionName, rawActionName, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<int> ResolveSingleActionFrameRemap(
            string actionName,
            string resolvedActionName,
            int availableFrameCount)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return Array.Empty<int>();
            }

            if (string.Equals(actionName, "ladder2", StringComparison.OrdinalIgnoreCase)
                && string.Equals(resolvedActionName, "ladder", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { Math.Min(1, availableFrameCount - 1) };
            }

            if (string.Equals(actionName, "rope2", StringComparison.OrdinalIgnoreCase)
                && string.Equals(resolvedActionName, "rope", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { Math.Min(1, availableFrameCount - 1) };
            }

            if (string.Equals(actionName, "prone2", StringComparison.OrdinalIgnoreCase)
                && string.Equals(resolvedActionName, "prone", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { Math.Min(1, availableFrameCount - 1) };
            }

            if (actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase)
                && string.Equals(resolvedActionName, "alert", StringComparison.OrdinalIgnoreCase)
                && TryParseIndexedAlertFrame(actionName, availableFrameCount, out int alertFrameIndex))
            {
                return new[] { alertFrameIndex };
            }

            return Array.Empty<int>();
        }

        private static IEnumerable<string> EnumerateHeuristicAttackAliases(
            string playerActionName,
            PlayerState state,
            string weaponType)
        {
            if (!IsHeuristicAttackAction(playerActionName))
            {
                yield break;
            }

            bool floating = state is PlayerState.Jumping or PlayerState.Falling or PlayerState.Swimming or PlayerState.Flying;
            string normalizedWeaponType = weaponType?.Trim().ToLowerInvariant();

            if (playerActionName.IndexOf("prone", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                yield return "proneStab";
            }

            bool useRangedShootFamily = IsRangedWeaponType(normalizedWeaponType)
                                        || ContainsAnyFragment(playerActionName, RangedHeuristicFragments);
            bool usePolearmSwingFamily = IsPolearmWeaponType(normalizedWeaponType);
            bool useTwoHandedMeleeFamily = IsTwoHandedMeleeWeaponType(normalizedWeaponType);
            bool preferStabFamily = ContainsAnyFragment(playerActionName, StabHeuristicFragments);

            if (useRangedShootFamily)
            {
                foreach (string candidate in EnumerateRangedAttackCandidates(playerActionName, floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            if (preferStabFamily)
            {
                foreach (string candidate in EnumerateStabCandidates(useTwoHandedMeleeFamily, floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            if (usePolearmSwingFamily)
            {
                foreach (string candidate in EnumerateSwingCandidates("swingP", "swingT", floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            if (ContainsAnyFragment(playerActionName, SwingHeuristicFragments)
                || playerActionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foreach (string candidate in EnumerateSwingCandidates(
                             useTwoHandedMeleeFamily ? "swingT" : "swingO",
                             useTwoHandedMeleeFamily ? "swingO" : "swingT",
                             floating))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> EnumerateRangedAttackCandidates(string playerActionName, bool floating)
        {
            if (floating)
            {
                yield return "shootF";
            }

            if (string.Equals(playerActionName, "attack2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "shoot2";
                yield return "shoot1";
            }
            else
            {
                yield return "shoot1";
                yield return "shoot2";
            }

            if (!floating)
            {
                yield return "shootF";
            }
        }

        private static IEnumerable<string> EnumerateStabCandidates(bool preferTwoHandedFamily, bool floating)
        {
            foreach (string candidate in EnumerateAttackFamilyCandidates(
                         preferTwoHandedFamily ? "stabT" : "stabO",
                         preferTwoHandedFamily ? "stabO" : "stabT",
                         floating,
                         includeThirdGroundFrame: false))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateSwingCandidates(
            string primaryPrefix,
            string secondaryPrefix,
            bool floating)
        {
            foreach (string candidate in EnumerateAttackFamilyCandidates(
                         primaryPrefix,
                         secondaryPrefix,
                         floating,
                         includeThirdGroundFrame: true))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateAttackFamilyCandidates(
            string primaryPrefix,
            string secondaryPrefix,
            bool floating,
            bool includeThirdGroundFrame)
        {
            if (floating)
            {
                yield return primaryPrefix + "F";
            }

            yield return primaryPrefix + "1";
            yield return primaryPrefix + "2";

            if (includeThirdGroundFrame)
            {
                yield return primaryPrefix + "3";
            }

            if (!floating)
            {
                yield return primaryPrefix + "F";
            }

            if (floating)
            {
                yield return secondaryPrefix + "F";
            }

            yield return secondaryPrefix + "1";
            yield return secondaryPrefix + "2";

            if (includeThirdGroundFrame)
            {
                yield return secondaryPrefix + "3";
            }

            if (!floating)
            {
                yield return secondaryPrefix + "F";
            }
        }

        private static bool IsHeuristicAttackAction(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                return false;
            }

            return playerActionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
                   || ContainsAnyFragment(playerActionName, SwingHeuristicFragments)
                   || ContainsAnyFragment(playerActionName, StabHeuristicFragments)
                   || ContainsAnyFragment(playerActionName, RangedHeuristicFragments);
        }

        private static bool ContainsAnyFragment(string actionName, IEnumerable<string> fragments)
        {
            if (string.IsNullOrWhiteSpace(actionName) || fragments == null)
            {
                return false;
            }

            foreach (string fragment in fragments)
            {
                if (!string.IsNullOrWhiteSpace(fragment)
                    && actionName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRangedWeaponType(string weaponType)
        {
            return weaponType is "bow" or "crossbow" or "claw" or "gun" or "double bowgun" or "cannon";
        }

        private static bool IsPolearmWeaponType(string weaponType)
        {
            return weaponType is "spear" or "polearm";
        }

        private static bool IsTwoHandedMeleeWeaponType(string weaponType)
        {
            return weaponType is "2h sword" or "2h axe" or "2h blunt";
        }
    }
}
