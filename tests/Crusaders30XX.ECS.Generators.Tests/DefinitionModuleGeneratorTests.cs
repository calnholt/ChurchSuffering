using Microsoft.CodeAnalysis;
using Xunit;

namespace Crusaders30XX.ECS.Generators.Tests;

public sealed class DefinitionModuleGeneratorTests
{
    [Fact]
    public async Task Complete_modules_generate_dense_catalog_tables_and_direct_dispatch()
    {
        GeneratorTestHost.TestRun run = await GeneratorTestHost.RunAsync("""
using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Generated;

[assembly: DefinitionCatalog(typeof(CardId))]

namespace Sample;

public readonly record struct CardData(int Cost);
public readonly record struct UpgradeData(int Cost);
public struct PlayContext { public int Total; }

[CardDefinition(CardId.Strike, Handler = nameof(BuildPlayCommands))]
public static partial class StrikeCard
{
    public static CardData Definition => new(1);
    public static UpgradeData Upgrade => new(0);
    public static ReadOnlySpan<EffectSpec> Effects => new EffectSpec[] { new(7, 4) };
    public static ReadOnlySpan<ConditionSpec> Conditions => new ConditionSpec[] { new(3, 2) };
    public static void BuildPlayCommands(ref PlayContext context) => context.Total += 4;
}

[CardDefinition(CardId.Block)]
public static partial class BlockCard
{
    public static CardData Definition => new(2);
    public static UpgradeData Upgrade => new(1);
}

public static class CatalogSmokeTest
{
    public static int Run()
    {
        PlayContext context = default;
        bool dispatched = GeneratedCardCatalog.Dispatch(CardId.Strike, ref context);
        return dispatched && GeneratedCardCatalog.IsDefined(CardId.Block)
            ? context.Total + GeneratedCardCatalog.GetEffects(CardId.Strike).Length
            : -1;
    }
}
""");

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.AnalyzerDiagnostics);
        Assert.Empty(run.CompilationErrors);

        string catalog = run.Generated["GeneratedCardCatalog.g.cs"];
        Assert.Contains("public const int Capacity = 2;", catalog, StringComparison.Ordinal);
        Assert.Contains("public const int DefinitionCount = 2;", catalog, StringComparison.Ordinal);
        Assert.Contains("global::Sample.StrikeCard.BuildPlayCommands(ref context);", catalog, StringComparison.Ordinal);
        Assert.Contains("effects0.CopyTo(new global::System.Span", catalog, StringComparison.Ordinal);
        Assert.Contains("DefinitionDebugMetadata<", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Reflection", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Delegate", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Func", catalog, StringComparison.Ordinal);

        int dispatchStart = catalog.IndexOf("public static bool Dispatch", StringComparison.Ordinal);
        Assert.DoesNotContain("new ", catalog.Substring(dispatchStart), StringComparison.Ordinal);

        using var assemblyBytes = new MemoryStream();
        Assert.True(run.Compilation.Emit(assemblyBytes).Success);
        System.Reflection.Assembly assembly = System.Reflection.Assembly.Load(assemblyBytes.ToArray());
        object? result = assembly.GetType("Sample.CatalogSmokeTest")!
            .GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, null);
        Assert.Equal(5, result);
    }

    [Fact]
    public async Task Duplicate_ids_report_compile_time_diagnostic()
    {
        GeneratorTestHost.TestRun run = await GeneratorTestHost.RunAsync("""
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;

namespace Sample;

public readonly record struct CardData(int Cost);

[CardDefinition(CardId.Strike)]
public static partial class FirstCard
{
    public static CardData Definition => new(1);
}

[CardDefinition(CardId.Strike)]
public static partial class SecondCard
{
    public static CardData Definition => new(2);
}
""");

        Diagnostic diagnostic = Assert.Single(run.GeneratorDiagnostics.Where(item => item.Id == "ECSDEF001"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.DoesNotContain("GeneratedCardCatalog.g.cs", run.Generated.Keys);
    }

    [Fact]
    public async Task Complete_catalog_marker_reports_each_missing_stable_id()
    {
        GeneratorTestHost.TestRun run = await GeneratorTestHost.RunAsync("""
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;

[assembly: DefinitionCatalog(typeof(CardId))]

namespace Sample;

public readonly record struct CardData(int Cost);

[CardDefinition(CardId.Strike)]
public static partial class StrikeCard
{
    public static CardData Definition => new(1);
}
""");

        Diagnostic diagnostic = Assert.Single(run.GeneratorDiagnostics.Where(item => item.Id == "ECSDEF002"));
        Assert.Contains("Block", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mismatched_handler_contexts_are_rejected()
    {
        GeneratorTestHost.TestRun run = await GeneratorTestHost.RunAsync("""
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;

namespace Sample;

public readonly record struct CardData(int Cost);
public struct FirstContext { }
public struct SecondContext { }

[CardDefinition(CardId.Strike, Handler = nameof(Handle))]
public static partial class StrikeCard
{
    public static CardData Definition => new(1);
    public static void Handle(ref FirstContext context) { }
}

[CardDefinition(CardId.Block, Handler = nameof(Handle))]
public static partial class BlockCard
{
    public static CardData Definition => new(2);
    public static void Handle(ref SecondContext context) { }
}
""");

        Assert.Contains(run.GeneratorDiagnostics, diagnostic => diagnostic.Id == "ECSDEF005");
        Assert.DoesNotContain("GeneratedCardCatalog.g.cs", run.Generated.Keys);
    }
}
