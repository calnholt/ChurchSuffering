using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Crusaders30XX.ECS.Generators;

internal sealed class DefinitionModuleModel
{
    public DefinitionModuleModel(
        INamedTypeSymbol symbol,
        INamedTypeSymbol idType,
        ushort value,
        string idName,
        IPropertySymbol definition,
        IPropertySymbol? upgrade,
        IPropertySymbol? effects,
        IPropertySymbol? conditions,
        IMethodSymbol? handler,
        Location location)
    {
        Symbol = symbol;
        IdType = idType;
        Value = value;
        IdName = idName;
        Definition = definition;
        Upgrade = upgrade;
        Effects = effects;
        Conditions = conditions;
        Handler = handler;
        Location = location;
    }

    public INamedTypeSymbol Symbol { get; }
    public INamedTypeSymbol IdType { get; }
    public ushort Value { get; }
    public string IdName { get; }
    public IPropertySymbol Definition { get; }
    public IPropertySymbol? Upgrade { get; }
    public IPropertySymbol? Effects { get; }
    public IPropertySymbol? Conditions { get; }
    public IMethodSymbol? Handler { get; }
    public Location Location { get; }
}

internal sealed class DefinitionDomainModel
{
    public DefinitionDomainModel(INamedTypeSymbol idType, IReadOnlyList<DefinitionModuleModel> modules)
    {
        IdType = idType;
        Modules = modules;
    }

    public INamedTypeSymbol IdType { get; }
    public IReadOnlyList<DefinitionModuleModel> Modules { get; }
}
