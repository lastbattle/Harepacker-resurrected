# P03 direct-session route inventory

Single ingress path: `MapleOnlineDirectSessionOwner` queues decrypted packets
from the active direct session and dispatches them on the game thread via
`DrainPendingInbound` to registered `IMapleOnlineStageHandler` implementations.
Handlers own opcode filtering; the owner owns connection lifecycle, migration
(N04), the post-connect hwid packet (N05), generations, and the trace ring.
Dispatch uses `AcceptsPacketRole`, so a handler can consume additional roles
during the post-migration window instead of the owner hardcoding stage rules.

## Wired handlers

| Handler | Role(s) accepted | Inbound opcodes | Outbound |
|---|---|---|---|
| `LoginOfficialSessionBridgeManager` | Login; Channel while `DirectChannelInboundEnabled` (post-migration window) | `LoginPacketType` numeric table (0-27, 141-146, 413, 414) plus runtime-configured mappings; unmapped opcodes are ignored | `TrySend*Request` builders prefer the owner's Login session |
| `PacketFieldOfficialSessionBridgeManager` | Channel | Field-scoped state opcodes 93, 149, 162, 163, 166, 167, 169, 174, 178 | `TrySendOutboundPacket` via the owner's Channel session |
| `ReactorPoolOfficialSessionBridgeManager` | Channel | Reactor-pool opcodes (334-337) | Touch-reactor requests (opcode 250) prefer the owner's Channel session |

## Relay-only (unchanged)

The remaining role-session bridges (cash, MTS, messenger, expedition, dojo,
carnival, massacre, transportation, snowball, coconut, memory game, cookie
house, guild boss, tournament, party raid, rock-paper-scissors, social list,
merchant/employee rooms, summoned, admin shop, local utility, map transfer,
field message box, packet script) keep their `MapleRoleSessionProxy` relay
ownership. Wiring each into the owner follows the same three-step pattern:
accept the owner in the constructor, implement the handler interface, prefer
the owner in the send path. Cash/ITC additionally need the N06 request opcodes
(43/180) sent on the field session before migrating (P11).

## Rules for adding handlers

1. Add the owner parameter to the bridge constructor and register the bridge.
2. Keep the existing private queue and drain semantics; the owner fan-out must
   not bypass per-stage opcode filtering.
3. Route outbound through the owner only while the role session is connected;
   keep the relay path as capture fallback.
