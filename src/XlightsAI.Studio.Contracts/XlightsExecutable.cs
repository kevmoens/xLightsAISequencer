using System.Diagnostics;
using System.Text.RegularExpressions;

namespace XlightsAI.Studio.Contracts;

/// <summary>
/// Resolves what a user-configured "xLights path" actually points at. On macOS xLights is an
/// <c>.app</c> bundle (a directory), so a path to <c>xLights.app</c> is resolved to the executable inside
/// <c>Contents/MacOS</c>; any other path is returned unchanged. Also reads the version from a bundle's
/// <c>Info.plist</c>, since <see cref="FileVersionInfo"/> only understands Windows executables.
/// </summary>
public static class XlightsExecutable
{
    private static readonly Regex BundleVersionPattern = new(
        @"<key>CFBundleShortVersionString</key>\s*<string>([^<]+)</string>", RegexOptions.Compiled);

    public static IReadOnlyList<string> MacOsBundleCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var bundles = new List<string> { "/Applications/xLights.app" };
        if (!string.IsNullOrWhiteSpace(home))
        {
            bundles.Add(Path.Combine(home, "Applications", "xLights.app"));
        }

        return bundles;
    }

    public static string? Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!trimmed.EndsWith(".app", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(trimmed))
        {
            return path;
        }

        var macOsDirectory = Path.Combine(trimmed, "Contents", "MacOS");
        if (!Directory.Exists(macOsDirectory))
        {
            return path;
        }

        var conventional = Path.Combine(macOsDirectory, Path.GetFileNameWithoutExtension(trimmed));
        if (File.Exists(conventional))
        {
            return conventional;
        }

        return Directory.EnumerateFiles(macOsDirectory).FirstOrDefault() ?? path;
    }

    public static string? TryReadVersion(string executablePath)
    {
        // <bundle>.app/Contents/MacOS/<exe> -> <bundle>.app/Contents/Info.plist
        var plistPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(executablePath)) ?? string.Empty, "Info.plist");
        if (File.Exists(plistPath))
        {
            try
            {
                var match = BundleVersionPattern.Match(File.ReadAllText(plistPath));
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        try
        {
            return FileVersionInfo.GetVersionInfo(executablePath).FileVersion;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
