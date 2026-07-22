# Harepacker UI design system

This document defines the shared desktop UI language for HaCreator, HaRepacker, and HaSharedLibrary. New UI must use it, and existing UI should be migrated to it when touched.

It applies to editor chrome, windows, dialogs, panels, inspectors, selectors, and
notifications. It does not restyle `MapSimulator`'s in-world/client-emulation UI;
those surfaces intentionally follow the MapleStory client assets and rendering
rules instead of the desktop editor theme.

The canonical WPF resource dictionary is:

`HaSharedLibrary/GUI/Themes/HarepackerTheme.xaml`

## Design goals

- Keep dense editor workflows readable without looking like a legacy property sheet.
- Make the active map, selection, layer, platform, and validation scope obvious.
- Keep destructive, primary, and secondary actions visually distinct.
- Preserve keyboard access, localization, DPI scaling, and event-driven behavior.
- Prefer native WPF/XAML for new surfaces. Use `WindowsFormsHost` only for controls whose rendering or lifecycle still requires WinForms.

## Visual language

- Canvas: cool light gray (`HareCanvasBrush`).
- Surfaces and cards: white with a one-pixel cool-gray border.
- Primary accent: blue (`HareAccentBrush`); reserve it for selection and the main action.
- Success, warning, and danger colors communicate status, never decoration.
- Text: near-black blue-gray; secondary text uses `HareMutedTextBrush`.
- Corners: 4 px for controls, 7 px for cards, 10 px for large containers.
- Spacing: 4 px micro, 8 px control, 12 px related groups, 16 px cards, 20 px page edges.

## Page anatomy

Use this order where applicable:

1. Page title and one-line purpose for embedded views; one-line purpose only for windows.
2. Compact command bar containing the most common actions.
3. Main content, usually a two- or three-column `Grid`.
4. Contextual inspector/details pane.
5. Bottom status or validation area only when state needs persistent visibility.

Use `Grid` and `DockPanel` for primary composition. Avoid fixed pixel positioning except for icons and deliberate compact controls. Long forms should use a scroll viewer and aligned label/value columns.

## Shared resources

Merge the dictionary into a WPF window or user control:

```xaml
<ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="/HaSharedLibrary;component/GUI/Themes/HarepackerTheme.xaml"/>
    </ResourceDictionary.MergedDictionaries>
</ResourceDictionary>
```

Important named styles:

- `HarePageTitleStyle`
- `HareSectionTitleStyle`
- `HareMutedTextStyle`
- `HareCardStyle`
- `HareToolbarStyle`
- `HareButtonStyle`
- `HarePrimaryButtonStyle`
- `HareDangerButtonStyle`
- `HareIconButtonStyle`

Use the native `Window.Title` or `ThemedDialogWindow.Title` as the primary title
for a window. Do not repeat the same operation name in the window body with
`HarePageTitleStyle`, a large bold `TextBlock`, or another heading style. Keep
`HarePageTitleStyle` for embedded pages and intentional in-content navigation,
such as wizard step names. Product branding in an About window and status text
that is not the window caption are also valid exceptions.

The dictionary also supplies restrained implicit styles for standard buttons, text inputs, selectors, lists, trees, grids, tabs, group boxes, separators, and tooltips.

## Interaction rules

- The default action uses `HarePrimaryButtonStyle`; use only one primary action per local context.
- Destructive actions use `HareDangerButtonStyle` and must have explicit text or a tooltip.
- Icon-only buttons require a tooltip and an automation/accessibility name.
- Enter accepts and Escape cancels in modal dialogs unless a multiline editor owns the key.
- Filtering should update results immediately and must not destroy selection silently.
- Validation labels belong next to the affected input; full-operation errors belong in the status/error surface.
- Disabled controls remain visible when they teach the user what becomes available; hide controls only when they are irrelevant.
- Preserve numeric-input behavior when migrating WinForms controls. Mark integer `TextBox` controls with `InputScope="Number"`; the shared input registration enforces typing and paste validation. Use `HaSharedLibrary.GUI.NumericTextBox` with `AllowDecimal="True"` for floating-point or decimal values, and set `UseInvariantCulture="True"` when the backing parser uses invariant formatting.

## Localization

Desktop UI must ship in English, Chinese (Traditional), Chinese (Simplified),
Korean, and Japanese. English is the neutral resource. This repository's
existing satellite-resource convention uses `zh-CHT`, `zh-CHS`, `ko`, and `ja`.
Shared libraries may use the modern parent cultures `zh-Hant` and `zh-Hans`;
.NET resolves `zh-CHT` and `zh-CHS` through those parent satellites.

- Put user-visible titles, labels, buttons, menu text, tooltips, validation text,
  confirmation prompts, and status messages in `.resx` resources.
- Bind XAML to resource values; do not duplicate translated strings in XAML.
- Keep programmatic identifiers, WZ property names, file-format tokens, and
  user-authored content untranslated.
- Format variable messages from localized templates rather than concatenating
  translated fragments.
- When removing or renaming a control, remove its resource key from every
  culture only after a repository search confirms that no code or XAML consumes
  it.
- A localization change is complete only when the same key set exists in the
  neutral, Traditional Chinese (`zh-CHT` or `zh-Hant`), Simplified Chinese
  (`zh-CHS` or `zh-Hans`), `ko`, and `ja` resources.

## Editor-specific rules

- Asset browsers use search, category/tree navigation, thumbnails, and a selection summary.
- Map visibility filters remain available without opening a dialog.
- “Validate current map” and “Validate all maps” are separate actions with explicit scope.
- Layer and platform controls stay visible while editing.
- Instance editors expose serializable item properties unless a property is derived or unsafe to edit directly.

## WinForms migration

When replacing a WinForms surface:

1. Preserve the existing class-facing behavior or update every caller in the same change.
2. Preserve control names needed by code-behind until behavior has been moved safely.
3. Convert docking/anchoring to `Grid`, `DockPanel`, and content sizing.
4. Keep localized text in resources; do not discard translated `.resx` data during migration.
5. Retain `WindowsFormsHost` only for specialized WinForms controls, DirectX surfaces, or an intentionally staged migration.
6. Verify opening, DPI scaling, keyboard actions, focus order, selection, and close/cancel behavior.

### Intentional native-host exception

`HaCreator/MapEditor/MultiBoard.xaml` may keep its `WindowsFormsHost` for
`DirectXHolder`. The renderer owns an HWND, DirectX device lifecycle, and legacy
input bridge; replacing that host is a renderer project, not a visual restyle.
Keep commands, status, filters, inspectors, and every non-renderer surface in
native WPF. Any new host exception must be documented here with the concrete
lifecycle constraint that requires it.

The hosted map editor renderer is demand-driven: static maps sleep until an edit
or viewport change invalidates them. Moving backgrounds and Spine previews are
capped at 30 FPS, while ordinary WZ animations wake at their current frame's
declared delay. It presents with vertical synchronization and runs below the WPF
UI thread's priority. Preserve this scheduling unless profiling shows a better
policy; continuous full-map redraws contend with WPF for the GPU and board locks.

## Review checklist

- Uses the canonical palette and shared styles.
- No duplicated page-local copy of the global button/input styles.
- Main action and validation scope are unambiguous.
- Layout works at 100%, 125%, and 150% scale and at the minimum window size.
- Tooltips and accessible names exist for icon-only controls.
- Existing commands, shortcuts, localization, and hosted-control lifecycles still work.
