[CmdletBinding()]
param(
    # The version, e.g. v1.1.0 or v1.1.0-beta.2. An existing tag is reused;
    # with -Message a new annotated tag is created from the current commit.
    [Parameter(Mandatory = $true)][string]$Tag,
    [string]$Message,
    [ValidateSet('Windows', 'WebGL')][string]$Target = 'Windows',
    # A Unity project folder reused across releases so its Library stays warm
    # (the first release imports everything and takes longer).
    [string]$Workspace,
    [string]$UnityEditor,
    [switch]$SkipBuild,
    [switch]$NoPush,
    [switch]$NoPublish
)

# One command per version: tag -> export exactly that tree -> build it with
# the version stamped in -> ZIP + SHA-256 -> push the tag -> GitHub Release.
# Every released version therefore stays downloadable from
# https://github.com/paekinator/configuratorNS/releases, source and player.

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
Set-Location -LiteralPath $repo
if ($Tag -notmatch '^v\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$') {
    throw "Tag must look like v1.2.3 or v1.2.3-beta.1 (got '$Tag')."
}

# --- 1. The tag is the version -------------------------------------------
$existing = git tag -l $Tag
if (-not $existing) {
    if (-not $Message) { throw "Tag $Tag does not exist. Pass -Message to create it from the current commit." }
    if (git status --porcelain) { throw 'Commit or stash your changes first: a version must be an exact commit.' }
    git tag -a $Tag -m $Message
    if ($LASTEXITCODE -ne 0) { throw 'git tag failed.' }
    Write-Output "Tagged $(git rev-parse --short HEAD) as $Tag"
}
$commit = git rev-parse "$Tag^{commit}"

# --- 2. Export exactly the tagged tree into the build workspace ----------
if (-not $Workspace) { $Workspace = Join-Path $repo 'tmp\release-workspace' }
$export = Join-Path $repo "tmp\release-export-$Tag"
if (Test-Path -LiteralPath $export) { Remove-Item -LiteralPath $export -Recurse -Force }
New-Item -ItemType Directory -Force -Path $export | Out-Null
$archive = "$export.zip"
git archive --format=zip -o $archive $Tag
if ($LASTEXITCODE -ne 0) { throw 'git archive failed.' }
Expand-Archive -Path $archive -DestinationPath $export -Force
Remove-Item -LiteralPath $archive
New-Item -ItemType Directory -Force -Path $Workspace | Out-Null
foreach ($d in @('Assets', 'Packages', 'ProjectSettings')) {
    robocopy (Join-Path $export $d) (Join-Path $Workspace $d) /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $d (exit $LASTEXITCODE)." }
}
$global:LASTEXITCODE = 0
Remove-Item -LiteralPath $export -Recurse -Force

# --- 3. Build from the workspace with the version stamped in -------------
$versionFile = Join-Path $Workspace 'ProjectSettings\ProjectVersion.txt'
$required = ([regex]::Match((Get-Content -LiteralPath $versionFile -Raw), '(?m)^m_EditorVersion:\s*(\S+)')).Groups[1].Value
if (-not $UnityEditor) { $UnityEditor = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$required\Editor\Unity.exe" }
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw "Unity $required is required (install it through Unity Hub or pass -UnityEditor)." }
$method = if ($Target -eq 'WebGL') { 'ConfiguratorBuild.BuildWebGL' } else { 'ConfiguratorBuild.BuildWindows' }
$buildTarget = if ($Target -eq 'WebGL') { 'WebGL' } else { 'Win64' }
$out = Join-Path $Workspace "Builds\$Tag-$Target"
New-Item -ItemType Directory -Force -Path (Join-Path $repo 'Logs') | Out-Null
$log = Join-Path $repo "Logs\release-$Tag-$Target.log"
if (-not $SkipBuild) {
    if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
    $lock = Join-Path $Workspace 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $lock) { Remove-Item -LiteralPath $lock }
    $unityArgs = @('-batchmode', '-nographics', '-quit', '-projectPath', "`"$Workspace`"", '-buildTarget', $buildTarget,
                   '-executeMethod', $method, '-configuratorOutput', "`"$out`"",
                   '-configuratorVersion', $Tag.TrimStart('v'), '-logFile', "`"$log`"")
    Write-Output "Building $Tag ($Target) with Unity $required ..."
    $p = Start-Process -FilePath $UnityEditor -ArgumentList $unityArgs -Wait -PassThru -WindowStyle Hidden
    if ($p.ExitCode -ne 0) { throw "Unity build failed (exit $($p.ExitCode)); read $log" }
}
if (-not (Test-Path -LiteralPath (Join-Path $out 'build-report.json'))) { throw "No build-report.json in $out; the build is incomplete." }

# --- 4. Package: one ZIP and checksum per version and target -------------
$buildsDir = Join-Path $repo 'Builds'
New-Item -ItemType Directory -Force -Path $buildsDir | Out-Null
$suffix = if ($Target -eq 'WebGL') { 'webgl' } else { 'windows-x64' }
$zip = Join-Path $buildsDir "configurator-$Tag-$suffix.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip }
$files = @(Get-ChildItem -LiteralPath $out | Where-Object { $_.Name -notlike '*_DoNotShip' })
Compress-Archive -LiteralPath $files.FullName -DestinationPath $zip
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$zip.sha256" -Value "$hash  $(Split-Path -Leaf $zip)" -Encoding ascii

# --- 5. Release notes: the CHANGELOG.md section headed by this tag -------
$notes = Join-Path $buildsDir "configurator-$Tag-notes.md"
$section = @()
$changelog = Join-Path $repo 'CHANGELOG.md'
if (Test-Path -LiteralPath $changelog) {
    $inside = $false
    foreach ($line in Get-Content -LiteralPath $changelog) {
        if ($line -match '^## ') {
            $inside = $line -match [regex]::Escape($Tag)
            continue
        }
        if ($inside) { $section += $line }
    }
}
if ($section.Count -eq 0) { $section = @("Release $Tag.") }
$section += ''
$section += "Built from commit $commit with Unity $required. SHA-256 of the archive: $hash."
Set-Content -LiteralPath $notes -Value ($section -join "`n") -Encoding utf8

# --- 6. Publish: the tag to GitHub, the files to its Release -------------
if (-not $NoPush) {
    git push origin $Tag
    if ($LASTEXITCODE -ne 0) { throw 'git push of the tag failed.' }
}
$gh = Get-Command gh -ErrorAction SilentlyContinue
Write-Output "Packaged $zip"
if ($NoPublish -or -not $gh) {
    Write-Output "Create the release at https://github.com/paekinator/configuratorNS/releases/new?tag=$Tag"
    Write-Output "  title: $Tag   notes: $notes   attach: $zip and $zip.sha256"
    if (-not $gh) { Write-Output "  (install GitHub CLI and run 'gh auth login' once; this script then publishes by itself)" }
} else {
    & gh release view $Tag *> $null
    if ($LASTEXITCODE -eq 0) {
        & gh release upload $Tag $zip "$zip.sha256" --clobber
    } else {
        $flags = @()
        if ($Tag -match '-') { $flags += '--prerelease' }
        & gh release create $Tag $zip "$zip.sha256" --title $Tag --notes-file $notes @flags
    }
    if ($LASTEXITCODE -ne 0) { throw 'gh release failed.' }
    Write-Output "Published https://github.com/paekinator/configuratorNS/releases/tag/$Tag"
}
