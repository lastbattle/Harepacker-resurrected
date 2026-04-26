using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly EngagementProposalInboxManager _engagementProposalInbox = new();

        private void EnsureEngagementProposalInboxState(bool shouldRun)
        {
        }

        private void DrainEngagementProposalInbox()
        {
            while (_engagementProposalInbox.TryDequeue(out EngagementProposalInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyEngagementProposalInboxMessage(message, out string detail);
                _engagementProposalInbox.RecordDispatchResult(message, applied, detail);
                if (string.IsNullOrWhiteSpace(detail))
                {
                    continue;
                }

                if (applied)
                {
                    _chat?.AddSystemMessage(detail, currTickCount);
                }
                else
                {
                    _chat?.AddErrorMessage(detail, currTickCount);
                }
            }
        }

        private bool TryApplyEngagementProposalInboxMessage(EngagementProposalInboxMessage message, out string detail)
        {
            detail = null;
            if (message == null)
            {
                detail = "Engagement inbox message was empty.";
                return false;
            }

            string partnerName = string.IsNullOrWhiteSpace(message.PartnerName)
                ? _playerManager?.Player?.Build?.Name
                : message.PartnerName;

            if (message.Kind == EngagementProposalInboxMessageKind.Decision)
            {
                return _engagementProposalController.TryApplyDecisionPayload(
                    message.RequestPayload,
                    uiWindowManager,
                    out detail);
            }

            return _engagementProposalController.TryOpenIncomingProposalFromRequestPayload(
                message.ProposerName,
                partnerName,
                message.SealItemId,
                message.RequestPayload,
                message.CustomMessage,
                uiWindowManager,
                _playerManager?.Player?.Build,
                _fontChat,
                ShowUtilityFeedbackMessage,
                () => ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.EngagementProposal),
                out detail);
        }

        private string DescribeEngagementProposalInboxStatus()
        {
            return "Engagement proposal inbox adapter-only, listener-fallback retired.";
        }

        private string TryAutoDispatchOutgoingEngagementProposalRequest()
        {
            if (!_engagementProposalController.TryBuildInboxDispatch(
                    EngagementProposalRuntime.DefaultSealItemId,
                    customMessage: null,
                    out EngagementProposalInboxDispatch dispatch,
                    out string dispatchMessage))
            {
                return $" {dispatchMessage}";
            }

            EngagementProposalInboxMessage localMessage = new(
                dispatch.ProposerName,
                dispatch.PartnerName,
                dispatch.SealItemId,
                dispatch.RequestPayload,
                dispatch.CustomMessage,
                source: "engagement-local",
                rawText: "local dispatch");
            _engagementProposalInbox.EnqueueLocal(localMessage);
            return " Auto-dispatched the staged engagement request through the local engagement inbox adapter.";
        }

        private ChatCommandHandler.CommandResult HandleEngagementProposalInboxCommand(string[] args)
        {
            const string usage = "Usage: /engage inbox [status|start|stop]";
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeEngagementProposalInboxStatus()} {_engagementProposalInbox.LastStatus}");
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(
                    "Engagement proposal inbox loopback listener is retired; use role-session ingress or packet commands for local injection.");
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info("Engagement proposal inbox loopback listener is already retired.");
            }

            return ChatCommandHandler.CommandResult.Error(usage);
        }
    }
}
