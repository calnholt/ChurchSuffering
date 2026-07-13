#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Rendering;

public sealed class Ecs045IntegrationAuditTests
{
    private const string Owner = "ECS-045";

    [Fact]
    public void Ledger_and_mapping_cover_the_exact_ecs045_inventory()
    {
        string root = FindRepositoryRoot();
        string mapping = File.ReadAllText(Path.Combine(
            root, "docs", "migration", "data-oriented-ecs", "ecs045-presentation-rendering-mapping.md"));

        AssertLedgerCoverage(root, mapping, "components.csv", 9);
        AssertLedgerCoverage(root, mapping, "events.csv", 38);
        AssertLedgerCoverage(root, mapping, "systems.csv", 135);
        AssertLedgerCoverage(root, mapping, "event-subscriptions.csv", 175);
    }

    [Fact]
    public void Ledger_components_and_events_are_unmanaged_and_generated()
    {
        string root = FindRepositoryRoot();
        Assembly assembly = typeof(World).Assembly;
        Type[] presentationTypes = assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Crusaders30XX.ECS.DataOriented.Gameplay.Presentation", StringComparison.Ordinal) == true)
            .ToArray();

        string[] componentNames = ReadLedgerRows(root, "components.csv")
            .Select(row => row.LegacyType)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(9, componentNames.Length);
        foreach (string componentName in componentNames)
        {
            Type component = Assert.Single(presentationTypes, type => type.Name == componentName);
            Assert.True(typeof(IComponent).IsAssignableFrom(component), component.FullName);
            Assert.True(IsUnmanaged(component), component.FullName);
            Assert.True(GeneratedComponentRegistry.TryGetDescriptor(component.FullName!, out _), component.FullName);
        }

        string[] eventNames = ReadLedgerRows(root, "events.csv")
            .Select(row => row.LegacyType)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(38, eventNames.Length);
        foreach (string eventName in eventNames)
        {
            Type eventType = Assert.Single(presentationTypes, type => type.Name == eventName);
            Assert.True(IsUnmanaged(eventType), eventType.FullName);
        }
    }

    [Fact]
    public void Presentation_hub_exposes_and_routes_all_38_events()
    {
        var hub = new PresentationEventHub();
        IEventRoute[] routes = hub.BuildRoutes();
        Assert.Equal(38, routes.Length);
        Assert.Equal(routes.Length, routes.Select(route => route.EventTypeId).Distinct().Count());
        Assert.Equal(routes.Length, routes.Select(route => route.EventName).Distinct(StringComparer.Ordinal).Count());

        var world = new World(GeneratedComponentRegistry.Create());
        var runtime = new EventRuntime(new EventRoutingEndpoint(routes));
        world.AttachEventRuntime(runtime);
        PropertyInfo[] streams = typeof(PresentationEventHub)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => IsEventStream(property.PropertyType))
            .ToArray();

        Assert.Equal(38, streams.Length);
        Assert.Equal(38, streams.Select(property => property.PropertyType.GenericTypeArguments[0]).Distinct().Count());
        foreach (PropertyInfo property in streams)
        {
            object stream = property.GetValue(hub)!;
            Type payloadType = property.PropertyType.GenericTypeArguments[0];
            MethodInfo publish = property.PropertyType.GetMethod(nameof(EventStream<int>.Publish))!;
            publish.Invoke(stream, [Activator.CreateInstance(payloadType)]);
        }

