using Avalonia.Controls;
using Avalonia.Interactivity;
using XlightsAI.Studio.Shell;

namespace XlightsAI.Studio.App.Views.Screens;

public partial class FirstRunView : UserControl
{
    public FirstRunView() => InitializeComponent();

    private async void OnBrowseWorkspace(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        var picked = await PathPicker.PickFolderAsync(
            this,
            "Choose a workspace folder for your projects",
            shell.FirstRun.WorkspaceDirectory);

        if (picked is not null)
        {
            shell.FirstRun.WorkspaceDirectory = picked;
        }
    }
}
