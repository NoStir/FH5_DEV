using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using MA_FH5Trainer.Resources.Keybinds;
using MA_FH5Trainer.Resources.Theme;
using MA_FH5Trainer.ViewModels.Windows;

namespace MA_FH5Trainer.Views.Windows;

public partial class MainWindow
{
    private bool _isListeningForAnyInput = false;
    private GlobalHotkey? _currentHotkeyBeingSet = null;
    private DispatcherTimer? _inputListeningTimer = null;

    public MainWindow()
    {
        Instance = this;
        ViewModel = new MainWindowViewModel();
        DataContext = this;
        Loaded += (_, _) =>
        {
            ViewModel.HotkeysEnabled = HotkeysManager.SetupSystemHook();
            set.IsEnabled = ViewModel.HotkeysEnabled;
        };

        ViewModel.MakeExpandersView();
        InitializeComponent();
        InitializeUnifiedInput();
        
        // Subscribe to gamepad button events for listening mode
        GamepadManager.AnyButtonPressed += OnAnyInputCaptured;
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from gamepad events
        GamepadManager.AnyButtonPressed -= OnAnyInputCaptured;
        
        // Clean up any active timer and reset listening states
        _inputListeningTimer?.Stop();
        _inputListeningTimer = null;
        _isListeningForAnyInput = false;
        
        HotkeysManager.ShutdownSystemHook();
        base.OnClosed(e);
    }

    public static MainWindow? Instance { get; private set; } = null;
    public MainWindowViewModel ViewModel { get; }
    public Theming Theming => Theming.GetInstance();

    private void MainWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        var isLeftButton = e.ChangedButton == MouseButton.Left;
        if (!isLeftButton)
        {
            return;
        }

        Point position = e.GetPosition(this);
        bool isWithinTopArea = position.Y < 50;
        if (!isWithinTopArea)
        {
            return;
        }

