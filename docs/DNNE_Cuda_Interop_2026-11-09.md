# Prepared native CUDA interoperability check

Read-only toolchain inspection found the NVIDIA driver at
`C:/Windows/System32/nvcuda.dll`, version 32.0.16.1692, and installed Visual C++
tools in Visual Studio 2019 Build Tools, 2022 Community and 2026 Community.
No CUDA toolkit was found on PATH or in the standard NVIDIA GPU Computing Toolkit
directory. This is not a claim that every disk location was searched.

NVIDIA's standalone NVRTC compiler is a viable candidate for compiling CUDA C++
device kernels from the C# host. The pinned compiler supports `compute_120` and
allows explicit control of floating-point contraction and fast-math options.
See [NVRTC 13.0 documentation](https://docs.nvidia.com/cuda/archive/13.0.2/nvrtc/index.html).
This avoids requiring a complete system-wide toolkit for the initial probe;
it does not establish a DNNE GPU backend or predict performance.

`prepare-cuda-nvrtc.py` fetches the official Windows x64 NVRTC 13.0.88 archive
from the [CUDA 13.0.2 redistribution manifest](https://developer.download.nvidia.com/compute/cuda/redist/redistrib_13.0.2.json).
The archive is pinned to 318,076,161 bytes and SHA-256
`8c50a52467826167e0dbe99936140c52d62272bfc5849fe2d6587d050c8c5d29`.
The local artifact is `artifacts/cuda-nvrtc-13.0.88`. It retains the original ZIP,
supplied license and extracted files with per-file hashes. Preparation does not
execute the compiler, change drivers, edit PATH or alter Entity's environment.
Incomplete or changed dependencies are rejected rather than overwritten.

`tools/CudaInteropProbe` is a standalone diagnostic executable with no reference
from DNNE, Avatar or Unity. It verifies the compiler DLLs against the pinned ZIP,
queries the actual device architecture, compiles one elementary double-precision
kernel and uses the CUDA driver API to allocate persistent buffers and launch it.
It uses an explicit thread-owned context and destroys that context at the end;
it never resets the device. The previous current context is restored.

The future check covers lengths 1, 255, 256, 257, 4,097 and 65,537, including
partial blocks. Seven additions of exact binary fractions must retain changes
far below single-precision resolution at weight 1. Both guard regions and all
result bits must match. Compile options disable contraction and fast math; this
is a compiler/transfer/launch/precision check, not a neuronal solver comparison,
memory-safety proof for arbitrary kernels, speed benchmark or physiology test.
Transient GPU API failures fail the probe; there is no CPU fallback disguised as
successful GPU execution. Failed contexts cannot continue computation.

The C# project builds with zero warnings/errors. **The probe has not been run.**
Run any actual check in a visible PowerShell window, with frozen source and
verified dependencies/D: backup and no live monitoring. In particular, do not
treat the completed build or downloaded compiler as a GPU qualification.
The brain performance study must be reviewed before selecting the neuronal port.
The driver API binding is probe-local and is not yet a production resource or
multi-threaded execution layer.
