#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

PUBLISH_ROOT="$ROOT_DIR/publish"
CPU_OUT="$PUBLISH_ROOT/linux-x64-cpu"
CUDA_OUT="$PUBLISH_ROOT/linux-x64-cuda"
mkdir -p "$PUBLISH_ROOT"

echo "[1/2] Building Linux x64 CPU package..."
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=false \
  -p:OnnxRuntimeFlavor=cpu \
  -o "$CPU_OUT"

echo "[2/2] Building Linux x64 CUDA package..."
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=false \
  -p:OnnxRuntimeFlavor=cuda \
  -o "$CUDA_OUT"

CUDA_SOURCE="$ROOT_DIR/Native/cuda/linux-x64"
CUDA_TARGET="$CUDA_OUT/cuda"
if [[ -d "$CUDA_SOURCE" ]]; then
  mkdir -p "$CUDA_TARGET"
  cp -a "$CUDA_SOURCE"/. "$CUDA_TARGET"/ 2>/dev/null || true
fi

cat > "$CUDA_OUT/run-handwritingpad-cuda.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
APP_DIR="$(cd "$(dirname "$0")" && pwd)"
export LD_LIBRARY_PATH="$APP_DIR/cuda:$APP_DIR:$LD_LIBRARY_PATH"
exec "$APP_DIR/HandwritingPad" "$@"
EOF
chmod +x "$CUDA_OUT/run-handwritingpad-cuda.sh"

cat > "$CUDA_OUT/CUDA_REQUIREMENTS.txt" <<'EOF'
CUDA package notes:

- This package assumes NVIDIA Driver and CUDA Toolkit 12.x are installed on the target machine.
- Bundled cuDNN SO files are loaded from ./cuda via run-handwritingpad-cuda.sh.
- libcuda.so.1 still comes from the NVIDIA Driver.
- CUDA Toolkit SO files such as libcudart.so.12 and libcublas.so.12 still come from CUDA Toolkit / LD_LIBRARY_PATH / ldconfig.
- If ./cuda is empty, run scripts/download_cuda_deps.sh before publishing.
EOF

echo "Done. Output directories:"
echo "  publish/linux-x64-cpu"
echo "  publish/linux-x64-cuda"
