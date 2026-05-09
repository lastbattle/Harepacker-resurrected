using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.UI
{
    internal enum SharedFadeYesNoModalType
    {
        MessengerInvite = 0,
        FriendRegister = 1,
        TradeInvite = 2,
        CashTradeInvite = 3,
        NewMemo = 4,
        PartyInvite = 5,
        AllianceInvite = 6,
        QuestClear = 7,
        GuildInvite = 8,
        UserAlarm = 9,
        ParcelAlarm = 10,
        PartyQuestAlarm = 11,
        FamilyInvite = 12,
        PartyApply = 13,
        ExpeditionInvite = 14,
        ExpeditionApply = 15,
        FollowRequest = 16,
        NewYearCardArrived = 17,
        Generic = 1000
    }

    internal enum SharedFadeYesNoModalPhase
    {
        Closed = 0,
        FadingIn = 1,
        Visible = 2,
        FadingOut = 3
    }

    internal enum SharedFadeYesNoModalButton
    {
        Ok = 2000,
        Cancel = 2001
    }

    internal readonly record struct SharedFadeYesNoButtonLayout(
        int OkId,
        int CancelId,
        int ButtonX,
        int OkY,
        int CancelY,
        bool ShowsOkButton);

    internal readonly record struct SharedFadeYesNoVisualProfile(
        string FrameName,
        string IconName,
        int NativeWidth,
        int NativeHeight,
        int AnchorX,
        int BottomOffset,
        int IconX,
        int IconCenterHeight,
        bool UsesBlackText = false,
        bool SuppressesIcon = false);

    internal sealed record SharedFadeYesNoModalRequest(
        SharedFadeYesNoModalType Type,
        string Title,
        string Body,
        string Footer = "",
        int LifetimeMilliseconds = SharedFadeYesNoModalOwner.DefaultLifetimeMilliseconds,
        int StackIndex = 0,
        bool QuickDelivery = false,
        InGameConfirmDialogPresentation Presentation = null);

    internal sealed record SharedFadeYesNoModalSnapshot(
        bool IsActive,
        SharedFadeYesNoModalType Type,
        SharedFadeYesNoModalPhase Phase,
        int StackIndex,
        int PendingCount,
        int LifetimeMilliseconds,
        int CreatedTick,
        int FadePhaseTick,
        string DrawRoute,
        SharedFadeYesNoButtonLayout ButtonLayout,
        bool OnceClicked,
        bool QuickDelivery,
        string Title,
        string Body,
        string Footer,
        InGameConfirmDialogPresentation Presentation);

    internal sealed class SharedFadeYesNoModalOwner
    {
        internal const int OkButtonId = (int)SharedFadeYesNoModalButton.Ok;
        internal const int CancelButtonId = (int)SharedFadeYesNoModalButton.Cancel;
        internal const int DefaultLifetimeMilliseconds = 6000;
        internal const int FadeInMilliseconds = 120;
        internal const int FadeOutMilliseconds = 120;
        internal const int InviteAnchorX = 389;
        internal const int AlarmAnchorX = 440;
        internal const int ExpeditionInviteAnchorX = 349;
        internal const int InviteBottomOffset = 113;
        internal const int AlarmBottomOffset = 97;
        internal const int PartyQuestBottomOffset = 107;
        internal const int StackStep = 5;

        private static readonly ISet<SharedFadeYesNoModalType> WideButtonTypes = new HashSet<SharedFadeYesNoModalType>
        {
            SharedFadeYesNoModalType.ExpeditionApply,
            SharedFadeYesNoModalType.FamilyInvite,
            SharedFadeYesNoModalType.GuildInvite,
            SharedFadeYesNoModalType.FriendRegister,
            SharedFadeYesNoModalType.PartyApply,
            SharedFadeYesNoModalType.AllianceInvite,
            SharedFadeYesNoModalType.MessengerInvite,
            SharedFadeYesNoModalType.TradeInvite,
            SharedFadeYesNoModalType.PartyInvite,
            SharedFadeYesNoModalType.FollowRequest
        };

        private SharedFadeYesNoModalRequest _activeRequest;
        private readonly Queue<SharedFadeYesNoModalRequest> _pendingRequests = new();
        private SharedFadeYesNoModalPhase _phase = SharedFadeYesNoModalPhase.Closed;
        private int _createdTick = int.MinValue;
        private int _phaseTick = int.MinValue;
        private bool _onceClicked;

        internal bool IsActive => _activeRequest != null && _phase != SharedFadeYesNoModalPhase.Closed;
        internal SharedFadeYesNoModalType ActiveType => _activeRequest?.Type ?? SharedFadeYesNoModalType.Generic;
        internal int ActiveStackIndex => Math.Max(0, _activeRequest?.StackIndex ?? 0);
        internal int PendingCount => _pendingRequests.Count;

        internal void Show(SharedFadeYesNoModalRequest request, int currentTick)
        {
            _activeRequest = request ?? throw new ArgumentNullException(nameof(request));
            _createdTick = currentTick;
            _phaseTick = currentTick;
            _phase = SharedFadeYesNoModalPhase.FadingIn;
            _onceClicked = false;
        }

        internal bool Enqueue(SharedFadeYesNoModalRequest request, int currentTick)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!IsActive)
            {
                Show(request, currentTick);
                return true;
            }

            if (IsSameModalPayload(_activeRequest, request))
            {
                Show(request, currentTick);
                return true;
            }

            _pendingRequests.Enqueue(request);
            return false;
        }

        internal bool TryActivateNext(int currentTick)
        {
            if (IsActive || _pendingRequests.Count == 0)
            {
                return false;
            }

            Show(_pendingRequests.Dequeue(), currentTick);
            return true;
        }

        internal void ClearPending()
        {
            _pendingRequests.Clear();
        }

        internal bool Update(int currentTick)
        {
            if (!IsActive)
            {
                return false;
            }

            if (_phase == SharedFadeYesNoModalPhase.FadingIn
                && currentTick - _phaseTick >= FadeInMilliseconds)
            {
                _phase = SharedFadeYesNoModalPhase.Visible;
                _phaseTick = currentTick;
            }

            if (_phase == SharedFadeYesNoModalPhase.FadingOut)
            {
                if (currentTick - _phaseTick >= FadeOutMilliseconds)
                {
                    _activeRequest = null;
                    _phase = SharedFadeYesNoModalPhase.Closed;
                    _phaseTick = int.MinValue;
                    TryActivateNext(currentTick);
                }

                return false;
            }

            int lifetime = _activeRequest.LifetimeMilliseconds;
            if (lifetime >= 0
                && currentTick - _createdTick >= lifetime)
            {
                Close(currentTick);
                return true;
            }

            return false;
        }

        internal bool TryClick(int buttonId, int currentTick, out SharedFadeYesNoModalButton clickedButton)
        {
            clickedButton = default;
            if (!IsActive || _onceClicked || _phase == SharedFadeYesNoModalPhase.FadingOut)
            {
                return false;
            }

            SharedFadeYesNoButtonLayout layout = ResolveButtonLayout(ActiveType, _activeRequest.QuickDelivery);
            if (buttonId == OkButtonId)
            {
                if (!layout.ShowsOkButton)
                {
                    return false;
                }

                clickedButton = SharedFadeYesNoModalButton.Ok;
            }
            else if (buttonId == CancelButtonId)
            {
                clickedButton = SharedFadeYesNoModalButton.Cancel;
            }
            else
            {
                return false;
            }

            if (ActiveType != SharedFadeYesNoModalType.NewMemo)
            {
                _onceClicked = true;
            }

            Close(currentTick);
            return true;
        }

        internal void Close(int currentTick)
        {
            if (!IsActive)
            {
                return;
            }

            if (_phase == SharedFadeYesNoModalPhase.FadingOut)
            {
                return;
            }

            _phase = SharedFadeYesNoModalPhase.FadingOut;
            _phaseTick = currentTick;
        }

        internal void CloseImmediately()
        {
            _activeRequest = null;
            _phase = SharedFadeYesNoModalPhase.Closed;
            _phaseTick = int.MinValue;
        }

        internal SharedFadeYesNoModalSnapshot CaptureSnapshot()
        {
            if (!IsActive)
            {
                return new SharedFadeYesNoModalSnapshot(
                    false,
                    SharedFadeYesNoModalType.Generic,
                    SharedFadeYesNoModalPhase.Closed,
                    0,
                    PendingCount,
                    0,
                    int.MinValue,
                    int.MinValue,
                    "CUIFadeYesNo::Draw inactive",
                    ResolveButtonLayout(SharedFadeYesNoModalType.Generic, quickDelivery: false),
                    false,
                    false,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    null);
            }

            return new SharedFadeYesNoModalSnapshot(
                true,
                ActiveType,
                _phase,
                ActiveStackIndex,
                PendingCount,
                _activeRequest.LifetimeMilliseconds,
                _createdTick,
                _phaseTick,
                ResolveDrawRoute(ActiveType),
                ResolveButtonLayout(ActiveType, _activeRequest.QuickDelivery),
                _onceClicked,
                _activeRequest.QuickDelivery,
                _activeRequest.Title ?? string.Empty,
                _activeRequest.Body ?? string.Empty,
                _activeRequest.Footer ?? string.Empty,
                _activeRequest.Presentation);
        }

        internal static SharedFadeYesNoVisualProfile ResolveVisualProfile(SharedFadeYesNoModalType type, bool quickDelivery)
        {
            return type switch
            {
                SharedFadeYesNoModalType.FriendRegister => InviteProfile("backgrnd", "icon1"),
                SharedFadeYesNoModalType.TradeInvite => InviteProfile("backgrnd", "icon2"),
                SharedFadeYesNoModalType.CashTradeInvite => AlarmProfile("backgrnd6", "icon9", 160, 44),
                SharedFadeYesNoModalType.NewMemo => AlarmProfile("backgrnd3", null, 155, 44, suppressesIcon: true),
                SharedFadeYesNoModalType.ExpeditionApply => InviteProfile("backgrnd9", "icon0"),
                SharedFadeYesNoModalType.PartyInvite => InviteProfile("backgrnd", "icon5"),
                SharedFadeYesNoModalType.QuestClear => AlarmProfile("backgrnd4", quickDelivery ? "icon7" : "icon6", 155, 44),
                SharedFadeYesNoModalType.GuildInvite => InviteProfile("backgrnd", "icon5"),
                SharedFadeYesNoModalType.UserAlarm => AlarmProfile("backgrnd2", quickDelivery ? "icon4" : "icon3", 155, 44),
                SharedFadeYesNoModalType.ParcelAlarm => AlarmProfile("backgrnd4", "delivery", 155, 44),
                SharedFadeYesNoModalType.PartyQuestAlarm => new SharedFadeYesNoVisualProfile(
                    "backgrnd5",
                    null,
                    155,
                    51,
                    AlarmAnchorX,
                    PartyQuestBottomOffset,
                    6,
                    37,
                    UsesBlackText: true,
                    SuppressesIcon: true),
                SharedFadeYesNoModalType.FamilyInvite => InviteProfile("backgrnd", "icon8"),
                SharedFadeYesNoModalType.PartyApply => InviteProfile("backgrnd7", "icon5"),
                SharedFadeYesNoModalType.ExpeditionInvite => new SharedFadeYesNoVisualProfile(
                    "backgrnd8",
                    "icon0",
                    246,
                    60,
                    ExpeditionInviteAnchorX,
                    InviteBottomOffset,
                    6,
                    37),
                SharedFadeYesNoModalType.AllianceInvite => InviteProfile("backgrnd", "icon5"),
                SharedFadeYesNoModalType.FollowRequest => InviteProfile("backgrnd9", null, suppressesIcon: true),
                SharedFadeYesNoModalType.NewYearCardArrived => AlarmProfile("backgrnd2", "icon7", 155, 44),
                _ => InviteProfile("backgrnd", "icon0")
            };
        }

        internal static SharedFadeYesNoButtonLayout ResolveButtonLayout(
            SharedFadeYesNoModalType type,
            bool quickDelivery)
        {
            int buttonX = type == SharedFadeYesNoModalType.CashTradeInvite ? 141 : 136;
            int okY = 7;
            int cancelY = 20;

            if (WideButtonTypes.Contains(type))
            {
                buttonX = 188;
                okY = 12;
                cancelY = 31;
            }
            else if (type == SharedFadeYesNoModalType.ExpeditionInvite)
            {
                buttonX = 228;
                okY = 12;
                cancelY = 31;
            }

            bool showsOk = type != SharedFadeYesNoModalType.UserAlarm
                && (type != SharedFadeYesNoModalType.ParcelAlarm || quickDelivery);

            return new SharedFadeYesNoButtonLayout(
                OkButtonId,
                CancelButtonId,
                buttonX,
                okY,
                cancelY,
                showsOk);
        }

        internal static string ResolveDrawRoute(SharedFadeYesNoModalType type)
        {
            return type switch
            {
                SharedFadeYesNoModalType.MessengerInvite => "CUIFadeYesNo::Draw messenger invite",
                SharedFadeYesNoModalType.FriendRegister => "CUIFadeYesNo::Draw friend register",
                SharedFadeYesNoModalType.TradeInvite => "CUIFadeYesNo::Draw trade invite",
                SharedFadeYesNoModalType.CashTradeInvite => "CUIFadeYesNo::Draw cash trade invite",
                SharedFadeYesNoModalType.NewMemo => "CUIFadeYesNo::Draw new memo",
                SharedFadeYesNoModalType.ExpeditionApply => "CUIFadeYesNo::Draw expedition apply",
                SharedFadeYesNoModalType.PartyInvite => "CUIFadeYesNo::Draw party invite",
                SharedFadeYesNoModalType.QuestClear => "CUIFadeYesNo::Draw quest clear",
                SharedFadeYesNoModalType.GuildInvite => "CUIFadeYesNo::Draw guild invite",
                SharedFadeYesNoModalType.UserAlarm => "CUIFadeYesNo::Draw user alarm",
                SharedFadeYesNoModalType.ParcelAlarm => "CUIFadeYesNo::Draw parcel alarm",
                SharedFadeYesNoModalType.PartyQuestAlarm => "CUIFadeYesNo::Draw party quest alarm",
                SharedFadeYesNoModalType.FamilyInvite => "CUIFadeYesNo::Draw family invite",
                SharedFadeYesNoModalType.PartyApply => "CUIFadeYesNo::Draw party apply",
                SharedFadeYesNoModalType.ExpeditionInvite => "CUIFadeYesNo::Draw expedition invite",
                SharedFadeYesNoModalType.AllianceInvite => "CUIFadeYesNo::Draw alliance invite",
                SharedFadeYesNoModalType.FollowRequest => "CUIFadeYesNo::Draw follow request",
                SharedFadeYesNoModalType.NewYearCardArrived => "CUIFadeYesNo::Draw New Year card arrived",
                _ => "CUIFadeYesNo::Draw generic simulator confirmation"
            };
        }

        internal static string FormatSnapshotForTrace(SharedFadeYesNoModalSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsActive)
            {
                return "CFadeWnd/CUIFadeYesNo owner inactive.";
            }

            SharedFadeYesNoVisualProfile visualProfile = ResolveVisualProfile(snapshot.Type, snapshot.QuickDelivery);

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}; phase={1}; stack={2}; pending={3}; lifetime={4}; frame={11}({12}x{13}); icon={14}; buttons OK:{5}@({6},{7}) Cancel:{8}@({6},{9}); OKVisible={10}.",
                snapshot.DrawRoute,
                snapshot.Phase,
                snapshot.StackIndex,
                snapshot.PendingCount,
                snapshot.LifetimeMilliseconds,
                snapshot.ButtonLayout.OkId,
                snapshot.ButtonLayout.ButtonX,
                snapshot.ButtonLayout.OkY,
                snapshot.ButtonLayout.CancelId,
                snapshot.ButtonLayout.CancelY,
                snapshot.ButtonLayout.ShowsOkButton,
                visualProfile.FrameName,
                visualProfile.NativeWidth,
                visualProfile.NativeHeight,
                visualProfile.IconName ?? "none");
        }

        private static SharedFadeYesNoVisualProfile InviteProfile(
            string frameName,
            string iconName,
            int nativeWidth = 206,
            int nativeHeight = 60,
            bool suppressesIcon = false)
        {
            return new SharedFadeYesNoVisualProfile(
                frameName,
                iconName,
                nativeWidth,
                nativeHeight,
                InviteAnchorX,
                InviteBottomOffset,
                6,
                TypeUsesTallIconCenter(frameName, iconName) ? 53 : 37,
                SuppressesIcon: suppressesIcon);
        }

        private static SharedFadeYesNoVisualProfile AlarmProfile(
            string frameName,
            string iconName,
            int nativeWidth,
            int nativeHeight,
            bool suppressesIcon = false)
        {
            return new SharedFadeYesNoVisualProfile(
                frameName,
                iconName,
                nativeWidth,
                nativeHeight,
                AlarmAnchorX,
                AlarmBottomOffset,
                6,
                37,
                SuppressesIcon: suppressesIcon);
        }

        private static bool TypeUsesTallIconCenter(string frameName, string iconName)
        {
            return string.Equals(frameName, "backgrnd", StringComparison.Ordinal)
                && (string.Equals(iconName, "icon1", StringComparison.Ordinal)
                    || string.Equals(iconName, "icon8", StringComparison.Ordinal)
                    || string.Equals(iconName, "icon0", StringComparison.Ordinal));
        }

        private static bool IsSameModalPayload(
            SharedFadeYesNoModalRequest current,
            SharedFadeYesNoModalRequest candidate)
        {
            return current != null
                && candidate != null
                && current.Type == candidate.Type
                && current.StackIndex == candidate.StackIndex
                && current.QuickDelivery == candidate.QuickDelivery
                && string.Equals(current.Title, candidate.Title, StringComparison.Ordinal)
                && string.Equals(current.Body, candidate.Body, StringComparison.Ordinal)
                && string.Equals(current.Footer, candidate.Footer, StringComparison.Ordinal);
        }
    }
}
