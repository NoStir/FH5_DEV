using System.Windows;
using MA_FH5Trainer.Cheats;
using MA_FH5Trainer.Models;
using MA_FH5Trainer.Resources;
using MA_FH5Trainer.Resources.Keybinds;
using MA_FH5Trainer.Services;
using MA_FH5Trainer.ViewModels.Windows;
using MA_FH5Trainer.Views.Windows;
using MahApps.Metro.Controls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static MA_FH5Trainer.Resources.Cheats;

namespace MA_FH5Trainer;

public partial class App
{
    private const string MutexName = "{(4A771E61-6684-449F-8952-B31582A8877E)}";
    private Mutex _mutex = null!;

    private static readonly IHost Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(c =>
        {
            c.SetBasePath(AppContext.BaseDirectory);
        }).
        ConfigureServices((_, services) =>
        {
            services.AddHostedService<ApplicationHostService>();
            services.AddSingleton<MetroWindow, MainWindow>();
        }).Build();
    
    public static T GetRequiredService<T>() where T : class
    {
        return Host.Services.GetRequiredService<T>();
    }
    
    private async void App_OnStartup(object sender, StartupEventArgs e)
    {
        await Host.StartAsync();
        //HotkeysManager.SetupSystemHook();
    }

    private async void App_OnExit(object sender, ExitEventArgs e)
    {
        //HotkeysManager.ShutdownSystemHook();
        DisconnectFromGame();
        
        await Host.StopAsync();
        Host.Dispose();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);

        if (createdNew)
        {
            base.OnStartup(e);
            SetupExceptionHandling();
        }
        else
        {
            MessageBox.Show("Another instance of the tool is already running.", "Information", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            Current.Shutdown();
        }
    }
    
    // https://stackoverflow.com/a/46804709
    private void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ReportException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

        DispatcherUnhandledException += (_, e) =>
        {
            ReportException(e.Exception, "Application.Current.DispatcherUnhandledException");
            e.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ReportException(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };
    }
    
    private static void ReportException(Exception exception, string source)
    {
        // Create a user-friendly error message
        string userMessage = GetUserFriendlyErrorMessage(exception);
        
        // Create technical details for developers
        string technicalDetails = $"\n\n=== Technical Details ===\n" +
                                 $"Source: {source}\n" +
                                 $"Exception: {exception.Message}\n" +
                                 $"Stack Trace: {exception.StackTrace}\n" +
                                 $"Tool Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}\n" +
                                 $"Game: {GameVerPlat.GetInstance().Name}\n" +
                                 $"Game Version: {GameVerPlat.GetInstance().Update}\n" +
                                 $"Platform: {GameVerPlat.GetInstance().Platform}";

        string fullMessage = $"{userMessage}\n\n" +
                           "Please copy this error (Ctrl+C) and report it on GitHub or Discord (discord.gg/rHzev9brJ3)\n" +
                           $"{technicalDetails}";

        MessageBox.Show(
            fullMessage,
            "FH5 Trainer - Unexpected Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
        
        Environment.Exit(1);
    }

    private static string GetUserFriendlyErrorMessage(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => "The trainer needs administrator privileges to access game memory. Please run as administrator.",
            System.ComponentModel.Win32Exception win32Ex when win32Ex.NativeErrorCode == 5 => "Access denied. Please run the trainer as administrator.",
            System.IO.FileNotFoundException => "A required file is missing. Please reinstall the trainer.",
            OutOfMemoryException => "The system is running low on memory. Please close other applications and try again.",
            InvalidOperationException => "The trainer encountered an invalid state. This may happen if the game process is not available.",
            _ => "An unexpected error occurred. The trainer will now close to prevent further issues."
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        finally
        {
            _mutex.Dispose();
        }
        
        base.OnExit(e);
    }

    private static void DisconnectFromGame()
    {
        foreach (var cheatInstance in g_CachedInstances.Where(kv => typeof(ICheatsBase).IsAssignableFrom(kv.Key)))
        {
            ((ICheatsBase)cheatInstance.Value).Cleanup();
        }
        _ = Imports.CloseHandle(MA_FH5Trainer.Resources.Memory.GetInstance().MProc.Handle);
    }
}