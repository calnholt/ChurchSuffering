using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public class TransformResolverServiceTests
{
	[Fact]
	public void Unparented_ui_bounds_remain_world_space()
	{
		var entityManager = new EntityManager();
		var entity = entityManager.CreateEntity("Unparented");
		entityManager.AddComponent(entity, new Transform { Position = new Vector2(100, 200) });
		var ui = new UIElement { Bounds = new Rectangle(10, 20, 30, 40) };
		entityManager.AddComponent(entity, ui);

		Assert.Equal(new Vector2(100, 200), TransformResolverService.ResolveWorldPosition(entityManager, entity));
		Assert.Equal(new Rectangle(110, 220, 30, 40), TransformResolverService.ResolveLocalBounds(
			entityManager,
			entity,
			ui.Bounds));
		Assert.Equal(ui.Bounds, TransformResolverService.ResolveUIBounds(entityManager, entity, ui));
	}

	[Fact]
	public void Parented_position_and_bounds_accumulate_translation()
	{
		var entityManager = new EntityManager();
		var root = entityManager.CreateEntity("Root");
		entityManager.AddComponent(root, new Transform { Position = new Vector2(100, 200) });
		var parent = entityManager.CreateEntity("Parent");
		entityManager.AddComponent(parent, new Transform { Position = new Vector2(30, 40) });
		entityManager.AddComponent(parent, new ParentTransform { Parent = root });
		var child = entityManager.CreateEntity("Child");
		entityManager.AddComponent(child, new Transform { Position = new Vector2(5, 7) });
		entityManager.AddComponent(child, new ParentTransform { Parent = parent });
		var ui = new UIElement { Bounds = new Rectangle(1, 2, 20, 10) };
		entityManager.AddComponent(child, ui);

		Assert.Equal(new Vector2(135, 247), TransformResolverService.ResolveWorldPosition(entityManager, child));
		Assert.Equal(new Rectangle(136, 249, 20, 10), TransformResolverService.ResolveUIBounds(
			entityManager,
			child,
			ui));
	}

	[Fact]
	public void Missing_parent_falls_back_to_own_transform()
	{
		var entityManager = new EntityManager();
		var parent = entityManager.CreateEntity("Parent");
		entityManager.AddComponent(parent, new Transform { Position = new Vector2(100, 200) });
		var child = entityManager.CreateEntity("Child");
		entityManager.AddComponent(child, new Transform { Position = new Vector2(5, 7) });
		entityManager.AddComponent(child, new ParentTransform { Parent = parent });
		entityManager.DestroyEntity(parent.Id);

		Assert.Equal(new Vector2(5, 7), TransformResolverService.ResolveWorldPosition(entityManager, child));
		Assert.Equal(new Rectangle(6, 9, 20, 10), TransformResolverService.ResolveLocalBounds(
			entityManager,
			child,
			new Rectangle(1, 2, 20, 10)));
	}

	[Fact]
	public void Cyclic_parent_chain_falls_back_to_each_entitys_own_transform()
	{
		var entityManager = new EntityManager();
		var first = entityManager.CreateEntity("First");
		entityManager.AddComponent(first, new Transform { Position = new Vector2(10, 20) });
		var second = entityManager.CreateEntity("Second");
		entityManager.AddComponent(second, new Transform { Position = new Vector2(30, 40) });
		entityManager.AddComponent(first, new ParentTransform { Parent = second });
		entityManager.AddComponent(second, new ParentTransform { Parent = first });

		Assert.Equal(new Vector2(10, 20), TransformResolverService.ResolveWorldPosition(entityManager, first));
		Assert.Equal(new Vector2(30, 40), TransformResolverService.ResolveWorldPosition(entityManager, second));
	}
}
