# P01 authority and disposition inventory

Structural inventory of the online/offline seams, debug surfaces and cleanup
candidates, with real callers and disposition. Per-caller line references are
omitted on purpose: the surface is uniform and the owning files are named.

## Online session authority (production, wired to the owner)

| Seam | Callers | Disposition |
|---|---|---|
| `MapleOnlineDirectSessionOwner` | `MapSimulator` ctor (online authority only), `PumpOnlineSessionLifecycle`, `BeginOnlineMigrationFromLoginHandoff` | Keep; the only production transport authority. |
| `LoginOfficialSessionBridgeManager` | Ctor wiring, `DrainLoginPacketInbox`, `TrySendLive*Request`, `TryResolveLiveOfficialCheckPasswordAuth` | Keep; login stage handler + outbound builders. |
| `PacketFieldOfficialSessionBridgeManager` | Ctor wiring, field-state drain | Keep; field stage handler. |
| `ReactorPoolOfficialSessionBridgeManager` | Ctor wiring, reactor runtime touch requests | Keep; field action handler. |
| `GameSessionAuthority` / `GameSessionOptions.Authority` | Host (`Program.cs --online`), `MapSimulator` ctor, `PumpOnlineSessionLifecycle` | Keep; explicit mode contract, offline default. |

## Relay capture tooling (offline/injection, not production authority)

| Seam | Callers | Disposition |
|---|---|---|
| `MapleRoleSessionProxy` + factory (26 role bridges: cash, MTS, messenger, expedition, dojo, carnival, massacre, transportation, snowball, coconut, memory game, cookie house, guild boss, tournament, party raid, rock-paper-scissors, social list, merchant/employee rooms, summoned, admin shop, local utility, map transfer, field message box, packet script, remote user, social room merchant) | `MapSimulator` ctor, chat session commands, update-loop discovery/refresh | Keep as capture/replay tooling; wire to the owner later per `P03-ROUTE-INVENTORY.md`; no removal. |
| 32 `*PacketInboxManager` / `*PacketTransportManager` state owners (login, field, reactor, guild boss, local utility, stage transition, wedding, mob attack, NPC pool, combo counter, ...) | Stage drains in `MapSimulator*.cs` partials | Keep; per-stage state application and queue semantics. |
| `Retired listener` status shims (e.g. `LocalUtilityPacketTransportManager` loopback) | Status surfaces, chat commands | Keep until the owner wiring replaces each outbound path; then consolidate in the owning slice. |

## Debug / injection surfaces

| Surface | Examples | Disposition |
|---|---|---|
| Chat command families in `MapSimulator.ChatCommands.cs` and session partials (`/loginpacket`, `/fieldstate`, `/guildboss`, `/partyraid`, `/messenger`, `/family`, `/guildbbs`, `/guildui`, and the per-feature `raw`/`packetraw` injectors) | `HandleChatCommand`, per-family dispatchers | Keep for capture/verification and offline scenarios. The online production flow must not depend on them (P09: debug text is not normal client UI); the manual auth command stays as the machine-id/passport override. |
| `LoginIssuedDirectConnect` record | `IssuePacketOwned*DirectConnect`, online migration | Keep; now feeds the real migration instead of endpoint-only metadata. |
| Description-backed skill aliases (`PacketOwnedSkillAliasCatalog`, `SkillLoader` alias helpers) | Skill preview/cast paths | P06/P07: replace with v95 data/ID rules in native mode; keep for generic preview. |
| Approximation owners flagged in `CURRENT-STATE.md` (`PlayerCombat` variance/bounds, `MobAI` heuristics, revive spawn approximation) | Combat/mob/death paths | P07/P08: replace or isolate per package; do not promote into the v95 contract. |
| Generated string pools, packet constants, native tables | Across `Simulator/` | Keep unless independently disproved; size/naming is not deletion evidence. |

## Offline behavior guarantees

`GameSessionAuthority.Offline` is the default: no owner is constructed, the
offline client mode flags default true, the title/roster flows keep their
bootstrap behavior, and no online code path runs. All online additions gate on
the authority, so the detached editor preview is unchanged.
