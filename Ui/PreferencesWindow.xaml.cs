using System.IO;
using System.Windows;
using ImportCanonPhotos.Core;
using Microsoft.Win32;

namespace ImportCanonPhotos.Ui;

internal sealed partial class PreferencesWindow : Window
{
    private readonly AppSettings _settings;

    public PreferencesWindow(AppSettings settings)
    {
        InitializeComponent();

        // Work on a copy so Cancel really discards.
        _settings = settings.Clone();
        Load(_settings);
    }

    /// <summary>Non-null once the user has saved.</summary>
    public AppSettings? Result { get; private set; }

    private void Load(AppSettings settings)
    {
        CameraFolderBox.Text = settings.CameraDcimFolder;
        DropboxFolderBox.Text = settings.DropboxFolder;
        OutputFolderBox.Text = settings.OutputFolder;
        ConverterPathBox.Text = settings.AdobeDngConverterPath;

        LosslessCheck.IsChecked = settings.ImportLossless;
        ReEncodeCheck.IsChecked = settings.ReEncodeVideos;
        OverwriteCheck.IsChecked = settings.OverwriteExisting;
        DeleteSourceCheck.IsChecked = settings.DeleteSourceAfterImport;
    }

    private void BrowseCamera_Click(object sender, RoutedEventArgs e) =>
        BrowseForFolder(CameraFolderBox, "Select the camera's DCIM folder");

    private void BrowseDropbox_Click(object sender, RoutedEventArgs e) =>
        BrowseForFolder(DropboxFolderBox, "Select the Dropbox Camera Uploads folder");

    private void BrowseOutput_Click(object sender, RoutedEventArgs e) =>
        BrowseForFolder(OutputFolderBox, "Select the photo library folder");

    private static void BrowseForFolder(System.Windows.Controls.TextBox target, string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = SafeDirectory(AppSettings.Expand(target.Text)),
        };

        if (dialog.ShowDialog() == true)
        {
            target.Text = dialog.FolderName;
        }
    }

    private void BrowseConverter_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Locate Adobe DNG Converter",
            Filter = "Adobe DNG Converter|Adobe DNG Converter.exe|Executables (*.exe)|*.exe|All files (*.*)|*.*",
            InitialDirectory = SafeDirectory(Path.GetDirectoryName(AppSettings.Expand(ConverterPathBox.Text)) ?? string.Empty),
        };

        if (dialog.ShowDialog() == true)
        {
            ConverterPathBox.Text = dialog.FileName;
        }
    }

    private static string SafeDirectory(string candidate) =>
        !string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate) ? candidate : string.Empty;

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        Load(new AppSettings());
        StatusText.Text = "Defaults restored. Choose Save to keep them.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.CameraDcimFolder = CameraFolderBox.Text.Trim();
        _settings.DropboxFolder = DropboxFolderBox.Text.Trim();
        _settings.OutputFolder = OutputFolderBox.Text.Trim();
        _settings.AdobeDngConverterPath = ConverterPathBox.Text.Trim();

        _settings.ImportLossless = LosslessCheck.IsChecked == true;
        _settings.ReEncodeVideos = ReEncodeCheck.IsChecked == true;
        _settings.OverwriteExisting = OverwriteCheck.IsChecked == true;
        _settings.DeleteSourceAfterImport = DeleteSourceCheck.IsChecked == true;

        if (string.IsNullOrWhiteSpace(_settings.OutputFolder))
        {
            StatusText.Text = "An output folder is required.";
            return;
        }

        SettingsStore.Save(_settings);
        AppLog.Info("Preferences saved.");
        Result = _settings;

        // Assigning DialogResult closes a dialog; calling Close() as well is redundant.
        DialogResult = true;
    }
}
