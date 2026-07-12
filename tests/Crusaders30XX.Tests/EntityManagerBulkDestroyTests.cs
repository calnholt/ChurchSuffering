using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class EntityManagerBulkDestroyTests
{
	private sealed class TestComponent : IComponent
	{
		public Entity Owner { get; set; }
	}

	private sealed class DisposableComponent : IComponent, IDisposable
	{
		public Entity Owner { get; set; }
		public bool WasDisposed { get; private set; }
		public void Dispose() => WasDisposed = true;
	}

	[Fact]
	public void DestroyEntities_RemovesMatchesFromEntitiesAndComponentIndexes()
	{
		var manager = new EntityManager();
		var first = manager.CreateEntity("first");
		var second = manager.CreateEntity("second");
		var retained = manager.CreateEntity("retained");
		manager.AddComponent(first, new TestComponent());
		manager.AddComponent(second, new TestComponent());
		manager.AddComponent(retained, new TestComponent());

		int destroyed = manager.DestroyEntities(entity => entity != retained);

		Assert.Equal(2, destroyed);
		Assert.Null(manager.GetEntity(first.Id));
		Assert.Null(manager.GetEntity(second.Id));
		Assert.Same(retained, manager.GetEntity(retained.Id));
		Assert.Equal(new[] { retained }, manager.GetEntitiesWithComponent<TestComponent>().ToArray());
	}

	[Fact]
	public void DestroyEntities_DisposesComponentsExactlyWhenEntityMatches()
	{
		var manager = new EntityManager();
		var removed = manager.CreateEntity("removed");
		var retained = manager.CreateEntity("retained");
		var removedComponent = new DisposableComponent();
		var retainedComponent = new DisposableComponent();
		manager.AddComponent(removed, removedComponent);
		manager.AddComponent(retained, retainedComponent);

		int destroyed = manager.DestroyEntities(entity => entity == removed);

		Assert.Equal(1, destroyed);
		Assert.True(removedComponent.WasDisposed);
		Assert.False(retainedComponent.WasDisposed);
	}
}
