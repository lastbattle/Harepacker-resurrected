using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketScriptClientMessageBuilder
    {
        internal static byte[] BuildQuizClientPacket(
            int npcId,
            string title,
            string problemText,
            string hintText,
            int correctAnswer,
            int questionNumber,
            int remainingSeconds)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = CreateHeader(stream, npcId, 6);
            writer.Write((byte)0);
            WriteMapleString(writer, title);
            WriteMapleString(writer, problemText);
            WriteMapleString(writer, hintText);
            writer.Write(correctAnswer);
            writer.Write(questionNumber);
            writer.Write(remainingSeconds);
            return stream.ToArray();
        }

        internal static byte[] BuildQuizClientClosePacket(int npcId)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = CreateHeader(stream, npcId, 6);
            writer.Write((byte)1);
            return stream.ToArray();
        }

        internal static byte[] BuildSpeedQuizClientPacket(
            int npcId,
            int currentQuestion,
            int totalQuestions,
            int correctAnswers,
            int remainingQuestions,
            int remainingSeconds)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = CreateHeader(stream, npcId, 7);
            writer.Write((byte)0);
            writer.Write(currentQuestion);
            writer.Write(totalQuestions);
            writer.Write(correctAnswers);
            writer.Write(remainingQuestions);
            writer.Write(remainingSeconds);
            return stream.ToArray();
        }

        internal static byte[] BuildSpeedQuizClientClosePacket(int npcId)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = CreateHeader(stream, npcId, 7);
            writer.Write((byte)1);
            return stream.ToArray();
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
            using MemoryStream stream = new();
            using BinaryWriter writer = CreateHeader(stream, npcId, 15);
            writer.Write(slideMenuType);
            writer.Write(initialSelectionId);
            WriteMapleString(writer, buttonInfo);
            return stream.ToArray();
        }

        private static byte[] BuildPetClientPacketCore(
            int npcId,
            byte messageType,
            string prompt,
            IReadOnlyList<long> petSerialNumbers,
            bool exceptionExists)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = CreateHeader(stream, npcId, messageType);
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

            return stream.ToArray();
        }

        private static BinaryWriter CreateHeader(Stream stream, int npcId, byte messageType)
        {
            BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write(messageType);
            writer.Write((byte)0);
            return writer;
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            value ??= string.Empty;
            byte[] bytes = Encoding.Default.GetBytes(value);
            writer.Write((ushort)Math.Min(bytes.Length, ushort.MaxValue));
            writer.Write(bytes, 0, Math.Min(bytes.Length, ushort.MaxValue));
        }
    }
}
