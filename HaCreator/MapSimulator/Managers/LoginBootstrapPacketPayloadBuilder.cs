using System;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    public static class LoginBootstrapPacketPayloadBuilder
    {
        public static byte[] BuildCheckPinCodeResult(byte resultCode, byte? secondaryCode = null, string textValue = null)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(resultCode);
            if (secondaryCode.HasValue)
            {
                writer.Write(secondaryCode.Value);
            }

            WriteMapleString(writer, textValue);
            writer.Flush();
            return stream.ToArray();
        }

        public static byte[] BuildUpdatePinCodeResult(byte resultCode, byte? secondaryCode = null, string textValue = null)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(resultCode);
            if (secondaryCode.HasValue)
            {
                writer.Write(secondaryCode.Value);
            }

            WriteMapleString(writer, textValue);
            writer.Flush();
            return stream.ToArray();
        }

        public static byte[] BuildEnableSpwResult(bool enabled, byte resultCode = 0, string textValue = null)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(enabled ? (byte)1 : (byte)0);
            writer.Write(resultCode);
            WriteMapleString(writer, textValue);
            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            byte[] bytes = Encoding.ASCII.GetBytes(value);
            writer.Write((short)bytes.Length);
            writer.Write(bytes);
        }
    }
}
