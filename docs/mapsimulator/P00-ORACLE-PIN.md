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
| v95 WZ/IMG export | Not pinned yet in this repo; comparison scenarios below must be re-run against the identified export before any visual/data claim. |
| Controlled compatible v95 server | Not available in this environment; live level-E evidence is pending. |
| Login endpoint assumption | Classic login listener on port 8484 when `--online` omits the port. |
| Migration endpoint | Server-authored: decoded from the login MigrateCommand payload (address + port), matching `CClientSocket::OnMigrateCommand`. |

## Confirmed native observations (this slice)

| ID | Address | Symbol | Confirmed behavior |
|---|---|---|---|
| N04a | `0x9e0300` | `CWvsContext::IssueConnect` | Obtains the `CClientSocket` singleton, calls `Close`, builds a `CONNECTCONTEXT` with the supplied address and `bLogin = 0`, then calls `Connect` on the same owner. |
| N04b | `0x4add50` | `CClientSocket::OnMigrateCommand` | Success branch ensures an `CInterStage`, decodes a 4-byte address and 2-byte port, then calls `IssueConnect`. Failure branch throws a `CDisconnectException` for non-guests and returns guests to the title path. |
| N04c | `0x4ae990` | `CClientSocket::Close` | `ClearSendReceiveCtx` (crypto teardown), `closesocket`, handle reset to `-1`. |

## Open native follow-up

`CClientSocket::Connect` and `CClientSocket::OnConnect` are obfuscated
(VM-style control flow), so the exact packet the client sends after a
channel-connect handshake is not yet recovered. The transport slice implements
close/connect/handshake/generations from N04; the post-migration client-to-
channel request packet remains a scoped P04 follow-up.

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
