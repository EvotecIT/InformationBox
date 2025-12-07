$ErrorActionPreference = "Stop"

# Paths
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "InformationBox/InformationBox.csproj"
$configuration = "Release"
$runtime = "win-x64"
$publishDir = Join-Path $root "InformationBox/bin/$configuration/net10.0-windows/$runtime/publish"
$artefactsDir = Join-Path $root "Build/Artefacts"
$unpackedDir = Join-Path $artefactsDir "unpacked"
$zipPath = Join-Path $artefactsDir "InformationBox.zip"

Write-Host "==> Publishing portable build"
dotnet publish $project `
    -c $configuration `
    -r $runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:PublishReadyToRun=true `
    /p:PublishTrimmed=false `
    /p:IncludeNativeLibrariesForSelfExtract=true

if (-not (Test-Path $publishDir)) {
    throw "Publish output not found at $publishDir"
}

Write-Host "==> Preparing artefacts folder"
Remove-Item $artefactsDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $unpackedDir | Out-Null

Write-Host "==> Copying unpacked output"
Copy-Item -Path (Join-Path $publishDir "*") -Destination $unpackedDir -Recurse -Force

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "==> Creating zip"
Compress-Archive -Path (Join-Path $unpackedDir "*") -DestinationPath $zipPath

Write-Host "==> Done"
Write-Host "Unpacked: $unpackedDir"
Write-Host "Zip:      $zipPath"
