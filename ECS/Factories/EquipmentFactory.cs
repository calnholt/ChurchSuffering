using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Objects.Equipment;

namespace ChurchSuffering.ECS.Factories
{
    public static class EquipmentFactory
    {
        private static readonly IReadOnlyDictionary<EquipmentId, Func<EquipmentBase>> EquipmentConstructors =
            new Dictionary<EquipmentId, Func<EquipmentBase>>
            {
                { EquipmentId.BulwarkPlate, () => new BulwarkPlate() },
                { EquipmentId.FleetfootGreaves, () => new FleetfootGreaves() },
                { EquipmentId.HeartforgeCuirass, () => new HeartforgeCuirass() },
                { EquipmentId.HelmOfSeeing, () => new HelmOfSeeing() },
                { EquipmentId.IvoryCoif, () => new IvoryCoif() },
                { EquipmentId.IvoryTreads, () => new IvoryTreads() },
                { EquipmentId.IvoryVest, () => new IvoryVest() },
                { EquipmentId.IvoryWraps, () => new IvoryWraps() },
                { EquipmentId.KnightlyChest, () => new KnightlyChest() },
                { EquipmentId.KnightlyGrieves, () => new KnightlyGrieves() },
                { EquipmentId.KnightlyGauntlets, () => new KnightlyGauntlets() },
                { EquipmentId.KnightlyHelm, () => new KnightlyHelm() },
                { EquipmentId.KunaiSheath, () => new KunaiSheath() },
                { EquipmentId.OathbreakerCoif, () => new OathbreakerCoif() },
                { EquipmentId.PiercedHeartPlate, () => new PiercedHeartPlate() },
                { EquipmentId.PurgingBracers, () => new PurgingBracers() },
                { EquipmentId.SanctifiedCirclet, () => new SanctifiedCirclet() },
                { EquipmentId.ScarletCoif, () => new ScarletCoif() },
                { EquipmentId.ScarletTreads, () => new ScarletTreads() },
                { EquipmentId.ScarletVest, () => new ScarletVest() },
                { EquipmentId.ScarletWraps, () => new ScarletWraps() },
                { EquipmentId.SunderstepTreads, () => new SunderstepTreads() },
                { EquipmentId.WarbringerBracers, () => new WarbringerBracers() },
                { EquipmentId.WhetstoneGauntlets, () => new WhetstoneGauntlets() },
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
