$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$ModelsDir = Join-Path $RootDir "Assets\Models"
$ServerDir = Join-Path $ModelsDir "ppocrv5_server_rec"
$MobileDir = Join-Path $ModelsDir "ppocrv5_mobile_rec"
New-Item -ItemType Directory -Force -Path $ServerDir | Out-Null
New-Item -ItemType Directory -Force -Path $MobileDir | Out-Null

$ServerModelUrl = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.8.0/onnx/PP-OCRv5/rec/ch_PP-OCRv5_rec_server.onnx"
$MobileModelUrl = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.8.0/onnx/PP-OCRv5/rec/ch_PP-OCRv5_rec_mobile.onnx"
$SharedDictUrl = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.8.0/paddle/PP-OCRv5/rec/ch_PP-OCRv5_rec_server/ppocrv5_dict.txt"

function Download-IfMissing($Url, $OutFile) {
    if (Test-Path $OutFile) {
        Write-Host "[skip] $OutFile already exists"
        return
    }
    Write-Host "[download] $Url"
    Invoke-WebRequest -Uri $Url -OutFile $OutFile
}

Download-IfMissing $ServerModelUrl (Join-Path $ServerDir "inference.onnx")
Download-IfMissing $MobileModelUrl (Join-Path $MobileDir "inference.onnx")
Download-IfMissing $SharedDictUrl (Join-Path $ModelsDir "ppocrv5_dict.txt")

Write-Host "Models downloaded."
Write-Host "Server model SHA256 expected: e09385400eaaaef34ceff54aeb7c4f0f1fe014c27fa8b9905d4709b65746562a"
Write-Host "Mobile model SHA256 expected: 5825fc7ebf84ae7a412be049820b4d86d77620f204a041697b0494669b1742c5"
Write-Host "Shared dictionary: Assets/Models/ppocrv5_dict.txt"
