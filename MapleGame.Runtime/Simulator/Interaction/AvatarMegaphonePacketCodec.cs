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
        internal const int ConsumeCashItemUseRequestOpcode = 0x55;

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

        internal static byte[] EncodeConsumeCashItemUseRequestPayload(AvatarMegaphoneConsumeCashItemUseRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            PacketWriter writer = new();
            writer.WriteInt(Math.Max(0, request.ClientTick));
            writer.WriteShort(request.InventoryPosition);
            writer.WriteInt(request.ItemId);
            foreach (string fragment in NormalizeFragments(request.MessageFragments))
            {
                writer.WriteMapleString(fragment ?? string.Empty);
            }

            writer.WriteByte((byte)(request.Whisper ? 1 : 0));
            return writer.ToArray();
        }

        internal static bool TryDecodeConsumeCashItemUseRequestPayload(
            byte[] payload,
            out AvatarMegaphoneConsumeCashItemUseRequest request,
            out string error)
        {
            request = null;
            error = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                error = "Avatar megaphone consume-cash item request payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                int clientTick = reader.ReadInt();
                short inventoryPosition = reader.ReadShort();
                int itemId = reader.ReadInt();
                string[] fragments = new string[MessageFragmentCount];
                for (int i = 0; i < fragments.Length; i++)
                {
                    fragments[i] = reader.ReadMapleString();
                }

                bool whisper = reader.ReadByte() != 0;
                request = new AvatarMegaphoneConsumeCashItemUseRequest(
                    clientTick,
                    inventoryPosition,
                    itemId,
                    fragments,
                    whisper);
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "Avatar megaphone consume-cash item request ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "Avatar megaphone consume-cash item request could not be read.";
                return false;
            }
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

    internal sealed class AvatarMegaphoneConsumeCashItemUseRequest
    {
        internal AvatarMegaphoneConsumeCashItemUseRequest(
            int clientTick,
            int inventoryPosition,
            int itemId,
            string[] messageFragments,
            bool whisper)
        {
            ClientTick = clientTick;
            InventoryPosition = inventoryPosition;
            ItemId = itemId;
            MessageFragments = messageFragments ?? Array.Empty<string>();
            Whisper = whisper;
        }

        internal int ClientTick { get; }
        internal int InventoryPosition { get; }
        internal int ItemId { get; }
        internal string[] MessageFragments { get; }
        internal bool Whisper { get; }
    }
}
