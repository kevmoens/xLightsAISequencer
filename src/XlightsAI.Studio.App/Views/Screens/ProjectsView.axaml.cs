using Avalonia.Controls;
using Avalonia.Interactivity;
using XlightsAI.Studio.Shell;

namespace XlightsAI.Studio.App.Views.Screens;

public partial class ProjectsView : UserControl
{
    public ProjectsView() => InitializeComponent();

    private async void OnBrowseWorkspace(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        var picked = await PathPicker.PickFolderAsync(
            this,
            "Choose the project workspace folder",
            shell.Projects.WorkspaceDirectory);

        if (picked is not null)
        {
            shell.Projects.WorkspaceDirectory = picked;
        }
    }
}
