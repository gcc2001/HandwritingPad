# HandwritingPad

Avalonia + ONNX Runtime + PP-OCRv5 的跨平台离线手写 OCR 输入板。

>   **说明**
>
>   本项目(含文档)均由AI生成，只有少量代码和项目编译、功能测试由人工完成，仓库维护者不对AI生成的代码和使用的模型所产生的版权问题负责
>
>   Releases不提供CUDA版本，如需使用，请自行根据脚本下载模型、补充依赖(相关脚本在`Scripts`中)

## 功能

- UI 保留模型切换：`PP-OCRv5 Server` / `PP-OCRv5 Mobile`。
- UI 不提供 CPU/CUDA 推理类型切换；推理后端由发布包决定。
- 同一份源码可以发布 CPU 版和 CUDA 版。
- Server/Mobile 两个 ONNX 模型共用一套字典：`Assets/Models/ppocrv5_dict.txt`。
- 支持简体中文、繁体中文、英文。
- 保留手写增强逻辑：bbox 裁切、多渲染、CTC beam search、左右结构修正、标点几何识别。
- 支持候选点击复制到剪贴板，可选自动粘贴。
- 支持点击候选后显示简单联想候选。
- 显示本次推理耗时与当前发布包后端。

## 前置条件

### CPU 版

只需要 .NET SDK 用于编译发布；运行发布包不需要额外 OCR 依赖。

### CUDA 版

假定最终用户会安装：

- NVIDIA Driver，并确保 `nvidia-smi` 可运行。
- CUDA Toolkit 12.x，并确保 CUDA `bin` / `lib64` 可被系统加载。

项目会通过脚本下载并打包 cuDNN 9.x 到应用目录：

- Windows: `Native/cuda/win-x64`
- Linux: `Native/cuda/linux-x64`

注意：`nvcuda.dll` / `libcuda.so.1` 来自 NVIDIA Driver，不能用应用包替代。

## 下载 OCR 模型

Windows PowerShell：

```powershell
.\scripts\download_models.ps1
```

Linux / macOS：

```bash
./scripts/download_models.sh
```

下载后应有：

```text
Assets/Models/ppocrv5_server_rec/inference.onnx
Assets/Models/ppocrv5_mobile_rec/inference.onnx
Assets/Models/ppocrv5_dict.txt
```

## 下载 CUDA 相关依赖（cuDNN）

Windows PowerShell：

```powershell
.\scripts\download_cuda_deps.ps1
```

Linux：

```bash
./scripts/download_cuda_deps.sh
```

脚本会下载 NVIDIA cuDNN 9.x CUDA 12 archive，并只提取运行所需库到 `Native/cuda/...`。运行脚本即表示你需要遵守 NVIDIA cuDNN 软件许可条款。

## 一键发布 CPU + CUDA

Windows：

```powershell
.\scripts\build-win-x64.ps1
```

输出：

```text
publish/win-x64-cpu
publish/win-x64-cuda
```

Linux：

```bash
./scripts/build-linux-x64.sh
```

输出：

```text
publish/linux-x64-cpu
publish/linux-x64-cuda
```

Linux CUDA 包请通过以下脚本启动，以便加载应用内置 cuDNN：

```bash
publish/linux-x64-cuda/run-handwritingpad-cuda.sh
```

Windows CUDA 包启动时会自动把应用目录下的 `cuda/` 加入 DLL 搜索路径。

## 本地运行

CPU：

```bash
dotnet run -p:OnnxRuntimeFlavor=cpu
```

CUDA：

```bash
dotnet run -p:OnnxRuntimeFlavor=cuda
```

本地 CUDA 运行同样要求系统能加载 CUDA Toolkit 和 `Native/cuda/...` 中的 cuDNN。Linux 下建议从发布目录用启动脚本验证 CUDA 包。

## 备注

- GPU 加速只发生在 ONNX Runtime 的 `InferenceSession.Run()` 阶段。
- SkiaSharp 渲染、Bitmap 转 Tensor、CTC 解码和候选重排仍在 CPU 上。
- 如果 CUDA 版报缺库，程序会列出缺失的 DLL/SO 名称。
