using System;
using System.Text;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Managers
{
    public static class LoginBootstrapPacketPayloadBuilder
    {
        public static byte[] BuildCheckPinCodeResult(byte resultCode, byte? secondaryCode = null, string textValue = null)
        {
            using PacketWriter writer = new();
            writer.Write(resultCode);
            if (secondaryCode.HasValue)
            {
                writer.Write(secondaryCode.Value);
            }

            WriteMapleString(writer, textValue);
            writer.Flush();
            return writer.ToArray();
        }

        public static byte[] BuildUpdatePinCodeResult(byte resultCode, byte? secondaryCode = null, string textValue = null)
        {
            using PacketWriter writer = new();
            writer.Write(resultCode);
            if (secondaryCode.HasValue)
            {
                writer.Write(secondaryCode.Value);
            }

            WriteMapleString(writer, textValue);
            writer.Flush();
            return writer.ToArray();
        }

        public static byte[] BuildEnableSpwResult(bool enabled, byte resultCode = 0, string textValue = null)
        {
            using PacketWriter writer = new();
            writer.Write(enabled ? (byte)1 : (byte)0);
            writer.Write(resultCode);
            WriteMapleString(writer, textValue);
            writer.Flush();
            return writer.ToArray();
        }

        private static void WriteMapleString(PacketWriter writer, string value)
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
