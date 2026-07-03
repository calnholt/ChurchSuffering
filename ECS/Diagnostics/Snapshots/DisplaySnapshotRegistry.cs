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
            Register(new ColorlessCardSnapshotFixture());
            Register(new QuestRewardModalSnapshotFixture());
            Register(new ModularFxSnapshotFixture());
            Register(new NarrativeEventModalSnapshotFixture());
            Register(new WayStationSnapshotFixture());
            Register(new PlayerHudSnapshotFixture());
            Register(new EquipmentTooltipSnapshotFixture());
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
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.SoldShopSlot));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.EncounterRewardModal));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.ReplacementModal));
            Register(new ClimbSnapshotFixture(ClimbSnapshotVariant.InventoryOverlay));
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
