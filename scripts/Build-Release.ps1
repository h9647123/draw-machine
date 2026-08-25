[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$SkipInnoSetup
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$desktopProject = Join-Path $repositoryRoot 'DrawMachineDesktop\DrawMachineDesktop.csproj'
$installerProject = Join-Path $repositoryRoot 'DrawMachineInstaller\DrawMachineInstaller.csproj'
$setupScript = Join-Path $repositoryRoot 'DrawMachineSetup\DrawMachineSetup.iss'
$desktopProjectXml = [xml](Get-Content -Raw $desktopProject)
$version = $desktopProjectXml.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'DrawMachineDesktop.csproj must define a Version value.'
}

$artifactRoot = Join-Path $repositoryRoot "artifacts\$version"
$desktopOutput = Join-Path $artifactRoot 'desktop'
$installerOutput = Join-Path $artifactRoot 'installer'
$setupOutput = Join-Path $artifactRoot 'setup'
$payloadPath = Join-Path $repositoryRoot 'DrawMachineInstaller\Payload\DrawMachine.exe'

if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Force $desktopOutput, $installerOutput | Out-Null

dotnet publish $desktopProject -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $desktopOutput

$desktopExecutable = Join-Path $desktopOutput '抽号机.exe'
if (-not (Test-Path -LiteralPath $desktopExecutable -PathType Leaf)) {
    throw "Desktop publish did not produce $desktopExecutable"
}

New-Item -ItemType Directory -Force (Split-Path -Parent $payloadPath) | Out-Null
Copy-Item -LiteralPath $desktopExecutable -Destination $payloadPath -Force

dotnet publish $installerProject -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $installerOutput

if (-not $SkipInnoSetup) {
    $innoSetup = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1

    if ($innoSetup) {
        New-Item -ItemType Directory -Force $setupOutput | Out-Null
        & $innoSetup "/DSourceExe=$desktopExecutable" "/DOutputDir=$setupOutput" $setupScript
    }
    else {
        Write-Warning 'Inno Setup 6 was not found; skipped the traditional setup executable.'
    }
}

Get-ChildItem -LiteralPath $artifactRoot -Recurse -File |
    Select-Object FullName, Length, LastWriteTime |
    Format-Table -AutoSize
