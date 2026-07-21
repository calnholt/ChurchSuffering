using System;
using System.Collections.Generic;

namespace ChurchSuffering.ECS.Core
{
	/// <summary>
	/// Simple global scheduler that runs actions after a delay in seconds.
	/// </summary>
	public static class TimerScheduler
	{
		private class ScheduledItem
		{
			public float RemainingSeconds;
			public Action Callback;
			public bool Cancelled;
		}

		private static readonly List<ScheduledItem> _items = new();
		private static readonly Queue<ScheduledItem> _toAdd = new();

		/// <summary>
		/// Schedule an action to run after delaySeconds.
		/// Returns a handle you can pass to Cancel.
		/// </summary>
		public static object Schedule(float delaySeconds, Action callback)
		{
			if (callback == null) return null;
			var item = new ScheduledItem
			{
				RemainingSeconds = Math.Max(0f, delaySeconds),
				Callback = callback,
				Cancelled = false
			};
			_toAdd.Enqueue(item);
			return item;
		}

		/// <summary>
		/// Cancel a scheduled action.
		/// </summary>
		public static void Cancel(object handle)
		{
			if (handle is ScheduledItem item)
			{
				item.Cancelled = true;
			}
		}

		/// <summary>
		/// Clear all scheduled actions.
		/// </summary>
		public static void Clear()
		{
			_items.Clear();
			while (_toAdd.Count > 0) _toAdd.Dequeue();
		}

		/// <summary>
		/// Advance the scheduler by deltaSeconds; run any actions that expire.
		/// </summary>
		public static void Update(float deltaSeconds)
		{
			// Add pending items (avoids modifying the list during enumeration)
			while (_toAdd.Count > 0)
			{
				_items.Add(_toAdd.Dequeue());
			}

			if (_items.Count == 0) return;

			float dt = Math.Max(0f, deltaSeconds);
			for (int i = _items.Count - 1; i >= 0; i--)
			{
				var item = _items[i];
				if (item.Cancelled)
				{
					_items.RemoveAt(i);
					continue;
				}

				item.RemainingSeconds -= dt;
				if (item.RemainingSeconds <= 0f)
				{
					_items.RemoveAt(i);
					try
					{
						item.Callback?.Invoke();
					}
					catch (Exception)
					{
						// Swallow exceptions to avoid breaking the scheduler loop.
						// Consider logging if you have a logging system.
					}
				}
			}
		}
	}
}


