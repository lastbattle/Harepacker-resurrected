using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using MapleLib.PacketLib;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UnitTest_MapSimulator
{
    [TestClass]
    public class PacketOwnedSocialUtilityDialogDispatcherTests
    {
        [TestMethod]
        public void ParcelOnPacket_OpenSubtype_ReplacesReceiveSessionThroughDispatcher()
        {
            MemoMailboxManager mailbox = new();
            PacketOwnedSocialUtilityDialogDispatcher dispatcher = CreateDispatcher(mailbox: mailbox);

            byte[] payload =
            {
                8,
                0,
                0,
                0
            };

            bool applied = dispatcher.TryApplyParcelPacket(payload, out string message);

            Assert.IsTrue(applied);
            Assert.IsTrue(message.Contains("packet-owned receive owner", StringComparison.Ordinal));
            MemoMailboxSnapshot snapshot = mailbox.GetSnapshot();
            Assert.AreEqual(ParcelDialogTab.Receive, snapshot.ActiveTab);
            Assert.AreEqual(0, snapshot.Entries.Count);
            Assert.IsTrue(dispatcher.DescribeParcelStatus().Contains("Last subtype=8", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TrunkOnPacket_OpenSubtype_ReplacesStorageSnapshotThroughDispatcher()
        {
            SimulatorStorageRuntime storageRuntime = new();
            PacketOwnedSocialUtilityDialogDispatcher dispatcher = CreateDispatcher(storageRuntime: storageRuntime);

            byte[] payload = CreateTrunkSnapshotPayload(22, 28, 54321, new Dictionary<InventoryType, IReadOnlyList<InventorySlotData>>
            {
                [InventoryType.EQUIP] = new[]
                {
                    new InventorySlotData
                    {
                        ItemId = 1302000,
                        Quantity = 1
                    }
                }
            });

            bool applied = dispatcher.TryApplyTrunkPacket(payload, out string message);

            Assert.IsTrue(applied);
            Assert.IsTrue(message.Contains("opened the packet-owned trunk dialog", StringComparison.Ordinal));
            Assert.AreEqual(1, storageRuntime.GetUsedSlotCount());
            Assert.AreEqual(54321, storageRuntime.GetMesoCount());
            Assert.IsTrue(dispatcher.DescribeTrunkStatus().Contains("Last subtype=22", StringComparison.Ordinal));
        }

        [TestMethod]
        public void MessengerOnPacket_SelfEnterResultSubtype_UsesClientOwnedDispatch()
        {
            MessengerRuntime messengerRuntime = new();
            PacketOwnedSocialUtilityDialogDispatcher dispatcher = CreateDispatcher(messengerRuntime: messengerRuntime);

            byte[] payload =
            {
                1,
                0
            };

            bool applied = dispatcher.TryApplyMessengerDispatchPacket(payload, out string message);

            Assert.IsTrue(applied);
            Assert.IsTrue(message.Contains("self-enter", StringComparison.OrdinalIgnoreCase));
            MessengerSnapshot snapshot = messengerRuntime.BuildSnapshot(Environment.TickCount);
            Assert.IsTrue(snapshot.LastPacketSummary.Contains("CUIMessenger::OnPacket dispatched subtype 1", StringComparison.Ordinal));
            Assert.AreEqual(1, snapshot.Participants.Count(participant => participant != null));
        }

        private static PacketOwnedSocialUtilityDialogDispatcher CreateDispatcher(
            MemoMailboxManager mailbox = null,
            SimulatorStorageRuntime storageRuntime = null,
            MessengerRuntime messengerRuntime = null)
        {
            return new PacketOwnedSocialUtilityDialogDispatcher(
                new MapleTvRuntime(),
                mailbox ?? new MemoMailboxManager(),
                () => storageRuntime ?? new SimulatorStorageRuntime(),
                messengerRuntime ?? new MessengerRuntime());
        }

        private static byte[] CreateTrunkSnapshotPayload(
            byte subtype,
            int slotLimit,
            long meso,
            IReadOnlyDictionary<InventoryType, IReadOnlyList<InventorySlotData>> rowsByType)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(subtype);
            writer.Write(slotLimit);
            writer.Write(meso);

            foreach (InventoryType type in new[]
                     {
                         InventoryType.EQUIP,
                         InventoryType.USE,
                         InventoryType.SETUP,
                         InventoryType.ETC,
                         InventoryType.CASH
                     })
            {
                IReadOnlyList<InventorySlotData> rows = rowsByType != null && rowsByType.TryGetValue(type, out IReadOnlyList<InventorySlotData> resolvedRows)
                    ? resolvedRows
                    : Array.Empty<InventorySlotData>();
                writer.Write(rows.Count);
                foreach (InventorySlotData row in rows)
                {
                    writer.Write(row.ItemId);
                    writer.Write(row.Quantity);
                }
            }

            return stream.ToArray();
        }
    }
}
