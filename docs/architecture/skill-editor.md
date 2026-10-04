# Skill Editor architecture

The HaCreator Skill Editor is a native WPF workspace for player-skill books and every other image below `Skill`. Post-Big Bang data is its semantic baseline; V-Update/64-bit data uses the same catalog and lossless raw-property path. The editor changes authored WZ/IMG data and previews presentation; it does not simulate combat damage or server-side skill behavior.

## Data flow

1. `SkillJobCatalog` enumerates image names and subdirectories through `IDataSource.GetImageNamesInDirectory`. It does not parse books during catalog creation.
2. Expanding a book parses only that image and reads its immediate `skill` entries. String names are joined lazily from `String/Skill.img` by exact ID.
3. `SkillDocument` deep-clones the selected Skill and optional String subtrees. Semantic, raw, and visual editors all mutate these detached trees and share one snapshot-based undo history.
4. `SkillAnimationDocumentAdapter` reuses Animation Editor frame/layer models but merges changes back into the detached skill. It never calls `AnimationAssetRepository.Commit`; arbitrary frame keys, sibling order, links, and unknown metadata stay intact.
5. `SkillEditorRepository.Save` replaces only the selected live subtrees and saves their owning images in Skill-then-String order. If a later write fails it restores and re-saves prior snapshots. Compensation failure returns a partial-save result with exact recovery paths.

Create, duplicate, rename/move, and delete use the same document transaction. A move snapshots both source and destination books, and String metadata is created, moved, or deleted only when the user explicitly includes it. Successful transactions invalidate both the old and new skill identities; failed and compensated transactions leave caches unchanged and the detached document dirty.

IMG filesystem sources persist through their immediate image-save path. WZ sources retain the owning archive's existing deferred dirty/repack behavior. The Skill Editor never bypasses `IDataSource`, reconstructs nested save paths from display labels, or claims that a Skill-plus-String update is filesystem-atomic. A successful save validates the detached document, snapshots the live subtrees, replaces only the selected nodes, saves each exact owning path, compensates already-persisted images after a later failure, then accepts the new detached baseline and invalidates only affected caches.

## Compatibility rules

- Property names, order, case, whitespace, WZ types, sparse frame keys, UOLs, `_inlink`, and `_outlink` values are serialized exactly unless the user explicitly changes them.
- Names are schema tokens, not normalization candidates. Historical spellings such as `specialAffcted`, `avaliableInJumpingState`, `peicing`, and `weapon ` with trailing whitespace remain untouched. A familiar field name never justifies coercing its authored WZ type.
- Formula and explicit-level structures remain peer representations. The formula evaluator is preview-only and never rewrites source strings.
- Unknown and modern-only structures remain available in Raw properties. Known panels are an ergonomic projection, not a replacement schema.
- Catalog badges and property-name search inspect only direct authored children. Scalar, canvas, and UOL entries in special books remain selectable with unknown activity metadata; catalog loading never resolves their links merely to infer a badge.
- Raw payload editing includes Canvas PNG import/export and binary, Raw, and video import/export. Raw/video replacement retains the format discriminator and nested metadata, while type changes remain explicit destructive operations.
- Root and staged action values retain their scalar/container shape. Preview candidates remember source path, type, key, and sibling order.
- Character and effect tracks sample one monotonic clock while using independent authored delays. Character composition is renderer-neutral and supports ordinary body/head/face/hair/equipment action layers without a MapSimulator graphics device.
- Global-region relationship shapes are interpreted by container semantics: indexed `skillList`/`cancelableSkillID` values and nested `skill` fields are skill IDs, numeric `req`/`finalAttack`/`psdSkill` keys are skill IDs, and `additional_process` values remain process codes rather than false skill references.

## Audited schema contract

The post-Big Bang baseline audit traversed more than 150,000 property nodes across the Skill category. It found `Int`, `Vector`, `Canvas`, `SubProperty`, `String`, `UOL`, and image boundaries. V-Update/64-bit data additionally requires `Short`, `Long`, `Float`, `Double`, `Null`, Raw/video, binary/sound, and link-bearing canvas handling. The raw editor is the compatibility contract for every MapleLib property class; semantic panels never form a serialization allowlist.

