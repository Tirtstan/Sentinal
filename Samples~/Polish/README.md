# Layers and local players

Import this sample from Package Manager. Open LayersAndPlayers.unity.

Before running it, add SampleSettings.asset to Player Settings > Preloaded Assets in a standalone project. The sample controller registers that asset for the scene and restores the previous settings on destruction. In an existing project, run the sample alone, without game systems or other open views. Do not replace or edit the game's settings asset.

The sample uses pointer buttons and the Input System UI module. Its two PlayerInput objects demonstrate independent ownership without requiring two gamepads or gameplay actions. Click Player 1 menu and Player 2 menu to open both, then close either independently. These players have no gameplay action assets; map switching and controller pairing are deliberately left to the host project.

Open tabs, then click the left or right toggle to switch panels. Each panel closes the window. Open dialog to see CanvasGroupFade. The screen ignores Back; the window and dialog allow closure. All sample layer input policies are No change, so the example does not alter host action maps.

The scene and settings are authored assets. SampleMenus only registers settings and forwards button requests to the assigned views. No runtime UI or settings are generated.
