namespace Crusaders30XX.ECS.Generators.Tests;

using Xunit;

public sealed class DataOrientedEcsAnalyzerTests
{
    [Theory]
    [InlineData("public sealed class Bad : IComponent { }", "ECSGEN001")]
    [InlineData("public struct Bad : IComponent { public string Value; }", "ECSGEN002")]
    [InlineData("public sealed class Bad : ITag { }", "ECSGEN003")]
    [InlineData("public struct Bad : ITag { public int Value; }", "ECSGEN004")]
    [InlineData("public struct Bad : IComponent, ITag { }", "ECSGEN005")]
    [InlineData("public struct Bad<T> : IComponent where T : unmanaged { public T Value; }", "ECSGEN012")]
    public async Task Invalid_component_and_tag_declarations_fail_compilation(string declaration, string expectedId)
    {
        GeneratorTestHost.TestRun run = await GeneratorTestHost.RunAsync($$"""
using Crusaders30XX.ECS.DataOriented.Core;
namespace Sample;
{{declaration}}
""");

        Assert.Contains(run.AnalyzerDiagnostics, diagnostic => diagnostic.Id == expectedId);
    }

    [Fact]
    public async Task More_than_512_registered_types_reports_hard_limit()
    {
        string declarations = string.Join('\n', Enumerable.Range(0, 513)
            .Select(index => $"public struct Component{index} : IComponent {{ public int Value; }}"));
        GeneratorTestHost.TestRun run = await GeneratorTestHost.RunAsync($$"""
using Crusaders30XX.ECS.DataOriented.Core;
namespace Sample;
{{declarations}}
""");

        var diagnostic = Assert.Single(run.AnalyzerDiagnostics, item => item.Id == "ECSGEN006");
        Assert.Contains("513", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_stable_query_ids_are_rejected()
    {
        GeneratorTestHost.TestRun run = await GeneratorTestHost.RunAsync("""
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Generated;
namespace Sample;
public struct Position : IComponent { public int X; }
[EcsQuery("duplicate", typeof(Position))] public partial struct FirstQuery { }
[EcsQuery("duplicate", typeof(Position))] public partial struct SecondQuery { }
""");

        Assert.Contains(run.AnalyzerDiagnostics, diagnostic => diagnostic.Id == "ECSGEN007");
    }

    [Theory]
    [InlineData("[EcsQuery(\"bad\")] public partial struct BadQuery { }", "ECSGEN009")]
    [InlineData("[EcsQuery(\"bad\", typeof(Position), typeof(Position))] public partial struct BadQuery { }", "ECSGEN011")]
    [InlineData("[EcsQuery(\"bad\", typeof(Tag))] public partial struct BadQuery { }", "ECSGEN010")]
    [InlineData("[EcsQuery(\"bad\", typeof(Position))] public struct BadQuery { }", "ECSGEN008")]
    public async Task Invalid_query_declarations_report_diagnostics(string declaration, string expectedId)
    {
        GeneratorTestHost.TestRun run = await GeneratorTestHost.RunAsync($$"""
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Generated;
namespace Sample;
public struct Position : IComponent { public int X; }
public struct Tag : ITag { }
{{declaration}}
""");

        Assert.Contains(run.AnalyzerDiagnostics, diagnostic => diagnostic.Id == expectedId);
    }

    [Fact]
    public async Task Nine_returned_components_report_query_arity_diagnostic()
    {
        string declarations = string.Join('\n', Enumerable.Range(1, 9)
            .Select(index => $"public struct C{index} : IComponent {{ public int Value; }}"));
        string typeArguments = string.Join(", ", Enumerable.Range(1, 9).Select(index => $"typeof(C{index})"));
        GeneratorTestHost.TestRun run = await GeneratorTestHost.RunAsync($$"""
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Generated;
namespace Sample;
{{declarations}}
[EcsQuery("too-wide", {{typeArguments}})] public partial struct WideQuery { }
""");

        Assert.Contains(run.AnalyzerDiagnostics, diagnostic => diagnostic.Id == "ECSGEN009");
    }
}
