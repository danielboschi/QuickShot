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
    # Sem compressao interna: ela reduz o arquivo, mas mantem as bibliotecas expandidas na memoria.
    '-p:DebugType=None', '-p:DebugSymbols=false', '-p:EnableCompressionInSingleFile=false',
    '-o', $outputPath
)
if ($NoRestore) { $publishArgs += '--no-restore' }
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Publicação falhou com código $LASTEXITCODE." }
Write-Host "Aplicação: $(Join-Path $outputPath 'QuickShot.exe')"
if ($Compact) { Write-Host 'Esta versão requer .NET Desktop Runtime 10 x64 instalado.' }
