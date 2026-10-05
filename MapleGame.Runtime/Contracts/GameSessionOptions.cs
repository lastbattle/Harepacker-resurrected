using HaSharedLibrary.Render.DX;
using System;
using System.IO;

namespace HaCreator.MapSimulator.Contracts
{
    /// <summary>Host-selected options copied before constructing a game session.</summary>
    public sealed record GameSessionOptions
    {
        public GameSessionOptions(ISimulatorProfileStorage profileStorage)
        {
            ProfileStorage = profileStorage ?? throw new ArgumentNullException(nameof(profileStorage));
        }

        public ISimulatorProfileStorage ProfileStorage { get; }
        /// <summary>
        /// Explicit session authority. The offline default keeps the detached
        /// preview behavior; an online authority routes production state
        /// through a client-owned direct session.
        /// </summary>
        public GameSessionAuthority Authority { get; init; } = GameSessionAuthority.Offline.Instance;
        /// <summary>Optional append-only sink for the online session acceptance trace.</summary>
        public string OnlineTracePath { get; init; }
        public RenderResolution Resolution { get; init; } = RenderResolution.Res_1024x768;
        public string ContentRootDirectory { get; init; } = Path.Combine(AppContext.BaseDirectory, "Content");
        /// <summary>MapleStory client screenshot folder mode used by packet-owned anti-macro flows.</summary>
        public int AntiMacroScreenshotSaveLocation { get; init; }
        /// <summary>NPC rx0 origin adjustment supplied by the host.</summary>
        public int NpcRx0Offset { get; init; } = 20;
        /// <summary>NPC rx1 origin adjustment supplied by the host.</summary>
        public int NpcRx1Offset { get; init; } = 20;
    }
}
