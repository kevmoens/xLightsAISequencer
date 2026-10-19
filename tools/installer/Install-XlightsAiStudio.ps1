<#
.SYNOPSIS
    PHASE-15 Step 095 ("Build installer/uninstaller..."). Installs an already-published
    self-contained win-x64 bundle (the real `dotnet publish -r win-x64 --self-contained true`
    output PHASE-15 Step 094 already proved produces a working app) into a per-user, non-admin
    location. Never requires an elevated prompt and never touches the user's own workspace/
    project folder (that is chosen separately, during first run -- see FirstRunViewModel) or any
    settings/diagnostics the app has already written -- this script only ever owns the files it
    itself copies, recorded in its own uninstall manifest (see Uninstall-XlightsAiStudio.ps1).

.PARAMETER PublishDir
    The real self-contained publish output directory (e.g. the folder `dotnet publish -r win-x64
    --self-contained true -o <PublishDir>` wrote).

.PARAMETER InstallRoot
    Destination root. Defaults to a per-user, non-admin location under %LOCALAPPDATA% -- Section
    1/implementation guidance: "no environment variable/Python setup or admin-only project
    storage" applied here to the *install location itself*, not only the workspace folder.

.PARAMETER CreateShortcut
    Also create a Start Menu shortcut. Best-effort / non-fatal: a shortcut failing to create (e.g.
    a non-interactive CI/test environment) never fails the install itself.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'XlightsAI.Studio'),

    [switch]$CreateShortcut
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PublishDir -PathType Container)) {
    throw "PublishDir '$PublishDir' does not exist or is not a directory. Publish the app first (see implementation-evidence/PHASE-15/STEP-094.md for the exact command)."
}

$appDir = Join-Path $InstallRoot 'app'
New-Item -ItemType Directory -Force -Path $appDir | Out-Null

# Real copy of the real publish output -- never a simulated/partial install.
Copy-Item -Path (Join-Path $PublishDir '*') -Destination $appDir -Recurse -Force

# Record exactly which files this installer owns, so Uninstall-XlightsAiStudio.ps1 never deletes
# anything it did not itself put there (Section 1: "Uninstall leaves user projects intact unless
# expressly asked" -- same "never a silent overwrite/delete of something we didn't create"
# convention the rest of this project already follows, e.g. XsqExporter's export-collision policy).
$ownedFiles = Get-ChildItem -LiteralPath $appDir -Recurse -File | ForEach-Object { $_.FullName }
$manifest = [ordered]@{
    InstalledAtUtc = (Get-Date -AsUTC).ToString('o')
    InstallRoot    = $InstallRoot
    AppDirectory   = $appDir
    OwnedFileCount = $ownedFiles.Count
    OwnedFiles     = $ownedFiles
}
$manifestPath = Join-Path $InstallRoot 'uninstall-manifest.json'
($manifest | ConvertTo-Json -Depth 4) | Set-Content -LiteralPath $manifestPath -Encoding utf8

$exePath = Join-Path $appDir 'XlightsAI.Studio.exe'

if ($CreateShortcut) {
    try {
        $startMenuDir = [Environment]::GetFolderPath('StartMenu')
        $shortcutPath = Join-Path $startMenuDir 'xLights AI Choreography Studio.lnk'
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $exePath
        $shortcut.WorkingDirectory = $appDir
        $shortcut.Save()
        Write-Host "Shortcut created at '$shortcutPath'."
    }
    catch {
        # Non-fatal, per-the-script's own disclosed scope: a shortcut is a convenience, not a
        # correctness requirement -- the app still launches directly from $exePath without one.
        Write-Warning "Could not create a Start Menu shortcut: $($_.Exception.Message)"
    }
}

Write-Host "Installed to '$appDir' ($($ownedFiles.Count) files). Manifest: '$manifestPath'."
Write-Host "Executable: '$exePath'."
