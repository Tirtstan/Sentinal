# Sentinal

![Sentinal header](Documentation/Images/Header.png)

Sentinal is a Unity UGUI routing and input-control package. It provides view history, keyboard and gamepad selection, cancel and back handling, view-layer isolation, and Input System action-map gates.

Selection replaces UGUI's built-in navigation instead of working around it: automatic spatial search with per-direction angles, optional diagonals, ordered preferred targets, and opt-in wrapping. Controls created or enabled at runtime take part with no extra setup.

Use it for controller-driven pause menus, settings screens, couch co-op lobbies, tabbed panels, modal prompts, and HUD overlays.

[Quick start](#quick-start) | [Why Sentinal](#why-sentinal) | [Components](Documentation/Components.md) | [Installation](#installation) | [Samples & debugging](#samples--debugging)

---

## Why Sentinal

![Sentinal Debug Window](Documentation/Images/SentinalDebugWindow.png)
_The Sentinal Debug Window at **Window > Sentinal > Debug** shows the view stack, priorities, and input gates._

- Routable views: add `ViewSelector`, open directly or through a `ViewAddress` asset. Priority and recency resolve overlaps.
- Selection: `SelectionNavigator` replaces UGUI navigation with spatial search, preferred targets, and opt-in wrap. Runtime controls join automatically.
- Dismissal: one `ViewDismissalInputHandler` sends Cancel/Back to the top non-root view, so `Esc`, `B`, and `Circle` share a route.
- Input gates: per-view `ViewInputSystemHandler` and `ActionMapGate` toggle actions by focus. Views register themselves, no manager prefab needed.

## Quick start

Build a pause menu. The whole package shows up in this one example, and the hierarchy below is the shape Sentinal expects: one view root, plain buttons under it, one navigator per button.

```
PauseMenu                         ViewSelector, ViewInputSystemHandler, ActionMapGate
├── Header                        TextMeshProUGUI ("Paused")
└── Buttons
    ├── ResumeButton              Button, SelectionNavigator
    ├── OptionsButton             Button, SelectionNavigator
    └── QuitButton                Button, SelectionNavigator
```

### 1. Lay out the hierarchy

Build the tree above with normal UGUI objects. One view per root, buttons as siblings under a shared parent.

### 2. Make the panel routable

Add `ViewSelector` to `PauseMenu`, set **First Selected** to `ResumeButton`, then `pauseMenu.Open()` / `pauseMenu.Close()`. `Priority` wins overlaps, `Root View` marks screens cancel must never close.

![ViewSelector inspector](Documentation/Images/ViewSelector.png)

### 3. Make the buttons selectable

Add `SelectionNavigator` to each button. That is the whole wiring step: it disables UGUI navigation, registers while enabled, and resolves preferred targets first, spatial search second, wrap last. See `Components.md` for angles, diagonals, masks, and wrap rules.

![SelectionNavigator inspector](Documentation/Images/SelectionNavigator.png)

Select a button in the Scene view to verify: solid lines are authored targets, dotted lines are automatic hits, cyan `(wrap)` lines are wraps, the fan is the search cone.

### 4. Gate input while the menu is focused

Add `ViewInputSystemHandler` plus `ActionMapGate` to `PauseMenu`. The handler enables view input only while focused; the gate swaps action maps (e.g. enable `UI`, disable `Gameplay`). Details in `Components.md`.

![ViewInputSystemHandler inspector](Documentation/Images/ViewInput.png)
![ActionMapGate inspector](Documentation/Images/ActionMapGate.png)

### 5. Wire back navigation once, globally

Add `ViewDismissalInputHandler` to a persistent UI object and point it at `UI/Cancel`. One handler covers every menu.

![ViewDismissal inspector](Documentation/Images/ViewDismissal.png)

For address-based routing (`ViewAddress`, `ViewLink`, prefabs, Addressables), see [Components.md](Documentation/Components.md).

## Component map

| Area                  | Use these                                                                                                  | What they solve                                      |
| --------------------- | ---------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| Routing               | `SentinalViewRouter`, `ViewSelector`, `ViewAddress`, `ViewLink`                                            | Open, close, and focus views without scene refs.     |
| Selection navigation  | `SelectionNavigator`                                                                                       | Controller navigation for dynamic layouts.           |
| View layering         | `ViewGroupConfig`, `ViewGroupMask`                                                                         | Isolate HUDs, menus, popups, and tabs.               |
| Input ownership       | `SentinalPlayer`, `ViewInputSystemHandler`, `ActionMapGate`, `ViewDismissalInputHandler`                   | Route input to the right player, gate maps by focus. |
| Input-driven controls | `InputActionButton`, `InputActionButtonHold`, `TabbedView`, `TabbedViewInputHandler`, `DisplayInputString` | Buttons, holds, tabs, and prompts from actions.      |
| Text prompts          | `TextInputGateway`, `PromptedTextField`                                                                    | Controller-friendly text entry.                      |

Full guide: [Components.md](Documentation/Components.md).

## Installation

Requires Unity `2021.3`+, UGUI, Input System (for `Sentinal.InputSystem`), TextMeshPro (for prompts). Add via **Package Manager > + > Add package from git URL**:

```text
https://github.com/Tirtstan/Sentinal.git
```

## Samples & debugging

Import **Examples** from the Package Manager (`Samples/Examples`) for routing, gated menus, action buttons, and tabs. Inspect live views with **Window > Sentinal > Debug**, or `SentinalViewRouter.GetDebugString()`.
