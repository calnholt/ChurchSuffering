#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Effects;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Integration;

public sealed class Ecs046CompletenessAuditTests
{
    private static readonly IReadOnlyDictionary<string, string> CompletedDomainDocuments =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ECS-040"] = "ecs040-global-ui-mapping.md",
            ["ECS-041"] = "ecs-041-card-gameplay-mapping.md",
            ["ECS-042"] = "ecs042-combat-mapping.md",
            ["ECS-043"] = "ecs043-effects-equipment-medals.md",
            ["ECS-044"] = "ecs044-meta-game-mapping.md",
            ["ECS-045"] = "ecs045-presentation-rendering-mapping.md",
        };

    [Fact]
    public void Completed_domain_ledgers_have_mapping_evidence_for_every_key()
    {
        string root = FindRepositoryRoot();
        string migration = Path.Combine(root, "docs", "migration", "data-oriented-ecs");
        var documents = CompletedDomainDocuments.ToDictionary(
            pair => pair.Key,
            pair => File.ReadAllText(Path.Combine(migration, pair.Value)),
            StringComparer.Ordinal);
        string metaSource = string.Join('\n', Directory.EnumerateFiles(
                Path.Combine(root, "ECS", "DataOriented", "Gameplay", "Meta"), "*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));
        string effectSource = string.Join('\n', Directory.EnumerateFiles(
                Path.Combine(root, "ECS", "DataOriented", "Gameplay", "Effects"), "*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));
        var missing = new List<string>();
        var auditedRows = 0;

        foreach ((string file, string typeColumn) in new[]
                 {
                     ("components.csv", "LegacyType"),
                     ("events.csv", "LegacyType"),
                     ("systems.csv", "LegacyType"),
                     ("event-subscriptions.csv", "EventType"),
                     ("object-behaviors.csv", "LegacyType"),
                 })
        {
            string[] lines = File.ReadAllLines(Path.Combine(migration, file));
            string[] headers = lines[0].Split(',');
            int keyIndex = Array.IndexOf(headers, "Key");
            int ownerIndex = Array.IndexOf(headers, "OwnerTask");
            int typeIndex = Array.IndexOf(headers, typeColumn);
            int sourceIndex = Array.IndexOf(headers, "Source");
            foreach (string line in lines.Skip(1))
            {
                string[] columns = line.Split(',');
                if (!CompletedDomainDocuments.ContainsKey(columns[ownerIndex]))
                    continue;

                auditedRows++;
                string document = documents[columns[ownerIndex]];
                bool mapped = document.Contains(columns[keyIndex], StringComparison.Ordinal) ||
                    (file == "event-subscriptions.csv"
                        ? document.Contains(Path.GetFileNameWithoutExtension(columns[sourceIndex]), StringComparison.Ordinal) ||
                          columns[ownerIndex] == "ECS-044" &&
                          columns[sourceIndex].StartsWith("ECS/Objects/Achievements/", StringComparison.Ordinal) &&
                          document.Contains("Nineteen achievement definitions", StringComparison.Ordinal)
                        : document.Contains(columns[typeIndex], StringComparison.Ordinal) ||
                          columns[ownerIndex] == "ECS-043" &&
                          columns[sourceIndex].StartsWith("ECS/Objects/Temperance/", StringComparison.Ordinal) &&
                          Regex.IsMatch(effectSource, $@"\b{Regex.Escape(columns[typeIndex])}\b") &&
                          document.Contains("Folded object behaviors", StringComparison.Ordinal) ||
                          columns[ownerIndex] == "ECS-044" &&
                          Regex.IsMatch(metaSource, $@"\b{Regex.Escape(columns[typeIndex])}\b") &&
                          (document.Contains("Same-named", StringComparison.Ordinal) ||
                           document.Contains("GeneratedMetaObjectCatalog", StringComparison.Ordinal)));
                if (!mapped)
                    missing.Add($"{file}:{columns[keyIndex]}");
            }
        }

        // ECS-050 removed the two Game1-owned legacy static-event subscriptions.
        Assert.Equal(1159, auditedRows);
        Assert.Empty(missing);
    }

    [Fact]
    public void New_domain_sources_have_no_legacy_runtime_or_private_event_runtime_dependencies()
    {
        string root = FindRepositoryRoot();
        string dataOriented = Path.Combine(root, "ECS", "DataOriented");
        (string Name, Regex Pattern)[] forbidden =
        [
            ("old ECS namespace", new Regex(@"Crusaders30XX\.ECS\.Core", RegexOptions.CultureInvariant)),
            ("static EventManager", new Regex(@"\bEventManager\b", RegexOptions.CultureInvariant)),
            ("static typed event stream", new Regex(@"\bstatic\s+(?:readonly\s+)?(?:EventStream|IEventStream)\s*<", RegexOptions.CultureInvariant)),
            ("direct hardware state", new Regex(@"\b(?:MouseState|GamePadState|KeyboardState)\b|\b(?:Mouse|GamePad|Keyboard)\.GetState\s*\(", RegexOptions.CultureInvariant)),
            ("legacy component lookup", new Regex(@"\b(?:GetComponent|TryGetComponent|HasComponent|GetEntitiesWithComponent|GetEntitiesWithComponents)\s*<", RegexOptions.CultureInvariant)),
            ("LINQ query", new Regex(@"using\s+System\.Linq\s*;|\.(?:Where|Select|SelectMany|OrderBy|OrderByDescending|ThenBy|ThenByDescending|Intersect|ToList|First|FirstOrDefault)\s*\(", RegexOptions.CultureInvariant)),
            ("private EventRuntime", new Regex(@"\bnew\s+EventRuntime\s*\(|\.AttachEventRuntime\s*\(|public\s+EventRuntime\s+Attach\s*\(", RegexOptions.CultureInvariant)),
        ];

        var violations = new List<string>();
        foreach (string path in Directory.EnumerateFiles(dataOriented, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, path);
            bool ownsRootRuntime = relative == "ECS/DataOriented/Integration/DataOrientedGameRuntime.cs";
            if (relative is "ECS/DataOriented/Systems/SystemScheduler.cs" or "ECS/DataOriented/Events/World.Events.cs") continue;

            string source = File.ReadAllText(path);
            foreach ((string name, Regex pattern) in forbidden)
            {
                if (ownsRootRuntime && name == "private EventRuntime") continue;
                if (pattern.IsMatch(source))
                    violations.Add($"{relative}: {name}");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Ecs043_event_consumers_are_not_noop_scheduled_systems()
    {
        Type[] consumers =
        [
            typeof(PassiveEffectRuntimeSystem),
            typeof(EquipmentEffectRuntimeSystem),
            typeof(MedalEffectRuntimeSystem),
            typeof(TemperanceEffectRuntimeSystem),
        ];

        Assert.All(consumers, type => Assert.False(typeof(IGameSystem).IsAssignableFrom(type), type.FullName));
    }

    [Fact]
    public void Queued_rule_driver_declares_exclusive_world_access_and_root_ordering()
    {
        var rules = new QueuedRuleRuntime<AuditRuleState>(new AuditRuleEndpoint());
        SystemId predecessor = new(99901);
        var system = new EventQueueSystem<AuditRuleState>(rules, [predecessor]);
        SystemDescriptor descriptor = system.Descriptor;

        Assert.True(descriptor.RequiresExclusiveWorldAccess);
        Assert.True(descriptor.RecordsStructuralCommands);
        Assert.Equal(EventBarrier.AfterSystem, descriptor.EventBarrier);
        Assert.Equal([predecessor], descriptor.RunsAfter.ToArray());
    }

    [Fact]
    public void Root_combat_slot_rebinds_successive_sessions_and_rejects_stale_targets()
    {
        var world = new World(GeneratedComponentRegistry.Create());
        var hub = new CombatEventHub();
        var slot = new CombatSessionSlot(world);
        var consumers = new CombatOwnedEventConsumers(slot);
        var runtime = new EventRuntime(new EventRoutingEndpoint(hub.BuildRoutes(consumers.RegisterRoutes())));
        world.AttachEventRuntime(runtime);

        CombatSession first = CombatSession.Create(world, hub, EnemyId.TrainingDemon);
        slot.Bind(first);
        slot.Unbind(first);
        CombatSession second = CombatSession.Create(world, hub, EnemyId.TrainingDemon);
        slot.Bind(second);
        IGameSystem[] operational = CombatGameplayComposition.Create(slot).Systems.ToArray();

        Assert.Same(second, slot.RequireActive());
        Assert.All(operational, system => Assert.DoesNotContain(
            system.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            field => field.FieldType == typeof(CombatSession)));

        hub.SetThreat.Publish(new SetThreatEvent(second.Player, 9));
        runtime.DrainBarrier();
        Assert.Equal(9, world.Get<Threat>(second.Player).Amount);

        int staleHp = world.Get<HP>(first.Player).Current;
        hub.SetHp.Publish(new SetHpEvent(first.Player, 1));
        Assert.Throws<InvalidOperationException>(runtime.DrainBarrier);
        Assert.Equal(staleHp, world.Get<HP>(first.Player).Current);
    }

    private readonly record struct AuditRuleState;

    private sealed class AuditRuleEndpoint : IRuleRoutingEndpoint<AuditRuleState>
    {
        public RuleExecutionStatus Execute(
            RuleTypeId ruleType,
            ref AuditRuleState state,
            ref RuleExecutionContext<AuditRuleState> context) => RuleExecutionStatus.Completed;
    }

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
}
