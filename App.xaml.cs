using System.Windows;
using System.Windows.Threading;
using ImportCanonPhotos.Core;

namespace ImportCanonPhotos;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The instance Mutex is released in OnExit; Application is not IDisposable.")]
internal sealed partial class App : Application
{
    // Local\ rather than Global\: a per-user tray app only needs one instance per session,
    // and creating Global\ objects needs a privilege standard users do not have.
    private const string InstanceMutexName = @"Local\ImportCanonPhotos.SingleInstance";

    private Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // A desktop shortcut plus the HKCU Run entry makes a second launch likely, and two
        // tray icons is a confusing result.
        _instanceMutex = new Mutex(true, InstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.Dispose();
        _instanceMutex = null;
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Unhandled exception on the UI thread.", e.Exception);

        MessageBox.Show(
            $"Something went wrong:{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}" +
            $"{Environment.NewLine}{Environment.NewLine}See Show Logs for details.",
            "Import Canon Photos",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // A tray app should not disappear because one operation threw.
        e.Handled = true;
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLog.Error("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }
}
