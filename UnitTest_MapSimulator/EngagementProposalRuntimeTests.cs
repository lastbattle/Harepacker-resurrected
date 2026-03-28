using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.Wz;
using System;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class EngagementProposalRuntimeTests
    {
        [Fact]
        public void TryAccept_OpenProposal_EmitsClientAcceptPacketAndClosesDialog()
        {
            EngagementProposalRuntime runtime = new();
            runtime.OpenProposal("Athena", "Lirin");

            bool accepted = runtime.TryAccept(out EngagementProposalResponse response, out string message);
            EngagementProposalSnapshot snapshot = runtime.BuildSnapshot();

            Assert.True(accepted);
            Assert.Equal(EngagementProposalRuntime.AcceptPacketType, response.PacketType);
            Assert.Equal(new byte[] { EngagementProposalRuntime.AcceptPayloadValue }, response.Payload);
            Assert.False(snapshot.IsOpen);
            Assert.True(snapshot.LastAccepted);
            Assert.Contains("packet 161", message);
        }

        [Fact]
        public void Dismiss_OpenProposal_DoesNotEmitPacketResponse()
        {
            EngagementProposalRuntime runtime = new();
            runtime.OpenProposal("Athena", "Lirin");

            string message = runtime.Dismiss();
            EngagementProposalSnapshot snapshot = runtime.BuildSnapshot();

            Assert.False(snapshot.IsOpen);
            Assert.False(snapshot.LastAccepted);
            Assert.Equal(-1, snapshot.LastResponsePacketType);
            Assert.Empty(snapshot.LastResponsePayload);
            Assert.Contains("without sending", message);
        }

        [Fact]
        public void OpenProposal_UsesCachedRingAndSealMetadata()
        {
            WzInformationManager previousInfoManager = global::HaCreator.Program.InfoManager;
            try
            {
                WzInformationManager infoManager = new();
                infoManager.ItemNameCache[2240000] = Tuple.Create("Use", "Moonstone Engagement Ring Box", "Required for proposal.");
                infoManager.ItemNameCache[4210000] = Tuple.Create("Etc", "Moonstone Wedding Ticket", "If you discard it, you break off the engagement.");
                global::HaCreator.Program.InfoManager = infoManager;

                EngagementProposalRuntime runtime = new();
                runtime.UpdateLocalContext(new CharacterBuild { Name = "Lirin" });
                runtime.OpenProposal("Athena", "Lirin", 2240000, 4210000, "Will you marry me?");

                EngagementProposalSnapshot snapshot = runtime.BuildSnapshot();

                Assert.Contains("Moonstone Engagement Ring Box", snapshot.BodyText);
                Assert.Contains("Moonstone Wedding Ticket", snapshot.BodyText);
                Assert.Contains("Will you marry me?", snapshot.BodyText);
            }
            finally
            {
                global::HaCreator.Program.InfoManager = previousInfoManager;
            }
        }
    }
}
