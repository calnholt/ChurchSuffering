using Crusaders30XX.ECS.Benchmarks.Baselines;
using Xunit;

namespace Crusaders30XX.ECS.Benchmarks.Tests;

public sealed class LegacyBaselineLoaderTests
{
    [Fact]
    public void Loads_committed_ecs_000_artifact()
    {
        string repositoryRoot = FindRepositoryRoot();
        LegacyPerformanceArtifact artifact = LegacyBaselineLoader.Load(Path.Combine(
            repositoryRoot,
            "tests",
            "PerformanceBaselines",
            "legacy-ecs-initial.json"));

        Assert.Equal(1, artifact.SchemaVersion);
        Assert.Equal("legacy-object-ecs", artifact.Runtime);
        Assert.Equal("Release", artifact.BuildConfiguration);
        Assert.Equal(1337, artifact.FixedSeed);
        Assert.Equal(["battle", "climb"], artifact.Workloads.Select(result => result.Name));
        Assert.All(artifact.Workloads, workload =>
        {
            Assert.Equal(artifact.SampleFrames, workload.CpuScopes.EcsUpdate.SampleCount);
            Assert.True(workload.ProcessedRows > 0);
        });
    }

    [Fact]
    public void Rejects_non_release_artifact()
    {
        string path = Path.Combine(Path.GetTempPath(), $"invalid-legacy-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "schemaVersion": 1,
                  "runtime": "legacy-object-ecs",
                  "buildConfiguration": "Debug",
                  "sampleFrames": 1,
                  "workloads": []
                }
                """);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => LegacyBaselineLoader.Load(path));
            Assert.Contains("not captured in Release", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string FindRepositoryRoot()
    {
        string? root = LegacyBaselineLoader.FindDefaultPath(AppContext.BaseDirectory);
        Assert.NotNull(root);
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(root)!, "..", ".."));
    }
}
