# P00: pinned v95 oracle and comparison scenarios

This record pins the oracle used for the P00-P04 slice. Keep local environment
details (install paths, machine identity) out of the repository; store them in
the private comparison record.

## Pinned executable identity

| Field | Value |
|---|---|
| Build | GMS v95 client (`MapleStory.exe`) |
| SHA-256 | `D73827EB68DDB5B2CF73C7D2AC069B394C76B3B9586E7463FB58A15240FB09ED` |
| Size (bytes) | 13557760 |
| Locale | GMS (English, global service) |

The IDA database for this build is available in the working environment and was
used for the native observations below. All decompilation was read-only.

## Pinned data and server assumptions

| Input | Status |
|---|---|
| v95 WZ/IMG export | Pinned: `gms_v95` IMG filesystem export (pre-BB, GMS encryption, patch version 95, manifest stamp `v20260305_0140`, manifest SHA-256 `E7C34103B9892C1356B6D094B589ADFA218ADC205CD6FCA2315C5726D771ACEB`). Categories: Base, Character (8316 files), Effect, Etc, Item, List, Map (6816), Mob (1893), Morph, Npc (1838), Quest (3231), Reactor (454), Skill (117), Sound, String, TamingMob, UI. |
| Controlled compatible v95 server | Not available in this environment; live level-E evidence is pending. |
| Login endpoint assumption | Classic login listener on port 8484 when `--online` omits the port. |
| Migration endpoint | Server-authored: decoded from the login MigrateCommand payload (address + port), matching `CClientSocket::OnMigrateCommand`. |

## Confirmed native observations (this slice)

| ID | Address | Symbol | Confirmed behavior |
|---|---|---|---|
| N04a | `0x9e0300` | `CWvsContext::IssueConnect` | Obtains the `CClientSocket` singleton, calls `Close`, builds a `CONNECTCONTEXT` with the supplied address and `bLogin = 0`, then calls `Connect` on the same owner. |
| N04b | `0x4add50` | `CClientSocket::OnMigrateCommand` | Success branch ensures an `CInterStage`, decodes a 4-byte address and 2-byte port, then calls `IssueConnect`. Failure branch throws a `CDisconnectException` for non-guests and returns guests to the title path. |
| N04c | `0x4ae990` | `CClientSocket::Close` | `ClearSendReceiveCtx` (crypto teardown), `closesocket`, handle reset to `-1`. |
| N05 | `0x4aef10` / `0x4af9f0` | `CClientSocket::OnConnect` / `SendPacket` | Every successful connection emits opcode 0x1A with `[u16 length][hwid blob]` (blob file-validated: version 0x5F, format 8, cap 0x2000). |
| N06 | `0x9dc4d1` / `0x9df13d` | `SendMigrateToShopRequest` / `SendMigrateToITCRequest` | Shop migration request is opcode 43 with `Encode4(update_time)`; ITC is opcode 180 empty; both on the current connection, then the N04 flow dials the service endpoint. `ConnectLogin` (0x4b0590) is obfuscated. |

## Resolved native follow-up

The post-connect request packet is recovered as N05 in
[NATIVE-EVIDENCE.md](NATIVE-EVIDENCE.md): `CClientSocket::OnConnect` emits
opcode 0x1A with `[u16 length][hwid blob]` on every successful connection
(an empty blob is the native case for an absent hwid file), so the channel
side of a migration receives this as the client's first request. The direct
session owner sends the same packet after its handshake; the hwid blob source
for a specific controlled server remains a configuration input.

## Comparison scenarios (acceptance gates)

These are the level-E scenarios for the slice. Each needs a controlled server
and the pinned export before it can be checked; a passing managed test does not
close them.

1. Login handshake success (v95 init, wrong-version rejection) and authentication
   result handling.
2. World/channel data and character roster rendered from server packets.
3. Character selection closes the login connection before the channel migration
   dial (N04 order).
4. Server-authored field state applied before field input is enabled.
5. One field action produces the expected server response.
6. Disconnect and reconnect without retained sockets, crypto state, packets,
   actors, or scene state.
