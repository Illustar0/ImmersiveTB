using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace ImmersiveTB.Benchmarks;

/// <summary>
///     Configures repeated buffer operations so memory-owner costs are measurable.
/// </summary>
internal sealed class PixelBufferBenchmarkConfig : ManualConfig
{
    /// <summary>
    ///     Creates a high-invocation in-process job and managed-memory diagnostics.
    /// </summary>
    public PixelBufferBenchmarkConfig()
    {
        ArtifactsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ImmersiveTB.Benchmarks",
            "BenchmarkDotNet.Artifacts"
        );
        Directory.CreateDirectory(ArtifactsPath);

        AddJob(
            Job.Default
                .WithId("BufferInProcess")
                .WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(10)
                .WithInvocationCount(131072)
                .WithUnrollFactor(1)
        );
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddLogger(ConsoleLogger.Default);
    }
}