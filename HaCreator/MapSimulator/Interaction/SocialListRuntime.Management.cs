using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum GuildManageTab
    {
        Position = 0,
        Admission = 1,
        Change = 2
    }

    internal enum AllianceEditorFocus
    {
        RankTitle = 0,
        Notice = 1
    }

    internal sealed partial class SocialListRuntime
    {
        private readonly List<string> _guildRankTitles = new()
        {
            "Master",
            "Jr. Master",
            "Veteran",
            "Member",
            "Recruit"
        };

        private readonly List<string> _allianceRankTitles = new()
        {
            "Chief",
            "Executive",
            "Captain",
            "Member",
            "Recruit"
        };

        private GuildManageTab _guildManageCurrentTab;
        private int _guildManageSelectedRankIndex;
        private bool _guildManageRequiresApproval = true;
        private bool _guildManageEditing;
        private string _guildManageDraft = string.Empty;
        private string _guildNoticeText = "Attendance check at reset. Boss signups after event run.";

        private AllianceEditorFocus _allianceEditorFocus;
        private int _allianceSelectedRankIndex;
        private bool _allianceEditorEditing;
        private string _allianceEditorDraft = string.Empty;
        private string _allianceNoticeText = "Union training routes rotate nightly. Whisper for regroup.";
        private PacketOwnedSocialMutation? _pendingPacketOwnedGuildMutation;
        private PacketOwnedSocialMutation? _pendingPacketOwnedAllianceMutation;

        internal void OpenGuildManageWindow(GuildManageTab initialTab)
        {
            _guildManageCurrentTab = initialTab;
            _guildManageEditing = false;
            _guildManageDraft = string.Empty;
            _guildManageSelectedRankIndex = Math.Clamp(_guildManageSelectedRankIndex, 0, Math.Max(0, _guildRankTitles.Count - 1));
            NotifyGuildManageTabObserved();
            if (_guildManageCurrentTab == GuildManageTab.Position)
            {
                NotifyGuildManageRankObserved();
            }
        }

        internal GuildManageSnapshot BuildGuildManageSnapshot()
        {
            string effectiveRole = GetEffectiveGuildRoleLabel();
            string selectedTitle = _guildRankTitles.Count == 0
                ? string.Empty
                : _guildRankTitles[Math.Clamp(_guildManageSelectedRankIndex, 0, _guildRankTitles.Count - 1)];
            string editableText = _guildManageCurrentTab switch
            {
                GuildManageTab.Position => _guildManageEditing ? _guildManageDraft : selectedTitle,
                GuildManageTab.Change => _guildManageEditing ? _guildManageDraft : _guildNoticeText,
                _ => string.Empty
            };

            return new GuildManageSnapshot
            {
                CurrentTab = _guildManageCurrentTab,
                RankTitles = _guildRankTitles.ToArray(),
                SelectedRankIndex = Math.Clamp(_guildManageSelectedRankIndex, 0, Math.Max(0, _guildRankTitles.Count - 1)),
                RequiresApproval = _guildManageRequiresApproval,
                EditableText = editableText,
                NoticeText = _guildNoticeText,
                IsEditing = _guildManageEditing,
                CanEdit = CanEditCurrentGuildManageTab(),
                CanPageBackward = _guildManageCurrentTab == GuildManageTab.Position && _guildManageSelectedRankIndex > 0,
                CanPageForward = _guildManageCurrentTab == GuildManageTab.Position && _guildManageSelectedRankIndex < _guildRankTitles.Count - 1,
                SummaryLines = BuildGuildManageSummary(effectiveRole, selectedTitle)
            };
        }

        internal void SelectGuildManageTab(GuildManageTab tab)
        {
            _guildManageCurrentTab = tab;
            _guildManageEditing = false;
            _guildManageDraft = string.Empty;
            NotifyGuildManageTabObserved();
        }

        internal void SelectGuildManageRank(int visibleIndex)
        {
            if (_guildManageCurrentTab != GuildManageTab.Position || visibleIndex < 0 || visibleIndex >= _guildRankTitles.Count)
            {
                return;
            }

            _guildManageSelectedRankIndex = visibleIndex;
            NotifyGuildManageRankObserved();
        }

        internal void MoveGuildManageRankSelection(int delta)
        {
            if (_guildManageCurrentTab != GuildManageTab.Position || _guildRankTitles.Count == 0)
            {
                return;
            }

            int previousIndex = _guildManageSelectedRankIndex;
            _guildManageSelectedRankIndex = Math.Clamp(_guildManageSelectedRankIndex + delta, 0, _guildRankTitles.Count - 1);
            if (_guildManageSelectedRankIndex != previousIndex)
            {
                NotifyGuildManageRankObserved();
            }
        }

        internal string BeginGuildManageEdit()
        {
            if (_guildManageCurrentTab == GuildManageTab.Position && !CanManageGuildRanks())
            {
                return NotifySocialEditorPrompt($"Guild rank titles are read-only while the active authority role is {GetEffectiveGuildRoleLabel()}.");
            }

            if (_guildManageCurrentTab == GuildManageTab.Admission)
            {
                return NotifySocialEditorPrompt("Guild admission uses the OK/NO controls instead of freeform text.");
            }

            if (_guildManageCurrentTab == GuildManageTab.Change && !CanEditGuildNotice())
            {
                return NotifySocialEditorPrompt($"Guild notice editing is read-only while the active authority role is {GetEffectiveGuildRoleLabel()}.");
            }

            _guildManageEditing = true;
            _guildManageDraft = _guildManageCurrentTab == GuildManageTab.Position
                ? _guildRankTitles[_guildManageSelectedRankIndex]
                : _guildNoticeText;
            return NotifySocialEditorPrompt(_guildManageCurrentTab == GuildManageTab.Position
                ? $"Editing guild rank title {_guildManageSelectedRankIndex + 1}."
                : "Editing guild notice text.");
        }

        internal void SetGuildManageDraft(string value)
        {
            if (_guildManageEditing)
            {
                _guildManageDraft = value ?? string.Empty;
            }
        }

        internal string SaveGuildManageEdit()
        {
            if (!_guildManageEditing)
            {
                return "Use EDIT before saving guild management changes.";
            }

            string committedValue = string.IsNullOrWhiteSpace(_guildManageDraft)
                ? (_guildManageCurrentTab == GuildManageTab.Position ? $"Rank {_guildManageSelectedRankIndex + 1}" : "Guild notice")
                : _guildManageDraft.Trim();

            if (_guildManageCurrentTab == GuildManageTab.Position)
            {
                if (TryStagePacketOwnedManageMutation(
                        SocialListTab.Guild,
                        "Guild rank-title edit",
                        new PacketOwnedSocialMutation(
                            PacketOwnedSocialMutationKind.GuildRankTitle,
                            _guildManageSelectedRankIndex,
                            committedValue,
                            null),
                        out string requestMessage))
                {
                    _guildManageEditing = false;
                    _guildManageDraft = string.Empty;
                    return requestMessage;
                }

                _guildRankTitles[_guildManageSelectedRankIndex] = committedValue;
                NotifySocialTextEditCommitted(committedValue);
            }
            else if (_guildManageCurrentTab == GuildManageTab.Change)
            {
                if (TryStagePacketOwnedManageMutation(
                        SocialListTab.Guild,
                        "Guild notice edit",
                        new PacketOwnedSocialMutation(
                            PacketOwnedSocialMutationKind.GuildNotice,
                            null,
                            committedValue,
                            null),
                        out string requestMessage))
                {
                    _guildManageEditing = false;
                    _guildManageDraft = string.Empty;
                    return requestMessage;
                }

                _guildNoticeText = committedValue;
                NotifySocialTextEditCommitted(committedValue);
            }

            _guildManageEditing = false;
            _guildManageDraft = string.Empty;

            return _guildManageCurrentTab == GuildManageTab.Position
                ? $"Guild rank title {_guildManageSelectedRankIndex + 1} saved as \"{committedValue}\"."
                : "Guild notice text saved.";
        }

        internal string CancelGuildManageEdit()
        {
            if (!_guildManageEditing)
            {
                return null;
            }

            _guildManageEditing = false;
            _guildManageDraft = string.Empty;
            return NotifySocialEditorPrompt("Guild management edit canceled.");
        }

        internal string SetGuildAdmission(bool requiresApproval)
        {
            if (!CanToggleGuildAdmission())
            {
                return NotifySocialEditorPrompt($"Guild admission is read-only while the active authority role is {GetEffectiveGuildRoleLabel()}.");
            }

            if (TryStagePacketOwnedManageMutation(
                    SocialListTab.Guild,
                    "Guild admission toggle",
                    new PacketOwnedSocialMutation(
                        PacketOwnedSocialMutationKind.GuildAdmission,
                        null,
                        null,
                        requiresApproval),
                    out string requestMessage))
            {
                return requestMessage;
            }

            _guildManageRequiresApproval = requiresApproval;
            NotifySocialTextEditCommitted(requiresApproval ? "Approval required." : "Open enrollment.");
            return requiresApproval
                ? "Guild admission now requires approval."
                : "Guild admission now accepts open enrollment.";
        }

        internal void OpenAllianceEditor(AllianceEditorFocus focus)
        {
            _allianceEditorFocus = focus;
            _allianceEditorEditing = false;
            _allianceEditorDraft = string.Empty;
            _allianceSelectedRankIndex = Math.Clamp(_allianceSelectedRankIndex, 0, Math.Max(0, _allianceRankTitles.Count - 1));
            NotifyAllianceEditorFocusObserved();
        }

        internal AllianceEditorSnapshot BuildAllianceEditorSnapshot()
        {
            string selectedTitle = _allianceRankTitles.Count == 0
                ? string.Empty
                : _allianceRankTitles[Math.Clamp(_allianceSelectedRankIndex, 0, Math.Max(0, _allianceRankTitles.Count - 1))];
            string editableText = _allianceEditorFocus == AllianceEditorFocus.Notice
                ? (_allianceEditorEditing ? _allianceEditorDraft : _allianceNoticeText)
                : (_allianceEditorEditing ? _allianceEditorDraft : selectedTitle);

            return new AllianceEditorSnapshot
            {
                RankTitles = _allianceRankTitles.ToArray(),
                SelectedRankIndex = Math.Clamp(_allianceSelectedRankIndex, 0, Math.Max(0, _allianceRankTitles.Count - 1)),
                Focus = _allianceEditorFocus,
                EditableText = editableText,
                NoticeText = _allianceNoticeText,
                IsEditing = _allianceEditorEditing,
                CanEdit = _allianceEditorFocus == AllianceEditorFocus.Notice ? CanEditAllianceNotice() : CanEditAllianceRanks(),
                SummaryLines = BuildAllianceEditorSummary(selectedTitle)
            };
        }

        internal void SelectAllianceRankTitle(int visibleIndex)
        {
            if (visibleIndex < 0 || visibleIndex >= _allianceRankTitles.Count)
            {
                return;
            }

            _allianceEditorFocus = AllianceEditorFocus.RankTitle;
            _allianceSelectedRankIndex = visibleIndex;
            NotifyAllianceRankObserved();
        }

        internal void FocusAllianceNotice()
        {
            _allianceEditorFocus = AllianceEditorFocus.Notice;
            NotifyAllianceEditorFocusObserved();
        }

        internal string BeginAllianceEdit()
        {
            if (_allianceEditorFocus == AllianceEditorFocus.Notice && !CanEditAllianceNotice())
            {
                return NotifySocialEditorPrompt($"Alliance notice editing is read-only while the active authority role is {GetEffectiveAllianceRoleLabel()}.");
            }

            if (_allianceEditorFocus == AllianceEditorFocus.RankTitle && !CanEditAllianceRanks())
            {
                return NotifySocialEditorPrompt($"Alliance rank titles are read-only while the active authority role is {GetEffectiveAllianceRoleLabel()}.");
            }

            _allianceEditorEditing = true;
            _allianceEditorDraft = _allianceEditorFocus == AllianceEditorFocus.Notice
                ? _allianceNoticeText
                : _allianceRankTitles[_allianceSelectedRankIndex];
            return NotifySocialEditorPrompt(_allianceEditorFocus == AllianceEditorFocus.Notice
                ? "Editing alliance notice text."
                : $"Editing alliance rank title {_allianceSelectedRankIndex + 1}.");
        }

        internal void SetAllianceEditorDraft(string value)
        {
            if (_allianceEditorEditing)
            {
                _allianceEditorDraft = value ?? string.Empty;
            }
        }

        internal string SaveAllianceEdit()
        {
            if (!_allianceEditorEditing)
            {
                return "Use EDIT before saving alliance changes.";
            }

            string committedValue = string.IsNullOrWhiteSpace(_allianceEditorDraft)
                ? (_allianceEditorFocus == AllianceEditorFocus.Notice ? "Alliance notice" : $"Rank {_allianceSelectedRankIndex + 1}")
                : _allianceEditorDraft.Trim();
            if (_allianceEditorFocus == AllianceEditorFocus.Notice)
            {
                if (TryStagePacketOwnedManageMutation(
                        SocialListTab.Alliance,
                        "Alliance notice edit",
                        new PacketOwnedSocialMutation(
                            PacketOwnedSocialMutationKind.AllianceNotice,
                            null,
                            committedValue,
                            null),
                        out string requestMessage))
                {
                    _allianceEditorEditing = false;
                    _allianceEditorDraft = string.Empty;
                    return requestMessage;
                }

                _allianceNoticeText = committedValue;
                NotifySocialTextEditCommitted(committedValue);
            }
            else
            {
                if (TryStagePacketOwnedManageMutation(
                        SocialListTab.Alliance,
                        "Alliance rank-title edit",
                        new PacketOwnedSocialMutation(
                            PacketOwnedSocialMutationKind.AllianceRankTitle,
                            _allianceSelectedRankIndex,
                            committedValue,
                            null),
                        out string requestMessage))
                {
                    _allianceEditorEditing = false;
                    _allianceEditorDraft = string.Empty;
                    return requestMessage;
                }

                _allianceRankTitles[_allianceSelectedRankIndex] = committedValue;
                NotifySocialTextEditCommitted(committedValue);
            }

            _allianceEditorEditing = false;
            _allianceEditorDraft = string.Empty;
            return _allianceEditorFocus == AllianceEditorFocus.Notice
                ? "Alliance notice text saved."
                : $"Alliance rank title {_allianceSelectedRankIndex + 1} saved as \"{committedValue}\".";
        }

        internal string SetPacketGuildRankTitles(IReadOnlyList<string> rankTitles, int guildId)
        {
            if (ShouldIgnoreGuildScopedResult(guildId, out int activeGuildId))
            {
                return $"Ignored client OnGuildResult({(byte)SocialListClientGuildResultKind.RankTitles}) for guild {guildId} because the active packet-owned guild context is {activeGuildId}.";
            }

            if (rankTitles == null || rankTitles.Count == 0)
            {
                return "Client guild-result rank-title payload did not include any titles.";
            }

            RememberPacketGuildId(guildId);
            _guildRankTitles.Clear();
            for (int i = 0; i < rankTitles.Count; i++)
            {
                _guildRankTitles.Add(string.IsNullOrWhiteSpace(rankTitles[i]) ? $"Rank {i + 1}" : rankTitles[i].Trim());
            }

            foreach (string rankTitle in _guildRankTitles)
            {
                NotifySocialTextEditCommitted(rankTitle);
            }

            _guildManageSelectedRankIndex = Math.Clamp(_guildManageSelectedRankIndex, 0, Math.Max(0, _guildRankTitles.Count - 1));
            _lastPacketSyncSummaryByTab[SocialListTab.Guild] =
                $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.RankTitles}) refreshed {_guildRankTitles.Count} guild rank titles"
                + (guildId > 0 ? $" for guild {guildId}." : ".");
            return $"Guild rank titles now follow the client OnGuildResult({(byte)SocialListClientGuildResultKind.RankTitles}) payload.";
        }

        internal string SetPacketGuildNoticeText(string notice, int guildId)
        {
            if (ShouldIgnoreGuildScopedResult(guildId, out int activeGuildId))
            {
                return $"Ignored client OnGuildResult({(byte)SocialListClientGuildResultKind.Notice}) for guild {guildId} because the active packet-owned guild context is {activeGuildId}.";
            }

            RememberPacketGuildId(guildId);
            _guildNoticeText = string.IsNullOrWhiteSpace(notice) ? string.Empty : notice.Trim();
            NotifySocialTextEditCommitted(_guildNoticeText);
            _lastPacketSyncSummaryByTab[SocialListTab.Guild] =
                $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.Notice}) refreshed guild notice text"
                + (guildId > 0 ? $" for guild {guildId}." : ".");
            return string.IsNullOrWhiteSpace(_guildNoticeText)
                ? $"Guild notice cleared from client OnGuildResult({(byte)SocialListClientGuildResultKind.Notice})."
                : $"Guild notice now follows the client OnGuildResult({(byte)SocialListClientGuildResultKind.Notice}) payload.";
        }

        internal string SetPacketAllianceRankTitles(IReadOnlyList<string> rankTitles, int allianceId)
        {
            if (rankTitles == null || rankTitles.Count == 0)
            {
                return "Client alliance-result rank-title payload did not include any titles.";
            }

            _allianceRankTitles.Clear();
            for (int i = 0; i < rankTitles.Count; i++)
            {
                _allianceRankTitles.Add(string.IsNullOrWhiteSpace(rankTitles[i]) ? $"Rank {i + 1}" : rankTitles[i].Trim());
            }

            foreach (string rankTitle in _allianceRankTitles)
            {
                NotifySocialTextEditCommitted(rankTitle);
            }

            _allianceSelectedRankIndex = Math.Clamp(_allianceSelectedRankIndex, 0, Math.Max(0, _allianceRankTitles.Count - 1));
            _lastPacketSyncSummaryByTab[SocialListTab.Alliance] =
                $"Client OnAllianceResult({(byte)SocialListClientAllianceResultKind.RankTitles}) refreshed {_allianceRankTitles.Count} alliance rank titles"
                + (allianceId > 0 ? $" for alliance {allianceId}." : ".");
            return $"Alliance rank titles now follow the client OnAllianceResult({(byte)SocialListClientAllianceResultKind.RankTitles}) payload.";
        }

        internal string SetPacketAllianceNoticeText(string notice, int allianceId)
        {
            _allianceNoticeText = string.IsNullOrWhiteSpace(notice) ? string.Empty : notice.Trim();
            NotifySocialTextEditCommitted(_allianceNoticeText);
            _lastPacketSyncSummaryByTab[SocialListTab.Alliance] =
                $"Client OnAllianceResult({(byte)SocialListClientAllianceResultKind.Notice}) refreshed alliance notice text"
                + (allianceId > 0 ? $" for alliance {allianceId}." : ".");
            return string.IsNullOrWhiteSpace(_allianceNoticeText)
                ? $"Alliance notice cleared from client OnAllianceResult({(byte)SocialListClientAllianceResultKind.Notice})."
                : $"Alliance notice now follows the client OnAllianceResult({(byte)SocialListClientAllianceResultKind.Notice}) payload.";
        }

        internal string CancelAllianceEdit()
        {
            if (!_allianceEditorEditing)
            {
                return null;
            }

            _allianceEditorEditing = false;
            _allianceEditorDraft = string.Empty;
            return NotifySocialEditorPrompt("Alliance edit canceled.");
        }

        private IReadOnlyList<string> BuildGuildManageSummary(string localRole, string selectedTitle)
        {
            return _guildManageCurrentTab switch
            {
                GuildManageTab.Position => new[]
                {
                    $"Guild role: {localRole}",
                    $"Selected rank: {selectedTitle}",
                    $"{GetGuildAuthoritySummary()}. {(CanManageGuildRanks() ? "Edit or save to rename guild rank titles." : "Rank-title edits remain locked.")}"
                },
                GuildManageTab.Admission => new[]
                {
                    $"Guild role: {localRole}",
                    _guildManageRequiresApproval ? "Admission mode: approval required" : "Admission mode: open enrollment",
                    $"{GetGuildAuthoritySummary()}. {(CanToggleGuildAdmission() ? "Use OK/NO to mirror the client admission toggle." : "Admission stays read-only.")}"
                },
                _ => new[]
                {
                    $"Guild role: {localRole}",
                    "Guild notice text is edited from the Change tab.",
                    $"{GetGuildAuthoritySummary()}. {(CanEditGuildNotice() ? "Use EDIT then SAVE to commit the notice draft." : "Notice editing stays read-only.")}"
                }
            };
        }

        private void NotifySocialTextEditCommitted(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                NotifySocialChatObserved(text.Trim());
            }
        }

        private string NotifySocialEditorPrompt(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                NotifySocialChatObserved(message.Trim());
            }

            return message;
        }

        private bool TryStagePacketOwnedManageMutation(
            SocialListTab tab,
            string requestLabel,
            PacketOwnedSocialMutation mutation,
            out string requestMessage)
        {
            if (!IsPacketOwned(tab))
            {
                requestMessage = null;
                return false;
            }

            if (_lastPendingRequestByTab.TryGetValue(tab, out string pendingRequest)
                && !string.IsNullOrWhiteSpace(pendingRequest))
            {
                requestMessage = NotifySocialEditorPrompt(
                    $"{pendingRequest} is already pending packet-owned {GetHeaderTitle(tab).ToLowerInvariant()} approval.");
                return true;
            }

            _lastPendingRequestByTab[tab] = requestLabel;
            _lastPacketSyncSummaryByTab[tab] =
                $"{requestLabel} was sent through packet-owned {GetHeaderTitle(tab).ToLowerInvariant()} authority and is awaiting approval.";

            if (tab == SocialListTab.Guild)
            {
                _pendingPacketOwnedGuildMutation = mutation;
            }
            else if (tab == SocialListTab.Alliance)
            {
                _pendingPacketOwnedAllianceMutation = mutation;
            }

            requestMessage = NotifySocialEditorPrompt(
                $"{requestLabel} is pending packet-owned {GetHeaderTitle(tab).ToLowerInvariant()} approval.");
            return true;
        }

        private bool TryGetPendingPacketOwnedManageMutation(SocialListTab tab, out PacketOwnedSocialMutation mutation)
        {
            switch (tab)
            {
                case SocialListTab.Guild when _pendingPacketOwnedGuildMutation.HasValue:
                    mutation = _pendingPacketOwnedGuildMutation.Value;
                    return true;
                case SocialListTab.Alliance when _pendingPacketOwnedAllianceMutation.HasValue:
                    mutation = _pendingPacketOwnedAllianceMutation.Value;
                    return true;
                default:
                    mutation = default;
                    return false;
            }
        }

        private void ClearPendingPacketOwnedManageMutation(SocialListTab tab)
        {
            if (tab == SocialListTab.Guild)
            {
                _pendingPacketOwnedGuildMutation = null;
            }
            else if (tab == SocialListTab.Alliance)
            {
                _pendingPacketOwnedAllianceMutation = null;
            }
        }

        private string ApplyPendingPacketOwnedManageMutation(SocialListTab tab, PacketOwnedSocialMutation mutation)
        {
            switch (mutation.Kind)
            {
                case PacketOwnedSocialMutationKind.GuildRankTitle:
                    if (mutation.Index.HasValue && !string.IsNullOrWhiteSpace(mutation.TextValue))
                    {
                        int rankIndex = Math.Clamp(mutation.Index.Value, 0, Math.Max(0, _guildRankTitles.Count - 1));
                        _guildRankTitles[rankIndex] = mutation.TextValue.Trim();
                        NotifySocialTextEditCommitted(_guildRankTitles[rankIndex]);
                        return $"Guild rank title {rankIndex + 1} applied from packet-owned approval.";
                    }

                    break;

                case PacketOwnedSocialMutationKind.GuildNotice:
                    if (!string.IsNullOrWhiteSpace(mutation.TextValue))
                    {
                        _guildNoticeText = mutation.TextValue.Trim();
                        NotifySocialTextEditCommitted(_guildNoticeText);
                        return "Guild notice text applied from packet-owned approval.";
                    }

                    break;

                case PacketOwnedSocialMutationKind.GuildAdmission:
                    if (mutation.BoolValue.HasValue)
                    {
                        _guildManageRequiresApproval = mutation.BoolValue.Value;
                        NotifySocialTextEditCommitted(_guildManageRequiresApproval ? "Approval required." : "Open enrollment.");
                        return _guildManageRequiresApproval
                            ? "Guild admission toggle applied from packet-owned approval: approval required."
                            : "Guild admission toggle applied from packet-owned approval: open enrollment.";
                    }

                    break;

                case PacketOwnedSocialMutationKind.AllianceRankTitle:
                    if (mutation.Index.HasValue && !string.IsNullOrWhiteSpace(mutation.TextValue))
                    {
                        int rankIndex = Math.Clamp(mutation.Index.Value, 0, Math.Max(0, _allianceRankTitles.Count - 1));
                        _allianceRankTitles[rankIndex] = mutation.TextValue.Trim();
                        NotifySocialTextEditCommitted(_allianceRankTitles[rankIndex]);
                        return $"Alliance rank title {rankIndex + 1} applied from packet-owned approval.";
                    }

                    break;

                case PacketOwnedSocialMutationKind.AllianceNotice:
                    if (!string.IsNullOrWhiteSpace(mutation.TextValue))
                    {
                        _allianceNoticeText = mutation.TextValue.Trim();
                        NotifySocialTextEditCommitted(_allianceNoticeText);
                        return "Alliance notice text applied from packet-owned approval.";
                    }

                    break;
            }

            return $"Packet-owned {GetHeaderTitle(tab).ToLowerInvariant()} approval did not include an applicable local mutation.";
        }

        private enum PacketOwnedSocialMutationKind
        {
            GuildRankTitle,
            GuildNotice,
            GuildAdmission,
            AllianceRankTitle,
            AllianceNotice
        }

        private readonly record struct PacketOwnedSocialMutation(
            PacketOwnedSocialMutationKind Kind,
            int? Index,
            string TextValue,
            bool? BoolValue);

        private void NotifyGuildManageTabObserved()
        {
            NotifySocialEditorPrompt(_guildManageCurrentTab switch
            {
                GuildManageTab.Position => "Guild management tab: rank titles.",
                GuildManageTab.Admission => "Guild management tab: admission mode.",
                GuildManageTab.Change => "Guild management tab: notice text.",
                _ => null
            });
        }

        private void NotifyGuildManageRankObserved()
        {
            if (_guildRankTitles.Count == 0)
            {
                return;
            }

            int rankIndex = Math.Clamp(_guildManageSelectedRankIndex, 0, _guildRankTitles.Count - 1);
            NotifySocialEditorPrompt($"Guild rank title {rankIndex + 1} selected: {_guildRankTitles[rankIndex]}.");
        }

        private void NotifyAllianceRankObserved()
        {
            if (_allianceRankTitles.Count == 0)
            {
                return;
            }

            int rankIndex = Math.Clamp(_allianceSelectedRankIndex, 0, _allianceRankTitles.Count - 1);
            NotifySocialEditorPrompt($"Alliance rank title {rankIndex + 1} selected: {_allianceRankTitles[rankIndex]}.");
        }

        private void NotifyAllianceEditorFocusObserved()
        {
            if (_allianceEditorFocus == AllianceEditorFocus.Notice)
            {
                NotifySocialEditorPrompt("Alliance editor focus: notice text.");
                return;
            }

            NotifyAllianceRankObserved();
        }

        private IReadOnlyList<string> BuildAllianceEditorSummary(string selectedTitle)
        {
            return new[]
            {
                $"Alliance role: {GetEffectiveAllianceRoleLabel()}",
                _allianceEditorFocus == AllianceEditorFocus.Notice ? "Focus: alliance notice" : $"Focus: rank title {_allianceSelectedRankIndex + 1} ({selectedTitle})",
                $"{GetAllianceAuthoritySummary()}. {((_allianceEditorFocus == AllianceEditorFocus.Notice ? CanEditAllianceNotice() : CanEditAllianceRanks()) ? "Use EDIT and SAVE to commit the focused field." : "The focused field is read-only.")}"
            };
        }

        private bool CanEditCurrentGuildManageTab()
        {
            return _guildManageCurrentTab switch
            {
                GuildManageTab.Position => CanManageGuildRanks(),
                GuildManageTab.Admission => CanToggleGuildAdmission(),
                GuildManageTab.Change => CanEditGuildNotice(),
                _ => false
            };
        }

        private bool CanManageGuild()
        {
            return HasGuildAdministrativeAuthority();
        }

        private bool CanManageAlliance()
        {
            return HasAllianceAdministrativeAuthority();
        }

        private string GetLocalAllianceRoleLabel()
        {
            SocialEntryState localAllianceEntry = _entriesByTab.TryGetValue(SocialListTab.Alliance, out List<SocialEntryState> entries)
                ? entries.FirstOrDefault(entry => entry.IsLocalPlayer)
                : null;
            return string.IsNullOrWhiteSpace(localAllianceEntry?.PrimaryText)
                ? "Member"
                : localAllianceEntry.PrimaryText;
        }
    }

    internal sealed class GuildManageSnapshot
    {
        public GuildManageTab CurrentTab { get; init; }
        public IReadOnlyList<string> RankTitles { get; init; } = Array.Empty<string>();
        public int SelectedRankIndex { get; init; }
        public bool RequiresApproval { get; init; }
        public string EditableText { get; init; } = string.Empty;
        public string NoticeText { get; init; } = string.Empty;
        public bool IsEditing { get; init; }
        public bool CanEdit { get; init; }
        public bool CanPageBackward { get; init; }
        public bool CanPageForward { get; init; }
        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();
    }

    internal sealed class AllianceEditorSnapshot
    {
        public IReadOnlyList<string> RankTitles { get; init; } = Array.Empty<string>();
        public int SelectedRankIndex { get; init; }
        public AllianceEditorFocus Focus { get; init; }
        public string EditableText { get; init; } = string.Empty;
        public string NoticeText { get; init; } = string.Empty;
        public bool IsEditing { get; init; }
        public bool CanEdit { get; init; }
        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();
    }
}
