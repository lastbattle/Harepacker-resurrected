using System;

namespace HaCreator.MapSimulator.Contracts
{
    /// <summary>
    /// Explicit session authority selected by the host. Online authority means
    /// server-authored state drives the runtime through a client-owned direct
    /// connection; offline authority preserves editor preview, detached maps
    /// and local behavior. Production mutations must consult this contract
    /// instead of inferring mode from feature flags.
    /// </summary>
    public abstract record GameSessionAuthority
    {
        private GameSessionAuthority()
        {
        }

        public sealed record Offline : GameSessionAuthority
        {
            public static Offline Instance { get; } = new();

            private Offline()
            {
            }

            public override string ToString() => "Offline";
        }

        public sealed record Online : GameSessionAuthority
        {
            public Online(string loginHost, int loginPort)
            {
                if (string.IsNullOrWhiteSpace(loginHost))
                    throw new ArgumentException("Online authority requires a login host.", nameof(loginHost));
                ArgumentOutOfRangeException.ThrowIfNegative(loginPort);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(loginPort, ushort.MaxValue);

                LoginHost = loginHost.Trim();
                LoginPort = loginPort;
            }

            public string LoginHost { get; }
            public int LoginPort { get; }

            public override string ToString() => $"Online (login {LoginHost}:{LoginPort})";
        }
    }
}
