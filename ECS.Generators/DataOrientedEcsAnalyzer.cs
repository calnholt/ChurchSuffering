using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Crusaders30XX.ECS.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DataOrientedEcsAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DiagnosticDescriptors.ComponentMustBeStruct,
        DiagnosticDescriptors.ComponentMustBeUnmanaged,
        DiagnosticDescriptors.TagMustBeStruct,
        DiagnosticDescriptors.TagMustBeEmpty,
        DiagnosticDescriptors.AmbiguousComponentKind,
        DiagnosticDescriptors.TypeLimitExceeded,
        DiagnosticDescriptors.DuplicateStableId,
        DiagnosticDescriptors.QueryMustBePartialStruct,
        DiagnosticDescriptors.InvalidQueryArity,
        DiagnosticDescriptors.InvalidQueryType,
        DiagnosticDescriptors.DuplicateQueryType,
        DiagnosticDescriptors.GenericEcsTypeNotSupported,
        DiagnosticDescriptors.InaccessibleEcsType,
        DiagnosticDescriptors.InvalidStableId);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            INamedTypeSymbol? componentInterface = startContext.Compilation.GetTypeByMetadataName(SymbolFacts.ComponentMetadataName);
            INamedTypeSymbol? tagInterface = startContext.Compilation.GetTypeByMetadataName(SymbolFacts.TagMetadataName);
            var ecsTypes = new ConcurrentBag<INamedTypeSymbol>();
            var queries = new ConcurrentBag<QueryInfo>();

            startContext.RegisterSymbolAction(symbolContext =>
            {
                var type = (INamedTypeSymbol)symbolContext.Symbol;
                AnalyzeEcsType(symbolContext, type, componentInterface, tagInterface, ecsTypes);
                AnalyzeQuery(symbolContext, type, componentInterface, tagInterface, queries);
            }, SymbolKind.NamedType);

            startContext.RegisterCompilationEndAction(endContext =>
            {
                AnalyzeTypeLimit(endContext, ecsTypes);
                AnalyzeDuplicateStableIds(endContext, queries);
            });
        });
    }

    private static void AnalyzeEcsType(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        INamedTypeSymbol? componentInterface,
        INamedTypeSymbol? tagInterface,
        ConcurrentBag<INamedTypeSymbol> ecsTypes)
    {
        bool isComponent = SymbolFacts.Implements(type, componentInterface);
        bool isTag = SymbolFacts.Implements(type, tagInterface);
        if (!isComponent && !isTag)
        {
            return;
        }

        ecsTypes.Add(type);
        Location location = SymbolFacts.Location(type);
        if (isComponent && isTag)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.AmbiguousComponentKind,
                location,
                type.ToDisplayString()));
            return;
        }

        if (type.Arity != 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GenericEcsTypeNotSupported,
                location,
                type.ToDisplayString()));
        }

        if (!SymbolFacts.IsGeneratorAccessible(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InaccessibleEcsType,
                location,
                type.ToDisplayString()));
        }

        if (isComponent)
        {
            if (type.TypeKind != TypeKind.Struct)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ComponentMustBeStruct,
                    location,
                    type.ToDisplayString()));
            }
            else if (!type.IsUnmanagedType)
            {
                IFieldSymbol? field = SymbolFacts.FirstManagedField(type);
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ComponentMustBeUnmanaged,
                    field?.Locations.FirstOrDefault() ?? location,
                    type.ToDisplayString(),
                    field?.Name ?? "<unknown>"));
            }
        }
        else if (type.TypeKind != TypeKind.Struct)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TagMustBeStruct,
                location,
                type.ToDisplayString()));
        }
        else
        {
            IFieldSymbol? field = SymbolFacts.FirstInstanceField(type);
            if (field is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TagMustBeEmpty,
                    field.Locations.FirstOrDefault() ?? location,
                    type.ToDisplayString(),
                    field.Name));
            }
        }
    }

    private static void AnalyzeQuery(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        INamedTypeSymbol? componentInterface,
        INamedTypeSymbol? tagInterface,
        ConcurrentBag<QueryInfo> queries)
    {
        AttributeData? attribute = type.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass?.ToDisplayString() == SymbolFacts.QueryAttributeMetadataName);
        if (attribute is null)
        {
            return;
        }

        string? stableId = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : null;
        queries.Add(new QueryInfo(type, stableId));
        Location location = SymbolFacts.Location(type);

        if (type.TypeKind != TypeKind.Struct || type.ContainingType is not null || type.Arity != 0 || !SymbolFacts.IsPartial(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.QueryMustBePartialStruct,
                location,
                type.ToDisplayString()));
        }

        if (string.IsNullOrWhiteSpace(stableId))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidStableId,
                location,
                type.ToDisplayString()));
        }

        ImmutableArray<ITypeSymbol> returnedTypes = GetReturnedTypes(attribute);
        if (returnedTypes.Length is < 1 or > 8)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidQueryArity,
                location,
                type.ToDisplayString(),
                returnedTypes.Length));
        }

        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (ITypeSymbol returnedType in returnedTypes)
        {
            if (returnedType is not INamedTypeSymbol named || !SymbolFacts.Implements(named, componentInterface))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidQueryType,
                    location,
                    type.ToDisplayString(),
                    returnedType.ToDisplayString(),
                    "a returned type",
                    "IComponent"));
            }
            else if (!seen.Add(returnedType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateQueryType,
                    location,
                    type.ToDisplayString(),
                    returnedType.ToDisplayString()));
            }
        }

        AnalyzeFilter(context, type, attribute, "All", componentInterface, tagInterface);
        AnalyzeFilter(context, type, attribute, "Any", componentInterface, tagInterface);
        AnalyzeFilter(context, type, attribute, "None", componentInterface, tagInterface);
    }

    private static void AnalyzeFilter(
        SymbolAnalysisContext context,
        INamedTypeSymbol queryType,
        AttributeData attribute,
        string filterName,
        INamedTypeSymbol? componentInterface,
        INamedTypeSymbol? tagInterface)
    {
        KeyValuePair<string, TypedConstant> argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == filterName);
        if (argument.Key is null || argument.Value.Kind != TypedConstantKind.Array)
        {
            return;
        }

        foreach (TypedConstant value in argument.Value.Values)
        {
            if (value.Value is not INamedTypeSymbol named ||
                (!SymbolFacts.Implements(named, componentInterface) && !SymbolFacts.Implements(named, tagInterface)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidQueryType,
                    SymbolFacts.Location(queryType),
                    queryType.ToDisplayString(),
                    (value.Value as ITypeSymbol)?.ToDisplayString() ?? "<invalid>",
                    $"the {filterName} filter",
                    "IComponent or ITag"));
            }
        }
    }

    private static ImmutableArray<ITypeSymbol> GetReturnedTypes(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length < 2 || attribute.ConstructorArguments[1].Kind != TypedConstantKind.Array)
        {
            return ImmutableArray<ITypeSymbol>.Empty;
        }

        return attribute.ConstructorArguments[1].Values
            .Select(value => value.Value)
            .OfType<ITypeSymbol>()
            .ToImmutableArray();
    }

    private static void AnalyzeTypeLimit(CompilationAnalysisContext context, ConcurrentBag<INamedTypeSymbol> ecsTypes)
    {
        int count = ecsTypes.Distinct(SymbolEqualityComparer.Default).Count();
        if (count > 512)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TypeLimitExceeded,
                Location.None,
                count));
        }
    }

    private static void AnalyzeDuplicateStableIds(CompilationAnalysisContext context, ConcurrentBag<QueryInfo> queries)
    {
        foreach (IGrouping<string, QueryInfo> group in queries
                     .Where(query => !string.IsNullOrWhiteSpace(query.StableId))
                     .GroupBy(query => query.StableId!, System.StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            QueryInfo first = group.OrderBy(query => SymbolFacts.MetadataName(query.Type), System.StringComparer.Ordinal).First();
            foreach (QueryInfo duplicate in group.Where(query => !SymbolEqualityComparer.Default.Equals(query.Type, first.Type)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateStableId,
                    SymbolFacts.Location(duplicate.Type),
                    group.Key,
                    first.Type.ToDisplayString()));
            }
        }
    }

    private sealed class QueryInfo
    {
        public QueryInfo(INamedTypeSymbol type, string? stableId)
        {
            Type = type;
            StableId = stableId;
        }

        public INamedTypeSymbol Type { get; }

        public string? StableId { get; }
    }
}
