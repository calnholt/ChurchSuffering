using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Services
{
	public class GoldManagementService
	{
		private readonly System.Action<ModifyGoldRequestEvent> _onModifyGoldRequestHandler;

		public GoldManagementService()
		{
			_onModifyGoldRequestHandler = OnModifyGoldRequest;
			EventManager.Subscribe(_onModifyGoldRequestHandler);
		}

		private void OnModifyGoldRequest(ModifyGoldRequestEvent evt)
		{
			if (evt == null || evt.Delta <= 0) return;

			int oldGold = 0;
			try { oldGold = SaveCache.GetGold(); } catch { oldGold = 0; }

			SaveCache.AddGold(evt.Delta);

			int newGold = oldGold + evt.Delta;
			try { newGold = SaveCache.GetGold(); } catch { }

			EventManager.Publish(new GoldChanged
			{
				OldGold = oldGold,
				NewGold = newGold,
				Delta = newGold - oldGold,
				Reason = evt.Reason ?? string.Empty
			});
		}

		public void Dispose()
		{
			try { EventManager.Unsubscribe(_onModifyGoldRequestHandler); } catch { }
		}
	}
}
