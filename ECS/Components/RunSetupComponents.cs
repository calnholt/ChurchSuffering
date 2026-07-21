using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.RunSetup;

namespace Crusaders30XX.ECS.Components
{
	public sealed class RunSetup : IComponent
	{
		private int _selectedPenanceLevel;

		public Entity Owner { get; set; }
		public StartingWeapon SelectedWeapon { get; set; } = StartingWeapon.Sword;
		public int SelectedPenanceLevel
		{
			get => _selectedPenanceLevel;
			set => _selectedPenanceLevel = PenanceRules.ClampLevel(value);
		}

		public string WeaponId => PenanceRules.GetWeaponId(SelectedWeapon);
		public PenanceCalculation Penance => PenanceRules.Calculate(SelectedPenanceLevel);
	}
}
