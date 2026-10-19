using Avalonia.Controls;
using XlightsAI.Studio.Shell;

namespace XlightsAI.Studio.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(ShellViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
