# Menu Navigation System

A Unity package for managing menu navigation.

## Set Up

1.  Add the `MenuNavigatorManager` singleton object in your scene(s).
2.  Add a `MenuNavigator` component onto your desired user interface element.

> [!NOTE]  
> **`MenuNavigatorManager` is required for this package to work. `MenuNavigator` expects its attached game object to be disabled `(OnDisable)` and enabled `(OnEnable)` to respond to close and open events.**

## Features

-   **Menu Stack Management**: Handles multiple menus with a history-based system
-   **Input System Integration (Optional)**: Switching between action maps
-   **Automatic Navigation**: Auto-selection of UI elements when menus open/close
-   **Flexible Closing**: Support for custom close behaviors via the `ICloseableMenu` interface
-   **Event System**: Events for menu open/close/switch operations

## Requirements

-   Unity 2021.3 or later
-   Input System package (optional, for input handling features)
-   TextMeshPro (for sample scene)
