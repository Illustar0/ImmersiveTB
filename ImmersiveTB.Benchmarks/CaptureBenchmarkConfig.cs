using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace ImmersiveTB.Benchmarks;

/// <summary>
///     Runs WGC benchmarks in-process so capture resources remain in the benchmark host.
/// </summary>
internal sealed class CaptureBenchmarkConfig : ManualConfig
{
    /// <summary>
    ///     Configures low-invocation monitoring runs for frame-latency measurements.
    /// </summary>
    public CaptureBenchmarkConfig()
    {
        ArtifactsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ImmersiveTB.Benchmarks",
            "BenchmarkDotNet.Artifacts"
        );
        Directory.CreateDirectory(ArtifactsPath);

        AddJob(
            Job.Default
                .WithId("InProcess")
                .WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithStrategy(RunStrategy.Monitoring)
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(12)
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
        );
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddLogger(ConsoleLogger.Default);
    }
}