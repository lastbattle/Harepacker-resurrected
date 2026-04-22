using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using System;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketOwnedAccountMoreInfoPacketType = 133;
        private const int PacketOwnedSetGenderPacketType = 58;
        private const int PacketOwnedAccountMoreInfoUiType = 40;
        private const byte PacketOwnedAccountMoreInfoFirstEntrySubtype = 0;
        private const byte PacketOwnedAccountMoreInfoLoadRequestSubtype = 1;
        private const byte PacketOwnedAccountMoreInfoSaveRequestSubtype = 3;
        private const byte PacketOwnedAccountMoreInfoLoadResultSubtype = 2;
        private const byte PacketOwnedAccountMoreInfoSaveResultSubtype = 4;

        private readonly Managers.AccountMoreInfoRuntime _accountMoreInfoRuntime = new();

        private void WireAccountMoreInfoWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AccountMoreInfo) is not AccountMoreInfoWindow accountMoreInfoWindow)
            {
                return;
            }

            accountMoreInfoWindow.SetFont(_fontChat);
            accountMoreInfoWindow.SetSnapshotProvider(_accountMoreInfoRuntime.BuildSnapshot);
            accountMoreInfoWindow.SetHandlers(
                HandlePacketOwnedAccountMoreInfoSaveRequested,
                () => ClosePacketOwnedAccountMoreInfoOwner("CUIAccountMoreInfo cancel/close button closed UI owner 40."),
                (field, delta) => _accountMoreInfoRuntime.AdjustField(field, delta),
                index => _accountMoreInfoRuntime.TogglePlayStyle(index),
                index => _accountMoreInfoRuntime.ToggleActivity(index));
        }

        private bool TryApplyPacketOwnedAccountMoreInfoPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length == 0)
            {
                message = "Account-more-info payload must contain a subtype byte.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            byte subtype = payload[0];
            switch (subtype)
            {
                case PacketOwnedAccountMoreInfoFirstEntrySubtype:
                    message = OpenPacketOwnedAccountMoreInfoOwner(firstEntry: true);
                    return true;

                case PacketOwnedAccountMoreInfoLoadResultSubtype:
                    return ApplyPacketOwnedAccountMoreInfoLoadResult(payload, out message);

                case PacketOwnedAccountMoreInfoSaveResultSubtype:
                    return ApplyPacketOwnedAccountMoreInfoSaveResult(payload, out message);

                case PacketOwnedAccountMoreInfoLoadRequestSubtype:
                    if (payload.Length == 17)
                    {
                        return ApplyPacketOwnedAccountMoreInfoLoadResult(payload, out message);
                    }

                    message = $"Account-more-info subtype {subtype} is the client load-request byte for outbound opcode {Managers.AccountMoreInfoRuntime.ClientOpcode}; use server subtype {PacketOwnedAccountMoreInfoLoadResultSubtype} for OnLoadAccountMoreInfoResult payloads.";
                    return false;

                case PacketOwnedAccountMoreInfoSaveRequestSubtype:
                    if (payload.Length == 2)
                    {
                        return ApplyPacketOwnedAccountMoreInfoSaveResult(payload, out message);
                    }

                    message = $"Account-more-info subtype {subtype} is the client save-request byte for outbound opcode {Managers.AccountMoreInfoRuntime.ClientOpcode}; use server subtype {PacketOwnedAccountMoreInfoSaveResultSubtype} for OnSaveAccountMoreInfoResult payloads.";
                    return false;

                default:
                    message = $"Unsupported account-more-info subtype {subtype}. Expected first-entry {PacketOwnedAccountMoreInfoFirstEntrySubtype}, load result {PacketOwnedAccountMoreInfoLoadResultSubtype}, or save result {PacketOwnedAccountMoreInfoSaveResultSubtype}.";
                    return false;
            }
        }

        private bool TryApplyPacketOwnedSetGenderPayload(byte[] payload, out string message)
        {
            if (payload == null || payload.Length < 1)
            {
                message = "SetGender payload must contain the raw gender byte.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            _accountMoreInfoRuntime.ApplySetGender(payload[0], Environment.TickCount);
            message = $"CWvsContext::OnSetGender applied adjacent gender byte {payload[0].ToString(CultureInfo.InvariantCulture)} to the account-more-info context state without opening a separate dialog owner.";
            return true;
        }

        private string OpenPacketOwnedAccountMoreInfoOwner(bool firstEntry)
        {
            bool shouldDispatchLoadRequest = _accountMoreInfoRuntime.OpenOrRefreshFromPacket(firstEntry);
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AccountMoreInfo) is not AccountMoreInfoWindow window)
            {
                string unavailable = $"CWvsContext::OnAccountMoreInfo requested UI_Open({PacketOwnedAccountMoreInfoUiType}), but the simulator account-more-info owner is unavailable.";
                _accountMoreInfoRuntime.RecordDispatchStatus(unavailable);
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            ShowWindow(MapSimulatorWindowNames.AccountMoreInfo, window, trackDirectionModeOwner: true);
            uiWindowManager?.BringToFront(window);
            if (firstEntry)
            {
                ShowUtilityFeedbackMessage(AccountMoreInfoOwnerStringPoolText.ResolveFirstEntryPrompt());
            }

            if (!shouldDispatchLoadRequest)
            {
                const string duplicateOpenStatus = "CWvsContext::OnAccountMoreInfo subtype 0 refreshed existing UI type 40 and skipped duplicate load request dispatch because the owner was already open.";
                _accountMoreInfoRuntime.RecordDispatchStatus(duplicateOpenStatus);
                return duplicateOpenStatus;
            }

            byte[] loadPayload = _accountMoreInfoRuntime.BuildLoadRequestPayload();
            string dispatchStatus = DispatchPacketOwnedAccountMoreInfoClientRequest(
                Managers.AccountMoreInfoRuntime.ClientOpcode,
                loadPayload,
                "CUIAccountMoreInfo::SendLoadAccountMoreInfoRequest");
            _accountMoreInfoRuntime.RecordDispatchStatus(dispatchStatus);
            string promptStatus = firstEntry
                ? "Displayed first-entry account-more-info prompt text (StringPool 0x16B5). "
                : string.Empty;
            return $"CWvsContext::OnAccountMoreInfo opened UI type {PacketOwnedAccountMoreInfoUiType} and marked m_bMoreInfoFirst={(firstEntry ? 1 : 0)}. {promptStatus}{dispatchStatus}";
        }

        private bool ApplyPacketOwnedAccountMoreInfoLoadResult(byte[] payload, out string message)
        {
            if (_accountMoreInfoRuntime.TryApplyLoadResult(payload, out message))
            {
                return true;
            }

            return false;
        }

        private bool ApplyPacketOwnedAccountMoreInfoSaveResult(byte[] payload, out string message)
        {
            if (!_accountMoreInfoRuntime.TryApplySaveResult(payload, out bool succeeded, out message))
            {
                return false;
            }

            if (succeeded)
            {
                uiWindowManager?.HideWindow(MapSimulatorWindowNames.AccountMoreInfo);
            }
            else
            {
                ShowUtilityFeedbackMessage(AccountMoreInfoOwnerStringPoolText.ResolveSaveFailedNotice());
            }

            return true;
        }

        private void HandlePacketOwnedAccountMoreInfoSaveRequested()
        {
            byte[] savePayload = _accountMoreInfoRuntime.BuildSaveRequestPayload();
            if (savePayload == null || savePayload.Length == 0)
            {
                return;
            }

            string dispatchStatus = DispatchPacketOwnedAccountMoreInfoClientRequest(
                Managers.AccountMoreInfoRuntime.ClientOpcode,
                savePayload,
                "CUIAccountMoreInfo::SendSaveAccountMoreInfoRequest");
            _accountMoreInfoRuntime.RecordDispatchStatus(dispatchStatus);
            ShowUtilityFeedbackMessage(dispatchStatus);
        }

        private void ClosePacketOwnedAccountMoreInfoOwner(string reason)
        {
            bool showFirstEntryCloseNotice = _accountMoreInfoRuntime.Close(reason);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.AccountMoreInfo);
            if (showFirstEntryCloseNotice)
            {
                ShowUtilityFeedbackMessage(AccountMoreInfoOwnerStringPoolText.ResolveExitWithoutInfoNotice());
            }
        }

        private string DispatchPacketOwnedAccountMoreInfoClientRequest(int opcode, byte[] payload, string source)
        {
            string payloadHex = payload?.Length > 0 ? Convert.ToHexString(payload) : "<empty>";
            byte[] safePayload = payload ?? Array.Empty<byte>();
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(opcode, safePayload, out string dispatchStatus))
            {
                return $"{source} emitted opcode {opcode} [{payloadHex}] through the live local-utility bridge. {dispatchStatus}";
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(opcode, safePayload, out string outboxStatus))
            {
                return $"{source} emitted opcode {opcode} [{payloadHex}] through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
            }

            string bridgeDeferredStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(opcode, safePayload, out bridgeDeferredStatus))
            {
                return $"{source} queued opcode {opcode} [{payloadHex}] for deferred official-session injection after immediate delivery was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(opcode, safePayload, out string queuedStatus))
            {
                return $"{source} queued opcode {opcode} [{payloadHex}] for deferred generic local-utility outbox delivery after immediate delivery was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
            }

            return $"{source} kept opcode {opcode} [{payloadHex}] simulator-local because neither the live bridge nor the generic outbox nor either deferred queue accepted it. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
        }
    }
}
