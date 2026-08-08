using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using ImportCanonPhotos.Core;
using ImportCanonPhotos.Ui;

namespace ImportCanonPhotos;

internal sealed partial class MainWindow : Window
{
    private AppSettings _settings;
    private PreferencesWindow? _preferencesWindow;
    private int _importRunning;

    public MainWindow()
    {
        InitializeComponent();

        Directory.CreateDirectory(AppPaths.RootFolder);
        _settings = SettingsStore.Load();
        AppLog.Info("Import Canon Photos started.");
    }

    // ------------------------------------------------------------------ Import

    private async void ImportFromCamera_Click(object sender, RoutedEventArgs e) =>
        await RunImportAsync(
            "Importing from camera",
            (engine, token) => engine.ImportFromCameraAsync(token));

    private async void ImportFromDropbox_Click(object sender, RoutedEventArgs e) =>
        await RunImportAsync(
            "Importing from Dropbox",
            (engine, token) => engine.ImportFromDropboxAsync(token));

    private async Task RunImportAsync(
        string title,
        Func<ImportEngine, CancellationToken, Task<ImportResult>> operation)
    {
        // Interlocked rather than a bool: the menu items are only cosmetic reinforcement.
        if (Interlocked.CompareExchange(ref _importRunning, 1, 0) != 0)
        {
            MessageBox.Show(
                "An import is already running.",
                "Import Canon Photos",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SetImportMenusEnabled(false);
        TrayIcon.ToolTipText = $"Import Canon Photos - {title}";

        var window = new ProgressWindow(title);
        window.Show();
        window.Activate();

        try
        {
            var engine = new ImportEngine(_settings, window.Progress);

            // Keep the tray message pump free: the engine does synchronous file IO.
            var result = await Task.Run(
                () => operation(engine, window.CancellationToken),
                window.CancellationToken);

            window.ShowSummary(result);
        }
        catch (OperationCanceledException)
        {
            AppLog.Info($"{title} cancelled by the user.");
            window.ShowSummary(new ImportResult { Cancelled = true });
        }
        catch (ImportException ex)
        {
            AppLog.Error($"{title} could not start.", ex);
            window.ShowFailure(ex.Message);
            MessageBox.Show(ex.Message, "Import Canon Photos", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            AppLog.Error($"{title} failed.", ex);
            window.ShowFailure($"{ex.Message}{Environment.NewLine}{Environment.NewLine}See the log for details.");
            MessageBox.Show(
                $"The import failed:{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "Import Canon Photos",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            TrayIcon.ToolTipText = "Import Canon Photos";
            SetImportMenusEnabled(true);
            Interlocked.Exchange(ref _importRunning, 0);
        }
    }

    private void SetImportMenusEnabled(bool enabled)
    {
        ImportCameraMenuItem.IsEnabled = enabled;
        ImportDropboxMenuItem.IsEnabled = enabled;
    }

    // ----------------------------------------------------------------- Menu

    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        if (_preferencesWindow is not null)
        {
            _preferencesWindow.Activate();
            return;
        }

        // Deliberately no Owner: MainWindow is a zero-size, fully transparent window and
        // owning a dialog from it can place the dialog off-screen.
        _preferencesWindow = new PreferencesWindow(_settings);

        try
        {
            if (_preferencesWindow.ShowDialog() == true && _preferencesWindow.Result is not null)
            {
                _settings = _preferencesWindow.Result;
            }
        }
        finally
        {
            _preferencesWindow = null;
        }
    }

    private void ShowLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(AppLog.LogFilePath))
            {
                AppLog.Info("Log opened before anything else was written.");
            }

            // UseShellExecute must be true: it defaults to false on .NET, and a bare
            // path with it false throws Win32Exception.
            Process.Start(new ProcessStartInfo(AppLog.LogFilePath) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not open the log file.", ex);
            MessageBox.Show(
                $"Could not open the log:{Environment.NewLine}{AppLog.LogFilePath}",
                "Import Canon Photos",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";
        Dispatcher.BeginInvoke(() =>
            MessageBox.Show(
                $"Import Canon Photos v{version}\n\nImports photos from a Canon camera card and from Dropbox Camera Uploads, converting CR3 to DNG.",
                "About Import Canon Photos",
                MessageBoxButton.OK,
                MessageBoxImage.Information));
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        AppLog.Info("Import Canon Photos exiting.");
        Application.Current.Shutdown();
    }

    private void TrayIcon_LeftClick(object sender, RoutedEventArgs e)
    {
        if (TrayIcon?.ContextMenu != null)
        {
            TrayIcon.ContextMenu.IsOpen = true;
        }
    }
}
