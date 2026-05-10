$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
Set-Location $RootDir

$PublishRoot = Join-Path $RootDir "publish"
$CpuOut = Join-Path $PublishRoot "win-x64-cpu"
$CudaOut = Join-Path $PublishRoot "win-x64-cuda"

New-Item -ItemType Directory -Force -Path $PublishRoot | Out-Null

Write-Host "[1/2] Building Windows x64 CPU package..."
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false `
  -p:OnnxRuntimeFlavor=cpu `
  -o $CpuOut

Write-Host "[2/2] Building Windows x64 CUDA package..."
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false `
  -p:OnnxRuntimeFlavor=cuda `
  -o $CudaOut

$CudaSource = Join-Path $RootDir "Native\cuda\win-x64"
$CudaTarget = Join-Path $CudaOut "cuda"
if (Test-Path $CudaSource) {
    New-Item -ItemType Directory -Force -Path $CudaTarget | Out-Null
    Copy-Item -Path (Join-Path $CudaSource "*") -Destination $CudaTarget -Recurse -Force -ErrorAction SilentlyContinue
}

$Readme = @"
CUDA package notes:

- This package assumes NVIDIA Driver and CUDA Toolkit 12.x are installed on the target machine.
- Bundled cuDNN DLLs are loaded from ./cuda.
- nvcuda.dll still comes from the NVIDIA Driver.
- CUDA Toolkit DLLs such as cudart64_12.dll and cublas64_12.dll still come from CUDA Toolkit PATH.
- If ./cuda is empty, run scripts/download_cuda_deps.ps1 before publishing.
"@
Set-Content -Path (Join-Path $CudaOut "CUDA_REQUIREMENTS.txt") -Value $Readme -Encoding UTF8

Write-Host "Done. Output directories:"
Write-Host "  publish/win-x64-cpu"
Write-Host "  publish/win-x64-cuda"
