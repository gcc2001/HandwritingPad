#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
MODELS_DIR="$ROOT_DIR/Assets/Models"
SERVER_DIR="$MODELS_DIR/ppocrv5_server_rec"
MOBILE_DIR="$MODELS_DIR/ppocrv5_mobile_rec"
mkdir -p "$SERVER_DIR" "$MOBILE_DIR"

SERVER_MODEL_URL="https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.8.0/onnx/PP-OCRv5/rec/ch_PP-OCRv5_rec_server.onnx"
MOBILE_MODEL_URL="https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.8.0/onnx/PP-OCRv5/rec/ch_PP-OCRv5_rec_mobile.onnx"
SHARED_DICT_URL="https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.8.0/paddle/PP-OCRv5/rec/ch_PP-OCRv5_rec_server/ppocrv5_dict.txt"

download() {
  local url="$1"
  local out="$2"
  if [[ -f "$out" ]]; then
    echo "[skip] $out already exists"
    return
  fi
  echo "[download] $url"
  curl -L --retry 3 --retry-delay 2 -o "$out" "$url"
}

download "$SERVER_MODEL_URL" "$SERVER_DIR/inference.onnx"
download "$MOBILE_MODEL_URL" "$MOBILE_DIR/inference.onnx"
download "$SHARED_DICT_URL" "$MODELS_DIR/ppocrv5_dict.txt"

echo "Models downloaded."
echo "Server model SHA256 expected: e09385400eaaaef34ceff54aeb7c4f0f1fe014c27fa8b9905d4709b65746562a"
echo "Mobile model SHA256 expected: 5825fc7ebf84ae7a412be049820b4d86d77620f204a041697b0494669b1742c5"
echo "Shared dictionary: Assets/Models/ppocrv5_dict.txt"
