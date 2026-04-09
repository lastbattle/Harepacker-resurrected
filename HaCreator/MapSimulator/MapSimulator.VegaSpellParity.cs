using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int VegaOwnerLaunchDelayMs = 50;
        private const int VegaOwnerResultDelayMs = 50;
        private int _lastVegaExclusiveRequestTick = int.MinValue;
        private PendingVegaLaunchState _pendingVegaLaunchState;
        private PendingVegaCastState _pendingVegaCastState;
        private PendingVegaPromptState _pendingVegaPromptState;
        private Action _inGameConfirmAcceptedAction;
        private Action _inGameConfirmCancelledAction;

        private sealed class PendingVegaLaunchState
        {
            public int ModifierItemId { get; init; }
            public string Source { get; init; } = string.Empty;
            public int RequestedAtTick { get; init; }
            public int ReadyAtTick { get; init; }
        }

        private sealed class PendingVegaCastState
        {
            public VegaSpellUI.VegaOwnerRequest Request { get; init; }
            public bool UseWhiteScroll { get; init; }
            public int RequestedAtTick { get; init; }
            public int ResultReadyAtTick { get; set; }
            public byte[] EncodedPayload { get; init; } = Array.Empty<byte>();
            public bool ResultApplied { get; set; }
            public ItemUpgradeUI.ItemUpgradeAttemptResult Result { get; set; }
        }

        private sealed class PendingVegaPromptState
        {
            public VegaSpellUI.VegaOwnerRequest Request { get; init; }
            public int RequestedAtTick { get; init; }
        }

        private void WireVegaSpellWindowOwnerCallbacks(VegaSpellUI vegaSpellWindow)
        {
            if (vegaSpellWindow == null)
            {
                return;
            }

            vegaSpellWindow.StartSpellCastRequested = HandleVegaSpellCastRequested;
            vegaSpellWindow.ValidationFailed = HandleVegaSpellValidationFailed;
            vegaSpellWindow.ResultAcknowledged = HandleVegaSpellResultAcknowledged;
        }

        private void QueueVegaSpellWindowLaunch(int itemId, string source)
        {
            if (!ItemUpgradeUI.IsVegaSpellConsumable(itemId))
            {
                return;
            }

            StampPacketOwnedUtilityRequestState();
            _pendingVegaLaunchState = new PendingVegaLaunchState
            {
                ModifierItemId = itemId,
                Source = string.IsNullOrWhiteSpace(source) ? "inventory-use" : source.Trim(),
                RequestedAtTick = currTickCount,
                ReadyAtTick = currTickCount + VegaOwnerLaunchDelayMs
            };
        }

        private void UpdateVegaSpellOwnerState()
        {
            ProcessPendingVegaLaunchState();
            ProcessPendingVegaCastState();
        }

        private void ProcessPendingVegaLaunchState()
        {
            if (_pendingVegaLaunchState == null ||
                unchecked(currTickCount - _pendingVegaLaunchState.ReadyAtTick) < 0)
            {
                return;
            }

            PendingVegaLaunchState launchState = _pendingVegaLaunchState;
            _pendingVegaLaunchState = null;

            if (!TryShowVegaSpellWindow(out VegaSpellUI vegaSpellWindow))
            {
                return;
            }

            vegaSpellWindow.PrepareModifierSelection(launchState.ModifierItemId);
            vegaSpellWindow.SetOwnerStatusMessage(
                $"Recovered Vega owner launch from {launchState.Source}. Select equipment, then send the dedicated request.");
        }

        private void ProcessPendingVegaCastState()
        {
            if (_pendingVegaCastState == null ||
                _pendingVegaCastState.ResultApplied ||
                unchecked(currTickCount - _pendingVegaCastState.ResultReadyAtTick) < 0 ||
                uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is not ItemUpgradeUI itemUpgradeWindow)
            {
                return;
            }

            itemUpgradeWindow.PrepareEquipmentSelection(_pendingVegaCastState.Request.Slot);
            itemUpgradeWindow.PrepareConsumableSelection(_pendingVegaCastState.Request.ModifierItemId);

            ItemUpgradeUI.ItemUpgradeAttemptResult result = itemUpgradeWindow.TryApplyPreparedUpgrade();
            result = RewriteVegaOwnerResultMessage(result, _pendingVegaCastState.UseWhiteScroll);

            _pendingVegaCastState.ResultApplied = true;
            _pendingVegaCastState.Result = result;
            _lastVegaExclusiveRequestTick = currTickCount;

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow)
            {
                vegaSpellWindow.ApplyResolvedSpellResult(result);
            }
        }

        private bool HandleVegaSpellCastRequested(VegaSpellUI.VegaOwnerRequest request)
        {
            if (_pendingVegaCastState != null)
            {
                ShowUtilityFeedbackMessage(VegaOwnerStringPoolText.GetExclusiveRequestNotice());
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI busyWindow)
                {
                    busyWindow.SetOwnerStatusMessage(VegaOwnerStringPoolText.GetExclusiveRequestNotice());
                }

                return true;
            }

            if (_pendingEquipmentChangeRequests.Count > 0)
            {
                ShowUtilityFeedbackMessage(VegaOwnerStringPoolText.GetExclusiveRequestNotice());
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI equipmentBlockedWindow)
                {
                    equipmentBlockedWindow.SetOwnerStatusMessage(VegaOwnerStringPoolText.GetExclusiveRequestNotice());
                }

                return true;
            }

            if (_gameState.PendingMapChange || _playerManager?.Player?.Build == null)
            {
                ShowUtilityFeedbackMessage(VegaOwnerStringPoolText.GetBlockedStateNotice());
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI blockedWindow)
                {
                    blockedWindow.SetOwnerStatusMessage(VegaOwnerStringPoolText.GetBlockedStateNotice());
                }

                return true;
            }

            if (request.RequiresWhiteScrollPrompt)
            {
                _pendingVegaPromptState = new PendingVegaPromptState
                {
                    Request = request,
                    RequestedAtTick = currTickCount
                };
                ShowVegaWhiteScrollPrompt(request);
                return true;
            }

            DispatchVegaSpellRequest(request, useWhiteScroll: false);
            return true;
        }

        private void HandleVegaSpellValidationFailed(VegaSpellUI.VegaOwnerValidationFailure failure)
        {
            string message = failure switch
            {
                VegaSpellUI.VegaOwnerValidationFailure.IncompatiblePair => VegaOwnerStringPoolText.GetIncompatiblePairNotice(),
                _ => VegaOwnerStringPoolText.GetMissingSelectionNotice()
            };

            ShowUtilityFeedbackMessage(message);
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow)
            {
                vegaSpellWindow.SetOwnerStatusMessage(message);
            }
        }

        private void HandleVegaSpellResultAcknowledged()
        {
            _pendingVegaCastState = null;
            _pendingVegaPromptState = null;
        }

        private void ShowVegaWhiteScrollPrompt(VegaSpellUI.VegaOwnerRequest request)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is not InGameConfirmDialogWindow confirmDialogWindow)
            {
                DispatchVegaSpellRequest(request, useWhiteScroll: false);
                return;
            }

            ConfigureInGameConfirmDialog(
                "Vega's Spell",
                VegaOwnerStringPoolText.GetWhiteScrollPrompt(),
                $"Recovered CUIVega::OnButtonClicked white-scroll branch for {request.ScrollName}.",
                onConfirm: () =>
                {
                    ShowUtilityFeedbackMessage(VegaOwnerStringPoolText.GetWhiteScrollUsedNotice());
                    DispatchVegaSpellRequest(request, useWhiteScroll: true);
                },
                onCancel: () =>
                {
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow)
                    {
                        vegaSpellWindow.RestoreReadyStatusMessage();
                    }

                    _pendingVegaPromptState = null;
                });

            confirmDialogWindow.Show();
            uiWindowManager.ShowWindow(confirmDialogWindow);
        }

        private void DispatchVegaSpellRequest(VegaSpellUI.VegaOwnerRequest request, bool useWhiteScroll)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) is not VegaSpellUI vegaSpellWindow)
            {
                return;
            }

            _pendingVegaPromptState = null;
            int readyAtTick = currTickCount + Math.Max(vegaSpellWindow.GetCastingDurationMs(), VegaOwnerResultDelayMs);
            _pendingVegaCastState = new PendingVegaCastState
            {
                Request = request,
                UseWhiteScroll = useWhiteScroll,
                RequestedAtTick = currTickCount,
                ResultReadyAtTick = readyAtTick,
                EncodedPayload = EncodeVegaRequestPayload(request, useWhiteScroll, currTickCount)
            };
            _lastVegaExclusiveRequestTick = currTickCount;
            StampPacketOwnedUtilityRequestState();

            string status = useWhiteScroll
                ? $"{request.ModifierName} sent with White Scroll protection."
                : $"Sent {request.ModifierName} request for {request.EquipName}.";
            vegaSpellWindow.StartSpellCast(status);
        }

        private static byte[] EncodeVegaRequestPayload(VegaSpellUI.VegaOwnerRequest request, bool useWhiteScroll, int updateTick)
        {
            byte[] payload = new byte[(sizeof(int) * 6) + sizeof(byte)];
            Buffer.BlockCopy(BitConverter.GetBytes(request.EquipItemId), 0, payload, 0, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes((int)request.Slot), 0, payload, sizeof(int), sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(request.ScrollItemId), 0, payload, sizeof(int) * 2, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(request.ModifierItemId), 0, payload, sizeof(int) * 3, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(request.ModifiedSuccessRate), 0, payload, sizeof(int) * 4, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(updateTick), 0, payload, sizeof(int) * 5, sizeof(int));
            payload[payload.Length - 1] = useWhiteScroll ? (byte)1 : (byte)0;
            return payload;
        }

        private static ItemUpgradeUI.ItemUpgradeAttemptResult RewriteVegaOwnerResultMessage(
            ItemUpgradeUI.ItemUpgradeAttemptResult result,
            bool usedWhiteScroll)
        {
            if (!usedWhiteScroll || !result.Success.HasValue)
            {
                return result;
            }

            string message = result.Success.Value
                ? VegaOwnerStringPoolText.GetWhiteScrollSuccessNotice()
                : VegaOwnerStringPoolText.GetWhiteScrollProtectedFailureNotice();
            return new ItemUpgradeUI.ItemUpgradeAttemptResult(
                result.Success,
                message,
                result.ConsumableItemId,
                result.ModifierItemId);
        }

        private void ConfigureInGameConfirmDialog(
            string title,
            string body,
            string footer,
            Action onConfirm,
            Action onCancel)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is not InGameConfirmDialogWindow confirmDialogWindow)
            {
                onConfirm?.Invoke();
                return;
            }

            _inGameConfirmAcceptedAction = onConfirm;
            _inGameConfirmCancelledAction = onCancel;
            confirmDialogWindow.Configure(title, body, footer);
        }

        private void ClearInGameConfirmDialogActions()
        {
            _inGameConfirmAcceptedAction = null;
            _inGameConfirmCancelledAction = null;
        }
    }
}
