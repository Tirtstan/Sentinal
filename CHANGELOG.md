# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

- **ActionMapManager**: `PlayerInput` action maps will no longer be modified when switching to a view that does not have a `ViewInputSystemHandler`. _This was causing an issue where `ViewSelector`s without a `ViewInputSystemHandler` were disabling all action maps on affected `PlayerInput`s._

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
