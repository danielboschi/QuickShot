$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $projectRoot 'publish\win-x64'
$outputDir = Join-Path $projectRoot 'dist'
$issFile = Join-Path $PSScriptRoot 'QuickShot.iss'
$nugetConfig = Join-Path $projectRoot 'NuGet.Config'

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $isccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
} else {
    $isccPath = $iscc.Source
}
if (-not $isccPath) { throw 'Inno Setup 6 não encontrado. Instale-o e execute este script novamente.' }

dotnet restore (Join-Path $projectRoot 'QuickShot.csproj') -r win-x64 --configfile $nugetConfig
if ($LASTEXITCODE -ne 0) { throw "dotnet restore falhou com código $LASTEXITCODE." }

dotnet publish (Join-Path $projectRoot 'QuickShot.csproj') -c Release -r win-x64 --self-contained true --no-restore `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false `
    -p:EnableCompressionInSingleFile=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou com código $LASTEXITCODE." }

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
& $isccPath $issFile
if ($LASTEXITCODE -ne 0) { throw "Inno Setup falhou com código $LASTEXITCODE." }
Write-Host "Instalador criado em: $(Join-Path $outputDir 'QuickShot-Setup-1.0.0-x64.exe')"
