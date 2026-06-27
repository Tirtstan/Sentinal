# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0] - 2026-06-27

### 🚀 Highlights

- **Decoupled Address-Based View Routing (`ViewAddress` & `ViewLink`)**:
    - **Never drag scene references between scripts again!** Define lightweight `ViewAddress` ScriptableObject keys to represent your UI screens (e.g. `SettingsAddress`, `InventoryAddress`, `PauseMenuAddress`).
    - Trigger `SentinalViewRouter.OpenView(address)` from anywhere in your codebase — if the panel is already in the scene, Sentinal brings it to focus; if it's missing, Sentinal dynamically instantiates its fallback prefab on the fly!
    - Attach a **`ViewLink`** component to any standard UGUI Button to make it open an address on click automatically with zero code.
- **Dynamic Multiplayer Role Mapping (`SentinalPlayer`)**:
    - Built for local multiplayer, split-screen, and couch co-op! Centralize controller assignments via `SentinalPlayer.SetPrimaryPlayer()` or assign specific role keys (`Player 1`, `Player 2`, etc.).
    - Action map gates, tab controls, and back-button cancel handlers automatically adapt live when players join, leave, or rebind gamepads.
- **Managerless Stack Architecture & Fast Play Mode**:
    - Replaced the scene-bound `SentinalManager` singleton with static `SentinalViewRouter`.
    - Views register dynamically on enable/disable, **removing the mandatory manager GameObject from your scenes.**
    - Fully supports Unity's **Fast Play Mode** (Domain Reload Disabled). Unity 2021.3 LTS through Unity 6.5 ready.
- **Strongly Typed View Grouping (`ViewGroupMask`)**:
    - Multi-layered UI control! Isolate gameplay HUD overlays, party feeds, and full-screen menus into distinct group channels using bitmasks (`ViewGroupMask`).
    - Integrated `ViewGroupMask` into `ViewDismissalInputHandler` so cancel/back actions can be filtered to only dismiss matching UI groups.
    - Features a one-click Editor generator for `SentinalViewGroups.asset` in `Assets/Resources`.
- **Modular Asynchronous Text Input Gateway**:
    - Prompt players for text (character naming, room codes, bug reports) using `ITextInputGateway` and `TextInputPrompt`.
    - Includes `PromptedTextField` component for seamless TextMeshPro integration.

### Changed

- **Core architecture**:
    - Replaced scene-bound `SentinalManager` singleton with static global `SentinalViewRouter`.
    - Routing lifecycle now relies directly on `ViewSelector` enable/disable registration flow, removing mandatory manager scene objects.
    - Full support for Unity Fast Play Mode (Domain Reload disabled).
- **View Grouping & Editor Tools**:
    - Replaced raw integer/attribute mask fields with strongly typed `ViewGroupMask` struct supporting bitwise operations (`&`, `|`, `^`, `~`) and `ViewGroupMask.Everything` / `ViewGroupMask.Nothing` presets.
    - Enhanced `ViewGroupMaskDrawer` and `ViewGroupConfig` for full cross-platform compatibility and zero console warnings across Unity 2021.3 LTS through Unity 6.5.
    - Improved missing view groups configuration UI in the Inspector with standard `EditorGUI.PrefixLabel` alignment and one-click asset creation.
- **Documentation**:
    - Refreshed `README.md` for 4.0.0.

### Added

- **Address-based view routing**:
    - Added `ViewAddress` ScriptableObject asset for decoupled view targeting and fallback prefab instantiation.
    - Added `ViewAddressRegistry` for runtime registration and resolution.
    - Added `ViewLink` component for zero-code UGUI button address binding.
    - Added `SentinalViewRouter.OpenView(ViewAddress)` API.
- **Player role mapping utility (`SentinalPlayer`)**:
    - Added `SentinalPlayer` static registry API (`SetPlayer`, `GetPlayer`, `TryGetPlayer`, `SetPrimaryPlayer`).
    - Added event-driven `OnPlayerChanged` callbacks to `ViewInputSystemHandler`, `ActionMapGate`, and `ViewDismissalInputHandler` for real-time input re-binding.
    - Added fallback resolution to primary player (`key 0`) or `PlayerInput.all[0]`.
- **View Dismissal Group Filtering**:
    - Added `ViewGroupMask` property to `ViewDismissalInputHandler` for group-filtered cancel/back action handling.
- **Text Input Gateway System**:
    - Added `ITextInputGateway` interface and `TextInputGateway` implementation for modal text prompts.
    - Added `TextInputPrompt` request struct (title, placeholder, initial text, submission callback).
    - Added `PromptedTextField` component and `PromptedTextFieldEditor` for TextMeshPro input field integration.

