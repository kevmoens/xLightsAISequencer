using Avalonia.Controls;
using Avalonia.Interactivity;
using XlightsAI.Studio.Projects;
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

    /// <summary>
    /// User feedback (2026-11-01): "The Project should have a drop down for existing projects and have
    /// that load the workspace folder and project name." Picking a recent project fills both fields
    /// (<see cref="ProjectsViewModel.WorkspaceDirectory"/>/<see cref="ProjectsViewModel.NewProjectName"/>,
    /// the latter purely for display/consistency here -- opening only ever reads WorkspaceDirectory) and
    /// immediately re-opens it, the same "picking an already-known-good entry skips the extra click"
    /// convention <c>ShowImportView.OnRecentShowDirectorySelected</c> already establishes.
    /// </summary>
    private void OnRecentProjectSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell || sender is not ComboBox { SelectedItem: ProjectSummary project })
        {
            return;
        }

        shell.Projects.WorkspaceDirectory = project.RootDirectory;
        shell.Projects.NewProjectName = project.Name;
        if (shell.OpenProjectCommand.CanExecute(null))
        {
            shell.OpenProjectCommand.Execute(null);
        }
    }
}
