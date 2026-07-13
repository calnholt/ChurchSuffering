using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Crusaders30XX.ECS.Generators;

internal sealed class EcsTypeModel
{
    public EcsTypeModel(INamedTypeSymbol symbol, bool isTag)
    {
        Symbol = symbol;
        IsTag = isTag;
        MetadataName = SymbolFacts.MetadataName(symbol);
        TypeReference = SymbolFacts.TypeReference(symbol);
    }

    public INamedTypeSymbol Symbol { get; }

    public bool IsTag { get; }

    public string MetadataName { get; }

    public string TypeReference { get; }
}

internal sealed class QueryModel
{
    public QueryModel(
        INamedTypeSymbol symbol,
        string stableId,
        ImmutableArray<ITypeSymbol> returned,
        ImmutableArray<ITypeSymbol> all,
        ImmutableArray<ITypeSymbol> any,
        ImmutableArray<ITypeSymbol> none,
        bool includeDisabled)
    {
        Symbol = symbol;
        StableId = stableId;
        Returned = returned;
        All = all;
        Any = any;
        None = none;
        IncludeDisabled = includeDisabled;
    }

    public INamedTypeSymbol Symbol { get; }

    public string StableId { get; }

    public ImmutableArray<ITypeSymbol> Returned { get; }

    public ImmutableArray<ITypeSymbol> All { get; }

    public ImmutableArray<ITypeSymbol> Any { get; }

    public ImmutableArray<ITypeSymbol> None { get; }

    public bool IncludeDisabled { get; }
}
