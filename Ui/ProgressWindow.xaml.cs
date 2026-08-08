using System.Windows;
using ImportCanonPhotos.Core;

namespace ImportCanonPhotos.Ui;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The CancellationTokenSource is disposed in OnClosed; Window is not IDisposable.")]
internal sealed partial class ProgressWindow : Window
{
    private readonly CancellationTokenSource _cancellation = new();
    private bool _finished;

    public ProgressWindow(string title)
    {
        InitializeComponent();

        Title = title;
        PhaseText.Text = title;

        // Constructed here, on the UI thread, so Progress<T> captures this
        // SynchronizationContext and posts every callback back to the Dispatcher.
        Progress = new Progress<ImportProgress>(Update);
    }

    public IProgress<ImportProgress> Progress { get; }

    public CancellationToken CancellationToken => _cancellation.Token;

    private void Update(ImportProgress progress)
    {
        PhaseText.Text = progress.Phase;
        CurrentFileText.Text = progress.CurrentFile;

        PhaseBar.Maximum = Math.Max(1, progress.PhaseTotal);
        PhaseBar.Value = Math.Min(progress.PhaseDone, PhaseBar.Maximum);

        OverallBar.Maximum = Math.Max(1, progress.OverallTotal);
        OverallBar.Value = Math.Min(progress.OverallDone, OverallBar.Maximum);

        OverallText.Text = $"Overall: {progress.OverallDone} of {progress.OverallTotal}";
    }

    /// <summary>Switches the window from "working" to "done", leaving the tallies on screen.</summary>
    public void ShowSummary(ImportResult result)
    {
        _finished = true;

        PhaseText.Text = result.Cancelled ? "Cancelled" : "Finished";
        CurrentFileText.Text = string.Empty;
        PhaseBar.Value = PhaseBar.Maximum;

        OverallText.Text =
            $"{result.Imported.Count} imported, {result.Skipped.Count} skipped, {result.Failed.Count} failed" +
            (result.DeletedFromSource > 0 ? $", {result.DeletedFromSource} deleted from source" : string.Empty);

        if (!result.Cancelled)
        {
            OverallBar.Value = OverallBar.Maximum;
        }

        CancelButton.Content = "Close";
        CancelButton.IsEnabled = true;
    }

    public void ShowFailure(string message)
    {
        _finished = true;
        PhaseText.Text = "Import failed";
        CurrentFileText.Text = message;
        CancelButton.Content = "Close";
        CancelButton.IsEnabled = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_finished)
        {
            Close();
            return;
        }

        _cancellation.Cancel();
        CancelButton.IsEnabled = false;
        CancelButton.Content = "Cancelling...";
        PhaseText.Text = "Cancelling, finishing the current file...";
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_finished)
        {
            _cancellation.Cancel();
        }

        _cancellation.Dispose();
        base.OnClosed(e);
    }
}
