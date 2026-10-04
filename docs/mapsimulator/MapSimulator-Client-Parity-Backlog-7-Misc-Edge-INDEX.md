# Misc/Edge research navigation

`MapSimulator-Client-Parity-Backlog-7-Misc-Edge.md` remains the archival research
record because its evidence index and remaining-work tables contain long, paired
native observations. It is intentionally not copied into smaller files: duplicate
copies would drift and make status claims even less reliable. Use the execution
packages as the current work queue and this page to find the relevant section.

| Research family | Execution package | Section in the archival file |
|---|---|---|
| NPC talk, NPC pool, NPC assets and actions | P08, P06 | `Interaction and NPC parity`; `NPC pool lifecycle and runtime presentation` |
| Reactor layer build, hit props and packet lifecycle | P08, P12 | `Reactor layer-build and hit-prop ownership`; `Reactor packet lifecycle` |
| Direction mode and stand-alone mode | P05, P09 | `Packet-authored direction and stand-alone mode parity`; `Direction-mode release timing` |
| Field state, scripts and packet feedback | P03, P08, P12 | `Field-state and script-message parity`; `Packet-authored field messaging and feedback` |
| Animation, projectile, social/event effects | P06, P07, P13 | `CAnimationDisplayer` animation-owner sections |
| Sound manager and audio lifecycle | P13 | `Shared sound-manager and audio lifecycle parity` |
| Field utility, stage transition and map-load presentation | P03, P04, P12 | `Packet-authored field utility parity`; `Stage-transition and map-load presentation` |
| Passenger movement and context-owned stage period | P04, P05 | `Passenger passive-move parity`; `Context-owned stage-period transition` |
| Initial quiz and utility owner | P09, P12 | `Initial-quiz utility owner parity` |

Search the archival file for the exact heading text. The file contains both an
evidence index and a remaining-work section, so heading names can appear twice.
Read the row under **Remaining Parity Backlog** when assigning work, then use the
earlier matching heading for recovered native evidence. Record the specific row
and current source owner in the child task; do not copy the entire paragraph.

The archival document was normalized during the documentation audit: old slash paths now point
to `MapleGame.Runtime/Simulator/...`, while the `HaCreator.MapSimulator` namespace
is preserved where it describes a real compatibility identity. Its status labels
remain historical until the package acceptance criteria and validation levels in
`VALIDATION.md` are met.
