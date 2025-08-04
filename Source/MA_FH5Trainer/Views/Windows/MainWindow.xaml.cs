using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using MA_FH5Trainer.Resources.Keybinds;
using MA_FH5Trainer.Resources.Theme;
using MA_FH5Trainer.ViewModels.Windows;

namespace MA_FH5Trainer.Views.Windows;

public partial class MainWindow
{
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
    }
    
    protected override void OnClosed(EventArgs e)
    {
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
            // Handle gamepad input
            if (GamepadButtonComboBox?.SelectedItem is not GamepadButton gamepadButton)
            {
                MessageBox.Show("No gamepad button selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (HotkeysManager.CheckExists(gamepadButton))
            {
                MessageBox.Show("Gamepad button already assigned!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            hotkey.UseGamepad = true;
            hotkey.GamepadButton = gamepadButton;
            hotkey.Key = Key.None;
            hotkey.Modifier = ModifierKeys.None;
            hotkey.Hotkey = new MahApps.Metro.Controls.HotKey(Key.None);
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
            if (GamepadButtonComboBox != null)
            {
                GamepadButtonComboBox.SelectedItem = hotkey.GamepadButton;
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

    private void InitializeGamepadButtons()
    {
        if (GamepadButtonComboBox == null)
        {
            return;
        }

        // Populate gamepad button ComboBox
        var gamepadButtons = Enum.GetValues<GamepadButton>()
            .Where(b => b != GamepadButton.None)
            .ToArray();
        
        GamepadButtonComboBox.ItemsSource = gamepadButtons;
        GamepadButtonComboBox.SelectedIndex = 0;
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