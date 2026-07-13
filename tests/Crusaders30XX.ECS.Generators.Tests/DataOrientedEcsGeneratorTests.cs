using Microsoft.CodeAnalysis;
using Xunit;

namespace Crusaders30XX.ECS.Generators.Tests;

public sealed class DataOrientedEcsGeneratorTests
{
    [Fact]
    public async Task Registration_and_spawn_bundle_snapshot_is_deterministic()
    {
        GeneratorTestHost.TestRun run = await GeneratorTestHost.RunAsync("""
using Crusaders30XX.ECS.DataOriented.Core;

namespace Sample;

public struct Velocity : IComponent { public float X; }
public struct Disabled : ITag { }
public struct Position : IComponent { public float X; public float Y; }
""");

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.AnalyzerDiagnostics);
        Assert.Empty(run.CompilationErrors);

        string registry = run.Generated["GeneratedComponentRegistry.g.cs"];
        string extensions = run.Generated["GeneratedSpawnBundleExtensions.g.cs"];
        string snapshot = string.Join('\n', new[]
        {
            FindLine(registry, "GeneratedComponentDescriptor(0"),
            FindLine(registry, "GeneratedComponentDescriptor(1"),
            FindLine(registry, "GeneratedComponentDescriptor(2"),
            FindLine(registry, "RegisterTag<"),
            FindLine(registry, "RegisterComponent<global::Sample.Position>"),
            FindLine(registry, "RegisterComponent<global::Sample.Velocity>"),
            FindLine(extensions, "WithDisabled"),
            FindLine(extensions, "WithPosition"),
            FindLine(extensions, "WithVelocity"),
        });

        Assert.Equal("""
new GeneratedComponentDescriptor(0, "Sample.Disabled", "Disabled", true),
new GeneratedComponentDescriptor(1, "Sample.Position", "Position", false),
new GeneratedComponentDescriptor(2, "Sample.Velocity", "Velocity", false),
registry.RegisterTag<global::Sample.Disabled>(0);
registry.RegisterComponent<global::Sample.Position>(1);
registry.RegisterComponent<global::Sample.Velocity>(2);
public static ref global::Crusaders30XX.ECS.DataOriented.Core.SpawnBundle WithDisabled(this ref global::Crusaders30XX.ECS.DataOriented.Core.SpawnBundle bundle)
public static ref global::Crusaders30XX.ECS.DataOriented.Core.SpawnBundle WithPosition(this ref global::Crusaders30XX.ECS.DataOriented.Core.SpawnBundle bundle, in global::Sample.Position value)
public static ref global::Crusaders30XX.ECS.DataOriented.Core.SpawnBundle WithVelocity(this ref global::Crusaders30XX.ECS.DataOriented.Core.SpawnBundle bundle, in global::Sample.Velocity value)
""".Trim(), snapshot);
    }

    [Fact]
    public async Task Query_declarations_generate_every_supported_arity_and_masks()
    {
        string components = string.Join('\n', Enumerable.Range(1, 8)
            .Select(index => $"public struct C{index} : IComponent {{ public int Value; }}"));
        string queries = string.Join('\n', Enumerable.Range(1, 8).Select(index =>
            $"[EcsQuery(\"query-{index}\", {string.Join(", ", Enumerable.Range(1, index).Select(component => $"typeof(C{component})"))}, All = new[] {{ typeof(RequiredTag) }}, None = new[] {{ typeof(ExcludedTag) }}, IncludeDisabled = true)] public partial struct Query{index} {{ }}"));
        GeneratorTestHost.TestRun run = await GeneratorTestHost.RunAsync($$"""
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Generated;

namespace Sample;

{{components}}
public struct RequiredTag : ITag { }
public struct ExcludedTag : ITag { }
{{queries}}
""");

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.AnalyzerDiagnostics);
        Assert.Empty(run.CompilationErrors);
        for (int arity = 1; arity <= 8; arity++)
        {
            string query = run.Generated[$"Sample_Query{arity}.Query.g.cs"];
            Assert.Contains($"public const int Arity = {arity};", query, StringComparison.Ordinal);
            Assert.Contains($"GeneratedQueryDescriptor<{string.Join(", ", Enumerable.Range(1, arity).Select(index => $"global::Sample.C{index}"))}>", query, StringComparison.Ordinal);
            Assert.Contains("ComponentType<global::Sample.RequiredTag>.Id", query, StringComparison.Ordinal);
            Assert.Contains("ComponentType<global::Sample.ExcludedTag>.Id", query, StringComparison.Ordinal);
            Assert.Contains("public const bool IncludeDisabled = true;", query, StringComparison.Ordinal);
        }
    }

    private static string FindLine(string source, string fragment)
    {
        return source.Split('\n').Single(line => line.Contains(fragment, StringComparison.Ordinal)).Trim();
    }
}
