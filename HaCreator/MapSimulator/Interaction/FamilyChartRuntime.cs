using HaCreator.MapSimulator.Character;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class FamilyChartRuntime
    {
        private const int LocalPlayerId = 120;
        private const int DefaultFamilyHeadId = 100;
        private const int DirectJuniorSlotLeft = 5;
        private const int DirectJuniorSlotRight = 6;
        private const int GrandchildSlotStart = 7;
        private const int EntitlementDurationMs = 15 * 60 * 1000;
        private const int ClientFamilyTreeTitleStringId = 4610;
        private const int ClientFamilyTreeNoSelectionStringId = 4608;
        private const int ClientFamilyTreeJuniorCountStringId = 4611;
        private const int ClientFamilyTreeGrandchildCountStringId = 4612;
        private const int ClientFamilyTreeEmptyBranchStringId = 0x11FD;
        private const int ClientFamilyTreeJuniorEntryStringId = 0x1201;

        private readonly Dictionary<int, FamilyMemberState> _members = new();
        private readonly Queue<FamilyRecruitSeed> _juniorSeeds = new();
        private readonly List<string> _precepts = new()
        {
            "Travel as one family and always answer a summon.",
            "Train juniors before chasing new privileges.",
            "Keep one safe town registered before field testing.",
            "Donate reputation before claiming the weekly special."
        };

        private int _selectedMemberId = LocalPlayerId;
        private int _familyHeadId = DefaultFamilyHeadId;
        private int _preceptIndex;
        private FamilyEntitlementType _entitlementType = FamilyEntitlementType.DropAndExpBuff;
        private int _entitlementUsesLeft = 3;
        private string _locationSummary = "Maple Island";
        private FamilyPrivilegeState _activePrivilege;

        private int FamilyHeadId => _familyHeadId;

        internal FamilyChartRuntime()
        {
            SeedDefaultFamily();
        }

        internal void UpdateLocalContext(CharacterBuild build, string locationSummary, int channel)
        {
            _locationSummary = string.IsNullOrWhiteSpace(locationSummary) ? "Field" : locationSummary.Trim();
            FamilyMemberState localPlayer = GetMember(LocalPlayerId);
            if (localPlayer == null)
            {
                return;
            }

            localPlayer.Name = string.IsNullOrWhiteSpace(build?.Name) ? "Player" : build.Name.Trim();
            localPlayer.JobName = string.IsNullOrWhiteSpace(build?.JobName) ? "Beginner" : build.JobName.Trim();
            localPlayer.Level = Math.Max(1, build?.Level ?? 1);
            localPlayer.LocationSummary = $"{_locationSummary}  CH {Math.Max(1, channel)}";
            localPlayer.CurrentReputation = Math.Max(120, (localPlayer.Level * 18) + (CountDescendants(localPlayer.Id) * 25));
            localPlayer.TodayReputation = Math.Max(25, localPlayer.Level / 2);
            localPlayer.IsOnline = true;

            if (!_members.ContainsKey(_selectedMemberId))
            {
                _selectedMemberId = LocalPlayerId;
            }
        }

        internal FamilyChartSnapshot BuildChartSnapshot()
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            IReadOnlyList<int> focusOrder = BuildFocusOrder();
            int focusIndex = GetFocusIndex(focusOrder, selectedMember?.Id ?? LocalPlayerId);
            FamilyPrivilegeState activePrivilege = GetActivePrivilege(Environment.TickCount);

            return new FamilyChartSnapshot
            {
                SelectedMemberId = selectedMember?.Id ?? LocalPlayerId,
                SelectedMemberName = selectedMember?.Name ?? "Player",
                SelectedRank = GetRankLabel(selectedMember),
                LocationSummary = selectedMember?.LocationSummary ?? _locationSummary,
                TotalMembers = _members.Count,
                JuniorCount = GetStatisticValue(selectedMember),
                CurrentReputation = selectedMember?.CurrentReputation ?? 0,
                TodayReputation = selectedMember?.TodayReputation ?? 0,
                SpecialReputationCost = GetSpecialReputationCost(selectedMember),
                SpecialUsesLeft = _entitlementUsesLeft,
                Precept = _precepts[_preceptIndex],
                EntitlementLabel = GetEntitlementLabel(_entitlementType),
                EntitlementIndex = (int)_entitlementType,
                DetailLines = BuildDetailLines(selectedMember, activePrivilege),
                CanPageBackward = focusIndex > 0,
                CanPageForward = focusIndex < focusOrder.Count - 1,
                CanAddJunior = CanAddJunior(selectedMember),
                CanUseSpecial = CanExecuteEntitlement(selectedMember),
                Page = focusIndex + 1,
                TotalPages = Math.Max(1, focusOrder.Count)
            };
        }

        internal FamilyTreeSnapshot BuildTreeSnapshot()
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            IReadOnlyList<int> focusOrder = BuildFocusOrder();
            int focusIndex = GetFocusIndex(focusOrder, selectedMember?.Id ?? LocalPlayerId);
            Dictionary<int, int> slotMembers = BuildTreeLayout(selectedMember);

            FamilyTreeNodeSnapshot[] nodes = Enumerable.Range(0, 11)
                .Select(slotIndex => CreateNodeSnapshot(slotIndex, slotMembers))
                .ToArray();

            return new FamilyTreeSnapshot
            {
                Nodes = nodes,
                TotalMembers = _members.Count,
                FocusName = selectedMember?.Name ?? "Player",
                TitleText = BuildTreeTitle(selectedMember),
                JuniorCountText = BuildJuniorCountText(selectedMember),
                SummaryLines = BuildTreeSummaryLines(selectedMember),
                CanPageBackward = focusIndex > 0,
                CanPageForward = focusIndex < focusOrder.Count - 1,
                CanAddJunior = CanAddJunior(selectedMember),
                CanRemoveSelected = CanRemoveSelected(selectedMember),
                Page = focusIndex + 1,
                TotalPages = Math.Max(1, focusOrder.Count)
            };
        }

        internal IReadOnlyList<FamilyTrackedMemberSnapshot> BuildTrackedMembersSnapshot()
        {
            return _members.Values
                .Select(member => new FamilyTrackedMemberSnapshot
                {
                    Name = member.Name,
                    LocationSummary = member.LocationSummary,
                    IsOnline = member.IsOnline,
                    SimulatedPosition = member.SimulatedPosition,
                    IsLocalPlayer = member.Id == LocalPlayerId
                })
                .ToArray();
        }

        internal string MoveFocus(int delta)
        {
            IReadOnlyList<int> focusOrder = BuildFocusOrder();
            if (focusOrder.Count == 0)
            {
                return "Family chart has no selectable members.";
            }

            int currentIndex = GetFocusIndex(focusOrder, _selectedMemberId);
            int nextIndex = Math.Clamp(currentIndex + delta, 0, focusOrder.Count - 1);
            _selectedMemberId = focusOrder[nextIndex];

            FamilyMemberState selectedMember = GetSelectedMember();
            return $"Focused family branch on {selectedMember?.Name ?? "Player"}.";
        }

        internal string SelectMemberById(int memberId)
        {
            if (!_members.ContainsKey(memberId))
            {
                return $"Family member id {memberId} is not present in the current simulator roster.";
            }

            _selectedMemberId = memberId;
            FamilyMemberState selectedMember = GetSelectedMember();
            return $"Selected {selectedMember?.Name ?? "family member"} in the family tree.";
        }

        internal string SelectNode(int slotIndex)
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            if (!BuildTreeLayout(selectedMember).TryGetValue(slotIndex, out int memberId) || !_members.ContainsKey(memberId))
            {
                return slotIndex is DirectJuniorSlotLeft or DirectJuniorSlotRight
                    ? "That junior-entry slot is still empty."
                    : "That family-tree slot is empty.";
            }

            _selectedMemberId = memberId;
            FamilyMemberState member = GetSelectedMember();
            return $"Selected {member?.Name ?? "family member"} in the family tree.";
        }

        internal string AddJunior()
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            if (!CanAddJunior(selectedMember))
            {
                return selectedMember == null
                    ? "Family chart has no selected member."
                    : $"{selectedMember.Name} cannot register another junior in the simulator roster.";
            }

            FamilyRecruitSeed recruit = _juniorSeeds.Count > 0
                ? _juniorSeeds.Dequeue()
                : new FamilyRecruitSeed($"Junior {_members.Count + 1}", "Adventurer", 18 + _members.Count, "Family Hall", true, new Vector2(180f + (_members.Count * 12f), -30f));
            int nextId = _members.Keys.Max() + 1;

            FamilyMemberState newJunior = new(
                nextId,
                recruit.Name,
                recruit.JobName,
                recruit.Level,
                recruit.LocationSummary,
                selectedMember.Id,
                65 + (selectedMember.Children.Count * 15),
                12 + selectedMember.Children.Count,
                recruit.IsOnline,
                recruit.SimulatedPosition);
            _members[newJunior.Id] = newJunior;
            selectedMember.Children.Add(newJunior.Id);
            selectedMember.CurrentReputation += 30;
            selectedMember.TodayReputation += 8;
            _selectedMemberId = newJunior.Id;

            return $"{newJunior.Name} was registered under {selectedMember.Name}.";
        }

        internal string RemoveSelectedMember()
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            if (!CanRemoveSelected(selectedMember))
            {
                return selectedMember == null
                    ? "Family chart has no selected member."
                    : $"{selectedMember.Name} cannot be removed from the simulator family tree.";
            }

            List<int> branchMembers = new();
            CollectBranchMemberIds(selectedMember.Id, branchMembers);
            foreach (int memberId in branchMembers.OrderByDescending(GetDepth))
            {
                if (_members.TryGetValue(memberId, out FamilyMemberState member))
                {
                    if (member.ParentId.HasValue && _members.TryGetValue(member.ParentId.Value, out FamilyMemberState parent))
                    {
                        parent.Children.Remove(memberId);
                    }

                    _members.Remove(memberId);
                }
            }

            _selectedMemberId = LocalPlayerId;
            return $"Removed {selectedMember.Name}'s simulator family branch.";
        }

        internal string CyclePrecept()
        {
            _preceptIndex = (_preceptIndex + 1) % _precepts.Count;
            return $"Family precept updated to: {_precepts[_preceptIndex]}";
        }

        internal string CycleEntitlement()
        {
            _entitlementType = (FamilyEntitlementType)(((int)_entitlementType + 1) % 5);
            return $"Family entitlement switched to {GetEntitlementLabel(_entitlementType)}.";
        }

        internal FamilyEntitlementUseResult ExecuteSelectedEntitlement(int currentTick, Vector2 localPlayerPosition)
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            if (!CanExecuteEntitlement(selectedMember))
            {
                return new FamilyEntitlementUseResult("That family entitlement cannot be used right now.");
            }

            FamilyMemberState localPlayer = GetMember(LocalPlayerId);
            localPlayer ??= selectedMember;
            string localLocation = localPlayer?.LocationSummary ?? _locationSummary;
            string selectedLocation = selectedMember?.LocationSummary ?? localLocation;

            _entitlementUsesLeft = Math.Max(0, _entitlementUsesLeft - 1);

            switch (_entitlementType)
            {
                case FamilyEntitlementType.MoveToMember:
                {
                    if (selectedMember == null || selectedMember.Id == LocalPlayerId)
                    {
                        return new FamilyEntitlementUseResult("Select another online family member before moving.");
                    }

                    if (!selectedMember.IsOnline)
                    {
                        return new FamilyEntitlementUseResult($"{selectedMember.Name} is offline, so the move entitlement cannot resolve.");
                    }

                    bool sameField = IsSameField(localLocation, selectedLocation);
                    if (!sameField)
                    {
                        return new FamilyEntitlementUseResult($"Prepared a move request to {selectedMember.Name}, but cross-map transfer still remains outside the simulator field seam.");
                    }

                    return new FamilyEntitlementUseResult(
                        $"Moved to {selectedMember.Name}'s family-chart branch in {selectedLocation}.",
                        RequestTeleport: true,
                        TeleportPosition: selectedMember.SimulatedPosition);
                }
                case FamilyEntitlementType.SummonMember:
                {
                    if (selectedMember == null || selectedMember.Id == LocalPlayerId)
                    {
                        return new FamilyEntitlementUseResult("Select another family member before using the summon entitlement.");
                    }

                    selectedMember.LocationSummary = localLocation;
                    selectedMember.IsOnline = true;
                    selectedMember.SimulatedPosition = new Vector2(localPlayerPosition.X + 64f, localPlayerPosition.Y);
                    return new FamilyEntitlementUseResult($"Summoned {selectedMember.Name} to {localLocation}.");
                }
                case FamilyEntitlementType.DropBuff:
                case FamilyEntitlementType.ExpBuff:
                case FamilyEntitlementType.DropAndExpBuff:
                    _activePrivilege = new FamilyPrivilegeState(_entitlementType, currentTick + EntitlementDurationMs);
                    return new FamilyEntitlementUseResult($"Applied {GetEntitlementLabel(_entitlementType)} for the current simulator family session.");
                default:
                    return new FamilyEntitlementUseResult("That family entitlement is not modeled yet.");
            }
        }

        internal void SetSelectedEntitlement(FamilyEntitlementType entitlementType)
        {
            _entitlementType = entitlementType;
        }

        internal string DescribeStatus()
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            FamilyMemberState head = GetMember(_familyHeadId);
            string headName = head?.Name ?? "(missing)";
            string selectedName = selectedMember?.Name ?? "(none)";
            return $"Family roster: {_members.Count} members, head {headName} (#{_familyHeadId}), selected {selectedName} (#{selectedMember?.Id ?? 0}), entitlement {_entitlementUsesLeft} use(s) left on {GetEntitlementLabel(_entitlementType)}.";
        }

        internal string ResetToSeedFamily()
        {
            ResetRuntimeState();
            SeedDefaultFamily();
            return "Restored the seeded simulator family roster.";
        }

        internal string ClearRosterFromPacket()
        {
            ResetRuntimeState();
            return "Cleared the simulator family roster. Sync packet members to rebuild it.";
        }

        internal string RemoveMemberFromPacket(int memberId)
        {
            if (!_members.TryGetValue(memberId, out FamilyMemberState selectedMember))
            {
                return $"Family member id {memberId} is not present in the current simulator roster.";
            }

            List<int> branchMembers = new();
            CollectBranchMemberIds(memberId, branchMembers);
            foreach (int branchMemberId in branchMembers.OrderByDescending(GetDepth))
            {
                if (!_members.TryGetValue(branchMemberId, out FamilyMemberState member))
                {
                    continue;
                }

                DetachFromParent(member);
                _members.Remove(branchMemberId);
            }

            NormalizeRosterState();
            return $"Removed synced family branch rooted at {selectedMember.Name} (#{memberId}).";
        }

        internal string UpsertMemberFromPacket(
            int memberId,
            int? parentId,
            string name,
            string jobName,
            int level,
            string locationSummary,
            bool isOnline,
            int currentReputation,
            int todayReputation)
        {
            if (memberId <= 0)
            {
                return "Packet family sync requires a positive member id.";
            }

            if (parentId == memberId)
            {
                return $"Family member #{memberId} cannot parent itself.";
            }

            if (parentId.HasValue && !_members.ContainsKey(parentId.Value))
            {
                return $"Add parent #{parentId.Value} before syncing child #{memberId}.";
            }

            bool isHead = !parentId.HasValue;
            FamilyMemberState member = GetMember(memberId);
            int? previousParentId = member?.ParentId;
            if (member == null)
            {
                member = new FamilyMemberState(
                    memberId,
                    string.IsNullOrWhiteSpace(name) ? $"Member {memberId}" : name.Trim(),
                    string.IsNullOrWhiteSpace(jobName) ? "Adventurer" : jobName.Trim(),
                    Math.Max(1, level),
                    string.IsNullOrWhiteSpace(locationSummary) ? "Family Hall" : locationSummary.Trim(),
                    parentId,
                    Math.Max(0, currentReputation),
                    Math.Max(0, todayReputation),
                    isOnline,
                    Vector2.Zero);
                _members[memberId] = member;
            }
            else
            {
                member.Name = string.IsNullOrWhiteSpace(name) ? member.Name : name.Trim();
                member.JobName = string.IsNullOrWhiteSpace(jobName) ? member.JobName : jobName.Trim();
                member.Level = Math.Max(1, level);
                member.LocationSummary = string.IsNullOrWhiteSpace(locationSummary) ? member.LocationSummary : locationSummary.Trim();
                member.ParentId = parentId;
                member.CurrentReputation = Math.Max(0, currentReputation);
                member.TodayReputation = Math.Max(0, todayReputation);
                member.IsOnline = isOnline;
            }

            if (previousParentId != parentId)
            {
                DetachFromParent(memberId, previousParentId);
            }

            if (parentId.HasValue && _members.TryGetValue(parentId.Value, out FamilyMemberState parent)
                && !parent.Children.Contains(memberId))
            {
                parent.Children.Add(memberId);
            }

            if (isHead)
            {
                _familyHeadId = memberId;
            }

            NormalizeRosterState();
            return $"Synced family member #{memberId} {member.Name} under {(parentId.HasValue ? $"#{parentId.Value}" : "the family head root")}.";
        }

        private void ResetRuntimeState()
        {
            _members.Clear();
            _juniorSeeds.Clear();
            _selectedMemberId = LocalPlayerId;
            _familyHeadId = DefaultFamilyHeadId;
            _preceptIndex = 0;
            _entitlementType = FamilyEntitlementType.DropAndExpBuff;
            _entitlementUsesLeft = 3;
            _activePrivilege = null;
        }

        private void SeedDefaultFamily()
        {
            AddMember(new FamilyMemberState(100, "Ephenia", "Bishop", 126, "Orbis  CH 8", null, 540, 42, true, new Vector2(260f, -20f)));
            AddMember(new FamilyMemberState(110, "Cassia", "Paladin", 94, "Leafre  CH 6", 100, 360, 21, true, new Vector2(180f, 10f)));
            AddMember(new FamilyMemberState(111, "Rowan", "Ranger", 90, "Mu Lung  CH 4", 100, 288, 16, false, new Vector2(340f, 10f)));
            AddMember(new FamilyMemberState(LocalPlayerId, "Player", "Beginner", 30, "Maple Island  CH 1", 110, 198, 15, true, new Vector2(205f, 70f)));
            AddMember(new FamilyMemberState(121, "Targa", "F/P Mage", 52, "Ludibrium  CH 11", 110, 140, 7, true, new Vector2(348f, 70f)));
            AddMember(new FamilyMemberState(130, "Puck", "Assassin", 31, "Kerning City  CH 3", LocalPlayerId, 82, 6, true, new Vector2(92f, 128f)));
            AddMember(new FamilyMemberState(131, "Nina", "Cleric", 29, "Henesys  CH 2", LocalPlayerId, 78, 5, true, new Vector2(330f, 128f)));
            AddMember(new FamilyMemberState(150, "Basil", "Page", 18, "Ellinia  CH 5", 130, 40, 3, true, new Vector2(61f, 182f)));
            AddMember(new FamilyMemberState(151, "Rhea", "Hunter", 17, "Sleepywood  CH 9", 130, 36, 2, false, new Vector2(211f, 182f)));
            AddMember(new FamilyMemberState(152, "Dory", "Spearman", 19, "Perion  CH 10", 131, 42, 4, true, new Vector2(342f, 182f)));
            AddMember(new FamilyMemberState(153, "Seth", "Wizard", 16, "Kerning Square  CH 7", 131, 32, 2, true, new Vector2(490f, 182f)));

            _juniorSeeds.Enqueue(new FamilyRecruitSeed("Mira", "Bandit", 27, "Nautilus  CH 13", true, new Vector2(330f, 128f)));
            _juniorSeeds.Enqueue(new FamilyRecruitSeed("Vale", "Priest", 41, "Ariant  CH 2", true, new Vector2(92f, 128f)));
            _juniorSeeds.Enqueue(new FamilyRecruitSeed("Iris", "Brawler", 38, "Singapore  CH 1", false, new Vector2(211f, 182f)));
        }

        private void AddMember(FamilyMemberState member)
        {
            _members[member.Id] = member;
            if (!member.ParentId.HasValue)
            {
                _familyHeadId = member.Id;
            }

            if (member.ParentId.HasValue && _members.TryGetValue(member.ParentId.Value, out FamilyMemberState parent))
            {
                if (!parent.Children.Contains(member.Id))
                {
                    parent.Children.Add(member.Id);
                }
            }
        }

        private FamilyMemberState GetSelectedMember()
        {
            return GetMember(_selectedMemberId) ?? GetMember(LocalPlayerId) ?? GetMember(FamilyHeadId);
        }

        private FamilyMemberState GetMember(int id)
        {
            return _members.TryGetValue(id, out FamilyMemberState member) ? member : null;
        }

        private IReadOnlyList<int> BuildFocusOrder()
        {
            Queue<int> pending = new();
            List<int> ordered = new();
            if (_members.ContainsKey(_familyHeadId))
            {
                pending.Enqueue(_familyHeadId);
            }

            while (pending.Count > 0)
            {
                int memberId = pending.Dequeue();
                if (!_members.ContainsKey(memberId))
                {
                    continue;
                }

                ordered.Add(memberId);
                foreach (int childId in GetChildren(memberId))
                {
                    pending.Enqueue(childId);
                }
            }

            return ordered;
        }

        private static int GetFocusIndex(IReadOnlyList<int> focusOrder, int memberId)
        {
            for (int i = 0; i < focusOrder.Count; i++)
            {
                if (focusOrder[i] == memberId)
                {
                    return i;
                }
            }

            return 0;
        }

        private Dictionary<int, int> BuildTreeLayout(FamilyMemberState focus)
        {
            Dictionary<int, int> layout = new();
            FamilyMemberState head = GetMember(_familyHeadId);
            if (head != null)
            {
                layout[0] = head.Id;
            }

            if (focus == null)
            {
                return layout;
            }

            FamilyMemberState topBranchSibling = GetChildren(_familyHeadId)
                .Select(GetMember)
                .FirstOrDefault(member => member != null && member.Id != GetTopPathChildId(focus.Id));
            if (topBranchSibling != null)
            {
                layout[1] = topBranchSibling.Id;
            }

            FamilyMemberState parent = focus.ParentId.HasValue ? GetMember(focus.ParentId.Value) : null;
            if (parent != null)
            {
                layout[2] = parent.Id;
            }

            layout[3] = focus.Id;

            FamilyMemberState sibling = parent == null
                ? GetChildren(_familyHeadId).Select(GetMember).FirstOrDefault(member => member != null && member.Id != focus.Id)
                : GetChildren(parent.Id).Select(GetMember).FirstOrDefault(member => member != null && member.Id != focus.Id);
            if (sibling != null)
            {
                layout[4] = sibling.Id;
            }

            List<FamilyMemberState> children = GetChildren(focus.Id).Select(GetMember).Where(member => member != null).ToList();
            if (children.Count > 0)
            {
                layout[5] = children[0].Id;
            }

            if (children.Count > 1)
            {
                layout[6] = children[1].Id;
            }

            LayoutGrandchildren(layout, children.ElementAtOrDefault(0), 7, 8);
            LayoutGrandchildren(layout, children.ElementAtOrDefault(1), 9, 10);

            return layout;
        }

        private void LayoutGrandchildren(Dictionary<int, int> layout, FamilyMemberState member, int leftSlot, int rightSlot)
        {
            if (member == null)
            {
                return;
            }

            List<FamilyMemberState> grandchildren = GetChildren(member.Id).Select(GetMember).Where(child => child != null).ToList();
            if (grandchildren.Count > 0)
            {
                layout[leftSlot] = grandchildren[0].Id;
            }

            if (grandchildren.Count > 1)
            {
                layout[rightSlot] = grandchildren[1].Id;
            }
        }

        private FamilyTreeNodeSnapshot CreateNodeSnapshot(int slotIndex, Dictionary<int, int> slotMembers)
        {
            if (!slotMembers.TryGetValue(slotIndex, out int memberId) || !_members.TryGetValue(memberId, out FamilyMemberState member))
            {
                return new FamilyTreeNodeSnapshot
                {
                    SlotIndex = slotIndex,
                    PlaceholderText = slotIndex is DirectJuniorSlotLeft or DirectJuniorSlotRight
                        ? GetClientPlaceholderText(slotIndex)
                        : slotIndex == 3
                            ? string.Empty
                            : GetClientPlaceholderText(slotIndex)
                };
            }

            return new FamilyTreeNodeSnapshot
            {
                SlotIndex = slotIndex,
                MemberId = member.Id,
                Name = member.Name,
                Rank = GetRankLabel(member),
                Detail = $"Lv.{member.Level} {member.JobName}",
                StatisticText = slotIndex >= GrandchildSlotStart ? GetStatisticValue(member).ToString() : string.Empty,
                IsLeader = member.Id == _familyHeadId,
                IsLocalPlayer = member.Id == LocalPlayerId,
                IsSelected = member.Id == _selectedMemberId,
                IsOnline = member.IsOnline,
                UseAlertNameColor = ShouldUseAlertNameColor(slotIndex, member)
            };
        }

        private IReadOnlyList<int> GetChildren(int memberId)
        {
            FamilyMemberState member = GetMember(memberId);
            return member != null ? member.Children : Array.Empty<int>();
        }

        private int GetTopPathChildId(int memberId)
        {
            FamilyMemberState current = GetMember(memberId);
            FamilyMemberState previous = current;
            while (current?.ParentId.HasValue == true)
            {
                previous = current;
                current = GetMember(current.ParentId.Value);
                if (current?.Id == _familyHeadId)
                {
                    return previous.Id;
                }
            }

            return memberId;
        }

        private IReadOnlyList<string> BuildDetailLines(FamilyMemberState selectedMember, FamilyPrivilegeState activePrivilege)
        {
            if (selectedMember == null)
            {
                return Array.Empty<string>();
            }

            List<string> lines = new()
            {
                $"{selectedMember.Name} is the {GetRankLabel(selectedMember).ToLowerInvariant()} branch focus.",
                $"{selectedMember.Level} {selectedMember.JobName} at {selectedMember.LocationSummary}.",
                $"{GetStatisticValue(selectedMember)} direct juniors, {CountDescendants(selectedMember.Id)} total descendants."
            };

            if (activePrivilege != null)
            {
                TimeSpan remaining = TimeSpan.FromMilliseconds(Math.Max(0, activePrivilege.ExpiresAtTick - Environment.TickCount));
                lines.Add($"{GetEntitlementLabel(activePrivilege.Type)} active for {Math.Max(0, remaining.Minutes):00}:{Math.Max(0, remaining.Seconds):00}.");
            }

            return lines;
        }

        private IReadOnlyList<string> BuildTreeSummaryLines(FamilyMemberState selectedMember)
        {
            if (selectedMember == null)
            {
                return Array.Empty<string>();
            }

            return new[]
            {
                $"{selectedMember.Name}  {GetRankLabel(selectedMember)}",
                $"{selectedMember.LocationSummary}",
                $"{GetStatisticValue(selectedMember)} direct juniors, {CountDescendants(selectedMember.Id)} total descendants"
            };
        }

        private bool CanAddJunior(FamilyMemberState selectedMember)
        {
            return selectedMember != null && selectedMember.Children.Count < 2;
        }

        private bool CanRemoveSelected(FamilyMemberState selectedMember)
        {
            if (selectedMember == null || selectedMember.Id == FamilyHeadId || selectedMember.Id == LocalPlayerId)
            {
                return false;
            }

            List<int> branchMembers = new();
            CollectBranchMemberIds(selectedMember.Id, branchMembers);
            return !branchMembers.Contains(LocalPlayerId);
        }

        private bool CanExecuteEntitlement(FamilyMemberState selectedMember)
        {
            return _entitlementUsesLeft > 0
                && selectedMember != null
                && (selectedMember.Id == LocalPlayerId || selectedMember.IsOnline);
        }

        private void CollectBranchMemberIds(int memberId, List<int> results)
        {
            results.Add(memberId);
            foreach (int childId in GetChildren(memberId))
            {
                CollectBranchMemberIds(childId, results);
            }
        }

        private int GetSpecialReputationCost(FamilyMemberState selectedMember)
        {
            int baseCost = _entitlementType switch
            {
                FamilyEntitlementType.MoveToMember => 300,
                FamilyEntitlementType.SummonMember => 500,
                FamilyEntitlementType.DropBuff => 700,
                FamilyEntitlementType.ExpBuff => 700,
                _ => 900
            };

            return Math.Max(baseCost, baseCost + (GetStatisticValue(selectedMember) * 25));
        }

        private int CountDescendants(int memberId)
        {
            int count = 0;
            foreach (int childId in GetChildren(memberId))
            {
                count += 1 + CountDescendants(childId);
            }

            return count;
        }

        private int GetDepth(int memberId)
        {
            int depth = 0;
            FamilyMemberState member = GetMember(memberId);
            while (member?.ParentId.HasValue == true)
            {
                depth++;
                member = GetMember(member.ParentId.Value);
            }

            return depth;
        }

        private int GetStatisticValue(FamilyMemberState member)
        {
            if (member == null)
            {
                return _members.Count;
            }

            return member.Id == _familyHeadId
                ? _members.Count - 1
                : member.Children.Count;
        }

        private bool ShouldUseAlertNameColor(int slotIndex, FamilyMemberState member)
        {
            if (slotIndex < DirectJuniorSlotLeft || member?.ParentId is null)
            {
                return false;
            }

            FamilyMemberState parent = GetMember(member.ParentId.Value);
            return parent != null && member.Level > parent.Level;
        }

        private FamilyPrivilegeState GetActivePrivilege(int currentTick)
        {
            if (_activePrivilege != null && currentTick >= _activePrivilege.ExpiresAtTick)
            {
                _activePrivilege = null;
            }

            return _activePrivilege;
        }

        private string GetRankLabel(FamilyMemberState member)
        {
            if (member == null)
            {
                return "Member";
            }

            if (member.Id == _familyHeadId)
            {
                return "Leader";
            }

            return member.Children.Count > 0 ? "Senior" : "Junior";
        }

        private string BuildTreeTitle(FamilyMemberState selectedMember)
        {
            return selectedMember == null
                ? "Family Tree"
                : $"{selectedMember.Name}'s Family Tree";
        }

        private string BuildJuniorCountText(FamilyMemberState selectedMember)
        {
            int juniorCount = Math.Max(0, GetStatisticValue(selectedMember));
            return $"{juniorCount} junior member(s)";
        }

        private string GetClientPlaceholderText(int slotIndex)
        {
            return slotIndex is DirectJuniorSlotLeft or DirectJuniorSlotRight
                ? "Junior Entry"
                : "Empty Branch";
        }

        private void NormalizeRosterState()
        {
            if (_members.Count == 0)
            {
                _selectedMemberId = LocalPlayerId;
                _familyHeadId = DefaultFamilyHeadId;
                return;
            }

            if (!_members.ContainsKey(_familyHeadId))
            {
                FamilyMemberState head = _members.Values
                    .FirstOrDefault(member => !member.ParentId.HasValue)
                    ?? _members.Values.OrderBy(member => member.Id).First();
                _familyHeadId = head.Id;
                head.ParentId = null;
            }

            foreach (FamilyMemberState member in _members.Values)
            {
                member.Children.RemoveAll(childId => !_members.ContainsKey(childId));
            }

            if (!_members.ContainsKey(_selectedMemberId))
            {
                _selectedMemberId = _members.ContainsKey(LocalPlayerId) ? LocalPlayerId : _familyHeadId;
            }
        }

        private void DetachFromParent(FamilyMemberState member)
        {
            DetachFromParent(member?.Id ?? 0, member?.ParentId);
        }

        private void DetachFromParent(int memberId, int? parentId)
        {
            if (!parentId.HasValue || !_members.TryGetValue(parentId.Value, out FamilyMemberState parent))
            {
                return;
            }

            parent.Children.Remove(memberId);
        }

        private static bool IsSameField(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            int leftChannelSeparator = left.IndexOf("  CH ", StringComparison.OrdinalIgnoreCase);
            int rightChannelSeparator = right.IndexOf("  CH ", StringComparison.OrdinalIgnoreCase);
            string leftField = leftChannelSeparator >= 0 ? left[..leftChannelSeparator] : left;
            string rightField = rightChannelSeparator >= 0 ? right[..rightChannelSeparator] : right;
            return string.Equals(leftField.Trim(), rightField.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetEntitlementLabel(FamilyEntitlementType entitlementType)
        {
            return entitlementType switch
            {
                FamilyEntitlementType.MoveToMember => "Move to the location of another character",
                FamilyEntitlementType.SummonMember => "Summon another character to my location",
                FamilyEntitlementType.DropBuff => "Drop Rate Buff",
                FamilyEntitlementType.ExpBuff => "EXP Buff",
                _ => "Drop Rate, EXP Buff"
            };
        }

        private sealed class FamilyMemberState
        {
            public FamilyMemberState(
                int id,
                string name,
                string jobName,
                int level,
                string locationSummary,
                int? parentId,
                int currentReputation,
                int todayReputation,
                bool isOnline,
                Vector2 simulatedPosition)
            {
                Id = id;
                Name = name;
                JobName = jobName;
                Level = level;
                LocationSummary = locationSummary;
                ParentId = parentId;
                CurrentReputation = currentReputation;
                TodayReputation = todayReputation;
                IsOnline = isOnline;
                SimulatedPosition = simulatedPosition;
            }

            public int Id { get; }
            public string Name { get; set; }
            public string JobName { get; set; }
            public int Level { get; set; }
            public string LocationSummary { get; set; }
            public int? ParentId { get; set; }
            public int CurrentReputation { get; set; }
            public int TodayReputation { get; set; }
            public bool IsOnline { get; set; }
            public Vector2 SimulatedPosition { get; set; }
            public List<int> Children { get; } = new();
        }

        private sealed class FamilyPrivilegeState
        {
            public FamilyPrivilegeState(FamilyEntitlementType type, int expiresAtTick)
            {
                Type = type;
                ExpiresAtTick = expiresAtTick;
            }

            public FamilyEntitlementType Type { get; }
            public int ExpiresAtTick { get; }
        }

        private readonly record struct FamilyRecruitSeed(
            string Name,
            string JobName,
            int Level,
            string LocationSummary,
            bool IsOnline,
            Vector2 SimulatedPosition);
    }

    internal readonly record struct FamilyEntitlementUseResult(
        string Message,
        bool RequestTeleport = false,
        Vector2 TeleportPosition = default);

    internal sealed class FamilyChartSnapshot
    {
        public int SelectedMemberId { get; init; }
        public string SelectedMemberName { get; init; } = "Player";
        public string SelectedRank { get; init; } = "Junior";
        public string LocationSummary { get; init; } = string.Empty;
        public int TotalMembers { get; init; }
        public int JuniorCount { get; init; }
        public int CurrentReputation { get; init; }
        public int TodayReputation { get; init; }
        public int SpecialReputationCost { get; init; }
        public int SpecialUsesLeft { get; init; }
        public string Precept { get; init; } = string.Empty;
        public string EntitlementLabel { get; init; } = string.Empty;
        public int EntitlementIndex { get; init; }
        public IReadOnlyList<string> DetailLines { get; init; } = Array.Empty<string>();
        public bool CanPageBackward { get; init; }
        public bool CanPageForward { get; init; }
        public bool CanAddJunior { get; init; }
        public bool CanUseSpecial { get; init; }
        public int Page { get; init; }
        public int TotalPages { get; init; } = 1;
    }

    internal sealed class FamilyTreeSnapshot
    {
        public IReadOnlyList<FamilyTreeNodeSnapshot> Nodes { get; init; } = Array.Empty<FamilyTreeNodeSnapshot>();
        public int TotalMembers { get; init; }
        public string FocusName { get; init; } = string.Empty;
        public string TitleText { get; init; } = string.Empty;
        public string JuniorCountText { get; init; } = string.Empty;
        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();
        public bool CanPageBackward { get; init; }
        public bool CanPageForward { get; init; }
        public bool CanAddJunior { get; init; }
        public bool CanRemoveSelected { get; init; }
        public int Page { get; init; }
        public int TotalPages { get; init; } = 1;
    }

    internal sealed class FamilyTreeNodeSnapshot
    {
        public int SlotIndex { get; init; }
        public int MemberId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Rank { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public string StatisticText { get; init; } = string.Empty;
        public string PlaceholderText { get; init; } = string.Empty;
        public bool IsLeader { get; init; }
        public bool IsLocalPlayer { get; init; }
        public bool IsSelected { get; init; }
        public bool IsOnline { get; init; }
        public bool UseAlertNameColor { get; init; }
    }

    internal sealed class FamilyTrackedMemberSnapshot
    {
        public string Name { get; init; } = string.Empty;
        public string LocationSummary { get; init; } = string.Empty;
        public bool IsOnline { get; init; }
        public Vector2 SimulatedPosition { get; init; }
        public bool IsLocalPlayer { get; init; }
    }

    internal enum FamilyEntitlementType
    {
        MoveToMember = 0,
        SummonMember = 1,
        DropBuff = 2,
        ExpBuff = 3,
        DropAndExpBuff = 4
    }
}
