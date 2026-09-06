# Quest editor and conversation preview

The quest editor accepts both quest storage layouts used by MapleStory clients. Legacy data is read from and written to the aggregate `Quest/QuestInfo.img`, `Act.img`, `Check.img`, and `Say.img` images. Newer per-quest images under `Quest/QuestData/<quest-id>.img` are detected automatically; their `QuestInfo`, `Act`, `Check`, and `Say` roots are projected into the same editor caches and written back to the same standalone image. Create, import, update, relationship editing, and delete operations preserve the detected layout without relying on a client-version boundary.

## Extended quest properties

The purpose-built controls cover the common post-Big Bang quest fields. V-Update and 64-bit data adds fields and nested structures that do not always have a dedicated control, including extended conversation nodes, account and world-state checks, navigation metadata, regional job lists, transfer actions, progress messages, and expanded item or skill metadata.

These values appear in the **Extended Properties** panel for their owning section. `QuestInfo` fields are shown in Quest Info, conversation fields in Say Info, conditions in Check, and actions in Act. The panel uses checkboxes for Boolean values and validated text input for numeric, string, and UOL values. The property name, WZ type, and nested path remain visible so similarly named fields can be distinguished.

Known and future fields use the same editor. When a property is not recognized in the known post-Big Bang or V/64-bit structures, selecting it displays a non-blocking compatibility notice. The value remains editable because a client may still consume region-specific or newer data.

Loading is lazy per selected quest. Saving first serializes the purpose-built model and then merges preserved extended subtrees back into their original section and parent path. Original scalar types, rich conversation shapes, stable collection identities, and unsupported containers are retained. Fields that cannot be displayed as editable scalars are still round-tripped without loss.

When a rich conversation subtree occupies the same path as a simple conversation string, edits made through the standard conversation control are copied into the rich message leaf unless that leaf was edited directly in Say Info's Extended Properties.

## Conversation preview

`HaCreator/GUI/Quest/NpcConversationPreview` provides a resizable conversation studio below the `QuestEditor` conversation grids. It is available for start, end, stop-start, and stop-end quest conversations. Its raw markup editor and rendered client preview share the selected `Quest/Say.img` line, so edits in either the grid or studio stay synchronized. For Yes/No conversations, the client-style Yes and No buttons open the corresponding response sequence and the OK button advances through its lines before returning to the main prompt. Stop conversation groups expose their individual responses as selectable lines for preview and editing.

The studio includes formatting buttons, an insert-token list, context hints after typing `#`, keyboard shortcuts, and line/column/token status. Both markup and rendered modes are editable and synchronize with the selected conversation. Rendered mode serializes visible text styling back into MapleStory markup while preserving token-backed names, images, progress bars, and menu markers as embedded elements. Editing and rendered output share the same client dialogue surface, while the vertical list/studio split remains user-resizable.

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
