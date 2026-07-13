#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Generated;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented;

public sealed class Ecs020ComponentLayoutTests
{
    [Fact]
    public void Shared_components_are_unmanaged_and_have_deterministic_sizes()
    {
        AssertComponent<ActionPoints>(4);
        AssertComponent<ActorPresentationState>(24);
        AssertComponent<Animation>(12);
        AssertComponent<BattlePresentationTransform>(16);
        AssertComponent<Courage>(4);
        AssertComponent<EntityMetadata>(4);
        AssertComponent<HP>(12);
        AssertComponent<InputContext>(12);
        AssertComponent<Intellect>(4);
        AssertComponent<MaxHandSize>(4);
        AssertComponent<OwnedByScene>(1);
        AssertComponent<ParallaxLayer>(16);
        AssertComponent<ParentTransform>(8);
        AssertComponent<PositionTween>(24);
        AssertComponent<SceneState>(1);
        AssertComponent<Sprite>(28);
        AssertComponent<Temperance>(4);
        AssertComponent<Threat>(4);
        AssertComponent<TooltipMetadata>(16);
        AssertComponent<Transform>(24);
        AssertComponent<UIElement>(28);
    }

    [Fact]
    public void Persistence_tags_are_fieldless_and_generated_as_tags()
    {
        AssertTag<DontDestroyOnLoad>();
        AssertTag<DontDestroyOnReload>();

        Assert.True(GeneratedComponentRegistry.TryGetDescriptor(
            typeof(DontDestroyOnLoad).FullName!,
            out GeneratedComponentDescriptor loadDescriptor));
        Assert.True(loadDescriptor.IsTag);
        Assert.True(GeneratedComponentRegistry.TryGetDescriptor(
            typeof(DontDestroyOnReload).FullName!,
            out GeneratedComponentDescriptor reloadDescriptor));
        Assert.True(reloadDescriptor.IsTag);
    }

    private static void AssertComponent<T>(int expectedSize)
        where T : unmanaged, IComponent
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        Assert.Equal(expectedSize, Unsafe.SizeOf<T>());
        Assert.True(GeneratedComponentRegistry.TryGetDescriptor(
            typeof(T).FullName!,
            out GeneratedComponentDescriptor descriptor));
        Assert.False(descriptor.IsTag);
    }

    private static void AssertTag<T>()
        where T : unmanaged, ITag
    {
        FieldInfo[] fields = typeof(T).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.DoesNotContain(fields, field => !field.IsStatic);
    }
}
