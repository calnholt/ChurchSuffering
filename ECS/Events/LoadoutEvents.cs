namespace Crusaders30XX.ECS.Events
{
	public class LoadoutCardAdded
	{
		public string LoadoutId;
		public string EntryId;
		public string CardKey;
	}

	public class LoadoutCardRemoved
	{
		public string LoadoutId;
		public string EntryId;
		public string CardKey;
	}
}
