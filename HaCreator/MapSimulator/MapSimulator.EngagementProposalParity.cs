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
        private bool _engagementProposalInboxEnabled;
        private int _engagementProposalInboxConfiguredPort = EngagementProposalInboxManager.DefaultPort;

        private void EnsureEngagementProposalInboxState(bool shouldRun)
        {
            if (!shouldRun || !_engagementProposalInboxEnabled)
            {
                if (_engagementProposalInbox.IsRunning)
                {
                    _engagementProposalInbox.Stop();
                }

                return;
            }

            if (_engagementProposalInbox.IsRunning && _engagementProposalInbox.Port == _engagementProposalInboxConfiguredPort)
            {
                return;
            }

            if (_engagementProposalInbox.IsRunning)
            {
                _engagementProposalInbox.Stop();
            }

            try
            {
                _engagementProposalInbox.Start(_engagementProposalInboxConfiguredPort);
            }
            catch (Exception ex)
            {
                _engagementProposalInbox.Stop();
                _chat?.AddErrorMessage($"Engagement proposal inbox failed to start: {ex.Message}", currTickCount);
            }
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
            if (!_engagementProposalController.TryOpenIncomingProposalFromRequestPayload(
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
                    out detail))
            {
                return false;
            }

            return true;
        }

        private string DescribeEngagementProposalInboxStatus()
        {
            string enabledText = _engagementProposalInboxEnabled ? "enabled" : "disabled";
            string listeningText = _engagementProposalInbox.IsRunning
                ? $"listening on 127.0.0.1:{_engagementProposalInbox.Port}"
                : $"configured for 127.0.0.1:{_engagementProposalInboxConfiguredPort}";
            return $"Engagement proposal inbox {enabledText}, {listeningText}, received {_engagementProposalInbox.ReceivedCount} request(s).";
        }

        private string TryAutoDispatchOutgoingEngagementProposalRequest()
        {
            if (!_engagementProposalInboxEnabled || !_engagementProposalInbox.IsRunning)
            {
                return string.Empty;
            }

            if (!_engagementProposalController.TryBuildInboxDispatch(
                    EngagementProposalRuntime.DefaultSealItemId,
                    customMessage: null,
                    out EngagementProposalInboxDispatch dispatch,
                    out string dispatchMessage))
            {
                return $" {dispatchMessage}";
            }

            try
            {
                EngagementProposalInboxManager.SendRequest(
                    EngagementProposalInboxManager.DefaultHost,
                    _engagementProposalInbox.Port,
                    dispatch);
                return $" Auto-dispatched the staged engagement request through the inbox transport to 127.0.0.1:{_engagementProposalInbox.Port}.";
            }
            catch (Exception ex)
            {
                return $" Engagement inbox transport dispatch failed after opening the requester owner: {ex.Message}";
            }
        }

        private ChatCommandHandler.CommandResult HandleEngagementProposalInboxCommand(string[] args)
        {
            const string usage = "Usage: /engage inbox [status|start [port]|stop]";
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeEngagementProposalInboxStatus()} {_engagementProposalInbox.LastStatus}");
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                int port = EngagementProposalInboxManager.DefaultPort;
                if (args.Length > 1 && (!int.TryParse(args[1], out port) || port <= 0 || port > ushort.MaxValue))
                {
                    return ChatCommandHandler.CommandResult.Error(usage);
                }

                _engagementProposalInboxConfiguredPort = port;
                _engagementProposalInboxEnabled = true;
                EnsureEngagementProposalInboxState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeEngagementProposalInboxStatus()} {_engagementProposalInbox.LastStatus}");
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _engagementProposalInboxEnabled = false;
                EnsureEngagementProposalInboxState(shouldRun: false);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeEngagementProposalInboxStatus()} {_engagementProposalInbox.LastStatus}");
            }

            return ChatCommandHandler.CommandResult.Error(usage);
        }
    }
}
