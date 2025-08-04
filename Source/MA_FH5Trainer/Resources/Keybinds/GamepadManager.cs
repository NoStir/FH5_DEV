using System.Timers;
using SharpDX.XInput;

namespace MA_FH5Trainer.Resources.Keybinds;

/// <summary>
/// Manages gamepad input detection and state polling
/// </summary>
public static class GamepadManager
{
    private static readonly Controller[] s_controllers = new Controller[4];
    private static readonly State[] s_previousStates = new State[4];
    private static readonly State[] s_currentStates = new State[4];
    private static System.Timers.Timer? s_pollTimer;
    private static bool s_isInitialized = false;

    /// <summary>
    /// Event raised when a gamepad button is pressed
    /// </summary>
    public static event Action<int, GamepadButton>? ButtonPressed;

    static GamepadManager()
    {
        for (int i = 0; i < 4; i++)
        {
            s_controllers[i] = new Controller((UserIndex)i);
        }
    }

    /// <summary>
    /// Initializes gamepad polling
    /// </summary>
    public static void Initialize()
    {
        if (s_isInitialized)
            return;

        s_pollTimer = new System.Timers.Timer(16); // ~60 FPS polling
        s_pollTimer.Elapsed += PollGamepads;
        s_pollTimer.AutoReset = true;
        s_pollTimer.Start();

        s_isInitialized = true;
    }

    /// <summary>
    /// Shuts down gamepad polling
    /// </summary>
    public static void Shutdown()
    {
        if (!s_isInitialized)
            return;

        s_pollTimer?.Stop();
        s_pollTimer?.Dispose();
        s_pollTimer = null;

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
}
