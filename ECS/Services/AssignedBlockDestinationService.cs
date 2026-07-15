using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;

namespace Crusaders30XX.ECS.Services;

/// <summary>
/// Read-only destination rules for spent card blockers.
/// </summary>
public static class AssignedBlockDestinationService
{
	public static CardZoneType Resolve(Entity card)
	{
		var destinationOverride = card?.GetComponent<AssignedBlockDestinationOverride>();
		if (destinationOverride != null) return destinationOverride.Destination;
		return card?.GetComponent<ExhaustOnBlock>() != null
			? CardZoneType.ExhaustPile
			: CardZoneType.DiscardPile;
	}

	public static string GetMoveReason(CardZoneType destination) => destination switch
	{
		CardZoneType.DrawPile => "AssignedBlockToDrawPile",
		CardZoneType.ExhaustPile => "AssignedBlockToExhaust",
		_ => "AssignedBlockToDiscard",
	};
}
