# Gamepad Support Implementation

This document outlines the gamepad input support that has been added to the FH5 Trainer application.

## Overview

The application now supports binding features to gamepad buttons in addition to keyboard hotkeys. This allows users to control trainer features using Xbox-compatible controllers.

## Architecture

### New Files Created

1. **`GamepadButton.cs`** - Enum defining supported gamepad buttons
2. **`GamepadManager.cs`** - Manages gamepad input polling and state detection
3. Updated **`GlobalHotkey.cs`** - Extended to support gamepad bindings
4. Updated **`HotkeysManager.cs`** - Enhanced to handle both keyboard and gamepad input
5. Updated **`MainWindow.xaml`** and **`MainWindow.xaml.cs`** - UI for gamepad binding

### Supported Gamepad Buttons

- Face buttons: A, B, X, Y
- Shoulder buttons: Left/Right Bumper
- System buttons: Back, Start
- Stick clicks: Left/Right Stick
- D-Pad: Up, Down, Left, Right
- Triggers: Left/Right Trigger (with threshold)

## Usage

### For Users

1. In the main window, select a cheat from the dropdown
2. Choose between "Keyboard" or "Gamepad" input type
3. For gamepad input:
   - Select the desired gamepad button from the dropdown
   - Click "SET" to assign the binding
4. Save all hotkeys using the "Save All Hotkeys" button

### For Developers

#### Creating Keyboard Hotkeys (existing)
```csharp
private readonly GlobalHotkey _keyboardHotkey = new("Feature Name", ModifierKeys.Ctrl, Key.F1, CallbackMethod, 250);
```

#### Creating Gamepad Hotkeys (new)
```csharp
private readonly GlobalHotkey _gamepadHotkey = new("Feature Name (Gamepad)", GamepadButton.A, CallbackMethod, 250);
```

#### Registering Hotkeys
```csharp
HotkeysManager.Register(_keyboardHotkey);
HotkeysManager.Register(_gamepadHotkey);
```

## Dependencies

- **SharpDX.XInput** (4.2.0) - Provides XInput support for Xbox-compatible controllers

## Technical Details

### Polling Rate
- Gamepad input is polled at ~60 FPS (16ms intervals)
- Button press events are detected on state transitions (not pressed → pressed)

### Controller Support
- Supports up to 4 controllers simultaneously
- Automatically detects connected/disconnected controllers
- Works with Xbox controllers and other XInput-compatible devices

### Persistence
- Gamepad bindings are saved/loaded just like keyboard hotkeys
- Settings are stored in JSON format in the system temp directory

## Example Implementation

See `Handling.xaml.cs` for an example of how the Jump Hack feature has been extended to support both keyboard (Q key) and gamepad (A button) input:

```csharp
// Keyboard hotkey
private readonly GlobalHotkey _velocityHotkey = new("Velocity", ModifierKeys.None, Key.Q, VelocityCallback, 1);

// Gamepad hotkey
private readonly GlobalHotkey _jumpHackGamepadHotkey = new("Jump Hack (Gamepad)", GamepadButton.A, JumpHackCallback, 1000);
```

## Error Handling

- Graceful fallback if no controllers are connected
- Prevents duplicate button assignments
- Automatic cleanup on application shutdown

## Future Enhancements

Potential areas for future improvement:
- Analog stick input support
- Controller-specific button layouts
- Multiple controller assignment per feature
- Button combination support (e.g., LB + A)
