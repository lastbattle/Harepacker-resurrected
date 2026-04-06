using HaCreator.MapSimulator.Interaction;
using System;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const string SocialListPacketPayloadUsage =
            "Usage: /sociallist packet [status|<friend|party|guild|alliance|blacklist> <payloadhex=..|payloadb64=..>|owner <tab> <local|packet> [summary]|seed <tab>|clear <tab>|remove <tab> <name>|select <tab> <name>|summary <tab> <summary>|resolve <tab> <approve|reject> [summary]|upsert <tab> <name>|<primary>|<secondary>|<location>|<channel>|<online>|<leader>|<blocked>|<local>|guildauth <clear|payloadhex=..|payloadb64=..|<role>|<rank>|<admission>|<notice>>|allianceauth <clear|payloadhex=..|payloadb64=..|<role>|<rank>|<notice>>|guildui <clear|payloadhex=..|payloadb64=..|<member>|<guildName>|<guildLevel>>]";
        private const string SocialListPacketRawUsage =
            "Usage: /sociallist packetraw <friend|party|guild|alliance|blacklist|guildauth|allianceauth|guildui> <hex>";

        private ChatCommandHandler.CommandResult HandleSocialListPacketCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_socialListRuntime.DescribeStatus());
            }

            string packetAction = args[0].ToLowerInvariant();
            if (TryParseSocialListTabToken(packetAction, out SocialListTab packetTab))
            {
                if (!TryParseSocialListPacketPayloadArgument(args, 1, out byte[] rosterPayload, out string payloadError))
                {
                    return ChatCommandHandler.CommandResult.Error(payloadError ?? SocialListPacketPayloadUsage);
                }

                string clientFamily = packetTab switch
                {
                    SocialListTab.Friend => "CWvsContext::OnFriendResult",
                    SocialListTab.Party => "CWvsContext::OnPartyResult",
                    SocialListTab.Guild => "CWvsContext::OnGuildResult",
                    SocialListTab.Alliance => "CWvsContext::OnAllianceResult",
                    _ => "social-list roster"
                };
                return ChatCommandHandler.CommandResult.Ok(
                    $"{clientFamily}: {_socialListRuntime.ApplyPacketOwnedRosterPayload(packetTab, rosterPayload)}");
            }

            if (string.Equals(packetAction, "guildauth", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] guildAuthorityPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"CWvsContext::OnGuildResult authority: {_socialListRuntime.ApplyPacketOwnedGuildAuthorityPayload(guildAuthorityPayload)}");
            }

            if (string.Equals(packetAction, "allianceauth", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] allianceAuthorityPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"CWvsContext::OnAllianceResult authority: {_socialListRuntime.ApplyPacketOwnedAllianceAuthorityPayload(allianceAuthorityPayload)}");
            }

            if (string.Equals(packetAction, "guildui", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] guildUiPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"CWvsContext::OnGuildResult UI: {_socialListRuntime.ApplyPacketOwnedGuildUiPayload(guildUiPayload)}");
            }

            return ChatCommandHandler.CommandResult.Error(SocialListPacketPayloadUsage);
        }

        private ChatCommandHandler.CommandResult HandleSocialListPacketRawCommand(string[] args)
        {
            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] payload))
            {
                return ChatCommandHandler.CommandResult.Error(SocialListPacketRawUsage);
            }

            string target = args[0];
            if (TryParseSocialListTabToken(target, out SocialListTab rosterTab))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyPacketOwnedRosterPayload(rosterTab, payload));
            }

            if (string.Equals(target, "guildauth", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyPacketOwnedGuildAuthorityPayload(payload));
            }

            if (string.Equals(target, "allianceauth", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyPacketOwnedAllianceAuthorityPayload(payload));
            }

            if (string.Equals(target, "guildui", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyPacketOwnedGuildUiPayload(payload));
            }

            return ChatCommandHandler.CommandResult.Error(SocialListPacketRawUsage);
        }

        private static bool TryParseSocialListPacketPayloadArgument(string[] args, int payloadIndex, out byte[] payload, out string error)
        {
            payload = null;
            error = SocialListPacketPayloadUsage;
            if (args == null || args.Length <= payloadIndex)
            {
                return false;
            }

            if (!TryParseBinaryPayloadArgument(args[payloadIndex], out payload, out string parseError))
            {
                error = parseError ?? SocialListPacketPayloadUsage;
                return false;
            }

            return true;
        }
    }
}
