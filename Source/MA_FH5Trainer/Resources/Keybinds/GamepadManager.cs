using System.Timers;
using SharpDX.XInput;
using DirectInput = SharpDX.DirectInput;

namespace MA_FH5Trainer.Resources.Keybinds;

/// <summary>
/// Manages gamepad and steering wheel input detection and state polling
/// </summary>
public static class GamepadManager
{
    private static readonly Controller[] s_controllers = new Controller[4];
    private static readonly State[] s_previousStates = new State[4];
    private static readonly State[] s_currentStates = new State[4];
    
    // DirectInput for steering wheels
    private static DirectInput.DirectInput? s_directInput;
    private static readonly List<DirectInput.Joystick> s_steeringWheels = new();
    private static readonly Dictionary<DirectInput.Joystick, DirectInput.JoystickState> s_previousWheelStates = new();
    private static readonly Dictionary<DirectInput.Joystick, DirectInput.JoystickState> s_currentWheelStates = new();
    
    private static System.Timers.Timer? s_pollTimer;
    private static bool s_isInitialized = false;

    /// <summary>
    /// Event raised when a gamepad button is pressed
    /// </summary>
    public static event Action<int, GamepadButton>? ButtonPressed;

    /// <summary>
    /// Event raised when any gamepad button is pressed during listening mode
    /// </summary>
    public static event Action<int, GamepadButton>? AnyButtonPressed;

    private static bool s_isListening = false;

    static GamepadManager()
    {
        for (int i = 0; i < 4; i++)
        {
            s_controllers[i] = new Controller((UserIndex)i);
        }
    }

    /// <summary>
    /// Initializes gamepad and steering wheel polling
    /// </summary>
    public static void Initialize()
    {
        if (s_isInitialized)
            return;

        // Initialize DirectInput for steering wheels
        InitializeSteeringWheels();

        s_pollTimer = new System.Timers.Timer(16); // ~60 FPS polling
        s_pollTimer.Elapsed += PollGamepads;
        s_pollTimer.AutoReset = true;
        s_pollTimer.Start();

        s_isInitialized = true;
    }

    /// <summary>
    /// Shuts down gamepad and steering wheel polling
    /// </summary>
    public static void Shutdown()
    {
        if (!s_isInitialized)
            return;

        s_pollTimer?.Stop();
        s_pollTimer?.Dispose();
        s_pollTimer = null;

        // Clean up DirectInput resources
        foreach (var wheel in s_steeringWheels)
        {
            wheel?.Dispose();
        }
        s_steeringWheels.Clear();
        s_previousWheelStates.Clear();
        s_currentWheelStates.Clear();
        
        s_directInput?.Dispose();
        s_directInput = null;

        s_isInitialized = false;
    }

