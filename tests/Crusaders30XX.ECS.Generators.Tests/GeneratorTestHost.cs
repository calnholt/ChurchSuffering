using System.Collections.Immutable;
using System.Reflection;
using Crusaders30XX.ECS.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Crusaders30XX.ECS.Generators.Tests;

internal static class GeneratorTestHost
{
    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    public static async Task<TestRun> RunAsync(string userSource)
    {
        CSharpParseOptions parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        CSharpCompilation inputCompilation = CSharpCompilation.Create(
            "GeneratorTests",
            new[]
            {
                CSharpSyntaxTree.ParseText(RuntimeContracts, parseOptions),
                CSharpSyntaxTree.ParseText(userSource, parseOptions),
            },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[]
            {
                new DataOrientedEcsGenerator().AsSourceGenerator(),
                new DefinitionModuleGenerator().AsSourceGenerator(),
            },
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);
        ImmutableArray<Diagnostic> analyzerDiagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new DataOrientedEcsAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        GeneratorDriverRunResult result = driver.GetRunResult();
        Dictionary<string, string> generated = result.Results
            .SelectMany(item => item.GeneratedSources)
            .ToDictionary(item => item.HintName, item => item.SourceText.ToString(), StringComparer.Ordinal);
        return new TestRun(outputCompilation, generated, generatorDiagnostics, analyzerDiagnostics);
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        string[] trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return trustedAssemblies.Select(path => MetadataReference.CreateFromFile(path)).ToImmutableArray<MetadataReference>();
    }

    internal sealed class TestRun
    {
        public TestRun(
            Compilation compilation,
            IReadOnlyDictionary<string, string> generated,
            ImmutableArray<Diagnostic> generatorDiagnostics,
            ImmutableArray<Diagnostic> analyzerDiagnostics)
        {
            Compilation = compilation;
            Generated = generated;
            GeneratorDiagnostics = generatorDiagnostics;
            AnalyzerDiagnostics = analyzerDiagnostics;
        }

        public Compilation Compilation { get; }

        public IReadOnlyDictionary<string, string> Generated { get; }

        public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }

        public ImmutableArray<Diagnostic> AnalyzerDiagnostics { get; }

        public ImmutableArray<Diagnostic> CompilationErrors => Compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
    }

    private const string RuntimeContracts = """
namespace Crusaders30XX.ECS.DataOriented.Core
{
    public interface IComponent { }
    public interface ITag { }

    public readonly struct ComponentSignature
    {
        public static ComponentSignature Empty => default;
        public ComponentSignature With(int typeId) => this;
    }

    public static class ComponentType<T>
    {
        public static int Id => 0;
        public static bool IsTag => false;
    }

    public sealed class ComponentTypeRegistry
    {
        public void RegisterComponent<T>(int id) where T : unmanaged, IComponent { }
        public void RegisterTag<T>(int id) where T : unmanaged, ITag { }
        public void Seal() { }
    }

    public struct SpawnBundle
    {
        public void Add<T>(in T value) where T : unmanaged, IComponent { }
        public void AddTag<T>() where T : unmanaged, ITag { }
    }
}

namespace Crusaders30XX.ECS.Data.Ids
{
    public enum CardId : ushort { Strike = 0, Block = 1 }
    public enum EnemyId : ushort { Demon = 0, Ogre = 1 }
    public enum EnemyAttackId : ushort { Strike = 0, Slam = 1 }
    public enum EquipmentId : ushort { Sword = 0, Shield = 1 }
    public enum MedalId : ushort { Courage = 0, Faith = 1 }
}

namespace Crusaders30XX.ECS.DataOriented.Definitions
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public abstract class DefinitionModuleAttribute : System.Attribute
    {
        protected DefinitionModuleAttribute(System.Type idType, ushort value)
        {
            IdType = idType;
            Value = value;
        }

        public System.Type IdType { get; }
        public ushort Value { get; }
        public string? Handler { get; set; }
    }

    public sealed class CardDefinitionAttribute(Crusaders30XX.ECS.Data.Ids.CardId id)
        : DefinitionModuleAttribute(typeof(Crusaders30XX.ECS.Data.Ids.CardId), (ushort)id);
    public sealed class EnemyDefinitionAttribute(Crusaders30XX.ECS.Data.Ids.EnemyId id)
        : DefinitionModuleAttribute(typeof(Crusaders30XX.ECS.Data.Ids.EnemyId), (ushort)id);
    public sealed class EnemyAttackDefinitionAttribute(Crusaders30XX.ECS.Data.Ids.EnemyAttackId id)
        : DefinitionModuleAttribute(typeof(Crusaders30XX.ECS.Data.Ids.EnemyAttackId), (ushort)id);
    public sealed class EquipmentDefinitionAttribute(Crusaders30XX.ECS.Data.Ids.EquipmentId id)
        : DefinitionModuleAttribute(typeof(Crusaders30XX.ECS.Data.Ids.EquipmentId), (ushort)id);
    public sealed class MedalDefinitionAttribute(Crusaders30XX.ECS.Data.Ids.MedalId id)
        : DefinitionModuleAttribute(typeof(Crusaders30XX.ECS.Data.Ids.MedalId), (ushort)id);

    [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class DefinitionCatalogAttribute(System.Type idType) : System.Attribute
    {
        public System.Type IdType { get; } = idType;
        public bool RequireComplete { get; set; } = true;
    }

    public readonly record struct EffectSpec(int Id, int Magnitude);
    public readonly record struct ConditionSpec(int Id, int Threshold);
    public readonly record struct DefinitionCatalogEntry(
        bool IsDefined,
        int EffectOffset,
        int EffectCount,
        int ConditionOffset,
        int ConditionCount,
        bool HasHandler);
    public readonly record struct DefinitionDebugMetadata<TId>(
        TId Id,
        string IdName,
        string ModuleName,
        int EffectOffset,
        int EffectCount,
        int ConditionOffset,
        int ConditionCount,
        bool HasHandler)
        where TId : unmanaged, System.Enum;
}
""";
}
