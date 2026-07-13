#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;

namespace Crusaders30XX.ECS.DataOriented.Definitions;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public abstract class DefinitionModuleAttribute : Attribute
{
    protected DefinitionModuleAttribute(Type idType, ushort value)
    {
        IdType = idType;
        Value = value;
    }

    public Type IdType { get; }

    public ushort Value { get; }

    public string? Handler { get; set; }
}

public sealed class CardDefinitionAttribute(CardId id)
    : DefinitionModuleAttribute(typeof(CardId), (ushort)id);

public sealed class EnemyDefinitionAttribute(EnemyId id)
    : DefinitionModuleAttribute(typeof(EnemyId), (ushort)id);

public sealed class EnemyAttackDefinitionAttribute(EnemyAttackId id)
    : DefinitionModuleAttribute(typeof(EnemyAttackId), (ushort)id);

public sealed class EquipmentDefinitionAttribute(EquipmentId id)
    : DefinitionModuleAttribute(typeof(EquipmentId), (ushort)id);

public sealed class MedalDefinitionAttribute(MedalId id)
    : DefinitionModuleAttribute(typeof(MedalId), (ushort)id);

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class DefinitionCatalogAttribute(Type idType) : Attribute
{
    public Type IdType { get; } = idType;

    public bool RequireComplete { get; set; } = true;
}
