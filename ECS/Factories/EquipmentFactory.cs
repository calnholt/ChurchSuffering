using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Objects.Equipment;

namespace Crusaders30XX.ECS.Factories
{
    public static class EquipmentFactory
    {
        private static readonly IReadOnlyDictionary<EquipmentId, Func<EquipmentBase>> EquipmentConstructors =
            new Dictionary<EquipmentId, Func<EquipmentBase>>
            {
                { EquipmentId.CrimsonCuirass, () => new CrimsonCuirass() },
                { EquipmentId.CrimsonGreathelm, () => new CrimsonGreathelm() },
                { EquipmentId.CrimsonGreaves, () => new CrimsonGreaves() },
                { EquipmentId.CrimsonVambraces, () => new CrimsonVambraces() },
                { EquipmentId.HelmOfSeeing, () => new HelmOfSeeing() },
                { EquipmentId.IvoryCoif, () => new IvoryCoif() },
                { EquipmentId.IvoryTreads, () => new IvoryTreads() },
                { EquipmentId.IvoryVest, () => new IvoryVest() },
                { EquipmentId.IvoryWraps, () => new IvoryWraps() },
                { EquipmentId.KnightlyChest, () => new KnightlyChest() },
                { EquipmentId.KnightlyGrieves, () => new KnightlyGrieves() },
                { EquipmentId.KnightlyGauntlets, () => new KnightlyGauntlets() },
                { EquipmentId.KnightlyHelm, () => new KnightlyHelm() },
                { EquipmentId.PaleCuirass, () => new PaleCuirass() },
                { EquipmentId.PaleGreathelm, () => new PaleGreathelm() },
                { EquipmentId.PaleGreaves, () => new PaleGreaves() },
                { EquipmentId.PaleVambraces, () => new PaleVambraces() },
                { EquipmentId.PiercedHeartPlate, () => new PiercedHeartPlate() },
                { EquipmentId.PurgingBracers, () => new PurgingBracers() },
                { EquipmentId.ScarletCoif, () => new ScarletCoif() },
                { EquipmentId.ScarletTreads, () => new ScarletTreads() },
                { EquipmentId.ScarletVest, () => new ScarletVest() },
                { EquipmentId.ScarletWraps, () => new ScarletWraps() },
            };

        public static EquipmentBase Create(EquipmentId equipmentId)
        {
            return EquipmentConstructors.TryGetValue(equipmentId, out var create)
                ? create()
                : null;
        }

        public static EquipmentBase Create(string equipmentId)
        {
            return GameIdExtensions.TryParseEquipmentId(equipmentId, out var parsed)
                ? Create(parsed)
                : null;
        }

        public static Dictionary<EquipmentId, EquipmentBase> GetAllEquipment()
        {
            return EquipmentConstructors.ToDictionary(
                entry => entry.Key,
                entry => entry.Value());
        }
    }
}
