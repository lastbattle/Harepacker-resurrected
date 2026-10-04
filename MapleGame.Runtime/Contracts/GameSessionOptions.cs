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
