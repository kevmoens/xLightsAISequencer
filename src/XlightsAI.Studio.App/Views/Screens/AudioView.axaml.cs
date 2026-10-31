using Avalonia.Controls;
using Avalonia.Interactivity;
using XlightsAI.Studio.Shell;

namespace XlightsAI.Studio.App.Views.Screens;

public partial class AudioView : UserControl
{
    public AudioView() => InitializeComponent();

    private async void OnBrowseAudioFile(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        var picked = await PathPicker.PickFileAsync(
            this,
            "Choose the song for this sequence",
            [PathPicker.AudioFiles, PathPicker.AllFiles],
            shell.Audio.AudioFilePath);

        if (picked is not null)
        {
            shell.Audio.AudioFilePath = picked;
        }
    }
}
