#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;

public readonly record struct EntityInspectionRecord(
    EntityId Entity,
    ComponentSignature Signature,
    byte Alive,
    byte Enabled);

public readonly record struct WorldDiagnosticSnapshot(
    int EntityCount,
    int ArchetypeCount,
    long StructuralMoveCount,
    int PendingEventCount);

/// <summary>Reusable profiler and entity-inspector data, with formatting left to debug UI adapters.</summary>
public sealed class PresentationDiagnosticsStore
{
    private SystemProfileSnapshot[] profiles;
    private EntityInspectionRecord[] entities;
    private int profileCount;
    private int entityCount;

    public PresentationDiagnosticsStore(int initialCapacity = 32)
    {
        if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        profiles = new SystemProfileSnapshot[initialCapacity];
        entities = new EntityInspectionRecord[initialCapacity];
    }

    public ReadOnlySpan<SystemProfileSnapshot> Profiles => profiles.AsSpan(0, profileCount);
    public ReadOnlySpan<EntityInspectionRecord> Entities => entities.AsSpan(0, entityCount);
    public WorldDiagnosticSnapshot World { get; private set; }

    public void CaptureProfiles(SystemScheduler scheduler, ReadOnlySpan<SystemId> systems)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        Ensure(ref profiles, systems.Length);
        profileCount = 0;
        for (var index = 0; index < systems.Length; index++)
        {
            if (scheduler.TryGetProfile(systems[index], out SystemProfileSnapshot profile))
                profiles[profileCount++] = profile;
        }
    }

    public void Inspect(World world, ReadOnlySpan<EntityId> requested)
    {
        ArgumentNullException.ThrowIfNull(world);
        Ensure(ref entities, requested.Length);
        entityCount = requested.Length;
        for (var index = 0; index < requested.Length; index++)
        {
            EntityId entity = requested[index];
            bool alive = world.IsAlive(entity);
            entities[index] = new EntityInspectionRecord(
                entity,
                alive ? world.GetSignature(entity) : default,
                alive ? (byte)1 : (byte)0,
                alive && world.IsEnabled(entity) ? (byte)1 : (byte)0);
        }

        World = new WorldDiagnosticSnapshot(
            world.EntityCount,
            world.ArchetypeCount,
            world.StructuralMoveCount,
            world.HasEventRuntime ? world.Events.PendingEventCount : 0);
    }

    private static void Ensure<T>(ref T[] values, int required)
    {
        if (required <= values.Length) return;
        Array.Resize(ref values, Math.Max(required, Math.Max(4, values.Length * 2)));
    }
}

public readonly record struct NewWorldSnapshotFixture(string Id, SceneGroup Scene, int VariantCount);

/// <summary>
/// Isolated new-world snapshot registration. It intentionally does not capture, verify,
/// accept, or touch legacy baseline images.
/// </summary>
public sealed class NewWorldSnapshotFixtureHost
{
    private static readonly NewWorldSnapshotFixture[] Fixtures =
    [
        new("card", SceneGroup.Snapshot, 3),
        new("brittle-card", SceneGroup.Snapshot, 1),
        new("frozen-card", SceneGroup.Snapshot, 2),
        new("thorned-card", SceneGroup.Snapshot, 2),
        new("scorched-card", SceneGroup.Snapshot, 1),
        new("cursed-card", SceneGroup.Snapshot, 1),
        new("colorless-card", SceneGroup.Snapshot, 3),
        new("quest-reward-modal", SceneGroup.Snapshot, 1),
        new("booster-pack-opening", SceneGroup.Snapshot, 7),
        new("modular-fx", SceneGroup.Snapshot, 1),
        new("passive-application", SceneGroup.Snapshot, 3),
        new("narrative-event-modal", SceneGroup.Snapshot, 4),
        new("waystation", SceneGroup.WayStation, 1),
        new("player-hud", SceneGroup.Battle, 6),
        new("equipment-tooltip", SceneGroup.Battle, 3),
        new("enemy-damage-meter", SceneGroup.Battle, 4),
        new("enemy-attack-banner", SceneGroup.Battle, 6),
        new("assigned-block-rail", SceneGroup.Battle, 6),
        new("enemy-defeat-burst", SceneGroup.Battle, 3),
        new("guardian-angel", SceneGroup.Battle, 5),
        new("pause-menu", SceneGroup.Snapshot, 2),
        new("hotkey-hints", SceneGroup.Snapshot, 3),
        new("battle-phase-transition", SceneGroup.Battle, 7),
        new("achievement-overview", SceneGroup.Achievement, 1),
        new("achievement-detail", SceneGroup.Achievement, 1),
        new("climb-no-events", SceneGroup.Climb, 1),
        new("climb-hazard-event", SceneGroup.Climb, 1),
        new("climb-character-event", SceneGroup.Climb, 1),
        new("climb-hazard-hover-preview", SceneGroup.Climb, 1),
        new("climb-character-hover-preview", SceneGroup.Climb, 1),
        new("climb-hazard-confirmation", SceneGroup.Climb, 1),
        new("climb-character-summary", SceneGroup.Climb, 1),
        new("climb-character-dialog", SceneGroup.Climb, 1),
        new("climb-active-events", SceneGroup.Climb, 1),
        new("climb-hover-preview", SceneGroup.Climb, 1),
        new("climb-medal-tooltip-hover", SceneGroup.Climb, 1),
        new("climb-sold-shop-slot", SceneGroup.Climb, 1),
        new("climb-encounter-reward-modal", SceneGroup.Climb, 1),
        new("climb-replacement-modal", SceneGroup.Climb, 1),
        new("climb-inventory-overlay", SceneGroup.Climb, 1),
        new("climb-inventory-equipment-tooltip", SceneGroup.Climb, 1),
        new("card-list-modal-top", SceneGroup.Climb, 1),
        new("card-list-modal-middle", SceneGroup.Climb, 1),
        new("card-list-modal-bottom", SceneGroup.Climb, 1),
        new("climb-header", SceneGroup.Climb, 4),
        new("climb-resource-acquisition", SceneGroup.Climb, 4),
    ];

    public ReadOnlySpan<NewWorldSnapshotFixture> Registered => Fixtures;

    public bool TryResolve(ReadOnlySpan<char> id, out NewWorldSnapshotFixture fixture)
    {
        for (var index = 0; index < Fixtures.Length; index++)
        {
            if (id.Equals(Fixtures[index].Id, StringComparison.Ordinal))
            {
                fixture = Fixtures[index];
                return true;
            }
        }

        fixture = default;
        return false;
    }
}
