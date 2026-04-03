using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Owns authenticated login-session state that is authored by login packets.
    /// This keeps the active roster in one place instead of relying on ad hoc
    /// packet-profile snapshots scattered across the simulator UI seams.
    /// </summary>
    public sealed class LoginBackendSessionManager
    {
        private readonly List<LoginSelectWorldCharacterEntry> _viewAllEntries = new();
        private readonly Dictionary<int, LoginSelectWorldResultProfile> _selectWorldProfilesByWorld = new();
        private readonly Dictionary<int, LoginSelectWorldResultProfile> _viewAllProfilesByWorld = new();
        private LoginExtraCharInfoResultProfile _lastExtraCharInfoResultProfile;

        public int? AuthenticatedAccountId { get; private set; }
        public int ActiveWorldId { get; private set; }
        public LoginSelectWorldResultProfile SelectWorldRosterProfile { get; private set; }
        public LoginSelectWorldResultProfile ViewAllCharRosterProfile { get; private set; }
        public int ViewAllExpectedCharacterCount { get; private set; }
        public int ViewAllRemainingServerCount { get; private set; }
        public bool CanHaveExtraCharacter { get; private set; }

        public void Reset()
        {
            _lastExtraCharInfoResultProfile = null;
            AuthenticatedAccountId = null;
            ClearRosterProfiles();
        }

        public void ClearRosterProfiles()
        {
            ActiveWorldId = 0;
            CanHaveExtraCharacter = false;
            SelectWorldRosterProfile = null;
            ViewAllCharRosterProfile = null;
            ViewAllExpectedCharacterCount = 0;
            ViewAllRemainingServerCount = 0;
            _viewAllEntries.Clear();
            _selectWorldProfilesByWorld.Clear();
            _viewAllProfilesByWorld.Clear();
        }

        public bool SetAuthenticatedAccountId(int? accountId)
        {
            if (!accountId.HasValue || accountId.Value <= 0)
            {
                return false;
            }

            if (AuthenticatedAccountId.HasValue && AuthenticatedAccountId.Value != accountId.Value)
            {
                ClearRosterProfiles();
            }

            bool changed = AuthenticatedAccountId != accountId.Value;
            AuthenticatedAccountId = accountId.Value;
            return ReevaluateExtraCharacterEntitlement() || changed;
        }

        public bool ApplyExtraCharInfoResult(LoginExtraCharInfoResultProfile profile)
        {
            _lastExtraCharInfoResultProfile = CloneExtraCharInfoProfile(profile);
            return ReevaluateExtraCharacterEntitlement();
        }

        public bool ConsumeExtraCharacterEntitlement()
        {
            _lastExtraCharInfoResultProfile = null;
            return SetExtraCharacterEntitlement(false);
        }

        public bool SetExtraCharacterEntitlement(bool canHaveExtraCharacter)
        {
            if (CanHaveExtraCharacter == canHaveExtraCharacter)
            {
                return false;
            }

            CanHaveExtraCharacter = canHaveExtraCharacter;
            int buyCharacterCount = canHaveExtraCharacter ? 1 : 0;
            SelectWorldRosterProfile = WithBuyCharacterCount(SelectWorldRosterProfile, buyCharacterCount);
            ViewAllCharRosterProfile = WithBuyCharacterCount(ViewAllCharRosterProfile, buyCharacterCount);
            UpdateProfileBuyCharacterCount(_selectWorldProfilesByWorld, buyCharacterCount);
            UpdateProfileBuyCharacterCount(_viewAllProfilesByWorld, buyCharacterCount);
            return true;
        }

        public void SetActiveWorld(int worldId)
        {
            ActiveWorldId = Math.Max(0, worldId);

            if (_selectWorldProfilesByWorld.TryGetValue(ActiveWorldId, out LoginSelectWorldResultProfile selectWorldProfile))
            {
                SelectWorldRosterProfile = CloneProfile(selectWorldProfile);
            }
            else
            {
                SelectWorldRosterProfile = null;
            }

            if (_viewAllProfilesByWorld.TryGetValue(ActiveWorldId, out LoginSelectWorldResultProfile viewAllWorldProfile))
            {
                ViewAllCharRosterProfile = CloneProfile(viewAllWorldProfile);
            }
            else if (ViewAllCharRosterProfile?.Entries?.Count > 0)
            {
                ViewAllCharRosterProfile = CreateWorldSpecificViewAllRoster(ViewAllCharRosterProfile, ActiveWorldId);
            }
            else
            {
                ViewAllCharRosterProfile = null;
            }
        }

        public void ApplySelectWorldResult(LoginSelectWorldResultProfile profile, int worldId)
        {
            ActiveWorldId = Math.Max(0, worldId);
            SelectWorldRosterProfile = NormalizeRosterEntitlement(profile);

            if (SelectWorldRosterProfile != null &&
                LoginSelectWorldResultCodec.IsSuccessCode(SelectWorldRosterProfile.ResultCode))
            {
                _selectWorldProfilesByWorld[ActiveWorldId] = CloneProfile(SelectWorldRosterProfile);
            }
            else
            {
                _selectWorldProfilesByWorld.Remove(ActiveWorldId);
            }
        }

        public void ApplyViewAllCharResult(LoginViewAllCharResultPacketProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            switch (profile.Kind)
            {
                case LoginViewAllCharResultKind.Header:
                    _viewAllEntries.Clear();
                    ViewAllCharRosterProfile = null;
                    _viewAllProfilesByWorld.Clear();
                    ViewAllRemainingServerCount = Math.Max(0, profile.RelatedServerCount);
                    ViewAllExpectedCharacterCount = Math.Max(0, profile.CharacterCount);
                    break;

                case LoginViewAllCharResultKind.Characters:
                    if (profile.Entries != null)
                    {
                        _viewAllEntries.AddRange(profile.Entries.Where(entry => entry != null));
                    }

                    if (ViewAllRemainingServerCount > 0)
                    {
                        ViewAllRemainingServerCount--;
                    }

                    if (ViewAllRemainingServerCount <= 0)
                    {
                        ViewAllCharRosterProfile = CreateRosterProfile(
                            _viewAllEntries,
                            _viewAllEntries.Count,
                            CanHaveExtraCharacter ? 1 : 0,
                            profile.LoginOpt ?? ViewAllCharRosterProfile?.LoginOpt ?? false);
                        CacheViewAllWorldProfiles(ViewAllCharRosterProfile);
                    }
                    break;

                case LoginViewAllCharResultKind.Completion:
                    if (_viewAllEntries.Count > 0)
                    {
                        ViewAllCharRosterProfile ??= CreateRosterProfile(
                            _viewAllEntries,
                            _viewAllEntries.Count,
                            CanHaveExtraCharacter ? 1 : 0,
                            false);
                        CacheViewAllWorldProfiles(ViewAllCharRosterProfile);
                    }

                    ViewAllRemainingServerCount = 0;
                    break;
            }
        }

        public void SynchronizeResolvedRoster(
            IReadOnlyList<LoginSelectWorldCharacterEntry> entries,
            int slotCount,
            int buyCharacterCount,
            int worldId,
            bool includeSelectWorldRoster,
            bool includeViewAllRoster,
            bool selectWorldLoginOpt,
            bool viewAllLoginOpt)
        {
            ActiveWorldId = Math.Max(0, worldId);
            int normalizedBuyCharacterCount = NormalizeBuyCharacterCount(buyCharacterCount);
            LoginSelectWorldResultProfile resolvedProfile = CreateRosterProfile(
                entries,
                slotCount,
                normalizedBuyCharacterCount,
                selectWorldLoginOpt);

            if (includeSelectWorldRoster || HasSelectWorldRoster)
            {
                SelectWorldRosterProfile = resolvedProfile;
                _selectWorldProfilesByWorld[ActiveWorldId] = CloneProfile(resolvedProfile);
            }

            if (includeViewAllRoster || HasViewAllRoster || _viewAllEntries.Count > 0)
            {
                _viewAllEntries.Clear();
                if (entries != null)
                {
                    _viewAllEntries.AddRange(entries.Where(entry => entry != null));
                }

                ViewAllCharRosterProfile = CreateRosterProfile(
                    _viewAllEntries,
                    slotCount,
                    normalizedBuyCharacterCount,
                    viewAllLoginOpt);
                ViewAllExpectedCharacterCount = _viewAllEntries.Count;
                ViewAllRemainingServerCount = 0;
                CacheViewAllWorldProfiles(ViewAllCharRosterProfile);
            }
        }

        public bool TryGetActiveRoster(
            int worldId,
            bool preferViewAllRoster,
            bool hasViewAllPacket,
            bool hasSelectWorldPacket,
            out LoginSelectWorldResultProfile rosterProfile,
            out string packetSource)
        {
            ActiveWorldId = Math.Max(0, worldId);
            rosterProfile = null;
            packetSource = null;

            if (preferViewAllRoster &&
                hasViewAllPacket &&
                TryGetWorldRoster(_viewAllProfilesByWorld, ActiveWorldId, out rosterProfile))
            {
                packetSource = "ViewAllCharResult";
                return true;
            }

            if (hasSelectWorldPacket &&
                TryGetWorldRoster(_selectWorldProfilesByWorld, ActiveWorldId, out rosterProfile) &&
                LoginSelectWorldResultCodec.IsSuccessCode(rosterProfile.ResultCode))
            {
                packetSource = "SelectWorldResult";
                return true;
            }

            if (preferViewAllRoster &&
                hasViewAllPacket &&
                ViewAllCharRosterProfile != null)
            {
                rosterProfile = ViewAllCharRosterProfile;
                packetSource = "ViewAllCharResult";
                return true;
            }

            if (hasSelectWorldPacket &&
                SelectWorldRosterProfile != null &&
                LoginSelectWorldResultCodec.IsSuccessCode(SelectWorldRosterProfile.ResultCode))
            {
                rosterProfile = SelectWorldRosterProfile;
                packetSource = "SelectWorldResult";
                return true;
            }

            if (ViewAllCharRosterProfile != null)
            {
                rosterProfile = ViewAllCharRosterProfile;
                packetSource = "ViewAllCharResult";
                return true;
            }

            return false;
        }

        public bool TryGetWorldRoster(int worldId, bool preferViewAllRoster, out LoginSelectWorldResultProfile rosterProfile, out string packetSource)
        {
            int normalizedWorldId = Math.Max(0, worldId);
            ActiveWorldId = normalizedWorldId;
            rosterProfile = null;
            packetSource = null;

            if (preferViewAllRoster &&
                TryGetWorldRoster(_viewAllProfilesByWorld, normalizedWorldId, out rosterProfile))
            {
                packetSource = "ViewAllCharResult";
                return true;
            }

            if (TryGetWorldRoster(_selectWorldProfilesByWorld, normalizedWorldId, out rosterProfile) &&
                LoginSelectWorldResultCodec.IsSuccessCode(rosterProfile.ResultCode))
            {
                packetSource = "SelectWorldResult";
                return true;
            }

            if (TryGetWorldRoster(_viewAllProfilesByWorld, normalizedWorldId, out rosterProfile))
            {
                packetSource = "ViewAllCharResult";
                return true;
            }

            return false;
        }

        public IReadOnlyDictionary<int, LoginSelectWorldResultProfile> SnapshotCachedWorldRosters(bool preferViewAllRoster = true)
        {
            Dictionary<int, LoginSelectWorldResultProfile> rostersByWorld = new();
            if (preferViewAllRoster)
            {
                AddProfiles(rostersByWorld, _viewAllProfilesByWorld, requireSuccessResult: false, overwriteExisting: true);
                AddProfiles(rostersByWorld, _selectWorldProfilesByWorld, requireSuccessResult: true, overwriteExisting: false);
            }
            else
            {
                AddProfiles(rostersByWorld, _selectWorldProfilesByWorld, requireSuccessResult: true, overwriteExisting: true);
                AddProfiles(rostersByWorld, _viewAllProfilesByWorld, requireSuccessResult: false, overwriteExisting: false);
            }

            if (SelectWorldRosterProfile != null &&
                LoginSelectWorldResultCodec.IsSuccessCode(SelectWorldRosterProfile.ResultCode) &&
                !rostersByWorld.ContainsKey(ActiveWorldId))
            {
                rostersByWorld[ActiveWorldId] = CloneProfile(SelectWorldRosterProfile);
            }

            if (ViewAllCharRosterProfile != null &&
                (preferViewAllRoster || !rostersByWorld.ContainsKey(ActiveWorldId)))
            {
                rostersByWorld[ActiveWorldId] = CloneProfile(ViewAllCharRosterProfile);
            }

            return rostersByWorld;
        }

        public IReadOnlyList<LoginSelectWorldCharacterEntry> SnapshotAggregatedViewAllEntries()
        {
            return _viewAllEntries
                .Where(entry => entry != null)
                .Select(CloneEntry)
                .ToArray();
        }

        public bool TryRemoveCharacter(
            int characterId,
            out LoginSelectWorldCharacterEntry removedEntry,
            out int removedWorldId)
        {
            removedEntry = null;
            removedWorldId = 0;
            if (characterId <= 0)
            {
                return false;
            }

            bool removed = false;

            if (TryRemoveCharacterFromEntries(_viewAllEntries, characterId, out LoginSelectWorldCharacterEntry removedViewAllEntry))
            {
                removed = true;
                removedEntry ??= removedViewAllEntry;
                removedWorldId = Math.Max(0, removedViewAllEntry?.WorldId ?? 0);
                ViewAllExpectedCharacterCount = Math.Max(0, _viewAllEntries.Count);
                ViewAllRemainingServerCount = 0;

                int slotCount = ResolveRosterSlotCount(ViewAllCharRosterProfile, SelectWorldRosterProfile);
                int buyCharacterCount = ResolveRosterBuyCharacterCount(ViewAllCharRosterProfile, SelectWorldRosterProfile);
                bool loginOpt = ViewAllCharRosterProfile?.LoginOpt ?? false;
                LoginSelectWorldResultProfile aggregatedProfile = CreateRosterProfile(
                    _viewAllEntries,
                    slotCount,
                    buyCharacterCount,
                    loginOpt);
                CacheViewAllWorldProfiles(aggregatedProfile);
            }

            if (TryRemoveCharacterFromProfileMap(_selectWorldProfilesByWorld, characterId, out LoginSelectWorldCharacterEntry removedSelectWorldEntry))
            {
                removed = true;
                removedEntry ??= removedSelectWorldEntry;
                if (removedWorldId <= 0)
                {
                    removedWorldId = Math.Max(0, removedSelectWorldEntry?.WorldId ?? 0);
                }
            }

            if (SelectWorldRosterProfile != null)
            {
                SelectWorldRosterProfile = RemoveCharacterFromProfile(SelectWorldRosterProfile, characterId, out _);
            }

            if (ViewAllCharRosterProfile != null && !_viewAllEntries.Any(entry => entry?.CharacterId == characterId))
            {
                ViewAllCharRosterProfile = RemoveCharacterFromProfile(ViewAllCharRosterProfile, characterId, out _);
            }

            if (!removed)
            {
                return false;
            }

            if (removedWorldId > 0 && ActiveWorldId == 0)
            {
                ActiveWorldId = removedWorldId;
            }

            SetActiveWorld(ActiveWorldId);
            return true;
        }

        private bool HasSelectWorldRoster =>
            SelectWorldRosterProfile != null &&
            LoginSelectWorldResultCodec.IsSuccessCode(SelectWorldRosterProfile.ResultCode);

        private bool HasViewAllRoster => ViewAllCharRosterProfile != null;

        private bool ReevaluateExtraCharacterEntitlement()
        {
            bool canHaveExtraCharacter =
                _lastExtraCharInfoResultProfile != null &&
                AuthenticatedAccountId.HasValue &&
                AuthenticatedAccountId.Value == _lastExtraCharInfoResultProfile.AccountId &&
                _lastExtraCharInfoResultProfile.CanHaveExtraCharacter;
            return SetExtraCharacterEntitlement(canHaveExtraCharacter);
        }

        private int NormalizeBuyCharacterCount(int buyCharacterCount)
        {
            if (!CanHaveExtraCharacter)
            {
                return 0;
            }

            return Math.Clamp(Math.Max(0, buyCharacterCount), 0, 1);
        }

        private LoginSelectWorldResultProfile NormalizeRosterEntitlement(LoginSelectWorldResultProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            return WithBuyCharacterCount(profile, NormalizeBuyCharacterCount(profile.BuyCharacterCount));
        }

        private static bool TryGetWorldRoster(
            IReadOnlyDictionary<int, LoginSelectWorldResultProfile> profilesByWorld,
            int worldId,
            out LoginSelectWorldResultProfile rosterProfile)
        {
            rosterProfile = null;
            if (profilesByWorld == null ||
                !profilesByWorld.TryGetValue(Math.Max(0, worldId), out LoginSelectWorldResultProfile storedProfile) ||
                storedProfile == null)
            {
                return false;
            }

            rosterProfile = CloneProfile(storedProfile);
            return true;
        }

        private static void AddProfiles(
            IDictionary<int, LoginSelectWorldResultProfile> destination,
            IReadOnlyDictionary<int, LoginSelectWorldResultProfile> source,
            bool requireSuccessResult,
            bool overwriteExisting)
        {
            if (destination == null || source == null)
            {
                return;
            }

            foreach ((int worldId, LoginSelectWorldResultProfile profile) in source)
            {
                if (profile == null)
                {
                    continue;
                }

                if (requireSuccessResult && !LoginSelectWorldResultCodec.IsSuccessCode(profile.ResultCode))
                {
                    continue;
                }

                if (!overwriteExisting && destination.ContainsKey(worldId))
                {
                    continue;
                }

                destination[worldId] = CloneProfile(profile);
            }
        }

        private static bool TryRemoveCharacterFromEntries(
            ICollection<LoginSelectWorldCharacterEntry> entries,
            int characterId,
            out LoginSelectWorldCharacterEntry removedEntry)
        {
            removedEntry = null;
            if (entries == null || characterId <= 0)
            {
                return false;
            }

            LoginSelectWorldCharacterEntry entry = entries.FirstOrDefault(candidate => candidate?.CharacterId == characterId);
            if (entry == null)
            {
                return false;
            }

            removedEntry = CloneEntry(entry);
            entries.Remove(entry);
            return true;
        }

        private static bool TryRemoveCharacterFromProfileMap(
            IDictionary<int, LoginSelectWorldResultProfile> profilesByWorld,
            int characterId,
            out LoginSelectWorldCharacterEntry removedEntry)
        {
            removedEntry = null;
            if (profilesByWorld == null || profilesByWorld.Count == 0 || characterId <= 0)
            {
                return false;
            }

            bool removed = false;
            foreach (int worldId in profilesByWorld.Keys.ToArray())
            {
                LoginSelectWorldResultProfile updatedProfile = RemoveCharacterFromProfile(
                    profilesByWorld[worldId],
                    characterId,
                    out LoginSelectWorldCharacterEntry removedProfileEntry);
                if (removedProfileEntry == null)
                {
                    continue;
                }

                profilesByWorld[worldId] = updatedProfile;
                removedEntry ??= removedProfileEntry;
                removed = true;
            }

            return removed;
        }

        private void CacheViewAllWorldProfiles(LoginSelectWorldResultProfile profile)
        {
            _viewAllProfilesByWorld.Clear();
            if (profile == null)
            {
                ViewAllCharRosterProfile = null;
                return;
            }

            if (profile.Entries == null || profile.Entries.Count == 0)
            {
                LoginSelectWorldResultProfile emptyProfile = CloneProfile(profile);
                _viewAllProfilesByWorld[ActiveWorldId] = emptyProfile;
                ViewAllCharRosterProfile = CloneProfile(emptyProfile);
                return;
            }

            foreach (IGrouping<int, LoginSelectWorldCharacterEntry> group in profile.Entries
                         .Where(entry => entry != null)
                         .GroupBy(entry => Math.Max(0, entry.WorldId ?? 0)))
            {
                LoginSelectWorldResultProfile worldProfile = CreateRosterProfile(
                    group,
                    profile.SlotCount,
                    profile.BuyCharacterCount,
                    profile.LoginOpt);
                _viewAllProfilesByWorld[group.Key] = worldProfile;
            }

            if (_viewAllProfilesByWorld.TryGetValue(ActiveWorldId, out LoginSelectWorldResultProfile activeWorldProfile))
            {
                ViewAllCharRosterProfile = CloneProfile(activeWorldProfile);
            }
            else
            {
                ViewAllCharRosterProfile = CloneProfile(profile);
            }
        }

        private static LoginSelectWorldResultProfile CreateWorldSpecificViewAllRoster(
            LoginSelectWorldResultProfile profile,
            int worldId)
        {
            if (profile?.Entries == null)
            {
                return null;
            }

            int normalizedWorldId = Math.Max(0, worldId);
            LoginSelectWorldCharacterEntry[] worldEntries = profile.Entries
                .Where(entry => entry != null && Math.Max(0, entry.WorldId ?? 0) == normalizedWorldId)
                .Select(CloneEntry)
                .ToArray();
            return new LoginSelectWorldResultProfile
            {
                ResultCode = profile.ResultCode,
                Entries = worldEntries,
                LoginOpt = profile.LoginOpt,
                SlotCount = Math.Max(0, profile.SlotCount),
                BuyCharacterCount = Math.Max(0, profile.BuyCharacterCount)
            };
        }

        private static LoginSelectWorldResultProfile RemoveCharacterFromProfile(
            LoginSelectWorldResultProfile profile,
            int characterId,
            out LoginSelectWorldCharacterEntry removedEntry)
        {
            removedEntry = null;
            if (profile?.Entries == null || characterId <= 0)
            {
                return profile;
            }

            List<LoginSelectWorldCharacterEntry> remainingEntries = new(profile.Entries.Count);
            foreach (LoginSelectWorldCharacterEntry entry in profile.Entries.Where(entry => entry != null))
            {
                if (removedEntry == null && entry.CharacterId == characterId)
                {
                    removedEntry = CloneEntry(entry);
                    continue;
                }

                remainingEntries.Add(CloneEntry(entry));
            }

            if (removedEntry == null)
            {
                return profile;
            }

            return new LoginSelectWorldResultProfile
            {
                ResultCode = profile.ResultCode,
                Entries = remainingEntries,
                LoginOpt = profile.LoginOpt,
                SlotCount = Math.Max(0, profile.SlotCount),
                BuyCharacterCount = Math.Max(0, profile.BuyCharacterCount)
            };
        }

        private static LoginSelectWorldResultProfile CreateRosterProfile(
            IEnumerable<LoginSelectWorldCharacterEntry> entries,
            int slotCount,
            int buyCharacterCount,
            bool loginOpt)
        {
            LoginSelectWorldCharacterEntry[] clonedEntries = entries?
                .Where(entry => entry != null)
                .Select(CloneEntry)
                .ToArray()
                ?? Array.Empty<LoginSelectWorldCharacterEntry>();

            return new LoginSelectWorldResultProfile
            {
                ResultCode = 0,
                Entries = clonedEntries,
                LoginOpt = loginOpt,
                SlotCount = Math.Max(0, slotCount),
                BuyCharacterCount = Math.Max(0, buyCharacterCount)
            };
        }

        private static LoginSelectWorldResultProfile CloneProfile(LoginSelectWorldResultProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            return new LoginSelectWorldResultProfile
            {
                ResultCode = profile.ResultCode,
                Entries = profile.Entries?
                    .Where(entry => entry != null)
                    .Select(CloneEntry)
                    .ToArray()
                    ?? Array.Empty<LoginSelectWorldCharacterEntry>(),
                LoginOpt = profile.LoginOpt,
                SlotCount = Math.Max(0, profile.SlotCount),
                BuyCharacterCount = Math.Max(0, profile.BuyCharacterCount)
            };
        }

        private static LoginSelectWorldResultProfile WithBuyCharacterCount(
            LoginSelectWorldResultProfile profile,
            int buyCharacterCount)
        {
            if (profile == null)
            {
                return null;
            }

            return new LoginSelectWorldResultProfile
            {
                ResultCode = profile.ResultCode,
                Entries = profile.Entries?
                    .Where(entry => entry != null)
                    .Select(CloneEntry)
                    .ToArray()
                    ?? Array.Empty<LoginSelectWorldCharacterEntry>(),
                LoginOpt = profile.LoginOpt,
                SlotCount = Math.Max(0, profile.SlotCount),
                BuyCharacterCount = Math.Max(0, buyCharacterCount)
            };
        }

        private static void UpdateProfileBuyCharacterCount(
            IDictionary<int, LoginSelectWorldResultProfile> profilesByWorld,
            int buyCharacterCount)
        {
            if (profilesByWorld == null || profilesByWorld.Count == 0)
            {
                return;
            }

            foreach (int worldId in profilesByWorld.Keys.ToArray())
            {
                profilesByWorld[worldId] = WithBuyCharacterCount(profilesByWorld[worldId], buyCharacterCount);
            }
        }

        private static int ResolveRosterSlotCount(
            LoginSelectWorldResultProfile primaryProfile,
            LoginSelectWorldResultProfile fallbackProfile)
        {
            return Math.Max(
                0,
                primaryProfile?.SlotCount
                ?? fallbackProfile?.SlotCount
                ?? 0);
        }

        private static int ResolveRosterBuyCharacterCount(
            LoginSelectWorldResultProfile primaryProfile,
            LoginSelectWorldResultProfile fallbackProfile)
        {
            return Math.Max(
                0,
                primaryProfile?.BuyCharacterCount
                ?? fallbackProfile?.BuyCharacterCount
                ?? 0);
        }

        private static LoginSelectWorldCharacterEntry CloneEntry(LoginSelectWorldCharacterEntry entry)
        {
            return new LoginSelectWorldCharacterEntry
            {
                CharacterId = entry.CharacterId,
                WorldId = entry.WorldId,
                Name = entry.Name ?? string.Empty,
                Gender = entry.Gender,
                Skin = entry.Skin,
                FaceId = entry.FaceId,
                HairId = entry.HairId,
                Level = entry.Level,
                JobId = entry.JobId,
                SubJob = entry.SubJob,
                Strength = entry.Strength,
                Dexterity = entry.Dexterity,
                Intelligence = entry.Intelligence,
                Luck = entry.Luck,
                AbilityPoints = entry.AbilityPoints,
                HitPoints = entry.HitPoints,
                MaxHitPoints = entry.MaxHitPoints,
                ManaPoints = entry.ManaPoints,
                MaxManaPoints = entry.MaxManaPoints,
                Experience = entry.Experience,
                Fame = entry.Fame,
                FieldMapId = entry.FieldMapId,
                Portal = entry.Portal,
                PlayTime = entry.PlayTime,
                OnFamily = entry.OnFamily,
                WorldRank = entry.WorldRank,
                WorldRankMove = entry.WorldRankMove,
                JobRank = entry.JobRank,
                JobRankMove = entry.JobRankMove,
                AvatarLook = entry.AvatarLook,
                AvatarLookPacket = entry.AvatarLookPacket != null
                    ? (byte[])entry.AvatarLookPacket.Clone()
                    : Array.Empty<byte>()
            };
        }

        private static LoginExtraCharInfoResultProfile CloneExtraCharInfoProfile(LoginExtraCharInfoResultProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            return new LoginExtraCharInfoResultProfile
            {
                AccountId = profile.AccountId,
                ResultFlag = profile.ResultFlag,
                CanHaveExtraCharacter = profile.CanHaveExtraCharacter
            };
        }
    }
}
