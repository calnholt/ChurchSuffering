#nullable enable

using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.DataOriented.Components;

public struct Courage : IComponent
{
    public int Amount;
}

public struct ActionPoints : IComponent
{
    public int Current;
}

public struct Temperance : IComponent
{
    public int Amount;
}

public struct Threat : IComponent
{
    public int Amount;
}

public struct Intellect : IComponent
{
    public int Value;
}

public struct MaxHandSize : IComponent
{
    public const int DefaultValue = 4;

    public int Value;
}

public struct HP : IComponent
{
    public int Max;
    public int Current;
    public int UnscarredMax;
}
