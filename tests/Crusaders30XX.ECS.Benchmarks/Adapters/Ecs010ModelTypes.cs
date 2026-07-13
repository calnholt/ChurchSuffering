using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.Benchmarks.Adapters;

public struct HarnessPosition : IComponent
{
    public int X;
    public int Y;
}

public struct HarnessVelocity : IComponent
{
    public int X;
    public int Y;
}

public struct HarnessHealth : IComponent
{
    public int Current;
    public int Maximum;
}

public struct HarnessAuxiliary : IComponent
{
    public int First;
    public int Second;
}

public readonly struct HarnessPrimaryTag : ITag;

public readonly struct HarnessSecondaryTag : ITag;

internal static class FoundationRegistryFactory
{
    public static ComponentTypeRegistry Create()
    {
        var registry = new ComponentTypeRegistry();
        registry.RegisterComponent<HarnessPosition>(0);
        registry.RegisterComponent<HarnessVelocity>(1);
        registry.RegisterComponent<HarnessHealth>(2);
        registry.RegisterTag<HarnessPrimaryTag>(3);
        registry.RegisterTag<HarnessSecondaryTag>(4);
        registry.RegisterComponent<HarnessAuxiliary>(5);
        registry.Seal();
        return registry;
    }
}
