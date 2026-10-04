# Native evidence recorded during the port review

These observations were collected through the IDA-pro MCP integration. Links to the
relevant database views or functions can be added when they are available. Package
P00 must pin the executable and WZ/IMG content used for comparisons in the private
comparison record rather than publishing local environment details here.

The following are fresh static decompilation observations. They do not demonstrate
live gameplay, correct managed call-site wiring, or complete parity. Exact-name
lookups did not resolve the demangled names; address lookups resolved decorated
symbols at the addresses below. No database symbols or bytes were changed.

## N01: movement flush admission

`CMovePath::IsTimeForFlush`, `0x666870`, resolved as
`?IsTimeForFlush@CMovePath@@QAEHHH@Z`.

Observed control flow:

- Empty element list returns false.
- With short-update enabled, the duration threshold is 200 ms for a nonzero
  dynamic-foothold argument and 500 ms otherwise; without short-update it is 1000 ms.
- Gather duration below the threshold returns false.
- For non-short-update and non-flying motion, the function walks backward through
  elements and requires a positive foothold ID somewhere in the retained list.
- The remaining admitted cases return true.

Hex-Rays displays the second argument as a pointer-like type; its use in the
threshold expression is a nonzero test. Verify types/callers before copying that
signature. These thresholds alone do not prove packet emission timing: inspect
`MakeMovePath`, `Flush`, the caller, and the game clock together in P05.

## N02: movement encoding

`CMovePath::Encode`, `0x666e20`, resolved as
`?Encode@CMovePath@@QAEXAAVCOutPacket@@PAH@Z`.

Observed encoding begins with four 16-bit fields (x, y, vx, vy) and a one-byte
element count. Elements have an attribute byte with branch-specific fields; most
branches append move-action and elapsed-time fields, while attribute 9 has its own
status-byte branch. Client option 2 gates two random-count fields on applicable
elements. The function then emits a keypad-state count, pairs of packed nibbles,
and four 16-bit movement-rectangle bounds.

The managed `Simulator/Physics/CMovePathClientPacketCodec.cs` already has header,
attribute, random-count and optional flush-tail handling. This is useful existing
work, not evidence to replace the codec wholesale. Audit every real packet owner
for the correct header, option and tail choices; byte-for-byte fixtures from native
output must validate the full packet context. A managed encode/decode round trip
does not independently validate either side.

`CMovePath::Decode` at `0x667920` and `CUserLocal::Update` at `0x937330` resolved to
the expected decorated symbols. Their bodies were not reviewed in this pass.

## N03: floating collision

`CVecCtrl::CollisionDetectFloat`, `0x994740`, resolved as
`?CollisionDetectFloat@CVecCtrl@@UAEHABUAbsPos@@AAJH@Z`.

The native body does substantially more than bounding x/y to the map. It queries
crossing foothold candidates from the physical space, filters foothold state and
layer/mass relationships, handles reserved landing footholds and adjacent segment
ties, chooses a collision fraction, consumes elapsed time, projects velocity onto
the selected surface, and updates absolute/relative positions and owner callbacks.
It also contains sign-sensitive coordinate rounding. Recover individual branches
and types against callers and assembly before implementing them.

The managed `Simulator/Physics/CVecCtrl.cs::CollisionDetectFloat` currently has a
commented-out downward-landing block and active map-bound clamps. Its caller in
the floating update path is present. This directly contradicts the historical
movement backlog's claim that this seam has configured lookup/crossing-based landing.
Other player movement code may compensate for some cases; that does not implement
this native collision contract. P05 must trace the complete player path and compare
crossing, wall, slope, layer, reserved-landing and elapsed-time behavior.

## N04: endpoint migration uses the existing client socket owner

Freshly decompiled after the movement pass:

- `CWvsContext::IssueConnect`, `0x9e0300`: obtains
  `TSingleton<CClientSocket>::ms_pInstance`, calls `Close`, creates a connection
  context with the supplied address and `bLogin = 0`, then calls `Connect` on that
  same owner.
- `CClientSocket::OnMigrateCommand`, `0x4add50`: on its success branch ensures an
  interstage, decodes the address and port, and calls `CWvsContext::IssueConnect`.
  Its failure branch distinguishes guest and non-guest handling.
- `CClientSocket::Close`, `0x4ae990`: calls `ClearSendReceiveCtx`, closes a valid
  socket handle, and resets the handle to -1.

This confirms the migration ownership problem described in
[the architecture note](PORT-ARCHITECTURE.md). Sharing a relay per role is useful
tooling, but does not implement this direct-client close/connect transition.
This inspection does not prove all login, shop, ITC or error branches; verify each
call site and its request/state contract before implementing P02-P04 or P11.

## Extending this record

Give each future observation a stable ID, executable/data identity, address and
symbol, caller context, exact behavior established, uncertainty, managed owner,
and a reproducible comparison. Keep live trace/packet/screenshot artifacts with
their scenario metadata outside copyrighted game-asset source trees. Link them
from the owning package and [validation record](VALIDATION.md).
