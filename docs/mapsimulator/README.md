# GMS v95 client port: start here

This directory now has two layers: an executable porting plan and retained research.
The target is a **1:1 behavioral port of the GMS v95 client** in `MapleGame.Client`
and `MapleGame.Runtime`, including online account entry, field gameplay, UI, and
service-stage transitions. A playable offline map or a working packet handler does
not satisfy that target. HaCreator's detached offline preview remains a supported
host of the same runtime.

This review is an architecture/source assessment and execution
handoff, not a claim that the port is finished. No gameplay implementation was
changed by this review.

## Read in this order

1. [Current-state audit](CURRENT-STATE.md): verified problems, useful foundations,
   cleanup candidates, and limits of the review.
2. [Port architecture](PORT-ARCHITECTURE.md): ownership and the direct-client
   networking gap.
3. [Execution plan](EXECUTION-PLAN.md): dependency-ordered work packages, ownership,
   acceptance criteria, and an agent handoff template.
4. [Validation](VALIDATION.md): what existing tests prove and how to establish parity.
5. [Native evidence](NATIVE-EVIDENCE.md): the narrowly rechecked movement and
   socket-migration functions.
6. [Research map](RESEARCH-MAP.md): which historical document/section to consult for
   each work package. Read the relevant sections, not every backlog in full.

For the already-extracted host and asset boundaries, also read
[the extraction architecture](../architecture/map-simulator-extraction-plan.md) and
[the client launch guide](../../MapleGame.Client/README.md).

## Status and authority

`EXECUTION-PLAN.md` owns execution order and package status. Domain research owns
recovered addresses, data paths, and historical observations. Current source and
reproducible native observations resolve conflicts with either document.

Historical `Implemented`, `Partial`, and `Missing` rows are **historical claims**;
they are not a current parity certificate. In particular, the movement document
claims completed float landing while the current called implementation retains a
commented-out landing branch. Do not convert an entire subsystem to Complete after
one codec round trip, injected packet, screenshot, or successful build.

The eleven domain backlogs have been normalized with this warning, their literal
source paths now point at `MapleGame.Runtime/Simulator/...`, and the contradicted
float/login summaries were corrected. The 7.9 MB Misc/Edge record remains the sole
archival copy of its long evidence rows; use its
[navigation index](MapSimulator-Client-Parity-Backlog-7-Misc-Edge-INDEX.md) instead
of copying those paragraphs into new tasks.

Work packages start as **Open**. Use **In progress**, **Blocked** (with the specific
missing input), and **Verified** only with the scoped evidence required by the
package. Record implementation and native validation separately. These are project
work-item states, not automatic agent-goal states.

Most historical `HaCreator/MapSimulator/...` paths now resolve under
`MapleGame.Runtime/Simulator/...`. The retained `HaCreator.MapSimulator` namespace
is intentional; it does not create an editor assembly dependency. Resolve a symbol
before editing instead of mechanically renaming every historical path.

## Rules for the next agent

- Claim one work package and its files. Shared `MapSimulator` partials require a
  single integration owner when agents work concurrently.
- Recheck the source at the current revision. The audit baseline is
  `a85382914002a72da14a5d2b2c84032138754acb`; line numbers are navigation aids.
- Preserve useful codecs, native constants, WZ selection logic, and runtime/editor
  ownership. Large files or `Parity` suffixes alone are not deletion evidence.
- Keep native parity behavior separate from deliberate offline conveniences and
  capture/injection tooling. Do not promote a simulator heuristic into the v95
  contract to make tests pass.
- Update the relevant package with a short result and evidence link. Put detailed
  native research in a focused note; do not append another giant history paragraph
  to a legacy table cell. Preserve old research links until its evidence has been
  mapped to a replacement.
- A blocked native/server validation step can coexist with useful source work, but
  its package stays unverified. Do not silently narrow full-client scope to offline.
