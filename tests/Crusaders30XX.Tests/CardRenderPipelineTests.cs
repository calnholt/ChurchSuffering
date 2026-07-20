using System;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CardRenderPipelineTests
{
    [Fact]
    public void Catalog_exposes_the_canonical_pass_order()
    {
        ShaderRuntimeOptions.ConfigureFromArgs(Array.Empty<string>());
        var entityManager = new EntityManager();

        ICardOverlayPass[] passes = CardOverlayPassCatalog.Create(entityManager, null);

        Assert.Equal(
            ["Brittle", "Frozen", "Thorned", "Scorched", "Cursed", "Poison", "CardSheen"],
            passes.Select(pass => pass.Name));
    }

    [Fact]
    public void Catalog_filters_passes_by_card_components_and_keeps_sheen_last()
    {
        ShaderRuntimeOptions.ConfigureFromArgs(Array.Empty<string>());
        var entityManager = new EntityManager();
        Entity card = entityManager.CreateEntity("Card");
        entityManager.AddComponent(card, new Brittle());
        entityManager.AddComponent(card, new Frozen());
        entityManager.AddComponent(card, new Thorned());
        entityManager.AddComponent(card, new Scorched());
        entityManager.AddComponent(card, new Cursed());
        entityManager.AddComponent(card, new Poisoned { Owner = card });
        entityManager.AddComponent(card, new CardSheen
        {
            Owner = card,
            IsActive = true,
            HasActivationTime = true,
        });
        ICardOverlayPass[] passes = CardOverlayPassCatalog.Create(entityManager, null);

        string[] applicable = passes
            .Where(pass => pass.AppliesTo(card))
            .Select(pass => pass.Name)
            .ToArray();

        Assert.Equal(
            ["Brittle", "Frozen", "Thorned", "Scorched", "Cursed", "Poison", "CardSheen"],
            applicable);
    }

    [Fact]
    public void Composition_is_bypassed_when_disabled_or_suppressed()
    {
        var entityManager = new EntityManager();
        Entity card = entityManager.CreateEntity("Card");
        entityManager.AddComponent(card, new Brittle());
        ICardOverlayPass[] passes = CardOverlayPassCatalog.Create(entityManager, null);

        Assert.False(CardRenderPipeline.ShouldComposite(card, passes, false));

        entityManager.AddComponent(card, new SuppressCardVisualEffects());
        Assert.False(CardRenderPipeline.ShouldComposite(card, passes, true));
    }

    [Fact]
    public void Inactive_sheen_does_not_trigger_composition()
    {
        ShaderRuntimeOptions.ConfigureFromArgs(Array.Empty<string>());
        var entityManager = new EntityManager();
        Entity card = entityManager.CreateEntity("Card");
        entityManager.AddComponent(card, new CardSheen { Owner = card });
        ICardOverlayPass[] passes = CardOverlayPassCatalog.Create(entityManager, null);

        Assert.False(CardRenderPipeline.ShouldComposite(card, passes, true));
    }
}
