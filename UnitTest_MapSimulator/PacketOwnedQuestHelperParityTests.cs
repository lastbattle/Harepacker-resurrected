using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator
{
    public sealed class PacketOwnedQuestHelperParityTests
    {
        [Theory]
        [InlineData("resignquest")]
        [InlineData("resignquestreturn")]
        [InlineData("onresignquestreturn")]
        public void TryParsePacketType_RecognizesResignQuestAliases(string token)
        {
            bool parsed = LocalUtilityPacketInboxManager.TryParsePacketType(token, out int packetType);

            Assert.True(parsed);
            Assert.Equal(LocalUtilityPacketInboxManager.ResignQuestReturnClientPacketType, packetType);
        }

        [Theory]
        [InlineData("passmatename")]
        [InlineData("matename")]
        [InlineData("onpassmatename")]
        public void TryParsePacketType_RecognizesPassMateNameAliases(string token)
        {
            bool parsed = LocalUtilityPacketInboxManager.TryParsePacketType(token, out int packetType);

            Assert.True(parsed);
            Assert.Equal(LocalUtilityPacketInboxManager.PassMateNameClientPacketType, packetType);
        }

        [Fact]
        public void ResignQuestReturnPayload_RearmsAutoStartRegistration()
        {
            QuestRuntimeManager questRuntime = CreateQuestRuntimeWithoutWzLoad();
            MapSimulator simulator = CreateSimulatorForPacketOwnedUtilityTests(questRuntime);
            byte[] payload = BuildResignQuestPayload(4451);

            bool applied = InvokePacketHelper(simulator, "TryApplyPacketOwnedResignQuestReturnPayload", payload, out string message);

            Assert.True(applied);
            Assert.True(questRuntime.IsPacketOwnedAutoStartQuestRegistered(4451));
            Assert.Contains("4451", message, StringComparison.Ordinal);
            Assert.Contains("auto-start registration", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PassMateNamePayload_StoresTrimmedMateName()
        {
            QuestRuntimeManager questRuntime = CreateQuestRuntimeWithoutWzLoad();
            MapSimulator simulator = CreateSimulatorForPacketOwnedUtilityTests(questRuntime);
            byte[] payload = BuildPassMateNamePayload(4451, "   Garnox   ");

            bool applied = InvokePacketHelper(simulator, "TryApplyPacketOwnedPassMateNamePayload", payload, out string message);

            Assert.True(applied);
            Assert.True(questRuntime.TryGetPacketOwnedQuestMateName(4451, out string mateName));
            Assert.Equal("Garnox", mateName);
            Assert.Contains("Garnox", message, StringComparison.Ordinal);
        }

        [Fact]
        public void PassMateNamePayload_WithWhitespaceName_ClearsStoredMateName()
        {
            QuestRuntimeManager questRuntime = CreateQuestRuntimeWithoutWzLoad();
            questRuntime.SetPacketOwnedQuestMateName(4451, "ExistingName");
            MapSimulator simulator = CreateSimulatorForPacketOwnedUtilityTests(questRuntime);
            byte[] payload = BuildPassMateNamePayload(4451, "   ");

            bool applied = InvokePacketHelper(simulator, "TryApplyPacketOwnedPassMateNamePayload", payload, out string message);

            Assert.True(applied);
            Assert.False(questRuntime.TryGetPacketOwnedQuestMateName(4451, out _));
            Assert.Contains("Cleared", message, StringComparison.Ordinal);
        }

        private static QuestRuntimeManager CreateQuestRuntimeWithoutWzLoad()
        {
            QuestRuntimeManager questRuntime = new QuestRuntimeManager();
            SetField(questRuntime, "_definitionsLoaded", true);
            return questRuntime;
        }

        private static MapSimulator CreateSimulatorForPacketOwnedUtilityTests(QuestRuntimeManager questRuntime)
        {
            MapSimulator simulator = (MapSimulator)RuntimeHelpers.GetUninitializedObject(typeof(MapSimulator));
            UIManager uiManager = new UIManager
            {
                WindowManager = new UIWindowManager()
            };

            SetField(simulator, "_uiManager", uiManager);
            SetField(simulator, "_questRuntime", questRuntime);
            return simulator;
        }

        private static bool InvokePacketHelper(MapSimulator simulator, string methodName, byte[] payload, out string message)
        {
            MethodInfo method = typeof(MapSimulator).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            object[] arguments = { payload, null };
            bool applied = (bool)method.Invoke(simulator, arguments);
            message = arguments[1] as string ?? string.Empty;
            return applied;
        }

        private static byte[] BuildResignQuestPayload(ushort questId)
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(questId);
            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] BuildPassMateNamePayload(ushort questId, string mateName)
        {
            string safeMateName = mateName ?? string.Empty;
            byte[] nameBytes = Encoding.ASCII.GetBytes(safeMateName);

            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(questId);
            writer.Write((short)nameBytes.Length);
            writer.Write(nameBytes);
            writer.Flush();
            return stream.ToArray();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(target, value);
        }
    }
}
