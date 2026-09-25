# Reference results

Reference run on 2026-09-25:

- Windows 11 10.0.26200.9457
- Intel Core i5-12600K, 16 logical / 10 physical cores
- .NET 10.0.12, x64 RyuJIT
- BenchmarkDotNet 0.15.8
- Capture region: primary taskbar width × 10 physical pixels

## End-to-end capture latency

| Strategy                      | DotNext mean | CommunityToolkit mean |
|-------------------------------|-------------:|----------------------:|
| GDI: recreate DC and bitmap   |     4.848 ms |              4.790 ms |
| GDI: reuse screen + memory DC |     4.808 ms |              4.731 ms |
| GDI: reuse DC + bitmap        |     4.729 ms |              4.771 ms |
| GDI: direct DIB section       |     4.699 ms |              4.812 ms |
| WGC: no item/staging cache    |    21.623 ms |             19.574 ms |
| WGC: capture-item cache       |    18.177 ms |             18.120 ms |
| WGC: staging-texture cache    |    20.665 ms |             22.192 ms |
| WGC: item + staging caches    |    19.647 ms |             18.126 ms |

Each row used 3 warmups and 12 single-capture iterations. Single invocation is intentional: ImmersiveTB samples sparsely
after taskbar/window events, while batched captures would measure a different frame-pacing workload. WGC variance is
materially higher than GDI variance, so repeat the run before making a production decision.

On this machine:

- GDI native-resource caches and the direct `DIBSection` path did not show a clear latency advantage; their reported
  errors overlap.
- WGC capture-item caching trended faster than recreating the item, while staging-texture caching alone did not show a
  consistent improvement. The reported errors overlap, so this run does not establish a definitive cache choice.
- One-shot WGC measured roughly 4× slower than GDI because every operation still starts a frame pool/session and waits
  for its first frame.
- The buffer backend was not a meaningful capture-latency driver; native capture/session costs dominated it.

## Pooled pixel-buffer microbenchmark

This benchmark allocates a pooled 1920×10 BGRA8 buffer, copies 76,800 bytes, reads one byte, and returns the buffer.

| Backend                          |     Mean | Managed allocation |
|----------------------------------|---------:|-------------------:|
| DotNext                          | 1.279 µs |               49 B |
| CommunityToolkit.HighPerformance | 1.262 µs |               73 B |

This run used 131,072 invocations per iteration, 3 warmups, and 10 measured iterations. The timings were similar within
the reported error, while DotNext allocated 24 fewer bytes per operation.

Raw reports are generated at:

```text
%LOCALAPPDATA%\ImmersiveTB.Benchmarks\BenchmarkDotNet.Artifacts\results
```
