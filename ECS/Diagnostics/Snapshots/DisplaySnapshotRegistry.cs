using System;
using System.Collections.Generic;
using Crusaders30XX.Diagnostics.Snapshots.Fixtures;

namespace Crusaders30XX.Diagnostics.Snapshots
{
    public static class DisplaySnapshotRegistry
    {
        private static readonly Dictionary<string, IDisplaySnapshotFixture> Fixtures = new(StringComparer.OrdinalIgnoreCase);

        static DisplaySnapshotRegistry()
        {
            Register(new CardDisplaySnapshotFixture());
            Register(new BrittleCardSnapshotFixture());
            Register(new FrozenCardSnapshotFixture());
            Register(new ThornedCardSnapshotFixture());
            Register(new ScorchedCardSnapshotFixture());
            Register(new CursedCardSnapshotFixture());
            Register(new PoisonCardSnapshotFixture());
            Register(new CardRenderPipelineSnapshotFixture());
            Register(new ColorlessCardSnapshotFixture());
			Register(new DualColorCardSnapshotFixture());
            Register(new QuestRewardModalSnapshotFixture());
            Register(new ModularFxSnapshotFixture());
			Register(new PassiveApplicationSnapshotFixture());
            Register(new NarrativeEventModalSnapshotFixture());
            Register(new WayStationSnapshotFixture());
            Register(new PlayerHudSnapshotFixture());
            Register(new EquipmentTooltipSnapshotFixture());
			Register(new EnemyDamageMeterSnapshotFixture());
			Register(new EnemyAttackBannerSnapshotFixture());
			Register(new AssignedBlockRailSnapshotFixture());
			Register(new EnemyDefeatBurstSnapshotFixture());
			Register(new GuardianAngelSnapshotFixture());
			Register(new PauseMenuSnapshotFixture());
			Register(new HotKeySnapshotFixture());
			Register(new BattlePhaseTransitionSnapshotFixture());
			Register(new AchievementSnapshotFixture(AchievementSnapshotVariant.Overview));
			Register(new AchievementSnapshotFixture(AchievementSnapshotVariant.Detail));
			Register(new BoosterPackOpeningSnapshotFixture());
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.NoEvents));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.HazardEvent));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.CharacterEvent));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.HazardHoverPreview));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.CharacterHoverPreview));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.HazardConfirmation));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.CharacterSummary));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.CharacterDialog));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.ActiveEvents));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.HoverPreview));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.MedalTooltipHover));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.SoldShopSlot));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.EncounterRewardModal));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.ReplacementModal));
			Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.InventoryOverlay));
			Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.InventoryEquipmentTooltip));
			Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.CardListTop));
			Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.CardListMiddle));
			Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.CardListBottom));
			Register(new ClimbHeaderSnapshotFixture());
			Register(new ClimbResourceAcquisitionSnapshotFixture());
        }

        public static void Register(IDisplaySnapshotFixture fixture)
        {
            Fixtures[fixture.Id] = fixture;
        }

        public static bool TryGet(string fixtureId, out IDisplaySnapshotFixture fixture)
        {
            return Fixtures.TryGetValue(fixtureId, out fixture);
        }
    }
}
