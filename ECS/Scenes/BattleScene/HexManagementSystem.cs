using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems;

/// <summary>Owns the temporary Hex card cover and its end-of-turn cleanup.</summary>
public sealed class HexManagementSystem : Core.System
{
    public const string RestrictionName = "Hex";

    public HexManagementSystem(EntityManager entityManager) : base(entityManager)
    {
        EventManager.Subscribe<ApplyCardApplicationEvent>(OnApplyCardApplication);
        EventManager.Subscribe<RemoveCardApplication>(OnRemoveCardApplication);
        EventManager.Subscribe<CardPlayedEvent>(OnCardPlayed, priority: -100);
        EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
        EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
        EventManager.Subscribe<DeleteCachesEvent>(_ => RemoveAllHexes());
    }

    protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
    protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

    private void OnApplyCardApplication(ApplyCardApplicationEvent evt)
    {
        if (evt?.Type != CardApplicationType.Hex || evt.Amount <= 0) return;

        var cards = CardApplicationTargetingService.ResolveCandidates(EntityManager, evt.Card, evt.Target)
            .Where(CardApplicationTargetingService.IsEligibleForApplication)
            .Where(card => !card.HasComponent<Cursed>() && !card.HasComponent<Hexed>())
            .Where(card => !string.Equals(card.GetComponent<CardData>()?.Card?.CardId, Curse.CardIdValue, StringComparison.OrdinalIgnoreCase))
            .Where(card => !string.Equals(card.GetComponent<CardData>()?.Card?.CardId, Hex.CardIdValue, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(_ => Random.Shared.Next())
            .Take(evt.Amount)
            .ToList();

        foreach (var card in cards)
        {
            EventManager.Publish(new CardRestrictionMutationAnimationRequested
            {
                TargetCard = card,
                Type = CardApplicationType.Hex,
            });
        }
    }

    private void OnRemoveCardApplication(RemoveCardApplication evt)
    {
        if (evt?.Type == CardApplicationType.Hex) RemoveHexRuntime(EntityManager, evt.Card);
    }

    private void OnCardPlayed(CardPlayedEvent evt)
    {
        if (evt?.Card?.HasComponent<Hexed>() != true) return;
        ConvertPlayedHexToCurse(EntityManager, evt.Card);
    }

    private void OnChangeBattlePhase(ChangeBattlePhaseEvent evt)
    {
        if (evt?.Current == SubPhase.PlayerEnd) RemoveAllHexes();
    }

    private void OnLoadScene(LoadSceneEvent evt)
    {
        if (evt?.Scene != SceneId.Battle) RemoveAllHexes();
    }

    private void RemoveAllHexes()
    {
        foreach (var card in EntityManager.GetEntitiesWithComponent<Hexed>().ToList())
        {
            RemoveHexRuntime(EntityManager, card);
        }
    }

    public static void ApplyHexRuntime(EntityManager entityManager, Entity card)
    {
        if (entityManager == null || card == null || card.HasComponent<Hexed>() || card.HasComponent<Cursed>()) return;
        var cardData = card.GetComponent<CardData>();
        var currentCard = cardData?.Card;
        if (cardData == null || currentCard == null) return;

        if (card.GetComponent<CursedOriginalCard>() == null)
        {
            entityManager.AddComponent(card, new CursedOriginalCard
            {
                Owner = card,
                CardId = currentCard.CardId ?? string.Empty,
                Color = cardData.Color,
                IsUpgraded = currentCard.IsUpgraded,
                IsStarter = currentCard.IsStarter,
            });
        }

        var hex = CardFactory.Create(Hex.CardIdValue);
        if (hex == null) return;

        entityManager.AddComponent(card, new Hexed { Owner = card });
        cardData.Card = hex;
        hex.Initialize(entityManager, card);
        currentCard.Dispose();
        CursedManagementSystem.RefreshCoveredCardPresentation(entityManager, card);
    }

    public static void RemoveHexRuntime(EntityManager entityManager, Entity card)
    {
        if (entityManager == null || card == null || !card.HasComponent<Hexed>()) return;
        entityManager.RemoveComponent<Hexed>(card);
        CursedManagementSystem.RemoveCursedRuntime(entityManager, card);
    }

    private static void ConvertPlayedHexToCurse(EntityManager entityManager, Entity card)
    {
        if (entityManager == null || card == null || !card.HasComponent<Hexed>()) return;
        entityManager.RemoveComponent<Hexed>(card);
        CursedManagementSystem.ApplyCursedRuntime(entityManager, card);
        RunScopedStateService.SyncCardRestrictionsFromComponents(card);
    }
}
