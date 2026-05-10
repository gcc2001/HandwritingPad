$ErrorActionPreference = "Stop"

# Assumption: end users install NVIDIA Driver + CUDA Toolkit 12.x.
# This script downloads only cuDNN 9.x CUDA-12 runtime DLLs and places them under Native/cuda/win-x64.
# By running this script you must comply with NVIDIA Software License terms for cuDNN redistribution.

$RootDir = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $RootDir "Native\cuda\win-x64"
$WorkDir = Join-Path $RootDir ".cache\cuda-deps-win-x64"
$ArchiveName = "cudnn-windows-x86_64-9.21.1.3_cuda12-archive.zip"
$Url = "https://developer.download.nvidia.com/compute/cudnn/redist/cudnn/windows-x86_64/$ArchiveName"
$ArchivePath = Join-Path $WorkDir $ArchiveName
$ExtractDir = Join-Path $WorkDir "extract"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

if (!(Test-Path $ArchivePath)) {
    Write-Host "[download] $Url"
    Invoke-WebRequest -Uri $Url -OutFile $ArchivePath
}
else {
    Write-Host "[skip] archive already exists: $ArchivePath"
}

if (Test-Path $ExtractDir) {
    Remove-Item -Recurse -Force $ExtractDir
}
New-Item -ItemType Directory -Force -Path $ExtractDir | Out-Null

Write-Host "[extract] $ArchivePath"
Expand-Archive -Path $ArchivePath -DestinationPath $ExtractDir -Force

Write-Host "[copy] cuDNN DLLs -> $OutDir"
Get-ChildItem -Path $ExtractDir -Recurse -Filter "*.dll" | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $OutDir $_.Name) -Force
}

Get-ChildItem -Path $ExtractDir -Recurse -Filter "LICENSE*" | Select-Object -First 5 | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $OutDir $_.Name) -Force
}

Write-Host "Done. Bundled CUDA deps directory: $OutDir"
Write-Host "Note: nvcuda.dll and CUDA Toolkit DLLs still come from NVIDIA Driver / CUDA Toolkit on the user machine."
