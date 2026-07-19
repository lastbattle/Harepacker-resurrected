# Quest conversation preview

`HaCreator/GUI/Quest/NpcConversationPreview` provides a resizable conversation studio below the `QuestEditor` conversation grids. It is available for start, end, stop-start, and stop-end quest conversations. Its raw markup editor and rendered client preview share the selected `Quest/Say.img` line, so edits in either the grid or studio stay synchronized. Stop conversation groups expose their individual responses as selectable lines for preview and editing.

The studio includes formatting buttons, an insert-token list, context hints after typing `#`, keyboard shortcuts, and line/column/token status. Editing and rendered output share the same client dialogue surface, while the vertical list/studio split remains user-resizable.

Asset-backed tokens open selectors instead of inserting guessed IDs. Items, NPCs, maps, monsters, and skills reuse the existing selector dialogs. The WZ image option opens a category/IMG/property-tree browser that previews Canvas nodes and inserts a complete `#f...#` path.

The preview resolves the speaker from the quest's start/end `npc` check when possible. Users can override the speaker and switch the portrait side without changing quest data. NPC portraits, item icons, skill icons, and `#f...#` canvases are loaded from the active WZ/IMG data source.

Supported client text tokens include:

- colors: `#b`, `#r`, `#d`, `#g`, `#k`
- emphasis: `#e`, `#n`
- selections: `#L...#`, `#l`
- names: `#p...#`, `#m...#`, `#o...#`, `#t...#`, `#z...#`, `#q...#`
- images: `#i...#`, `#v...#`, `#s...#`, `#f...#`
- dynamic placeholders: `#h...#`, `#c...#`
- progress and quest values: `#B...#`, `#R...#`, `#x`
- control escapes: `\r\n`, `\r`, `\n`, `\t`, `\b`

Unknown tokens remain visible in purple italics so unsupported syntax is apparent during editing.
