using System;
using System.Collections.Generic;
using System.Text;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketScriptClientMessageBuilder
    {
        internal static byte[] BuildQuizClientPacket(
            int npcId,
            string title,
            string problemText,
            string hintText,
            int minInputLength,
            int maxInputLength,
            int remainingSeconds)
        {
            using PacketWriter writer = CreateHeader(npcId, 6);
            writer.Write((byte)0);
            WriteInitialQuizClientMapleString(writer, title);
            WriteInitialQuizClientMapleString(writer, problemText);
            WriteInitialQuizClientMapleString(writer, hintText);
            writer.Write(minInputLength);
            writer.Write(maxInputLength);
            writer.Write(remainingSeconds);
            return writer.ToArray();
        }

        internal static byte[] BuildQuizClientClosePacket(int npcId)
        {
            using PacketWriter writer = CreateHeader(npcId, 6);
            writer.Write((byte)1);
            return writer.ToArray();
        }

        internal static byte[] BuildSpeedQuizClientPacket(
            int npcId,
            int currentQuestion,
            int totalQuestions,
            int correctAnswers,
            int remainingQuestions,
            int remainingSeconds)
        {
            using PacketWriter writer = CreateHeader(npcId, 7);
            writer.Write((byte)0);
            writer.Write(currentQuestion);
            writer.Write(totalQuestions);
            writer.Write(correctAnswers);
            writer.Write(remainingQuestions);
            writer.Write(remainingSeconds);
            return writer.ToArray();
        }

        internal static byte[] BuildSpeedQuizClientClosePacket(int npcId)
        {
            using PacketWriter writer = CreateHeader(npcId, 7);
            writer.Write((byte)1);
            return writer.ToArray();
        }

        internal static byte[] BuildPetClientPacket(
            int npcId,
            string prompt,
            IReadOnlyList<long> petSerialNumbers)
        {
            return BuildPetClientPacketCore(npcId, 10, prompt, petSerialNumbers, exceptionExists: false);
        }

        internal static byte[] BuildPetAllClientPacket(
            int npcId,
            string prompt,
            IReadOnlyList<long> petSerialNumbers,
            bool exceptionExists)
        {
            return BuildPetClientPacketCore(npcId, 11, prompt, petSerialNumbers, exceptionExists);
        }

        internal static byte[] BuildSlideMenuClientPacket(int npcId, int slideMenuType, string buttonInfo)
        {
            return BuildSlideMenuClientPacket(npcId, slideMenuType, initialSelectionId: 0, buttonInfo);
        }

        internal static byte[] BuildSlideMenuClientPacket(int npcId, int slideMenuType, int initialSelectionId, string buttonInfo)
        {
            using PacketWriter writer = CreateHeader(npcId, 15);
            writer.Write(slideMenuType);
            writer.Write(initialSelectionId);
            WriteMapleString(writer, buttonInfo);
            return writer.ToArray();
        }

        private static byte[] BuildPetClientPacketCore(
            int npcId,
            byte messageType,
            string prompt,
            IReadOnlyList<long> petSerialNumbers,
            bool exceptionExists)
        {
            using PacketWriter writer = CreateHeader(npcId, messageType);
            WriteMapleString(writer, prompt);
            int count = Math.Min(byte.MaxValue, petSerialNumbers?.Count ?? 0);
            writer.Write((byte)count);
            if (messageType == 11)
            {
                writer.Write(exceptionExists ? (byte)1 : (byte)0);
            }

            for (int i = 0; i < count; i++)
            {
                writer.Write(petSerialNumbers[i]);
                writer.Write((byte)0);
            }

            return writer.ToArray();
        }

        private static PacketWriter CreateHeader(int npcId, byte messageType)
        {
            PacketWriter writer = new();
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write(messageType);
            writer.Write((byte)0);
            return writer;
        }

        private static void WriteMapleString(PacketWriter writer, string value)
        {
            value ??= string.Empty;
            byte[] bytes = Encoding.Default.GetBytes(value);
            int length = Math.Min(bytes.Length, ushort.MaxValue);
            writer.Write((ushort)length);
            writer.Write(bytes, 0, length);
        }

        private static void WriteInitialQuizClientMapleString(PacketWriter writer, string value)
        {
            byte[] bytes = InitialQuizTimerRuntime.EncodeClientMapleString(value);
            int length = Math.Min(bytes.Length, ushort.MaxValue);
            writer.Write((ushort)length);
            writer.Write(bytes, 0, length);
        }
    }
}
