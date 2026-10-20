param(
    [switch]$Compact,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$outputName = if ($Compact) { 'win-x64-compact' } else { 'win-x64' }
$outputPath = Join-Path $PSScriptRoot "publish\$outputName"
$selfContained = if ($Compact) { 'false' } else { 'true' }
$publishArgs = @(
    'publish', (Join-Path $PSScriptRoot 'QuickShot.csproj'),
    '-c', 'Release', '-r', 'win-x64', '--self-contained', $selfContained,
    '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=None', '-p:DebugSymbols=false', "-p:EnableCompressionInSingleFile=$selfContained",
    '-o', $outputPath
)
if ($NoRestore) { $publishArgs += '--no-restore' }
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Publicação falhou com código $LASTEXITCODE." }
Write-Host "Aplicação: $(Join-Path $outputPath 'QuickShot.exe')"
if ($Compact) { Write-Host 'Esta versão requer .NET Desktop Runtime 8 x64 instalado.' }
