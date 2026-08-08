# HaCreator Cutscene Workspace

The Cutscene Workspace is HaCreator's client-side editor for MapleStory reserved `Effect.wz/Direction*.img` scenes and their map trigger bindings. It deliberately does not modify server code or the source client files outside HaCreator's normal loaded-WZ save lifecycle.

## Entry points

- **Map > Cutscene Workspace** and the main command bar open the workspace for the selected map.
- **Map Info > Optional properties > Cutscene Workspace** opens the same workspace and keeps `onUserEnter` and `onFirstUserEnter` synchronized with the map-info form.
- The workspace can open without a selected map for Direction scene browsing and editing; map triggers are disabled in that state.

## Layout and interaction

The window follows `ui-design-system.md` and uses the shared Harepacker WPF theme.

- The left workspace card groups **Scene explorer** and **Selected event** into tabs. Scene explorer is shown first; selecting a scene or timeline event switches to Selected event, while the tab header always provides a quick way back to browsing. Scene explorer initially enumerates only `Effect` filenames for images whose names begin with `Direction`. Extracted IMG sources use the filesystem/category index and do not instantiate `WzImage` objects during this step. The workspace parses and discovers scene nodes only after the user selects a Direction image; navigating elsewhere drops clean workspace references, allowing the IMG source's bounded LRU cache to evict them, while clean legacy WZ images parsed by the workspace are unparsed directly. This avoids eagerly retaining every Direction property tree on large modern clients. The **Add scene** action prompts for a unique full scene path (defaulting to the next `SceneN` value under the selected scene's parent), requires the current `Direction*.img/` prefix, creates the scene under the requested existing property container, seeds it with a visual event, and keeps it pending until the workspace is saved. **Delete scene** removes the selected scene from the workspace and defers removal from the WZ image until save.
- The center preview displays the selected client resolution, spatial event markers, movement paths, typed map `directionInfo` markers, and the latest type-3 character appearance at its scene position. When a type-3 command has no `x`/`y`, the preview first uses the lowest-`z` non-origin spatial visual paired with the first character action (for example, `(279, 112)` in `Direction4.img/promotion/Scene20`), then falls back to a non-origin visual created at appearance time; explicit type-3 coordinates take precedence. Dragging the selected marker updates its map coordinates. Active visual events are composited in ascending `z` order and positioned using each animation frame's WZ canvas `origin`, including origins inherited through linked canvases. When multiple visual events share a nonzero `z` layer, the latest event replaces earlier events on that layer for the remainder of the preview; `z = 0` visuals continue to composite together. Zero-duration visual layers persist until the scene's field transition, while moving or explicitly timed visuals end at their declared duration.
- The full-width bottom timeline groups visual, character, sound, transition, and unsupported commands into synchronized lanes. Event blocks show their duration, select the matching detail row when clicked, drag horizontally to retime their start, and share the transport playhead, click-to-scrub behavior, and horizontal scrolling without consuming the map editor's existing right sidebar.
- The Selected event tab edits the supported reserved-scene fields and shows unrecognized fields in an editable name/value list while preserving untouched nested properties. Character-appearance equipment is split from those unknown fields into slot/item rows; each row uses a named slot combobox (for example, `1 — Cap`, `11 — Weapon`, or `21 — Medal`) instead of requiring raw slot numbers, and a slot already used by another row is removed from the available choices. Adding an item opens the same equipment-filtered item selector used by QuestEditor, and the selected appearance also gets a small standing-character preview assembled from Character WZ canvases. The preview consumes the renderer-neutral composition layer shared with MapSimulator, so its anchor and z-order rules stay aligned with the XNA character renderer. Visual fields include a searchable picker populated from paths referenced by the loaded Direction scenes, with thumbnails rendered before selection. The sound picker indexes all images in `Sound` without parsing them up front, then lazily loads the selected image's entries so effects and the complete BGM catalogue remain available without retaining the entire Sound property tree. Character actions use an editable searchable-style catalogue because valid action names are client-dependent.
- The right inspector enables each typed field only for the event types that use it. In particular, the `field`/Map ID editor and shared map selector are available only for type `2` field transitions; the other typed paths follow the same rule. Newer client commands that are not mapped by the editor remain available as raw timeline entries and are preserved when saved.
- The transport supports play, pause, stop, frame step, scrubbing, and looping. Playback advances the event-grid selection as each event start is reached, plays WZ sound events and sounds attached to visual events, and resets the playhead to zero when a non-looping scene finishes. A zero-duration visual remains visible until the scene's field transition or, for nonzero `z` layers, a later visual replaces it; `z = 0` visuals continue to composite together. An explicit positive duration still controls each visual's lifetime. Pausing, stopping, changing scenes, or scrubbing stops currently playing preview sounds.

