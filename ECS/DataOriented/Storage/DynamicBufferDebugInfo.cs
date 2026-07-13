#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.DataOriented.Storage;

public readonly record struct DynamicBufferDebugInfo(
    Type ElementType,
    int Index,
    int Generation,
    EntityId Owner,
    int Count,
    int Capacity);
