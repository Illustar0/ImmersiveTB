using BenchmarkDotNet.Running;
using WinRT;

namespace ImmersiveTB.Benchmarks;

/// <summary>
///     Starts the in-process benchmark host.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Runs benchmarks selected by BenchmarkDotNet command-line arguments.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}