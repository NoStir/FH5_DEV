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
    private bool _isListeningForGamepadInput = false;
    private GlobalHotkey? _currentHotkeyBeingSet = null;
    private DispatcherTimer? _gamepadListeningTimer = null;

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
        InitializeGamepadButtons();
        
        // Subscribe to gamepad button events for listening mode
        GamepadManager.AnyButtonPressed += OnGamepadButtonCaptured;
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from gamepad events
        GamepadManager.AnyButtonPressed -= OnGamepadButtonCaptured;
        
        // Clean up any active timer
        _gamepadListeningTimer?.Stop();
        _gamepadListeningTimer = null;
        
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

        if (KeyboardInputRadio?.IsChecked == true)
        {
            // Handle keyboard input
            if (HotKeyBox?.HotKey == null)
            {
                MessageBox.Show("No hotkey selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (HotkeysManager.CheckExists(HotKeyBox.HotKey.Key, HotKeyBox.HotKey.ModifierKeys))
            {
                MessageBox.Show("Hotkey already exists!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            hotkey.UseGamepad = false;
            hotkey.Key = HotKeyBox.HotKey.Key;
            hotkey.Modifier = HotKeyBox.HotKey.ModifierKeys;
            hotkey.GamepadButton = GamepadButton.None;
            hotkey.Hotkey = HotKeyBox.HotKey;
        }
        else
        {
            // Handle gamepad input with listening mode
            if (_isListeningForGamepadInput)
            {
                // Cancel listening mode
                ResetGamepadListening();
                return;
            }

            // Start listening for gamepad input
            var connectedControllers = GamepadManager.GetConnectedControllers();
            if (connectedControllers.Length == 0)
            {
                MessageBox.Show("No gamepad connected. Please connect a gamepad and try again.", 
                    "No Gamepad", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _isListeningForGamepadInput = true;
            _currentHotkeyBeingSet = hotkey;
            
            if (GamepadButtonTextBox != null)
            {
                GamepadButtonTextBox.Text = "Listening... Press any gamepad button";
            }
            
            if (set != null)
            {
                set.Content = "CANCEL";
            }
            
            GamepadManager.StartListening();

            // Set up timeout timer (10 seconds)
            _gamepadListeningTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _gamepadListeningTimer.Tick += (s, args) =>
            {
                ResetGamepadListening();
                MessageBox.Show("Gamepad input timeout. Please try again.", 
                    "Timeout", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            _gamepadListeningTimer.Start();
        }
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

        if (hotkey.UseGamepad)
        {
            // Set gamepad mode
            if (GamepadInputRadio != null)
            {
                GamepadInputRadio.IsChecked = true;
            }
            if (KeyboardInputBorder != null)
            {
                KeyboardInputBorder.Visibility = Visibility.Collapsed;
            }
            if (GamepadInputBorder != null)
            {
                GamepadInputBorder.Visibility = Visibility.Visible;
            }
            if (GamepadButtonTextBox != null)
            {
                GamepadButtonTextBox.Text = hotkey.GamepadButton == GamepadButton.None 
                    ? "None" 
                    : hotkey.GamepadButton.ToString();
            }
        }
        else
        {
            // Set keyboard mode
            if (KeyboardInputRadio != null)
            {
                KeyboardInputRadio.IsChecked = true;
            }
            if (KeyboardInputBorder != null)
            {
                KeyboardInputBorder.Visibility = Visibility.Visible;
            }
            if (GamepadInputBorder != null)
            {
                GamepadInputBorder.Visibility = Visibility.Collapsed;
            }
            if (HotKeyBox != null)
            {
                HotKeyBox.HotKey = hotkey.Hotkey;
            }
        }
    }

    private void OnGamepadButtonCaptured(int controllerIndex, GamepadButton button)
    {
        if (!_isListeningForGamepadInput || _currentHotkeyBeingSet == null)
            return;

        // Run on UI thread
        Dispatcher.Invoke(() =>
        {
            // Stop listening and timer
            GamepadManager.StopListening();
            _gamepadListeningTimer?.Stop();
            _gamepadListeningTimer = null;
            _isListeningForGamepadInput = false;

            // Check if this button is already assigned
            if (HotkeysManager.CheckExists(button))
            {
                MessageBox.Show("Gamepad button already assigned!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                ResetGamepadListening();
                return;
            }

            // Set the hotkey
            _currentHotkeyBeingSet.UseGamepad = true;
            _currentHotkeyBeingSet.GamepadButton = button;
            _currentHotkeyBeingSet.Key = Key.None;
            _currentHotkeyBeingSet.Modifier = ModifierKeys.None;
            _currentHotkeyBeingSet.Hotkey = new MahApps.Metro.Controls.HotKey(Key.None);

            // Update the UI
            if (GamepadButtonTextBox != null)
            {
                GamepadButtonTextBox.Text = button.ToString();
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

    private void ResetGamepadListening()
    {
        _isListeningForGamepadInput = false;
        GamepadManager.StopListening();
        
        // Stop and dispose timer
        _gamepadListeningTimer?.Stop();
        _gamepadListeningTimer = null;
        
        if (set != null)
        {
            set.Content = "SET";
            set.IsEnabled = true;
        }
        
        if (GamepadButtonTextBox != null && _currentHotkeyBeingSet != null)
        {
            GamepadButtonTextBox.Text = _currentHotkeyBeingSet.GamepadButton == GamepadButton.None 
                ? "None" 
                : _currentHotkeyBeingSet.GamepadButton.ToString();
        }
        
        _currentHotkeyBeingSet = null;
    }

    private void InitializeGamepadButtons()
    {
        // Initialize gamepad button display
        if (GamepadButtonTextBox != null)
        {
            GamepadButtonTextBox.Text = "None";
        }
    }

    private void InputType_Changed(object sender, RoutedEventArgs e)
    {
        if (KeyboardInputRadio?.IsChecked == true)
        {
            if (KeyboardInputBorder != null)
            {
                KeyboardInputBorder.Visibility = Visibility.Visible;
            }
            if (GamepadInputBorder != null)
            {
                GamepadInputBorder.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            if (KeyboardInputBorder != null)
            {
                KeyboardInputBorder.Visibility = Visibility.Collapsed;
            }
            if (GamepadInputBorder != null)
            {
                GamepadInputBorder.Visibility = Visibility.Visible;
            }
        }
    }
}