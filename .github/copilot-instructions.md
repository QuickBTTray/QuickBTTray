# Copilot Instructions: Tray Icon App for Bluetooth Audio Devices

## Project Overview
- **Purpose:**
  - A lightweight Windows tray application to manage Bluetooth audio devices.
  - Provides a tray icon and a right-click (RMB) menu for quick device connection/disconnection and settings.

## UI/UX Requirements
- **Framework:** WPF with WPF UI library (using .NET 8 LTS recommended).
- **Tray Icon:**
  - App lives in the system tray.
  - Right-clicking the tray icon opens a custom menu.
  - Single LMB clicking the tray icon should connect or diconnect the devices that are marked by check boxes in the custom menu.
  - Double LMB clicking the tray icon should should open Window Bluteooth & Devices settings page.
- **Menu Layout:**
  - **The tray icon opens a custom context menu with this layout:**
  ```
  +---------------------------------------------------------------+
  | [ ] BT Device 3   [status icon]   [Connect] [Disconnect]      |
  | [ ] BT Device 2   [status icon]   [Connect] [Disconnect]      |
  | [ ] BT Device 1   [status icon]   [Connect] [Disconnect]      |
  |--------------------------------------------------------------|
  | Connect by:    (•) UI   ( ) API                              |
  | Disconnect by: (•) UI   ( ) API                              |
  |--------------------------------------------------------------|
  | Exit                                                        |
  +-------------------------------------------------------------+
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
    - Each row: [1] Checkbox (select/enable), selectd ones are the ones we will try and connect/disconnect when single LMB on try icon, [2] Status icon (connected/disconnected), shows wheter the BT audio device is connected/disconnected, [3] Connect/Disconnect button, will manually connect or disconnect the device.
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
