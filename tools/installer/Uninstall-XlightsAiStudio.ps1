<#
.SYNOPSIS
    PHASE-15 Step 095 ("Build installer/uninstaller..."). Removes exactly the files
    Install-XlightsAiStudio.ps1 itself installed (per its own uninstall-manifest.json), and nothing
    else. Never deletes a user's workspace/project folder, settings, or diagnostics logs -- those
    all live under separate, independently-chosen paths (the user's own workspace directory from
    first run; %APPDATA%/local settings paths the app itself manages) that this script never
    touches. Required acceptance (phase test matrix row 6): "Uninstall leaves user projects intact
    unless expressly asked."

.PARAMETER InstallRoot
    Must match the InstallRoot passed to Install-XlightsAiStudio.ps1.

.PARAMETER RemoveShortcut
    Also remove the Start Menu shortcut, if one was created.
#>
[CmdletBinding()]
param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'XlightsAI.Studio'),

    [switch]$RemoveShortcut
)

$ErrorActionPreference = 'Stop'

$manifestPath = Join-Path $InstallRoot 'uninstall-manifest.json'

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    Write-Warning "No uninstall manifest found at '$manifestPath'. Nothing to do -- refusing to guess what to delete (never deletes a path it did not itself record installing)."
    return
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

$removed = 0
$missing = 0
foreach ($file in $manifest.OwnedFiles) {
    if (Test-Path -LiteralPath $file -PathType Leaf) {
        Remove-Item -LiteralPath $file -Force
        $removed++
    }
    else {
        # Already gone (e.g. a prior partial uninstall) -- not an error, just disclosed.
        $missing++
    }
}

# Only remove directories this installer created, and only once empty of anything else --
# a user-added file dropped into the app directory (however unlikely) is left alone rather than
# silently swept away along with it.
if (Test-Path -LiteralPath $manifest.AppDirectory) {
    Get-ChildItem -LiteralPath $manifest.AppDirectory -Recurse -Directory |
        Sort-Object { $_.FullName.Length } -Descending |
        ForEach-Object {
            if (-not (Get-ChildItem -LiteralPath $_.FullName -Force | Select-Object -First 1)) {
                Remove-Item -LiteralPath $_.FullName -Force
            }
        }
    if (-not (Get-ChildItem -LiteralPath $manifest.AppDirectory -Force | Select-Object -First 1)) {
        Remove-Item -LiteralPath $manifest.AppDirectory -Force
    }
}

Remove-Item -LiteralPath $manifestPath -Force

if (Test-Path -LiteralPath $InstallRoot) {
    if (-not (Get-ChildItem -LiteralPath $InstallRoot -Force | Select-Object -First 1)) {
        Remove-Item -LiteralPath $InstallRoot -Force
    }
}

if ($RemoveShortcut) {
    $startMenuDir = [Environment]::GetFolderPath('StartMenu')
    $shortcutPath = Join-Path $startMenuDir 'xLights AI Choreography Studio.lnk'
    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }
}

Write-Host "Uninstalled: removed $removed file(s) ($missing already absent). User workspace/projects/settings were never touched by this script."
