using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace XlightsAI.Studio.App.Views;

/// <summary>
/// Native folder/file pickers for the shell's path fields.
/// <para>
/// Every path the app needed — the project workspace, the xLights show folder, the song, the XSQ
/// output, the installed <c>xLights.exe</c> — was a bare <see cref="TextBox"/> the user had to
/// type an absolute path into from memory, in a desktop application whose entire job is pointing
/// at files on that desktop. These helpers are deliberately view-layer only: they set the same
/// bindable view-model string the text box already writes, so the typed path stays a perfectly
/// valid way in (scripted setups, pasted paths) and no view model learns about dialogs.
/// </para>
/// <para>
/// Each returns <see langword="null"/> when the user cancels or when no storage provider is
/// available (a headless/design-time host), and callers leave the current value untouched in that
/// case — cancelling a picker must never blank a path the user already had.
/// </para>
/// </summary>
internal static class PathPicker
{
    public static async Task<string?> PickFolderAsync(Visual owner, string title, string? startAt = null)
    {
        var provider = TopLevel.GetTopLevel(owner)?.StorageProvider;
        if (provider is null || !provider.CanPickFolder)
        {
            return null;
        }

        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await TryResolveFolderAsync(provider, startAt).ConfigureAwait(true),
        }).ConfigureAwait(true);

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public static async Task<string?> PickFileAsync(
        Visual owner,
        string title,
        IReadOnlyList<FilePickerFileType> fileTypes,
        string? startAt = null)
    {
        var provider = TopLevel.GetTopLevel(owner)?.StorageProvider;
        if (provider is null || !provider.CanOpen)
        {
            return null;
        }

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [.. fileTypes],
            SuggestedStartLocation = await TryResolveFolderAsync(provider, ParentOf(startAt)).ConfigureAwait(true),
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public static async Task<string?> PickSaveFileAsync(
        Visual owner,
        string title,
        string defaultExtension,
        IReadOnlyList<FilePickerFileType> fileTypes,
        string? startAt = null)
    {
        var provider = TopLevel.GetTopLevel(owner)?.StorageProvider;
        if (provider is null || !provider.CanSave)
        {
            return null;
        }

        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = defaultExtension,
            // The export screen owns collision handling explicitly (its own overwrite/backup/fail
            // policy), so the OS prompt would be a second, conflicting answer to the same question.
            ShowOverwritePrompt = false,
            SuggestedFileName = NameOf(startAt),
            FileTypeChoices = [.. fileTypes],
            SuggestedStartLocation = await TryResolveFolderAsync(provider, ParentOf(startAt)).ConfigureAwait(true),
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    public static FilePickerFileType AudioFiles { get; } = new("Audio files")
    {
        Patterns = ["*.wav", "*.mp3", "*.flac", "*.m4a", "*.ogg", "*.aac"],
    };

    public static FilePickerFileType SequenceFiles { get; } = new("xLights sequence")
    {
        Patterns = ["*.xsq"],
    };

    public static FilePickerFileType Executables { get; } = OperatingSystem.IsWindows()
        ? new FilePickerFileType("Programs") { Patterns = ["*.exe"] }
        : new FilePickerFileType("Programs") { Patterns = ["*"] };

    public static FilePickerFileType AllFiles { get; } = new("All files") { Patterns = ["*"] };

    /// <summary>
    /// Opens the picker where the user's current value points, so re-picking a path lands in the
    /// right place instead of the OS default. A value that no longer exists is simply ignored.
    /// </summary>
    private static async Task<IStorageFolder?> TryResolveFolderAsync(IStorageProvider provider, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Directory.Exists(path)
                ? await provider.TryGetFolderFromPathAsync(path).ConfigureAwait(true)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static string? ParentOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetDirectoryName(path);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? NameOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFileName(path);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
