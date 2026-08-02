using System.Net;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Captures the simulator-side handoff that mirrors the client's
    /// CWvsContext::IssueConnect call before field-entry audio is played.
    /// </summary>
    public sealed class LoginIssuedDirectConnect
    {
        public string SourcePacket { get; init; }
        public IPAddress ServerAddress { get; init; }
        public ushort Port { get; init; }
        public int CharacterId { get; init; }
        public byte AuthenCode { get; init; }
        public int PremiumArgument { get; init; }

        public bool IsPremium => ((AuthenCode >> 1) & 1) != 0;

        public string EndpointText =>
            ServerAddress != null
                ? $"{ServerAddress}:{Port}"
                : null;
    }
}
