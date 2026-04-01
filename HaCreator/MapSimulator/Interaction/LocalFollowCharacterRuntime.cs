using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal readonly record struct LocalFollowUserSnapshot(
        int CharacterId,
        string Name,
        bool Exists,
        bool IsAlive,
        bool IsImmovable,
        bool IsMounted,
        bool HasMorphTemplate,
        bool IsGhostAction,
        Vector2 Position,
        bool FacingRight)
    {
        public static LocalFollowUserSnapshot Missing(int characterId, string name = null)
        {
            return new LocalFollowUserSnapshot(
                characterId,
                name,
                Exists: false,
                IsAlive: false,
                IsImmovable: false,
                IsMounted: false,
                HasMorphTemplate: false,
                IsGhostAction: false,
                Position: Vector2.Zero,
                FacingRight: true);
        }

        public string DisplayName => string.IsNullOrWhiteSpace(Name)
            ? CharacterId > 0 ? $"Character {CharacterId}" : "Unknown character"
            : Name.Trim();
    }

    internal readonly record struct LocalFollowApplyResult(
        bool PlayerPositionChanged,
        Vector2 PlayerPosition,
        bool PlayerFacingRightChanged,
        bool PlayerFacingRight);

    internal sealed class LocalFollowCharacterRuntime
    {
        public const int FollowRequestThrottleMs = 1000;
        public const int FollowRequestOpcode = 134;

        private int _pendingOutgoingDriverId;
        private bool _pendingOutgoingAutoRequest;
        private bool _pendingOutgoingKeyInput;
        private int _lastOutgoingRequestTick = int.MinValue;
        private int _lastOutgoingRequestOpcodeDriverId;
        private bool _lastOutgoingRequestOpcodeAutoRequest;
        private bool _lastOutgoingRequestOpcodeKeyInput;
        private int _lastOutgoingRequestOpcodeTick = int.MinValue;

        private int _attachedDriverId;
        private int _attachedPassengerId;
        private int _incomingRequesterId;
        private int _lastAttachAckTick = int.MinValue;
        private string _lastStatusMessage = "Local follow idle.";

        public int PendingOutgoingDriverId => _pendingOutgoingDriverId;
        public int AttachedDriverId => _attachedDriverId;
        public int AttachedPassengerId => _attachedPassengerId;
        public int IncomingRequesterId => _incomingRequesterId;
        public string LastStatusMessage => _lastStatusMessage;

        public bool TrySendOutgoingRequest(
            LocalFollowUserSnapshot localUser,
            LocalFollowUserSnapshot driver,
            int currentTime,
            bool autoRequest,
            bool keyInput,
            out string message)
        {
            if (!CanSendOutgoingRequest(localUser, driver, currentTime, out message))
            {
                return false;
            }

            _pendingOutgoingDriverId = driver.CharacterId;
            _pendingOutgoingAutoRequest = autoRequest;
            _pendingOutgoingKeyInput = keyInput;
            _lastOutgoingRequestTick = currentTime;
            _lastOutgoingRequestOpcodeDriverId = driver.CharacterId;
            _lastOutgoingRequestOpcodeAutoRequest = autoRequest;
            _lastOutgoingRequestOpcodeKeyInput = keyInput;
            _lastOutgoingRequestOpcodeTick = currentTime;
            _lastStatusMessage =
                $"Queued local follow request to {driver.DisplayName}; simulated outpacket {FollowRequestOpcode} ({driver.CharacterId}, {(autoRequest ? 1 : 0)}, {(keyInput ? 1 : 0)}).";
            message = _lastStatusMessage;
            return true;
        }

        public void Clear()
        {
            _pendingOutgoingDriverId = 0;
            _pendingOutgoingAutoRequest = false;
            _pendingOutgoingKeyInput = false;
            _lastOutgoingRequestTick = int.MinValue;
            _lastOutgoingRequestOpcodeDriverId = 0;
            _lastOutgoingRequestOpcodeAutoRequest = false;
            _lastOutgoingRequestOpcodeKeyInput = false;
            _lastOutgoingRequestOpcodeTick = int.MinValue;
            _attachedDriverId = 0;
            _attachedPassengerId = 0;
            _incomingRequesterId = 0;
            _lastAttachAckTick = int.MinValue;
            _lastStatusMessage = "Local follow idle.";
        }

        public bool TryQueueIncomingRequest(LocalFollowUserSnapshot requester, out string message)
        {
            if (!requester.Exists || requester.CharacterId <= 0)
            {
                message = "Incoming follow request could not be opened because the requester was not found in the remote-user pool.";
                return false;
            }

            _incomingRequesterId = requester.CharacterId;
            _lastStatusMessage = $"Incoming follow request from {requester.DisplayName} is waiting for a local Yes/No response.";
            message = _lastStatusMessage;
            return true;
        }

        public bool TryAcceptIncomingRequest(LocalFollowUserSnapshot requester, out string message)
        {
            if (_incomingRequesterId <= 0 || requester.CharacterId != _incomingRequesterId || !requester.Exists)
            {
                message = "No matching incoming follow request is pending.";
                return false;
            }

            _incomingRequesterId = 0;
            _attachedPassengerId = requester.CharacterId;
            _lastStatusMessage = $"Accepted follow request from {requester.DisplayName}; the requester is now attached to the local driver seam.";
            message = _lastStatusMessage;
            return true;
        }

        public string DeclineIncomingRequest(LocalFollowUserSnapshot requester)
        {
            string displayName = requester.Exists && requester.CharacterId > 0
                ? requester.DisplayName
                : _incomingRequesterId > 0 ? $"Character {_incomingRequesterId}" : "the pending requester";
            _incomingRequesterId = 0;
            _lastStatusMessage = $"Declined follow request from {displayName}.";
            return _lastStatusMessage;
        }

        public string ClearAttachedPassenger(LocalFollowUserSnapshot requester, bool transferField, Vector2? transferPosition)
        {
            string displayName = requester.Exists && requester.CharacterId > 0
                ? requester.DisplayName
                : _attachedPassengerId > 0 ? $"Character {_attachedPassengerId}" : "the local passenger";
            _attachedPassengerId = 0;
            _lastStatusMessage = transferField && transferPosition.HasValue
                ? $"Detached passenger {displayName} from the local driver seam and preserved transfer position ({transferPosition.Value.X:0},{transferPosition.Value.Y:0})."
                : $"Detached passenger {displayName} from the local driver seam.";
            return _lastStatusMessage;
        }

        public string ApplyFollowFailure(FollowCharacterFailureInfo info)
        {
            if (info.ClearsPendingRequest)
            {
                _pendingOutgoingDriverId = 0;
                _pendingOutgoingAutoRequest = false;
                _pendingOutgoingKeyInput = false;
            }

            _lastStatusMessage = string.IsNullOrWhiteSpace(info.Message)
                ? "Local follow request failed."
                : info.Message.Trim();
            return _lastStatusMessage;
        }

        public string ApplyFollowFailureText(string message)
        {
            _lastStatusMessage = string.IsNullOrWhiteSpace(message)
                ? "Local follow request failed."
                : message.Trim();
            return _lastStatusMessage;
        }

        public string ApplyServerAttach(LocalFollowUserSnapshot driver, int currentTime)
        {
            _attachedDriverId = driver.CharacterId;
            _pendingOutgoingDriverId = 0;
            bool acknowledgedPendingAttach = _lastOutgoingRequestOpcodeDriverId == driver.CharacterId;
            if (acknowledgedPendingAttach)
            {
                _lastOutgoingRequestOpcodeDriverId = 0;
                _lastOutgoingRequestOpcodeAutoRequest = false;
                _lastOutgoingRequestOpcodeKeyInput = true;
                _lastOutgoingRequestOpcodeTick = currentTime;
                _lastAttachAckTick = currentTime;
                _lastStatusMessage =
                    $"Attached the local player to follow {driver.DisplayName}; simulated client acknowledgment outpacket {FollowRequestOpcode} (0, 0, 1).";
            }
            else
            {
                _lastStatusMessage = $"Attached the local player to follow {driver.DisplayName}.";
            }

            return _lastStatusMessage;
        }

        public LocalFollowApplyResult ApplyServerDetach(
            LocalFollowUserSnapshot previousDriver,
            bool transferField,
            Vector2? transferPosition)
        {
            Vector2 resolvedPosition = transferField && transferPosition.HasValue
                ? transferPosition.Value
                : previousDriver.Exists
                    ? previousDriver.Position
                    : Vector2.Zero;
            bool resolvedFacingRight = previousDriver.Exists
                ? previousDriver.FacingRight
                : true;

            _attachedDriverId = 0;
            _lastStatusMessage = transferField && transferPosition.HasValue
                ? $"Detached the local player from follow and moved to ({resolvedPosition.X:0},{resolvedPosition.Y:0}) using the transfer-field payload."
                : previousDriver.Exists
                    ? $"Detached the local player from follow and snapped back to {previousDriver.DisplayName} at ({resolvedPosition.X:0},{resolvedPosition.Y:0})."
                    : "Detached the local player from follow.";

            return new LocalFollowApplyResult(
                PlayerPositionChanged: transferField && transferPosition.HasValue || previousDriver.Exists,
                PlayerPosition: resolvedPosition,
                PlayerFacingRightChanged: previousDriver.Exists,
                PlayerFacingRight: resolvedFacingRight);
        }

        public string DescribeStatus(Func<int, string> nameResolver)
        {
            string pendingDriver = DescribeActor(_pendingOutgoingDriverId, nameResolver);
            string attachedDriver = DescribeActor(_attachedDriverId, nameResolver);
            string attachedPassenger = DescribeActor(_attachedPassengerId, nameResolver);
            string incomingRequester = DescribeActor(_incomingRequesterId, nameResolver);
            string lastRequest = _lastOutgoingRequestOpcodeTick == int.MinValue
                ? "none"
                : $"{FollowRequestOpcode}({_lastOutgoingRequestOpcodeDriverId},{(_lastOutgoingRequestOpcodeAutoRequest ? 1 : 0)},{(_lastOutgoingRequestOpcodeKeyInput ? 1 : 0)})@{_lastOutgoingRequestOpcodeTick}";
            string attachAck = _lastAttachAckTick == int.MinValue ? "none" : _lastAttachAckTick.ToString();
            return $"Local follow: pending={pendingDriver}, attachedDriver={attachedDriver}, passenger={attachedPassenger}, incoming={incomingRequester}, lastRequest={lastRequest}, attachAck={attachAck}. {_lastStatusMessage}";
        }

        private bool CanSendOutgoingRequest(
            LocalFollowUserSnapshot localUser,
            LocalFollowUserSnapshot driver,
            int currentTime,
            out string message)
        {
            if (!localUser.Exists || !localUser.IsAlive)
            {
                message = "Local follow request was rejected because the local player is not available.";
                return false;
            }

            if (localUser.IsImmovable)
            {
                message = "Local follow request was rejected because the local player is immovable.";
                return false;
            }

            if (localUser.IsMounted)
            {
                message = "Local follow request was rejected because the local player is mounted.";
                return false;
            }

            if (localUser.HasMorphTemplate || localUser.IsGhostAction)
            {
                message = "Local follow request was rejected because the local player is in a morph or ghost transform.";
                return false;
            }

            if (!driver.Exists || driver.CharacterId <= 0)
            {
                message = "Local follow request was rejected because the target driver was not found in the remote-user pool.";
                return false;
            }

            if (driver.IsMounted || driver.HasMorphTemplate || driver.IsGhostAction)
            {
                message = $"Local follow request was rejected because {driver.DisplayName} is not follow-eligible.";
                return false;
            }

            if (_lastOutgoingRequestTick != int.MinValue
                && currentTime != int.MinValue
                && currentTime - _lastOutgoingRequestTick < FollowRequestThrottleMs)
            {
                int remainingMs = FollowRequestThrottleMs - Math.Max(0, currentTime - _lastOutgoingRequestTick);
                message = $"Local follow request to {driver.DisplayName} is throttled for another {remainingMs} ms.";
                return false;
            }

            message = null;
            return true;
        }

        private static string DescribeActor(int characterId, Func<int, string> nameResolver)
        {
            if (characterId <= 0)
            {
                return "none";
            }

            string name = nameResolver?.Invoke(characterId)?.Trim();
            return string.IsNullOrWhiteSpace(name)
                ? characterId.ToString()
                : $"{name} ({characterId})";
        }
    }
}
