using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Events
{
	/// <summary>
	/// Fired when an ambush attack's timer expires, just before auto-confirming the enemy attack.
	/// Carries the active attack sequence so listeners can verify the attack is still current.
	/// </summary>
	public class AmbushTimerExpired
	{
		public int AttackSequence { get; set; }
	}
}