## Reserved scene model

The editor keeps the WZ event type protocol separate from server direction-packet modes. Supported WZ types are:

| Type | Editor command | Fields |
| --- | --- | --- |
| 0 | Visual / moving visual | `visual`, `sound`, `start`, `x`, `y`, `z`, `x1`, `y1`, `duration` |
| 2 | Field transition | `start`, `field` |
| 3 | Character appearance | equipment slot/value pairs |
| 4 | Character action | `action`, `start` |
| 5 | Sound | `sound`, `start` |
| 6 | Facial expression | expression ID in `x`, `start` |

Unsupported event types remain visible as raw commands. The editor changes only recognized properties; unrecognized nested properties stay attached to their original WZ nodes.

## Map direction triggers

Top-level map `directionInfo` is represented by `MapDirectionInfo` and `MapDirectionEvent` in MapleLib. Each event exposes `x`, `y`, `forcedInput`, and the string entries under `EventQ`. Unknown event, queue, and root properties are deep-cloned and written back so the model is round-trippable across client variants.

`MapLoader.VerifyMapPropsKnown` no longer classifies top-level `directionInfo` as an unsupported copied property. `MapInfo` parses it, and `MapSaver` writes the typed node before preserving other unsupported map properties.

## Persistence

- Saving a reserved scene updates its existing in-memory `WzImage` and calls `Program.MarkImageUpdated("Effect", image)`. The normal HaCreator finalize/repack or IMG-filesystem workflow performs the physical save.
- Saving map trigger changes marks the selected board dirty. The normal map save writes the typed `directionInfo` node and the script bindings.
- Client WZ assets used for discovery and preview are read through the existing data source. The workspace does not patch external server or client directories directly.

## Validation

The workspace exposes separate **Validate scene** and **Validate all** scopes. Results remain visible beneath the timeline and selecting an issue navigates to its scene event or map trigger. Validation reports unsupported WZ types as non-blocking warnings and blocks saving malformed edited data such as missing visual/sound/action/appearance fields, invalid transitions, moving visuals without a positive duration, negative timing, duplicate event or trigger IDs, and spatial triggers outside the selected map's bounds. Unknown properties are preserved rather than treated as destructive validation failures.

## Manual verification

1. Load a compatible data set containing `Effect/Direction*.img`, open a map, and launch **Cutscene Workspace**.
2. Confirm scene filtering, selection, timeline scrubbing, looping, and resolution changes remain responsive.
3. Drag event blocks horizontally, click empty timeline space to scrub, and confirm the detail grid and preview remain synchronized.
4. Edit or retime an event, press `Ctrl+Z` to undo it, then press `Ctrl+Y` to redo it; confirm a complete timeline drag is treated as one history step and that redo is cleared after a new edit.
5. Use the visual and sound Browse buttons, confirm visual thumbnail selection updates the event path, then switch among `Sound` images such as `Bgm00.img` and `Field.img` and filter their complete audio entries.
6. Edit one event of each supported type, save, then reopen it and confirm the values persist.
7. Open a type-3 appearance event, add or remove equipment through the item selector, choose named slots such as Cap, Weapon, and Medal from the slot combobox, confirm the character preview updates, and verify unknown fields remain separate.
8. Confirm the workspace opens on Scene explorer, then selects Selected event after a scene/event is loaded; switch back through the tab header and confirm scene browsing remains available.
9. Add and drag a map trigger, enter multiple `EventQ` scripts, save the map, reopen it, and confirm coordinates, `forcedInput`, queue order, and unknown fields round-trip.
10. Open the workspace from Map Info and confirm both user-enter script fields remain synchronized.
11. Check the window at 100%, 125%, and 150% DPI and at its minimum size.
12. Preview `Direction4.img/promotion/Scene3` and confirm its layered characters and logo remain aligned around the client-screen center as their animation frames advance.
13. Select `Direction4.img/meetWithDragon/Scene0`, add a scene, confirm the dialog defaults to a full path such as `Direction4.img/meetWithDragon/Scene2`, customize the path while retaining the `Direction4.img/` prefix, edit its initial event, save, reopen the image, and confirm the named scene is present; delete a scene and save to confirm it is removed, then close with discard before saving another deletion and confirm it is retained.
