using Avalonia.Controls;
using Avalonia.Interactivity;
using XlightsAI.Studio.Shell;

namespace XlightsAI.Studio.App.Views.Screens;

public partial class GenerateExportView : UserControl
{
    public GenerateExportView() => InitializeComponent();

    private async void OnBrowseXsqOutput(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        var picked = await PathPicker.PickSaveFileAsync(
            this,
            "Where should the sequence be written?",
            "xsq",
            [PathPicker.SequenceFiles],
            shell.ShowImport.XsqOutputPath);

        if (picked is not null)
        {
            shell.ShowImport.XsqOutputPath = picked;
        }
    }

    private async void OnBrowseXlightsExecutable(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        var picked = await PathPicker.PickFileAsync(
            this,
            "Locate the installed xLights program",
            [PathPicker.Executables, PathPicker.AllFiles],
            shell.ShowImport.XlightsExecutablePath);

        if (picked is not null)
        {
            shell.ShowImport.XlightsExecutablePath = picked;
        }
    }
}
