[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$publishRoot = Join-Path $artifactsRoot 'publish'
$installerProject = Join-Path $repositoryRoot 'installer\Hataori.Installer.wixproj'
$version = ([xml](Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version

function Invoke-DotNet {
    param([string[]]$DotNetArguments)

    & dotnet @DotNetArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE."
    }
}

foreach ($product in @('server', 'cli', 'monitor')) {
    $productPath = Join-Path $publishRoot $product
    if (Test-Path -LiteralPath $productPath) {
        Remove-Item -LiteralPath $productPath -Recurse -Force
    }
}

$publishProperties = @(
    '--configuration', $Configuration,
    '--runtime', $RuntimeIdentifier,
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

Invoke-DotNet -DotNetArguments (@('publish', (Join-Path $repositoryRoot 'src\Hataori.Server\Hataori.Server.csproj')) + $publishProperties + @('--output', (Join-Path $publishRoot 'server')))
Invoke-DotNet -DotNetArguments (@('publish', (Join-Path $repositoryRoot 'src\Hataori.Cli\Hataori.Cli.csproj')) + $publishProperties + @('--output', (Join-Path $publishRoot 'cli')))
Invoke-DotNet -DotNetArguments (@('publish', (Join-Path $repositoryRoot 'src\Hataori.Monitor\Hataori.Monitor.csproj')) + $publishProperties + @('--output', (Join-Path $publishRoot 'monitor')))
Invoke-DotNet -DotNetArguments @('build', $installerProject, '--configuration', $Configuration, "-p:PublishRoot=$publishRoot", "-p:ProductVersion=$version")

$msiPath = Join-Path $artifactsRoot "installer\Hataori-$version-x64.msi"
if (-not (Test-Path -LiteralPath $msiPath)) {
    throw "MSI was not created at '$msiPath'."
}

$hash = Get-FileHash -LiteralPath $msiPath -Algorithm SHA256
[pscustomobject]@{
    Version = $version
    MsiPath = $msiPath
    Sha256 = $hash.Hash
}
