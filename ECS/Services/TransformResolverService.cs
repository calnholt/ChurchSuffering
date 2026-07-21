using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Services
{
	public static class TransformResolverService
	{
		public static Vector2 ResolveWorldPosition(EntityManager entityManager, Entity entity)
		{
			var transform = entity?.GetComponent<Transform>();
			if (transform == null) return Vector2.Zero;

			Vector2 ownPosition = transform.Position;
			Vector2 worldPosition = ownPosition;
			var visited = new HashSet<int> { entity.Id };
			Entity current = entity;

			while (current.GetComponent<ParentTransform>() is ParentTransform parentTransform)
			{
				Entity parent = parentTransform.Parent;
				if (parent == null
					|| !parent.IsActive
					|| !ReferenceEquals(entityManager.GetEntity(parent.Id), parent)
					|| !visited.Add(parent.Id))
				{
					return ownPosition;
				}

				var parentEntityTransform = parent.GetComponent<Transform>();
				if (parentEntityTransform == null)
				{
					return ownPosition;
				}

				worldPosition += parentEntityTransform.Position;
				current = parent;
			}

			return worldPosition;
		}

		public static Rectangle ResolveLocalBounds(
			EntityManager entityManager,
			Entity entity,
			Rectangle localBounds)
		{
			if (entity?.GetComponent<Transform>() == null) return localBounds;

			Vector2 worldPosition = ResolveWorldPosition(entityManager, entity);
			return new Rectangle(
				(int)System.Math.Round(worldPosition.X + localBounds.X),
				(int)System.Math.Round(worldPosition.Y + localBounds.Y),
				localBounds.Width,
				localBounds.Height);
		}

		public static Rectangle ResolveUIBounds(
			EntityManager entityManager,
			Entity entity,
			UIElement uiElement)
		{
			if (uiElement == null) return Rectangle.Empty;

			return entity?.HasComponent<ParentTransform>() == true
				? ResolveLocalBounds(entityManager, entity, uiElement.Bounds)
				: uiElement.Bounds;
		}
	}
}
