using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CardRenderBoundsTests
{
	[Fact]
	public void Cursed_only_surface_stays_close_to_the_card_bounds()
	{
		(EntityManager entityManager, Entity card) = CreateCard();
		entityManager.AddComponent(card, new Cursed());

		Rectangle bounds = CardRenderBoundsService.GetBounds(
			entityManager,
			card,
			new Vector2(960, 540),
			1f,
			0f);

		Assert.InRange(bounds.Width, CardGeometrySettings.DefaultWidth, CardGeometrySettings.DefaultWidth + 10);
		Assert.InRange(bounds.Height, CardGeometrySettings.DefaultHeight, CardGeometrySettings.DefaultHeight + 10);
	}

	[Fact]
	public void Frozen_and_brittle_padding_cover_breath_and_falling_debris()
	{
		(EntityManager entityManager, Entity card) = CreateCard();
		entityManager.AddComponent(card, new Frozen());
		entityManager.AddComponent(card, new Brittle());

		Rectangle bounds = CardRenderBoundsService.GetBounds(
			entityManager,
			card,
			new Vector2(960, 540),
			1f,
			0f);

		Assert.True(bounds.Width > CardGeometrySettings.DefaultWidth + 100);
		Assert.True(bounds.Height > CardGeometrySettings.DefaultHeight * 2.5f);
		Assert.True(bounds.Y < 540 - CardGeometrySettings.DefaultHeight);
	}

	[Fact]
	public void Rotation_produces_an_axis_aligned_surface_for_the_expanded_card()
	{
		(EntityManager entityManager, Entity card) = CreateCard();
		entityManager.AddComponent(card, new Scorched());

		Rectangle unrotated = CardRenderBoundsService.GetBounds(
			entityManager,
			card,
			new Vector2(960, 540),
			1f,
			0f);
		Rectangle rotated = CardRenderBoundsService.GetBounds(
			entityManager,
			card,
			new Vector2(960, 540),
			1f,
			MathHelper.PiOver2);

		Assert.InRange(rotated.Width, unrotated.Height - 2, unrotated.Height + 2);
		Assert.InRange(rotated.Height, unrotated.Width - 2, unrotated.Width + 2);
	}

	[Fact]
	public void Cached_base_bounds_cover_rotated_corners_without_status_overflow()
	{
		(EntityManager entityManager, Entity card) = CreateCard();
		entityManager.AddComponent(card, new Frozen());

		Rectangle baseBounds = CardRenderBoundsService.GetBaseBounds(
			entityManager,
			card,
			new Vector2(960, 540),
			1f,
			MathHelper.ToRadians(20f));
		Rectangle statusBounds = CardRenderBoundsService.GetBounds(
			entityManager,
			card,
			new Vector2(960, 540),
			1f,
			MathHelper.ToRadians(20f));

		Assert.True(baseBounds.Width > CardGeometrySettings.DefaultWidth);
		Assert.True(baseBounds.Height > CardGeometrySettings.DefaultHeight);
		Assert.True(statusBounds.Width > baseBounds.Width);
		Assert.True(statusBounds.Height > baseBounds.Height);
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