Most player books follow this shape:

```text
Skill/<book>.img
├─ info/icon
└─ skill/<skill-id>
   ├─ icon, iconMouseOver, iconDisabled
   ├─ info and flags
   ├─ common or level
   ├─ PVPcommon (optional)
   ├─ req/finalAttack/psdSkill/skillList/changeSkill (optional)
   └─ action and effect/hit/affected/ball/summon/... visuals
```

The direct property families that drive semantic discovery are:

| Family | Representative authored names |
|---|---|
| Identity | `icon`, `iconMouseOver`, `iconDisabled`, numbered icons |
| Progression | `common`, `PVPcommon`, `level`, `maxLevel`, `masterLevel`, `combatOrders`, `CharLevel` |
| Classification | `info`, `info2`, `skillType`, `type`, `psd`, `psdSkill`, `pvp` |
| Visibility/lifetime | `invisible`, `disable`, `timeLimited`, `notRemoved`, `notExtend`, `notIncBuffDuration`, cooldown-reset/reduction flags |
| Relationships | `req`, `finalAttack`, `changeSkill`, `skillList`, `addAttack`, `cancelableSkillID`, `extraSkillInfo`, `exceedInfo` |
| Restrictions | `weapon`, `weapon `, `weapon2`, `subWeapon`, `eventTamingMob`, `mobCode`, `monkeyAction`, `reduceMoveTime` |
| Primary visuals | `action`, `effect*`, `hit*`, `affected*`, `ball*`, `summon`, `mob*`, `screen`, `tile*` |
| Staged visuals | `prepare*`, `keydown*`, `keydownloop*`, `keydownend*`, `finish*`, `repeat*`, `back*`, `flipBall`, `stopEffect` |
| Special behavior | `special*`, `specialAction*`, `state`, `Frame`, door variants, modern process/sequence/atom/particle structures |

Numbered variants are distinct authored collections, not aliases to rename. Several familiar properties—including `action`, `attackCount`, `damage`, `mobCount`, `mpCon`, `info`, `psd`, and `weapon`—can validly be scalars. Unknown names and unusual known-name/type combinations remain lossless Raw properties.

Formula-based `common`, optional `PVPcommon`, and explicit `level/<n>` tables are peer representations. The editor never migrates between them implicitly. Formula preview accepts integer literals, `x`, parentheses, `+ - * /`, and the client-style upward/downward rounding helpers `u(...)` and `d(...)`; it always saves the original string. Explicit-level grids use the union of authored fields, preserve absent cells as absent properties, retain per-cell WZ types, and make fill/copy an explicit undoable operation.

String metadata joins by the exact skill-ID string, including leading zeros. The observed fields are `name`, `desc`, `h`, `h1`, `h2`, `h3`, `pdesc`, `ph`, and `bookName`, with modern additions such as `h_7` and `hch`. `#property` substitution is preview-only. Enabling text editing is explicit because it adds `String/Skill.img` to the transaction; missing text falls back to bracketed numeric IDs.

Visual containers preserve frame metadata such as `origin`, `z`, `delay`, `a0`, `a1`, `head`, `lt`, `rb`, `z0`, and `z1`, plus nested maps, arbitrary siblings, and links. Delay may be an integer or string. UOLs occur inside ordinary effects, hit/affected/key stages, summon/special branches, tiles, and explicit levels, so preview resolution is cycle-bounded and broken targets remain diagnosable without materialization.

Special images are classified before numeric job-book logic. Attack types, ItemSkill, MobSkill, EliteMobSkill, FamiliarSkill, FieldSkill/HekatonFieldSkill, RidingSkillInfo, battlefield/minigame data, recipes, Dragon assets, and modern process graphs receive a schema-aware semantic projection while the complete ordered property tree remains editable. Relationship containers and batch property-copy operations provide dry-run previews before changing loaded documents.

