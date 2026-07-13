#nullable enable

using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Components;

public struct SceneState : IComponent
{
    public SceneGroup Current;
}

/// <summary>Identifies the scene whose teardown owns this entity.</summary>
public struct OwnedByScene : IComponent
{
    public SceneGroup Scene;
}

/// <summary>Excludes an entity from ordinary scene-transition teardown.</summary>
public struct DontDestroyOnLoad : ITag
{
}

/// <summary>Excludes an entity from same-scene reload teardown.</summary>
public struct DontDestroyOnReload : ITag
{
}
