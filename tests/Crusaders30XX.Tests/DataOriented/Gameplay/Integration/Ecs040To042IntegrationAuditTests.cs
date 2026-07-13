#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Integration;

public sealed class Ecs040To042IntegrationAuditTests
{
    private static readonly DomainAudit[] Domains =
    [
        new("ECS-040", 10, 15, 11, 51, "ecs040-global-ui-mapping.md"),
        new("ECS-041", 57, 58, 26, 90, "ecs-041-card-gameplay-mapping.md"),
        new("ECS-042", 24, 32, 30, 90, "ecs042-combat-mapping.md"),
    ];

    [Fact]
    public void Generated_component_registry_is_unique_and_gameplay_has_no_manual_registration()
    {
        GeneratedComponentDescriptor[] descriptors = GeneratedComponentRegistry.Descriptors.ToArray();

        Assert.Equal(GeneratedComponentRegistry.Count, descriptors.Length);
        Assert.Equal(descriptors.Length, descriptors.Select(value => value.Id).Distinct().Count());
        Assert.Equal(descriptors.Length, descriptors.Select(value => value.MetadataName).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Enumerable.Range(0, descriptors.Length), descriptors.Select(value => value.Id));

        string root = FindRepositoryRoot();
        string[] violations = GameplaySourceFiles(root)
            .Where(path => Regex.IsMatch(
                File.ReadAllText(path),
                @"\bregistry\s*\.\s*Register(?:Component|Tag)\s*<",
                RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        Assert.Empty(violations);
    }

    [Fact]
    public void System_ids_are_unique_and_system_constructors_never_receive_systems()
    {
        Type[] idOwners = [typeof(GlobalUiSystemIds), typeof(CardGameplaySystemIds), typeof(CombatSystemIds)];
        var ids = new List<(string Name, SystemId Id)>();
        foreach (Type owner in idOwners)
        {
            foreach (FieldInfo field in owner.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(SystemId))
                    ids.Add(($"{owner.Name}.{field.Name}", (SystemId)field.GetValue(null)!));
            }
        }

        Assert.All(ids, value => Assert.True(value.Id.IsValid, value.Name));
        Assert.Equal(ids.Count, ids.Select(value => value.Id.Value).Distinct().Count());

        Type systemContract = typeof(IGameSystem);
        Type[] gameplaySystems = typeof(World).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && systemContract.IsAssignableFrom(type))
            .Where(type => type.Namespace?.StartsWith("Crusaders30XX.ECS.DataOriented.Gameplay", StringComparison.Ordinal) == true)
            .ToArray();
        string[] injectedSystems = gameplaySystems
            .SelectMany(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters()
                    .Where(parameter => systemContract.IsAssignableFrom(parameter.ParameterType))
                    .Select(parameter => $"{type.FullName}({parameter.ParameterType.FullName})")))
            .ToArray();
        Assert.Empty(injectedSystems);
    }

    [Fact]
    public void Descriptor_scene_groups_and_boundary_phases_are_sensible()
    {
        var world = new World(GeneratedComponentRegistry.Create());
        CardGameplayComposition cards = CardGameplayComposition.Create(world, new CardGameplayEventHub());
        IGameSystem[] cardSystems = cards.CompatibilitySystems.ToArray();
        Assert.Equal(26, cardSystems.Length);
        Assert.All(cardSystems, system => Assert.Equal(SceneGroup.Battle, system.Descriptor.SceneGroup));
        Assert.Equal(SystemPhase.Rules, cardSystems.Single(system => system is CardPlaySystem).Descriptor.Phase);
        Assert.Equal(SystemPhase.Interaction, cardSystems.Single(system => system is CardHoverDetectionSystem).Descriptor.Phase);
        Assert.Equal(SystemPhase.LatePresentation, cardSystems.Single(system => system is HandCardBoundsLateSystem).Descriptor.Phase);
        Assert.Equal(SystemPhase.RenderExtraction, cardSystems.Single(system => system is CardShaderCompositorSystem).Descriptor.Phase);
        Assert.Equal(2, cards.Systems.Length);
        Assert.IsType<DeckManagementSystem>(cards.Systems[0]);
        Assert.IsType<BattlePileInputSystem>(cards.Systems[1]);

        Type[] combatOperationalTypes = typeof(CombatSystemBase).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(CombatSystemBase).IsAssignableFrom(type))
            .ToArray();
        Assert.Equal(2, combatOperationalTypes.Length);
        Assert.Contains(typeof(AttackResolutionSystem), combatOperationalTypes);
        Assert.Contains(typeof(EnemyAttackProgressManagementSystem), combatOperationalTypes);
        Assert.All(combatOperationalTypes, type =>
            Assert.Equal(type, type.GetMethod(nameof(IGameSystem.Update))?.DeclaringType));
        Assert.DoesNotContain(combatOperationalTypes, type => type.GetConstructor(Type.EmptyTypes) is not null);

        Assert.Equal(SceneGroup.Global, new HighlightSettingsSystem().Descriptor.SceneGroup);
        Assert.Equal(SystemPhase.Presentation, new HighlightSettingsSystem().Descriptor.Phase);
    }

    [Fact]
    public void Ledger_event_types_are_unmanaged_and_event_stream_owners_are_instance_scoped()
    {
        string root = FindRepositoryRoot();
        Assembly assembly = typeof(World).Assembly;
        Type[] dataOrientedTypes = assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("Crusaders30XX.ECS.DataOriented", StringComparison.Ordinal) == true)
            .ToArray();

        foreach (DomainAudit domain in Domains)
        {
            string[] eventNames = ReadLedgerRows(root, "events.csv", domain.Owner)
                .Select(row => row.LegacyType)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(domain.Events, eventNames.Length);
            foreach (string eventName in eventNames)
            {
                Type[] matches = dataOrientedTypes.Where(type => type.Name == eventName).ToArray();
                Assert.NotEmpty(matches);
                Assert.All(matches, type => Assert.True(IsUnmanaged(type), type.FullName));
            }
        }

        string[] staticStreams = dataOrientedTypes
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(field => IsEventStream(field.FieldType))
                .Select(field => $"{type.FullName}.{field.Name}"))
            .Concat(dataOrientedTypes.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(property => IsEventStream(property.PropertyType))
                .Select(property => $"{type.FullName}.{property.Name}")))
            .ToArray();
        Assert.Empty(staticStreams);
    }

    [Fact]
    public void Mapping_documents_cover_exact_ledger_counts_and_every_assigned_type()
    {
        string root = FindRepositoryRoot();
        foreach (DomainAudit domain in Domains)
        {
            string document = File.ReadAllText(Path.Combine(root, "docs", "migration", "data-oriented-ecs", domain.Document));
            AssertLedgerCoverage(root, document, domain.Owner, "components.csv", domain.Components);
            AssertLedgerCoverage(root, document, domain.Owner, "events.csv", domain.Events);
            AssertLedgerCoverage(root, document, domain.Owner, "systems.csv", domain.Systems);
            Assert.Equal(domain.Subscriptions, ReadLedgerRows(root, "event-subscriptions.csv", domain.Owner).Length);
            Assert.Contains(domain.Subscriptions.ToString(), document, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void New_gameplay_source_has_no_forbidden_legacy_or_runtime_patterns()
    {
        string root = FindRepositoryRoot();
        (string Name, Regex Pattern)[] forbidden =
        [
            ("legacy ECS namespace", new Regex(@"Crusaders30XX\.ECS\.Core", RegexOptions.CultureInvariant)),
            ("static EventManager", new Regex(@"\bEventManager\b", RegexOptions.CultureInvariant)),
            ("direct MouseState", new Regex(@"\bMouseState\b|\bMouse\.GetState\s*\(", RegexOptions.CultureInvariant)),
            ("direct GamePad state", new Regex(@"\bGamePadState\b|\bGamePad\.GetState\s*\(", RegexOptions.CultureInvariant)),
            ("LINQ", new Regex(@"using\s+System\.Linq\s*;|\.(?:Where|Select|ToList|First|FirstOrDefault|OrderBy|OrderByDescending)\s*\(", RegexOptions.CultureInvariant)),
            ("render draw method", new Regex(@"\b(?:public|private|protected|internal)\s+(?:static\s+)?void\s+Draw\s*\(", RegexOptions.CultureInvariant)),
            ("gameplay service", new Regex(@"using\s+Crusaders30XX\.[^;]*\.Services\s*;|\b[A-Za-z0-9_]+Service\s*\.", RegexOptions.CultureInvariant)),
        ];

        var violations = new List<string>();
        foreach (string path in GameplaySourceFiles(root))
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
    public void Descriptor_shells_have_an_explicit_consolidated_or_ecs045_owner()
    {
        string root = FindRepositoryRoot();
        string audit = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "migration",
            "data-oriented-ecs",
            "ecs040-042-integration-audit.md"));
        string[] shells =
        [
            "HotKeyProgressRingSystem", "MusicManagerSystem", "SoundEffectManagerSystem", "UIElementHighlightSystem",
            "AssignedBlocksToDiscardSystem", "DrawHandSystem", "CanPlayCardHighlightSystem", "CantPlayCardMessageSystem",
            "CardApplicationManagementSystem", "CardHoverDetectionSystem", "DeckEmptyDeathCheckSystem",
            "DiscardSpecificCardHighlightSystem", "HandBlockInteractionSystem", "HandCardBoundsLateSystem",
            "MarkedForSpecificDiscardSystem", "CardListModalSystem", "CardShaderCompositorSystem", "CardUsageTrackingSystem",
            "AnathemaManagementSystem", "AssignedBlockLifecycleSystem", "BattleBackgroundSystem",
            "BattlePileGamepadInputSystem", "BattleSceneSystem", "BattleStateInfoManagementSystem",
            "CanPlayHighlightSettingsSystem", "CathedralLightingSystem", "CourageManagerSystem",
            "DesertBackgroundEffectSystem", "EnemyDamageManagerSystem", "EnemyDefeatFlowSystem",
            "EnemyIntentPipsSystem", "EnemyIntentPlanningSystem", "EnemyPhaseFlowSystem", "HpManagementSystem",
            "MarkedForEndOfTurnSystem", "ModularEffectActorPresentationSystem", "ModularEffectCoordinatorSystem",
            "MustBeBlockedSystem", "PhaseChangeEventSystem", "PhaseCoordinatorSystem", "PlayerHudFeedbackSystem",
            "PlayerWispParticleSystem", "TestFightFlowSystem", "ThornedManagementSystem",
            "TribulationManagerSystem", "WeaponManagementSystem",
        ];
        Assert.All(shells, shell => Assert.Contains(shell, audit, StringComparison.Ordinal));
        Assert.Contains("consolidated", audit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ECS-045", audit, StringComparison.Ordinal);
    }

    private static void AssertLedgerCoverage(string root, string document, string owner, string csv, int expected)
    {
        string[] names = ReadLedgerRows(root, csv, owner)
            .Select(row => row.LegacyType)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, names.Length);
        Assert.All(names, name => Assert.Contains(name, document, StringComparison.Ordinal));
    }

    private static LedgerRow[] ReadLedgerRows(string root, string csv, string owner)
    {
        string[] lines = File.ReadAllLines(Path.Combine(root, "docs", "migration", "data-oriented-ecs", csv));
        string[] headers = lines[0].Split(',');
        int legacyType = Array.IndexOf(headers, "LegacyType");
        if (legacyType < 0) legacyType = Array.IndexOf(headers, "EventType");
        int ownerTask = Array.IndexOf(headers, "OwnerTask");
        return lines.Skip(1)
            .Select(line => line.Split(','))
            .Where(columns => columns.Length > Math.Max(legacyType, ownerTask) && columns[ownerTask] == owner)
            .Select(columns => new LedgerRow(columns[legacyType]))
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

    private static string[] GameplaySourceFiles(string root) =>
        Directory.GetFiles(Path.Combine(root, "ECS", "DataOriented", "Gameplay"), "*.cs", SearchOption.AllDirectories);

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

    private readonly record struct DomainAudit(
        string Owner,
        int Components,
        int Events,
        int Systems,
        int Subscriptions,
        string Document);

    private readonly record struct LedgerRow(string LegacyType);
}
