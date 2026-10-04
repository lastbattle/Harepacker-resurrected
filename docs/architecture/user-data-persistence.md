# User data persistence

HaCreator and HaRepacker use `HaSharedLibrary.Configuration` as the single owner of
per-user paths and HaRepacker settings serialization. Application code must not
construct `%APPDATA%` or `%LOCALAPPDATA%` paths directly.

## Canonical layout

Roaming data is stored below `%APPDATA%\Harepacker`:

```text
Harepacker/
├── HaCreator/
│   ├── Settings.json
│   ├── config.json
│   ├── Backups/
│   ├── AI/
│   │   └── Settings.json
│   ├── Databases/
│   │   └── map-history.db
│   ├── MapSimulator/
│   │   ├── Characters/
│   │   ├── item-maker-progression.json
│   │   ├── login-character-accounts.json
│   │   ├── map-transfer-destinations.json
│   │   ├── monster-book.json
│   │   ├── packet-owned-funckey-config.json
│   │   ├── quest-alarm.json
│   │   ├── skill-macros.json
│   │   ├── social-rooms.json
│   │   └── storage-accounts.json
│   └── .migrations/
└── HaRepacker/
    ├── Settings.txt
    ├── ApplicationSettings.txt
    ├── CustomKeys.txt
    ├── FHMapper/
    │   └── Settings.ini
    └── .migrations/
```

Large machine-local dependencies are stored below `%LOCALAPPDATA%\Harepacker`.
The ACE-Step runtime currently uses `AudioAI\ACE-Step-1.5` there.

Extracted WZ/IMG content is not application configuration. Existing configured
data roots remain unchanged and are not copied during settings migration.

## Simulator profiles

Simulator persistence is resolved through `ISimulatorProfileStorage` in
`MapleGame.Runtime/Contracts`. Stores accept an optional profile while
retaining their existing explicit file-path constructors for editor callers.
The default editor profile is `SimulatorProfileStorage.CreateHaCreatorPreview()`;
it uses the canonical `HaCreator/MapSimulator` subtree and preserves the existing
copy-based migration from legacy `%APPDATA%\HaCreator\Characters` and
`%APPDATA%\HaCreator\MapSimulator`. A standalone host must pass
`SimulatorProfileStorage.CreateStandaloneClient()` (or its own implementation),
which resolves to the separate `MapleGame.Client/MapSimulator` subtree and never
imports HaCreator data.

The character preset directory and these JSON stores are covered by the profile
contract: item-maker progression, login-character accounts, map-transfer
destinations, monster book, packet-owned function keys, quest alarms, skill
macros, social rooms, and storage accounts. The stores do not call
`UserDataPaths` directly. This keeps file ownership in the host composition root
and lets the standalone client inject an isolated profile without a
replacement global service locator. An explicitly supplied file path still wins
over the profile, preserving existing tests and editor integrations.

## Compatibility migration

`UserDataPaths` performs one-time, copy-based migration. Existing canonical data
always wins, legacy files are retained as recovery copies, and completion markers
prevent a deliberately deleted canonical setting from being restored on every
launch.

Legacy sources include:

- `%APPDATA%\HaCreator`
- `%APPDATA%\HaRepacker`
- `%LOCALAPPDATA%\HaCreator\AudioAI\ACE-Step-1.5`
- `hacreator.db` beside the executable or in the historical working directory

Map history is merged at the record level into the canonical SQLite database so
both historical database locations can contribute entries without duplicates.

## Adding persisted state

Add the path to `UserDataPaths`, under the owning application and feature folder.
Application-managed configuration and presets from `HaCreator/MapSimulator` must
use the `HaCreator/MapSimulator` user-data subtree. User-selected exports, such as
simulator screenshots, continue to use their explicitly selected destination.
When replacing an older path, provide the legacy path to the shared helper and
document the migration here.
