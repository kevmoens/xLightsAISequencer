using Avalonia.Controls;
using Avalonia.Interactivity;
using XlightsAI.Studio.Shell;

namespace XlightsAI.Studio.App.Views.Screens;

public partial class ShowImportView : UserControl
{
    public ShowImportView() => InitializeComponent();

    private async void OnBrowseShowFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        var picked = await PathPicker.PickFolderAsync(
            this,
            "Choose your xLights show folder",
            shell.ShowImport.ShowDirectory);

        if (picked is not null)
        {
            shell.ShowImport.ShowDirectory = picked;
        }
    }

    /// <summary>
    /// User feedback (2026-11-01): "when opening an existing show, can that be a drop down and select
    /// existing and it sets the directory too." Picking a recent show directory both fills the text field
    /// (via the same <see cref="ShowImportViewModel.ShowDirectory"/> property <see cref="OnBrowseShowFolder"/>
    /// sets) and immediately re-runs discovery, rather than requiring a second "Read show folder" click --
    /// picking an *already-known-good* folder is the one case where skipping that extra click is safe (it
    /// already succeeded once to be on this list at all).
    /// </summary>
    private void OnRecentShowDirectorySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell || sender is not ComboBox { SelectedItem: string directory })
        {
            return;
        }

        shell.ShowImport.ShowDirectory = directory;
        if (shell.DiscoverShowDirectoryCommand.CanExecute(null))
        {
            shell.DiscoverShowDirectoryCommand.Execute(null);
        }
    }
}
