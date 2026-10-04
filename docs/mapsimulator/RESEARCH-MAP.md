# Research crosswalk

The backlog files are valuable evidence stores, but they are too large and
overlapping to serve as an execution queue. Use this map to locate research for a
package, then record the specific section/function/data path used in the child task.
The package status lives in [EXECUTION-PLAN.md](EXECUTION-PLAN.md).

| Package | Primary research | Supporting research | Start with |
|---|---|---|---|
| P00 oracle | [NATIVE-EVIDENCE.md](NATIVE-EVIDENCE.md) | Network plan's IDA/status tail | Pin executable/data and split static evidence from live scenarios |
| P01 ownership/cleanup | [PORT-ARCHITECTURE.md](PORT-ARCHITECTURE.md), [CURRENT-STATE.md](CURRENT-STATE.md) | Extraction architecture | Contracts, offline defaults, caller inventory |
| P02 direct transport | Network Unification Plan | Backlog 11 login/entry; backlog 8 service-stage sections | Replace relay-as-authority; use N04 socket evidence |
| P03 routing | Network Unification Plan | Backlogs 5-11 packet-owned sections | Build stage/opcode/owner route table |
| P04 login/field entry | Backlog 11 Login Character Entry Flows | Network plan; backlog 8 stage transition/cash sections | Login codecs, roster, handoff, server field state |
| P05 movement | Backlog 3 Movement Physics | Backlog 1 Avatar Movement; N01-N03 | CVecCtrl owner and CMovePath callers |
| P06 avatar | Backlog 1 Avatar Movement | Backlog 10 companion/action-loader sections; backlog 6 mob/NPC asset sections | CharacterLoader/Assembler and action layers |
| P07 skills/combat | Backlog 2 Player Skills; Backlog 6 Combat Effects Mobs | Backlog 1 action; damage-number analysis | Native eligibility, packet, random and authority boundaries |
| P08 interaction/progression | Backlog 5 Interaction Environment; Backlog 8 Progression Utility Windows | Backlog 7 Misc Edge | Drops, inventory, quests, NPC/script/server ownership |
| P09 UI/input | Backlog 4 UI HUD Status | Backlog 8 utility windows; Backlog 7 field messaging | Window/input owner and v95 asset family |
| P10 social/companions | Backlog 10 Companions Social Systems | Backlog 7 Misc Edge; packet sections in 5/8 | Actor and transaction lifecycle |
| P11 services | Backlog 8 Progression Utility Windows | Network plan; Backlog 11 entry; service sections in 7/9 | Shop/ITC migration and return |
| P12 fields/minigames | Backlog 9 Special Fields Minigames | [Backlog 7 navigation](MapSimulator-Client-Parity-Backlog-7-Misc-Edge-INDEX.md); Backlog 5 field interactions | Field factory, score/timer, cleanup |
| P13 effects/audio | damage_number_analysis.md | Backlog 1, 2, 6, 7, 8 animation/audio sections | Timed render/audio ownership |
| P14 closure | [VALIDATION.md](VALIDATION.md), extraction architecture | All package records | Evidence audit, clean publish and supported scope |

## Legacy document handling

- `MapSimulator-Client-Parity-Backlog-1-Avatar-Movement.md` through `-11-*` are
  domain research. Their repeated `Current State`, `Recent Progress`, and
  `Remaining` sections can disagree with current source. Cite a narrow row or
  function; do not copy the whole status summary into a new task.
- `MapSimulator-Client-Parity-Backlog-7-Misc-Edge.md` is exceptionally large.
  Use its [navigation page](MapSimulator-Client-Parity-Backlog-7-Misc-Edge-INDEX.md),
  search exact headings/symbols, and split every child task into one owner and one
  observable contract. Keep the archival source as the sole copy of its evidence.
- `MapSimulator-Network-Unification-Plan.md` contains useful migration history and
  packet/IDA discoveries. Its relay migration status is not proof of a direct
  standalone socket; P02 owns that distinction.
- `damage_number_analysis.md` contains a narrow effects study. It does not certify
  all animations, combat calculations, or rendering.
- Historical references to `HaCreator/MapSimulator/...` should be resolved to
  `MapleGame.Runtime/Simulator/...` when touching a current owner. Preserve the
  `HaCreator.MapSimulator` namespace where it is a deliberate compatibility name.

## Child-task naming

Use `Pxx-topic[-slice]`, for example `P05-float-crossing`, `P07-melee-v95`, or
`P11-shop-return`. A child record must link its source symbols, one or more
research sections, its prerequisites and its validation evidence. When a source
claim is disproved, correct the narrow row and link this crosswalk; do not erase
the historical observation that explains why it was believed.