### Breaking

- `SentinalManager` public API and event subscriptions must be migrated to `SentinalViewRouter`.
- Existing project setup that depended on a manager GameObject should be updated to the router-based workflow.

### Migration Notes

If upgrading from 3.x:

- Replace `SentinalManager.Instance` usages with `SentinalViewRouter`.
- Remove scene dependencies that only existed to host `SentinalManager`.
- Update custom scripts/events to subscribe to `SentinalViewRouter` static events.
- If needed, adopt `ViewAddress` for decoupled open calls.
- Replace old action map manager/overlay setup with per-view `ActionMapGate`.
- Use `SentinalViewRouter.Refresh()` to refresh all views to reapply action maps and input handlers (e.g. `PlayerInput` created during runtime).

## [3.2.4] - 2026-02-27

### Fixed

- **ActionMapManager**:
    - Fixed default action maps overriding the current view's handler when returning to a root view.
    - `OnViewSwitch` no longer calls `CheckAndApplyDefaults()` when a handler was just applied — the view manages its own action maps.
    - `CheckAndApplyDefaults()` now skips applying defaults when the current view has an active handler with a snapshot, even if the view is a root view.
    - Stopped aggressively removing temporarily hidden (inactive) views from `handlerCache` during iteration in `AnyViewsWithInputHandlersOpen()` and `AnyNonRootViewsWithInputHandlersOpen()`. Cache cleanup now only happens in `OnViewRemoved()`.
    - `OnViewSwitch` now always restores the previous view's handler, even if the new view has no handler.
    - Added `ReapplyHandler()` to re-apply action maps for views becoming current again without overwriting their original snapshot.
- **SentinalManager**:
    - Replaced flat `hiddenViews` list with a `Stack<(ViewSelector, List<ViewSelector>)>` to correctly support nested `hideOtherViews` calls.
    - `RestoreHiddenViews(ViewSelector owner)` now pops only the entry matching the owner, preventing nested hides from corrupting each other.

### Changed

- **ActionMapManager**:
    - `ApplyDefaultActionMaps()` no longer clears `handlerSnapshots` — those belong to individual view handlers and must be preserved.

## [3.2.3] - 2026-02-20

### Fixed

- **ActionMapManager**:
    - Fixed multi-view action map switching bug where disabled (inactive) views were still cached as valid, preventing default action maps from applying.
    - Now checks `gameObject.activeSelf` when evaluating active views and automatically cleans up inactive entries from the handler cache.
    - Removed redundant re-application of handlers in `OnViewRemoved()` that was corrupting snapshot chains with multiple views.

### Changed

- **ActionMapManager**:
    - Converted `ActionMapSnapshot` from class to struct for improved performance.
    - Updated struct properties to PascalCase (`PlayerInput`, `State`) and added explicit constructor.

## [3.2.2] - 2026-02-19

### Fixed

- **ActionMapManager**:
    - Now bootstraps handler cache at `Start()` with any views already open in the scene using `GetViewHistory()`, preventing premature default action map application.
    - Deferred `CheckAndApplyDefaults()` by one frame in `OnViewRemoved()` to allow `OnViewAdded()` to complete the add-remove-add sequence atomically before evaluating defaults.

### Added

