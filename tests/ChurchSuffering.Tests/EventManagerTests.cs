using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class EventManagerTests : IDisposable
{
	public EventManagerTests() => EventManager.Clear();
	public void Dispose() => EventManager.Clear();

	[Fact]
	public void Publish_uses_descending_priority_and_stable_registration_order()
	{
		var calls = new List<string>();
		EventManager.Subscribe<TestEvent>(_ => calls.Add("normal-first"));
		EventManager.Subscribe<TestEvent>(_ => calls.Add("high"), 10);
		EventManager.Subscribe<TestEvent>(_ => calls.Add("normal-second"));

		EventManager.Publish(new TestEvent());

		Assert.Equal(new[] { "high", "normal-first", "normal-second" }, calls);
	}

	[Fact]
	public void Subscription_changes_during_publish_apply_to_the_next_publish()
	{
		var calls = new List<string>();
		Action<TestEvent> removed = _ => calls.Add("removed");
		Action<TestEvent> added = _ => calls.Add("added");
		EventManager.Subscribe<TestEvent>(_ =>
		{
			calls.Add("mutator");
			EventManager.Unsubscribe(removed);
			EventManager.Subscribe(added);
		}, 10);
		EventManager.Subscribe(removed);

		EventManager.Publish(new TestEvent());
		Assert.Equal(new[] { "mutator", "removed" }, calls);

		calls.Clear();
		EventManager.Publish(new TestEvent());
		Assert.Equal(new[] { "mutator", "added" }, calls);
	}

	[Fact]
	public void PublishPartitioned_preserves_priority_order_and_measures_each_partition()
	{
		var calls = new List<string>();
		EventManager.Subscribe<TestEvent>(_ => calls.Add("low-first"));
		EventManager.Subscribe<TestEvent>(_ => calls.Add("high"), 100);
		EventManager.Subscribe<TestEvent>(_ => calls.Add("low-second"));

		EventManager.PublishPartitioned(
			new TestEvent(),
			100,
			action =>
			{
				calls.Add("high-start");
				action();
				calls.Add("high-end");
			},
			action =>
			{
				calls.Add("low-start");
				action();
				calls.Add("low-end");
			});

		Assert.Equal(
			new[] { "high-start", "high", "high-end", "low-start", "low-first", "low-second", "low-end" },
			calls);
	}

	private sealed class TestEvent { }
}
