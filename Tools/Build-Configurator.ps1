[CmdletBinding()]
param(
    [ValidateSet('Windows', 'WebGL')]
    [string]$Target = 'Windows',
    [string]$UnityEditor,
    [switch]$Development,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$projectDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$versionFile = Join-Path $projectDirectory 'ProjectSettings/ProjectVersion.txt'
$versionMatch = [regex]::Match((Get-Content -LiteralPath $versionFile -Raw), '(?m)^m_EditorVersion:\s*(\S+)')
if (-not $versionMatch.Success) { throw "Cannot find the Unity version in $versionFile" }
$requiredVersion = $versionMatch.Groups[1].Value
if (-not $UnityEditor) {
    $UnityEditor = Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$requiredVersion/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
    throw "Unity $requiredVersion is required. Install it through Unity Hub or pass -UnityEditor with its Unity.exe path."
}
$UnityEditor = (Resolve-Path -LiteralPath $UnityEditor).Path
$moduleName = if ($Target -eq 'WebGL') { 'WebGLSupport' } else { 'windowsstandalonesupport' }
$modulePath = Join-Path (Split-Path -Parent $UnityEditor) "Data/PlaybackEngines/$moduleName"
if (-not (Test-Path -LiteralPath $modulePath -PathType Container)) {
    throw "$Target build support is missing from this editor. In Unity Hub, add the $Target build module to Unity $requiredVersion."
}

$buildMethod = if ($Target -eq 'WebGL') { 'ConfiguratorBuild.BuildWebGL' } else { 'ConfiguratorBuild.BuildWindows' }
$buildTarget = if ($Target -eq 'WebGL') { 'WebGL' } else { 'Win64' }
# Every run gets its own directory so a package cannot accidentally contain files from an older build.
$buildName = $Target + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
$outputDirectory = Join-Path $projectDirectory "Builds/$buildName"
$logDirectory = Join-Path $projectDirectory 'Logs'
$logPath = Join-Path $logDirectory "build-$buildName.log"
Write-Output "Project: $projectDirectory"
Write-Output "Required Unity version: $requiredVersion"
Write-Output "Editor: $UnityEditor"
Write-Output "Build method: $buildMethod"
Write-Output "Output: $outputDirectory"
Write-Output "Log: $logPath"
if ($CheckOnly) { return }

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$unityArguments = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', $projectDirectory,
    '-buildTarget', $buildTarget,
    '-executeMethod', $buildMethod,
    '-configuratorOutput', $outputDirectory,
    '-logFile', $logPath
)
if ($Development) { $unityArguments += '-configuratorDevelopment' }
# Start-Process joins ArgumentList with spaces; quote every token so workspace names remain intact.
$quotedArguments = foreach ($argument in $unityArguments) {
    if ($argument.Contains('"')) { throw 'Build paths cannot contain double quotes.' }
    '"' + $argument + '"'
}
$process = Start-Process -FilePath $UnityEditor -ArgumentList $quotedArguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Unity exited with code $($process.ExitCode). Read $logPath" }
$metadataPath = Join-Path $outputDirectory 'build-report.json'
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw "Unity did not produce build-report.json; this build is incomplete. Read $logPath"
}
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($metadata.unityVersion -ne $requiredVersion) {
    throw "Built with Unity $($metadata.unityVersion), expected $requiredVersion. No release package was created."
}

if ($Target -eq 'Windows') {
    $archivePath = Join-Path $projectDirectory "Builds/$buildName.zip"
    # Unity emits Burst debugging symbols beside the player; its DoNotShip
    # folders are not runtime dependencies and should stay out of the ZIP.
    $packageFiles = @(Get-ChildItem -LiteralPath $outputDirectory | Where-Object { $_.Name -notlike '*_DoNotShip' })
    Compress-Archive -LiteralPath $packageFiles.FullName -DestinationPath $archivePath
    $archiveHash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
    Set-Content -LiteralPath ($archivePath + '.sha256') -Value ($archiveHash.Hash.ToLowerInvariant() + '  ' + (Split-Path -Leaf $archivePath)) -Encoding ascii
    Write-Output "Windows package: $archivePath"
    Write-Output "SHA-256: $($archiveHash.Hash.ToLowerInvariant())"
}
Write-Output "Build complete: $outputDirectory"