- **ActionMapManager**:
    - Added `defaultsRequireAllViewsGone` toggle to control default action map behavior:
        - When false (default): Defaults apply only when non-root views with handlers are gone (root views don't block defaults).
        - When true: Defaults apply only when ALL views with input handlers are gone (including root views).

## [3.2.1] - 2026-02-14

## Changed

- **ActionMapManager**:
    - Removed all action map history tracking. _Was just causing so many conflicting issues, rather use_ `ViewSelector`_'s_ `onEnabledActionMaps` _and_ `onDisabledActionMaps` _to configure action maps._
- **ViewInputSystemHandler**:
    - When one action map is counted, it is treated as exclusive and toggles the affected `PlayerInput`s' action map(s). e.g If action map "UI" is the only enabled action map, every other action map will be disabled on `PlayerInput`(s).

## [3.2.0] - 2026-02-13

### Added

- **Grouping!** - Added view grouping system to filter exclusive/hide behaviors.
    - **SentinalViewGroups (ViewGroupConfig)**: New ScriptableObject asset (auto-created at `Assets/Resources/SentinalViewGroups.asset`) for managing view groups. Also creatable via `Assets > Create > Sentinal > View Groups`.
    - **ViewSelector Grouping**: Added "Grouping" header section to `ViewSelector` with:
        - `groupMask` field: Bitmask selection (similar to Unity's LayerMask) for choosing which groups this view belongs to.
        - `exclusiveView` and `hideOtherViews` toggles moved under "Grouping" header.
    - **Grouping inspector UX**:
        - Group mask dropdown pulls names from the shared `SentinalViewGroups` asset.
        - If the asset is missing, it is auto-created; if still unavailable, the group mask field is hidden.
        - The mask can be set to "Nothing" to opt out of grouping for that view (does not participate in group-based hide/close).
    - **Group-filtered behavior**:
        - `exclusiveView` now only closes views within the same group(s) when a non-negative group mask is provided; a mask of `0` (Nothing) will not close any other groups.
        - `hideOtherViews` now only hides views within the same group(s) when grouping is configured; a mask of `0` (Nothing) will not hide any other groups.

### Changed

- **ViewSelector**:
    - `exclusiveView` and `hideOtherViews` toggles moved to the new "Grouping" header section.
    - Uses the shared `SentinalViewGroups` asset for group names.
- **SentinalManager**:
    - `CloseAllViews()` now has an overload `CloseAllViews(int groupMask, bool excludeRootViews = false)` for group filtering.
    - `HideAllViews(ViewSelector excludeView)` now filters by groups based on the view's `GroupMask`.
- **IViewSelector**: Added `GroupMask` property to interface.

## [3.1.4] - 2026-02-12

### Fixed

- **SentinalManager**: Fixed an issue with the hidden view causing a collection modification exception. Now creates a new instance of hidden views when restoring.

### Added

- **SentinalManager**: Will now `TrySelectCurrentView()` as soon as an `EventSystem` is available. This fixes no UI elements being selected if no already existing player(s) are available (joined in later).

## [3.1.3] - 2026-02-12

### Fixed

- **ViewDismissalInputHandler**: Closing the current view is now delayed to the next frame. This avoids `IndexOutOfRangeException` errors in `InputSystemUIInputModule` when `UI/Cancel` is bound to `Esc` and closing a view changes action maps.
- **ActionMapManager**:
    - After any view switch, default action maps are checked and applied again. This makes sure `defaultActionMaps` are used when the last non-root view in a menu chain is closed.
    - `CheckAndApplyDefaults()` now uses `SentinalManager.AnyNonRootViewsOpen` to decide when defaults should be active. Defaults are applied whenever there are no non-root views open.

## [3.1.2] - 2026-02-06

### Fixed

- **ActionMapManager**: `PlayerInput` action maps will no longer be modified when switching to a view that does not have a `ViewInputSystemHandler`. _This was causing an issue where_ `ViewSelector`_s without a_ `ViewInputSystemHandler` _were disabling all action maps on affected_ `PlayerInput`_s._

### Changed

- **SentinalManager**: Updated `ToString()` to show the priority of listed views as well.

## [3.1.1] - 2026-02-05

### Fixed

- **SentinalManager**: `OnDestroy` no longer nulls static events (`OnAdd`, `OnRemove`, `OnSwitch`). Only `Instance` is cleared. This fixes view events not firing for persistent subscribers (e.g. `ActionMapManager` on a DontDestroyOnLoad object) after loading a new scene, where the previous scene’s `SentinalManager` was destroyed and had cleared the event delegates.
- **ActionMapManager**: Re-subscribes to `SentinalManager` view events in `OnSceneLoaded`, so when used with DontDestroyOnLoad it continues to receive `OnSwitch` / `OnAdd` / `OnRemove` from the new scene’s `SentinalManager` after a scene load.

### Changed

- **ActionMapManager**:
    - History is now **per view selector (source)** instead of a fixed-size list: same selector overwrites its previous entry. No maximum entry count.
    - Extracted `SubscribeToViewEvents` / `UnsubscribeFromViewEvents` and call `ResubscribeToViewEvents()` after `ClearStateForNewScene()` on scene load.
- **ActionMapManagerEditor**:
    - History foldout shows “Action maps per view (N)”, one row per source, sorted by source name; elapsed-time display removed.

## [3.1.0] - 2026-02-03

### Changed

- **Terminology**: Renamed PreventDismissal back to **RootView** (`rootView` / `RootView`).
    - `IViewSelector.PreventDismissal` → `IViewSelector.RootView`.
    - `ViewSelector` inspector and serialized field now use "Root View"; behaviour unchanged (view that does not get auto-closed, has special permissions around being closed, can still be hidden).
    - `CloseAllViews(bool excludePreventDismissalViews)` → `CloseAllViews(bool excludeRootViews)`.
- **ActionMapManager**:
    - `ActionMapHistoryEntry.action` changed from `string` to `ActionMapAction` enum for type safety.
    - History display now uses enum comparison instead of string comparison.
- **ViewInputSystemHandler**:
    - Replaced `enableActionMaps` / `disableActionMaps` (string arrays) with `onEnabledActionMaps` / `onDisabledActionMaps` (`ActionMapConfig[]` arrays).
    - `onEnabledActionMaps` configures action maps when the handler is **enabled** (view is active).
    - `onDisabledActionMaps` configures action maps when the handler is **disabled** (view is inactive).
    - Each `ActionMapConfig` specifies both the action map name and whether it should be enabled or disabled.
    - Methods renamed: `GetEnableActionMaps()` → `GetOnEnabledActionMaps()`, `GetDisableActionMaps()` → `GetOnDisabledActionMaps()`.

### Added

- **SentinalManager**: Added `AnyNonRootViewsOpen` – returns whether any open views are not root views (useful e.g. to decide when to enable gameplay input when only root views like main menu are open).
- **ActionMapManager**:
    - Added `ActionMapAction` enum (`Enable`, `Disable`, `Restore`) for type-safe action map history entries.
    - Added `useDefaultActionMaps` toggle to control whether default action maps are applied.
    - Added `defaultActionMaps` field (`ActionMapConfig[]`) to configure action maps when no non-root views are open.
    - Automatically applies/restores default action maps based on `SentinalManager.AnyNonRootViewsOpen` state.
    - Editor now displays default action maps configuration in inspector (even when not playing).
- **Input System Backend Support**:
    - Added support for both **C# Events** (`InvokeCSharpEvents`) and **Unity Events** (`InvokeUnityEvents`) notification behaviors on `PlayerInput`.
    - `InputActionSelector` and `ViewInputSystemComponent` now automatically detect and support both event systems.
    - If `PlayerInput.notificationBehavior` is set to unsupported modes (`SendMessages`/`BroadcastMessages`), it will be automatically changed to `InvokeCSharpEvents` at runtime with a warning logged, prompting users to update the setting in the inspector.
    - `ViewInputSystemComponent` now subscribes to `onControlsChanged` (C# events) or `controlsChangedEvent` (Unity Events) based on the notification behavior.

### Documentation

- **README.md**: Updated features, API descriptions, and best practices to use Root View terminology.

## [3.0.0] - 2026-01-18

### Added

- **Priority-based focus selection**:
    - Added `priority` to `ViewSelector`.
    - `SentinalManager.CurrentView` is now determined by highest priority (tie-breaker: recency).
- **View metadata accessors**:
    - Added `Priority`, `PreventDismissal`, `ExclusiveView`, `HideOtherViews`, `TrackView`, `PreventSelection`,
      `AutoSelectOnEnable`, `RememberLastSelected`, `IsActive` to `IViewSelector`.
    - Added `SentinalManager.MostRecentView`.
- **New Input System pipeline** (requires Unity Input System / `ENABLE_INPUT_SYSTEM`):
    - Added `ViewDismissalInputHandler` (Cancel/Back closes current view, Focus re-selects current view).
    - Added `ViewInputSystemHandler` for per-view input gating (optionally following a `ViewSelector`) and per-view action map config.
    - Added `ActionMapManager` for action map overlay management and restoration.
    - Added Input System utility/components: `InputActionSelector`, `ViewInputSystemComponent`, `ViewInputActionHandler`,
      `DisplayInputString`, `InputActionButton`, `InputActionButtonHold`, `TabbedView`, `TabbedViewInputHandler`.
- **Editor improvements**:
    - Updated `SentinalManager` inspector debug UI (current vs most-recent, priority display, input enabled display).
    - Added convenience buttons to add recommended Input System components (`ViewDismissalInputHandler`, `ActionMapManager`).

### Changed

- **View dismissal model**:
    - Replaced the old “root view” concept with **dismissal-protection** (`preventDismissal`).
    - `CloseAllViews(...)` now rather supports excluding dismissal-protected views.
- **Package metadata**:
    - Unity requirement updated to **2021.3**.
    - `changelogUrl` now points to `CHANGELOG.md` (instead of latest GitHub release).
    - Keywords updated and the `Samples/Components` sample was removed from `package.json`.
- **Samples**:
    - Removed the old `Samples/Components` sample set; examples are now centered around `Samples/Examples`.

### Removed

- Removed legacy runtime components:
    - `InputSystemHandler`
    - `InputActionSwitcher`
- Removed old editor:
    - `SentinalViewSelectorEditor`
