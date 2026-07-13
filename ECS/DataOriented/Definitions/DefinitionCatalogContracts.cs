#nullable enable

using System;

namespace Crusaders30XX.ECS.DataOriented.Definitions;

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
    where TId : unmanaged, Enum;