        Assert.Equal(38, runtime.PendingEventCount);
        runtime.DrainBarrier();
        Assert.Equal(38, runtime.LastBarrierEventCount);
        Assert.Equal(0, runtime.PendingEventCount);
    }

    [Fact]
    public void System_ids_are_globally_unique_and_draw_consumers_have_no_world_access()
    {
        Assembly assembly = typeof(World).Assembly;
        var ids = new List<(string Name, SystemId Id)>();
        foreach (Type type in assembly.GetTypes())
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(SystemId))
                    ids.Add(($"{type.FullName}.{field.Name}", (SystemId)field.GetValue(null)!));
            }
        }

        Assert.Contains(ids, value => value.Name.Contains(nameof(PresentationSystemIds), StringComparison.Ordinal));
        Assert.All(ids, value => Assert.True(value.Id.IsValid, value.Name));
        Assert.Equal(ids.Count, ids.Select(value => value.Id.Value).Distinct().Count());

        Type drawConsumer = typeof(RenderPacketDrawConsumer);
        Assert.Empty(drawConsumer.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.DoesNotContain(drawConsumer.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(method => method.GetParameters()), parameter => parameter.ParameterType == typeof(World));
        Assert.DoesNotContain(drawConsumer.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters()), parameter => parameter.ParameterType == typeof(World));
        Assert.Equal(typeof(ReadOnlySpan<RenderPacket>), typeof(RenderPacketStore)
            .GetMethod(nameof(RenderPacketStore.GetLayer))!.ReturnType);
    }

    [Fact]
    public void Presentation_sources_have_no_hardware_polling_linq_or_obvious_hot_path_allocations()
    {
        string root = FindRepositoryRoot();
        (string Name, Regex Pattern)[] forbidden =
        [
            ("direct hardware input", new Regex(
                @"\b(?:MouseState|KeyboardState|GamePadState)\b|\b(?:Mouse|Keyboard|GamePad)\.GetState\s*\(",
                RegexOptions.CultureInvariant)),
            ("LINQ", new Regex(
                @"using\s+System\.Linq\s*;|\.(?:Where|Select|ToList|First|FirstOrDefault|OrderBy|OrderByDescending)\s*\(",
                RegexOptions.CultureInvariant)),
            ("hot collection allocation", new Regex(
                @"new\s+(?:List|Dictionary|HashSet|Queue|Stack)<|\.ToArray\s*\(",
                RegexOptions.CultureInvariant)),
            ("legacy event manager", new Regex(@"\bEventManager\b", RegexOptions.CultureInvariant)),
        ];

        var violations = new List<string>();
        foreach (string path in PresentationSourceFiles(root))
        {
            string source = File.ReadAllText(path);
            foreach ((string name, Regex pattern) in forbidden)
            {
                if (pattern.IsMatch(source))
                    violations.Add($"{Path.GetRelativePath(root, path)}: {name}");
            }
        }
        Assert.Empty(violations);
    }

    [Fact]
    public void New_world_snapshot_host_matches_all_46_legacy_fixture_ids_exactly()
    {
        string[] expected =
        [
            "achievement-detail", "achievement-overview", "assigned-block-rail", "battle-phase-transition",
            "booster-pack-opening", "brittle-card", "card", "card-list-modal-bottom", "card-list-modal-middle",
            "card-list-modal-top", "climb-active-events", "climb-character-dialog", "climb-character-event",
            "climb-character-hover-preview", "climb-character-summary", "climb-encounter-reward-modal",
            "climb-hazard-confirmation", "climb-hazard-event", "climb-hazard-hover-preview", "climb-header",
            "climb-hover-preview", "climb-inventory-equipment-tooltip", "climb-inventory-overlay",
            "climb-medal-tooltip-hover", "climb-no-events", "climb-replacement-modal",
            "climb-resource-acquisition", "climb-sold-shop-slot", "colorless-card", "cursed-card",
            "enemy-attack-banner", "enemy-damage-meter", "enemy-defeat-burst", "equipment-tooltip", "frozen-card",
            "guardian-angel", "hotkey-hints", "modular-fx", "narrative-event-modal", "passive-application",
            "pause-menu", "player-hud", "quest-reward-modal", "scorched-card", "thorned-card", "waystation",
        ];
        var host = new NewWorldSnapshotFixtureHost();
        NewWorldSnapshotFixture[] registered = host.Registered.ToArray();
        string[] actual = registered.Select(fixture => fixture.Id).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(46, registered.Length);
        Assert.Equal(registered.Length, registered.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(expected, actual);
        Assert.All(expected, id => Assert.True(host.TryResolve(id, out _), id));
    }

    private static void AssertLedgerCoverage(string root, string mapping, string csv, int expected)
    {
        LedgerRow[] rows = ReadLedgerRows(root, csv);
        Assert.Equal(expected, rows.Length);
        Assert.All(rows, row => Assert.Contains(row.Key, mapping, StringComparison.Ordinal));
    }

    private static LedgerRow[] ReadLedgerRows(string root, string csv)
    {
        string[] lines = File.ReadAllLines(Path.Combine(root, "docs", "migration", "data-oriented-ecs", csv));
        string[] headers = lines[0].Split(',');
        int key = Array.IndexOf(headers, "Key");
        int legacyType = Array.IndexOf(headers, "LegacyType");
        if (legacyType < 0) legacyType = Array.IndexOf(headers, "EventType");
        int ownerTask = Array.IndexOf(headers, "OwnerTask");
        return lines.Skip(1)
            .Select(line => line.Split(','))
            .Where(columns => columns.Length > Math.Max(ownerTask, Math.Max(key, legacyType)) && columns[ownerTask] == Owner)
            .Select(columns => new LedgerRow(columns[key], columns[legacyType]))
            .ToArray();
    }

    private static bool IsEventStream(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventStream<>);

    private static bool IsUnmanaged(Type type)
    {
        if (!type.IsValueType) return false;
        if (type.IsPrimitive || type.IsEnum || type.IsPointer) return true;
        return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .All(field => IsUnmanaged(field.FieldType));
    }

    private static string[] PresentationSourceFiles(string root) =>
        Directory.GetFiles(Path.Combine(root, "ECS", "DataOriented"), "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Presentation{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                           path.Contains($"{Path.DirectorySeparatorChar}Rendering{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            // Static host command/output catalogs allocate only during process bootstrap and are
            // not part of presentation update, extraction, or draw hot paths.
            .Where(path => !path.EndsWith("SnapshotLaunchOutputCatalog.cs", StringComparison.Ordinal))
            .ToArray();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Crusaders30XX.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private readonly record struct LedgerRow(string Key, string LegacyType);
}
