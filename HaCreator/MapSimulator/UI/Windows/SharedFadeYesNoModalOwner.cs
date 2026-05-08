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
        ExpeditionApply = 5,
        PartyInvite = 6,
        QuestClear = 7,
        GuildInvite = 8,
        UserAlarm = 9,
        ParcelAlarm = 10,
        PartyQuestAlarm = 11,
        FamilyInvite = 12,
        PartyApply = 13,
        ExpeditionInvite = 14,
        AllianceInvite = 15,
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
        int LifetimeMilliseconds,
        int CreatedTick,
        int FadePhaseTick,
        string DrawRoute,
        SharedFadeYesNoButtonLayout ButtonLayout,
        bool OnceClicked,
        string Title,
        string Body,
        string Footer);

    internal sealed class SharedFadeYesNoModalOwner
    {
        internal const int OkButtonId = (int)SharedFadeYesNoModalButton.Ok;
        internal const int CancelButtonId = (int)SharedFadeYesNoModalButton.Cancel;
        internal const int DefaultLifetimeMilliseconds = 6000;
        internal const int FadeInMilliseconds = 120;
        internal const int FadeOutMilliseconds = 120;

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
        private SharedFadeYesNoModalPhase _phase = SharedFadeYesNoModalPhase.Closed;
        private int _createdTick = int.MinValue;
        private int _phaseTick = int.MinValue;
        private bool _onceClicked;

        internal bool IsActive => _activeRequest != null && _phase != SharedFadeYesNoModalPhase.Closed;
        internal SharedFadeYesNoModalType ActiveType => _activeRequest?.Type ?? SharedFadeYesNoModalType.Generic;
        internal int ActiveStackIndex => Math.Max(0, _activeRequest?.StackIndex ?? 0);

        internal void Show(SharedFadeYesNoModalRequest request, int currentTick)
        {
            _activeRequest = request ?? throw new ArgumentNullException(nameof(request));
            _createdTick = currentTick;
            _phaseTick = currentTick;
            _phase = SharedFadeYesNoModalPhase.FadingIn;
            _onceClicked = false;
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
            if (!IsActive || _onceClicked)
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

            _phase = SharedFadeYesNoModalPhase.FadingOut;
            _phaseTick = currentTick;
            _activeRequest = null;
            _phase = SharedFadeYesNoModalPhase.Closed;
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
                    0,
                    int.MinValue,
                    int.MinValue,
                    "CUIFadeYesNo::Draw inactive",
                    ResolveButtonLayout(SharedFadeYesNoModalType.Generic, quickDelivery: false),
                    false,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            }

            return new SharedFadeYesNoModalSnapshot(
                true,
                ActiveType,
                _phase,
                ActiveStackIndex,
                _activeRequest.LifetimeMilliseconds,
                _createdTick,
                _phaseTick,
                ResolveDrawRoute(ActiveType),
                ResolveButtonLayout(ActiveType, _activeRequest.QuickDelivery),
                _onceClicked,
                _activeRequest.Title ?? string.Empty,
                _activeRequest.Body ?? string.Empty,
                _activeRequest.Footer ?? string.Empty);
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

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}; phase={1}; stack={2}; lifetime={3}; buttons OK:{4}@({5},{6}) Cancel:{7}@({5},{8}); OKVisible={9}.",
                snapshot.DrawRoute,
                snapshot.Phase,
                snapshot.StackIndex,
                snapshot.LifetimeMilliseconds,
                snapshot.ButtonLayout.OkId,
                snapshot.ButtonLayout.ButtonX,
                snapshot.ButtonLayout.OkY,
                snapshot.ButtonLayout.CancelId,
                snapshot.ButtonLayout.CancelY,
                snapshot.ButtonLayout.ShowsOkButton);
        }
    }
}
