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

        public int? AuthenticatedAccountId { get; private set; }
        public int ActiveWorldId { get; private set; }
        public LoginSelectWorldResultProfile SelectWorldRosterProfile { get; private set; }
        public LoginSelectWorldResultProfile ViewAllCharRosterProfile { get; private set; }
        public int ViewAllExpectedCharacterCount { get; private set; }
        public int ViewAllRemainingServerCount { get; private set; }

        public void Reset()
        {
            AuthenticatedAccountId = null;
            ClearRosterProfiles();
        }

        public void ClearRosterProfiles()
        {
            ActiveWorldId = 0;
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
            return changed;
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
            SelectWorldRosterProfile = CloneProfile(profile);

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

        public void ApplyViewAllCharResult(LoginViewAllCharResultPacketProfile profile, bool canHaveExtraCharacter)
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
                            canHaveExtraCharacter ? 1 : 0,
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
                            canHaveExtraCharacter ? 1 : 0,
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
            LoginSelectWorldResultProfile resolvedProfile = CreateRosterProfile(entries, slotCount, buyCharacterCount, selectWorldLoginOpt);

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

                ViewAllCharRosterProfile = CreateRosterProfile(_viewAllEntries, slotCount, buyCharacterCount, viewAllLoginOpt);
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
                ViewAllCharRosterProfile?.Entries?.Count > 0)
            {
                rosterProfile = ViewAllCharRosterProfile;
                packetSource = "ViewAllCharResult";
                return true;
            }

            if (hasSelectWorldPacket &&
                SelectWorldRosterProfile != null &&
                LoginSelectWorldResultCodec.IsSuccessCode(SelectWorldRosterProfile.ResultCode) &&
                SelectWorldRosterProfile.Entries.Count > 0)
            {
                rosterProfile = SelectWorldRosterProfile;
                packetSource = "SelectWorldResult";
                return true;
            }

            if (ViewAllCharRosterProfile?.Entries?.Count > 0)
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

        private bool HasSelectWorldRoster =>
            SelectWorldRosterProfile != null &&
            LoginSelectWorldResultCodec.IsSuccessCode(SelectWorldRosterProfile.ResultCode);

        private bool HasViewAllRoster => ViewAllCharRosterProfile?.Entries?.Count > 0;

        private static bool TryGetWorldRoster(
            IReadOnlyDictionary<int, LoginSelectWorldResultProfile> profilesByWorld,
            int worldId,
            out LoginSelectWorldResultProfile rosterProfile)
        {
            rosterProfile = null;
            if (profilesByWorld == null ||
                !profilesByWorld.TryGetValue(Math.Max(0, worldId), out LoginSelectWorldResultProfile storedProfile) ||
                storedProfile?.Entries?.Count <= 0)
            {
                return false;
            }

            rosterProfile = CloneProfile(storedProfile);
            return true;
        }

        private void CacheViewAllWorldProfiles(LoginSelectWorldResultProfile profile)
        {
            _viewAllProfilesByWorld.Clear();
            if (profile?.Entries == null || profile.Entries.Count == 0)
            {
                ViewAllCharRosterProfile = null;
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
            if (worldEntries.Length == 0)
            {
                return null;
            }

            return new LoginSelectWorldResultProfile
            {
                ResultCode = profile.ResultCode,
                Entries = worldEntries,
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
    }
}
