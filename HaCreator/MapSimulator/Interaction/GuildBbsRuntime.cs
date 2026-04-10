using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum GuildBbsEmoticonKind
    {
        None,
        Basic,
        Cash
    }

    internal enum GuildBbsPermissionLevel
    {
        None,
        Member,
        JrMaster,
        Master
    }

    [Flags]
    internal enum GuildBbsPermissionMask
    {
        None = 0,
        WriteThread = 1 << 0,
        WriteNotice = 1 << 1,
        Reply = 1 << 2,
        OwnThread = 1 << 3,
        Moderate = 1 << 4
    }

    internal sealed class GuildBbsRuntime
    {
        private const int VisibleThreadCount = 8;
        private const int VisibleCommentCount = 4;
        private const int VisibleCashEmoticonCount = 7;
        private const int DefaultBasicEmoticonCount = 3;
        private const int DefaultCashEmoticonCount = 7;
        internal const int ClientCashEmoticonCount = 7;
        private const int CashEmoticonItemIdStart = 5290000;
        private const int ClientCashEmoticonIdStart = 100;
        private const GuildBbsPermissionMask SupportedPermissionMask =
            GuildBbsPermissionMask.WriteThread
            | GuildBbsPermissionMask.WriteNotice
            | GuildBbsPermissionMask.Reply
            | GuildBbsPermissionMask.OwnThread
            | GuildBbsPermissionMask.Moderate;
        private const int MaxTitleLength = 25;
        private const int MaxThreadBodyLength = 600;
        private const int MaxReplyBodyLength = 25;
        private const int EnterTextStringPoolId = 0xEB1;

        private sealed class GuildBbsCommentState
        {
            public int CommentId { get; init; }
            public string Author { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public DateTimeOffset CreatedAt { get; set; }
            public GuildBbsEmoticonKind EmoticonKind { get; set; }
            public int EmoticonSlot { get; set; } = -1;
            public int CashEmoticonPageIndex { get; set; }
        }

        private sealed class GuildBbsThreadState
        {
            public int ThreadId { get; init; }
            public string Title { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public string Author { get; set; } = string.Empty;
            public DateTimeOffset CreatedAt { get; set; }
            public bool IsNotice { get; set; }
            public GuildBbsEmoticonKind EmoticonKind { get; set; }
            public int EmoticonSlot { get; set; } = -1;
            public int CashEmoticonPageIndex { get; set; }
            public List<GuildBbsCommentState> Comments { get; } = new();
        }

        private sealed class GuildBbsComposeState
        {
            public int EditThreadId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public bool IsNotice { get; set; }
            public GuildBbsEmoticonKind EmoticonKind { get; set; }
            public int EmoticonSlot { get; set; } = -1;
            public int CashEmoticonPageIndex { get; set; }
        }

        private sealed class GuildBbsReplyDraftState
        {
            public string Body { get; set; } = string.Empty;
            public GuildBbsEmoticonKind EmoticonKind { get; set; }
            public int EmoticonSlot { get; set; } = -1;
            public int CashEmoticonPageIndex { get; set; }
        }

        private readonly List<GuildBbsThreadState> _threads = new();
        private readonly HashSet<int> _inventoryOwnedCashEmoticonIds = new();
        private readonly HashSet<int> _packetOwnedCashEmoticonIds = new();
        private GuildBbsComposeState _compose = new();
        private readonly GuildBbsReplyDraftState _replyDraft = new();
        private string _localPlayerName = "Player";
        private string _guildName = "Maple Guild";
        private string _locationSummary = "Field";
        private string _guildRoleLabel = "Master";
        private GuildBbsPermissionLevel _permissionLevel = GuildBbsPermissionLevel.Master;
        private GuildBbsPermissionMask _rolePermissionMask = SupportedPermissionMask;
        private GuildBbsPermissionMask? _packetPermissionMask;
        private bool _hasPacketCashOwnershipOverride;
        private int _selectedThreadId;
        private int _threadPageIndex;
        private int _commentPageIndex;
        private int _nextThreadId = 1;
        private int _nextCommentId = 1;
        private int _draftCounter = 1;
        private int _basicEmoticonCount = DefaultBasicEmoticonCount;
        private int _cashEmoticonCount = DefaultCashEmoticonCount;

        public GuildBbsRuntime()
        {
            SeedDefaultThreads();
        }

        public bool IsWriteMode { get; private set; }
        public Action<string, int> SocialChatObserved { get; set; }

        public void ConfigureEmoticonCatalog(int basicEmoticonCount, int cashEmoticonCount)
        {
            _basicEmoticonCount = Math.Max(1, basicEmoticonCount);
            _cashEmoticonCount = Math.Max(1, Math.Min(ClientCashEmoticonCount, cashEmoticonCount));
            NormalizeDraftState();
        }

        public void UpdateLocalContext(string playerName, string guildName, string locationSummary, string guildRoleLabel, IEnumerable<int> ownedCashEmoticonItemIds)
        {
            _localPlayerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
            _guildName = string.IsNullOrWhiteSpace(guildName) ? "Maple Guild" : guildName.Trim();
            _locationSummary = string.IsNullOrWhiteSpace(locationSummary) ? "Field" : locationSummary.Trim();
            _guildRoleLabel = string.IsNullOrWhiteSpace(guildRoleLabel) ? "Member" : guildRoleLabel.Trim();
            _permissionLevel = ResolvePermissionLevel(_guildRoleLabel);
            _rolePermissionMask = ResolvePermissionMask(_permissionLevel);

            _inventoryOwnedCashEmoticonIds.Clear();
            if (ownedCashEmoticonItemIds != null)
            {
                foreach (int itemId in ownedCashEmoticonItemIds)
                {
                    if (itemId >= CashEmoticonItemIdStart && itemId < CashEmoticonItemIdStart + _cashEmoticonCount)
                    {
                        _inventoryOwnedCashEmoticonIds.Add(itemId);
                    }
                }
            }

            NormalizeDraftState();
        }

        public string ApplyPermissionMaskOverride(GuildBbsPermissionMask mask)
        {
            _packetPermissionMask = NormalizePermissionMask(mask);
            NormalizeDraftState();
            return $"Guild BBS authority override is now packet-owned: {DescribePermissionMask(EffectivePermissionMask)}.";
        }

        public string ClearPermissionMaskOverride()
        {
            _packetPermissionMask = null;
            NormalizeDraftState();
            return $"Guild BBS authority reverted to guild-role rules: {DescribePermissionMask(EffectivePermissionMask)}.";
        }

        public string ApplyPermissionPacket(byte[] payload)
        {
            if (!TryDecodePermissionPacket(payload, out GuildBbsPermissionMask decodedMask, out string detail))
            {
                return detail;
            }

            _packetPermissionMask = decodedMask;
            NormalizeDraftState();
            return $"Decoded Guild BBS authority packet -> {DescribePermissionMask(decodedMask)}.";
        }

        public string ApplyCashOwnershipPacket(byte[] payload)
        {
            if (!TryDecodeCashOwnershipPacket(payload, out HashSet<int> decodedOwnership, out string detail))
            {
                return detail;
            }

            _packetOwnedCashEmoticonIds.Clear();
            foreach (int itemId in decodedOwnership)
            {
                _packetOwnedCashEmoticonIds.Add(itemId);
            }

            _hasPacketCashOwnershipOverride = true;
            NormalizeDraftState();
            return $"Decoded Guild BBS cash-entitlement packet -> {decodedOwnership.Count}/{_cashEmoticonCount} owned ({CashOwnershipSourceLabel}).";
        }

        public string ClearCashOwnershipPacket()
        {
            _packetOwnedCashEmoticonIds.Clear();
            _hasPacketCashOwnershipOverride = false;
            NormalizeDraftState();
            return $"Guild BBS cash entitlement reverted to inventory-owned state: {OwnedCashEmoticonCount}/{_cashEmoticonCount}.";
        }

        public GuildBbsSnapshot BuildSnapshot()
        {
            IReadOnlyList<GuildBbsThreadState> orderedThreads = GetOrderedThreads();
            EnsureSelection(orderedThreads);
            EnsureThreadPageInRange(orderedThreads.Count);

            GuildBbsThreadState selectedThread = orderedThreads.FirstOrDefault(thread => thread.ThreadId == _selectedThreadId);
            GuildBbsThreadState noticeThread = orderedThreads.FirstOrDefault(thread => thread.IsNotice);
            int threadPageCount = Math.Max(1, (int)Math.Ceiling(orderedThreads.Count / (double)VisibleThreadCount));
            IReadOnlyList<GuildBbsThreadEntrySnapshot> visibleThreads = orderedThreads
                .Skip(_threadPageIndex * VisibleThreadCount)
                .Take(VisibleThreadCount)
                .Select(CreateThreadEntrySnapshot)
                .ToArray();

            GuildBbsThreadSnapshot selectedThreadSnapshot = null;
            if (selectedThread != null)
            {
                IReadOnlyList<GuildBbsCommentState> orderedComments = selectedThread.Comments
                    .OrderBy(comment => comment.CreatedAt)
                    .ToArray();
                EnsureCommentPageInRange(orderedComments.Count);

                int commentPageCount = Math.Max(1, (int)Math.Ceiling(orderedComments.Count / (double)VisibleCommentCount));
                IReadOnlyList<GuildBbsCommentSnapshot> visibleComments = orderedComments
                    .Skip(_commentPageIndex * VisibleCommentCount)
                    .Take(VisibleCommentCount)
                    .Select(comment => new GuildBbsCommentSnapshot
                    {
                        CommentId = comment.CommentId,
                        Author = comment.Author,
                        Body = comment.Body,
                        DateText = comment.CreatedAt.ToLocalTime().ToString("MM.dd HH:mm"),
                        Emoticon = CreateEmoticonSnapshot(comment.EmoticonKind, comment.EmoticonSlot, comment.CashEmoticonPageIndex),
                        CanDelete = CanDeleteComment(comment)
                    })
                    .ToArray();

                selectedThreadSnapshot = new GuildBbsThreadSnapshot
                {
                    ThreadId = selectedThread.ThreadId,
                    Title = selectedThread.Title,
                    Body = selectedThread.Body,
                    Author = selectedThread.Author,
                    DateText = selectedThread.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm"),
                    IsNotice = selectedThread.IsNotice,
                    Emoticon = CreateEmoticonSnapshot(selectedThread.EmoticonKind, selectedThread.EmoticonSlot, selectedThread.CashEmoticonPageIndex),
                    CanEdit = CanEditThread(selectedThread),
                    CanDelete = CanDeleteThread(selectedThread),
                    Comments = visibleComments,
                    TotalCommentCount = orderedComments.Count,
                    CommentPageIndex = _commentPageIndex,
                    CommentPageCount = commentPageCount
                };
            }
            else
            {
                _commentPageIndex = 0;
            }

            return new GuildBbsSnapshot
            {
                GuildName = _guildName,
                LocalPlayerName = _localPlayerName,
                GuildRoleLabel = _guildRoleLabel,
                IsWriteMode = IsWriteMode,
                SelectedThreadId = _selectedThreadId,
                Threads = visibleThreads,
                TotalThreadCount = orderedThreads.Count,
                ThreadPageIndex = _threadPageIndex,
                ThreadPageCount = threadPageCount,
                SelectedThread = selectedThreadSnapshot,
                NoticeThread = noticeThread == null ? null : CreateThreadEntrySnapshot(noticeThread),
                Permission = BuildPermissionSnapshot(selectedThread),
                Compose = new GuildBbsComposeSnapshot
                {
                    Title = _compose.Title,
                    Body = _compose.Body,
                    IsNotice = _compose.IsNotice,
                    ModeText = _compose.EditThreadId > 0 ? "EDIT THREAD" : "WRITE THREAD",
                    CashEmoticonPageIndex = _compose.CashEmoticonPageIndex,
                    CashEmoticonPageCount = GetCashEmoticonPageCount(),
                    CashEmoticonOwnership = BuildCashEmoticonOwnershipSnapshot(),
                    SelectedEmoticon = CreateEmoticonSnapshot(_compose.EmoticonKind, _compose.EmoticonSlot, _compose.CashEmoticonPageIndex)
                },
                ReplyDraft = new GuildBbsReplyDraftSnapshot
                {
                    Body = _replyDraft.Body,
                    CashEmoticonPageIndex = _replyDraft.CashEmoticonPageIndex,
                    CashEmoticonPageCount = GetCashEmoticonPageCount(),
                    CashEmoticonOwnership = BuildCashEmoticonOwnershipSnapshot(),
                    SelectedEmoticon = CreateEmoticonSnapshot(_replyDraft.EmoticonKind, _replyDraft.EmoticonSlot, _replyDraft.CashEmoticonPageIndex)
                }
            };
        }

        public void SelectThread(int threadId)
        {
            IReadOnlyList<GuildBbsThreadState> orderedThreads = GetOrderedThreads();
            if (!orderedThreads.Any(thread => thread.ThreadId == threadId))
            {
                return;
            }

            _selectedThreadId = threadId;
            _commentPageIndex = 0;

            int threadIndex = orderedThreads
                .Select((thread, index) => new { thread.ThreadId, index })
                .First(entry => entry.ThreadId == threadId)
                .index;
            _threadPageIndex = threadIndex / VisibleThreadCount;
        }

        public string BeginWrite()
        {
            if (!CanWriteThread())
            {
                return "Guild BBS posting is locked for the current simulator guild role.";
            }

            IsWriteMode = true;
            _compose = CreateDraftFromContext();
            return "Guild BBS write mode opened.";
        }

        public string BeginEditSelected()
        {
            GuildBbsThreadState selectedThread = GetSelectedThread();
            if (selectedThread == null)
            {
                return "Select a Guild BBS thread before editing.";
            }

            if (!CanEditThread(selectedThread))
            {
                return "Only your own Guild BBS thread or an officer-moderated thread can be edited here.";
            }

            IsWriteMode = true;
            _compose = new GuildBbsComposeState
            {
                EditThreadId = selectedThread.ThreadId,
                Title = selectedThread.Title,
                Body = selectedThread.Body,
                IsNotice = selectedThread.IsNotice,
                EmoticonKind = selectedThread.EmoticonKind,
                EmoticonSlot = selectedThread.EmoticonSlot,
                CashEmoticonPageIndex = selectedThread.CashEmoticonPageIndex
            };
            NormalizeCashSelection(_compose);
            return $"Editing thread #{selectedThread.ThreadId}.";
        }

        public string CancelCompose()
        {
            if (!IsWriteMode)
            {
                return "Guild BBS write mode is not active.";
            }

            IsWriteMode = false;
            _compose = new GuildBbsComposeState();
            return "Guild BBS write mode closed.";
        }

        public string ToggleNotice()
        {
            if (IsWriteMode)
            {
                if (!_compose.IsNotice && !CanWriteNotice())
                {
                    return "Only simulated Jr. Master or Master roles can register a guild notice.";
                }

                if (!_compose.IsNotice && HasExistingNotice(_compose.EditThreadId))
                {
                    return "A guild notice is already registered. Remove it before drafting another notice.";
                }

                _compose.IsNotice = !_compose.IsNotice;
                return _compose.IsNotice
                    ? "Current Guild BBS draft is now marked as a notice."
                    : "Current Guild BBS draft is no longer marked as a notice.";
            }

            if (!CanWriteNotice())
            {
                return "Only simulated Jr. Master or Master roles can register a guild notice.";
            }

            if (HasExistingNotice())
            {
                return "A guild notice is already registered. Remove it before drafting another notice.";
            }

            IsWriteMode = true;
            _compose = CreateDraftFromContext();
            _compose.IsNotice = true;
            return "Guild BBS notice draft opened.";
        }

        public string SubmitCompose()
        {
            if (!IsWriteMode)
            {
                return "Guild BBS write mode is not active.";
            }

            string resolvedTitle = _compose.Title?.Trim() ?? string.Empty;
            string resolvedBody = _compose.Body?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resolvedTitle) || string.IsNullOrWhiteSpace(resolvedBody))
            {
                return GetEnterTextNotice();
            }

            if (_compose.IsNotice)
            {
                if (!CanWriteNotice())
                {
                    return "Only simulated Jr. Master or Master roles can register a guild notice.";
                }

                if (HasExistingNotice(_compose.EditThreadId))
                {
                    return "A guild notice is already registered. Remove it before drafting another notice.";
                }
            }

            if (_compose.EditThreadId > 0)
            {
                GuildBbsThreadState existingThread = _threads.FirstOrDefault(thread => thread.ThreadId == _compose.EditThreadId);
                if (existingThread == null)
                {
                    IsWriteMode = false;
                    _compose = new GuildBbsComposeState();
                    return "The selected Guild BBS thread no longer exists.";
                }

                if (!CanEditThread(existingThread))
                {
                    return "Only your own Guild BBS thread or an officer-moderated thread can be edited here.";
                }

                existingThread.Title = resolvedTitle;
                existingThread.Body = resolvedBody;
                existingThread.IsNotice = _compose.IsNotice;
                existingThread.CreatedAt = DateTimeOffset.Now;
                existingThread.EmoticonKind = _compose.EmoticonKind;
                existingThread.EmoticonSlot = _compose.EmoticonSlot;
                existingThread.CashEmoticonPageIndex = _compose.CashEmoticonPageIndex;
                _selectedThreadId = existingThread.ThreadId;
            }
            else
            {
                GuildBbsThreadState newThread = new GuildBbsThreadState
                {
                    ThreadId = _nextThreadId++,
                    Title = resolvedTitle,
                    Body = resolvedBody,
                    Author = _localPlayerName,
                    CreatedAt = DateTimeOffset.Now,
                    IsNotice = _compose.IsNotice,
                    EmoticonKind = _compose.EmoticonKind,
                    EmoticonSlot = _compose.EmoticonSlot,
                    CashEmoticonPageIndex = _compose.CashEmoticonPageIndex
                };
                _threads.Add(newThread);
                _selectedThreadId = newThread.ThreadId;
                _draftCounter++;
            }

            NotifySocialChatObserved(resolvedTitle, resolvedBody);
            IsWriteMode = false;
            _compose = new GuildBbsComposeState();
            SelectThread(_selectedThreadId);
            return "Guild BBS thread registered.";
        }

        public string DeleteSelectedThread()
        {
            GuildBbsThreadState selectedThread = GetSelectedThread();
            if (selectedThread == null)
            {
                return "Select a Guild BBS thread before deleting it.";
            }

            if (!CanDeleteThread(selectedThread))
            {
                return "Only your own Guild BBS thread or an officer-moderated thread can be deleted here.";
            }

            _threads.Remove(selectedThread);
            _selectedThreadId = 0;
            EnsureSelection(GetOrderedThreads());
            _commentPageIndex = 0;
            return $"Deleted Guild BBS thread #{selectedThread.ThreadId}.";
        }

        public string AddReply()
        {
            GuildBbsThreadState selectedThread = GetSelectedThread();
            if (selectedThread == null)
            {
                return "Select a Guild BBS thread before replying.";
            }

            if (!CanReplyToThread())
            {
                return "Guild BBS replies are locked for the current simulator guild role.";
            }

            string replyBody = _replyDraft.Body?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(replyBody))
            {
                return GetEnterTextNotice();
            }

            selectedThread.Comments.Add(new GuildBbsCommentState
            {
                CommentId = _nextCommentId++,
                Author = _localPlayerName,
                Body = replyBody,
                CreatedAt = DateTimeOffset.Now,
                EmoticonKind = _replyDraft.EmoticonKind,
                EmoticonSlot = _replyDraft.EmoticonSlot,
                CashEmoticonPageIndex = _replyDraft.CashEmoticonPageIndex
            });

            _replyDraft.Body = string.Empty;
            _replyDraft.EmoticonKind = GuildBbsEmoticonKind.None;
            _replyDraft.EmoticonSlot = -1;
            _replyDraft.CashEmoticonPageIndex = 0;

            IReadOnlyList<GuildBbsCommentState> orderedComments = selectedThread.Comments.OrderBy(comment => comment.CreatedAt).ToArray();
            _commentPageIndex = Math.Max(0, (orderedComments.Count - 1) / VisibleCommentCount);
            NotifySocialChatObserved(replyBody);
            return $"Added a Guild BBS reply to thread #{selectedThread.ThreadId}.";
        }

        public string DeleteLatestReply()
        {
            GuildBbsThreadState selectedThread = GetSelectedThread();
            if (selectedThread == null)
            {
                return "Select a Guild BBS thread before removing a reply.";
            }

            GuildBbsCommentState comment = selectedThread.Comments
                .LastOrDefault(CanDeleteComment);
            if (comment == null)
            {
                return "No deletable Guild BBS reply is available on the selected thread.";
            }

            selectedThread.Comments.Remove(comment);
            EnsureCommentPageInRange(selectedThread.Comments.Count);
            return $"Removed the latest Guild BBS reply from thread #{selectedThread.ThreadId}.";
        }

        public string DeleteReplyAtVisibleIndex(int visibleIndex)
        {
            GuildBbsThreadState selectedThread = GetSelectedThread();
            if (selectedThread == null)
            {
                return "Select a Guild BBS thread before removing a reply.";
            }

            if (visibleIndex < 0 || visibleIndex >= VisibleCommentCount)
            {
                return "That Guild BBS reply slot is outside the visible client comment range.";
            }

            IReadOnlyList<GuildBbsCommentState> orderedComments = selectedThread.Comments
                .OrderBy(comment => comment.CreatedAt)
                .ToArray();
            int commentIndex = (_commentPageIndex * VisibleCommentCount) + visibleIndex;
            if (commentIndex < 0 || commentIndex >= orderedComments.Count)
            {
                return "No Guild BBS reply is loaded in that visible client comment slot.";
            }

            GuildBbsCommentState comment = orderedComments[commentIndex];
            if (!CanDeleteComment(comment))
            {
                return "That Guild BBS reply cannot be deleted by the current simulator authority.";
            }

            selectedThread.Comments.Remove(comment);
            EnsureCommentPageInRange(selectedThread.Comments.Count);
            return $"Removed Guild BBS reply #{comment.CommentId} from thread #{selectedThread.ThreadId}.";
        }

        public string SetComposeTitle(string title)
        {
            _compose.Title = SanitizeSingleLineText(title, MaxTitleLength);
            return $"Compose title updated ({_compose.Title.Length}/{MaxTitleLength}).";
        }

        public string SetComposeBody(string body)
        {
            _compose.Body = SanitizeText(body, MaxThreadBodyLength);
            return $"Compose body updated ({_compose.Body.Length}/{MaxThreadBodyLength}).";
        }

        public string SetReplyDraft(string body)
        {
            _replyDraft.Body = SanitizeSingleLineText(body, MaxReplyBodyLength);
            return $"Reply draft updated ({_replyDraft.Body.Length}/{MaxReplyBodyLength}).";
        }

        public string MoveThreadPage(int delta)
        {
            int pageCount = Math.Max(1, (int)Math.Ceiling(_threads.Count / (double)VisibleThreadCount));
            int nextPage = Math.Clamp(_threadPageIndex + delta, 0, pageCount - 1);
            if (nextPage == _threadPageIndex)
            {
                return $"Guild BBS thread page {_threadPageIndex + 1}/{pageCount}.";
            }

            _threadPageIndex = nextPage;
            IReadOnlyList<GuildBbsThreadState> orderedThreads = GetOrderedThreads();
            GuildBbsThreadState firstThread = orderedThreads.Skip(_threadPageIndex * VisibleThreadCount).FirstOrDefault();
            if (firstThread != null)
            {
                _selectedThreadId = firstThread.ThreadId;
                _commentPageIndex = 0;
            }

            return $"Guild BBS thread page {_threadPageIndex + 1}/{pageCount}.";
        }

        public string SetThreadPage(int pageIndex)
        {
            int pageCount = Math.Max(1, (int)Math.Ceiling(_threads.Count / (double)VisibleThreadCount));
            int nextPage = Math.Clamp(pageIndex, 0, pageCount - 1);
            if (nextPage == _threadPageIndex)
            {
                return $"Guild BBS thread page {_threadPageIndex + 1}/{pageCount}.";
            }

            _threadPageIndex = nextPage;
            IReadOnlyList<GuildBbsThreadState> orderedThreads = GetOrderedThreads();
            GuildBbsThreadState firstThread = orderedThreads.Skip(_threadPageIndex * VisibleThreadCount).FirstOrDefault();
            if (firstThread != null)
            {
                _selectedThreadId = firstThread.ThreadId;
                _commentPageIndex = 0;
            }

            return $"Guild BBS thread page {_threadPageIndex + 1}/{pageCount}.";
        }

        public string MoveCommentPage(int delta)
        {
            GuildBbsThreadState selectedThread = GetSelectedThread();
            if (selectedThread == null)
            {
                return "Select a Guild BBS thread before paging its comments.";
            }

            int pageCount = Math.Max(1, (int)Math.Ceiling(selectedThread.Comments.Count / (double)VisibleCommentCount));
            int nextPage = Math.Clamp(_commentPageIndex + delta, 0, pageCount - 1);
            _commentPageIndex = nextPage;
            return $"Guild BBS comment page {_commentPageIndex + 1}/{pageCount}.";
        }

        public string SetCommentPage(int pageIndex)
        {
            GuildBbsThreadState selectedThread = GetSelectedThread();
            if (selectedThread == null)
            {
                return "Select a Guild BBS thread before paging its comments.";
            }

            int pageCount = Math.Max(1, (int)Math.Ceiling(selectedThread.Comments.Count / (double)VisibleCommentCount));
            _commentPageIndex = Math.Clamp(pageIndex, 0, pageCount - 1);
            return $"Guild BBS comment page {_commentPageIndex + 1}/{pageCount}.";
        }

        public string MoveComposeCashEmoticonPage(int delta)
        {
            int nextPage = Math.Clamp(_compose.CashEmoticonPageIndex + delta, 0, GetCashEmoticonPageCount() - 1);
            _compose.CashEmoticonPageIndex = nextPage;
            return $"Guild BBS compose cash emoticon page {_compose.CashEmoticonPageIndex + 1}/{GetCashEmoticonPageCount()}.";
        }

        public string MoveReplyCashEmoticonPage(int delta)
        {
            int nextPage = Math.Clamp(_replyDraft.CashEmoticonPageIndex + delta, 0, GetCashEmoticonPageCount() - 1);
            _replyDraft.CashEmoticonPageIndex = nextPage;
            return $"Guild BBS reply cash emoticon page {_replyDraft.CashEmoticonPageIndex + 1}/{GetCashEmoticonPageCount()}.";
        }

        public string SelectComposeEmoticon(GuildBbsEmoticonKind kind, int slotIndex, int cashPageIndex)
        {
            if (!TryResolveEmoticonSelection(kind, slotIndex, cashPageIndex, out GuildBbsEmoticonKind resolvedKind, out int resolvedSlot, out int resolvedPage))
            {
                _compose.EmoticonKind = GuildBbsEmoticonKind.None;
                _compose.EmoticonSlot = -1;
                return "Compose emoticon cleared.";
            }

            if (resolvedKind == GuildBbsEmoticonKind.Cash && !IsCashEmoticonOwned(resolvedSlot))
            {
                return $"Cash emoticon {resolvedSlot + 1} is not owned by the current Guild BBS entitlement source.";
            }

            _compose.EmoticonKind = resolvedKind;
            _compose.EmoticonSlot = resolvedSlot;
            _compose.CashEmoticonPageIndex = Math.Clamp(resolvedPage, 0, GetCashEmoticonPageCount() - 1);
            return $"Compose emoticon set to {DescribeEmoticon(resolvedKind, resolvedSlot, resolvedPage)}.";
        }

        public string SelectReplyEmoticon(GuildBbsEmoticonKind kind, int slotIndex, int cashPageIndex)
        {
            if (!TryResolveEmoticonSelection(kind, slotIndex, cashPageIndex, out GuildBbsEmoticonKind resolvedKind, out int resolvedSlot, out int resolvedPage))
            {
                _replyDraft.EmoticonKind = GuildBbsEmoticonKind.None;
                _replyDraft.EmoticonSlot = -1;
                return "Reply emoticon cleared.";
            }

            if (resolvedKind == GuildBbsEmoticonKind.Cash && !IsCashEmoticonOwned(resolvedSlot))
            {
                return $"Cash emoticon {resolvedSlot + 1} is not owned by the current Guild BBS entitlement source.";
            }

            _replyDraft.EmoticonKind = resolvedKind;
            _replyDraft.EmoticonSlot = resolvedSlot;
            _replyDraft.CashEmoticonPageIndex = Math.Clamp(resolvedPage, 0, GetCashEmoticonPageCount() - 1);
            return $"Reply emoticon set to {DescribeEmoticon(resolvedKind, resolvedSlot, resolvedPage)}.";
        }

        public string DescribeStatus()
        {
            GuildBbsThreadState selectedThread = GetOrderedThreads().FirstOrDefault(thread => thread.ThreadId == _selectedThreadId);
            string threadSummary = selectedThread == null
                ? "none"
                : $"#{selectedThread.ThreadId} \"{selectedThread.Title}\" ({selectedThread.Comments.Count} comment(s))";
            return $"Guild BBS: threads={_threads.Count}, threadPage={_threadPageIndex + 1}, commentPage={_commentPageIndex + 1}, selected={threadSummary}, mode={(IsWriteMode ? "write" : "read")}, guild={_guildName}, role={_guildRoleLabel}, authority={AuthoritySourceLabel} [{DescribePermissionMask(EffectivePermissionMask)}], cashEmoticons={OwnedCashEmoticonCount}/{_cashEmoticonCount} ({CashOwnershipSourceLabel})";
        }

        private GuildBbsComposeState CreateDraftFromContext()
        {
            return new GuildBbsComposeState
            {
                Title = $"{_guildName} Roll Call {_draftCounter}",
                Body = $"Field check from {_locationSummary}. Reply here so guild-room and board parity can be validated together.",
                IsNotice = false,
                CashEmoticonPageIndex = 0
            };
        }

        private GuildBbsThreadState GetSelectedThread()
        {
            return _threads.FirstOrDefault(thread => thread.ThreadId == _selectedThreadId);
        }

        private IReadOnlyList<GuildBbsThreadState> GetOrderedThreads()
        {
            return _threads
                .OrderByDescending(thread => thread.IsNotice)
                .ThenByDescending(thread => thread.CreatedAt)
                .ToArray();
        }

        private GuildBbsPermissionSnapshot BuildPermissionSnapshot(GuildBbsThreadState selectedThread)
        {
            return new GuildBbsPermissionSnapshot
            {
                PermissionLabel = _guildRoleLabel,
                CanWrite = CanWriteThread(),
                CanWriteNotice = CanWriteNotice(),
                CanReply = CanReplyToThread(),
                CanEditSelectedThread = selectedThread != null && CanEditThread(selectedThread),
                CanDeleteSelectedThread = selectedThread != null && CanDeleteThread(selectedThread),
                CanDeleteReply = selectedThread != null && selectedThread.Comments.Any(CanDeleteComment),
                OwnedCashEmoticonCount = OwnedCashEmoticonCount,
                AuthoritySourceLabel = AuthoritySourceLabel,
                CashOwnershipSourceLabel = CashOwnershipSourceLabel,
                PermissionMaskText = DescribePermissionMask(EffectivePermissionMask)
            };
        }

        private IReadOnlyList<bool> BuildCashEmoticonOwnershipSnapshot()
        {
            bool[] ownership = new bool[_cashEmoticonCount];
            for (int slotIndex = 0; slotIndex < _cashEmoticonCount; slotIndex++)
            {
                ownership[slotIndex] = IsCashEmoticonOwned(slotIndex);
            }

            return ownership;
        }

        private static GuildBbsThreadEntrySnapshot CreateThreadEntrySnapshot(GuildBbsThreadState thread)
        {
            if (thread == null)
            {
                return null;
            }

            return new GuildBbsThreadEntrySnapshot
            {
                ThreadId = thread.ThreadId,
                Title = thread.Title,
                Author = thread.Author,
                DateText = thread.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd"),
                CommentCount = thread.Comments.Count,
                IsNotice = thread.IsNotice,
                Emoticon = CreateEmoticonSnapshot(thread.EmoticonKind, thread.EmoticonSlot, thread.CashEmoticonPageIndex)
            };
        }

        private void EnsureSelection(IReadOnlyList<GuildBbsThreadState> orderedThreads)
        {
            if (_selectedThreadId > 0 && orderedThreads.Any(thread => thread.ThreadId == _selectedThreadId))
            {
                return;
            }

            GuildBbsThreadState selectedThread = orderedThreads.Skip(_threadPageIndex * VisibleThreadCount).FirstOrDefault()
                ?? orderedThreads.FirstOrDefault();
            _selectedThreadId = selectedThread?.ThreadId ?? 0;
        }

        private void EnsureThreadPageInRange(int totalThreadCount)
        {
            int maxPageIndex = Math.Max(0, (int)Math.Ceiling(totalThreadCount / (double)VisibleThreadCount) - 1);
            _threadPageIndex = Math.Clamp(_threadPageIndex, 0, maxPageIndex);
        }

        private void EnsureCommentPageInRange(int totalCommentCount)
        {
            int maxPageIndex = Math.Max(0, (int)Math.Ceiling(totalCommentCount / (double)VisibleCommentCount) - 1);
            _commentPageIndex = Math.Clamp(_commentPageIndex, 0, maxPageIndex);
        }

        private static string SanitizeText(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string trimmed = value.Replace("\r", string.Empty).TrimStart();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private static string SanitizeSingleLineText(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string singleLine = value
                .Replace("\r", string.Empty)
                .Replace("\n", " ")
                .TrimStart();
            return singleLine.Length <= maxLength ? singleLine : singleLine[..maxLength];
        }

        private bool TryResolveEmoticonSelection(
            GuildBbsEmoticonKind kind,
            int slotIndex,
            int cashPageIndex,
            out GuildBbsEmoticonKind resolvedKind,
            out int resolvedSlot,
            out int resolvedPage)
        {
            resolvedKind = GuildBbsEmoticonKind.None;
            resolvedSlot = -1;
            resolvedPage = 0;

            switch (kind)
            {
                case GuildBbsEmoticonKind.Basic:
                    if (slotIndex < 0 || slotIndex >= _basicEmoticonCount)
                    {
                        return false;
                    }

                    resolvedKind = GuildBbsEmoticonKind.Basic;
                    resolvedSlot = slotIndex;
                    return true;
                case GuildBbsEmoticonKind.Cash:
                    int resolvedPageIndex = Math.Max(0, cashPageIndex);
                    int absoluteSlotIndex = (resolvedPageIndex * VisibleCashEmoticonCount) + slotIndex;
                    if (slotIndex < 0 || slotIndex >= VisibleCashEmoticonCount || absoluteSlotIndex >= _cashEmoticonCount)
                    {
                        return false;
                    }

                    resolvedKind = GuildBbsEmoticonKind.Cash;
                    resolvedSlot = absoluteSlotIndex;
                    resolvedPage = resolvedPageIndex;
                    return true;
                default:
                    return false;
            }
        }

        private static GuildBbsEmoticonSnapshot CreateEmoticonSnapshot(GuildBbsEmoticonKind kind, int slotIndex, int cashPageIndex)
        {
            if (kind == GuildBbsEmoticonKind.None || slotIndex < 0)
            {
                return null;
            }

            return new GuildBbsEmoticonSnapshot
            {
                Kind = kind,
                SlotIndex = slotIndex,
                CashPageIndex = cashPageIndex,
                DisplayText = DescribeEmoticon(kind, slotIndex, cashPageIndex)
            };
        }

        private static string DescribeEmoticon(GuildBbsEmoticonKind kind, int slotIndex, int cashPageIndex)
        {
            return kind switch
            {
                GuildBbsEmoticonKind.Basic => $"Basic {slotIndex + 1}",
                GuildBbsEmoticonKind.Cash => $"Cash {(slotIndex / VisibleCashEmoticonCount) + 1}-{(slotIndex % VisibleCashEmoticonCount) + 1}",
                _ => "None"
            };
        }

        private bool CanWriteThread()
        {
            return EffectivePermissionMask.HasFlag(GuildBbsPermissionMask.WriteThread);
        }

        private bool CanWriteNotice()
        {
            return EffectivePermissionMask.HasFlag(GuildBbsPermissionMask.WriteNotice);
        }

        private bool CanReplyToThread()
        {
            return EffectivePermissionMask.HasFlag(GuildBbsPermissionMask.Reply);
        }

        private bool CanModerateAnyThread()
        {
            return EffectivePermissionMask.HasFlag(GuildBbsPermissionMask.Moderate);
        }

        private bool CanEditThread(GuildBbsThreadState thread)
        {
            return thread != null
                && ((IsLocalAuthor(thread.Author) && EffectivePermissionMask.HasFlag(GuildBbsPermissionMask.OwnThread))
                    || CanModerateAnyThread());
        }

        private bool CanDeleteThread(GuildBbsThreadState thread)
        {
            return thread != null
                && ((IsLocalAuthor(thread.Author) && EffectivePermissionMask.HasFlag(GuildBbsPermissionMask.OwnThread))
                    || CanModerateAnyThread());
        }

        private bool CanDeleteComment(GuildBbsCommentState comment)
        {
            return comment != null
                && ((IsLocalAuthor(comment.Author) && EffectivePermissionMask.HasFlag(GuildBbsPermissionMask.OwnThread))
                    || CanModerateAnyThread());
        }

        private bool IsLocalAuthor(string author)
        {
            return string.Equals(author, _localPlayerName, StringComparison.OrdinalIgnoreCase);
        }

        private bool HasExistingNotice(int excludedThreadId = 0)
        {
            return _threads.Any(thread => thread.IsNotice && thread.ThreadId != excludedThreadId);
        }

        private int GetCashEmoticonPageCount()
        {
            return Math.Max(1, (int)Math.Ceiling(_cashEmoticonCount / (double)VisibleCashEmoticonCount));
        }

        private bool IsCashEmoticonOwned(int slotIndex)
        {
            int itemId = CashEmoticonItemIdStart + slotIndex;
            return EffectiveOwnedCashEmoticonIds.Contains(itemId);
        }

        private void NormalizeDraftState()
        {
            NormalizeCashSelection(_compose);
            NormalizeCashSelection(_replyDraft);
        }

        private void NormalizeCashSelection(GuildBbsComposeState compose)
        {
            compose.CashEmoticonPageIndex = Math.Clamp(compose.CashEmoticonPageIndex, 0, GetCashEmoticonPageCount() - 1);
            if (compose.EmoticonKind == GuildBbsEmoticonKind.Cash && !IsCashEmoticonOwned(compose.EmoticonSlot))
            {
                compose.EmoticonKind = GuildBbsEmoticonKind.None;
                compose.EmoticonSlot = -1;
                compose.CashEmoticonPageIndex = 0;
            }
        }

        private void NormalizeCashSelection(GuildBbsReplyDraftState replyDraft)
        {
            replyDraft.CashEmoticonPageIndex = Math.Clamp(replyDraft.CashEmoticonPageIndex, 0, GetCashEmoticonPageCount() - 1);
            if (replyDraft.EmoticonKind == GuildBbsEmoticonKind.Cash && !IsCashEmoticonOwned(replyDraft.EmoticonSlot))
            {
                replyDraft.EmoticonKind = GuildBbsEmoticonKind.None;
                replyDraft.EmoticonSlot = -1;
                replyDraft.CashEmoticonPageIndex = 0;
            }
        }

        private static GuildBbsPermissionLevel ResolvePermissionLevel(string guildRoleLabel)
        {
            if (string.IsNullOrWhiteSpace(guildRoleLabel))
            {
                return GuildBbsPermissionLevel.Member;
            }

            string normalized = guildRoleLabel.Trim().ToLowerInvariant();
            if (normalized.Contains("master"))
            {
                return normalized.Contains("jr")
                    ? GuildBbsPermissionLevel.JrMaster
                    : GuildBbsPermissionLevel.Master;
            }

            if (normalized.Contains("representative"))
            {
                return GuildBbsPermissionLevel.JrMaster;
            }

            if (normalized.Contains("member"))
            {
                return GuildBbsPermissionLevel.Member;
            }

            return GuildBbsPermissionLevel.Member;
        }

        private GuildBbsPermissionMask EffectivePermissionMask => NormalizePermissionMask(_packetPermissionMask ?? _rolePermissionMask);

        private IReadOnlyCollection<int> EffectiveOwnedCashEmoticonIds =>
            _hasPacketCashOwnershipOverride
                ? _packetOwnedCashEmoticonIds
                : _inventoryOwnedCashEmoticonIds;
        private int OwnedCashEmoticonCount => EffectiveOwnedCashEmoticonIds.Count;
        private string AuthoritySourceLabel => _packetPermissionMask.HasValue ? "Packet" : "Guild role";
        private string CashOwnershipSourceLabel => _hasPacketCashOwnershipOverride ? "Packet" : "Inventory";
        private static GuildBbsPermissionMask ResolvePermissionMask(GuildBbsPermissionLevel permissionLevel)
        {
            return permissionLevel switch
            {
                GuildBbsPermissionLevel.Master => SupportedPermissionMask,
                GuildBbsPermissionLevel.JrMaster => SupportedPermissionMask,
                GuildBbsPermissionLevel.Member => GuildBbsPermissionMask.WriteThread | GuildBbsPermissionMask.Reply | GuildBbsPermissionMask.OwnThread,
                _ => GuildBbsPermissionMask.None
            };
        }

        private static GuildBbsPermissionMask NormalizePermissionMask(GuildBbsPermissionMask mask)
        {
            return mask & SupportedPermissionMask;
        }

        private static string DescribePermissionMask(GuildBbsPermissionMask mask)
        {
            if (mask == GuildBbsPermissionMask.None)
            {
                return "none";
            }

            var names = new List<string>(5);
            if (mask.HasFlag(GuildBbsPermissionMask.WriteThread))
            {
                names.Add("write");
            }

            if (mask.HasFlag(GuildBbsPermissionMask.WriteNotice))
            {
                names.Add("notice");
            }

            if (mask.HasFlag(GuildBbsPermissionMask.Reply))
            {
                names.Add("reply");
            }

            if (mask.HasFlag(GuildBbsPermissionMask.OwnThread))
            {
                names.Add("own-thread");
            }

            if (mask.HasFlag(GuildBbsPermissionMask.Moderate))
            {
                names.Add("moderate");
            }

            return string.Join(", ", names);
        }

        private bool TryDecodePermissionPacket(byte[] payload, out GuildBbsPermissionMask mask, out string detail)
        {
            mask = GuildBbsPermissionMask.None;
            detail = null;
            if (payload == null || payload.Length == 0)
            {
                detail = "Guild BBS authority packet payload is empty.";
                return false;
            }

            if (TryFindPermissionMaskCandidate(payload, out GuildBbsPermissionMask decodedMask, out string decodedDetail))
            {
                mask = decodedMask;
                detail = decodedDetail;
                return true;
            }

            mask = NormalizePermissionMask((GuildBbsPermissionMask)payload[0]);
            detail = $"Fell back to authority mask byte 0x{payload[0]:X2} at offset 0 because no exact packet-shaped mask field was found.";
            return true;
        }

        private bool TryFindPermissionMaskCandidate(byte[] payload, out GuildBbsPermissionMask mask, out string detail)
        {
            mask = GuildBbsPermissionMask.None;
            detail = null;

            for (int offset = 0; offset < payload.Length; offset++)
            {
                if (TryResolvePermissionMaskCandidate(payload[offset], out GuildBbsPermissionMask byteMask))
                {
                    mask = byteMask;
                    detail = $"Decoded authority mask byte 0x{payload[offset]:X2} at offset {offset}.";
                    return true;
                }
            }

            for (int offset = 0; offset <= payload.Length - sizeof(short); offset++)
            {
                ushort candidate = BitConverter.ToUInt16(payload, offset);
                if (TryResolvePermissionMaskCandidate(candidate, out GuildBbsPermissionMask shortMask))
                {
                    mask = shortMask;
                    detail = $"Decoded authority mask ushort 0x{candidate:X4} at offset {offset}.";
                    return true;
                }
            }

            for (int offset = 0; offset <= payload.Length - sizeof(int); offset++)
            {
                uint candidate = BitConverter.ToUInt32(payload, offset);
                if (TryResolvePermissionMaskCandidate(candidate, out GuildBbsPermissionMask intMask))
                {
                    mask = intMask;
                    detail = $"Decoded authority mask uint 0x{candidate:X8} at offset {offset}.";
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolvePermissionMaskCandidate(uint rawValue, out GuildBbsPermissionMask mask)
        {
            mask = GuildBbsPermissionMask.None;
            if (rawValue > (uint)SupportedPermissionMask)
            {
                return false;
            }

            mask = NormalizePermissionMask((GuildBbsPermissionMask)rawValue);
            return true;
        }

        private bool TryDecodeCashOwnershipPacket(byte[] payload, out HashSet<int> ownedItemIds, out string detail)
        {
            ownedItemIds = new HashSet<int>();
            detail = null;
            if (payload == null || payload.Length == 0)
            {
                detail = "Guild BBS cash-entitlement packet payload is empty.";
                return false;
            }

            foreach (byte value in payload)
            {
                if (TryResolvePacketCashItemId(value, out int byteItemId))
                {
                    ownedItemIds.Add(byteItemId);
                }
            }

            for (int offset = 0; offset <= payload.Length - sizeof(short); offset++)
            {
                short candidate = BitConverter.ToInt16(payload, offset);
                if (TryResolvePacketCashItemId(candidate, out int shortItemId))
                {
                    ownedItemIds.Add(shortItemId);
                }
            }

            for (int offset = 0; offset <= payload.Length - sizeof(int); offset++)
            {
                int candidate = BitConverter.ToInt32(payload, offset);
                if (TryResolvePacketCashItemId(candidate, out int intItemId))
                {
                    ownedItemIds.Add(intItemId);
                }
            }

            detail = ownedItemIds.Count == 0
                ? "Decoded Guild BBS cash-entitlement packet with no owned emoticons."
                : $"Decoded {ownedItemIds.Count} Guild BBS cash emoticon entitlement(s).";
            return true;
        }

        private bool TryResolvePacketCashItemId(int rawValue, out int itemId)
        {
            itemId = 0;
            if (rawValue >= CashEmoticonItemIdStart && rawValue < CashEmoticonItemIdStart + _cashEmoticonCount)
            {
                itemId = rawValue;
                return true;
            }

            int slotIndex = rawValue - ClientCashEmoticonIdStart;
            if (slotIndex >= 0 && slotIndex < _cashEmoticonCount)
            {
                itemId = CashEmoticonItemIdStart + slotIndex;
                return true;
            }

            return false;
        }

        private static string GetEnterTextNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                EnterTextStringPoolId,
                "Please enter the text.",
                appendFallbackSuffix: false,
                minimumHexWidth: 0);
        }

        private void SeedDefaultThreads()
        {
            GuildBbsThreadState welcomeThread = AddThread(
                "Welcome to the guild board",
                "This simulator-owned board mirrors the dedicated Guild BBS surface instead of collapsing guild traffic into chat alone.",
                "Shanks",
                DateTimeOffset.Now.AddDays(-4),
                isNotice: true,
                GuildBbsEmoticonKind.Basic,
                0,
                0);
            welcomeThread.Comments.Add(new GuildBbsCommentState
            {
                CommentId = _nextCommentId++,
                Author = "Targa",
                Body = "Use this thread to verify board selection and notice rendering.",
                CreatedAt = DateTimeOffset.Now.AddDays(-3).AddHours(2),
                EmoticonKind = GuildBbsEmoticonKind.Basic,
                EmoticonSlot = 1
            });

            GuildBbsThreadState parityThread = AddThread(
                "Guild BBS parity checklist",
                "Reply here after testing thread selection, write mode, comment rows, and keyboard text entry against the dedicated board layout.",
                "Rondo",
                DateTimeOffset.Now.AddDays(-2),
                isNotice: false,
                GuildBbsEmoticonKind.Cash,
                2,
                0);
            parityThread.Comments.Add(new GuildBbsCommentState
            {
                CommentId = _nextCommentId++,
                Author = "Rin",
                Body = "Date labels and author columns now have a dedicated board owner.",
                CreatedAt = DateTimeOffset.Now.AddDays(-2).AddHours(1),
                EmoticonKind = GuildBbsEmoticonKind.Cash,
                EmoticonSlot = 4,
                CashEmoticonPageIndex = 0
            });

            AddThread(
                "Zakum sign-up",
                "Post your preferred channel and ready state before moving to El Nath.",
                "Aria",
                DateTimeOffset.Now.AddHours(-18),
                isNotice: false,
                GuildBbsEmoticonKind.None,
                -1,
                0);
        }

        private GuildBbsThreadState AddThread(
            string title,
            string body,
            string author,
            DateTimeOffset createdAt,
            bool isNotice,
            GuildBbsEmoticonKind emoticonKind,
            int emoticonSlot,
            int cashEmoticonPageIndex)
        {
            var thread = new GuildBbsThreadState
            {
                ThreadId = _nextThreadId++,
                Title = title,
                Body = body,
                Author = author,
                CreatedAt = createdAt,
                IsNotice = isNotice,
                EmoticonKind = emoticonKind,
                EmoticonSlot = emoticonSlot,
                CashEmoticonPageIndex = cashEmoticonPageIndex
            };
            _threads.Add(thread);
            if (_selectedThreadId == 0)
            {
                _selectedThreadId = thread.ThreadId;
            }

            return thread;
        }

        private void NotifySocialChatObserved(params string[] messages)
        {
            if (messages == null || messages.Length == 0)
            {
                return;
            }

            HashSet<string> observed = null;
            int tickCount = Environment.TickCount;
            foreach (string message in messages)
            {
                string normalized = message?.Trim();
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                observed ??= new HashSet<string>(StringComparer.Ordinal);
                if (!observed.Add(normalized))
                {
                    continue;
                }

                SocialChatObserved?.Invoke(normalized, tickCount);
            }
        }
    }

    internal sealed class GuildBbsSnapshot
    {
        public string GuildName { get; init; } = string.Empty;
        public string LocalPlayerName { get; init; } = string.Empty;
        public string GuildRoleLabel { get; init; } = string.Empty;
        public bool IsWriteMode { get; init; }
        public int SelectedThreadId { get; init; }
        public int TotalThreadCount { get; init; }
        public int ThreadPageIndex { get; init; }
        public int ThreadPageCount { get; init; }
        public IReadOnlyList<GuildBbsThreadEntrySnapshot> Threads { get; init; } = Array.Empty<GuildBbsThreadEntrySnapshot>();
        public GuildBbsThreadEntrySnapshot NoticeThread { get; init; }
        public GuildBbsThreadSnapshot SelectedThread { get; init; }
        public GuildBbsPermissionSnapshot Permission { get; init; } = new();
        public GuildBbsComposeSnapshot Compose { get; init; } = new();
        public GuildBbsReplyDraftSnapshot ReplyDraft { get; init; } = new();
    }

    internal sealed class GuildBbsThreadEntrySnapshot
    {
        public int ThreadId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public string DateText { get; init; } = string.Empty;
        public int CommentCount { get; init; }
        public bool IsNotice { get; init; }
        public GuildBbsEmoticonSnapshot Emoticon { get; init; }
    }

    internal sealed class GuildBbsThreadSnapshot
    {
        public int ThreadId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public string DateText { get; init; } = string.Empty;
        public bool IsNotice { get; init; }
        public GuildBbsEmoticonSnapshot Emoticon { get; init; }
        public bool CanEdit { get; init; }
        public bool CanDelete { get; init; }
        public int TotalCommentCount { get; init; }
        public int CommentPageIndex { get; init; }
        public int CommentPageCount { get; init; }
        public IReadOnlyList<GuildBbsCommentSnapshot> Comments { get; init; } = Array.Empty<GuildBbsCommentSnapshot>();
    }

    internal sealed class GuildBbsCommentSnapshot
    {
        public int CommentId { get; init; }
        public string Author { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string DateText { get; init; } = string.Empty;
        public GuildBbsEmoticonSnapshot Emoticon { get; init; }
        public bool CanDelete { get; init; }
    }

    internal sealed class GuildBbsComposeSnapshot
    {
        public string Title { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public bool IsNotice { get; init; }
        public string ModeText { get; init; } = string.Empty;
        public int CashEmoticonPageIndex { get; init; }
        public int CashEmoticonPageCount { get; init; }
        public IReadOnlyList<bool> CashEmoticonOwnership { get; init; } = Array.Empty<bool>();
        public GuildBbsEmoticonSnapshot SelectedEmoticon { get; init; }
    }

    internal sealed class GuildBbsReplyDraftSnapshot
    {
        public string Body { get; init; } = string.Empty;
        public int CashEmoticonPageIndex { get; init; }
        public int CashEmoticonPageCount { get; init; }
        public IReadOnlyList<bool> CashEmoticonOwnership { get; init; } = Array.Empty<bool>();
        public GuildBbsEmoticonSnapshot SelectedEmoticon { get; init; }
    }

    internal sealed class GuildBbsPermissionSnapshot
    {
        public string PermissionLabel { get; init; } = string.Empty;
        public string AuthoritySourceLabel { get; init; } = string.Empty;
        public string CashOwnershipSourceLabel { get; init; } = string.Empty;
        public string PermissionMaskText { get; init; } = string.Empty;
        public bool CanWrite { get; init; }
        public bool CanWriteNotice { get; init; }
        public bool CanReply { get; init; }
        public bool CanEditSelectedThread { get; init; }
        public bool CanDeleteSelectedThread { get; init; }
        public bool CanDeleteReply { get; init; }
        public int OwnedCashEmoticonCount { get; init; }
    }

    internal sealed class GuildBbsEmoticonSnapshot
    {
        public GuildBbsEmoticonKind Kind { get; init; }
        public int SlotIndex { get; init; }
        public int CashPageIndex { get; init; }
        public string DisplayText { get; init; } = string.Empty;
    }
}