Job enrichment is version- and region-aware. The numeric IDs remain stable keys, while `VersionInfo.SourceRegion` selects observed aliases such as SEA-region ZEN versus Global-region Jett for `508` and `570`-`572`, and SEA-region Len versus Global-region Ren for `16002`/`161xx`. The V-Update catalog includes class names for suffix-14 books and observed regional/legacy books, but keeps shared roots (`40000...`, `50000...`, and `800000...`) outside the player hierarchy unless explicit version metadata identifies a player class.

The visual workspace preserves sparse frame keys and non-frame siblings. Frame duplicate/delete/reorder operations do not silently rekey; rekeying is a separate previewed command. With the timeline focused, Ctrl+C copies the selected frame as both a lossless editor frame and PNG clipboard data, and Ctrl+V inserts it after the selected frame; image-only clipboard data uses the selected frame as its metadata template. Frame import and export support PNG and lossless WebP through the shared Animation Editor codec; exports default to `<skillId>.<frameId>.png` and switch the extension when WebP is selected. Stage playback honors an explicit `time` first, then hold/release for indefinite stages, and finally authored frame timing. Layer origin, delay, Z, alpha, zoom, fit, panning, onion skin, scrub, and playback speed are preview controls over the detached document and share its undo history.

### Action and timing resolution

Action candidates remain ordered, typed source values carrying their path, raw/sparse key, sibling order, and authored scalar/container shape. Only authored strings or links resolving to string/action containers qualify; integers, formulas, canvases, malformed containers, and broken links are not coerced. Preview resolution uses this order:

1. An explicit action on the active staged container wins.
2. Shared simulator/editor policy may retain a qualifying prepare action during a held stage; inheritance is not assumed for every skill.
3. Otherwise the root `action` scalar or ordered children provide candidates.
4. No declaration requests `stand1` with neutral status. An empty, unknown, or profile-incompatible declaration requests `stand1` with a warning naming the token and source path.
5. If a custom profile lacks `stand1`, the first composable body action is a last-resort preview and the complete fallback chain is reported. If none exists, only character layers are hidden; effect preview continues.

Multiple root candidates are choices, not a guessed serialized sequence. Auto mode selects an explicit stage action and then the first composable ordered candidate. Manual selection changes preview only. The prepare/key-down/release controller is labeled as preview policy and never serializes an inferred execution graph.

One monotonic clock supplies absolute and stage-local time. Effect and character frames independently use their authored timing; neither is stretched to match the other. Stage `time` has precedence, indefinite key-down uses explicit hold/release, finite effect delays/repeat define the remaining extent, and character duration is only a fallback when no effect extent exists. Zero or negative authored delays are preserved and diagnosed while preview uses a bounded display fallback to avoid busy loops.

Coordinate composition retains canvas origin/flip, character map/head/body anchors, Z, alpha, links, and attachment policy. Facing mirrors the character and verified character-attached effects exactly once; world-relative layers remain in world space. Unknown `pos` or anchor policies produce visible alignment diagnostics instead of guessed serialization behavior.

## Validation policy

Current-skill and all-loaded-change validation are separate operations. Issues carry severity, exact WZ path, localized text, and a navigation target.

Save-blocking errors include invalid or duplicate skill identity, a renamed ID that disagrees with its node name, malformed formulas edited through the semantic surface, invalid values for their retained WZ types, newly broken required links, duplicate frame keys introduced by an edit, invalid playback delays, and unresolved owning Skill/String images.

Warnings preserve data and allow save. They include unexpected book placement, missing String/name/icon/max-level/reference metadata, conflicting declared maximum levels, unknown properties or unusual types, preview division by zero, pre-existing broken links, sparse or non-zero frame keys, unsupported modern semantic graphs, placeholder images, unresolved character actions, and unknown preview anchor policies. A warning must never trigger normalization, link flattening, or node deletion.

## Resource ownership

Preview profiles contain body, head, face, hair, equipment, facing, and preset data. Changing a profile never edits the skill. Images parsed by the preview service are tracked through leases; clearing a selection/profile/window releases only service-owned, unchanged, non-shared images and always disposes editor-owned bitmaps.

Decoded character frames use bounded LRU storage. Images loaded through the active data source remain source-owned; the editor does not unparse shared or changed images. Selecting another skill, replacing the preview profile, and closing the workspace clear editor-owned bitmaps and timers.

