using HaCreator.MapSimulator.Character;
using System;
using System.Globalization;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int MonsterBookRegistrationResponseDelayMs = 120;
        private PendingMonsterBookRegistrationRequest _pendingMonsterBookRegistrationRequest;

        private sealed class PendingMonsterBookRegistrationRequest
        {
            public CharacterBuild Build { get; init; }
            public int CharacterId { get; init; }
            public string CharacterName { get; init; } = string.Empty;
            public int MobId { get; init; }
            public bool Registered { get; init; }
            public long SentTick { get; init; }
            public int ResponseDelayMs { get; init; } = MonsterBookRegistrationResponseDelayMs;
            public string RequestSummary { get; init; } = string.Empty;
        }

        private string DispatchMonsterBookRegistrationRequest(
            CharacterBuild build,
            int characterId,
            string characterName,
            int mobId,
            bool registered,
            out int responseDelayMs)
        {
            responseDelayMs = MonsterBookRegistrationResponseDelayMs;
            string actionLabel = registered ? "register" : "release";
            string ownerLabel = string.IsNullOrWhiteSpace(characterName)
                ? "the active character"
                : characterName.Trim();
            return $"Book Collection queued a local {actionLabel} request for mob {mobId.ToString(CultureInfo.InvariantCulture)} on {ownerLabel}.";
        }

        private void ProcessPendingMonsterBookRegistrationRequest()
        {
            PendingMonsterBookRegistrationRequest request = _pendingMonsterBookRegistrationRequest;
            if (request == null)
            {
                return;
            }

            long elapsedMs = Environment.TickCount64 - request.SentTick;
            if (elapsedMs < request.ResponseDelayMs)
            {
                return;
            }

            _pendingMonsterBookRegistrationRequest = null;
            _monsterBookManager.SetRegisteredCard(
                request.Build,
                request.CharacterId,
                request.CharacterName,
                request.MobId,
                request.Registered);

            string actionLabel = request.Registered ? "registered" : "released";
            ShowUtilityFeedbackMessage(
                $"Monster Book {actionLabel} mob {request.MobId.ToString(CultureInfo.InvariantCulture)} ({request.CharacterName}) via the local parity response lane.");
        }
    }
}
