$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "InformationBox/InformationBox.csproj"
$configuration = "Release"
$runtime = "win-x64"

$artefactsDir = Join-Path $root "Artefacts"
$portableDir = Join-Path $artefactsDir "portable"              # self-contained, loose files
$singleFxDir = Join-Path $artefactsDir "single-fx"             # framework-dependent, single file (compressed), needs .NET runtime
$fxDir = Join-Path $artefactsDir "fx"                          # framework-dependent, loose files
$singleContainedDir = Join-Path $artefactsDir "single-contained" # self-contained, single file with native self-extract

$portableZip = Join-Path $artefactsDir "InformationBox-portable.zip"
$singleFxZip = Join-Path $artefactsDir "InformationBox-singlefx.zip"
$fxZip = Join-Path $artefactsDir "InformationBox-fx.zip"
$singleContainedZip = Join-Path $artefactsDir "InformationBox-single-contained.zip"

Remove-Item $artefactsDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $portableDir, $singleFxDir, $fxDir, $singleContainedDir | Out-Null

function Publish-App {
    param(
        [string]$outputDir,
        [bool]$selfContained,
        [bool]$singleFile,
        [bool]$readyToRun,
        [bool]$compressSingleFile = $false,
        [bool]$selfExtract = $false,
        [bool]$emitDocs = $false
    )

    dotnet publish $project `
        -c $configuration `
        -r $runtime `
        --self-contained:$selfContained `
        /p:PublishSingleFile=$singleFile `
        /p:PublishReadyToRun=$readyToRun `
        /p:PublishTrimmed=false `
        /p:IncludeNativeLibrariesForSelfExtract=$selfExtract `
        /p:EnableCompressionInSingleFile=$compressSingleFile `
        /p:GenerateDocumentationFile=$emitDocs `
        /p:DebugType=None `
        /p:DebugSymbols=false `
        /p:ExcludeSymbolsFromSingleFile=true `
        /p:ErrorOnDuplicatePublishOutputFiles=false `
        /p:UseAppHost=true `
        /p:PublishDir=$outputDir
}

Write-Host "==> Portable (self-contained, loose files, no ReadyToRun)"
Publish-App -outputDir $portableDir -selfContained $true -singleFile $false -readyToRun $false

Write-Host "==> Single-fx (framework-dependent, single file, compressed) - requires installed .NET runtime"
Publish-App -outputDir $singleFxDir -selfContained $false -singleFile $true -readyToRun $false -compressSingleFile $true

Write-Host "==> FX (framework-dependent, loose files) - smallest unpacked size"
Publish-App -outputDir $fxDir -selfContained $false -singleFile $false -readyToRun $false

Write-Host "==> Single-contained (self-contained, single file + self-extract) - closest to old single-file experience"
Publish-App -outputDir $singleContainedDir -selfContained $true -singleFile $true -readyToRun $false -compressSingleFile $true -selfExtract $true

if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
if (Test-Path $singleFxZip) { Remove-Item $singleFxZip -Force }
if (Test-Path $fxZip) { Remove-Item $fxZip -Force }
if (Test-Path $singleContainedZip) { Remove-Item $singleContainedZip -Force }

Write-Host "==> Zipping outputs"
Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $portableZip
Compress-Archive -Path (Join-Path $singleFxDir "*") -DestinationPath $singleFxZip
Compress-Archive -Path (Join-Path $fxDir "*") -DestinationPath $fxZip
Compress-Archive -Path (Join-Path $singleContainedDir "*") -DestinationPath $singleContainedZip -Update:$false -CompressionLevel Optimal

Write-Host "==> Done"
Write-Host "Portable unpacked: $portableDir"
Write-Host "Portable zip:      $portableZip"
Write-Host "Single-fx unpacked: $singleFxDir"
Write-Host "Single-fx zip:      $singleFxZip"
Write-Host "FX unpacked:        $fxDir"
Write-Host "FX zip:             $fxZip"
Write-Host "Single-contained unpacked: $singleContainedDir"
Write-Host "Single-contained zip:      $singleContainedZip"
