using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class VisualEffectRequestFactoryTests
{
	[Fact]
	public void Card_attack_effect_uses_player_as_source_and_enemy_as_target()
	{
		var entityManager = BuildBattleActors(out var player, out var enemy);
		var card = CreateCard(entityManager, new Smite(), new Vector2(900f, 700f));

		var request = VisualEffectRequestFactory.ForCard(
			entityManager,
			card,
			card.GetComponent<CardData>().Card.VisualEffectRecipe);

		Assert.NotNull(request);
		Assert.Same(player, request.Source);
		Assert.Same(enemy, request.Target);
		Assert.Equal("smite", request.SourceId);
		Assert.Equal(VisualEffectSourceKind.Card, request.SourceKind);
	}

	[Fact]
	public void Player_targeted_card_effect_uses_player_as_source_and_target()
	{
		var entityManager = BuildBattleActors(out var player, out _);
		var card = CreateCard(entityManager, new Smite(), new Vector2(900f, 700f));

		var request = VisualEffectRequestFactory.ForCard(
			entityManager,
			card,
			VisualEffectPresets.PlayerBuff());

		Assert.NotNull(request);
		Assert.Same(player, request.Source);
		Assert.Same(player, request.Target);
		Assert.Equal(VisualEffectSourceKind.Card, request.SourceKind);
	}

	[Fact]
	public void Attack_card_sequence_does_not_request_player_buff_effects()
	{
		var entityManager = BuildBattleActors(out var player, out var enemy);
		var card = CreateCard(entityManager, new CrimsonRite(), new Vector2(900f, 700f));

		var requests = VisualEffectRequestFactory.ForCardSequence(
			entityManager,
			card,
			card.GetComponent<CardData>().Card.VisualEffectSequence);

		Assert.NotEmpty(requests);
		Assert.All(requests, request => Assert.Same(enemy, request.Target));
		Assert.DoesNotContain(card.GetComponent<CardData>().Card.VisualEffectSequence.Beats,
			beat => beat.Modules.Contains(VisualEffectModule.ActorSquashStretch));
		Assert.DoesNotContain(card.GetComponent<CardData>().Card.VisualEffectSequence.Beats,
			beat => beat.StartSfx == SfxTrack.Prayer || beat.StartSfx == SfxTrack.GainAegis);
		Assert.All(requests, request => Assert.True(request.TimingOverride.HasValue));
	}

	private static EntityManager BuildBattleActors(out Entity player, out Entity enemy)
	{
		var entityManager = new EntityManager();
		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new Transform { Position = new Vector2(100f, 300f) });

		enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy());
		entityManager.AddComponent(enemy, new Transform { Position = new Vector2(700f, 300f) });

		return entityManager;
	}

	private static Entity CreateCard(EntityManager entityManager, CardBase cardBase, Vector2 position)
	{
		var card = entityManager.CreateEntity("Card");
		entityManager.AddComponent(card, new CardData { Card = cardBase });
		entityManager.AddComponent(card, new Transform { Position = position });
		return card;
	}
}
