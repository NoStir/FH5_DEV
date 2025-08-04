using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ControlzEx.Theming;
using static System.Windows.Media.ColorConverter;

namespace MA_FH5Trainer.Resources.Theme;

public sealed class Theming : INotifyPropertyChanged
{
    private static readonly object s_lock = new object();
    private static Theming? _instance;
    public static Theming GetInstance()
    {
        lock (s_lock)
        {
            if (_instance != null)
            {
                return _instance;
            }
            
            _instance = new Theming();
            return _instance;
        }
    }
    
    private Brush _lighterColour = new SolidColorBrush((Color)ConvertFromString("#2C2C2C"));
    public Brush LighterColour
    {
        get => _lighterColour;
        private set => SetField(ref _lighterColour, value);
    }
    
    private Brush _lightColour = new SolidColorBrush((Color)ConvertFromString("#242424"));
    public Brush LightColour
    {
        get => _lightColour;
        private set => SetField(ref _lightColour, value);
    }
    
    private Brush _mainColour = new SolidColorBrush((Color)ConvertFromString("#1C1C1C"));
    public Brush MainColour
    {
        get => _mainColour;
        private set => SetField(ref _mainColour, value);
    }
    
    private Brush _darkishColour = new SolidColorBrush((Color)ConvertFromString("#121212"));
    public Brush DarkishColour
    {
        get => _darkishColour;
        private set => SetField(ref _darkishColour, value);
    }
    
    private Brush _darkColour = new SolidColorBrush((Color)ConvertFromString("#0D0D0D"));
    public Brush DarkColour
    {
        get => _darkColour;
        private set => SetField(ref _darkColour, value);
    }
    
    private Brush _darkerColour = new SolidColorBrush((Color)ConvertFromString("#080808"));
    public Brush DarkerColour
    {
        get => _darkerColour;
        private set => SetField(ref _darkerColour, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }
}