        DragMove();
    }

    private void WindowStateAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        switch (button.Tag)
        {
            case "1":
            {
                SystemCommands.MinimizeWindow(this);
                break;
            }
            case "2":
            {
                SystemCommands.CloseWindow(this);
                break;
            }
        }
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        ViewModel.Close();
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var dataContext = button?.DataContext;
        if (dataContext is not GlobalHotkey hotkey)
        {
            MessageBox.Show("No hotkey selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (_isListeningForAnyInput)
        {
            // Cancel listening mode
            ResetInputListening();
            return;
        }

        // Start listening for any input type
        _isListeningForAnyInput = true;
        _currentHotkeyBeingSet = hotkey;
        
        if (UnifiedInputTextBox != null)
        {
            UnifiedInputTextBox.Text = "Listening... Press any key, gamepad, or wheel button";
        }
        
        if (set != null)
        {
            set.Content = "CANCEL";
        }
        
        // Start listening for gamepad and wheel input
        GamepadManager.StartListening();

        // Set up timeout timer (10 seconds)
        _inputListeningTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _inputListeningTimer.Tick += (s, args) =>
        {
            ResetInputListening();
            MessageBox.Show("Input capture timeout. Please try again.", 
                "Timeout", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        _inputListeningTimer.Start();

        // Enable keyboard input capture
        this.KeyDown += OnKeyboardInputCaptured;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        HotkeysManager.SaveAll();
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box)
        {
            return;
        }

        GlobalHotkey? hotkey = ((GlobalHotkey?)box.SelectedItem);
        if (hotkey == null)
        {
            return;
        }

        // Update the unified input display based on the hotkey type
        if (UnifiedInputTextBox != null)
        {
            if (hotkey.UseSteeringWheel)
            {
                UnifiedInputTextBox.Text = hotkey.GamepadButton == GamepadButton.None 
                    ? "None" 
                    : $"Wheel: {hotkey.GamepadButton}";
            }
            else if (hotkey.UseGamepad)
            {
                UnifiedInputTextBox.Text = hotkey.GamepadButton == GamepadButton.None 
                    ? "None" 
                    : $"Gamepad: {hotkey.GamepadButton}";
            }
            else
            {
                if (hotkey.Key == Key.None)
                {
                    UnifiedInputTextBox.Text = "None";
                }
                else
                {
                    string modifierText = hotkey.Modifier != ModifierKeys.None ? hotkey.Modifier.ToString() + " + " : "";
                    UnifiedInputTextBox.Text = $"Keyboard: {modifierText}{hotkey.Key}";
                }
            }
        }
    }

    private void OnAnyInputCaptured(int controllerIndex, GamepadButton button)
    {
        if (!_isListeningForAnyInput || _currentHotkeyBeingSet == null)
            return;

        // Check if this is a wheel button (controllerIndex == -1 indicates wheel)
        bool isWheelButton = controllerIndex == -1;
        
        // Run on UI thread
        Dispatcher.Invoke(() =>
        {
            // Stop listening and timer
            GamepadManager.StopListening();
            _inputListeningTimer?.Stop();
            _inputListeningTimer = null;
            _isListeningForAnyInput = false;
            this.KeyDown -= OnKeyboardInputCaptured;

            // Check if this button is already assigned
            if (HotkeysManager.CheckExists(button))
            {
                string inputType = isWheelButton ? "Steering wheel" : "Gamepad";
                MessageBox.Show($"{inputType} button already assigned!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                ResetInputListening();
                return;
            }

            // Set the hotkey
            _currentHotkeyBeingSet.UseGamepad = !isWheelButton;
            _currentHotkeyBeingSet.UseSteeringWheel = isWheelButton;
            _currentHotkeyBeingSet.GamepadButton = button;
            _currentHotkeyBeingSet.Key = Key.None;
            _currentHotkeyBeingSet.Modifier = ModifierKeys.None;
            _currentHotkeyBeingSet.Hotkey = new MahApps.Metro.Controls.HotKey(Key.None);

            // Update the UI
            if (UnifiedInputTextBox != null)
            {
                string prefix = isWheelButton ? "Wheel" : "Gamepad";
                UnifiedInputTextBox.Text = $"{prefix}: {button}";
            }

            // Reset state
            _currentHotkeyBeingSet = null;
            if (set != null)
            {
                set.Content = "SET";
                set.IsEnabled = true;
            }
        });
    }

    private void OnKeyboardInputCaptured(object sender, KeyEventArgs e)
    {
        if (!_isListeningForAnyInput || _currentHotkeyBeingSet == null)
            return;

        e.Handled = true;

        var key = e.Key;
        var modifiers = Keyboard.Modifiers;

        // Ignore modifier keys alone
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        // Stop listening and timer
        _inputListeningTimer?.Stop();
        _inputListeningTimer = null;
        _isListeningForAnyInput = false;
        this.KeyDown -= OnKeyboardInputCaptured;
        GamepadManager.StopListening();

        // Check if this hotkey already exists
        if (HotkeysManager.CheckExists(key, modifiers))
        {
            MessageBox.Show("Keyboard hotkey already assigned!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            ResetInputListening();
            return;
        }

        // Set the hotkey
        _currentHotkeyBeingSet.UseGamepad = false;
        _currentHotkeyBeingSet.UseSteeringWheel = false;
        _currentHotkeyBeingSet.Key = key;
        _currentHotkeyBeingSet.Modifier = modifiers;
        _currentHotkeyBeingSet.GamepadButton = GamepadButton.None;
        _currentHotkeyBeingSet.Hotkey = new MahApps.Metro.Controls.HotKey(key, modifiers);

        // Update the UI
        if (UnifiedInputTextBox != null)
        {
            string modifierText = modifiers != ModifierKeys.None ? modifiers.ToString() + " + " : "";
            UnifiedInputTextBox.Text = $"Keyboard: {modifierText}{key}";
        }

        // Reset state
        _currentHotkeyBeingSet = null;
        if (set != null)
        {
            set.Content = "SET";
            set.IsEnabled = true;
        }
    }

    private void ResetInputListening()
    {
        _isListeningForAnyInput = false;
        GamepadManager.StopListening();
        this.KeyDown -= OnKeyboardInputCaptured;
        
        // Stop and dispose timer
        _inputListeningTimer?.Stop();
        _inputListeningTimer = null;
        
        if (set != null)
        {
            set.Content = "SET";
            set.IsEnabled = true;
        }
        
        if (UnifiedInputTextBox != null && _currentHotkeyBeingSet != null)
        {
            // Restore the original display based on current hotkey
            if (_currentHotkeyBeingSet.UseSteeringWheel)
            {
                UnifiedInputTextBox.Text = _currentHotkeyBeingSet.GamepadButton == GamepadButton.None 
                    ? "None" 
                    : $"Wheel: {_currentHotkeyBeingSet.GamepadButton}";
            }
            else if (_currentHotkeyBeingSet.UseGamepad)
            {
                UnifiedInputTextBox.Text = _currentHotkeyBeingSet.GamepadButton == GamepadButton.None 
                    ? "None" 
                    : $"Gamepad: {_currentHotkeyBeingSet.GamepadButton}";
            }
            else
            {
                if (_currentHotkeyBeingSet.Key == Key.None)
                {
                    UnifiedInputTextBox.Text = "None";
                }
                else
                {
                    string modifierText = _currentHotkeyBeingSet.Modifier != ModifierKeys.None ? _currentHotkeyBeingSet.Modifier.ToString() + " + " : "";
                    UnifiedInputTextBox.Text = $"Keyboard: {modifierText}{_currentHotkeyBeingSet.Key}";
                }
            }
        }
        
        _currentHotkeyBeingSet = null;
    }

    private void InitializeUnifiedInput()
    {
        // Initialize unified input display
        if (UnifiedInputTextBox != null)
        {
            UnifiedInputTextBox.Text = "None";
        }
    }

}