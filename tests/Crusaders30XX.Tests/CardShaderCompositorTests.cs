using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CardShaderCompositorTests
{
	[Fact]
	public void Cursed_only_surface_stays_close_to_the_card_bounds()
	{
		(EntityManager entityManager, Entity card) = CreateCard();
		entityManager.AddComponent(card, new Cursed());

		CardShaderCompositorSystem.CardSurfaceBounds bounds =
			CardShaderCompositorSystem.CalculateSurfaceBounds(entityManager, card, new Vector2(960, 540), 1f, 0f);

		Assert.InRange(bounds.Size.X, CardGeometrySettings.DefaultWidth, CardGeometrySettings.DefaultWidth + 10);
		Assert.InRange(bounds.Size.Y, CardGeometrySettings.DefaultHeight, CardGeometrySettings.DefaultHeight + 10);
	}

	[Fact]
	public void Frozen_and_brittle_padding_cover_breath_and_falling_debris()
	{
		(EntityManager entityManager, Entity card) = CreateCard();
		entityManager.AddComponent(card, new Frozen());
		entityManager.AddComponent(card, new Brittle());

		CardShaderCompositorSystem.CardSurfaceBounds bounds =
			CardShaderCompositorSystem.CalculateSurfaceBounds(entityManager, card, new Vector2(960, 540), 1f, 0f);

		Assert.True(bounds.Size.X > CardGeometrySettings.DefaultWidth + 100);
		Assert.True(bounds.Size.Y > CardGeometrySettings.DefaultHeight * 2.5f);
		Assert.True(bounds.Origin.Y < 540 - CardGeometrySettings.DefaultHeight);
	}

	[Fact]
	public void Rotation_produces_an_axis_aligned_surface_for_the_expanded_card()
	{
		(EntityManager entityManager, Entity card) = CreateCard();
		entityManager.AddComponent(card, new Scorched());

		CardShaderCompositorSystem.CardSurfaceBounds unrotated =
			CardShaderCompositorSystem.CalculateSurfaceBounds(entityManager, card, new Vector2(960, 540), 1f, 0f);
		CardShaderCompositorSystem.CardSurfaceBounds rotated =
			CardShaderCompositorSystem.CalculateSurfaceBounds(entityManager, card, new Vector2(960, 540), 1f, MathHelper.PiOver2);

		Assert.InRange(rotated.Size.X, unrotated.Size.Y - 2f, unrotated.Size.Y + 2f);
		Assert.InRange(rotated.Size.Y, unrotated.Size.X - 2f, unrotated.Size.X + 2f);
	}

	private static (EntityManager EntityManager, Entity Card) CreateCard()
	{
		var entityManager = new EntityManager();
		Entity settings = entityManager.CreateEntity("CardGeometrySettings");
		entityManager.AddComponent(settings, new CardGeometrySettings
		{
			CardWidth = CardGeometrySettings.DefaultWidth,
			CardHeight = CardGeometrySettings.DefaultHeight,
			CardOffsetYExtra = CardGeometrySettings.DefaultOffsetYExtra,
			CardGap = CardGeometrySettings.DefaultGap,
			CardCornerRadius = CardGeometrySettings.DefaultCornerRadius,
		});
		return (entityManager, entityManager.CreateEntity("Card"));
	}
}
