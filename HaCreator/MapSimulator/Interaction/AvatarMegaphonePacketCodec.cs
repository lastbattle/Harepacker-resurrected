using HaCreator.MapSimulator.Managers;
using MapleLib.PacketLib;
using System;
using System.IO;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class AvatarMegaphonePacketCodec
    {
        internal const int MessageFragmentCount = 4;

        internal static bool TryDecodeSetAvatarMegaphonePayload(
            byte[] payload,
            out AvatarMegaphonePacket packet,
            out string error)
        {
            packet = null;
            error = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                error = "Avatar megaphone packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                int itemId = reader.ReadInt();
                string sender = reader.ReadMapleString();
                string[] fragments = new string[MessageFragmentCount];
                for (int i = 0; i < fragments.Length; i++)
                {
                    fragments[i] = reader.ReadMapleString();
                }

                int channelId = reader.ReadInt();
                bool whisper = reader.ReadByte() != 0;
                if (!LoginAvatarLookCodec.TryDecode(reader, out LoginAvatarLook avatarLook, out error))
                {
                    return false;
                }

                packet = new AvatarMegaphonePacket(itemId, sender, fragments, channelId, whisper, avatarLook);
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "Avatar megaphone packet ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "Avatar megaphone packet could not be read.";
                return false;
            }
        }

        internal static byte[] EncodeSetAvatarMegaphonePayload(AvatarMegaphonePacket packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            PacketWriter writer = new();
            writer.WriteInt(packet.ItemId);
            writer.WriteMapleString(packet.Sender ?? string.Empty);
            foreach (string fragment in NormalizeFragments(packet.MessageFragments))
            {
                writer.WriteMapleString(fragment ?? string.Empty);
            }

            writer.WriteInt(packet.ChannelId);
            writer.WriteByte((byte)(packet.Whisper ? 1 : 0));
            writer.WriteBytes(LoginAvatarLookCodec.Encode(packet.AvatarLook));
            return writer.ToArray();
        }

        private static string[] NormalizeFragments(System.Collections.Generic.IEnumerable<string> fragments)
        {
            string[] normalized = (fragments ?? Array.Empty<string>())
                .Take(MessageFragmentCount)
                .ToArray();
            if (normalized.Length < MessageFragmentCount)
            {
                Array.Resize(ref normalized, MessageFragmentCount);
            }

            for (int i = 0; i < normalized.Length; i++)
            {
                normalized[i] ??= string.Empty;
            }

            return normalized;
        }
    }

    internal sealed class AvatarMegaphonePacket
    {
        internal AvatarMegaphonePacket(
            int itemId,
            string sender,
            string[] messageFragments,
            int channelId,
            bool whisper,
            LoginAvatarLook avatarLook)
        {
            ItemId = itemId;
            Sender = sender ?? string.Empty;
            MessageFragments = messageFragments ?? Array.Empty<string>();
            ChannelId = channelId;
            Whisper = whisper;
            AvatarLook = avatarLook;
        }

        internal int ItemId { get; }
        internal string Sender { get; }
        internal string[] MessageFragments { get; }
        internal int ChannelId { get; }
        internal bool Whisper { get; }
        internal LoginAvatarLook AvatarLook { get; }
    }
}