    /// <summary>
    /// Checks if any connected controller has the specified button pressed
    /// </summary>
    /// <param name="button">The button to check</param>
    /// <returns>True if the button is pressed on any controller</returns>
    public static bool IsButtonPressed(GamepadButton button)
    {
        if (!s_isInitialized)
            return false;

        for (int i = 0; i < 4; i++)
        {
            if (s_controllers[i].IsConnected && IsButtonPressed(i, button))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a specific controller has the specified button pressed
    /// </summary>
    /// <param name="controllerIndex">Controller index (0-3)</param>
    /// <param name="button">The button to check</param>
    /// <returns>True if the button is pressed</returns>
    public static bool IsButtonPressed(int controllerIndex, GamepadButton button)
    {
        if (controllerIndex < 0 || controllerIndex >= 4 || !s_controllers[controllerIndex].IsConnected)
            return false;

        var gamepad = s_currentStates[controllerIndex].Gamepad;

        return button switch
        {
            GamepadButton.A => gamepad.Buttons.HasFlag(GamepadButtonFlags.A),
            GamepadButton.B => gamepad.Buttons.HasFlag(GamepadButtonFlags.B),
            GamepadButton.X => gamepad.Buttons.HasFlag(GamepadButtonFlags.X),
            GamepadButton.Y => gamepad.Buttons.HasFlag(GamepadButtonFlags.Y),
            GamepadButton.LeftBumper => gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder),
            GamepadButton.RightBumper => gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder),
            GamepadButton.Back => gamepad.Buttons.HasFlag(GamepadButtonFlags.Back),
            GamepadButton.Start => gamepad.Buttons.HasFlag(GamepadButtonFlags.Start),
            GamepadButton.LeftStick => gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb),
            GamepadButton.RightStick => gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb),
            GamepadButton.DPadUp => gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp),
            GamepadButton.DPadDown => gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown),
            GamepadButton.DPadLeft => gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft),
            GamepadButton.DPadRight => gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight),
            GamepadButton.LeftTrigger => gamepad.LeftTrigger > 128, // Trigger threshold
            GamepadButton.RightTrigger => gamepad.RightTrigger > 128, // Trigger threshold
            _ => false
        };
    }

    private static void PollGamepads(object? sender, ElapsedEventArgs e)
    {
        try
        {
            // Poll Xbox controllers
            for (int i = 0; i < 4; i++)
            {
                if (!s_controllers[i].IsConnected)
                    continue;

                // Store previous state
                s_previousStates[i] = s_currentStates[i];

                // Get current state
                if (s_controllers[i].GetState(out s_currentStates[i]))
                {
                    // Check for button press events (transition from not pressed to pressed)
                    CheckButtonEvents(i);
                }
            }

            // Poll steering wheels
            foreach (var wheel in s_steeringWheels.ToArray()) // ToArray to avoid modification during iteration
            {
                try
                {
                    if (s_previousWheelStates.TryGetValue(wheel, out var prevState))
                    {
                        s_previousWheelStates[wheel] = s_currentWheelStates[wheel];
                    }

                    wheel.Poll();
                    var currentState = wheel.GetCurrentState();
                    s_currentWheelStates[wheel] = currentState;

                    // Check for wheel button press events
                    CheckWheelButtonEvents(wheel);
                }
                catch (Exception)
                {
                    // Wheel disconnected or error, remove it
                    s_steeringWheels.Remove(wheel);
                    s_previousWheelStates.Remove(wheel);
                    s_currentWheelStates.Remove(wheel);
                    wheel?.Dispose();
                }
            }
        }
        catch (Exception)
        {
            // Ignore polling errors
        }
    }

    private static void CheckButtonEvents(int controllerIndex)
    {
        var previous = s_previousStates[controllerIndex].Gamepad;
        var current = s_currentStates[controllerIndex].Gamepad;

        // Check each button for press events
        CheckButton(controllerIndex, GamepadButton.A, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.A) && current.Buttons.HasFlag(GamepadButtonFlags.A));
        CheckButton(controllerIndex, GamepadButton.B, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.B) && current.Buttons.HasFlag(GamepadButtonFlags.B));
        CheckButton(controllerIndex, GamepadButton.X, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.X) && current.Buttons.HasFlag(GamepadButtonFlags.X));
        CheckButton(controllerIndex, GamepadButton.Y, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.Y) && current.Buttons.HasFlag(GamepadButtonFlags.Y));
        CheckButton(controllerIndex, GamepadButton.LeftBumper, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder) && current.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder));
        CheckButton(controllerIndex, GamepadButton.RightBumper, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.RightShoulder) && current.Buttons.HasFlag(GamepadButtonFlags.RightShoulder));
        CheckButton(controllerIndex, GamepadButton.Back, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.Back) && current.Buttons.HasFlag(GamepadButtonFlags.Back));
        CheckButton(controllerIndex, GamepadButton.Start, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.Start) && current.Buttons.HasFlag(GamepadButtonFlags.Start));
        CheckButton(controllerIndex, GamepadButton.LeftStick, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.LeftThumb) && current.Buttons.HasFlag(GamepadButtonFlags.LeftThumb));
        CheckButton(controllerIndex, GamepadButton.RightStick, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.RightThumb) && current.Buttons.HasFlag(GamepadButtonFlags.RightThumb));
        CheckButton(controllerIndex, GamepadButton.DPadUp, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && current.Buttons.HasFlag(GamepadButtonFlags.DPadUp));
        CheckButton(controllerIndex, GamepadButton.DPadDown, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.DPadDown) && current.Buttons.HasFlag(GamepadButtonFlags.DPadDown));
        CheckButton(controllerIndex, GamepadButton.DPadLeft, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.DPadLeft) && current.Buttons.HasFlag(GamepadButtonFlags.DPadLeft));
        CheckButton(controllerIndex, GamepadButton.DPadRight, 
            !previous.Buttons.HasFlag(GamepadButtonFlags.DPadRight) && current.Buttons.HasFlag(GamepadButtonFlags.DPadRight));
        CheckButton(controllerIndex, GamepadButton.LeftTrigger, 
            previous.LeftTrigger <= 128 && current.LeftTrigger > 128);
        CheckButton(controllerIndex, GamepadButton.RightTrigger, 
            previous.RightTrigger <= 128 && current.RightTrigger > 128);
    }

    private static void CheckButton(int controllerIndex, GamepadButton button, bool isPressed)
    {
        if (isPressed)
        {
            ButtonPressed?.Invoke(controllerIndex, button);
            
            // If we're in listening mode, also raise the AnyButtonPressed event
            if (s_isListening)
            {
                AnyButtonPressed?.Invoke(controllerIndex, button);
            }
        }
    }

    private static void CheckWheelButtonEvents(DirectInput.Joystick wheel)
    {
        if (!s_previousWheelStates.TryGetValue(wheel, out var previous) || 
            !s_currentWheelStates.TryGetValue(wheel, out var current))
            return;

        // Check each wheel button for press events (with bounds checking)
        var buttonCount = Math.Min(current.Buttons.Length, 16); // Limit to 16 buttons
        
        if (buttonCount > 0) CheckWheelButton(wheel, GamepadButton.WheelButton1, previous.Buttons.Length > 0 && current.Buttons.Length > 0 && !previous.Buttons[0] && current.Buttons[0]);
        if (buttonCount > 1) CheckWheelButton(wheel, GamepadButton.WheelButton2, previous.Buttons.Length > 1 && current.Buttons.Length > 1 && !previous.Buttons[1] && current.Buttons[1]);
        if (buttonCount > 2) CheckWheelButton(wheel, GamepadButton.WheelButton3, previous.Buttons.Length > 2 && current.Buttons.Length > 2 && !previous.Buttons[2] && current.Buttons[2]);
        if (buttonCount > 3) CheckWheelButton(wheel, GamepadButton.WheelButton4, previous.Buttons.Length > 3 && current.Buttons.Length > 3 && !previous.Buttons[3] && current.Buttons[3]);
        if (buttonCount > 4) CheckWheelButton(wheel, GamepadButton.WheelButton5, previous.Buttons.Length > 4 && current.Buttons.Length > 4 && !previous.Buttons[4] && current.Buttons[4]);
        if (buttonCount > 5) CheckWheelButton(wheel, GamepadButton.WheelButton6, previous.Buttons.Length > 5 && current.Buttons.Length > 5 && !previous.Buttons[5] && current.Buttons[5]);
        if (buttonCount > 6) CheckWheelButton(wheel, GamepadButton.WheelButton7, previous.Buttons.Length > 6 && current.Buttons.Length > 6 && !previous.Buttons[6] && current.Buttons[6]);
        if (buttonCount > 7) CheckWheelButton(wheel, GamepadButton.WheelButton8, previous.Buttons.Length > 7 && current.Buttons.Length > 7 && !previous.Buttons[7] && current.Buttons[7]);
        if (buttonCount > 8) CheckWheelButton(wheel, GamepadButton.WheelButton9, previous.Buttons.Length > 8 && current.Buttons.Length > 8 && !previous.Buttons[8] && current.Buttons[8]);
        if (buttonCount > 9) CheckWheelButton(wheel, GamepadButton.WheelButton10, previous.Buttons.Length > 9 && current.Buttons.Length > 9 && !previous.Buttons[9] && current.Buttons[9]);
        if (buttonCount > 10) CheckWheelButton(wheel, GamepadButton.WheelButton11, previous.Buttons.Length > 10 && current.Buttons.Length > 10 && !previous.Buttons[10] && current.Buttons[10]);
        if (buttonCount > 11) CheckWheelButton(wheel, GamepadButton.WheelButton12, previous.Buttons.Length > 11 && current.Buttons.Length > 11 && !previous.Buttons[11] && current.Buttons[11]);
        if (buttonCount > 12) CheckWheelButton(wheel, GamepadButton.LeftPaddle, previous.Buttons.Length > 12 && current.Buttons.Length > 12 && !previous.Buttons[12] && current.Buttons[12]);
        if (buttonCount > 13) CheckWheelButton(wheel, GamepadButton.RightPaddle, previous.Buttons.Length > 13 && current.Buttons.Length > 13 && !previous.Buttons[13] && current.Buttons[13]);
        if (buttonCount > 14) CheckWheelButton(wheel, GamepadButton.WheelStart, previous.Buttons.Length > 14 && current.Buttons.Length > 14 && !previous.Buttons[14] && current.Buttons[14]);
        if (buttonCount > 15) CheckWheelButton(wheel, GamepadButton.WheelSelect, previous.Buttons.Length > 15 && current.Buttons.Length > 15 && !previous.Buttons[15] && current.Buttons[15]);

        // Check D-pad (with bounds checking)
        if (previous.PointOfViewControllers.Length > 0 && current.PointOfViewControllers.Length > 0)
        {
            var prevDPad = GetDPadFromPOV(previous.PointOfViewControllers[0]);
            var currDPad = GetDPadFromPOV(current.PointOfViewControllers[0]);
            
            if (prevDPad != GamepadButton.WheelDPadUp && currDPad == GamepadButton.WheelDPadUp)
                CheckWheelButton(wheel, GamepadButton.WheelDPadUp, true);
            if (prevDPad != GamepadButton.WheelDPadDown && currDPad == GamepadButton.WheelDPadDown)
                CheckWheelButton(wheel, GamepadButton.WheelDPadDown, true);
            if (prevDPad != GamepadButton.WheelDPadLeft && currDPad == GamepadButton.WheelDPadLeft)
                CheckWheelButton(wheel, GamepadButton.WheelDPadLeft, true);
            if (prevDPad != GamepadButton.WheelDPadRight && currDPad == GamepadButton.WheelDPadRight)
                CheckWheelButton(wheel, GamepadButton.WheelDPadRight, true);
        }
    }

    private static GamepadButton GetDPadFromPOV(int pov)
    {
        if (pov == -1) return GamepadButton.None;
        
        if (pov >= 31500 || pov <= 4500) return GamepadButton.WheelDPadUp;
        if (pov >= 4500 && pov <= 13500) return GamepadButton.WheelDPadRight;
        if (pov >= 13500 && pov <= 22500) return GamepadButton.WheelDPadDown;
        if (pov >= 22500 && pov <= 31500) return GamepadButton.WheelDPadLeft;
        
        return GamepadButton.None;
    }

    private static void CheckWheelButton(DirectInput.Joystick wheel, GamepadButton button, bool isPressed)
    {
        if (isPressed)
        {
            ButtonPressed?.Invoke(-1, button); // Use -1 to indicate wheel instead of gamepad controller
            
            // If we're in listening mode, also raise the AnyButtonPressed event
            if (s_isListening)
            {
                AnyButtonPressed?.Invoke(-1, button);
            }
        }
    }

    /// <summary>
    /// Gets a list of connected controller indices
    /// </summary>
    /// <returns>Array of connected controller indices</returns>
    public static int[] GetConnectedControllers()
    {
        var connected = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            if (s_controllers[i].IsConnected)
                connected.Add(i);
        }
        return connected.ToArray();
    }

    /// <summary>
    /// Starts listening for any gamepad button press
    /// </summary>
    public static void StartListening()
    {
        s_isListening = true;
    }

    /// <summary>
    /// Stops listening for gamepad button presses
    /// </summary>
    public static void StopListening()
    {
        s_isListening = false;
    }

    /// <summary>
    /// Gets whether the manager is currently listening for button presses
    /// </summary>
    public static bool IsListening => s_isListening;

    /// <summary>
    /// Initializes DirectInput and detects steering wheels
    /// </summary>
    private static void InitializeSteeringWheels()
    {
        try
        {
            s_directInput = new DirectInput.DirectInput();
            
            // Find all joystick devices that could be steering wheels
            var joystickDevices = s_directInput.GetDevices(DirectInput.DeviceType.Joystick, DirectInput.DeviceEnumerationFlags.AllDevices);
            
            foreach (var deviceInstance in joystickDevices)
            {
                try
                {
                    var joystick = new DirectInput.Joystick(s_directInput, deviceInstance.InstanceGuid);
                    
                    // Check if this might be a steering wheel by looking for steering axis
                    joystick.Properties.BufferSize = 128;
                    joystick.Acquire();
                    
                    // Add to our list of steering wheels
                    s_steeringWheels.Add(joystick);
                    s_previousWheelStates[joystick] = new DirectInput.JoystickState();
                    s_currentWheelStates[joystick] = new DirectInput.JoystickState();
                }
                catch (Exception)
                {
                    // Skip devices that can't be initialized
                }
            }
        }
        catch (Exception)
        {
            // DirectInput initialization failed, continue without steering wheel support
        }
    }

    /// <summary>
    /// Checks if any steering wheel has the specified button pressed
    /// </summary>
    /// <param name="button">The wheel button to check</param>
    /// <returns>True if the button is pressed on any wheel</returns>
    public static bool IsWheelButtonPressed(GamepadButton button)
    {
        if (!s_isInitialized || s_steeringWheels.Count == 0)
            return false;

        foreach (var wheel in s_steeringWheels)
        {
            if (IsWheelButtonPressed(wheel, button))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a specific steering wheel has the specified button pressed
    /// </summary>
    /// <param name="wheel">The steering wheel joystick</param>
    /// <param name="button">The button to check</param>
    /// <returns>True if the button is pressed</returns>
    private static bool IsWheelButtonPressed(DirectInput.Joystick wheel, GamepadButton button)
    {
        if (!s_currentWheelStates.TryGetValue(wheel, out var state))
            return false;

        return button switch
        {
            GamepadButton.WheelButton1 => state.Buttons.Length > 0 && state.Buttons[0],
            GamepadButton.WheelButton2 => state.Buttons.Length > 1 && state.Buttons[1],
            GamepadButton.WheelButton3 => state.Buttons.Length > 2 && state.Buttons[2],
            GamepadButton.WheelButton4 => state.Buttons.Length > 3 && state.Buttons[3],
            GamepadButton.WheelButton5 => state.Buttons.Length > 4 && state.Buttons[4],
            GamepadButton.WheelButton6 => state.Buttons.Length > 5 && state.Buttons[5],
            GamepadButton.WheelButton7 => state.Buttons.Length > 6 && state.Buttons[6],
            GamepadButton.WheelButton8 => state.Buttons.Length > 7 && state.Buttons[7],
            GamepadButton.WheelButton9 => state.Buttons.Length > 8 && state.Buttons[8],
            GamepadButton.WheelButton10 => state.Buttons.Length > 9 && state.Buttons[9],
            GamepadButton.WheelButton11 => state.Buttons.Length > 10 && state.Buttons[10],
            GamepadButton.WheelButton12 => state.Buttons.Length > 11 && state.Buttons[11],
            GamepadButton.LeftPaddle => state.Buttons.Length > 12 && state.Buttons[12],
            GamepadButton.RightPaddle => state.Buttons.Length > 13 && state.Buttons[13],
            GamepadButton.WheelStart => state.Buttons.Length > 14 && state.Buttons[14],
            GamepadButton.WheelSelect => state.Buttons.Length > 15 && state.Buttons[15],
            GamepadButton.WheelDPadUp => state.PointOfViewControllers.Length > 0 && (state.PointOfViewControllers[0] >= 31500 || state.PointOfViewControllers[0] <= 4500),
            GamepadButton.WheelDPadRight => state.PointOfViewControllers.Length > 0 && (state.PointOfViewControllers[0] >= 4500 && state.PointOfViewControllers[0] <= 13500),
            GamepadButton.WheelDPadDown => state.PointOfViewControllers.Length > 0 && (state.PointOfViewControllers[0] >= 13500 && state.PointOfViewControllers[0] <= 22500),
            GamepadButton.WheelDPadLeft => state.PointOfViewControllers.Length > 0 && (state.PointOfViewControllers[0] >= 22500 && state.PointOfViewControllers[0] <= 31500),
            _ => false
        };
    }

    /// <summary>
    /// Gets a list of connected steering wheel names
    /// </summary>
    /// <returns>Array of connected steering wheel names</returns>
    public static string[] GetConnectedSteeringWheels()
    {
        return s_steeringWheels.Where(w => w != null).Select(w => w.Information.ProductName).ToArray();
    }
}
