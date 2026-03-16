using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class GuildBbsRuntime
    {
        private sealed class GuildBbsCommentState
        {
            public int CommentId { get; init; }
            public string Author { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public DateTimeOffset CreatedAt { get; set; }
        }

        private sealed class GuildBbsThreadState
        {
            public int ThreadId { get; init; }
            public string Title { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public string Author { get; set; } = string.Empty;
            public DateTimeOffset CreatedAt { get; set; }
            public bool IsNotice { get; set; }
            public List<GuildBbsCommentState> Comments { get; } = new();
        }

        private sealed class GuildBbsComposeState
        {
            public int EditThreadId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public bool IsNotice { get; set; }
        }

        private readonly List<GuildBbsThreadState> _threads = new();
        private GuildBbsComposeState _compose = new();
        private string _localPlayerName = "Player";
        private string _guildName = "Maple Guild";
        private string _locationSummary = "Field";
        private int _selectedThreadId;
        private int _nextThreadId = 1;
        private int _nextCommentId = 1;
        private int _draftCounter = 1;

        public GuildBbsRuntime()
        {
            SeedDefaultThreads();
        }

        public bool IsWriteMode { get; private set; }

        public void UpdateLocalContext(string playerName, string guildName, string locationSummary)
        {
            _localPlayerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
            _guildName = string.IsNullOrWhiteSpace(guildName) ? "Maple Guild" : guildName.Trim();
            _locationSummary = string.IsNullOrWhiteSpace(locationSummary) ? "Field" : locationSummary.Trim();
        }

        public GuildBbsSnapshot BuildSnapshot()
        {
            IReadOnlyList<GuildBbsThreadState> orderedThreads = GetOrderedThreads();
            EnsureSelection(orderedThreads);

            GuildBbsThreadState selectedThread = orderedThreads.FirstOrDefault(thread => thread.ThreadId == _selectedThreadId);
            return new GuildBbsSnapshot
            {
                GuildName = _guildName,
                LocalPlayerName = _localPlayerName,
                IsWriteMode = IsWriteMode,
                SelectedThreadId = _selectedThreadId,
                Threads = orderedThreads
                    .Select(thread => new GuildBbsThreadEntrySnapshot
                    {
                        ThreadId = thread.ThreadId,
                        Title = thread.Title,
                        Author = thread.Author,
                        DateText = thread.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd"),
                        CommentCount = thread.Comments.Count,
                        IsNotice = thread.IsNotice
                    })
                    .ToArray(),
                SelectedThread = selectedThread == null
                    ? null
                    : new GuildBbsThreadSnapshot
                    {
                        ThreadId = selectedThread.ThreadId,
                        Title = selectedThread.Title,
                        Body = selectedThread.Body,
                        Author = selectedThread.Author,
                        DateText = selectedThread.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm"),
                        IsNotice = selectedThread.IsNotice,
                        Comments = selectedThread.Comments
                            .OrderBy(comment => comment.CreatedAt)
                            .Select(comment => new GuildBbsCommentSnapshot
                            {
                                CommentId = comment.CommentId,
                                Author = comment.Author,
                                Body = comment.Body,
                                DateText = comment.CreatedAt.ToLocalTime().ToString("MM.dd HH:mm")
                            })
                            .ToArray()
                    },
                Compose = new GuildBbsComposeSnapshot
                {
                    Title = _compose.Title,
                    Body = _compose.Body,
                    IsNotice = _compose.IsNotice,
                    ModeText = _compose.EditThreadId > 0 ? "EDIT THREAD" : "WRITE THREAD"
                }
            };
        }

        public void SelectThread(int threadId)
        {
            if (_threads.Any(thread => thread.ThreadId == threadId))
            {
                _selectedThreadId = threadId;
            }
        }

        public string BeginWrite()
        {
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

            IsWriteMode = true;
            _compose = new GuildBbsComposeState
            {
                EditThreadId = selectedThread.ThreadId,
                Title = selectedThread.Title,
                Body = selectedThread.Body,
                IsNotice = selectedThread.IsNotice
            };
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
                _compose.IsNotice = !_compose.IsNotice;
                return _compose.IsNotice
                    ? "Current Guild BBS draft is now marked as a notice."
                    : "Current Guild BBS draft is no longer marked as a notice.";
            }

            GuildBbsThreadState selectedThread = GetSelectedThread();
            if (selectedThread == null)
            {
                return "Select a Guild BBS thread before toggling notice state.";
            }

            selectedThread.IsNotice = !selectedThread.IsNotice;
            return selectedThread.IsNotice
                ? $"Thread #{selectedThread.ThreadId} promoted to guild notice."
                : $"Thread #{selectedThread.ThreadId} removed from guild notice.";
        }

        public string SubmitCompose()
        {
            if (!IsWriteMode)
            {
                return "Guild BBS write mode is not active.";
            }

            string resolvedTitle = string.IsNullOrWhiteSpace(_compose.Title) ? $"Guild update {_draftCounter}" : _compose.Title.Trim();
            string resolvedBody = string.IsNullOrWhiteSpace(_compose.Body)
                ? $"Posting from {_locationSummary} for {_guildName} parity checks."
                : _compose.Body.Trim();

            if (_compose.EditThreadId > 0)
            {
                GuildBbsThreadState existingThread = _threads.FirstOrDefault(thread => thread.ThreadId == _compose.EditThreadId);
                if (existingThread == null)
                {
                    IsWriteMode = false;
                    _compose = new GuildBbsComposeState();
                    return "The selected Guild BBS thread no longer exists.";
                }

                existingThread.Title = resolvedTitle;
                existingThread.Body = resolvedBody;
                existingThread.IsNotice = _compose.IsNotice;
                existingThread.CreatedAt = DateTimeOffset.Now;
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
                    IsNotice = _compose.IsNotice
                };
                _threads.Add(newThread);
                _selectedThreadId = newThread.ThreadId;
                _draftCounter++;
            }

            IsWriteMode = false;
            _compose = new GuildBbsComposeState();
            return "Guild BBS thread registered.";
        }

        public string DeleteSelectedThread()
        {
            GuildBbsThreadState selectedThread = GetSelectedThread();
            if (selectedThread == null)
            {
                return "Select a Guild BBS thread before deleting it.";
            }

            _threads.Remove(selectedThread);
            _selectedThreadId = 0;
            EnsureSelection(GetOrderedThreads());
            return $"Deleted Guild BBS thread #{selectedThread.ThreadId}.";
        }

        public string AddReply()
        {
            GuildBbsThreadState selectedThread = GetSelectedThread();
            if (selectedThread == null)
            {
                return "Select a Guild BBS thread before replying.";
            }

            selectedThread.Comments.Add(new GuildBbsCommentState
            {
                CommentId = _nextCommentId++,
                Author = _localPlayerName,
                Body = $"Checked in from {_locationSummary}.",
                CreatedAt = DateTimeOffset.Now
            });
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
                .LastOrDefault(entry => string.Equals(entry.Author, _localPlayerName, StringComparison.OrdinalIgnoreCase));
            if (comment == null)
            {
                return "No simulator-owned Guild BBS reply is available to delete on the selected thread.";
            }

            selectedThread.Comments.Remove(comment);
            return $"Removed the latest Guild BBS reply from thread #{selectedThread.ThreadId}.";
        }

        public string DescribeStatus()
        {
            GuildBbsThreadState selectedThread = GetOrderedThreads().FirstOrDefault(thread => thread.ThreadId == _selectedThreadId);
            string threadSummary = selectedThread == null
                ? "none"
                : $"#{selectedThread.ThreadId} \"{selectedThread.Title}\" ({selectedThread.Comments.Count} comment(s))";
            return $"Guild BBS: threads={_threads.Count}, selected={threadSummary}, mode={(IsWriteMode ? "write" : "read")}, guild={_guildName}";
        }

        private GuildBbsComposeState CreateDraftFromContext()
        {
            return new GuildBbsComposeState
            {
                Title = $"{_guildName} Roll Call {_draftCounter}",
                Body = $"Field check from {_locationSummary}. Reply here so guild-room and board parity can be validated together.",
                IsNotice = false
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

        private void EnsureSelection(IReadOnlyList<GuildBbsThreadState> orderedThreads)
        {
            if (_selectedThreadId > 0 && orderedThreads.Any(thread => thread.ThreadId == _selectedThreadId))
            {
                return;
            }

            _selectedThreadId = orderedThreads.FirstOrDefault()?.ThreadId ?? 0;
        }

        private void SeedDefaultThreads()
        {
            GuildBbsThreadState welcomeThread = AddThread(
                "Welcome to the guild board",
                "This simulator-owned board mirrors the dedicated Guild BBS surface instead of collapsing guild traffic into chat alone.",
                "Shanks",
                DateTimeOffset.Now.AddDays(-4),
                isNotice: true);
            welcomeThread.Comments.Add(new GuildBbsCommentState
            {
                CommentId = _nextCommentId++,
                Author = "Targa",
                Body = "Use this thread to verify board selection and notice rendering.",
                CreatedAt = DateTimeOffset.Now.AddDays(-3).AddHours(2)
            });

            GuildBbsThreadState parityThread = AddThread(
                "Guild BBS parity checklist",
                "Reply here after testing thread selection, write mode, and comment rows against the client-backed layout.",
                "Rondo",
                DateTimeOffset.Now.AddDays(-2),
                isNotice: false);
            parityThread.Comments.Add(new GuildBbsCommentState
            {
                CommentId = _nextCommentId++,
                Author = "Rin",
                Body = "Date labels and author columns now have a dedicated board owner.",
                CreatedAt = DateTimeOffset.Now.AddDays(-2).AddHours(1)
            });

            AddThread(
                "Zakum sign-up",
                "Post your preferred channel and ready state before moving to El Nath.",
                "Aria",
                DateTimeOffset.Now.AddHours(-18),
                isNotice: false);
        }

        private GuildBbsThreadState AddThread(string title, string body, string author, DateTimeOffset createdAt, bool isNotice)
        {
            var thread = new GuildBbsThreadState
            {
                ThreadId = _nextThreadId++,
                Title = title,
                Body = body,
                Author = author,
                CreatedAt = createdAt,
                IsNotice = isNotice
            };
            _threads.Add(thread);
            if (_selectedThreadId == 0)
            {
                _selectedThreadId = thread.ThreadId;
            }

            return thread;
        }
    }

    internal sealed class GuildBbsSnapshot
    {
        public string GuildName { get; init; } = string.Empty;
        public string LocalPlayerName { get; init; } = string.Empty;
        public bool IsWriteMode { get; init; }
        public int SelectedThreadId { get; init; }
        public IReadOnlyList<GuildBbsThreadEntrySnapshot> Threads { get; init; } = Array.Empty<GuildBbsThreadEntrySnapshot>();
        public GuildBbsThreadSnapshot SelectedThread { get; init; }
        public GuildBbsComposeSnapshot Compose { get; init; } = new();
    }

    internal sealed class GuildBbsThreadEntrySnapshot
    {
        public int ThreadId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public string DateText { get; init; } = string.Empty;
        public int CommentCount { get; init; }
        public bool IsNotice { get; init; }
    }

    internal sealed class GuildBbsThreadSnapshot
    {
        public int ThreadId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public string DateText { get; init; } = string.Empty;
        public bool IsNotice { get; init; }
        public IReadOnlyList<GuildBbsCommentSnapshot> Comments { get; init; } = Array.Empty<GuildBbsCommentSnapshot>();
    }

    internal sealed class GuildBbsCommentSnapshot
    {
        public int CommentId { get; init; }
        public string Author { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string DateText { get; init; } = string.Empty;
    }

    internal sealed class GuildBbsComposeSnapshot
    {
        public string Title { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public bool IsNotice { get; init; }
        public string ModeText { get; init; } = string.Empty;
    }
}
