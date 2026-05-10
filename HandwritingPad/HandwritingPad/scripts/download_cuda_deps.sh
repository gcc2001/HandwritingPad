#!/usr/bin/env bash
set -euo pipefail

# Assumption: end users install NVIDIA Driver + CUDA Toolkit 12.x.
# This script downloads only cuDNN 9.x CUDA-12 runtime SO files and places them under Native/cuda/linux-x64.
# By running this script you must comply with NVIDIA Software License terms for cuDNN redistribution.

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$ROOT_DIR/Native/cuda/linux-x64"
WORK_DIR="$ROOT_DIR/.cache/cuda-deps-linux-x64"
ARCHIVE_NAME="cudnn-linux-x86_64-9.21.1.3_cuda12-archive.tar.xz"
URL="https://developer.download.nvidia.com/compute/cudnn/redist/cudnn/linux-x86_64/$ARCHIVE_NAME"
ARCHIVE_PATH="$WORK_DIR/$ARCHIVE_NAME"
EXTRACT_DIR="$WORK_DIR/extract"

mkdir -p "$OUT_DIR" "$WORK_DIR"

if [[ ! -f "$ARCHIVE_PATH" ]]; then
  echo "[download] $URL"
  curl -L --retry 3 --retry-delay 2 -o "$ARCHIVE_PATH" "$URL"
else
  echo "[skip] archive already exists: $ARCHIVE_PATH"
fi

rm -rf "$EXTRACT_DIR"
mkdir -p "$EXTRACT_DIR"

echo "[extract] $ARCHIVE_PATH"
tar -xf "$ARCHIVE_PATH" -C "$EXTRACT_DIR"

echo "[copy] cuDNN SO files -> $OUT_DIR"
find "$EXTRACT_DIR" \( -type f -o -type l \) -name 'libcudnn*.so*' -exec cp -a {} "$OUT_DIR" \;
find "$EXTRACT_DIR" \( -type f -o -type l \) -name 'LICENSE*' -exec cp -a {} "$OUT_DIR" \; 2>/dev/null || true

chmod -R u+rwX "$OUT_DIR"

echo "Done. Bundled CUDA deps directory: $OUT_DIR"
echo "Note: libcuda.so.1 and CUDA Toolkit libs still come from NVIDIA Driver / CUDA Toolkit on the user machine."
