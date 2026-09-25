[CmdletBinding()]
param(
    [string]$Filter = "*",
    [switch]$List
)

$ErrorActionPreference = "Stop"
$projectDirectory = $PSScriptRoot
$project = Join-Path $projectDirectory "ImmersiveTB.Benchmarks.csproj"

dotnet build $project --configuration Release -p:Platform=x64
if ($LASTEXITCODE -ne 0)
{
    throw "Benchmark build failed with exit code $LASTEXITCODE."
}

$benchmarkArguments = if ($List)
{
    @("--list", "flat")
}
else
{
    @("--filter", $Filter)
}

dotnet run --project $project --configuration Release --no-build -p:Platform=x64 -- @benchmarkArguments
if ($LASTEXITCODE -ne 0)
{
    throw "Benchmark run failed with exit code $LASTEXITCODE."
}

$results = Join-Path $env:LOCALAPPDATA (
"ImmersiveTB.Benchmarks\BenchmarkDotNet.Artifacts\results"
)
Write-Host "Benchmark reports: $results"
