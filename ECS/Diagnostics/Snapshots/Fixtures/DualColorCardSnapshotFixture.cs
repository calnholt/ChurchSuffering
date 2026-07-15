using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures;

public sealed class DualColorCardSnapshotFixture : IDisplaySnapshotFixture
{
	public string Id => "dual-color-card";
	public int WarmupFrames => 2;
	public string OutputFileName => "pairings";

	private readonly List<Entity> _cards = new();
	private Texture2D _pixel;

	public void Setup(DisplaySnapshotContext ctx, string[] args)
	{
		DestroyCards(ctx);

		var pairings = new[]
		{
			(CardData.CardColor.White, CardData.CardColor.Black),
			(CardData.CardColor.Red, CardData.CardColor.Black),
			(CardData.CardColor.Black, CardData.CardColor.White),
		};

		foreach (var (printedColor, secondaryColor) in pairings)
		{
			var card = EntityFactory.CreateCardFromDefinition(
				ctx.World.EntityManager,
				"strike",
				printedColor);
			if (card == null)
			{
				throw new DisplaySnapshotSetupException("Failed to create Dual Color snapshot card.");
			}

			ctx.World.EntityManager.AddComponent(card, new DualColor
			{
				Owner = card,
				SecondaryColor = secondaryColor,
			});
			var ui = card.GetComponent<UIElement>();
			if (ui != null) ui.IsInteractable = false;
			_cards.Add(card);
		}

		_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
		_pixel.SetData([Color.White]);
	}

	public void Draw(DisplaySnapshotContext ctx)
	{
		ctx.SpriteBatch.Draw(
			_pixel,
			new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
			new Color(28, 30, 34));

		const int cardWidth = 268;
		const int cardHeight = 377;
		const int gap = 40;
		int totalWidth = cardWidth * _cards.Count + gap * (_cards.Count - 1);
		int startX = (Game1.VirtualWidth - totalWidth) / 2;
		int startY = (Game1.VirtualHeight - cardHeight) / 2;

		for (int i = 0; i < _cards.Count; i++)
		{
			EventManager.Publish(new CardRenderScaledEvent
			{
				Card = _cards[i],
				Position = new Vector2(startX + i * (cardWidth + gap), startY),
				Scale = 1f,
			});
		}
	}

	private void DestroyCards(DisplaySnapshotContext ctx)
	{
		foreach (var card in _cards)
		{
			ctx.World.EntityManager.DestroyEntity(card.Id);
		}
		_cards.Clear();
	}
}
