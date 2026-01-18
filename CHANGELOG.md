# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

