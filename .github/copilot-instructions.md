## Reference Implementation
- The `BT-cpl_shortcut` project in this workspace contains a fully working WinForms version of the app.
- Its logic for finding, listing, connecting, and disconnecting Bluetooth audio devices (both API and UI paths) is tested and should be used as a blueprint/guide for the business logic in this WPF UI app.
- When implementing or refactoring device management, refer to the code in `BT-cpl_shortcut` for correct logic and edge case handling.

# Copilot Instructions: Tray Icon App for Bluetooth Audio Devices

## Project Overview
- **Purpose:**
  - A lightweight Windows tray application to manage Bluetooth audio devices.
  - Provides a tray icon and a right-click (RMB) menu for quick device connection/disconnection and settings.

## UI/UX Requirements
- **Framework:** WPF with WPF UI library (using .NET 8 LTS recommended).
- **Tray Icon:**
  - App lives in the system tray. Use the "BT-cpl_shortcut.ico" icon for the tray.
  - Right-clicking the tray icon opens a custom menu.
  - Single LMB clicking the tray icon should connect or diconnect the devices that are marked by check boxes in the custom menu.
  - Double LMB clicking the tray icon should should open Window Bluteooth & Devices settings page.
  - Because we can both single and double LMB click the tray icon, we should have a short delay (e.g. 300ms) after the first click to determine if it's a single or double click before executing the action.
- **Menu Layout:**
  - **The tray icon opens a custom context menu with this layout:**
  ```
  +-------------------------------------------------------------+
  | Title text                    [settings gear icon]  [Exit]  |
  |-------------------------------------------------------------|
  | [1] BT Device 3   [2 status icon]   [3 Connect/Disconnect]  |
  | [1] BT Device 2   [2 status icon]   [3 Connect/Disconnect]  |
  | [1] BT Device 1   [2 status icon]   [3 Connect/Disconnect]  |
  |-------------------------------------------------------------|
  | Open Bluetooth & Devices Settings                           |
  |-------------------------------------------------------------|
  ```
  - **Settings menu**
  ```
  +---------------------------------------------------------------+
  | Settings                                 [QuickBTTray-v1.2.3] |
  |---------------------------------------------------------------|
  | [ ] Notification                                              |
  | [ ] Start with Windows                                        |
  |---------------------------------------------------------------|
  | Connect via:    (o) API   ( ) UI                              |
  | Disconnect via: (o) API   ( ) UI                              |
  +---------------------------------------------------------------+
  ```

  - **Bluetooth Devices Section:**
    - Each row: [ ] Checkbox (select/enable for batch connect/disconnect), [status icon] (shows connected/disconnected), [Connect] and [Disconnect] buttons for manual control.
  - **Divider:**
    - Horizontal line separates devices from connection method options.
  - **Connection Method Section:**
    - "Connect by" and "Disconnect by" each have radio buttons to select between UI (Windows UI) or API (programmatic) method.
  - **Exit Option:**
    - Simple menu item to close the app.
  - **Bluetooth Devices Section:**
    - Each row: [1] Checkbox (select/enable), selected ones are the ones we will try and connect/disconnect when single LMB on tray icon, [2] Status icon (connected/disconnected), shows whether the BT audio device is connected/disconnected, [3] Connect/Disconnect button, will manually connect or disconnect the device.
  - **Divider:**
    - Horizontal line separates devices from settings.
  - **Settings Section:**
    - "Connect by" uses a radio button to select between UI (Windows UI) or API (programmatic) method for connecting devices.
    - "Disconnect by" uses a radio button to select between UI (Windows UI) or API (programmatic) method for disconnecting devices.
  - **Exit Option:**
    - Simple menu item to close the app.
- **Styling:**
  - Dark background, white text.
  - Menu visually grouped: devices (top), settings (middle), exit (bottom).
  - Menu should automatically follow Windows dark/light mode.

## Architecture & Design
- **Separation of Concerns:**
  - Keep business logic and menu UI as decoupled as possible (MVVM pattern recommended).
  - Allow for easy swapping of UI implementation without changing core logic.
- **Bluetooth Device Logic:**
  - Scaffold as interfaces/services for easy testing and future extension.

## Technical Notes
- **.NET Version:** .NET 8 LTS preferred (WPF UI supports .NET 6+).
- **WPF UI Library:** Use for modern theming and automatic dark/light mode support.
- **Deployment:**
  - App should be standalone and lightweight.
  - Minimize dependencies and keep resource usage low.
  - Windows UI navigating to Bluetooth & Devices settings page can be done via `ms-settings:bluetooth` URI. or `Bthprops.cpl\BluetoothFindNextDevice`

## References
- [WPF UI Documentation](https://wpfui.lepo.co/)
- [ModernWpf (alternative)](https://github.com/Kinnara/ModernWpf)

---

> These instructions summarize the requirements and design goals for the tray icon app as discussed in chat. Use this as a guide for Copilot and contributors to maintain consistency and focus.
