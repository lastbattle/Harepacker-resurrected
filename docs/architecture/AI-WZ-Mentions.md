# AI chat WZ references

The chat input includes a single-line footer showing the selected AI model, a
persistent `@` reference hint, and progress. It truncates with an ellipsis when
space is limited; hover to read the full line. The model refreshes after changing
AI settings and whenever the window is activated.
Below the footer, a shared apply-controls panel groups “Apply as it works” with
“Apply selected changes.” The mode checkbox stays available when there are no
pending changes; the apply button appears when changes are ready for review.
Type `@` in the AI Map Editor message box to browse loaded categories. Search by
logical path, ID, or a cached map, NPC, monster, or equipment name. Category aliases
include `maps`, `objects`, `backgrounds`, `tiles`, `npcs`, `monsters`, `reactors`,
`strings`, `items`, and `equipment`. For example, `@objects house` filters object images.
Other loaded categories, including sound, skills, and quests, are available by path.

Use Up/Down to select, Enter/Tab or a mouse click to insert, Right Arrow to browse
an image or property, and Escape to dismiss. Selecting a directory browses it.
Shift+Enter still inserts a newline. Narrow the search when the first 100 results
do not include the desired entry. An empty search lists category roots.

References are editable plain text, such as `@{Map/Obj/house.img/snow/house/0}`.
The exact logical path is retained in chat history and sent to the agent with
syntax guidance. These are data references, not executable links or edit commands.
Images and arbitrary nested WZ properties can be referenced; String property
values can be searched within their parent path. Name searches depend on the
editor's existing name caches; uncached assets remain accessible by path or ID.

The catalog uses metadata APIs for IMG projects and traverses all split WZ roots
for WZ projects. It never bulk-parses images: only an image explicitly browsed with
`.img/` is loaded. Searches are debounced on the UI dispatcher to respect WZ object
ownership. Catalog metadata is refreshed when map context is refreshed. The first
search can take longer on large projects while metadata is collected.

## Agent resolution

The shared agent system prompt and MCP initialization instructions direct agents to
call `resolve_wz_reference` before name discovery. The same read-only tool is exposed
in Chat Completions, Responses and compact MCP tool lists. It accepts either the
literal `@{...}` mention or its inner path. WZ lookup traverses split category roots;
IMG lookup uses the data source's logical image path API. Nested properties are
resolved exactly without following links or substituting similar names.

The response includes `imagePath`, property type and bounded scalar value, plus
paginated child paths (`offset`, `nextOffset`, `hasMore`; default 50, maximum 100).
Strings are limited to 1,024 characters and explicitly flagged when truncated.
No binary payloads are serialized. Recognized map/asset paths return `nextQuery`
with exact arguments; complete artwork references also return `previewArguments`
for `get_asset_preview`. Mob/NPC IDs preserve leading zeroes and yield ID-filtered
prerequisite queries. Reading a reference does not bypass existing edit prerequisites.
Unsupported editing categories remain readable source data. Missing references
produce explicit errors, not name-search fallbacks.

## Manual verification

1. Load a WZ or IMG project, open a map and AI Map Editor, and type `@`.
2. Browse Map, search an NPC/monster name or ID, and filter `@objects`.
3. Use Right Arrow on an image, descend to a property, then insert with Enter.
4. Add a second reference in the middle of a sentence. Verify surrounding text
   remains intact, arrows select suggestions, Escape dismisses, and Shift+Enter
   inserts a newline. Click a result and verify insertion at the original caret.
5. Send a message and check that exact `@{...}` paths remain in the user bubble.
6. Try an unknown path and refresh map context after loading additional data.
7. Ask the agent to inspect a mentioned object: verify it calls
   `resolve_wz_reference`, then the returned detail/preview query without a set-name
   search. Repeat with a String property and a missing path; it should report the
   value or missing reference accurately without claiming a map edit.
