# HaCreator Cutscene Workspace

The Cutscene Workspace is HaCreator's client-side editor for MapleStory reserved `Effect.wz/Direction*.img` scenes and their map trigger bindings. It deliberately does not modify server code or the source client files outside HaCreator's normal loaded-WZ save lifecycle.

## Entry points

- **Map > Cutscene Workspace** and the main command bar open the workspace for the selected map.
- **Map Info > Optional properties > Cutscene Workspace** opens the same workspace and keeps `onUserEnter` and `onFirstUserEnter` synchronized with the map-info form.
- The workspace can open without a selected map for Direction scene browsing and editing; map triggers are disabled in that state.

## Layout and interaction

The window follows `ui-design-system.md` and uses the shared Harepacker WPF theme.

- The left scene explorer initially enumerates only `Effect` filenames for images whose names begin with `Direction`. Extracted IMG sources use the filesystem/category index and do not instantiate `WzImage` objects during this step. The workspace parses and discovers scene nodes only after the user selects a Direction image; navigating elsewhere drops clean workspace references, allowing the IMG source's bounded LRU cache to evict them, while clean legacy WZ images parsed by the workspace are unparsed directly. This avoids eagerly retaining every Direction property tree on large modern clients.
- The center preview displays the selected client resolution, spatial event markers, movement paths, and typed map `directionInfo` markers. Dragging the selected marker updates its map coordinates. All active visual events are composited in ascending `z` order and positioned using each animation frame's WZ canvas `origin`, including origins inherited through linked canvases. Zero-duration visual layers persist until the scene's field transition, while moving or explicitly timed visuals end at their declared duration.
- The full-width bottom timeline groups visual, character, sound, transition, and unsupported commands into synchronized lanes. Event blocks show their duration, select the matching detail row when clicked, drag horizontally to retime their start, and share the transport playhead, click-to-scrub behavior, and horizontal scrolling without consuming the map editor's existing right sidebar.
- The right inspector edits the supported reserved-scene fields and shows unrecognized fields read-only. Visual fields include a searchable picker populated from paths referenced by the loaded Direction scenes, with thumbnails rendered before selection. The sound picker indexes all images in `Sound` without parsing them up front, then lazily loads the selected image's entries so effects and the complete BGM catalogue remain available without retaining the entire Sound property tree. Character actions use an editable searchable-style catalogue because valid action names are client-dependent.
- The right inspector enables each typed field only for the event types that use it. In particular, the `field`/Map ID editor and shared map selector are available only for type `2` field transitions; the other typed paths follow the same rule. Newer client commands that are not mapped by the editor remain available as raw timeline entries and are preserved when saved.
- The transport supports play, pause, stop, frame step, scrubbing, and looping. Playback advances the event-grid selection as each event start is reached, plays WZ sound events and sounds attached to visual events, and resets the playhead to zero when a non-looping scene finishes. A zero-duration visual remains visible until the next event starts; an explicit positive duration still controls its lifetime. Pausing, stopping, changing scenes, or scrubbing stops currently playing preview sounds.

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
4. Use the visual and sound Browse buttons, confirm visual thumbnail selection updates the event path, then switch among `Sound` images such as `Bgm00.img` and `Field.img` and filter their complete audio entries.
5. Edit one event of each supported type, save, then reopen it and confirm the values persist.
6. Add and drag a map trigger, enter multiple `EventQ` scripts, save the map, reopen it, and confirm coordinates, `forcedInput`, queue order, and unknown fields round-trip.
7. Open the workspace from Map Info and confirm both user-enter script fields remain synchronized.
8. Check the window at 100%, 125%, and 150% DPI and at its minimum size.
9. Preview `Direction4.img/promotion/Scene3` and confirm its layered characters and logo remain aligned around the client-screen center as their animation frames advance.
