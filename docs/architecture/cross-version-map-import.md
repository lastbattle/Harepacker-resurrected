# Cross-version map import

The Maps sidebar includes an **Import** workflow for bringing selected maps from another extracted IMG data source into the data source currently loaded by HaCreator. The workflow is deliberately split into analysis, review, and execution: selecting a map never writes files.

## Import boundary

`MapImportService` reads the source and destination through `IDataSource`. A plan records every source reference and whether the destination already contains it. The confirmation window presents this plan before calling `Import`.

The source map picker can compare source map IDs with the currently loaded destination and optionally show only maps whose map IMG is not already present. The existing text filter continues to narrow the source list by map ID or label.

The dependency scan covers:

- the selected map image and an `info/link` map target;
- tile sets (`tS`), object sets (`oS`), and background sets (`bS`);
- NPC, mob, and reactor images referenced by map entries, including nested life rows and
  recursive `info/link` template images;
- the selected map entry in `String/Map.img` and referenced entries in `String/Npc.img`, `String/Mob.img`, and `String/ToolTipHelp.img`;
- the exact BGM property named by `info/bgm`.

Standalone asset images are copied as images. When an object, tile, or background IMG already exists in the destination, the referenced `l0/l1/l2`, `u/no`, or background type/number subtree is reviewed and merged if it is missing. Aggregate String and Sound images are likewise not replaced wholesale: only the required property subtree is deep-copied into the destination image. This avoids replacing unrelated data in the currently loaded destination with a higher-version aggregate image.

## Conflict and failure behavior

Existing destination dependencies are preserved. The user can explicitly opt into replacing a selected map IMG when that map ID already exists; this choice is shown as `Replace` in the confirmation plan. A source reference that cannot be resolved is retained in the plan as missing and skipped during execution, so the user can decide whether the remaining issues are acceptable before importing. Import results report individual failures rather than hiding partial completion.

After an import, the Maps list is refreshed. Opening or validating the imported map continues to use the normal `MapLoader` and Issues-tab paths, which remain the final compatibility check for version-specific structures that are not direct asset references.
