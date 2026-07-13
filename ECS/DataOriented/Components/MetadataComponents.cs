#nullable enable

using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;

namespace Crusaders30XX.ECS.DataOriented.Components;

/// <summary>
/// Optional indexed metadata for diagnostics and content-authored entity names.
/// Entity identity and enabled state remain world-owned.
/// </summary>
public struct EntityMetadata : IComponent
{
    public StringId Name;
}
