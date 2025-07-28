# Sentinal - Unity UI Management & Selection System

A Unity package for managing hierarchical menu/view navigation with history-based tracking management, input system integration, and automatic UI element selection.

## Quick Start

1. **Add the Core Manager**: Place the `Sentinal` singleton component in your scene.
1. **Setup Menu Views**: Add `SentinalViewSelector` components to your menu GameObjects for view history and selection support.
1. **Optional Input Integration**: Add `InputSystemHandler` for automatic input management.

> [!TIP]  
> You can use the provided **Sentinal** prefab within the "Examples" sample.

> [!IMPORTANT]  
> **The `Sentinal` component is required for this package to work. Auto selection and view tracking is triggered by GameObject activation/deactivation (`OnEnable`/`OnDisable`).**

## Features

### Core Navigation

-   **Menu/View Management:** Navigate through multiple menus/views with automatic history tracking.
-   **Smart UI Selection:** Auto-selection of UI elements with memory of last selected items.

## Input System Integration (Optional)

-   **Action Map Switching:** Automatic switching between Player/UI (or custom) action maps.
-   **Button Response:** Cancel and focus actions to backtrack and reselect views.

## Requirements

-   **Unity 2019.4** or later
-   **Input System** (optional, for input handling features)
-   **TextMeshPro** (for sample scene)

## Core Components

### `Sentinal` (Singleton Manager)

The central manager that handles all menu/view navigation logic.

```csharp
// Access the singleton
Sentinal.Instance.CloseCurrentView();
Sentinal.Instance.CloseAllViews();

// Check navigation state
bool hasMenus = Sentinal.Instance.AnyViewsOpen;
SentinalViewSelector current = Sentinal.Instance.CurrentView;
```

### `SentinalViewSelector` (Menu/View Component)

Add this to any parent or child `GameObject` that represents a menu or navigable view that has its active state enabled and disabled (`OnEnable`/`OnDisable`).

**Properties:**

-   `firstSelected` - The GameObject to auto-select when this view becomes active.
-   `rootView` - Treat as root view (added to history but never closed automatically).
-   `trackView` - Whether to include this view in the navigation history stack.
-   `autoSelectOnEnable` - Automatically select the first element when view is enabled.
-   `rememberLastSelected` - Remember and restore the last selected UI element.

### `InputSystemHandler` (Optional Input Manager)

Handles Input System integration for keyboard, gamepad, etc navigation.

**Properties:**

-   `playerInput` - Reference to the `PlayerInput` component for input handling. See `SetPlayerInput(PlayerInput)` for providing reference programmatically.
-   `cancelAction` - Input action to close the current menu/view.
-   `focusAction` - Input action to refocus the last selected element in current view.

### `InputActionSwitcher` (Auto Action Map Switching)

Automatically switches between action maps when menus open/close.

**Properties:**

-   `onEnableActionMapName` - Action map name to switch to when this view opens.
-   `onAllDisableActionMapName` - Action map name when all views are closed.
-   `rememberPreviousActionMap` - Remember and restore previous action map instead of using `onAllDisableActionMapName`.
-   `logSwitching` - Enable debug logging for action map switches.

## Usage Examples

### Basic Menu Setup

```csharp
// Your menu GameObject needs:
// 1. SentinalViewSelector component
// 2. Enable/Disable the GameObject to open/close menus

// Open a menu
menuGameObject.SetActive(true); // Automatically tracked by Sentinal

// Close current menu
Sentinal.Instance.CloseCurrentView();
```

### Custom Closeable Menu With `ICloseableView`

```csharp
public class CustomMenu : MonoBehaviour, ICloseableView
{
    public void Close()
    {
        // Custom close logic (animations, save data, etc.)
        StartCoroutine(CloseWithAnimation());
    }

    private IEnumerator CloseWithAnimation()
    {
        // Play close animation
        yield return new WaitForSeconds(0.3f);
        gameObject.SetActive(false);
    }
}
```

### Event Handling

```csharp
private void Start()
{
    Sentinal.Instance.OnPush += OnMenuOpened;
    Sentinal.Instance.OnPop += OnMenuClosed;
    Sentinal.Instance.OnSwitch += OnMenuSwitched;
}

private void OnMenuOpened(SentinalViewSelector view)
{
    Debug.Log($"Menu opened: {view.name}");
}

private void OnMenuSwitched(SentinalViewSelector from, SentinalViewSelector to)
{
    Debug.Log($"Switched from {from?.name} to {to?.name}");
}
```

## Debugging

### Runtime Inspector

The custom editor shows real-time debugging information:

-   Current view index in the stack.
-   Whether the view is currently active.

## Best Practices

1. **Always use GameObjects activation** for menu/view open/close operations.
2. **Set FirstSelected** on all `SentinalViewSelector` components for proper keyboard navigation.
3. **Use ICloseableView** for menus that need custom close animations or logic.
