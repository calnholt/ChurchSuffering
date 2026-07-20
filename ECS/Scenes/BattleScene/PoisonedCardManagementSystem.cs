using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems;

/// <summary>Applies the temporary poisoned-card consequence for the current block phase.</summary>
public sealed class PoisonedCardManagementSystem : Core.System
{
    private bool _appliedThisEnemyTurn;
    private readonly HashSet<int> _damagedCardIds = new();

    public PoisonedCardManagementSystem(EntityManager entityManager) : base(entityManager)
    {
        EventManager.Subscribe<ChangeBattlePhaseEvent>(OnPhaseChanged, priority: 10);
        EventManager.Subscribe<CardBlockedEvent>(OnCardBlocked);
        EventManager.Subscribe<BeginDefeatPresentationEvent>(OnBeginDefeatPresentation);
        EventManager.Subscribe<EnemyPhaseResetEvent>(_ => Clear());
        EventManager.Subscribe<DeleteCachesEvent>(_ => Clear());
    }

    protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
    protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

    private void OnPhaseChanged(ChangeBattlePhaseEvent evt)
    {
        if (evt.Current == SubPhase.EnemyStart)
        {
            _appliedThisEnemyTurn = false;
            _damagedCardIds.Clear();
            return;
        }

        if (evt.Current == SubPhase.PreBlock && !_appliedThisEnemyTurn)
        {
            ApplyOnePoisonedCard();
            _appliedThisEnemyTurn = true;
            return;
        }

        if (evt.Current == SubPhase.EnemyEnd) Clear();
    }

    private void ApplyOnePoisonedCard()
    {
        var player = EntityManager.GetEntity("Player");
        int stacks = player?.GetComponent<AppliedPassives>()?.Passives.TryGetValue(AppliedPassiveType.Poison, out int value) == true
            ? value
            : 0;
        if (stacks <= 0) return;

        var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
        var candidates = deck?.Hand
            .Where(card => card.GetComponent<Pledge>() == null
                && card.GetComponent<Poisoned>() == null
                && BlockValueService.GetTotalBlockValue(card) > 0)
            .ToList();
        if (candidates == null || candidates.Count == 0) return;

        Entity card = candidates[Random.Shared.Next(candidates.Count)];
        EntityManager.AddComponent(card, new Poisoned { Owner = card });
    }

    private void OnCardBlocked(CardBlockedEvent evt)
    {
        if (evt?.Card?.GetComponent<Poisoned>() == null ||
            !_damagedCardIds.Add(evt.Card.Id)) return;

        var player = EntityManager.GetEntity("Player");
        if (player == null) return;
        EventManager.Publish(new ModifyHpRequestEvent
        {
            Source = player,
            Target = player,
            Delta = -1,
            DamageType = ModifyTypeEnum.Effect,
        });
        EventManager.Publish(new PassiveTriggered { Owner = player, Type = AppliedPassiveType.Poison });
    }

    private void OnBeginDefeatPresentation(BeginDefeatPresentationEvent evt)
    {
        if (evt?.IsPreview != true) Clear();
    }

    private void Clear()
    {
        foreach (var card in EntityManager.GetEntitiesWithComponent<Poisoned>().ToList())
        {
            EntityManager.RemoveComponent<Poisoned>(card);
        }
        _appliedThisEnemyTurn = false;
        _damagedCardIds.Clear();
    }
}