## UI and localization

`SkillEditor.xaml` merges `HarepackerTheme.xaml`, uses DPI-safe grids and recycling virtualization, and follows the standard browser/workspace/inspector/status anatomy. All user-facing strings live in the neutral, `zh-CHT`, `zh-CHS`, `ko`, and `ja` Skill Editor resources with identical key sets.

The browser groups actual books by family, class, and advancement, retains unmatched/system/special paths, and exposes player/special scope, active/passive, hidden, warning, and opt-in exact-property filters. Book expansion resolves shallow metadata and String names without decoding icons. Placeholder status is reported from non-parsing directory metadata when the data source exposes it. Work is cancellable when the window or selection changes.

Catalog construction follows actual data: enumerate names/subdirectories without parsing, classify special paths before numeric job rules, inspect shallow skill IDs only on expansion, resolve String metadata lazily, then enrich known IDs with versioned class metadata. Missing names fall back to family/advancement plus numeric ID. Unmatched numeric books remain visible and are never guessed into player families. Filtering is immediate and preserves dirty selection; choosing another dirty skill or closing uses Save/Discard/Cancel. Selecting a job defaults to its first skill when available, and opening Visuals defaults to the first discovered effect track.

The Overview surface shows classification, maximum level, common requirement/relationship flags, rendered descriptions, and the complete observed String field set behind the explicit second-image edit toggle. Formula, PVP, and explicit-level modes appear only when their corresponding nodes exist. The Raw inspector presents the ordered hierarchy plus a type-aware value editor, diagnostics, explicit link materialization, and payload import/export. Evan player books expose direct navigation to matching `Skill/Dragon` asset books.

## Verification

`UnitTest_SkillEditor` covers the post-Big Bang expression grammar, data-derived catalog classification, V-Update detection, detached history, cross-image compensation and partial-save reporting, create/move/rename/delete, cache invalidation, schema-token preservation, raw/video payload ownership, staged action resolution, bounded preview timing, special schemas, batch edits, image leases, localization parity, multi-locale/DPI window construction, and an on-disk IMG reopen test. MapleLib has focused Raw/video constructor and replacement tests. Real client data is an opt-in verification input and is never checked into the repository.

Read-only inspection covered representative post-Big Bang and V-Update/64-bit client data. Validation confirmed the baseline special-file/Dragon topology and representative player/V-Update structures, including ordered actions, icon triplets, relationships, common values, effects, and process nodes. Global-region 64-bit data additionally verifies Jett naming, Pathfinder, legacy Explorer pirate books, Phantom/Luminous/Shade beginner books, Evan HEXA mastery, Demon Avenger, Beast Tamer, Erel Light, Sia Astelle, and named special files. `VersionInfo.IsVUpdate` controls V-Update catalog enrichment; process architecture is not used as a client-family proxy.

Run focused verification with:

```powershell
dotnet test UnitTest_SkillEditor/UnitTest_SkillEditor.csproj
dotnet test UnitTest_AnimationEditor/UnitTest_AnimationEditor.csproj
dotnet test MapleLib.Tests/MapleLib.Tests.csproj
```

UI behavior changes require manual checks at 100%, 125%, and 150% DPI: localized command/focus/automation behavior; lazy browsing and filtering; formula, explicit-level, missing-String, linked-animation, and special-book navigation; Save/Discard/Cancel and compensation reporting; frame/link preservation; synchronized action fallback; profile/facing alignment; PNG/WebP and clipboard operations; IMG versus WZ persistence; modern unknown-node preservation; and bounded memory while navigating repeatedly.

## Boundaries

The semantic surfaces do not promise client simulation or complete interpretation of every modern atom, particle, process, sequence, or field-skill graph. Raw preservation remains authoritative for those structures. Full parity for morphs, mounts/taming mobs, afterimages, special weapon ownership, mechanic/vehicle coordinators, and other specialized avatar paths requires dedicated renderer fixtures before enablement.

The editor never translates client-authored schema tokens or text, silently migrates formulas into level tables, renames historical misspellings, flattens links, infers destructive String deletion, embeds client assets in tests, or records personal export paths in documentation.
