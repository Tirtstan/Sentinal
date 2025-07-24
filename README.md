# Sentinal - Unity Menu Navigation System

A Unity package for managing hierarchical menu navigation with history-based stack management, input system integration, and automatic UI element selection.

## Quick Start

1. **Add the Core Manager**: Place the `Sentinal` singleton component in your scene.
2. **Setup Menu Views**: Add `SentinalViewSelector` components to your menu GameObjects for view history and selection support.
3. **Optional Input Integration**: Add `InputSystemHandler` for automatic input management.

> [!IMPORTANT]  
> **The `Sentinal` component is required for this package to work. Menu navigation is triggered by GameObject activation/deactivation (`OnEnable`/`OnDisable`).**

## Features

### Core Navigation

-   **Stack-Based Menu Management**: Navigate through multiple menus with automatic history tracking.
-   **Smart UI Selection**: Auto-selection of UI elements with memory of last selected items.
-   **Flexible View Switching**: Seamless transitions between different menu states.
-   **iew History Tracking**: Complete navigation history with debugging support.

### Input System Integration

-   **Input System Support**: Optional integration with Unity's new Input System.
-   **Action Map Switching**: Automatic switching between Player/UI action maps.
-   **Keyboard Navigation**: Built-in cancel and focus actions.
-   **Configurable Controls**: Customizable input actions for different menu behaviors.

### Developer Experience

-   **Custom Editor Tools**: Runtime debugging with view stack visualization.
-   **Extensible Interfaces**: `ICloseableView` to customise closing functionality.

## Requirements

-   **Unity 2019.4** or later
-   **Input System package** (optional, for input handling features)
-   **TextMeshPro** (for sample scenes)

## Core Components

### `Sentinal` (Singleton Manager)

The central manager that handles all menu navigation logic.

```csharp
// Access the singleton
Sentinal.Instance.CloseCurrentView();
Sentinal.Instance.CloseAllViews();

// Check navigation state
bool hasMenus = Sentinal.Instance.AnyViewsOpen;
SentinalViewSelector current = Sentinal.Instance.CurrentView;
```

### `SentinalViewSelector` (Menu Component)

Add this to any GameObject that represents a menu or navigable view.

```csharp
[SerializeField] private GameObject firstSelected;  // Auto-selected when menu opens
[SerializeField] private bool trackView = true;     // Include in navigation history
[SerializeField] private bool autoSelectOnEnable = true;
[SerializeField] private bool rememberLastSelected = true;
```

### `InputSystemHandler` (Optional Input Manager)

Handles Input System integration for keyboard/gamepad navigation.

```csharp
[SerializeField] private PlayerInput playerInput;
[SerializeField] private InputActionReference cancelAction;   // Close current menu
[SerializeField] private InputActionReference focusAction;    // Refocus last selected element
```

### `InputActionSwitcher` (Auto Action Map Switching)

Automatically switches between action maps when menus open/close.

```csharp
[SerializeField] private string onEnableActionMapName = "UI";
[SerializeField] private string onAllDisableActionMapName = "Player";
[SerializeField] private bool rememberPreviousActionMap = true;
```

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
