# ImmersiveTB capture benchmarks

This BenchmarkDotNet project measures real 10-pixel taskbar-strip captures. It is isolated from the production
`ImmersiveTB` project and owns its WGC/GDI experimental implementations.

## Run

From the repository root:

```powershell
.\ImmersiveTB.Benchmarks\Run-Benchmarks.ps1
```

Useful variants:

```powershell
# List all benchmark methods without running them
.\ImmersiveTB.Benchmarks\Run-Benchmarks.ps1 -List

# Run only end-to-end screen capture cases
.\ImmersiveTB.Benchmarks\Run-Benchmarks.ps1 -Filter "*ScreenCaptureBenchmarks*"

# Run only pooled pixel-buffer cases
.\ImmersiveTB.Benchmarks\Run-Benchmarks.ps1 -Filter "*PixelBufferBenchmarks*"
```

The script builds `Release|x64` and runs the benchmark directly with `dotnet run`. WGC uses the desktop
`IGraphicsCaptureItemInterop.CreateForMonitor` API, so the benchmark needs no package registration.

Requirements: Windows build 20348 or newer and the .NET 10 SDK.

## Scenario matrix

End-to-end cases use the same physical capture region and copy BGRA8 pixels into a pooled managed buffer:

- GDI recreates all native resources.
- GDI reuses only the screen and memory device contexts.
- GDI reuses device contexts and the same-size compatible bitmap.
- GDI writes through a top-down `DIBSection` instead of calling `GetDIBits`.
- WGC recreates both `GraphicsCaptureItem` and staging texture.
- WGC caches only `GraphicsCaptureItem`.
- WGC caches only the staging texture.
- WGC caches both resources.

Every case is parameterized with both `DotNext.Buffers.MemoryOwner<byte>` and
`CommunityToolkit.HighPerformance.Buffers.MemoryOwner<byte>`, yielding 16 end-to-end scenarios. `PixelBufferBenchmarks`
separately isolates pooled allocation, a 1920×10 BGRA8 copy, and buffer return.

All WGC cases retain the D3D device for the adapter lifetime, matching ImmersiveTB's singleton adapter model. The WGC
matrix varies only the capture-item and staging-texture hotspots named by each row. Desktop capture uses the system's
default capture border.

WGC deliberately recreates its frame pool and capture session per operation in every case. This keeps the comparison
focused on production-safe caches; a persistent session would continuously capture between samples and represents a
different energy-use model.

## Measurement notes

The capture job uses one capture per iteration because ImmersiveTB performs sparse, event-driven samples.
BenchmarkDotNet can warn that these iterations are shorter than its throughput-oriented 100 ms recommendation; batching
captures would change the frame-pacing behavior being measured. Use multiple runs and compare medians/distributions
before changing production defaults.

Reports are written under the user's local-app-data directory. The runner prints the exact
`BenchmarkDotNet.Artifacts\results` path after completion.

See [RESULTS.md](RESULTS.md) for the latest complete benchmark run.
