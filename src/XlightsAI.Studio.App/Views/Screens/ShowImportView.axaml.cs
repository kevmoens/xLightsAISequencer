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
}
