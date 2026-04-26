using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using System;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class UserInfoPopularityPreviewService
    {
        private const int MinFame = -30000;
        private const int MaxFame = 30000;
        private const int ClientRequestCooldownMs = 300000;
        private const int PendingResolveDelayMs = 900;
        private PendingPopularityRequest? _pendingRequest;
        private int _lastRequestTick = int.MinValue;

        internal bool CanRequest(UserInfoUI.UserInfoActionContext context, UserInfoUI.PopularityChangeDirection direction)
        {
            if (!CanRequest(context, direction, Environment.TickCount, out _))
            {
                return false;
            }

            return true;
        }

        internal bool CanRequest(
            UserInfoUI.UserInfoActionContext context,
            UserInfoUI.PopularityChangeDirection direction,
            int currentTick,
            out string gateMessage)
        {
            gateMessage = null;
            if (!context.IsRemoteTarget)
            {
                gateMessage = "Popularity requests only apply to an inspected target.";
                return false;
            }

            CharacterBuild targetBuild = context.Build;
            if (targetBuild == null || string.IsNullOrWhiteSpace(context.CharacterName))
            {
                gateMessage = "Popularity request target is unavailable.";
                return false;
            }

            if (direction == UserInfoUI.PopularityChangeDirection.Up && targetBuild.Fame >= MaxFame)
            {
                gateMessage = $"{context.CharacterName} is already at +{MaxFame} Fame.";
                return false;
            }

            if (direction == UserInfoUI.PopularityChangeDirection.Down && targetBuild.Fame <= MinFame)
            {
                gateMessage = $"{context.CharacterName} is already at {MinFame} Fame.";
                return false;
            }

            if (TryGetPendingGateMessage(context.CharacterName, out string pendingMessage))
            {
                gateMessage = pendingMessage;
                return false;
            }

            if (TryGetCooldownGateMessage(currentTick, out string cooldownMessage))
            {
                gateMessage = cooldownMessage;
                return false;
            }

            return true;
        }

        internal string HandleRequest(
            UserInfoUI.UserInfoActionContext context,
            UserInfoUI.PopularityChangeDirection direction,
            int currentTick,
            string dispatchStatus = null)
        {
            if (!CanRequest(context, direction, currentTick, out string gateMessage))
            {
                return gateMessage;
            }

            CharacterBuild targetBuild = context.Build;
            _pendingRequest = new PendingPopularityRequest(
                context.CharacterId,
                context.CharacterName.Trim(),
                targetBuild,
                direction,
                currentTick + PendingResolveDelayMs);
            _lastRequestTick = currentTick;

            string directionLabel = direction == UserInfoUI.PopularityChangeDirection.Up ? "up" : "down";
            string message = $"Popularity {directionLabel} request for {context.CharacterName} is now pending the delayed result branch.";
            return string.IsNullOrWhiteSpace(dispatchStatus)
                ? message
                : $"{message} {dispatchStatus}";
        }

        internal string Update(int currentTick, RemoteUserActorPool remoteUserPool)
        {
            if (!_pendingRequest.HasValue)
            {
                return null;
            }

            PendingPopularityRequest pending = _pendingRequest.Value;
            if (unchecked(currentTick - pending.ResolveAtTick) < 0)
            {
                return null;
            }

            _pendingRequest = null;

            CharacterBuild targetBuild = pending.TargetBuild;
            if (targetBuild == null)
            {
                return $"Popularity request for {pending.TargetName} expired before the target build could be resolved.";
            }

            if (pending.Direction == UserInfoUI.PopularityChangeDirection.Up && targetBuild.Fame >= MaxFame)
            {
                return $"{pending.TargetName} is already at +{MaxFame} Fame.";
            }

            if (pending.Direction == UserInfoUI.PopularityChangeDirection.Down && targetBuild.Fame <= MinFame)
            {
                return $"{pending.TargetName} is already at {MinFame} Fame.";
            }

            int delta = pending.Direction == UserInfoUI.PopularityChangeDirection.Up ? 1 : -1;
            int updatedFame = Math.Clamp(targetBuild.Fame + delta, MinFame, MaxFame);
            targetBuild.Fame = updatedFame;

            if (remoteUserPool != null &&
                (remoteUserPool.TryGetActor(pending.CharacterId, out var actor) || remoteUserPool.TryGetActorByName(pending.TargetName, out actor)) &&
                actor?.Build != null &&
                !ReferenceEquals(actor.Build, targetBuild))
            {
                actor.Build.Fame = updatedFame;
            }

            string directionLabel = pending.Direction == UserInfoUI.PopularityChangeDirection.Up ? "up" : "down";
            return $"Popularity {directionLabel} result applied for {pending.TargetName}. Fame is now {updatedFame}.";
        }

        internal bool TryApplyClientResultPayload(byte[] payload, RemoteUserActorPool remoteUserPool, out string message)
        {
            message = null;
            if (payload == null || payload.Length == 0)
            {
                message = "CUIUserInfo popularity result payload is missing the client result code.";
                return false;
            }

            int offset = 0;
            int resultCode = payload[offset++];
            switch (resultCode)
            {
                case 0:
                    if (!TryReadClientString(payload, ref offset, out string targetName, out message))
                    {
                        return false;
                    }

                    if (offset + sizeof(byte) + sizeof(int) > payload.Length)
                    {
                        message = "CUIUserInfo popularity result payload is missing the direction byte or updated fame value.";
                        return false;
                    }

                    bool increased = payload[offset++] != 0;
                    int updatedFame = ReadInt32LittleEndian(payload, offset);
                    ApplyPopularityResult(targetName, updatedFame, remoteUserPool);
                    _pendingRequest = null;
                    string directionLabel = increased ? "up" : "down";
                    message = $"CUIUserInfo::NotifyGivePopResult applied popularity {directionLabel} result for {targetName}. Fame is now {updatedFame}.";
                    return true;

                case 1:
                    _pendingRequest = null;
                    message = "CUIUserInfo popularity result rejected: the target cannot receive Fame right now.";
                    return true;

                case 2:
                    _pendingRequest = null;
                    message = "CUIUserInfo popularity result rejected: the local character cannot give Fame right now.";
                    return true;

                case 3:
                    _pendingRequest = null;
                    message = "CUIUserInfo popularity result rejected: this target was already processed recently.";
                    return true;

                case 4:
                    _pendingRequest = null;
                    message = "CUIUserInfo popularity result rejected by the server.";
                    return true;

                case 5:
                    if (!TryReadClientString(payload, ref offset, out string requesterName, out message))
                    {
                        return false;
                    }

                    if (offset >= payload.Length)
                    {
                        message = "CUIUserInfo popularity received-result payload is missing the direction byte.";
                        return false;
                    }

                    string receivedDirectionLabel = payload[offset] != 0 ? "up" : "down";
                    message = $"CUIUserInfo popularity received-result notice from {requesterName} ({receivedDirectionLabel}).";
                    return true;

                default:
                    _pendingRequest = null;
                    message = $"CUIUserInfo popularity result used an unknown client result code {resultCode}.";
                    return true;
            }
        }

        private void ApplyPopularityResult(string targetName, int updatedFame, RemoteUserActorPool remoteUserPool)
        {
            CharacterBuild targetBuild = null;
            if (_pendingRequest.HasValue)
            {
                PendingPopularityRequest pending = _pendingRequest.Value;
                if (string.Equals(pending.TargetName, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    targetBuild = pending.TargetBuild;
                }
            }

            if (targetBuild == null
                && remoteUserPool != null
                && remoteUserPool.TryGetActorByName(targetName, out RemoteUserActor actor)
                && actor?.Build != null)
            {
                targetBuild = actor.Build;
            }

            if (targetBuild != null)
            {
                targetBuild.Fame = Math.Clamp(updatedFame, MinFame, MaxFame);
            }
        }

        private static bool TryReadClientString(byte[] payload, ref int offset, out string value, out string message)
        {
            value = null;
            message = null;
            if (offset + sizeof(ushort) > payload.Length)
            {
                message = "CUIUserInfo popularity result payload is missing a client string length.";
                return false;
            }

            int length = payload[offset] | (payload[offset + 1] << 8);
            offset += sizeof(ushort);
            if (length < 0 || offset + length > payload.Length)
            {
                message = "CUIUserInfo popularity result payload has an invalid client string length.";
                return false;
            }

            value = Encoding.Default.GetString(payload, offset, length);
            offset += length;
            return true;
        }

        private static int ReadInt32LittleEndian(byte[] payload, int offset)
        {
            return payload[offset]
                | (payload[offset + 1] << 8)
                | (payload[offset + 2] << 16)
                | (payload[offset + 3] << 24);
        }

        private bool TryGetPendingGateMessage(string requestedTargetName, out string message)
        {
            message = null;
            if (!_pendingRequest.HasValue)
            {
                return false;
            }

            PendingPopularityRequest pending = _pendingRequest.Value;
            string pendingDirection = pending.Direction == UserInfoUI.PopularityChangeDirection.Up ? "up" : "down";
            message = string.Equals(pending.TargetName, requestedTargetName, StringComparison.OrdinalIgnoreCase)
                ? $"Popularity {pendingDirection} request for {pending.TargetName} is already waiting for the delayed result branch."
                : $"Popularity {pendingDirection} request for {pending.TargetName} is already waiting for the delayed result branch.";
            return true;
        }

        private bool TryGetCooldownGateMessage(int currentTick, out string message)
        {
            message = null;
            if (_lastRequestTick == int.MinValue)
            {
                return false;
            }

            int elapsed = unchecked(currentTick - _lastRequestTick);
            if (elapsed < 0 || elapsed >= ClientRequestCooldownMs)
            {
                return false;
            }

            int remainingSeconds = Math.Max(1, (ClientRequestCooldownMs - elapsed + 999) / 1000);
            message = $"Popularity request is still on the client cooldown for {remainingSeconds} second(s).";
            return true;
        }

        private readonly record struct PendingPopularityRequest(
            int CharacterId,
            string TargetName,
            CharacterBuild TargetBuild,
            UserInfoUI.PopularityChangeDirection Direction,
            int ResolveAtTick);
    }
}
