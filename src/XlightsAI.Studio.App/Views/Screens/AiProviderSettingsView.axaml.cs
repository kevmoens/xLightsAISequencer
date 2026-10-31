using Avalonia.Controls;
using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Director;
using XlightsAI.Studio.Shell;

namespace XlightsAI.Studio.App.Views.Screens;

/// <summary>
/// Build-plan Phase 5: the Settings screen's "connect an AI provider" editor. Deliberately
/// self-contained (constructs its own view model over the real local stores) rather than threaded
/// through <see cref="ShellViewModel"/>'s own already-large constructor -- the same scoping choice
/// several other screen editors in this project already make (e.g. <c>VisualBudgetEditorViewModel</c>,
/// <c>RoleRoutingEditorViewModel</c>), which exist and are independently testable without yet being
/// wired into the root shell composition.
/// </summary>
public partial class AiProviderSettingsView : UserControl
{
    public AiProviderSettingsView()
    {
        InitializeComponent();

        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XlightsAI.Studio");

        var configStore = new JsonRoleProviderConfigStore(Path.Combine(appDataDirectory, "ai-providers"));
        var credentialStore = new DpapiCredentialStore(Path.Combine(appDataDirectory, "credentials"));

        var viewModel = new AiProviderSettingsEditorViewModel(configStore, credentialStore);
        DataContext = viewModel;
        _ = viewModel.LoadAsync();
    }
}
